using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace CsfStudio.Core
{
    public class IniScanResult
    {
        public string KeyName { get; set; }
        public string SourceIniFile { get; set; }
        public string FullIniPath { get; set; }
        public string IniSection { get; set; }
        public string IniPropertyName { get; set; }
        public bool ExistsInCsf { get; set; }
        public bool IsNostrInline { get; set; }
    }

    public static class IniScanner
    {
        // Regex pattern to extract key and value from any INI line: PropertyName = Value
        private static readonly Regex KeyValueRegex = new Regex(
            @"^\s*([A-Za-z0-9_\.]+)\s*=\s*(.*?)\s*$",
            RegexOptions.Compiled);

        private static string CleanIniLine(string rawLine)
        {
            if (string.IsNullOrWhiteSpace(rawLine)) return string.Empty;

            string line = rawLine.Trim();

            // Strip inline comments starting with ';' or '#'
            int semiIdx = line.IndexOf(';');
            if (semiIdx >= 0) line = line.Substring(0, semiIdx).Trim();

            int hashIdx = line.IndexOf('#');
            if (hashIdx >= 0) line = line.Substring(0, hashIdx).Trim();

            return line;
        }

        public static List<IniScanResult> ScanIniFile(string iniFilePath, CsfSession session, string customPropertiesList = null)
        {
            var results = new List<IniScanResult>();
            if (!File.Exists(iniFilePath)) return results;

            if (string.IsNullOrWhiteSpace(customPropertiesList))
            {
                var cfg = ConfigManager.LoadConfig();
                customPropertiesList = cfg.IniScanProperties;
            }

            var allowedPropSet = new HashSet<string>(
                (customPropertiesList ?? string.Empty)
                    .Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(p => p.Trim()),
                StringComparer.OrdinalIgnoreCase
            );

            string[] lines = File.ReadAllLines(iniFilePath);
            string fileName = Path.GetFileName(iniFilePath);
            string fullPath = Path.GetFullPath(iniFilePath);

            var existingKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (session != null)
            {
                foreach (var doc in session.Documents)
                {
                    foreach (var lbl in doc.Document.Labels)
                    {
                        existingKeys.Add(lbl.Name);
                    }
                }
            }

            var dynamicCsfProperties = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // Pass 1: Dynamically discover any INI property that uses NOSTR: inline strings
            foreach (var rawLine in lines)
            {
                string line = CleanIniLine(rawLine);
                if (string.IsNullOrEmpty(line)) continue;

                var kvMatch = KeyValueRegex.Match(line);
                if (kvMatch.Success)
                {
                    string propName = kvMatch.Groups[1].Value.Trim();
                    string propVal = kvMatch.Groups[2].Value.Trim();

                    if (propVal.StartsWith("NOSTR:", StringComparison.OrdinalIgnoreCase))
                    {
                        dynamicCsfProperties.Add(propName);
                    }
                }
            }

            // Pass 2: Extract all CSF references and NOSTR inline strings
            string currentSection = "General";
            var seenKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var rawLine in lines)
            {
                string line = CleanIniLine(rawLine);
                if (string.IsNullOrEmpty(line)) continue;

                if (line.StartsWith("[") && line.EndsWith("]"))
                {
                    currentSection = line.Substring(1, line.Length - 2).Trim();
                    continue;
                }

                var kvMatch = KeyValueRegex.Match(line);
                if (kvMatch.Success)
                {
                    string propName = kvMatch.Groups[1].Value.Trim();
                    string propVal = kvMatch.Groups[2].Value.Trim();

                    // Check if property is in allowed properties list OR dynamically discovered via NOSTR:
                    bool isCsfProperty = allowedPropSet.Contains(propName) || 
                                         allowedPropSet.Any(p => IsPropertyMatch(propName, p)) || 
                                         dynamicCsfProperties.Contains(propName);

                    if (!isCsfProperty) continue;

                    // Skip numeric or common boolean keywords (YES, NO, 0, 1)
                    if (string.Equals(propVal, "YES", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(propVal, "NO", StringComparison.OrdinalIgnoreCase) ||
                        int.TryParse(propVal, out _))
                    {
                        continue;
                    }

                    bool isNostr = propVal.StartsWith("NOSTR:", StringComparison.OrdinalIgnoreCase);

                    string uniqueToken = $"{currentSection}:{propName}:{propVal}";
                    if (!seenKeys.Contains(uniqueToken))
                    {
                        seenKeys.Add(uniqueToken);
                        results.Add(new IniScanResult
                        {
                            KeyName = propVal,
                            SourceIniFile = fileName,
                            FullIniPath = fullPath,
                            IniSection = currentSection,
                            IniPropertyName = propName,
                            ExistsInCsf = isNostr || existingKeys.Contains(propVal),
                            IsNostrInline = isNostr
                        });
                    }
                }
            }

            return results;
        }

        private static bool IsPropertyMatch(string propName, string pattern)
        {
            if (string.Equals(propName, pattern, StringComparison.OrdinalIgnoreCase)) return true;

            if (pattern.Contains("*") || pattern.Contains("?"))
            {
                try
                {
                    string regexPattern = "^" + Regex.Escape(pattern)
                                                    .Replace(@"\*", ".*")
                                                    .Replace(@"\?", ".") + "$";
                    return Regex.IsMatch(propName, regexPattern, RegexOptions.IgnoreCase);
                }
                catch
                {
                    return false;
                }
            }

            return false;
        }
    }
}
