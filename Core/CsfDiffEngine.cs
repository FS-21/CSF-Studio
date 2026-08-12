using System;
using System.Collections.Generic;
using System.Linq;

namespace CsfStudio.Core
{
    public enum CsfDiffStatus
    {
        Unchanged,
        Added,
        Modified,
        Removed
    }

    public class CsfDiffItem
    {
        public string Key { get; set; }
        public CsfDiffStatus Status { get; set; }
        public bool ExistsInA { get; set; }
        public bool ExistsInB { get; set; }
        public string ValueA { get; set; }
        public string ExtraValueA { get; set; }
        public string ValueB { get; set; }
        public string ExtraValueB { get; set; }

        public override string ToString()
        {
            return $"[{Status}] {Key}";
        }
    }

    public class CsfDiffResult
    {
        public List<CsfDiffItem> Items { get; set; } = new List<CsfDiffItem>();
        public int TotalCount => Items.Count;
        public int AddedCount => Items.Count(i => i.Status == CsfDiffStatus.Added);
        public int ModifiedCount => Items.Count(i => i.Status == CsfDiffStatus.Modified);
        public int RemovedCount => Items.Count(i => i.Status == CsfDiffStatus.Removed);
        public int UnchangedCount => Items.Count(i => i.Status == CsfDiffStatus.Unchanged);
        public int TotalDifferences => AddedCount + ModifiedCount + RemovedCount;
    }

    public static class CsfDiffEngine
    {
        public static void SetString(this CsfDocument doc, string key, string value, string extraValue = null)
        {
            if (doc == null || string.IsNullOrEmpty(key)) return;
            var lbl = doc.Labels.FirstOrDefault(l => string.Equals(l.Name, key, StringComparison.OrdinalIgnoreCase));
            if (lbl == null)
            {
                lbl = new CsfLabel(key);
                doc.Labels.Add(lbl);
            }

            if (lbl.Strings.Count == 0)
            {
                lbl.Strings.Add(new CsfStringEntry(value, extraValue));
            }
            else
            {
                lbl.Strings[0].Value = value ?? string.Empty;
                lbl.Strings[0].ExtraValue = extraValue;
            }
        }

        public static CsfDiffResult Compare(CsfDocument docA, CsfDocument docB)
        {
            var result = new CsfDiffResult();
            if (docA == null && docB == null) return result;

            var mapA = new Dictionary<string, CsfLabel>(StringComparer.OrdinalIgnoreCase);
            var mapB = new Dictionary<string, CsfLabel>(StringComparer.OrdinalIgnoreCase);

            if (docA != null)
            {
                foreach (var lbl in docA.Labels)
                {
                    if (!string.IsNullOrEmpty(lbl.Name) && !mapA.ContainsKey(lbl.Name))
                    {
                        mapA[lbl.Name] = lbl;
                    }
                }
            }

            if (docB != null)
            {
                foreach (var lbl in docB.Labels)
                {
                    if (!string.IsNullOrEmpty(lbl.Name) && !mapB.ContainsKey(lbl.Name))
                    {
                        mapB[lbl.Name] = lbl;
                    }
                }
            }

            var keyList = new List<string>();
            var keySet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (docA != null)
            {
                foreach (var lbl in docA.Labels)
                {
                    if (keySet.Add(lbl.Name))
                    {
                        keyList.Add(lbl.Name);
                    }
                }
            }

            if (docB != null)
            {
                foreach (var lbl in docB.Labels)
                {
                    if (keySet.Add(lbl.Name))
                    {
                        keyList.Add(lbl.Name);
                    }
                }
            }

            foreach (string key in keyList)
            {
                bool existsA = mapA.TryGetValue(key, out var lblA);
                bool existsB = mapB.TryGetValue(key, out var lblB);

                string valA = existsA ? (lblA.FirstValue ?? string.Empty) : null;
                string extraA = existsA ? (lblA.FirstExtraValue ?? string.Empty) : null;

                string valB = existsB ? (lblB.FirstValue ?? string.Empty) : null;
                string extraB = existsB ? (lblB.FirstExtraValue ?? string.Empty) : null;

                CsfDiffStatus status;
                if (!existsA && existsB)
                {
                    status = CsfDiffStatus.Added;
                }
                else if (existsA && !existsB)
                {
                    status = CsfDiffStatus.Removed;
                }
                else if (!string.Equals(valA, valB, StringComparison.Ordinal) || !string.Equals(extraA, extraB, StringComparison.Ordinal))
                {
                    status = CsfDiffStatus.Modified;
                }
                else
                {
                    status = CsfDiffStatus.Unchanged;
                }

                result.Items.Add(new CsfDiffItem
                {
                    Key = key,
                    Status = status,
                    ExistsInA = existsA,
                    ExistsInB = existsB,
                    ValueA = valA,
                    ExtraValueA = extraA,
                    ValueB = valB,
                    ExtraValueB = extraB
                });
            }

            return result;
        }
    }
}
