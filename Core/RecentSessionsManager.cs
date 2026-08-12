using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Serialization;

namespace CsfStudio.Core
{
    public class SavedCsfFileInfo
    {
        public string LanguageTag { get; set; }
        public string TranslationContentLanguage { get; set; } = string.Empty;
        public string FilePath { get; set; }
    }

    public class RecentSessionItem
    {
        public string PrimaryTitle { get; set; }
        public string SecondaryInfo { get; set; }
        public DateTime Timestamp { get; set; }
        public List<SavedCsfFileInfo> Files { get; set; } = new List<SavedCsfFileInfo>();
        public string UnpinnedLanguageTags { get; set; } = string.Empty;
        public string LastSelectedKeyName { get; set; } = string.Empty;
        public string ActiveTabName { get; set; } = string.Empty;
        public string ActivePinnedLanguageTag { get; set; } = string.Empty;

        public string MenuDisplayText
        {
            get
            {
                if (string.IsNullOrEmpty(SecondaryInfo)) return PrimaryTitle;
                return $"{PrimaryTitle}\n    ↳ {SecondaryInfo}";
            }
        }

        public string ToolTipDetail
        {
            get
            {
                var lines = new List<string>
                {
                    $"Session Date: {Timestamp:yyyy-MM-dd HH:mm}",
                    $"Total CSF Files: {Files.Count}",
                    "Included Files & Labels:"
                };

                foreach (var f in Files)
                {
                    string tag = string.IsNullOrWhiteSpace(f.LanguageTag) ? "MAIN" : f.LanguageTag;
                    lines.Add($"  • [{tag}] {f.FilePath}");
                }

                return string.Join(Environment.NewLine, lines);
            }
        }
    }

    public static class RecentSessionsManager
    {
        private static readonly string AppDataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CSFStudio");
        private static readonly string ConfigFilePath = Path.Combine(AppDataFolder, "recent_sessions.xml");
        private static readonly XmlSerializer Serializer = new XmlSerializer(typeof(List<RecentSessionItem>));

        public static List<RecentSessionItem> GetRecentSessions()
        {
            try
            {
                if (!File.Exists(ConfigFilePath)) return new List<RecentSessionItem>();
                using (var stream = File.OpenRead(ConfigFilePath))
                {
                    var items = (List<RecentSessionItem>)Serializer.Deserialize(stream);
                    if (items != null)
                    {
                        foreach (var item in items)
                        {
                            if (item.Files != null)
                            {
                                foreach (var f in item.Files)
                                {
                                    if (string.IsNullOrWhiteSpace(f.LanguageTag))
                                    {
                                        f.LanguageTag = "MAIN";
                                    }
                                }
                            }
                            string labelsSummary = string.Join(", ", item.Files.Select(f => f.LanguageTag));
                            item.SecondaryInfo = $"{item.Files.Count} file(s) [{labelsSummary}]";
                        }
                    }
                    return items ?? new List<RecentSessionItem>();
                }
            }
            catch
            {
                return new List<RecentSessionItem>();
            }
        }

        public static RecentSessionItem FindRecentSession(CsfSession session)
        {
            if (session == null || session.Documents.Count == 0) return null;

            var validPaths = session.Documents
                .Where(d => !string.IsNullOrEmpty(d.FilePath) && File.Exists(d.FilePath))
                .Select(d => d.FilePath)
                .OrderBy(p => p)
                .ToList();

            if (validPaths.Count == 0) return null;
            string key = string.Join("|", validPaths);

            var recentList = GetRecentSessions();
            return recentList.FirstOrDefault(r =>
            {
                var rPaths = r.Files.Select(f => f.FilePath).OrderBy(p => p);
                return string.Equals(string.Join("|", rPaths), key, StringComparison.OrdinalIgnoreCase);
            });
        }

        public static void AddRecentSession(CsfSession session, int maxRecentSessions = 10, string unpinnedLanguageTags = null, string lastSelectedKeyName = null, string activeTabName = null, string activePinnedLanguageTag = null)
        {
            if (session == null || session.Documents.Count == 0) return;
            int limit = Math.Max(1, Math.Min(100, maxRecentSessions));

            try
            {
                if (!Directory.Exists(AppDataFolder))
                {
                    Directory.CreateDirectory(AppDataFolder);
                }

                var currentRecent = GetRecentSessions();

                var validFiles = session.Documents.Select((d, idx) => new SavedCsfFileInfo
                {
                    LanguageTag = CsfSessionDocument.ResolveLanguageTag(d.LanguageTag, d.Document, idx),
                    TranslationContentLanguage = d.TranslationContentLanguage ?? string.Empty,
                    FilePath = d.FilePath
                }).Where(f => !string.IsNullOrEmpty(f.FilePath) && File.Exists(f.FilePath)).ToList();

                if (validFiles.Count == 0) return;

                string firstFileName = Path.GetFileName(validFiles[0].FilePath);
                string labelsSummary = string.Join(", ", validFiles.Select(f => f.LanguageTag));

                var newSession = new RecentSessionItem
                {
                    PrimaryTitle = validFiles.Count == 1 ? $"📄 {firstFileName}" : $"📂 {firstFileName} + {validFiles.Count - 1} other(s)",
                    SecondaryInfo = $"{validFiles.Count} file(s) [{labelsSummary}]",
                    Timestamp = DateTime.Now,
                    Files = validFiles,
                    UnpinnedLanguageTags = unpinnedLanguageTags ?? string.Empty,
                    LastSelectedKeyName = lastSelectedKeyName ?? string.Empty,
                    ActiveTabName = activeTabName ?? string.Empty,
                    ActivePinnedLanguageTag = activePinnedLanguageTag ?? string.Empty
                };

                // Remove existing session duplicates with same file paths
                var newPathsKey = string.Join("|", validFiles.Select(f => f.FilePath).OrderBy(p => p));
                currentRecent.RemoveAll(r =>
                {
                    var existingKey = string.Join("|", r.Files.Select(f => f.FilePath).OrderBy(p => p));
                    return string.Equals(existingKey, newPathsKey, StringComparison.OrdinalIgnoreCase);
                });

                currentRecent.Insert(0, newSession);

                if (currentRecent.Count > limit)
                {
                    currentRecent = currentRecent.Take(limit).ToList();
                }

                using (var stream = File.Create(ConfigFilePath))
                {
                    Serializer.Serialize(stream, currentRecent);
                }
            }
            catch
            {
                // Ignore config persistence errors
            }
        }

        public static void ClearRecentSessions()
        {
            try
            {
                if (File.Exists(ConfigFilePath))
                {
                    File.Delete(ConfigFilePath);
                }
            }
            catch { }
        }
    }
}
