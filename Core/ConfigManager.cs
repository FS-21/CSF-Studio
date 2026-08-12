using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace CsfStudio.Core
{
    public enum StartupMainTabOption
    {
        MasterKeysView = 0,
        PlainKeyEditor = 1,
        RememberLastActive = 2
    }

    public class AppConfig
    {
        public bool SaveInAppData { get; set; } = false;
        public bool InspectorMultilineTabs { get; set; } = false;
        public int NotificationToastDurationMs { get; set; } = 5000;
        public CsfLanguage DefaultLanguage { get; set; } = CsfLanguage.EnglishUS;
        public string LastActiveMainTabName { get; set; } = "tabMaster";
        public StartupMainTabOption DefaultStartupMainTab { get; set; } = StartupMainTabOption.MasterKeysView;
        public int MaxMultiKeyDisplayCount { get; set; } = 10;
        public string UnpinnedLanguageTags { get; set; } = string.Empty;
        public string LastSelectedKeyName { get; set; } = string.Empty;

        // Filter Settings
        public bool KeyRegexMode { get; set; } = false;
        public bool ValRegexMode { get; set; } = false;
        public bool FilterLogicAnd { get; set; } = false;
        public int SelectedStatusFilterIndex { get; set; } = 0;
        public int MaxSearchHistoryItems { get; set; } = 10;
        public int MaxRecentSessionsItems { get; set; } = 10;
        public int MaxRecentEditedKeysItems { get; set; } = 10;

        // Mode-Specific Histories (Max 10 per mode)
        public List<string> KeySearchHistoryPlain { get; set; } = new List<string>();
        public List<string> KeySearchHistoryRegex { get; set; } = new List<string>();
        public List<string> ValueSearchHistoryPlain { get; set; } = new List<string>();
        public List<string> ValueSearchHistoryRegex { get; set; } = new List<string>();

        // Find & Replace Settings
        public List<string> FindHistoryPlain { get; set; } = new List<string>();
        public List<string> FindHistoryRegex { get; set; } = new List<string>();
        public List<string> ReplaceHistoryPlain { get; set; } = new List<string>();
        public List<string> ReplaceHistoryRegex { get; set; } = new List<string>();

        // Batch Rename History (uses MaxSearchHistoryItems)
        public List<string> BatchFindHistoryPlain { get; set; } = new List<string>();
        public List<string> BatchFindHistoryRegex { get; set; } = new List<string>();
        public List<string> BatchReplaceHistoryPlain { get; set; } = new List<string>();
        public List<string> BatchReplaceHistoryRegex { get; set; } = new List<string>();
        public bool FindMatchCase { get; set; } = false;
        public bool FindUseRegex { get; set; } = false;
        public bool FindSearchKey { get; set; } = true;
        public bool FindSearchValue { get; set; } = true;

        // INI Scanner Settings
        public string IniScanProperties { get; set; } = "UIName;EnemyUIName;WeaponUIName;UIDescription;BuildText;LoadScreenText;Briefing;WinText;LoseText;Text;Message;ToolTip;HarvesterCounter.Label;CostLabel;PowerLabel;PowerBlackoutLabel;TimeLabel;SWShotsFormat;Ranking;ShowBriefingResumeButton;CSF";

        // Undo Settings
        public int MaxUndoLevels { get; set; } = 100;

        // Window Geometry
        public bool IsMaximized { get; set; } = false;
        public int Width { get; set; } = 1020;
        public int Height { get; set; } = 680;
        public int MasterKeysViewPanelWidth { get; set; } = 200;
        public int MasterKeysViewInspectorHeight { get; set; } = 380;
        public int PlainKeyViewPanelWidth { get; set; } = 220;
        public bool ShowCategoryTree { get; set; } = true;
        public bool RememberPanelLayoutPositions { get; set; } = true;

        // Backup Settings
        public bool AutoCreateBackups { get; set; } = true;
        public int MaxBackupSnapshots { get; set; } = 10;
        public int AutoDeleteBackupDays { get; set; } = 30;
        public string BackupDirectoryPath { get; set; } = "Backups";

        // Preferences & Paths
        public bool SortByBinarySequence { get; set; } = true;
        public string DefaultCategoryPrefix { get; set; } = "CSF_";
        public string LastOpenDirectory { get; set; } = string.Empty;
        public string LastIniScanDirectory { get; set; } = string.Empty;
        public int LastSelectedCodepage { get; set; } = 1252;
    }

    public static class ConfigManager
    {
        private static readonly string AppDataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CSF Studio");
        private static readonly string AppDataIniPath = Path.Combine(AppDataDir, "settings.ini");
        private static readonly string LocalIniPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.ini");

        public static string GetActiveIniPath(bool saveInAppData)
        {
            if (saveInAppData)
            {
                if (!Directory.Exists(AppDataDir))
                {
                    Directory.CreateDirectory(AppDataDir);
                }
                return AppDataIniPath;
            }
            return LocalIniPath;
        }

        public static string ResolveBackupDirectory(string rawPath, bool saveInAppData)
        {
            if (string.IsNullOrWhiteSpace(rawPath)) rawPath = "Backups";
            rawPath = rawPath.Trim();

            if (Path.IsPathRooted(rawPath))
            {
                return rawPath;
            }

            string rootDir = saveInAppData 
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CSF Studio") 
                : AppDomain.CurrentDomain.BaseDirectory;

            return Path.GetFullPath(Path.Combine(rootDir, rawPath));
        }

        public static AppConfig LoadConfig()
        {
            var config = new AppConfig();
            string iniPath = File.Exists(LocalIniPath) ? LocalIniPath : (File.Exists(AppDataIniPath) ? AppDataIniPath : LocalIniPath);

            if (!File.Exists(iniPath))
            {
                SaveConfig(config);
                return config;
            }

            try
            {
                var lines = File.ReadAllLines(iniPath, Encoding.UTF8);
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

                    int eqIdx = line.IndexOf('=');
                    if (eqIdx <= 0) continue;

                    string key = line.Substring(0, eqIdx).Trim();
                    string val = line.Substring(eqIdx + 1).Trim();

                    switch (currentSection)
                    {
                        case "AppSettings":
                            if (key == "SaveInAppData") config.SaveInAppData = ParseBool(val, false);
                            else if (key == "InspectorMultilineTabs") config.InspectorMultilineTabs = ParseBool(val, false);
                            else if (key == "NotificationToastDurationMs") config.NotificationToastDurationMs = int.TryParse(val, out int ntd) ? Math.Max(1000, Math.Min(30000, ntd)) : 5000;
                            else if (key == "DefaultLanguage")
                            {
                                if (int.TryParse(val, out int dli) && Enum.IsDefined(typeof(CsfLanguage), dli)) config.DefaultLanguage = (CsfLanguage)dli;
                                else if (Enum.TryParse<CsfLanguage>(val, true, out var dl)) config.DefaultLanguage = dl;
                            }
                            else if (key == "LastActiveMainTabName" && !string.IsNullOrWhiteSpace(val)) config.LastActiveMainTabName = val;
                            else if (key == "DefaultStartupMainTab" && int.TryParse(val, out int dst) && Enum.IsDefined(typeof(StartupMainTabOption), dst)) config.DefaultStartupMainTab = (StartupMainTabOption)dst;
                            else if (key == "MaxMultiKeyDisplayCount" && int.TryParse(val, out int mmk)) config.MaxMultiKeyDisplayCount = Math.Max(1, Math.Min(100, mmk));
                            else if (key == "UnpinnedLanguageTags") config.UnpinnedLanguageTags = val;
                            else if (key == "LastSelectedKeyName") config.LastSelectedKeyName = val;
                            break;

                        case "FilterSettings":
                            if (key == "KeyRegexMode") config.KeyRegexMode = ParseBool(val, false);
                            else if (key == "ValRegexMode") config.ValRegexMode = ParseBool(val, false);
                            else if (key == "FilterLogicAnd") config.FilterLogicAnd = ParseBool(val, false);
                            else if (key == "SelectedStatusFilterIndex") config.SelectedStatusFilterIndex = int.TryParse(val, out int sfi) ? Math.Max(0, Math.Min(3, sfi)) : 0;
                            else if (key == "MaxSearchHistoryItems") config.MaxSearchHistoryItems = int.TryParse(val, out int mshi) ? Math.Max(1, Math.Min(100, mshi)) : 10;
                            else if (key == "MaxRecentSessionsItems") config.MaxRecentSessionsItems = int.TryParse(val, out int mrsi) ? Math.Max(1, Math.Min(100, mrsi)) : 10;
                            else if (key == "MaxRecentEditedKeysItems") config.MaxRecentEditedKeysItems = int.TryParse(val, out int mreki) ? Math.Max(1, Math.Min(100, mreki)) : 10;
                            else if (key == "KeySearchHistoryPlain") config.KeySearchHistoryPlain = ParseHistory(val, config.MaxSearchHistoryItems);
                            else if (key == "KeySearchHistoryRegex") config.KeySearchHistoryRegex = ParseHistory(val, config.MaxSearchHistoryItems);
                            else if (key == "ValueSearchHistoryPlain") config.ValueSearchHistoryPlain = ParseHistory(val, config.MaxSearchHistoryItems);
                            else if (key == "ValueSearchHistoryRegex") config.ValueSearchHistoryRegex = ParseHistory(val, config.MaxSearchHistoryItems);
                            else if (key == "KeySearchHistory" && config.KeySearchHistoryPlain.Count == 0) config.KeySearchHistoryPlain = ParseHistory(val, config.MaxSearchHistoryItems);
                            else if (key == "ValueSearchHistory" && config.ValueSearchHistoryPlain.Count == 0) config.ValueSearchHistoryPlain = ParseHistory(val, config.MaxSearchHistoryItems);
                            break;

                        case "FindReplaceSettings":
                            if (key == "FindHistoryPlain") config.FindHistoryPlain = ParseHistory(val, config.MaxSearchHistoryItems);
                            else if (key == "FindHistoryRegex") config.FindHistoryRegex = ParseHistory(val, config.MaxSearchHistoryItems);
                            else if (key == "ReplaceHistoryPlain") config.ReplaceHistoryPlain = ParseHistory(val, config.MaxSearchHistoryItems);
                            else if (key == "ReplaceHistoryRegex") config.ReplaceHistoryRegex = ParseHistory(val, config.MaxSearchHistoryItems);
                            else if (key == "FindHistory" && config.FindHistoryPlain.Count == 0) config.FindHistoryPlain = ParseHistory(val, config.MaxSearchHistoryItems);
                            else if (key == "ReplaceHistory" && config.ReplaceHistoryPlain.Count == 0) config.ReplaceHistoryPlain = ParseHistory(val, config.MaxSearchHistoryItems);
                            else if (key == "MatchCase") config.FindMatchCase = ParseBool(val, false);
                            else if (key == "UseRegex") config.FindUseRegex = ParseBool(val, false);
                            else if (key == "SearchKey") config.FindSearchKey = ParseBool(val, true);
                            else if (key == "SearchValue") config.FindSearchValue = ParseBool(val, true);
                            break;

                        case "BatchRenameSettings":
                            if (key == "BatchFindHistoryPlain") config.BatchFindHistoryPlain = ParseHistory(val, config.MaxSearchHistoryItems);
                            else if (key == "BatchFindHistoryRegex") config.BatchFindHistoryRegex = ParseHistory(val, config.MaxSearchHistoryItems);
                            else if (key == "BatchReplaceHistoryPlain") config.BatchReplaceHistoryPlain = ParseHistory(val, config.MaxSearchHistoryItems);
                            else if (key == "BatchReplaceHistoryRegex") config.BatchReplaceHistoryRegex = ParseHistory(val, config.MaxSearchHistoryItems);
                            break;

                        case "IniScannerSettings":
                            if (key == "IniScanProperties" && !string.IsNullOrWhiteSpace(val)) config.IniScanProperties = val;
                            break;

                        case "UndoSettings":
                            if (key == "MaxUndoLevels") config.MaxUndoLevels = int.TryParse(val, out int mul) ? Math.Max(10, Math.Min(1000, mul)) : 100;
                            break;

                        case "WindowSettings":
                            if (key == "IsMaximized") config.IsMaximized = ParseBool(val, false);
                            else if (key == "Width") config.Width = int.TryParse(val, out int w) ? Math.Max(600, w) : 1020;
                            else if (key == "Height") config.Height = int.TryParse(val, out int h) ? Math.Max(400, h) : 680;
                            else if (key == "MasterKeysViewPanelWidth") config.MasterKeysViewPanelWidth = int.TryParse(val, out int sm) ? sm : 200;
                            else if (key == "MasterKeysViewInspectorHeight") config.MasterKeysViewInspectorHeight = int.TryParse(val, out int smd) ? smd : 380;
                            else if (key == "PlainKeyViewPanelWidth") config.PlainKeyViewPanelWidth = int.TryParse(val, out int skw) ? Math.Max(150, Math.Min(600, skw)) : 220;
                            else if (key == "ShowCategoryTree") config.ShowCategoryTree = ParseBool(val, true);
                            else if (key == "RememberPanelLayoutPositions") config.RememberPanelLayoutPositions = ParseBool(val, true);
                            break;

                        case "BackupSettings":
                            if (key == "AutoCreateBackups") config.AutoCreateBackups = ParseBool(val, true);
                            else if (key == "MaxBackupSnapshots") config.MaxBackupSnapshots = int.TryParse(val, out int mbs) ? Math.Max(1, mbs) : 10;
                            else if (key == "AutoDeleteBackupDays") config.AutoDeleteBackupDays = int.TryParse(val, out int adbd) ? Math.Max(1, Math.Min(365, adbd)) : 30;
                            else if (key == "BackupDirectoryPath" && !string.IsNullOrWhiteSpace(val)) config.BackupDirectoryPath = val;
                            break;

                        case "PathPreferences":
                            if (key == "SortByBinarySequence") config.SortByBinarySequence = ParseBool(val, true);
                            else if (key == "LastOpenDirectory") config.LastOpenDirectory = val;
                            else if (key == "LastIniScanDirectory") config.LastIniScanDirectory = val;
                            else if (key == "LastSelectedCodepage") config.LastSelectedCodepage = int.TryParse(val, out int cp) ? cp : 1252;
                            else if (key == "DefaultCategoryPrefix" && !string.IsNullOrWhiteSpace(val)) config.DefaultCategoryPrefix = val.ToUpperInvariant();
                            break;
                    }
                }
            }
            catch { }

            return config;
        }

        public static void SaveConfig(AppConfig config)
        {
            if (config == null) return;

            string targetPath = GetActiveIniPath(config.SaveInAppData);

            var sb = new StringBuilder();
            sb.AppendLine("; ==============================================================================");
            sb.AppendLine("; CSF STUDIO CONFIGURATION FILE");
            sb.AppendLine("; Command & Conquer String Table (.CSF) Editor & Session Manager");
            sb.AppendLine("; ==============================================================================");
            sb.AppendLine();

            sb.AppendLine("[AppSettings]");
            sb.AppendLine("; Controls whether settings.ini is stored in %APPDATA%\\CSFStudio or in the local application folder.");
            sb.AppendLine("; Type: Boolean (true / false | yes / no | 1 / 0)");
            sb.AppendLine("; Default: true");
            sb.AppendLine($"SaveInAppData={config.SaveInAppData.ToString().ToLowerInvariant()}");
            sb.AppendLine($"InspectorMultilineTabs={config.InspectorMultilineTabs.ToString().ToLowerInvariant()}");
            sb.AppendLine();
            sb.AppendLine("; Duration in milliseconds for on-screen notification toast messages.");
            sb.AppendLine("; Type: Integer (Range: 1000 to 30000 ms)");
            sb.AppendLine("; Default: 5000");
            sb.AppendLine($"NotificationToastDurationMs={config.NotificationToastDurationMs}");
            sb.AppendLine();
            sb.AppendLine("; Default 32-bit binary language header ID assigned at offset 0x14 when creating new CSF files.");
            sb.AppendLine("; Type: Integer (DWORD Enum)");
            sb.AppendLine("; Values: 0=EnglishUS, 1=EnglishUK, 2=German, 3=French, 4=Spanish, 5=Italian, 6=Japanese, 7=Jabberwock, 8=Korean, 9=Chinese");
            sb.AppendLine("; Default: 0");
            sb.AppendLine($"DefaultLanguage={(int)config.DefaultLanguage}");
            sb.AppendLine();
            sb.AppendLine($"LastActiveMainTabName={config.LastActiveMainTabName}");
            sb.AppendLine($"DefaultStartupMainTab={(int)config.DefaultStartupMainTab}");
            sb.AppendLine($"MaxMultiKeyDisplayCount={config.MaxMultiKeyDisplayCount}");
            sb.AppendLine($"UnpinnedLanguageTags={config.UnpinnedLanguageTags ?? ""}");
            sb.AppendLine($"LastSelectedKeyName={config.LastSelectedKeyName ?? ""}");
            sb.AppendLine();

            sb.AppendLine("[FilterSettings]");
            sb.AppendLine("; Enable Regular Expression matching mode for the key search box in the main toolbar.");
            sb.AppendLine("; Type: Boolean (true / false)");
            sb.AppendLine($"KeyRegexMode={config.KeyRegexMode.ToString().ToLowerInvariant()}");
            sb.AppendLine();
            sb.AppendLine("; Enable Regular Expression matching mode for the value text search box in the main toolbar.");
            sb.AppendLine("; Type: Boolean (true / false)");
            sb.AppendLine($"ValRegexMode={config.ValRegexMode.ToString().ToLowerInvariant()}");
            sb.AppendLine();
            sb.AppendLine("; Logical combination filter logic for combining key name and value text filters.");
            sb.AppendLine("; Type: Boolean (true = AND / false = OR)");
            sb.AppendLine($"FilterLogicAnd={config.FilterLogicAnd.ToString().ToLowerInvariant()}");
            sb.AppendLine();
            sb.AppendLine("; Active dropdown selection index for status filtering (0 = All Labels, 1 = Unsaved, 2 = Missing, 3 = Complete).");
            sb.AppendLine("; Type: Integer (Range: 0 to 3)");
            sb.AppendLine($"SelectedStatusFilterIndex={config.SelectedStatusFilterIndex}");
            sb.AppendLine();
            sb.AppendLine("; Maximum number of query entries stored in search history dropdown lists.");
            sb.AppendLine("; Type: Integer (Range: 1 to 100)");
            sb.AppendLine($"MaxSearchHistoryItems={config.MaxSearchHistoryItems}");
            sb.AppendLine();
            sb.AppendLine("; Maximum number of recent session projects stored in the Recent Sessions list.");
            sb.AppendLine("; Type: Integer (Range: 1 to 50)");
            sb.AppendLine($"MaxRecentSessionsItems={config.MaxRecentSessionsItems}");
            sb.AppendLine();
            sb.AppendLine("; Maximum number of recently edited key names stored in history.");
            sb.AppendLine("; Type: Integer (Range: 1 to 50)");
            sb.AppendLine($"MaxRecentEditedKeysItems={config.MaxRecentEditedKeysItems}");
            sb.AppendLine();
            sb.AppendLine("; Search history list for plain text key searches (items separated by pipe '|').");
            sb.AppendLine("; Type: String (Pipe-delimited list)");
            sb.AppendLine($"KeySearchHistoryPlain={FormatHistory(config.KeySearchHistoryPlain, config.MaxSearchHistoryItems)}");
            sb.AppendLine();
            sb.AppendLine("; Search history list for regex key searches (items separated by pipe '|').");
            sb.AppendLine("; Type: String (Pipe-delimited list)");
            sb.AppendLine($"KeySearchHistoryRegex={FormatHistory(config.KeySearchHistoryRegex, config.MaxSearchHistoryItems)}");
            sb.AppendLine();
            sb.AppendLine("; Search history list for plain text value searches (items separated by pipe '|').");
            sb.AppendLine("; Type: String (Pipe-delimited list)");
            sb.AppendLine($"ValueSearchHistoryPlain={FormatHistory(config.ValueSearchHistoryPlain, config.MaxSearchHistoryItems)}");
            sb.AppendLine();
            sb.AppendLine("; Search history list for regex value searches (items separated by pipe '|').");
            sb.AppendLine("; Type: String (Pipe-delimited list)");
            sb.AppendLine($"ValueSearchHistoryRegex={FormatHistory(config.ValueSearchHistoryRegex, config.MaxSearchHistoryItems)}");
            sb.AppendLine();

            sb.AppendLine("[FindReplaceSettings]");
            sb.AppendLine("; History list for plain text find queries (items separated by pipe '|').");
            sb.AppendLine("; Type: String (Pipe-delimited list)");
            sb.AppendLine($"FindHistoryPlain={FormatHistory(config.FindHistoryPlain, config.MaxSearchHistoryItems)}");
            sb.AppendLine();
            sb.AppendLine("; History list for regex find queries (items separated by pipe '|').");
            sb.AppendLine("; Type: String (Pipe-delimited list)");
            sb.AppendLine($"FindHistoryRegex={FormatHistory(config.FindHistoryRegex, config.MaxSearchHistoryItems)}");
            sb.AppendLine();
            sb.AppendLine("; History list for plain text replacement values (items separated by pipe '|').");
            sb.AppendLine("; Type: String (Pipe-delimited list)");
            sb.AppendLine($"ReplaceHistoryPlain={FormatHistory(config.ReplaceHistoryPlain, config.MaxSearchHistoryItems)}");
            sb.AppendLine();
            sb.AppendLine("; History list for regex replacement values (items separated by pipe '|').");
            sb.AppendLine("; Type: String (Pipe-delimited list)");
            sb.AppendLine($"ReplaceHistoryRegex={FormatHistory(config.ReplaceHistoryRegex, config.MaxSearchHistoryItems)}");
            sb.AppendLine();
            sb.AppendLine("; Match Case option state in Find & Replace dialog.");
            sb.AppendLine("; Type: Boolean (true / false)");
            sb.AppendLine($"MatchCase={config.FindMatchCase.ToString().ToLowerInvariant()}");
            sb.AppendLine();
            sb.AppendLine("; Use Regular Expressions option state in Find & Replace dialog.");
            sb.AppendLine("; Type: Boolean (true / false)");
            sb.AppendLine($"UseRegex={config.FindUseRegex.ToString().ToLowerInvariant()}");
            sb.AppendLine();
            sb.AppendLine("; Search within key label names in Find & Replace dialog.");
            sb.AppendLine("; Type: Boolean (true / false)");
            sb.AppendLine($"SearchKey={config.FindSearchKey.ToString().ToLowerInvariant()}");
            sb.AppendLine();
            sb.AppendLine("; Search within text string values in Find & Replace dialog.");
            sb.AppendLine("; Type: Boolean (true / false)");
            sb.AppendLine($"SearchValue={config.FindSearchValue.ToString().ToLowerInvariant()}");
            sb.AppendLine();

            sb.AppendLine("[BatchRenameSettings]");
            sb.AppendLine("; History list for plain text batch rename find patterns (items separated by pipe '|').");
            sb.AppendLine("; Type: String (Pipe-delimited list)");
            sb.AppendLine($"BatchFindHistoryPlain={FormatHistory(config.BatchFindHistoryPlain, config.MaxSearchHistoryItems)}");
            sb.AppendLine();
            sb.AppendLine("; History list for regex batch rename find patterns (items separated by pipe '|').");
            sb.AppendLine("; Type: String (Pipe-delimited list)");
            sb.AppendLine($"BatchFindHistoryRegex={FormatHistory(config.BatchFindHistoryRegex, config.MaxSearchHistoryItems)}");
            sb.AppendLine();
            sb.AppendLine("; History list for plain text batch rename replacement strings (items separated by pipe '|').");
            sb.AppendLine("; Type: String (Pipe-delimited list)");
            sb.AppendLine($"BatchReplaceHistoryPlain={FormatHistory(config.BatchReplaceHistoryPlain, config.MaxSearchHistoryItems)}");
            sb.AppendLine();
            sb.AppendLine("; History list for regex batch rename replacement strings (items separated by pipe '|').");
            sb.AppendLine("; Type: String (Pipe-delimited list)");
            sb.AppendLine($"BatchReplaceHistoryRegex={FormatHistory(config.BatchReplaceHistoryRegex, config.MaxSearchHistoryItems)}");
            sb.AppendLine();

            sb.AppendLine("[IniScannerSettings]");
            sb.AppendLine("; Semicolon-separated list of .INI and .MAP property tag names scanned for CSF string key references.");
            sb.AppendLine("; Type: String (Semicolon-delimited list)");
            sb.AppendLine($"IniScanProperties={config.IniScanProperties}");
            sb.AppendLine();

            sb.AppendLine("[UndoSettings]");
            sb.AppendLine("; Maximum number of undo history levels stored for Ctrl+Z restoration.");
            sb.AppendLine("; Type: Integer (Range: 10 to 500)");
            sb.AppendLine("; Default: 100");
            sb.AppendLine($"MaxUndoLevels={config.MaxUndoLevels}");
            sb.AppendLine();

            sb.AppendLine("[WindowSettings]");
            sb.AppendLine("; Main window maximized state on application shutdown.");
            sb.AppendLine("; Type: Boolean (true / false)");
            sb.AppendLine($"IsMaximized={config.IsMaximized.ToString().ToLowerInvariant()}");
            sb.AppendLine();
            sb.AppendLine("; Main window width in pixels.");
            sb.AppendLine("; Type: Integer (Pixels)");
            sb.AppendLine($"Width={config.Width}");
            sb.AppendLine();
            sb.AppendLine("; Main window height in pixels.");
            sb.AppendLine("; Type: Integer (Pixels)");
            sb.AppendLine($"Height={config.Height}");
            sb.AppendLine();
            sb.AppendLine("; Width in pixels of the left category panel in Master Keys View.");
            sb.AppendLine("; Type: Integer (Pixels)");
            sb.AppendLine($"MasterKeysViewPanelWidth={config.MasterKeysViewPanelWidth}");
            sb.AppendLine();
            sb.AppendLine("; Height in pixels of the bottom Detail Inspector panel in Master Keys View.");
            sb.AppendLine("; Type: Integer (Pixels)");
            sb.AppendLine($"MasterKeysViewInspectorHeight={config.MasterKeysViewInspectorHeight}");
            sb.AppendLine();
            sb.AppendLine("; Width in pixels of the left key list panel in Plain Keys View.");
            sb.AppendLine("; Type: Integer (Pixels)");
            sb.AppendLine($"PlainKeyViewPanelWidth={config.PlainKeyViewPanelWidth}");
            sb.AppendLine();
            sb.AppendLine("; Visibility state of category tree sidebar.");
            sb.AppendLine("; Type: Boolean (true / false)");
            sb.AppendLine($"ShowCategoryTree={config.ShowCategoryTree.ToString().ToLowerInvariant()}");
            sb.AppendLine();
            sb.AppendLine("; Remembers panel splitter positions and window dimensions on exit.");
            sb.AppendLine("; Type: Boolean (true / false)");
            sb.AppendLine($"RememberPanelLayoutPositions={config.RememberPanelLayoutPositions.ToString().ToLowerInvariant()}");
            sb.AppendLine();

            sb.AppendLine("[BackupSettings]");
            sb.AppendLine("; Automatically create backup snapshots of open CSF files before saving.");
            sb.AppendLine("; Type: Boolean (true / false)");
            sb.AppendLine("; Default: true");
            sb.AppendLine($"AutoCreateBackups={config.AutoCreateBackups.ToString().ToLowerInvariant()}");
            sb.AppendLine();
            sb.AppendLine("; Maximum number of backup snapshots preserved per CSF session.");
            sb.AppendLine("; Type: Integer (Range: 1 to 100)");
            sb.AppendLine("; Default: 10");
            sb.AppendLine($"MaxBackupSnapshots={config.MaxBackupSnapshots}");
            sb.AppendLine();
            sb.AppendLine("; Retention threshold in days before older backup snapshots are automatically purged.");
            sb.AppendLine("; Type: Integer (Range: 1 to 365)");
            sb.AppendLine("; Default: 30");
            sb.AppendLine($"AutoDeleteBackupDays={config.AutoDeleteBackupDays}");
            sb.AppendLine();
            sb.AppendLine("; Directory folder path for storing backup snapshots (relative or absolute).");
            sb.AppendLine("; Type: Path (Directory Path String)");
            sb.AppendLine("; Default: Backups");
            sb.AppendLine($"BackupDirectoryPath={config.BackupDirectoryPath}");
            sb.AppendLine();

            sb.AppendLine("[PathPreferences]");
            sb.AppendLine("; Physically reorder key entries in open CSF documents to match the sequence of the main CSF file.");
            sb.AppendLine("; Type: Boolean (true / false)");
            sb.AppendLine("; Default: true");
            sb.AppendLine($"SortByBinarySequence={config.SortByBinarySequence.ToString().ToLowerInvariant()}");
            sb.AppendLine();
            sb.AppendLine("; Last browsed folder path for opening/saving CSF documents.");
            sb.AppendLine("; Type: Path (Directory Path String)");
            sb.AppendLine($"LastOpenDirectory={config.LastOpenDirectory}");
            sb.AppendLine();
            sb.AppendLine("; Last browsed folder path for scanning INI/MAP game files.");
            sb.AppendLine("; Type: Path (Directory Path String)");
            sb.AppendLine($"LastIniScanDirectory={config.LastIniScanDirectory}");
            sb.AppendLine();
            sb.AppendLine("; Default key label prefix assigned when inserting new string key slots.");
            sb.AppendLine("; Type: String (Uppercase text string)");
            sb.AppendLine("; Default: CSF_");
            sb.AppendLine($"DefaultCategoryPrefix={(string.IsNullOrWhiteSpace(config.DefaultCategoryPrefix) ? "CSF_" : config.DefaultCategoryPrefix.ToUpperInvariant())}");
            sb.AppendLine();
            sb.AppendLine("; Last selected source ANSI/multibyte encoding codepage in Convert Encoding dialog.");
            sb.AppendLine("; Type: Integer (Codepage ID, e.g. 1252, 1251, 936, 950, 932, 949, 866)");
            sb.AppendLine("; Default: 1252");
            sb.AppendLine($"LastSelectedCodepage={config.LastSelectedCodepage}");
            try
            {
                string transConfig = CsfStudio.Core.Translation.TranslationConfigManager.GetTranslationConfigIniString();
                if (!string.IsNullOrEmpty(transConfig))
                {
                    sb.AppendLine();
                    sb.Append(transConfig);
                }
            }
            catch { }

            try
            {
                File.WriteAllText(targetPath, sb.ToString(), Encoding.UTF8);

                string rootProjectIni = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..\\..\\settings.ini");
                if (Directory.Exists(Path.GetDirectoryName(rootProjectIni)))
                {
                    try { File.WriteAllText(rootProjectIni, sb.ToString(), Encoding.UTF8); } catch { }
                }

                // Clean up duplicate ini if location was toggled
                if (config.SaveInAppData && File.Exists(LocalIniPath))
                {
                    File.Delete(LocalIniPath);
                }
                else if (!config.SaveInAppData && File.Exists(AppDataIniPath))
                {
                    File.Delete(AppDataIniPath);
                }
            }
            catch { }
        }

        private static List<string> ParseHistory(string input, int maxItems = 10)
        {
            if (string.IsNullOrWhiteSpace(input)) return new List<string>();
            int limit = Math.Max(1, Math.Min(100, maxItems));
            return input.Split(new[] { "|||" }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(s => s.Trim())
                        .Where(s => !string.IsNullOrEmpty(s))
                        .Take(limit)
                        .ToList();
        }

        private static string FormatHistory(List<string> history, int maxItems = 10)
        {
            if (history == null || history.Count == 0) return string.Empty;
            int limit = Math.Max(1, Math.Min(100, maxItems));
            return string.Join("|||", history.Take(limit));
        }

        public static void AddHistoryItem(List<string> history, string newItem, int maxItems = 10)
        {
            if (string.IsNullOrWhiteSpace(newItem)) return;
            int limit = Math.Max(1, Math.Min(100, maxItems));

            string clean = newItem.Trim();
            history.RemoveAll(x => string.Equals(x, clean, StringComparison.OrdinalIgnoreCase));
            history.Insert(0, clean);

            while (history.Count > limit)
            {
                history.RemoveAt(history.Count - 1);
            }
        }

        public static bool ParseBool(string val, bool defaultValue = false)
        {
            if (string.IsNullOrWhiteSpace(val)) return defaultValue;
            string norm = val.Trim().ToLowerInvariant();

            if (norm == "true" || norm == "yes" || norm == "1" || norm == "on" || norm == "enabled")
            {
                return true;
            }
            if (norm == "false" || norm == "no" || norm == "0" || norm == "off" || norm == "disabled")
            {
                return false;
            }

            return bool.TryParse(norm, out bool result) ? result : defaultValue;
        }
    }
}
