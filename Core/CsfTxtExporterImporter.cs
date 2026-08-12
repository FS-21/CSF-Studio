using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace CsfStudio.Core
{
    public class ImportKeyDiff
    {
        public string KeyName { get; set; }
        public CsfSessionDocument TargetDocument { get; set; }
        public bool IsNewKey { get; set; }
        public string CurrentValue { get; set; }
        public string CurrentExtra { get; set; }
        public string ImportedValue { get; set; }
        public string ImportedExtra { get; set; }
        public bool ShouldImport { get; set; } = true;
    }

    public static class CsfTxtExporterImporter
    {
        private static readonly Encoding Utf8NoBom = new UTF8Encoding(false);

        /// <summary>
        /// Exports a CSF document to a plain text file in UTF-8 without BOM.
        /// If selectedKeyNames is provided, exports only the specified keys.
        /// </summary>
        public static void ExportToTxt(CsfDocument doc, string filePath, IEnumerable<string> selectedKeyNames = null)
        {
            if (doc == null) throw new ArgumentNullException(nameof(doc));

            var keysToExport = selectedKeyNames != null
                ? new HashSet<string>(selectedKeyNames, StringComparer.OrdinalIgnoreCase)
                : null;

            var sb = new StringBuilder();

            foreach (var label in doc.Labels)
            {
                if (keysToExport != null && !keysToExport.Contains(label.Name))
                {
                    continue;
                }

                sb.AppendLine($"[{label.Name}]");
                if (label.Strings.Count > 0)
                {
                    var firstStr = label.Strings[0];
                    string escapedValue = EscapeString(firstStr.Value);
                    sb.AppendLine($"Value={escapedValue}");

                    if (firstStr.HasExtra)
                    {
                        sb.AppendLine($"Sound={firstStr.ExtraValue}");
                    }
                }
                sb.AppendLine();
            }

            File.WriteAllText(filePath, sb.ToString(), Utf8NoBom);
        }

        /// <summary>
        /// Imports a UTF-8 plain text file and returns the list of parsed CSF labels.
        /// </summary>
        public static List<CsfLabel> ImportFromTxt(string filePath)
        {
            var labels = new List<CsfLabel>();
            string[] lines = File.ReadAllLines(filePath, Encoding.UTF8);

            CsfLabel currentLabel = null;
            string currentValue = null;
            string currentExtra = null;

            foreach (var rawLine in lines)
            {
                string line = rawLine.Trim();
                if (string.IsNullOrEmpty(line) || line.StartsWith(";") || line.StartsWith("#"))
                {
                    continue;
                }

                if (line.StartsWith("[") && line.EndsWith("]"))
                {
                    if (currentLabel != null)
                    {
                        currentLabel.Strings.Add(new CsfStringEntry(UnescapeString(currentValue ?? string.Empty), currentExtra));
                        labels.Add(currentLabel);
                    }

                    string keyName = line.Substring(1, line.Length - 2).Trim();
                    currentLabel = new CsfLabel(keyName);
                    currentValue = string.Empty;
                    currentExtra = null;
                }
                else if (currentLabel != null)
                {
                    if (line.StartsWith("Value=", StringComparison.OrdinalIgnoreCase))
                    {
                        currentValue = rawLine.Substring(rawLine.IndexOf('=') + 1);
                    }
                    else if (line.StartsWith("Sound=", StringComparison.OrdinalIgnoreCase))
                    {
                        currentExtra = rawLine.Substring(rawLine.IndexOf('=') + 1).Trim();
                    }
                }
            }

            if (currentLabel != null)
            {
                currentLabel.Strings.Add(new CsfStringEntry(UnescapeString(currentValue ?? string.Empty), currentExtra));
                labels.Add(currentLabel);
            }

            return labels;
        }

        /// <summary>
        /// Exports only a list of key names without text values (Key Structure Export).
        /// </summary>
        public static void ExportKeyStructureToTxt(IEnumerable<string> keyNames, string filePath)
        {
            var sb = new StringBuilder();

            foreach (var key in keyNames)
            {
                sb.AppendLine(key);
            }

            File.WriteAllText(filePath, sb.ToString(), Utf8NoBom);
        }

        /// <summary>
        /// Compares existing CSF entries against imported labels and returns a diff overview.
        /// </summary>
        public static List<ImportKeyDiff> CompareImportDiff(CsfDocument existingDoc, List<CsfLabel> importedLabels)
        {
            var diffList = new List<ImportKeyDiff>();
            var existingMap = existingDoc.Labels.ToDictionary(l => l.Name, StringComparer.OrdinalIgnoreCase);

            foreach (var impLbl in importedLabels)
            {
                string impVal = impLbl.FirstValue;
                string impExtra = impLbl.FirstExtraValue;

                if (existingMap.TryGetValue(impLbl.Name, out var existingLbl))
                {
                    diffList.Add(new ImportKeyDiff
                    {
                        KeyName = impLbl.Name,
                        IsNewKey = false,
                        CurrentValue = existingLbl.FirstValue,
                        CurrentExtra = existingLbl.FirstExtraValue,
                        ImportedValue = impVal,
                        ImportedExtra = impExtra,
                        ShouldImport = false
                    });
                }
                else
                {
                    diffList.Add(new ImportKeyDiff
                    {
                        KeyName = impLbl.Name,
                        IsNewKey = true,
                        CurrentValue = null,
                        CurrentExtra = null,
                        ImportedValue = impVal,
                        ImportedExtra = impExtra,
                        ShouldImport = true
                    });
                }
            }

            return diffList;
        }

        private static string EscapeString(string str)
        {
            if (string.IsNullOrEmpty(str)) return string.Empty;
            return str.Replace("\r\n", "\\n").Replace("\n", "\\n").Replace("\r", "\\n");
        }

        private static string UnescapeString(string str)
        {
            if (string.IsNullOrEmpty(str)) return string.Empty;
            return str.Replace("\\n", "\r\n");
        }
    }
}
