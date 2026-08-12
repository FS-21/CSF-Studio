using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace CsfStudio.Core
{
    public static class CsvJsonExporter
    {
        #region CSV Export / Import

        public static void ExportToCsv(CsfDocument doc, string filePath, char delimiter = ',')
        {
            using (var sw = new StreamWriter(filePath, false, Encoding.UTF8))
            {
                // Header row
                sw.WriteLine($"Label{delimiter}Value{delimiter}Sound");

                foreach (var label in doc.Labels)
                {
                    if (label.Strings.Count == 0)
                    {
                        sw.WriteLine($"{EscapeCsv(label.Name, delimiter)}{delimiter}{delimiter}");
                    }
                    else
                    {
                        foreach (var str in label.Strings)
                        {
                            string lName = EscapeCsv(label.Name, delimiter);
                            string val = EscapeCsv(str.Value, delimiter);
                            string extra = EscapeCsv(str.ExtraValue, delimiter);
                            sw.WriteLine($"{lName}{delimiter}{val}{delimiter}{extra}");
                        }
                    }
                }
            }
        }

        public static CsfDocument ImportFromCsv(string filePath, char delimiter = ',')
        {
            var doc = new CsfDocument();
            var lines = File.ReadAllLines(filePath, Encoding.UTF8);

            if (lines.Length == 0) return doc;

            // Skip header if present
            int startIdx = 0;
            if (lines[0].StartsWith("Label", StringComparison.OrdinalIgnoreCase))
            {
                startIdx = 1;
            }

            var labelMap = new Dictionary<string, CsfLabel>(StringComparer.OrdinalIgnoreCase);

            for (int i = startIdx; i < lines.Length; i++)
            {
                string line = lines[i];
                if (string.IsNullOrWhiteSpace(line)) continue;

                var columns = ParseCsvLine(line, delimiter);
                if (columns.Count == 0) continue;

                string name = columns[0];
                string val = columns.Count > 1 ? columns[1] : string.Empty;
                string extra = columns.Count > 2 ? (string.IsNullOrEmpty(columns[2]) ? null : columns[2]) : null;

                if (!labelMap.TryGetValue(name, out var label))
                {
                    label = new CsfLabel(name);
                    labelMap[name] = label;
                    doc.Labels.Add(label);
                }

                label.Strings.Add(new CsfStringEntry(val, extra));
            }

            return doc;
        }

        private static string EscapeCsv(string field, char delimiter)
        {
            if (string.IsNullOrEmpty(field)) return string.Empty;
            bool needsQuotes = field.Contains(delimiter.ToString()) || field.Contains("\"") || field.Contains("\n") || field.Contains("\r");
            if (!needsQuotes) return field;

            return "\"" + field.Replace("\"", "\"\"") + "\"";
        }

        private static List<string> ParseCsvLine(string line, char delimiter)
        {
            var results = new List<string>();
            var sb = new StringBuilder();
            bool inQuotes = false;

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (inQuotes)
                {
                    if (c == '"')
                    {
                        if (i + 1 < line.Length && line[i + 1] == '"')
                        {
                            sb.Append('"');
                            i++; // skip escaped quote
                        }
                        else
                        {
                            inQuotes = false;
                        }
                    }
                    else
                    {
                        sb.Append(c);
                    }
                }
                else
                {
                    if (c == '"')
                    {
                        inQuotes = true;
                    }
                    else if (c == delimiter)
                    {
                        results.Add(sb.ToString());
                        sb.Clear();
                    }
                    else
                    {
                        sb.Append(c);
                    }
                }
            }
            results.Add(sb.ToString());
            return results;
        }

        #endregion

        #region JSON Export / Import

        public static void ExportToJson(CsfDocument doc, string filePath)
        {
            var sb = new StringBuilder();
            sb.AppendLine("{");
            sb.AppendLine($"  \"version\": {doc.Version},");
            sb.AppendLine($"  \"language\": \"{doc.Language}\",");
            sb.AppendLine("  \"labels\": [");

            for (int i = 0; i < doc.Labels.Count; i++)
            {
                var lbl = doc.Labels[i];
                sb.AppendLine("    {");
                sb.AppendLine($"      \"name\": \"{EscapeJson(lbl.Name)}\",");
                sb.AppendLine("      \"strings\": [");
                for (int j = 0; j < lbl.Strings.Count; j++)
                {
                    var str = lbl.Strings[j];
                    sb.AppendLine("        {");
                    sb.AppendLine($"          \"value\": \"{EscapeJson(str.Value)}\",");
                    sb.AppendLine($"          \"sound\": {(str.ExtraValue == null ? "null" : $"\"{EscapeJson(str.ExtraValue)}\"")}");
                    sb.Append("        }");
                    if (j < lbl.Strings.Count - 1) sb.AppendLine(",");
                    else sb.AppendLine();
                }
                sb.AppendLine("      ]");
                sb.Append("    }");
                if (i < doc.Labels.Count - 1) sb.AppendLine(",");
                else sb.AppendLine();
            }

            sb.AppendLine("  ]");
            sb.AppendLine("}");

            File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
        }

        public static CsfDocument ImportFromJson(string filePath)
        {
            var doc = new CsfDocument();
            string json = File.ReadAllText(filePath, Encoding.UTF8);

            var labelMatches = System.Text.RegularExpressions.Regex.Matches(json, @"\{\s*""name""\s*:\s*""(?<name>[^""]+)""\s*,\s*""strings""\s*:\s*\[(?<strings>.*?)\]\s*\}", System.Text.RegularExpressions.RegexOptions.Singleline);

            foreach (System.Text.RegularExpressions.Match lMatch in labelMatches)
            {
                string name = UnescapeJson(lMatch.Groups["name"].Value);
                string strBlock = lMatch.Groups["strings"].Value;

                var label = new CsfLabel(name);

                var strMatches = System.Text.RegularExpressions.Regex.Matches(strBlock, @"\{\s*""value""\s*:\s*""(?<val>.*?)""\s*,\s*""sound""\s*:\s*(?:null|""(?<sound>.*?)""\s*)\}", System.Text.RegularExpressions.RegexOptions.Singleline);

                foreach (System.Text.RegularExpressions.Match sMatch in strMatches)
                {
                    string val = UnescapeJson(sMatch.Groups["val"].Value);
                    string sound = sMatch.Groups["sound"].Success ? UnescapeJson(sMatch.Groups["sound"].Value) : null;
                    label.Strings.Add(new CsfStringEntry(val, sound));
                }

                if (label.Strings.Count == 0)
                {
                    label.Strings.Add(new CsfStringEntry(string.Empty, null));
                }

                doc.Labels.Add(label);
            }

            return doc;
        }

        private static string EscapeJson(string str)
        {
            if (string.IsNullOrEmpty(str)) return string.Empty;
            return str
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\n", "\\n")
                .Replace("\r", "\\r")
                .Replace("\t", "\\t");
        }

        private static string UnescapeJson(string str)
        {
            if (string.IsNullOrEmpty(str)) return string.Empty;
            return str
                .Replace("\\\"", "\"")
                .Replace("\\\\", "\\")
                .Replace("\\n", "\n")
                .Replace("\\r", "\r")
                .Replace("\\t", "\t");
        }

        #endregion
    }
}
