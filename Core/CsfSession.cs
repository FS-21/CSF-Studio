using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace CsfStudio.Core
{
    public enum KeySyncStatus
    {
        Complete = 0,          // 🟢 Present with text in all loaded CSFs
        MissingInSome = 1,     // 🔴 Missing in one or more open CSFs
        UntranslatedOrEmpty = 2// 🟡 Present but empty or identical to base language
    }

    public class CsfSessionDocument
    {
        public string LanguageTag { get; set; } = "EN";
        public string TranslationContentLanguage { get; set; } = string.Empty;
        public string FilePath { get; set; } = null;
        public CsfDocument Document { get; set; } = new CsfDocument();
        public bool IsModified { get; set; } = false;

        public string FileName => string.IsNullOrEmpty(FilePath) ? "Untitled" : Path.GetFileName(FilePath);

        public override string ToString()
        {
            return $"[{LanguageTag}] {FileName}";
        }

        public static string ResolveLanguageTag(string tag, CsfDocument doc, int index = 0)
        {
            if (!string.IsNullOrWhiteSpace(tag)) return tag.Trim();

            return $"CSF_{index + 1:D2}";
        }

        public CsfSessionDocument(string tag, CsfDocument doc, string path = null, int index = 0)
        {
            Document = doc ?? new CsfDocument();
            LanguageTag = ResolveLanguageTag(tag, Document, index);
            FilePath = path;
        }
    }

    public class MasterKeyRow
    {
        public string KeyName { get; set; } = string.Empty;
        public string Category { get; set; } = "No category";

        // Number of documents the row was built against (set by BuildMasterKeyList).
        public int ExpectedLanguageCount { get; set; } = 0;

        // Computed live from ValuesPerLanguage so cached rows never go stale when
        // string values are edited in place (entries are shared by reference).
        public KeySyncStatus Status
        {
            get
            {
                if (ValuesPerLanguage.Count < ExpectedLanguageCount) return KeySyncStatus.MissingInSome;
                foreach (var entry in ValuesPerLanguage.Values)
                {
                    if (entry == null || string.IsNullOrWhiteSpace(entry.Value)) return KeySyncStatus.UntranslatedOrEmpty;
                }
                return KeySyncStatus.Complete;
            }
        }

        // Map: LanguageTag -> CsfStringEntry (null if key is missing in that CSF)
        public Dictionary<string, CsfStringEntry> ValuesPerLanguage { get; set; } 
            = new Dictionary<string, CsfStringEntry>(StringComparer.OrdinalIgnoreCase);

        // Indicates which languages have this key missing
        public HashSet<string> MissingLanguages { get; set; } 
            = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    }

    public class TabPageTagInfo
    {
        public string LanguageTag { get; set; }
        public string StatusKey { get; set; }
    }

    public class CsfSession
    {
        public List<CsfSessionDocument> Documents { get; } = new List<CsfSessionDocument>();
        
        // Designated Base / Master Reference Document
        public CsfSessionDocument BaseDocument => Documents.Count > 0 ? Documents[0] : null;

        // Indicates Single File Mode vs Multi-File Session Mode
        public bool IsSingleFileMode => Documents.Count <= 1;

        public event EventHandler SessionChanged;

        public void AddDocument(string tag, CsfDocument doc, string filePath = null)
        {
            int index = Documents.Count;
            Documents.Add(new CsfSessionDocument(tag, doc, filePath, index));
            OnSessionChanged();
        }

        public void RemoveDocument(CsfSessionDocument doc)
        {
            if (Documents.Remove(doc))
            {
                OnSessionChanged();
            }
        }

        public void Clear()
        {
            Documents.Clear();
            OnSessionChanged();
        }

        protected virtual void OnSessionChanged()
        {
            SessionChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Generates master key list based on FULL UNION of all opened CSF keys.
        /// </summary>
        /// <summary>
        /// Gets total count of unique keys across all open documents.
        /// </summary>
        public int TotalUniqueKeyCount
        {
            get
            {
                if (Documents.Count == 0) return 0;
                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var doc in Documents)
                {
                    foreach (var lbl in doc.Document.Labels)
                    {
                        seen.Add(lbl.Name);
                    }
                }
                return seen.Count;
            }
        }

        /// <summary>
        /// Builds master list of keys across all loaded CSF documents with O(1) dictionary lookup performance.
        /// </summary>
        /// <param name="sortByBinarySequence">If true, preserves physical sequence of base CSF first.</param>
        public List<MasterKeyRow> BuildMasterKeyList(bool sortByBinarySequence = true)
        {
            var masterRows = new List<MasterKeyRow>();
            if (Documents.Count == 0) return masterRows;

            var keyOrder = new List<string>();
            var seenKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (sortByBinarySequence && BaseDocument != null)
            {
                foreach (var lbl in BaseDocument.Document.Labels)
                {
                    if (!seenKeys.Contains(lbl.Name))
                    {
                        seenKeys.Add(lbl.Name);
                        keyOrder.Add(lbl.Name);
                    }
                }
            }

            foreach (var sDoc in Documents)
            {
                foreach (var lbl in sDoc.Document.Labels)
                {
                    if (!seenKeys.Contains(lbl.Name))
                    {
                        seenKeys.Add(lbl.Name);
                        keyOrder.Add(lbl.Name);
                    }
                }
            }

            // Pre-index labels for O(1) lookup speed per document
            var docLookups = new Dictionary<string, Dictionary<string, CsfLabel>>(StringComparer.OrdinalIgnoreCase);
            foreach (var sDoc in Documents)
            {
                var dict = new Dictionary<string, CsfLabel>(StringComparer.OrdinalIgnoreCase);
                foreach (var lbl in sDoc.Document.Labels)
                {
                    if (!dict.ContainsKey(lbl.Name))
                    {
                        dict[lbl.Name] = lbl;
                    }
                }
                docLookups[sDoc.LanguageTag] = dict;
            }

            foreach (var keyName in keyOrder)
            {
                var row = new MasterKeyRow
                {
                    KeyName = keyName,
                    Category = ExtractCategory(keyName),
                    // Distinct language tags, not document count: two documents may share a tag.
                    ExpectedLanguageCount = docLookups.Count
                };

                foreach (var sDoc in Documents)
                {
                    var dict = docLookups[sDoc.LanguageTag];
                    if (dict.TryGetValue(keyName, out var label) && label.Strings.Count > 0)
                    {
                        var entry = label.Strings[0];
                        row.ValuesPerLanguage[sDoc.LanguageTag] = entry;
                    }
                    else
                    {
                        row.MissingLanguages.Add(sDoc.LanguageTag);
                    }
                }

                masterRows.Add(row);
            }

            return masterRows;
        }

        /// <summary>
        /// Propagates missing keys across all open CSFs so they share the exact same key structure.
        /// </summary>
        public int SynchronizeAllMissingKeys(bool cloneBaseValue = false)
        {
            if (Documents.Count <= 1) return 0;

            var masterKeys = BuildMasterKeyList(false);
            int addedCount = 0;

            foreach (var row in masterKeys)
            {
                if (row.MissingLanguages.Count > 0)
                {
                    string defaultVal = string.Empty;
                    string defaultExtra = null;

                    if (cloneBaseValue && BaseDocument != null && row.ValuesPerLanguage.TryGetValue(BaseDocument.LanguageTag, out var baseEntry))
                    {
                        defaultVal = baseEntry.Value;
                        defaultExtra = baseEntry.ExtraValue;
                    }

                    foreach (var sDoc in Documents)
                    {
                        if (row.MissingLanguages.Contains(sDoc.LanguageTag))
                        {
                            sDoc.Document.Labels.Add(new CsfLabel(row.KeyName, defaultVal, defaultExtra));
                            sDoc.IsModified = true;
                            addedCount++;
                        }
                    }
                }
            }

            return addedCount;
        }

        public static string ExtractCategory(string keyName)
        {
            if (string.IsNullOrEmpty(keyName)) return "No category";
            int idx = keyName.IndexOf(':');
            if (idx > 0)
            {
                return keyName.Substring(0, idx);
            }
            idx = keyName.IndexOf('_');
            if (idx > 0)
            {
                return keyName.Substring(0, idx);
            }
            return "No category";
        }

        public bool KeyExists(string keyName)
        {
            if (string.IsNullOrEmpty(keyName)) return false;
            return Documents.Any(d => d.Document != null && d.Document.Labels.Any(l => string.Equals(l.Name, keyName, StringComparison.OrdinalIgnoreCase)));
        }

        public bool RenameKey(string oldKeyName, string newKeyName)
        {
            if (string.IsNullOrEmpty(oldKeyName) || string.IsNullOrEmpty(newKeyName)) return false;

            bool renamed = false;
            foreach (var sDoc in Documents)
            {
                if (sDoc.Document == null) continue;
                var lbl = sDoc.Document.Labels.FirstOrDefault(l => string.Equals(l.Name, oldKeyName, StringComparison.OrdinalIgnoreCase));
                if (lbl != null)
                {
                    lbl.Name = newKeyName;
                    sDoc.IsModified = true;
                    renamed = true;
                }
            }
            return renamed;
        }
    }
}
