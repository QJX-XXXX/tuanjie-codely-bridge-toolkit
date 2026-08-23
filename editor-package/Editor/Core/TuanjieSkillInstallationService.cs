using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace QJX.CodexTuanjieBridge.Editor
{
    public sealed class SkillInstallationResult
    {
        public bool Success { get; set; }
        public int Added { get; set; }
        public int Updated { get; set; }
        public int Unchanged { get; set; }
        public string InstallRoot { get; set; }
        public string CommitSha { get; set; }
        public string Warning { get; set; }
        public string Error { get; set; }
    }

    public static class TuanjieSkillInstallationService
    {
        public const string OwnershipMarkerName = ".tuanjie-codely-skill-sync";

        private const string RepositoryOwner = "QJX-XXXX";
        private const string RepositoryName = "tuanjie-codely-bridge-toolkit";
        private const string RepositoryBranch = "main";
        private const string UserAgent = "TuanjieCodelySkillInstaller/1.0";

        private static readonly IReadOnlyList<string> SupportedSkillNames =
            new[]
            {
                "tuanjie-workflows",
                "tuanjie-codely-bridge",
                "tuanjie-editor-automation",
                "tuanjie-package-management",
                "tuanjie-codely-custom-tools"
            };

        public static IReadOnlyList<string> SkillNames
        {
            get { return SupportedSkillNames; }
        }

        public static void InstallOrUpdateAsync(
            string installRoot,
            Action<SkillInstallationResult> onComplete)
        {
            Task.Run(() => RunInstallation(installRoot)).ContinueWith(task =>
            {
                SkillInstallationResult result;
                if (task.Status == TaskStatus.RanToCompletion)
                {
                    result = task.Result;
                }
                else
                {
                    Exception exception = task.Exception == null
                        ? null
                        : task.Exception.GetBaseException();
                    result = new SkillInstallationResult
                    {
                        Success = false,
                        InstallRoot = installRoot,
                        Error = exception == null
                            ? "Skill 安装任务未正常完成。"
                            : exception.Message
                    };
                }

                EditorApplication.delayCall += () =>
                {
                    if (onComplete != null)
                    {
                        onComplete(result);
                    }
                };
            });
        }

        public static int CountInstalledSkills(string installRoot)
        {
            if (string.IsNullOrWhiteSpace(installRoot))
            {
                return 0;
            }

            int count = 0;
            for (int index = 0; index < SupportedSkillNames.Count; index++)
            {
                string skillPath = Path.Combine(
                    installRoot,
                    SupportedSkillNames[index],
                    "SKILL.md");
                if (File.Exists(skillPath))
                {
                    count++;
                }
            }
            return count;
        }

        private static SkillInstallationResult RunInstallation(string installRoot)
        {
            string normalizedRoot = ResolveInstallRoot(installRoot);
            RemoteSnapshot snapshot = FetchRemoteSnapshot();
            string transactionId = Guid.NewGuid().ToString("N");
            string stagingRoot = normalizedRoot + ".tuanjie-staging-" + transactionId;
            string backupRoot = normalizedRoot + ".tuanjie-backup-" + transactionId;
            List<AppliedSkillChange> applied = new List<AppliedSkillChange>();
            int added = 0;
            int updated = 0;
            int unchanged = 0;
            bool preservedLegacyBackup = false;

            try
            {
                StageAndVerifySnapshot(snapshot, stagingRoot);
                PreflightTargets(normalizedRoot, snapshot);
                Directory.CreateDirectory(normalizedRoot);

                for (int index = 0; index < SupportedSkillNames.Count; index++)
                {
                    string skillName = SupportedSkillNames[index];
                    string targetPath = ResolvePathUnderRoot(normalizedRoot, skillName);
                    string stagedPath = ResolvePathUnderRoot(stagingRoot, skillName);
                    Dictionary<string, string> remoteFiles = snapshot.FilesBySkill[skillName];
                    bool targetExists = Directory.Exists(targetPath);
                    bool owned = targetExists && File.Exists(
                        Path.Combine(targetPath, OwnershipMarkerName));
                    bool legacySkill = targetExists && !owned &&
                        IsLegacySkillDirectory(targetPath, skillName);

                    if (owned && DirectoryMatchesSnapshot(targetPath, remoteFiles))
                    {
                        Directory.Delete(stagedPath, true);
                        unchanged++;
                        continue;
                    }

                    string backupPath = ResolvePathUnderRoot(backupRoot, skillName);
                    if (targetExists)
                    {
                        Directory.CreateDirectory(backupRoot);
                        Directory.Move(targetPath, backupPath);
                        preservedLegacyBackup = preservedLegacyBackup || legacySkill;
                    }

                    try
                    {
                        Directory.Move(stagedPath, targetPath);
                    }
                    catch
                    {
                        if (targetExists && Directory.Exists(backupPath) &&
                            !Directory.Exists(targetPath))
                        {
                            Directory.Move(backupPath, targetPath);
                        }
                        throw;
                    }

                    applied.Add(new AppliedSkillChange
                    {
                        TargetPath = targetPath,
                        BackupPath = backupPath,
                        HadTarget = targetExists
                    });
                    if (targetExists)
                    {
                        updated++;
                    }
                    else
                    {
                        added++;
                    }
                }

                string cleanupWarning = preservedLegacyBackup
                    ? "旧版 Skill 已保留备份：" + backupRoot
                    : TryDeleteDirectory(backupRoot);
                TryDeleteDirectory(stagingRoot);
                return new SkillInstallationResult
                {
                    Success = true,
                    Added = added,
                    Updated = updated,
                    Unchanged = unchanged,
                    InstallRoot = normalizedRoot,
                    CommitSha = snapshot.CommitSha,
                    Warning = cleanupWarning
                };
            }
            catch (Exception exception)
            {
                string rollbackError = Rollback(applied);
                TryDeleteDirectory(stagingRoot);
                TryDeleteDirectory(backupRoot);
                return new SkillInstallationResult
                {
                    Success = false,
                    Added = added,
                    Updated = updated,
                    Unchanged = unchanged,
                    InstallRoot = normalizedRoot,
                    CommitSha = snapshot == null ? string.Empty : snapshot.CommitSha,
                    Error = string.IsNullOrEmpty(rollbackError)
                        ? exception.Message
                        : exception.Message + "\n回滚也遇到问题：" + rollbackError
                };
            }
        }

        private static string ResolveInstallRoot(string installRoot)
        {
            if (string.IsNullOrWhiteSpace(installRoot))
            {
                throw new InvalidOperationException("Skill 安装目录不能为空。");
            }

            string fullPath = Path.GetFullPath(installRoot.Trim());
            string root = Path.GetPathRoot(fullPath);
            if (string.Equals(
                    fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                    root == null
                        ? string.Empty
                        : root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("不能把文件系统根目录作为 Skill 安装目录。");
            }
            return fullPath.TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar);
        }

        private static RemoteSnapshot FetchRemoteSnapshot()
        {
            using (HttpClient client = CreateGitHubClient())
            {
                string branchUrl = string.Format(
                    "https://api.github.com/repos/{0}/{1}/branches/{2}",
                    Uri.EscapeDataString(RepositoryOwner),
                    Uri.EscapeDataString(RepositoryName),
                    Uri.EscapeDataString(RepositoryBranch));
                GitHubBranchResponse branch = JsonUtility.FromJson<GitHubBranchResponse>(
                    DownloadString(client, branchUrl));
                string commitSha = branch == null || branch.commit == null
                    ? string.Empty
                    : branch.commit.sha;
                if (string.IsNullOrWhiteSpace(commitSha))
                {
                    throw new InvalidOperationException("无法解析 GitHub main 分支提交。");
                }

                string treeUrl = string.Format(
                    "https://api.github.com/repos/{0}/{1}/git/trees/{2}?recursive=1",
                    Uri.EscapeDataString(RepositoryOwner),
                    Uri.EscapeDataString(RepositoryName),
                    Uri.EscapeDataString(commitSha.Trim()));
                GitHubTreeResponse tree = JsonUtility.FromJson<GitHubTreeResponse>(
                    DownloadString(client, treeUrl));
                if (tree == null || tree.tree == null)
                {
                    throw new InvalidOperationException("无法解析 GitHub 文件树。");
                }
                if (tree.truncated)
                {
                    throw new InvalidOperationException(
                        "GitHub 返回了不完整文件树，已停止安装以避免删除有效文件。");
                }

                Dictionary<string, Dictionary<string, string>> filesBySkill =
                    new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);
                for (int index = 0; index < SupportedSkillNames.Count; index++)
                {
                    filesBySkill.Add(
                        SupportedSkillNames[index],
                        new Dictionary<string, string>(StringComparer.Ordinal));
                }

                for (int index = 0; index < tree.tree.Length; index++)
                {
                    GitHubTreeEntry entry = tree.tree[index];
                    if (entry == null || !string.Equals(
                            entry.type,
                            "blob",
                            StringComparison.Ordinal))
                    {
                        continue;
                    }

                    for (int skillIndex = 0;
                         skillIndex < SupportedSkillNames.Count;
                         skillIndex++)
                    {
                        string skillName = SupportedSkillNames[skillIndex];
                        string prefix = "skills/" + skillName + "/";
                        string remotePath = NormalizeRemotePath(entry.path);
                        if (!remotePath.StartsWith(prefix, StringComparison.Ordinal))
                        {
                            continue;
                        }

                        string relativePath = remotePath.Substring(prefix.Length);
                        string normalizedRelative;
                        if (!TryNormalizeRelativePath(
                                relativePath,
                                out normalizedRelative))
                        {
                            throw new InvalidOperationException(
                                "远端 Skill 包含不安全路径：" + remotePath);
                        }
                        filesBySkill[skillName][normalizedRelative] =
                            (entry.sha ?? string.Empty).Trim().ToLowerInvariant();
                        break;
                    }
                }

                for (int index = 0; index < SupportedSkillNames.Count; index++)
                {
                    string skillName = SupportedSkillNames[index];
                    Dictionary<string, string> files = filesBySkill[skillName];
                    if (!files.ContainsKey("SKILL.md"))
                    {
                        throw new InvalidOperationException(
                            "远端 Skill 缺少 SKILL.md：" + skillName);
                    }
                }

                return new RemoteSnapshot
                {
                    CommitSha = commitSha.Trim(),
                    FilesBySkill = filesBySkill
                };
            }
        }

        private static void StageAndVerifySnapshot(
            RemoteSnapshot snapshot,
            string stagingRoot)
        {
            Directory.CreateDirectory(stagingRoot);
            using (HttpClient client = CreateGitHubClient())
            {
                for (int index = 0; index < SupportedSkillNames.Count; index++)
                {
                    string skillName = SupportedSkillNames[index];
                    Dictionary<string, string> remoteFiles =
                        snapshot.FilesBySkill[skillName];
                    foreach (KeyValuePair<string, string> file in remoteFiles)
                    {
                        string remotePath = "skills/" + skillName + "/" + file.Key;
                        string targetPath = ResolvePathUnderRoot(
                            stagingRoot,
                            skillName + "/" + file.Key);
                        string directory = Path.GetDirectoryName(targetPath);
                        if (!string.IsNullOrEmpty(directory))
                        {
                            Directory.CreateDirectory(directory);
                        }

                        byte[] bytes = DownloadBytes(
                            client,
                            BuildRawFileUrl(snapshot.CommitSha, remotePath));
                        string actualHash = ComputeGitBlobSha1(bytes);
                        if (!string.Equals(
                                actualHash,
                                file.Value,
                                StringComparison.Ordinal))
                        {
                            throw new InvalidOperationException(
                                "下载文件校验失败：" + remotePath);
                        }
                        File.WriteAllBytes(targetPath, bytes);
                    }

                    string markerPath = ResolvePathUnderRoot(
                        stagingRoot,
                        skillName + "/" + OwnershipMarkerName);
                    File.WriteAllText(
                        markerPath,
                        RepositoryOwner + "/" + RepositoryName + "@" +
                        snapshot.CommitSha,
                        new UTF8Encoding(false));
                }
            }
        }

        private static void PreflightTargets(
            string installRoot,
            RemoteSnapshot snapshot)
        {
            for (int index = 0; index < SupportedSkillNames.Count; index++)
            {
                string skillName = SupportedSkillNames[index];
                string targetPath = ResolvePathUnderRoot(installRoot, skillName);
                string error;
                if (!CanManageExistingDirectory(
                        targetPath,
                        new HashSet<string>(
                            snapshot.FilesBySkill[skillName].Keys,
                            StringComparer.OrdinalIgnoreCase),
                        skillName,
                        out error))
                {
                    throw new InvalidOperationException(error);
                }
            }
        }

        public static bool CanManageExistingDirectory(
            string targetPath,
            ICollection<string> remoteRelativePaths,
            out string error)
        {
            return CanManageExistingDirectory(
                targetPath,
                remoteRelativePaths,
                string.Empty,
                out error);
        }

        internal static bool CanManageExistingDirectory(
            string targetPath,
            ICollection<string> remoteRelativePaths,
            string expectedSkillName,
            out string error)
        {
            error = string.Empty;
            if (!Directory.Exists(targetPath))
            {
                return true;
            }
            if (File.Exists(Path.Combine(targetPath, OwnershipMarkerName)))
            {
                return true;
            }

            string[] files = Directory.GetFiles(
                targetPath,
                "*",
                SearchOption.AllDirectories);
            if (files.Length == 0)
            {
                return true;
            }

            HashSet<string> remote = new HashSet<string>(
                remoteRelativePaths,
                StringComparer.OrdinalIgnoreCase);
            bool legacySkill = !string.IsNullOrWhiteSpace(expectedSkillName) &&
                IsLegacySkillDirectory(targetPath, expectedSkillName);
            bool hasSkillDefinition = false;
            for (int index = 0; index < files.Length; index++)
            {
                string relativePath = Path.GetRelativePath(targetPath, files[index])
                    .Replace('\\', '/');
                if (string.Equals(
                        relativePath,
                        OwnershipMarkerName,
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                if (string.Equals(
                        relativePath,
                        "SKILL.md",
                        StringComparison.OrdinalIgnoreCase))
                {
                    hasSkillDefinition = true;
                }
                if (!remote.Contains(relativePath) &&
                    !(legacySkill && IsKnownLegacySkillPath(relativePath)))
                {
                    error = "目标 Skill 目录包含不属于已识别 Skill 内容的文件，已拒绝覆盖：" +
                            relativePath + "\n目录：" + targetPath;
                    return false;
                }
            }

            if (!hasSkillDefinition)
            {
                error = "目标目录不是可识别的 Skill，已拒绝接管：" + targetPath;
                return false;
            }
            return true;
        }

        private static bool IsLegacySkillDirectory(
            string targetPath,
            string expectedSkillName)
        {
            string definitionPath = Path.Combine(targetPath, "SKILL.md");
            if (!File.Exists(definitionPath) ||
                string.IsNullOrWhiteSpace(expectedSkillName))
            {
                return false;
            }

            try
            {
                string[] lines = File.ReadAllLines(
                    definitionPath,
                    new UTF8Encoding(false, true));
                for (int index = 0; index < lines.Length; index++)
                {
                    string line = lines[index].Trim();
                    if (!line.StartsWith("name:", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    string actualName = line.Substring("name:".Length).Trim();
                    return string.Equals(
                        actualName,
                        expectedSkillName,
                        StringComparison.OrdinalIgnoreCase);
                }
            }
            catch (Exception)
            {
                return false;
            }
            return false;
        }

        private static bool IsKnownLegacySkillPath(string relativePath)
        {
            if (string.Equals(
                    relativePath,
                    "SKILL.md",
                    StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            int separatorIndex = relativePath.IndexOf('/');
            if (separatorIndex <= 0)
            {
                return false;
            }

            string topLevelDirectory = relativePath.Substring(0, separatorIndex);
            switch (topLevelDirectory.ToLowerInvariant())
            {
                case "agents":
                case "references":
                case "scripts":
                case "assets":
                case "examples":
                case "templates":
                    return true;
                default:
                    return false;
            }
        }

        private static bool DirectoryMatchesSnapshot(
            string targetPath,
            IDictionary<string, string> remoteFiles)
        {
            Dictionary<string, string> localFiles = new Dictionary<string, string>(
                StringComparer.OrdinalIgnoreCase);
            string[] files = Directory.GetFiles(
                targetPath,
                "*",
                SearchOption.AllDirectories);
            for (int index = 0; index < files.Length; index++)
            {
                string relativePath = Path.GetRelativePath(targetPath, files[index])
                    .Replace('\\', '/');
                if (!string.Equals(
                        relativePath,
                        OwnershipMarkerName,
                        StringComparison.OrdinalIgnoreCase))
                {
                    localFiles[relativePath] = files[index];
                }
            }
            if (localFiles.Count != remoteFiles.Count)
            {
                return false;
            }

            foreach (KeyValuePair<string, string> remoteFile in remoteFiles)
            {
                string localPath;
                if (!localFiles.TryGetValue(remoteFile.Key, out localPath) ||
                    !string.Equals(
                        ComputeGitBlobSha1(File.ReadAllBytes(localPath)),
                        remoteFile.Value,
                        StringComparison.Ordinal))
                {
                    return false;
                }
            }
            return true;
        }

        public static string ResolvePathUnderRoot(string root, string relativePath)
        {
            string safeRelativePath;
            if (!TryNormalizeRelativePath(relativePath, out safeRelativePath))
            {
                throw new InvalidOperationException("不安全的相对路径：" + relativePath);
            }

            string fullRoot = Path.GetFullPath(root)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
                Path.DirectorySeparatorChar;
            string fullPath = Path.GetFullPath(Path.Combine(
                fullRoot,
                safeRelativePath.Replace('/', Path.DirectorySeparatorChar)));
            if (!fullPath.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("路径越过 Skill 安装目录：" + relativePath);
            }
            return fullPath;
        }

        private static bool TryNormalizeRelativePath(
            string relativePath,
            out string normalizedPath)
        {
            normalizedPath = NormalizeRemotePath(relativePath);
            if (string.IsNullOrWhiteSpace(normalizedPath) ||
                Path.IsPathRooted(normalizedPath))
            {
                return false;
            }

            string[] segments = normalizedPath.Split('/');
            for (int index = 0; index < segments.Length; index++)
            {
                string segment = segments[index];
                if (string.IsNullOrWhiteSpace(segment) ||
                    string.Equals(segment, ".", StringComparison.Ordinal) ||
                    string.Equals(segment, "..", StringComparison.Ordinal) ||
                    segment.IndexOf(':') >= 0)
                {
                    return false;
                }
            }
            normalizedPath = string.Join("/", segments);
            return true;
        }

        private static string NormalizeRemotePath(string path)
        {
            return string.IsNullOrWhiteSpace(path)
                ? string.Empty
                : path.Replace('\\', '/').Trim().Trim('/');
        }

        private static HttpClient CreateGitHubClient()
        {
            HttpClient client = new HttpClient();
            client.Timeout = TimeSpan.FromSeconds(60);
            client.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
            client.DefaultRequestHeaders.Accept.ParseAdd(
                "application/vnd.github+json");
            return client;
        }

        private static string DownloadString(HttpClient client, string url)
        {
            using (HttpResponseMessage response = client.GetAsync(url)
                       .GetAwaiter().GetResult())
            {
                string body = response.Content.ReadAsStringAsync()
                    .GetAwaiter().GetResult();
                if (!response.IsSuccessStatusCode)
                {
                    throw new InvalidOperationException(
                        "GitHub 请求失败：" + (int)response.StatusCode + " " +
                        response.ReasonPhrase);
                }
                return body;
            }
        }

        private static byte[] DownloadBytes(HttpClient client, string url)
        {
            using (HttpResponseMessage response = client.GetAsync(url)
                       .GetAwaiter().GetResult())
            {
                if (!response.IsSuccessStatusCode)
                {
                    throw new InvalidOperationException(
                        "Skill 文件下载失败：" + (int)response.StatusCode + " " +
                        response.ReasonPhrase);
                }
                return response.Content.ReadAsByteArrayAsync()
                    .GetAwaiter().GetResult();
            }
        }

        private static string BuildRawFileUrl(string commitSha, string remotePath)
        {
            string[] segments = NormalizeRemotePath(remotePath).Split('/');
            for (int index = 0; index < segments.Length; index++)
            {
                segments[index] = Uri.EscapeDataString(segments[index]);
            }
            return string.Format(
                "https://raw.githubusercontent.com/{0}/{1}/{2}/{3}",
                Uri.EscapeDataString(RepositoryOwner),
                Uri.EscapeDataString(RepositoryName),
                Uri.EscapeDataString(commitSha),
                string.Join("/", segments));
        }

        private static string ComputeGitBlobSha1(byte[] bytes)
        {
            byte[] header = Encoding.UTF8.GetBytes("blob " + bytes.Length + "\0");
            using (SHA1 sha1 = SHA1.Create())
            {
                sha1.TransformBlock(header, 0, header.Length, null, 0);
                sha1.TransformFinalBlock(bytes, 0, bytes.Length);
                return BitConverter.ToString(sha1.Hash ?? new byte[0])
                    .Replace("-", string.Empty)
                    .ToLowerInvariant();
            }
        }

        private static string Rollback(IList<AppliedSkillChange> applied)
        {
            List<string> errors = new List<string>();
            for (int index = applied.Count - 1; index >= 0; index--)
            {
                AppliedSkillChange change = applied[index];
                try
                {
                    if (Directory.Exists(change.TargetPath))
                    {
                        Directory.Delete(change.TargetPath, true);
                    }
                    if (change.HadTarget && Directory.Exists(change.BackupPath))
                    {
                        Directory.Move(change.BackupPath, change.TargetPath);
                    }
                }
                catch (Exception exception)
                {
                    errors.Add(change.TargetPath + "：" + exception.Message);
                }
            }
            return string.Join("；", errors.ToArray());
        }

        private static string TryDeleteDirectory(string path)
        {
            if (!Directory.Exists(path))
            {
                return string.Empty;
            }
            try
            {
                Directory.Delete(path, true);
                return string.Empty;
            }
            catch (Exception exception)
            {
                return "临时备份目录未能清理，可手动删除：" + path +
                       "（" + exception.Message + "）";
            }
        }

        // JsonUtility 通过字段反序列化 GitHub 响应；这些字段不会由 C# 代码直接赋值。
#pragma warning disable 0649
        [Serializable]
        private sealed class GitHubBranchResponse
        {
            public GitHubCommit commit;
        }

        [Serializable]
        private sealed class GitHubCommit
        {
            public string sha;
        }

        [Serializable]
        private sealed class GitHubTreeResponse
        {
            public GitHubTreeEntry[] tree;
            public bool truncated;
        }

        [Serializable]
        private sealed class GitHubTreeEntry
        {
            public string path;
            public string type;
            public string sha;
        }
#pragma warning restore 0649

        private sealed class RemoteSnapshot
        {
            public string CommitSha;
            public Dictionary<string, Dictionary<string, string>> FilesBySkill;
        }

        private sealed class AppliedSkillChange
        {
            public string TargetPath;
            public string BackupPath;
            public bool HadTarget;
        }
    }
}
