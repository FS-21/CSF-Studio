using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace CsfStudio.Core
{
    public class SnapshotManifest
    {
        public string TimestampString { get; set; }
        public DateTime CreatedAt { get; set; }
        public string Cause { get; set; }
        public List<string> FileNames { get; set; } = new List<string>();
        public List<string> SourceFilePaths { get; set; } = new List<string>();
        public int TotalKeys { get; set; }
    }

    public class SessionSnapshot
    {
        public string SnapshotFolderPath { get; set; }
        public SnapshotManifest Manifest { get; set; } = new SnapshotManifest();

        public string DisplayName => $"{Manifest.CreatedAt:yyyy-MM-dd HH:mm:ss} ({Manifest.FileNames.Count} files, {Manifest.TotalKeys} keys)";
    }

    public enum BackupDiffType
    {
        Unchanged,
        Modified,
        AddedInMemory,
        DeletedInMemory,
        Renamed
    }

    public class BackupDiffItem
    {
        public string KeyName { get; set; }
        public string CurrentValue { get; set; }
        public string BackupValue { get; set; }
        public string CurrentExtra { get; set; }
        public string BackupExtra { get; set; }
        public BackupDiffType DiffType { get; set; }

        public string StatusDisplay
        {
            get {
                switch (DiffType)
                {
                    case BackupDiffType.Modified: return "🟡 Modified";
                    case BackupDiffType.AddedInMemory: return "🟢 Added in Session";
                    case BackupDiffType.DeletedInMemory: return "🔴 Deleted in Session";
                    case BackupDiffType.Renamed: return "🟣 Renamed";
                    default: return "🟢 Identical";
                }
            }
        }
    }

    public static class BackupManager
    {
        public static string GetBackupDirectory(string baseFilePath, string customBackupFolder = null, bool saveInAppData = false)
        {
            string mainBackupsFolder = ConfigManager.ResolveBackupDirectory(customBackupFolder, saveInAppData);

            if (string.IsNullOrEmpty(baseFilePath)) return Path.Combine(mainBackupsFolder, "DefaultSession");

            string normPath = baseFilePath.Trim().ToLowerInvariant();
            string fileName = Path.GetFileNameWithoutExtension(baseFilePath);
            if (string.IsNullOrEmpty(fileName)) fileName = "Session";

            using (var md5 = System.Security.Cryptography.MD5.Create())
            {
                byte[] bytes = md5.ComputeHash(System.Text.Encoding.UTF8.GetBytes(normPath));
                string hashStr = BitConverter.ToString(bytes).Replace("-", "").Substring(0, 8);
                return Path.Combine(mainBackupsFolder, $"{fileName}_{hashStr}");
            }
        }

        public static SessionSnapshot CreateSessionSnapshot(CsfSession session, string cause = "Manual Save", string customBackupFolder = null, bool saveInAppData = false)
        {
            if (session == null || session.Documents.Count == 0) return null;

            var baseDoc = session.BaseDocument ?? session.Documents.FirstOrDefault();
            string docPathOrName = !string.IsNullOrEmpty(baseDoc?.FilePath) ? baseDoc.FilePath : baseDoc?.FileName;
            string backupDir = GetBackupDirectory(docPathOrName, customBackupFolder, saveInAppData);
            if (string.IsNullOrEmpty(backupDir)) return null;

            try
            {
                if (!Directory.Exists(backupDir))
                {
                    Directory.CreateDirectory(backupDir);
                }

                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string snapshotFolder = Path.Combine(backupDir, $"Snapshot_{timestamp}");
                if (Directory.Exists(snapshotFolder))
                {
                    snapshotFolder = Path.Combine(backupDir, $"Snapshot_{timestamp}_{DateTime.Now.Millisecond}");
                }

                Directory.CreateDirectory(snapshotFolder);

                var manifest = new SnapshotManifest
                {
                    TimestampString = timestamp,
                    CreatedAt = DateTime.Now,
                    Cause = cause,
                    TotalKeys = session.BuildMasterKeyList().Count,
                    SourceFilePaths = session.Documents
                        .Where(d => !string.IsNullOrEmpty(d.FilePath))
                        .Select(d => d.FilePath)
                        .ToList()
                };

                foreach (var sDoc in session.Documents)
                {
                    string safeTag = string.IsNullOrEmpty(sDoc.LanguageTag) ? "DOC" : sDoc.LanguageTag.Trim();
                    string fname = string.IsNullOrEmpty(sDoc.FileName) ? "strings.csf" : sDoc.FileName;
                    string targetFileName = $"{safeTag}_{fname}";

                    string destPath = Path.Combine(snapshotFolder, targetFileName);

                    if (!sDoc.IsModified && !string.IsNullOrEmpty(sDoc.FilePath) && File.Exists(sDoc.FilePath))
                    {
                        try
                        {
                            File.Copy(sDoc.FilePath, destPath, true);
                        }
                        catch
                        {
                            CsfFileHandler.Save(sDoc.Document, destPath);
                        }
                    }
                    else
                    {
                        CsfFileHandler.Save(sDoc.Document, destPath);
                    }
                    manifest.FileNames.Add(targetFileName);
                }

                SaveManifest(snapshotFolder, manifest);

                // Auto-prune old backups according to user configuration
                var config = ConfigManager.LoadConfig();
                PruneOldSnapshots(baseDoc?.FilePath, maxDays: config.AutoDeleteBackupDays, maxSnapshots: config.MaxBackupSnapshots, customBackupFolder: customBackupFolder, saveInAppData: saveInAppData);

                return new SessionSnapshot
                {
                    SnapshotFolderPath = snapshotFolder,
                    Manifest = manifest
                };
            }
            catch
            {
                return null;
            }
        }

        private static void SaveManifest(string snapshotFolder, SnapshotManifest manifest)
        {
            string manifestPath = Path.Combine(snapshotFolder, "manifest.txt");
            using (var writer = new StreamWriter(manifestPath, false, System.Text.Encoding.UTF8))
            {
                writer.WriteLine("; ===========================================================================");
                writer.WriteLine("; CSF STUDIO - SNAPSHOT MANIFEST & AUDIT TRAIL");
                writer.WriteLine("; ===========================================================================");
                writer.WriteLine("; This manifest documents the backup snapshot created by CSF Studio.");
                writer.WriteLine("; ");
                writer.WriteLine("; NOTE ON BACKUP TIMING:");
                writer.WriteLine(";   - The .CSF files in this folder contain the PRE-SAVE state from disk");
                writer.WriteLine(";     right before your new modifications were written to disk.");
                writer.WriteLine("; ");
                writer.WriteLine("; FIELDS EXPLANATION:");
                writer.WriteLine(";   - Timestamp: Date and time of snapshot creation (YYYYMMDD_HHMMSS)");
                writer.WriteLine(";   - CreatedAt: ISO 8601 creation date and time");
                writer.WriteLine(";   - Cause: Trigger event (e.g., 'Save All Documents', 'Auto Backup')");
                writer.WriteLine(";   - TotalKeys: Number of string key labels in session at backup time");
                writer.WriteLine(";   - FileNames: Names of backup CSF files stored in this snapshot");
                writer.WriteLine(";   - SourceFilePaths: Absolute original disk paths of project files");
                writer.WriteLine("; ===========================================================================");
                writer.WriteLine();
                writer.WriteLine($"Timestamp={manifest.TimestampString}");
                writer.WriteLine($"CreatedAt={manifest.CreatedAt:o}");
                writer.WriteLine($"Cause={manifest.Cause}");
                writer.WriteLine($"TotalKeys={manifest.TotalKeys}");
                writer.WriteLine($"FileNames={string.Join(";", manifest.FileNames)}");
                writer.WriteLine($"SourceFilePaths={string.Join(";", manifest.SourceFilePaths)}");
            }
        }

        private static SnapshotManifest LoadManifest(string snapshotFolder)
        {
            string manifestPath = Path.Combine(snapshotFolder, "manifest.txt");
            var manifest = new SnapshotManifest();

            if (File.Exists(manifestPath))
            {
                var lines = File.ReadAllLines(manifestPath);
                foreach (var line in lines)
                {
                    string trimmed = line.Trim();
                    if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith(";")) continue;
                    int eq = trimmed.IndexOf('=');
                    if (eq <= 0) continue;
                    string key = trimmed.Substring(0, eq).Trim();
                    string val = trimmed.Substring(eq + 1).Trim();

                    if (string.Equals(key, "Timestamp", StringComparison.OrdinalIgnoreCase)) manifest.TimestampString = val;
                    else if (string.Equals(key, "CreatedAt", StringComparison.OrdinalIgnoreCase) && DateTime.TryParse(val, out var dt)) manifest.CreatedAt = dt;
                    else if (string.Equals(key, "Cause", StringComparison.OrdinalIgnoreCase)) manifest.Cause = val;
                    else if (string.Equals(key, "TotalKeys", StringComparison.OrdinalIgnoreCase) && int.TryParse(val, out var tk)) manifest.TotalKeys = tk;
                    else if (string.Equals(key, "FileNames", StringComparison.OrdinalIgnoreCase))
                    {
                        manifest.FileNames = val.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries).ToList();
                    }
                    else if (string.Equals(key, "SourceFilePaths", StringComparison.OrdinalIgnoreCase))
                    {
                        manifest.SourceFilePaths = val.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries).ToList();
                    }
                }
            }
            else
            {
                var dirInfo = new DirectoryInfo(snapshotFolder);
                manifest.CreatedAt = dirInfo.CreationTime;
                manifest.TimestampString = dirInfo.Name;
                manifest.FileNames = Directory.GetFiles(snapshotFolder, "*.csf").Select(Path.GetFileName).ToList();

                if (manifest.FileNames.Count == 0)
                {
                    // Clean up broken/orphan directory without CSF files
                    try { Directory.Delete(snapshotFolder, true); } catch { }
                }
                else
                {
                    try
                    {
                        var firstCsf = Directory.GetFiles(snapshotFolder, "*.csf").FirstOrDefault();
                        if (firstCsf != null)
                        {
                            var doc = CsfFileHandler.Load(firstCsf);
                            manifest.TotalKeys = doc?.Labels?.Count ?? 0;
                        }
                    }
                    catch { }
                }
            }

            return manifest;
        }

        public static List<SessionSnapshot> GetAvailableSnapshots(string baseFilePath, string customBackupFolder = null, bool saveInAppData = false)
        {
            var result = new List<SessionSnapshot>();
            if (string.IsNullOrEmpty(baseFilePath)) return result;

            string targetBackupDir = GetBackupDirectory(baseFilePath, customBackupFolder, saveInAppData);
            if (!Directory.Exists(targetBackupDir)) return result;

            var snapshotDirs = Directory.GetDirectories(targetBackupDir, "Snapshot_*");
            foreach (var sSnapDir in snapshotDirs)
            {
                if (!Directory.Exists(sSnapDir)) continue;
                var manifest = LoadManifest(sSnapDir);
                if (manifest.FileNames.Count == 0 && !Directory.Exists(sSnapDir)) continue;

                result.Add(new SessionSnapshot
                {
                    SnapshotFolderPath = sSnapDir,
                    Manifest = manifest
                });
            }

            return result.OrderByDescending(s => s.Manifest.CreatedAt).ToList();
        }

        private static void ForceDeleteDirectory(string targetDir)
        {
            if (string.IsNullOrEmpty(targetDir) || !Directory.Exists(targetDir)) return;

            GC.Collect();
            GC.WaitForPendingFinalizers();

            try
            {
                var dirInfo = new DirectoryInfo(targetDir);
                foreach (var file in dirInfo.GetFiles("*", SearchOption.AllDirectories))
                {
                    try
                    {
                        file.Attributes = FileAttributes.Normal;
                        file.Delete();
                    }
                    catch { }
                }

                foreach (var subDir in dirInfo.GetDirectories("*", SearchOption.AllDirectories))
                {
                    try
                    {
                        subDir.Attributes = FileAttributes.Normal;
                        subDir.Delete(true);
                    }
                    catch { }
                }

                dirInfo.Attributes = FileAttributes.Normal;
                dirInfo.Delete(true);
            }
            catch
            {
                try { Directory.Delete(targetDir, true); } catch { }
            }
        }

        public static bool DeleteSnapshot(SessionSnapshot snapshot)
        {
            if (snapshot == null || string.IsNullOrEmpty(snapshot.SnapshotFolderPath)) return false;
            try
            {
                if (Directory.Exists(snapshot.SnapshotFolderPath))
                {
                    ForceDeleteDirectory(snapshot.SnapshotFolderPath);
                    return !Directory.Exists(snapshot.SnapshotFolderPath);
                }
            }
            catch { }
            return false;
        }

        public static bool DeleteAllSnapshots(string baseFilePath, string customBackupDir = null, bool saveInAppData = false)
        {
            var snapshots = GetAvailableSnapshots(baseFilePath, customBackupDir, saveInAppData);
            if (snapshots.Count == 0) return false;

            foreach (var snap in snapshots)
            {
                try
                {
                    if (!string.IsNullOrEmpty(snap.SnapshotFolderPath) && Directory.Exists(snap.SnapshotFolderPath))
                    {
                        ForceDeleteDirectory(snap.SnapshotFolderPath);
                    }
                }
                catch { }
            }
            return true;
        }

        public static void PruneOldSnapshots(string baseFilePath, int maxDays = 30, int maxSnapshots = 30, string customBackupFolder = null, bool saveInAppData = false)
        {
            string backupDir = GetBackupDirectory(baseFilePath, customBackupFolder, saveInAppData);
            if (string.IsNullOrEmpty(backupDir) || !Directory.Exists(backupDir)) return;

            try
            {
                var snapshotDirs = Directory.GetDirectories(backupDir, "Snapshot_*")
                    .Select(d => new { Path = d, Created = Directory.GetCreationTime(d) })
                    .OrderByDescending(x => x.Created)
                    .ToList();

                DateTime thresholdDate = DateTime.Now.AddDays(-maxDays);

                for (int i = 0; i < snapshotDirs.Count; i++)
                {
                    var snap = snapshotDirs[i];
                    bool exceedsCount = (i >= maxSnapshots);
                    bool exceedsAge = (snap.Created < thresholdDate);

                    if (exceedsCount || exceedsAge)
                    {
                        try
                        {
                            Directory.Delete(snap.Path, true);
                        }
                        catch { }
                    }
                }
            }
            catch { }
        }


        public static List<BackupDiffItem> CompareSnapshotDocWithCurrent(CsfDocument currentDoc, CsfDocument backupDoc)
        {
            var result = new List<BackupDiffItem>();
            if (currentDoc == null && backupDoc == null) return result;

            var currMap = new Dictionary<string, CsfLabel>(StringComparer.OrdinalIgnoreCase);
            if (currentDoc != null)
            {
                foreach (var l in currentDoc.Labels)
                {
                    if (l != null && !string.IsNullOrEmpty(l.Name) && !currMap.ContainsKey(l.Name))
                        currMap[l.Name] = l;
                }
            }

            var bakMap = new Dictionary<string, CsfLabel>(StringComparer.OrdinalIgnoreCase);
            if (backupDoc != null)
            {
                foreach (var l in backupDoc.Labels)
                {
                    if (l != null && !string.IsNullOrEmpty(l.Name) && !bakMap.ContainsKey(l.Name))
                        bakMap[l.Name] = l;
                }
            }

            var allKeys = currMap.Keys.Union(bakMap.Keys, StringComparer.OrdinalIgnoreCase).OrderBy(k => k).ToList();

            foreach (var key in allKeys)
            {
                bool inCurr = currMap.TryGetValue(key, out var cLbl);
                bool inBak = bakMap.TryGetValue(key, out var bLbl);

                string cVal = cLbl?.FirstValue ?? string.Empty;
                string bVal = bLbl?.FirstValue ?? string.Empty;
                string cExtra = cLbl?.FirstExtraValue;
                string bExtra = bLbl?.FirstExtraValue;

                BackupDiffType type;
                if (!inBak && inCurr)
                {
                    type = BackupDiffType.AddedInMemory;
                }
                else if (inBak && !inCurr)
                {
                    type = BackupDiffType.DeletedInMemory;
                }
                else if (!string.Equals(cVal, bVal, StringComparison.Ordinal) || !string.Equals(cExtra, bExtra, StringComparison.OrdinalIgnoreCase))
                {
                    type = BackupDiffType.Modified;
                }
                else
                {
                    type = BackupDiffType.Unchanged;
                }

                result.Add(new BackupDiffItem
                {
                    KeyName = key,
                    CurrentValue = cVal,
                    BackupValue = bVal,
                    CurrentExtra = cExtra,
                    BackupExtra = bExtra,
                    DiffType = type
                });
            }

            // Consolidate Deleted + Added pairs with matching content into Renamed items (O(N) indexed lookup)
            var addedItems = result.Where(x => x.DiffType == BackupDiffType.AddedInMemory && !string.IsNullOrEmpty(x.CurrentValue)).ToList();
            var deletedItems = result.Where(x => x.DiffType == BackupDiffType.DeletedInMemory && !string.IsNullOrEmpty(x.BackupValue)).ToList();

            if (addedItems.Count > 0 && deletedItems.Count > 0)
            {
                var deletedByVal = new Dictionary<string, List<BackupDiffItem>>(StringComparer.Ordinal);
                foreach (var d in deletedItems)
                {
                    if (string.IsNullOrEmpty(d.BackupValue)) continue;
                    if (!deletedByVal.TryGetValue(d.BackupValue, out var list))
                    {
                        list = new List<BackupDiffItem>();
                        deletedByVal[d.BackupValue] = list;
                    }
                    list.Add(d);
                }

                var itemsToRemove = new HashSet<BackupDiffItem>();

                foreach (var added in addedItems)
                {
                    if (string.IsNullOrEmpty(added.CurrentValue)) continue;
                    if (deletedByVal.TryGetValue(added.CurrentValue, out var list))
                    {
                        var matchDeleted = list.FirstOrDefault(d =>
                            !itemsToRemove.Contains(d) &&
                            string.Equals(added.CurrentExtra ?? string.Empty, d.BackupExtra ?? string.Empty, StringComparison.OrdinalIgnoreCase));

                        if (matchDeleted != null)
                        {
                            added.DiffType = BackupDiffType.Renamed;
                            added.KeyName = $"{matchDeleted.KeyName} ➔ {added.KeyName}";
                            added.BackupValue = matchDeleted.BackupValue;
                            added.BackupExtra = matchDeleted.BackupExtra;
                            itemsToRemove.Add(matchDeleted);
                        }
                    }
                }

                foreach (var rem in itemsToRemove)
                {
                    result.Remove(rem);
                }
            }

            return result;
        }
    }
}
