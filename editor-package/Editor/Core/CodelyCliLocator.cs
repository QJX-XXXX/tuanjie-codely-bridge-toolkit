using System;
using System.Collections.Generic;
using System.IO;

namespace QJX.CodexTuanjieBridge.Editor
{
    public sealed class CodelyCliResolution
    {
        public bool Found { get; set; }
        public string Path { get; set; }
        public string Source { get; set; }
        public string Error { get; set; }
    }

    public static class CodelyCliLocator
    {
        public static CodelyCliResolution Resolve(
            string configuredPath,
            string environmentPath,
            Func<string, bool> fileExists,
            Func<IReadOnlyList<string>> findOnPath)
        {
            return Resolve(
                configuredPath,
                string.IsNullOrWhiteSpace(environmentPath)
                    ? new string[0]
                    : new[] { environmentPath },
                fileExists,
                findOnPath);
        }

        public static CodelyCliResolution Resolve(
            string configuredPath,
            IReadOnlyList<string> environmentPaths,
            Func<string, bool> fileExists,
            Func<IReadOnlyList<string>> findOnPath)
        {
            if (fileExists == null)
            {
                throw new ArgumentNullException("fileExists");
            }
            if (findOnPath == null)
            {
                throw new ArgumentNullException("findOnPath");
            }

            CodelyCliResolution configured = TryResolve(
                configuredPath,
                "EditorPrefs",
                fileExists);
            if (configured.Found)
            {
                return configured;
            }

            for (int index = 0;
                 environmentPaths != null && index < environmentPaths.Count;
                 index++)
            {
                CodelyCliResolution environment = TryResolve(
                    environmentPaths[index],
                    "CODELY_CLI_PATH",
                    fileExists);
                if (environment.Found)
                {
                    return environment;
                }
            }

            IReadOnlyList<string> pathCandidates;
            try
            {
                pathCandidates = findOnPath();
            }
            catch (Exception exception)
            {
                return Missing("查询 PATH 中的 CodelyCLI 失败：" + exception.Message);
            }

            if (pathCandidates != null)
            {
                for (int index = 0; index < pathCandidates.Count; index++)
                {
                    CodelyCliResolution pathResult = TryResolve(
                        pathCandidates[index],
                        "PATH",
                        fileExists);
                    if (pathResult.Found)
                    {
                        return pathResult;
                    }
                }
            }

            return Missing(
                "未找到 CodelyCLI，已检查 EditorPrefs、最新的 CODELY_CLI_PATH 和 PATH；" +
                "可点击“选择 CodelyCLI”指定 codely.cmd。");
        }

        private static CodelyCliResolution TryResolve(
            string candidate,
            string source,
            Func<string, bool> fileExists)
        {
            if (string.IsNullOrWhiteSpace(candidate))
            {
                return Missing(string.Empty);
            }

            string fullPath;
            try
            {
                fullPath = System.IO.Path.GetFullPath(
                    candidate.Trim().Trim('"'));
            }
            catch
            {
                return Missing(string.Empty);
            }

            bool exists;
            try
            {
                exists = fileExists(fullPath);
            }
            catch
            {
                exists = false;
            }
            if (!exists)
            {
                return Missing(string.Empty);
            }

            return new CodelyCliResolution
            {
                Found = true,
                Path = fullPath,
                Source = source,
                Error = string.Empty
            };
        }

        private static CodelyCliResolution Missing(string error)
        {
            return new CodelyCliResolution
            {
                Found = false,
                Path = string.Empty,
                Source = string.Empty,
                Error = error
            };
        }
    }

    internal static class CodelyCliEnvironmentReader
    {
        internal static IReadOnlyList<string> ReadValues(string variableName)
        {
            var values = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            AddValue(
                values,
                seen,
                Environment.GetEnvironmentVariable(variableName));
            AddTargetValue(values, seen, variableName, EnvironmentVariableTarget.User);
            AddTargetValue(values, seen, variableName, EnvironmentVariableTarget.Machine);
            return values;
        }

        internal static IReadOnlyList<string> FindExecutablesOnPath(
            IReadOnlyList<string> pathValues,
            string executableName,
            Func<string, bool> fileExists,
            Func<string, string> expandEnvironmentVariables)
        {
            if (fileExists == null)
            {
                throw new ArgumentNullException("fileExists");
            }
            if (expandEnvironmentVariables == null)
            {
                throw new ArgumentNullException("expandEnvironmentVariables");
            }

            var candidates = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int valueIndex = 0;
                 pathValues != null && valueIndex < pathValues.Count;
                 valueIndex++)
            {
                string pathValue;
                try
                {
                    pathValue = expandEnvironmentVariables(pathValues[valueIndex]);
                }
                catch
                {
                    continue;
                }
                if (string.IsNullOrWhiteSpace(pathValue))
                {
                    continue;
                }

                string[] directories = pathValue.Split(
                    new[] { Path.PathSeparator },
                    StringSplitOptions.RemoveEmptyEntries);
                for (int directoryIndex = 0;
                     directoryIndex < directories.Length;
                     directoryIndex++)
                {
                    string directory = directories[directoryIndex]
                        .Trim()
                        .Trim('"');
                    if (directory.Length == 0)
                    {
                        continue;
                    }

                    string candidate;
                    try
                    {
                        candidate = Path.GetFullPath(
                            Path.Combine(directory, executableName));
                    }
                    catch
                    {
                        continue;
                    }
                    bool exists;
                    try
                    {
                        exists = fileExists(candidate);
                    }
                    catch
                    {
                        exists = false;
                    }
                    if (exists && seen.Add(candidate))
                    {
                        candidates.Add(candidate);
                    }
                }
            }
            return candidates;
        }

        private static void AddTargetValue(
            ICollection<string> values,
            ISet<string> seen,
            string variableName,
            EnvironmentVariableTarget target)
        {
            try
            {
                AddValue(
                    values,
                    seen,
                    Environment.GetEnvironmentVariable(variableName, target));
            }
            catch
            {
                // 某些运行环境不支持读取指定作用域，继续检查其他作用域。
            }
        }

        private static void AddValue(
            ICollection<string> values,
            ISet<string> seen,
            string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return;
            }
            string expanded;
            try
            {
                expanded = Environment.ExpandEnvironmentVariables(value).Trim();
            }
            catch
            {
                expanded = value.Trim();
            }
            if (expanded.Length > 0 && seen.Add(expanded))
            {
                values.Add(expanded);
            }
        }
    }
}
