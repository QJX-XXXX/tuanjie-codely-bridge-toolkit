using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.PackageManager;

namespace QJX.CodexTuanjieBridge.Editor
{
    public sealed class SetupStatus
    {
        public TuanjieProjectStatus Project { get; set; }
        public bool BridgeInstalled { get; set; }
        public string BridgeVersion { get; set; }
        public bool DescriptorExists { get; set; }
        public CodelyCliResolution CodelyCli { get; set; }
        public string Error { get; set; }

        public bool CanConfigureClient
        {
            get
            {
                return Project != null &&
                       Project.CanWriteConfig &&
                       BridgeInstalled &&
                       CodelyCli != null &&
                       CodelyCli.Found;
            }
        }

        public static SetupStatus Collect(
            string projectRoot,
            string editorApplicationPath,
            string configuredCliPath)
        {
            var status = new SetupStatus
            {
                BridgeVersion = string.Empty,
                Error = string.Empty
            };
            status.Project = TuanjieProjectDetector.Detect(
                projectRoot,
                editorApplicationPath);

            try
            {
                UnityEditor.PackageManager.PackageInfo[] packages =
                    UnityEditor.PackageManager.PackageInfo.GetAllRegisteredPackages();
                for (int index = 0; packages != null && index < packages.Length; index++)
                {
                    if (packages[index] != null &&
                        string.Equals(
                            packages[index].name,
                            "cn.tuanjie.codely.bridge",
                            StringComparison.Ordinal))
                    {
                        status.BridgeInstalled = true;
                        status.BridgeVersion = packages[index].version;
                        break;
                    }
                }
            }
            catch (Exception exception)
            {
                status.Error = "读取已注册包列表失败：" + exception.Message;
            }

            string root = projectRoot ?? string.Empty;
            status.DescriptorExists = File.Exists(
                Path.Combine(root, ".com-unity-codely.json"));
            IReadOnlyList<string> environmentPaths =
                CodelyCliEnvironmentReader.ReadValues("CODELY_CLI_PATH");
            status.CodelyCli = CodelyCliLocator.Resolve(
                configuredCliPath,
                environmentPaths,
                File.Exists,
                FindCodelyCliOnPath);
            if (!status.CodelyCli.Found && string.IsNullOrEmpty(status.Error))
            {
                status.Error = status.CodelyCli.Error;
            }
            if (status.Project != null &&
                !status.Project.CanWriteConfig &&
                string.IsNullOrEmpty(status.Error))
            {
                status.Error = status.Project.Error;
            }
            return status;
        }

        private static IReadOnlyList<string> FindCodelyCliOnPath()
        {
            return CodelyCliEnvironmentReader.FindExecutablesOnPath(
                CodelyCliEnvironmentReader.ReadValues("PATH"),
                "codely.cmd",
                File.Exists,
                Environment.ExpandEnvironmentVariables);
        }
    }

    internal static class CodelyCliVersionProbe
    {
        internal static Task<string> ReadVersionAsync(
            string cliPath,
            TimeSpan timeout)
        {
            return Task.Run(() =>
            {
                if (string.IsNullOrWhiteSpace(cliPath))
                {
                    return "未设置 CodelyCLI 路径。";
                }
                using (var process = new Process())
                {
                    process.StartInfo = new ProcessStartInfo
                    {
                        FileName = "cmd.exe",
                        Arguments = "/c \"" + cliPath + "\" --version",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    };
                    try
                    {
                        process.Start();
                        if (!process.WaitForExit((int)timeout.TotalMilliseconds))
                        {
                            try
                            {
                                process.Kill();
                            }
                            catch
                            {
                                // 只结束本次探测创建的子进程，不能影响 Editor。
                            }
                            return "读取 CodelyCLI 版本超时。";
                        }
                        string output = process.StandardOutput.ReadToEnd().Trim();
                        string error = process.StandardError.ReadToEnd().Trim();
                        if (process.ExitCode != 0)
                        {
                            return string.IsNullOrEmpty(error)
                                ? "CodelyCLI 版本查询失败。"
                                : error;
                        }
                        return string.IsNullOrEmpty(output)
                            ? "CodelyCLI 未返回版本信息。"
                            : output;
                    }
                    catch (Exception exception)
                    {
                        return "读取 CodelyCLI 版本失败：" + exception.Message;
                    }
                }
            });
        }
    }
}
