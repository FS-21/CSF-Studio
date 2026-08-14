using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace CsfStudio.Core
{
    public class LanguageInfo
    {
        public string FileName { get; set; }
        public string Name { get; set; }
        public string Author { get; set; }
        public string MapEncoding { get; set; }

        public override string ToString()
        {
            return !string.IsNullOrWhiteSpace(Name) ? Name : FileName;
        }
    }

    public static class LanguageManager
    {
        private static readonly Dictionary<string, string> _strings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private static string _currentLanguageFile = "en.txt";

        public static string CurrentLanguageFile => _currentLanguageFile;

        public static void Initialize(string languageFileName)
        {
            LoadLanguage(languageFileName);
        }

        public static string GetString(string key, string fallbackText)
        {
            if (string.IsNullOrWhiteSpace(key)) return fallbackText ?? string.Empty;
            if (_strings.TryGetValue(key.Trim(), out string val))
            {
                return val;
            }
            return fallbackText ?? string.Empty;
        }

        public static string GetStringFormat(string key, string fallbackFormat, params object[] args)
        {
            string fmt = GetString(key, fallbackFormat);
            if (args == null || args.Length == 0) return fmt;
            try
            {
                return string.Format(fmt, args);
            }
            catch
            {
                return string.Format(fallbackFormat ?? string.Empty, args);
            }
        }

        public static LanguageInfo ParseLanguageHeader(string filePath)
        {
            var info = new LanguageInfo
            {
                FileName = Path.GetFileName(filePath),
                Name = Path.GetFileNameWithoutExtension(filePath),
                Author = "",
                MapEncoding = "utf-8"
            };

            if (!File.Exists(filePath)) return info;

            try
            {
                var lines = File.ReadAllLines(filePath, Encoding.UTF8);
                string currentSection = "";

                foreach (var rawLine in lines)
                {
                    string line = rawLine.Trim();
                    if (string.IsNullOrEmpty(line) || line.StartsWith(";") || line.StartsWith("#")) continue;

                    if (line.StartsWith("[") && line.EndsWith("]"))
                    {
                        currentSection = line.Substring(1, line.Length - 2).Trim();
                        continue;
                    }

                    if (currentSection.Equals("General", StringComparison.OrdinalIgnoreCase))
                    {
                        int eqIdx = line.IndexOf('=');
                        if (eqIdx <= 0) continue;

                        string key = line.Substring(0, eqIdx).Trim();
                        string val = line.Substring(eqIdx + 1).Trim();

                        if (key.Equals("Name", StringComparison.OrdinalIgnoreCase)) info.Name = val;
                        else if (key.Equals("Author", StringComparison.OrdinalIgnoreCase)) info.Author = val;
                        else if (key.Equals("MapEncoding", StringComparison.OrdinalIgnoreCase)) info.MapEncoding = val;
                    }
                }
            }
            catch { }

            return info;
        }

        public static List<LanguageInfo> GetAvailableLanguages(AppConfig config)
        {
            var result = new List<LanguageInfo>();
            var seenFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            string translationsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Translations");
            if (!Directory.Exists(translationsDir))
            {
                try { Directory.CreateDirectory(translationsDir); } catch { }
            }

            // 1. Load files explicitly listed in config.Translations section
            if (config != null && config.Translations != null)
            {
                foreach (var relPath in config.Translations)
                {
                    if (string.IsNullOrWhiteSpace(relPath)) continue;
                    string fullPath = Path.IsPathRooted(relPath) ? relPath : Path.Combine(translationsDir, relPath);
                    if (File.Exists(fullPath) && !seenFiles.Contains(fullPath))
                    {
                        seenFiles.Add(fullPath);
                        result.Add(ParseLanguageHeader(fullPath));
                    }
                }
            }

            // 2. Auto-discover any additional .txt files in Translations folder
            if (Directory.Exists(translationsDir))
            {
                try
                {
                    foreach (var file in Directory.GetFiles(translationsDir, "*.txt"))
                    {
                        if (!seenFiles.Contains(file))
                        {
                            seenFiles.Add(file);
                            string fileName = Path.GetFileName(file);
                            if (config != null && config.Translations != null && !config.Translations.Any(t => string.Equals(t, fileName, StringComparison.OrdinalIgnoreCase)))
                            {
                                config.Translations.Add(fileName);
                            }
                            result.Add(ParseLanguageHeader(file));
                        }
                    }
                }
                catch { }
            }

            return result;
        }

        public static void LoadLanguage(string languageFileName)
        {
            _strings.Clear();
            if (string.IsNullOrWhiteSpace(languageFileName)) languageFileName = "en.txt";
            _currentLanguageFile = languageFileName;

            string translationsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Translations");
            string fullPath = Path.IsPathRooted(languageFileName) ? languageFileName : Path.Combine(translationsDir, languageFileName);

            if (!File.Exists(fullPath))
            {
                // Fallback: check if en.txt exists beside exe
                string fallbackPath = Path.Combine(translationsDir, "en.txt");
                if (File.Exists(fallbackPath)) fullPath = fallbackPath;
                else return;
            }

            try
            {
                var lines = File.ReadAllLines(fullPath, Encoding.UTF8);
                string currentSection = "";

                foreach (var rawLine in lines)
                {
                    string line = rawLine.Trim();
                    if (string.IsNullOrEmpty(line) || line.StartsWith(";") || line.StartsWith("#")) continue;

                    if (line.StartsWith("[") && line.EndsWith("]"))
                    {
                        currentSection = line.Substring(1, line.Length - 2).Trim();
                        continue;
                    }

                    if (currentSection.Equals("Values", StringComparison.OrdinalIgnoreCase))
                    {
                        int eqIdx = line.IndexOf('=');
                        if (eqIdx <= 0) continue;

                        string key = line.Substring(0, eqIdx).Trim();
                        string val = line.Substring(eqIdx + 1).Trim();

                        // Unescape newlines (\n, \r\n)
                        val = val.Replace("\\r\\n", "\r\n").Replace("\\n", "\n");

                        _strings[key] = val;
                    }
                }
            }
            catch { }
        }

        public static void GenerateEnglishTranslationFile(string outputPath)
        {
            var keyValues = new SortedDictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            try
            {
                string exeLocation = System.Reflection.Assembly.GetExecutingAssembly().Location;
                string searchDir = !string.IsNullOrEmpty(exeLocation) ? Path.GetDirectoryName(exeLocation) : AppDomain.CurrentDomain.BaseDirectory;
                DirectoryInfo dir = new DirectoryInfo(searchDir);
                bool found = false;
                while (dir != null)
                {
                    if (Directory.GetFiles(dir.FullName, "*.csproj").Length > 0 || Directory.Exists(Path.Combine(dir.FullName, "UI")))
                    {
                        searchDir = dir.FullName;
                        found = true;
                        break;
                    }
                    dir = dir.Parent;
                }

                if (!found && Directory.Exists(Directory.GetCurrentDirectory()))
                {
                    dir = new DirectoryInfo(Directory.GetCurrentDirectory());
                    while (dir != null)
                    {
                        if (Directory.GetFiles(dir.FullName, "*.csproj").Length > 0 || Directory.Exists(Path.Combine(dir.FullName, "UI")))
                        {
                            searchDir = dir.FullName;
                            break;
                        }
                        dir = dir.Parent;
                    }
                }

                if (Directory.Exists(searchDir))
                {
                    var csFiles = Directory.GetFiles(searchDir, "*.cs", SearchOption.AllDirectories);
                    var callRegex = new System.Text.RegularExpressions.Regex(
                        @"LanguageManager\.GetString(?:Format)?\s*\(\s*""(?<key>[^""]+)""\s*,\s*(?<valexpr>(?:""(?:[^""\\]|\\.)*""\s*(?:\+\s*)?)+)",
                        System.Text.RegularExpressions.RegexOptions.Singleline);
                    var strPartRegex = new System.Text.RegularExpressions.Regex(
                        @"""(?<part>(?:[^""\\]|\\.)*)""",
                        System.Text.RegularExpressions.RegexOptions.Singleline);

                    foreach (var csFile in csFiles)
                    {
                        if (csFile.IndexOf(@"\obj\", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            csFile.IndexOf(@"\bin\", StringComparison.OrdinalIgnoreCase) >= 0)
                            continue;

                        string content = File.ReadAllText(csFile, Encoding.UTF8);
                        var matches = callRegex.Matches(content);
                        foreach (System.Text.RegularExpressions.Match match in matches)
                        {
                            string key = match.Groups["key"].Value;
                            string valexpr = match.Groups["valexpr"].Value;

                            var partMatches = strPartRegex.Matches(valexpr);
                            var valSb = new StringBuilder();
                            foreach (System.Text.RegularExpressions.Match pm in partMatches)
                            {
                                valSb.Append(pm.Groups["part"].Value);
                            }

                            string val = valSb.ToString();
                            val = val.Replace("\\\"", "\"").Replace("\\\\", "\\");

                            if (!keyValues.ContainsKey(key))
                            {
                                keyValues[key] = val;
                            }
                        }
                    }
                }
            }
            catch { }

            var sb = new StringBuilder();
            sb.AppendLine("[General]");
            sb.AppendLine("Name=English");
            sb.AppendLine("Author=");
            sb.AppendLine("MapEncoding=utf-8");
            sb.AppendLine();
            sb.AppendLine("[Values]");

            foreach (var kvp in keyValues)
            {
                string escapedVal = kvp.Value.Replace("\r\n", "\\n").Replace("\n", "\\n").Replace("\r", "");
                sb.AppendLine($"{kvp.Key}={escapedVal}");
            }

            string dirPath = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(dirPath) && !Directory.Exists(dirPath))
            {
                Directory.CreateDirectory(dirPath);
            }

            File.WriteAllText(outputPath, sb.ToString(), new UTF8Encoding(false));
        }
    }
}
