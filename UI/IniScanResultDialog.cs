using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using System.Text;
using CsfStudio.Core;

namespace CsfStudio.UI
{
    public class IniScanResultDialog : Form
    {
        private Button btnCopyMissing;
        private Button btnSaveMissing;
        private Button btnCreateMissing;
        private Button btnCreateAllMissing;
        private Button btnCopyNostr;
        private Button btnSaveNostr;
        private Button btnViewNostr;
        private Button btnClose;
        private CheckBox chkShowExisting;
        private CheckBox chkUniqueKeysOnly;
        private Label lblSummary;
        private TextBox txtIniFiles;
        private Button btnBrowseIniFiles;
        private Button btnRescanIniFiles;
        private Button btnClearIniFiles;
        private Label lblIniFilesCount;
        private TextBox txtRefCsfPath;
        private Button btnBrowseRefCsf;
        private Button btnClearRefCsf;
        private Label lblRefCsfCount;
        private DataGridView grid;

        private List<string> _currentIniPaths = new List<string>();
        private HashSet<string> _refCsfKeySet = null;
        private string _loadedRefCsfPath = null;

        private CsfSession _session;
        public Dictionary<string, List<IniScanResult>> ScannedFilesMap { get; private set; }
        public bool AnyKeysAdded { get; private set; } = false;
        public List<string> NewlyAddedKeys { get; private set; } = new List<string>();
        private List<IniScanResult> _allResults = new List<IniScanResult>();
        private bool _isMultiFile;

        public IniScanResultDialog(CsfSession session)
            : this(null as Dictionary<string, List<IniScanResult>>, session)
        {
        }

        public IniScanResultDialog(Dictionary<string, List<IniScanResult>> scannedFilesMap, CsfSession session)
        {
            _session = session;
            ScannedFilesMap = scannedFilesMap ?? new Dictionary<string, List<IniScanResult>>(StringComparer.OrdinalIgnoreCase);

            _allResults = new List<IniScanResult>();
            foreach (var list in ScannedFilesMap.Values)
            {
                if (list != null) _allResults.AddRange(list);
            }

            _currentIniPaths = ScannedFilesMap.Keys.ToList();
            _isMultiFile = _currentIniPaths.Count > 1;

            InitializeComponent();

            try
            {
                var cfg = ConfigManager.LoadConfig();

                if (_currentIniPaths.Count == 0 && cfg.LastIniScanFiles != null && cfg.LastIniScanFiles.Count > 0)
                {
                    var existing = cfg.LastIniScanFiles.Where(File.Exists).ToList();
                    if (existing.Count > 0)
                    {
                        ScanAndPopulate(existing, false);
                    }
                }
                else if (_currentIniPaths.Count > 0)
                {
                    UpdateIniFilesDisplay();
                    cfg.LastIniScanFiles = _currentIniPaths.ToList();
                    ConfigManager.SaveConfig(cfg);
                }

                if (!string.IsNullOrEmpty(cfg.LastIniScanReferenceCsfPath) && File.Exists(cfg.LastIniScanReferenceCsfPath))
                {
                    LoadReferenceCsf(cfg.LastIniScanReferenceCsfPath, false);
                }
            }
            catch { }

            PopulateAllData();

            this.Shown += (s, e) =>
            {
                grid.ClearSelection();
                UpdateButtonStates();
            };
        }

        public IniScanResultDialog(List<IniScanResult> results, CsfSession session)
            : this(GroupResultsByFile(results), session)
        {
        }

        private static Dictionary<string, List<IniScanResult>> GroupResultsByFile(List<IniScanResult> results)
        {
            var map = new Dictionary<string, List<IniScanResult>>(StringComparer.OrdinalIgnoreCase);
            if (results == null) return map;

            foreach (var item in results)
            {
                string key = string.IsNullOrEmpty(item.FullIniPath) ? item.SourceIniFile : item.FullIniPath;
                if (string.IsNullOrEmpty(key)) key = "ScannedFile.ini";

                if (!map.TryGetValue(key, out var list))
                {
                    list = new List<IniScanResult>();
                    map[key] = list;
                }
                list.Add(item);
            }
            return map;
        }

        private void InitializeComponent()
        {
            this.Text = LanguageManager.GetString("IniScan.Title", "C&C INI Reference Scanner Results");
            this.Size = new Size(1020, 600);
            this.MinimumSize = new Size(900, 500);
            this.StartPosition = FormStartPosition.CenterParent;
            this.ShowIcon = false;
            this.Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point, ((byte)(0)));

            var mainTable = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 3,
                ColumnCount = 1,
                Margin = new Padding(0),
                Padding = new Padding(0)
            };

            mainTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 126F));
            mainTable.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            mainTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 78F));

            // 1. TOP PANEL
            var panelTop = new Panel { Dock = DockStyle.Fill, Height = 126, Padding = new Padding(10), BackColor = Color.FromArgb(245, 247, 250), Margin = new Padding(0) };

            // Row 1: INI / MAP Files
            var pnlIniFiles = new FlowLayoutPanel
            {
                Location = new Point(10, 8),
                Size = new Size(950, 28),
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                AutoSize = true,
                BackColor = Color.Transparent,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };

            var lblIniTitle = new Label
            {
                Text = LanguageManager.GetString("IniScan.FilesLabel", "📄 Mod INI / Map Files:"),
                AutoSize = true,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.FromArgb(40, 60, 90),
                Margin = new Padding(0, 5, 6, 0)
            };

            txtIniFiles = new TextBox
            {
                Width = 380,
                Font = new Font("Segoe UI", 9F, FontStyle.Regular),
                Margin = new Padding(0, 1, 4, 0),
                AllowDrop = true
            };

            btnBrowseIniFiles = new Button
            {
                Text = LanguageManager.GetString("IniScan.FilesBrowse", "Browse..."),
                Height = 25,
                Width = 85,
                Margin = new Padding(0, 0, 4, 0),
                UseVisualStyleBackColor = true,
                Cursor = Cursors.Hand
            };

            btnRescanIniFiles = new Button
            {
                Text = LanguageManager.GetString("IniScan.FilesRescan", "🔄 Re-scan"),
                Height = 25,
                Width = 95,
                Margin = new Padding(0, 0, 4, 0),
                UseVisualStyleBackColor = true,
                Cursor = Cursors.Hand
            };

            btnClearIniFiles = new Button
            {
                Text = "❌",
                Height = 25,
                Width = 32,
                Margin = new Padding(0, 0, 8, 0),
                UseVisualStyleBackColor = true,
                Cursor = Cursors.Hand
            };

            lblIniFilesCount = new Label
            {
                Text = string.Empty,
                AutoSize = true,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.FromArgb(40, 90, 140),
                Margin = new Padding(0, 5, 0, 0)
            };

            string iniFilesTooltipText = LanguageManager.GetString(
                "IniScan.FilesTooltip",
                "Mod INI / Map Files to Scan:\nSelect or drop one or more configuration/map files (e.g. 'rulesmd.ini', 'artmd.ini', 'mission.map').\nThe scanner will inspect all string references (UIName, Name, etc.) and cross-reference them with your open CSF files.");

            var tip = new ToolTip();
            tip.SetToolTip(lblIniTitle, iniFilesTooltipText);
            tip.SetToolTip(txtIniFiles, iniFilesTooltipText);
            tip.SetToolTip(btnBrowseIniFiles, iniFilesTooltipText);
            tip.SetToolTip(btnRescanIniFiles, LanguageManager.GetString("IniScan.FilesRescanTooltip", "Re-scan the selected INI/Map files"));
            tip.SetToolTip(btnClearIniFiles, LanguageManager.GetString("IniScan.FilesClear", "Clear scanned files"));

            btnBrowseIniFiles.Click += (s, e) =>
            {
                using (var dlg = new OpenFileDialog())
                {
                    dlg.Filter = LanguageManager.GetString("Filter.IniMapFilter", "C&C INI & Map Files (*.ini;*.map)|*.ini;*.map|C&C INI Files (*.ini)|*.ini|C&C Map Files (*.map)|*.map|All Files (*.*)|*.*");
                    dlg.Title = LanguageManager.GetString("MainForm.ScanIniTitle", "Select Mod INI or Map Files (rulesmd.ini, artmd.ini, mission.map, etc.)");
                    dlg.Multiselect = true;
                    if (_currentIniPaths.Count > 0 && File.Exists(_currentIniPaths[0]))
                    {
                        try { dlg.InitialDirectory = Path.GetDirectoryName(_currentIniPaths[0]); } catch { }
                    }
                    if (dlg.ShowDialog(this) == DialogResult.OK && dlg.FileNames.Length > 0)
                    {
                        ScanAndPopulate(dlg.FileNames.ToList());
                    }
                }
            };

            btnRescanIniFiles.Click += (s, e) =>
            {
                if (_currentIniPaths != null && _currentIniPaths.Count > 0)
                {
                    ScanAndPopulate(_currentIniPaths.ToList());
                }
            };

            btnClearIniFiles.Click += (s, e) =>
            {
                _currentIniPaths.Clear();
                ScannedFilesMap.Clear();
                _allResults.Clear();
                txtIniFiles.Text = string.Empty;
                lblIniFilesCount.Text = string.Empty;
                try
                {
                    var cfg = ConfigManager.LoadConfig();
                    cfg.LastIniScanFiles.Clear();
                    ConfigManager.SaveConfig(cfg);
                }
                catch { }
                PopulateAllData();
            };

            txtIniFiles.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter)
                {
                    e.SuppressKeyPress = true;
                    string entered = txtIniFiles.Text.Trim();
                    if (!string.IsNullOrEmpty(entered))
                    {
                        var parts = entered.Split(new[] { ';', '|' }, StringSplitOptions.RemoveEmptyEntries)
                            .Select(p => p.Trim().Trim('"', '\''))
                            .Where(File.Exists)
                            .ToList();
                        if (parts.Count > 0)
                        {
                            ScanAndPopulate(parts);
                        }
                    }
                }
            };

            txtIniFiles.DragEnter += (s, e) =>
            {
                if (e.Data.GetDataPresent(DataFormats.FileDrop)) e.Effect = DragDropEffects.Copy;
            };

            txtIniFiles.DragDrop += (s, e) =>
            {
                var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files != null && files.Length > 0)
                {
                    var iniFiles = files.Where(f => f.EndsWith(".ini", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".map", StringComparison.OrdinalIgnoreCase)).ToList();
                    if (iniFiles.Count > 0)
                    {
                        ScanAndPopulate(iniFiles);
                    }
                }
            };

            pnlIniFiles.Controls.Add(lblIniTitle);
            pnlIniFiles.Controls.Add(txtIniFiles);
            pnlIniFiles.Controls.Add(btnBrowseIniFiles);
            pnlIniFiles.Controls.Add(btnRescanIniFiles);
            pnlIniFiles.Controls.Add(btnClearIniFiles);
            pnlIniFiles.Controls.Add(lblIniFilesCount);

            // Row 2: Reference CSF
            var pnlRefCsf = new FlowLayoutPanel
            {
                Location = new Point(10, 38),
                Size = new Size(950, 28),
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                AutoSize = true,
                BackColor = Color.Transparent,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };

            var lblRefCsfTitle = new Label
            {
                Text = LanguageManager.GetString("IniScan.RefCsfLabel", "📁 Base Reference CSF (Filter):"),
                AutoSize = true,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.FromArgb(40, 60, 90),
                Margin = new Padding(0, 5, 6, 0)
            };

            txtRefCsfPath = new TextBox
            {
                Width = 380,
                Font = new Font("Segoe UI", 9F, FontStyle.Regular),
                Margin = new Padding(0, 1, 4, 0),
                AllowDrop = true
            };

            btnBrowseRefCsf = new Button
            {
                Text = LanguageManager.GetString("IniScan.RefCsfBrowse", "Browse..."),
                Height = 25,
                Width = 85,
                Margin = new Padding(0, 0, 4, 0),
                UseVisualStyleBackColor = true,
                Cursor = Cursors.Hand
            };

            btnClearRefCsf = new Button
            {
                Text = "❌",
                Height = 25,
                Width = 32,
                Margin = new Padding(0, 0, 8, 0),
                UseVisualStyleBackColor = true,
                Cursor = Cursors.Hand
            };

            lblRefCsfCount = new Label
            {
                Text = string.Empty,
                AutoSize = true,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.FromArgb(20, 120, 60),
                Margin = new Padding(0, 5, 0, 0)
            };

            string refCsfTooltipText = LanguageManager.GetString(
                "IniScan.RefCsfTooltip",
                "Base Reference CSF File (Filter):\nAllows selecting the original base game CSF (e.g. 'ra2.csf').\nIf a key referenced in the INI/MAP files already exists in this base CSF, it will NOT be marked as missing (🔴 MISSING) nor created unnecessarily in your expansion/mod CSF, eliminating false positives.");

            tip.SetToolTip(lblRefCsfTitle, refCsfTooltipText);
            tip.SetToolTip(txtRefCsfPath, refCsfTooltipText);
            tip.SetToolTip(btnBrowseRefCsf, refCsfTooltipText);
            tip.SetToolTip(btnClearRefCsf, LanguageManager.GetString("IniScan.RefCsfClear", "Clear filter"));

            btnBrowseRefCsf.Click += (s, e) =>
            {
                using (var dlg = new OpenFileDialog())
                {
                    dlg.Filter = LanguageManager.GetString("Filter.CsfFiles", "C&C String Table Files (*.csf)|*.csf|All Files (*.*)|*.*");
                    dlg.Title = LanguageManager.GetString("IniScan.RefCsfBrowseTitle", "Select Base Reference CSF File (ra2.csf, language.csf, etc.)");
                    if (!string.IsNullOrEmpty(txtRefCsfPath.Text) && File.Exists(txtRefCsfPath.Text))
                    {
                        try
                        {
                            dlg.InitialDirectory = Path.GetDirectoryName(txtRefCsfPath.Text);
                            dlg.FileName = Path.GetFileName(txtRefCsfPath.Text);
                        }
                        catch { }
                    }
                    if (dlg.ShowDialog(this) == DialogResult.OK)
                    {
                        LoadReferenceCsf(dlg.FileName);
                    }
                }
            };

            btnClearRefCsf.Click += (s, e) =>
            {
                _refCsfKeySet = null;
                _loadedRefCsfPath = null;
                txtRefCsfPath.Text = string.Empty;
                lblRefCsfCount.Text = string.Empty;
                try
                {
                    var cfg = ConfigManager.LoadConfig();
                    cfg.LastIniScanReferenceCsfPath = string.Empty;
                    ConfigManager.SaveConfig(cfg);
                }
                catch { }
                PopulateAllData();
            };

            txtRefCsfPath.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter)
                {
                    e.SuppressKeyPress = true;
                    string entered = txtRefCsfPath.Text.Trim().Trim('"', '\'');
                    if (!string.Equals(entered, _loadedRefCsfPath, StringComparison.OrdinalIgnoreCase))
                    {
                        LoadReferenceCsf(entered);
                    }
                }
            };

            txtRefCsfPath.DragEnter += (s, e) =>
            {
                if (e.Data.GetDataPresent(DataFormats.FileDrop)) e.Effect = DragDropEffects.Copy;
            };

            txtRefCsfPath.DragDrop += (s, e) =>
            {
                var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files != null && files.Length > 0 && files[0].EndsWith(".csf", StringComparison.OrdinalIgnoreCase))
                {
                    LoadReferenceCsf(files[0]);
                }
            };

            pnlRefCsf.Controls.Add(lblRefCsfTitle);
            pnlRefCsf.Controls.Add(txtRefCsfPath);
            pnlRefCsf.Controls.Add(btnBrowseRefCsf);
            pnlRefCsf.Controls.Add(btnClearRefCsf);
            pnlRefCsf.Controls.Add(lblRefCsfCount);

            // Row 3: Summary
            lblSummary = new Label 
            { 
                Text = LanguageManager.GetString("IniScan.Calculating", "Calculating..."),
                Location = new Point(10, 68), 
                AutoSize = true, 
                Font = new Font("Segoe UI", 9F, FontStyle.Regular),
                ForeColor = Color.Black
            };

            // Row 4: Checkboxes
            var pnlCheckboxes = new FlowLayoutPanel
            {
                Location = new Point(10, 94),
                Size = new Size(950, 26),
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                AutoSize = true,
                BackColor = Color.Transparent,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };

            chkShowExisting = new CheckBox
            {
                Text = LanguageManager.GetString("IniScan.ShowExisting", "☑ Show Existing Keys (🟢 EXISTS) & Inline Literals (🔹 NOSTR)"),
                AutoSize = true,
                Checked = false, // Hidden by default
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.FromArgb(20, 60, 120),
                Cursor = Cursors.Hand
            };
            chkShowExisting.CheckedChanged += (s, e) => PopulateAllData();

            chkUniqueKeysOnly = new CheckBox
            {
                Text = LanguageManager.GetString("IniScan.ShowUniqueOnly", "☑ Show Unique Keys Only"),
                AutoSize = true,
                Checked = false,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.FromArgb(30, 90, 40),
                Cursor = Cursors.Hand,
                Margin = new Padding(25, 0, 0, 0)
            };
            chkUniqueKeysOnly.CheckedChanged += (s, e) => PopulateAllData();

            pnlCheckboxes.Controls.Add(chkShowExisting);
            pnlCheckboxes.Controls.Add(chkUniqueKeysOnly);

            panelTop.Controls.Add(pnlIniFiles);
            panelTop.Controls.Add(pnlRefCsf);
            panelTop.Controls.Add(lblSummary);
            panelTop.Controls.Add(pnlCheckboxes);

            // Form-level Drag & Drop support
            this.AllowDrop = true;
            this.DragEnter += (s, e) =>
            {
                if (e.Data.GetDataPresent(DataFormats.FileDrop)) e.Effect = DragDropEffects.Copy;
            };
            this.DragDrop += (s, e) =>
            {
                var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files != null && files.Length > 0)
                {
                    var iniFiles = files.Where(f => f.EndsWith(".ini", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".map", StringComparison.OrdinalIgnoreCase)).ToList();
                    var csfFiles = files.Where(f => f.EndsWith(".csf", StringComparison.OrdinalIgnoreCase)).ToList();

                    if (csfFiles.Count > 0)
                    {
                        LoadReferenceCsf(csfFiles[0]);
                    }
                    if (iniFiles.Count > 0)
                    {
                        ScanAndPopulate(iniFiles);
                    }
                }
            };

            // 2. MIDDLE UNIFIED GRID
            grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                RowHeadersVisible = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.None,
                Margin = new Padding(0),
                ShowCellToolTips = true
            };

            SetupGridColumns();

            grid.CellPainting += (s, e) =>
            {
                int statusColIdx = _isMultiFile ? 4 : 3;
                if (e.RowIndex >= 0 && e.ColumnIndex == statusColIdx)
                {
                    e.PaintBackground(e.ClipBounds, true);

                    if (grid.Rows[e.RowIndex].Tag is IniScanResult item)
                    {
                        Color badgeBg;
                        Color badgeBorder;
                        Color dotColor;
                        Color textColor;
                        string text;

                        if (item.IsNostrInline)
                        {
                            badgeBg = Color.FromArgb(243, 232, 255);
                            badgeBorder = Color.FromArgb(192, 132, 252);
                            dotColor = Color.FromArgb(147, 51, 234);
                            textColor = Color.FromArgb(88, 28, 135);
                            text = LanguageManager.GetString("IniScan.StatusInlineNostr", "INLINE (NOSTR)");
                        }
                        else if (item.ExistsInCsf)
                        {
                            badgeBg = Color.FromArgb(220, 252, 231);
                            badgeBorder = Color.FromArgb(74, 222, 128);
                            dotColor = Color.FromArgb(22, 163, 74);
                            textColor = Color.FromArgb(20, 83, 45);
                            text = LanguageManager.GetString("IniScan.StatusExists", "EXISTS");
                        }
                        else if (item.ExistsInRefCsf)
                        {
                            badgeBg = Color.FromArgb(224, 242, 254);
                            badgeBorder = Color.FromArgb(56, 189, 248);
                            dotColor = Color.FromArgb(2, 132, 199);
                            textColor = Color.FromArgb(12, 74, 110);
                            text = LanguageManager.GetString("IniScan.StatusBaseCsf", "IN BASE CSF");
                        }
                        else
                        {
                            badgeBg = Color.FromArgb(254, 226, 226);
                            badgeBorder = Color.FromArgb(248, 113, 113);
                            dotColor = Color.FromArgb(220, 38, 38);
                            textColor = Color.FromArgb(127, 29, 29);
                            text = LanguageManager.GetString("IniScan.StatusMissing", "MISSING");
                        }

                        text = text.Replace("🟢", "").Replace("🔴", "").Replace("🔹", "").Replace("⚪", "").Replace("🔵", "").Trim();

                        var g = e.Graphics;
                        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

                        int badgeMarginY = 3;
                        int badgeMarginX = 4;
                        var badgeRect = new Rectangle(
                            e.CellBounds.X + badgeMarginX,
                            e.CellBounds.Y + badgeMarginY,
                            e.CellBounds.Width - (badgeMarginX * 2),
                            e.CellBounds.Height - (badgeMarginY * 2));

                        if (badgeRect.Width > 20 && badgeRect.Height > 10)
                        {
                            using (var bgBrush = new SolidBrush(badgeBg))
                            using (var borderPen = new Pen(badgeBorder, 1f))
                            {
                                int radius = 4;
                                using (var path = GetRoundedRectPath(badgeRect, radius))
                                {
                                    g.FillPath(bgBrush, path);
                                    g.DrawPath(borderPen, path);
                                }
                            }

                            int dotSize = 8;
                            int dotX = badgeRect.X + 8;
                            int dotY = badgeRect.Y + (badgeRect.Height - dotSize) / 2;
                            using (var dotBrush = new SolidBrush(dotColor))
                            {
                                g.FillEllipse(dotBrush, dotX, dotY, dotSize, dotSize);
                            }

                            int textX = dotX + dotSize + 6;
                            var textRect = new Rectangle(textX, badgeRect.Y, Math.Max(0, badgeRect.Right - textX - 4), badgeRect.Height);
                            var flags = TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis;
                            using (var fontBold = new Font(e.CellStyle.Font ?? grid.Font, FontStyle.Bold))
                            {
                                TextRenderer.DrawText(g, text, fontBold, textRect, textColor, flags);
                            }
                        }
                    }

                    e.Handled = true;
                }
            };

            grid.CellToolTipTextNeeded += (s, e) =>
            {
                if (_isMultiFile && e.ColumnIndex == 0 && e.RowIndex >= 0 && e.RowIndex < grid.Rows.Count)
                {
                    var row = grid.Rows[e.RowIndex];
                    if (row.Tag is IniScanResult item && !string.IsNullOrEmpty(item.FullIniPath))
                    {
                        e.ToolTipText = string.Format(LanguageManager.GetString("IniScan.FullPathFormat", "Full Path: {0}"), item.FullIniPath);
                    }
                }
            };

            grid.SelectionChanged += (s, e) => UpdateButtonStates();

            // 3. BOTTOM ACTION PANEL (2 rows: Row 0 = Export / Clipboard tools, Row 1 = Session Actions & Close)
            var panelBottom = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 2,
                ColumnCount = 1,
                Margin = Padding.Empty,
                Padding = new Padding(10, 4, 10, 4),
                BackColor = Color.FromArgb(240, 242, 245)
            };
            panelBottom.RowStyles.Add(new RowStyle(SizeType.Absolute, 35F));
            panelBottom.RowStyles.Add(new RowStyle(SizeType.Absolute, 35F));

            var pnlRow0 = new Panel { Dock = DockStyle.Fill, Margin = Padding.Empty, Padding = Padding.Empty, BackColor = Color.Transparent };
            var pnlRow1 = new Panel { Dock = DockStyle.Fill, Margin = Padding.Empty, Padding = Padding.Empty, BackColor = Color.Transparent };

            var pnlNostrLeft = new FlowLayoutPanel
            {
                Dock = DockStyle.Left,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Margin = Padding.Empty,
                Padding = Padding.Empty,
                BackColor = Color.Transparent
            };

            var pnlKeysExportRight = new FlowLayoutPanel
            {
                Dock = DockStyle.Right,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
                Margin = Padding.Empty,
                Padding = Padding.Empty,
                BackColor = Color.Transparent
            };

            var pnlSessionActionsRight = new FlowLayoutPanel
            {
                Dock = DockStyle.Right,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
                Margin = Padding.Empty,
                Padding = Padding.Empty,
                BackColor = Color.Transparent
            };

            // Row 0 Controls: NOSTR on left, Keys Export on right
            btnCopyNostr = new Button
            {
                Text = LanguageManager.GetString("IniScan.CopyNostrDefault", "📋 Copy NOSTR"),
                Height = 32,
                AutoSize = true,
                MinimumSize = new Size(130, 32),
                Font = new Font("Segoe UI", 9F, FontStyle.Regular),
                Margin = new Padding(0, 0, 6, 0),
                Enabled = false,
                UseVisualStyleBackColor = true,
                Cursor = Cursors.Hand
            };

            btnSaveNostr = new Button
            {
                Text = LanguageManager.GetString("IniScan.SaveNostrDefault", "💾 Save NOSTR strings as..."),
                Height = 32,
                AutoSize = true,
                MinimumSize = new Size(170, 32),
                Font = new Font("Segoe UI", 9F, FontStyle.Regular),
                Margin = new Padding(0, 0, 6, 0),
                Enabled = false,
                UseVisualStyleBackColor = true,
                Cursor = Cursors.Hand
            };

            btnViewNostr = new Button
            {
                Text = LanguageManager.GetString("IniScan.ViewNostrDefault", "👁️ View NOSTR..."),
                Height = 32,
                AutoSize = true,
                MinimumSize = new Size(110, 32),
                Font = new Font("Segoe UI", 9F, FontStyle.Regular),
                Margin = new Padding(0, 0, 6, 0),
                Enabled = false,
                UseVisualStyleBackColor = true,
                Cursor = Cursors.Hand
            };

            pnlNostrLeft.Controls.Add(btnCopyNostr);
            pnlNostrLeft.Controls.Add(btnSaveNostr);
            pnlNostrLeft.Controls.Add(btnViewNostr);

            btnSaveMissing = new Button
            {
                Text = LanguageManager.GetString("IniScan.SaveKeysDefault", "💾 Save Keys as..."),
                Height = 32,
                AutoSize = true,
                MinimumSize = new Size(150, 32),
                Font = new Font("Segoe UI", 9F, FontStyle.Regular),
                Margin = new Padding(6, 0, 0, 0),
                Enabled = false,
                UseVisualStyleBackColor = true,
                Cursor = Cursors.Hand
            };

            btnCopyMissing = new Button
            {
                Text = LanguageManager.GetString("IniScan.CopyKeysDefault", "📋 Copy Keys"),
                Height = 32,
                AutoSize = true,
                MinimumSize = new Size(130, 32),
                Font = new Font("Segoe UI", 9F, FontStyle.Regular),
                Margin = new Padding(6, 0, 0, 0),
                Enabled = false,
                UseVisualStyleBackColor = true,
                Cursor = Cursors.Hand
            };

            pnlKeysExportRight.Controls.Add(btnSaveMissing);
            pnlKeysExportRight.Controls.Add(btnCopyMissing);

            pnlRow0.Controls.Add(pnlNostrLeft);
            pnlRow0.Controls.Add(pnlKeysExportRight);

            // Row 1 Controls: Close and Session Add Actions
            btnClose = new Button 
            { 
                Text = LanguageManager.GetString("Button.Close", "Close"), 
                Size = new Size(85, 32),
                Margin = new Padding(6, 0, 0, 0),
                UseVisualStyleBackColor = true
            };
            btnClose.Click += (s, e) =>
            {
                this.DialogResult = AnyKeysAdded ? DialogResult.OK : DialogResult.Cancel;
                this.Close();
            };

            btnCreateAllMissing = new Button 
            { 
                Text = LanguageManager.GetString("IniScan.AddAllMissing", "⚡ Add ALL Missing Keys to CSF"), 
                Height = 32,
                AutoSize = true,
                MinimumSize = new Size(220, 32),
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Margin = new Padding(6, 0, 6, 0),
                Enabled = false,
                UseVisualStyleBackColor = true
            };

            btnCreateMissing = new Button 
            { 
                Text = LanguageManager.GetString("IniScan.AddSelected", "⚡ Add Selected Keys..."), 
                Height = 32,
                AutoSize = true,
                MinimumSize = new Size(160, 32),
                Font = new Font("Segoe UI", 9F, FontStyle.Regular),
                Margin = new Padding(6, 0, 6, 0),
                Enabled = false,
                UseVisualStyleBackColor = true
            };

            pnlSessionActionsRight.Controls.Add(btnClose);
            pnlSessionActionsRight.Controls.Add(btnCreateAllMissing);
            pnlSessionActionsRight.Controls.Add(btnCreateMissing);

            pnlRow1.Controls.Add(pnlSessionActionsRight);

            panelBottom.Controls.Add(pnlRow0, 0, 0);
            panelBottom.Controls.Add(pnlRow1, 0, 1);

            btnCopyMissing.Click += (s, e) => CopyMissingKeysToClipboard();
            btnSaveMissing.Click += (s, e) => SaveMissingKeysAsTxt();
            btnCreateMissing.Click += (s, e) => AddMissingKeysToSession(isAllMode: false);
            btnCreateAllMissing.Click += (s, e) => AddMissingKeysToSession(isAllMode: true);
            btnCopyNostr.Click += (s, e) => CopyNostrToClipboard();
            btnSaveNostr.Click += (s, e) => SaveNostrStringsAsTxt();
            btnViewNostr.Click += (s, e) => ShowNostrTemplateDialog();

            mainTable.Controls.Add(panelTop, 0, 0);
            mainTable.Controls.Add(grid, 0, 1);
            mainTable.Controls.Add(panelBottom, 0, 2);

            this.Controls.Add(mainTable);

            ToolTipHelper.SetToolTip(btnCopyNostr, LanguageManager.GetString("ToolTip.IniScan.CopyNostr", "Copy all scanned inline NOSTR: strings to the clipboard formatted as [] and Value= ready to paste into your text editor."));
            ToolTipHelper.SetToolTip(btnSaveNostr, LanguageManager.GetString("ToolTip.IniScan.SaveNostr", "Save all scanned inline NOSTR: strings as a .txt file formatted as [] and Value= ready to edit in your text editor."));
            ToolTipHelper.SetToolTip(btnViewNostr, LanguageManager.GetString("ToolTip.IniScan.ViewNostr", "View / Preview NOSTR: Open a multiline text template containing all inline NOSTR: literals formatted as [] and Value=."));
            ToolTipHelper.SetToolTip(btnCopyMissing, LanguageManager.GetString("ToolTip.IniScan.CopyKeys", "Copy unique missing CSF keys to the clipboard formatted as [KEY]\nValue= ready to edit in your text editor."));
            ToolTipHelper.SetToolTip(btnSaveMissing, LanguageManager.GetString("ToolTip.IniScan.SaveKeys", "Save unique missing CSF keys as a .txt file formatted as [KEY]\nValue= ready to edit in your text editor."));
            ToolTipHelper.SetToolTip(btnCreateAllMissing, LanguageManager.GetString("ToolTip.IniScan.AddAllMissing", "Add ALL Missing Keys: Automatically add ALL unique missing string key slots found in scan into open CSF files."));
            ToolTipHelper.SetToolTip(btnCreateMissing, LanguageManager.GetString("ToolTip.IniScan.AddSelected", "Add Selected Keys: Automatically add only selected missing string key slots into open CSF files."));
            ToolTipHelper.SetToolTip(btnClose, LanguageManager.GetString("ToolTip.IniScan.Close", "Close window."));
            if (chkUniqueKeysOnly != null)
                ToolTipHelper.SetToolTip(chkUniqueKeysOnly, LanguageManager.GetString("ToolTip.IniScan.ShowUniqueOnly", "When checked, duplicates are hidden and each referenced CSF key is shown only once."));
        }

        private void SetupGridColumns()
        {
            grid.Columns.Clear();
            var boldStyle = new DataGridViewCellStyle { Font = new Font("Segoe UI", 9F, FontStyle.Bold) };
            string colKeyHeader = LanguageManager.GetString("IniScan.ColRefKey", "CSF Key (Referenced)");

            if (_isMultiFile)
            {
                var colFile = new DataGridViewTextBoxColumn { HeaderText = LanguageManager.GetString("IniScan.ColFile", "INI File"), ReadOnly = true, FillWeight = 20 };
                var colSection = new DataGridViewTextBoxColumn { HeaderText = LanguageManager.GetString("IniScan.ColSection", "INI Section"), ReadOnly = true, FillWeight = 20 };
                var colProperty = new DataGridViewTextBoxColumn { HeaderText = LanguageManager.GetString("IniScan.ColProperty", "INI Property"), ReadOnly = true, FillWeight = 18 };
                var colKey = new DataGridViewTextBoxColumn { HeaderText = colKeyHeader, ReadOnly = true, FillWeight = 27, DefaultCellStyle = boldStyle };
                var colStatus = new DataGridViewTextBoxColumn { HeaderText = LanguageManager.GetString("Grid.Column.Status", "CSF Status"), ReadOnly = true, FillWeight = 15 };

                colFile.ToolTipText = LanguageManager.GetString("ToolTip.IniScan.ColFile", "INI File: Name of the mod configuration or map file where this string reference was found.");
                colSection.ToolTipText = LanguageManager.GetString("ToolTip.IniScan.ColSection", "INI Section: The section header [SectionName] in the INI file containing this property.");
                colProperty.ToolTipText = LanguageManager.GetString("ToolTip.IniScan.ColProperty", "INI Property: The property key (e.g. UIName, Name) that refers to a CSF string table entry.");
                colKey.ToolTipText = LanguageManager.GetString("ToolTip.IniScan.ColRefKey", "CSF String Key: The label/key name expected in the CSF string table (e.g. 'Name:E1'). This is the entry that will be created in your CSF file(s).");
                colStatus.ToolTipText = LanguageManager.GetString("ToolTip.IniScan.ColStatus", "CSF Status:\n🟢 EXISTS: Found in current CSF\n🟢 IN BASE CSF: Found in base reference CSF\n🔴 MISSING: Not found in any CSF\n🔹 INLINE: Hardcoded NOSTR: literal");

                grid.Columns.AddRange(colFile, colSection, colProperty, colKey, colStatus);
            }
            else
            {
                var colSection = new DataGridViewTextBoxColumn { HeaderText = LanguageManager.GetString("IniScan.ColSection", "INI Section"), ReadOnly = true, FillWeight = 25 };
                var colProperty = new DataGridViewTextBoxColumn { HeaderText = LanguageManager.GetString("IniScan.ColProperty", "INI Property"), ReadOnly = true, FillWeight = 20 };
                var colKey = new DataGridViewTextBoxColumn { HeaderText = colKeyHeader, ReadOnly = true, FillWeight = 35, DefaultCellStyle = boldStyle };
                var colStatus = new DataGridViewTextBoxColumn { HeaderText = LanguageManager.GetString("Grid.Column.Status", "CSF Status"), ReadOnly = true, FillWeight = 20 };

                colSection.ToolTipText = LanguageManager.GetString("ToolTip.IniScan.ColSection", "INI Section: The section header [SectionName] in the INI file containing this property.");
                colProperty.ToolTipText = LanguageManager.GetString("ToolTip.IniScan.ColProperty", "INI Property: The property key (e.g. UIName, Name) that refers to a CSF string table entry.");
                colKey.ToolTipText = LanguageManager.GetString("ToolTip.IniScan.ColRefKey", "CSF String Key: The label/key name expected in the CSF string table (e.g. 'Name:E1'). This is the entry that will be created in your CSF file(s).");
                colStatus.ToolTipText = LanguageManager.GetString("ToolTip.IniScan.ColStatus", "CSF Status:\n🟢 EXISTS: Found in current CSF\n🟢 IN BASE CSF: Found in base reference CSF\n🔴 MISSING: Not found in any CSF\n🔹 INLINE: Hardcoded NOSTR: literal");

                grid.Columns.AddRange(colSection, colProperty, colKey, colStatus);
            }
        }

        private void UpdateIniFilesDisplay()
        {
            if (txtIniFiles == null) return;

            if (_currentIniPaths == null || _currentIniPaths.Count == 0)
            {
                txtIniFiles.Text = string.Empty;
                if (lblIniFilesCount != null) lblIniFilesCount.Text = string.Empty;
                return;
            }

            if (_currentIniPaths.Count == 1)
            {
                txtIniFiles.Text = _currentIniPaths[0];
                if (lblIniFilesCount != null)
                    lblIniFilesCount.Text = string.Format(LanguageManager.GetString("IniScan.FilesLoadedCountFormat", "({0:N0} file(s) loaded)"), 1);
            }
            else
            {
                txtIniFiles.Text = string.Join("; ", _currentIniPaths.Select(Path.GetFileName));
                if (lblIniFilesCount != null)
                    lblIniFilesCount.Text = string.Format(LanguageManager.GetString("IniScan.FilesLoadedCountFormat", "({0:N0} files loaded)"), _currentIniPaths.Count);
            }

            string fullPaths = string.Join(Environment.NewLine, _currentIniPaths);
            var tip = new ToolTip();
            tip.SetToolTip(txtIniFiles, fullPaths);
        }

        private void ScanAndPopulate(List<string> paths, bool triggerPopulate = true)
        {
            if (paths == null) paths = new List<string>();
            var validPaths = paths.Where(File.Exists).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

            _currentIniPaths = validPaths;
            UpdateIniFilesDisplay();

            ScannedFilesMap.Clear();
            _allResults.Clear();

            if (validPaths.Count > 0)
            {
                Cursor = Cursors.WaitCursor;
                try
                {
                    foreach (var file in validPaths)
                    {
                        var results = IniScanner.ScanIniFile(file, _session);
                        if (results != null)
                        {
                            ScannedFilesMap[file] = results;
                            _allResults.AddRange(results);
                        }
                    }

                    try
                    {
                        var cfg = ConfigManager.LoadConfig();
                        cfg.LastIniScanFiles = validPaths;
                        ConfigManager.SaveConfig(cfg);
                    }
                    catch { }
                }
                finally
                {
                    Cursor = Cursors.Default;
                }
            }

            bool wasMulti = _isMultiFile;
            _isMultiFile = validPaths.Count > 1;
            if (_isMultiFile != wasMulti || grid.Columns.Count == 0)
            {
                SetupGridColumns();
            }

            if (triggerPopulate)
            {
                PopulateAllData();
            }
        }

        private static System.Drawing.Drawing2D.GraphicsPath GetRoundedRectPath(Rectangle rect, int radius)
        {
            var path = new System.Drawing.Drawing2D.GraphicsPath();
            int maxRadius = Math.Min(rect.Width / 2, rect.Height / 2);
            if (radius > maxRadius) radius = maxRadius;
            if (radius <= 0)
            {
                path.AddRectangle(rect);
                return path;
            }
            int d = radius * 2;
            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        private void LoadReferenceCsf(string filePath, bool triggerRepopulate = true)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            {
                _refCsfKeySet = null;
                _loadedRefCsfPath = null;
                if (txtRefCsfPath != null) txtRefCsfPath.Text = string.Empty;
                if (lblRefCsfCount != null) lblRefCsfCount.Text = string.Empty;
                return;
            }

            try
            {
                Cursor = Cursors.WaitCursor;
                var doc = CsfFileHandler.Load(filePath);
                var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                if (doc != null && doc.Labels != null)
                {
                    foreach (var lbl in doc.Labels)
                    {
                        if (!string.IsNullOrWhiteSpace(lbl.Name))
                        {
                            set.Add(lbl.Name);
                        }
                    }
                }

                _refCsfKeySet = set;
                _loadedRefCsfPath = filePath;
                if (txtRefCsfPath != null) txtRefCsfPath.Text = filePath;
                if (lblRefCsfCount != null)
                {
                    lblRefCsfCount.Text = string.Format(
                        LanguageManager.GetString("IniScan.RefCsfLoadedCountFormat", "({0:N0} keys loaded)"),
                        set.Count);
                }

                try
                {
                    var cfg = ConfigManager.LoadConfig();
                    cfg.LastIniScanReferenceCsfPath = filePath;
                    ConfigManager.SaveConfig(cfg);
                }
                catch { }

                if (triggerRepopulate)
                {
                    PopulateAllData();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    string.Format(LanguageManager.GetString("Msg.ErrorLoadingReferenceCsfFormat", "Error loading base reference CSF file:\n{0}"), ex.Message),
                    LanguageManager.GetString("Title.ReferenceCsfError", "Reference CSF Error"),
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
            finally
            {
                Cursor = Cursors.Default;
            }
        }

        private void UpdateButtonStates()
        {
            var selectedMissingItems = new List<IniScanResult>();
            if (grid.SelectedRows.Count > 0)
            {
                foreach (DataGridViewRow row in grid.SelectedRows)
                {
                    if (row.Tag is IniScanResult item && !item.ExistsInCsf && !item.ExistsInRefCsf && !item.IsNostrInline)
                    {
                        selectedMissingItems.Add(item);
                    }
                }
            }

            var distinctSelectedKeys = selectedMissingItems.Select(r => r.KeyName).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            bool hasSelectedMissing = distinctSelectedKeys.Count > 0;

            var allMissingItems = _allResults.Where(r => !r.ExistsInCsf && !r.ExistsInRefCsf && !r.IsNostrInline).ToList();
            var distinctAllMissingKeys = allMissingItems.Select(r => r.KeyName).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            bool hasAnyMissing = distinctAllMissingKeys.Count > 0;

            bool isMultiCsf = (_session != null && _session.Documents.Count > 1);
            bool hasSessionDocs = (_session != null && _session.Documents.Count > 0);

            btnCreateAllMissing.Enabled = hasAnyMissing && hasSessionDocs;
            btnCreateMissing.Enabled = hasSelectedMissing && hasSessionDocs;
            int activeKeysCount = hasSelectedMissing ? distinctSelectedKeys.Count : distinctAllMissingKeys.Count;
            bool canExportKeys = hasSelectedMissing || hasAnyMissing;

            if (btnCopyMissing != null)
            {
                btnCopyMissing.Enabled = canExportKeys;
                btnCopyMissing.Text = canExportKeys
                    ? string.Format(LanguageManager.GetString("IniScan.CopyKeysCountFormat", "📋 Copy Keys ({0:N0})"), activeKeysCount)
                    : LanguageManager.GetString("IniScan.CopyKeysDefault", "📋 Copy Keys");
            }

            if (btnSaveMissing != null)
            {
                btnSaveMissing.Enabled = canExportKeys;
                btnSaveMissing.Text = canExportKeys
                    ? string.Format(LanguageManager.GetString("IniScan.SaveKeysCountFormat", "💾 Save Keys ({0:N0}) as..."), activeKeysCount)
                    : LanguageManager.GetString("IniScan.SaveKeysDefault", "💾 Save Keys as...");
            }

            int nostrCount = _allResults.Count(r => r.IsNostrInline);
            bool hasNostr = nostrCount > 0;

            if (btnCopyNostr != null)
            {
                btnCopyNostr.Enabled = hasNostr;
                btnCopyNostr.Text = hasNostr
                    ? string.Format(LanguageManager.GetString("IniScan.CopyNostrCountFormat", "📋 Copy NOSTR ({0:N0})"), nostrCount)
                    : LanguageManager.GetString("IniScan.CopyNostrDefault", "📋 Copy NOSTR");
            }

            if (btnSaveNostr != null)
            {
                btnSaveNostr.Enabled = hasNostr;
                btnSaveNostr.Text = hasNostr
                    ? string.Format(LanguageManager.GetString("IniScan.SaveNostrCountFormat", "💾 Save NOSTR ({0:N0}) as..."), nostrCount)
                    : LanguageManager.GetString("IniScan.SaveNostrDefault", "💾 Save NOSTR strings as...");
            }

            if (btnViewNostr != null)
            {
                btnViewNostr.Enabled = hasNostr;
                btnViewNostr.Text = hasNostr
                    ? string.Format(LanguageManager.GetString("IniScan.ViewNostrCountFormat", "👁️ View NOSTR ({0:N0})..."), nostrCount)
                    : LanguageManager.GetString("IniScan.ViewNostrDefault", "👁️ View NOSTR...");
            }
        }

        private void PopulateAllData()
        {
            if (_allResults == null || _allResults.Count == 0)
            {
                lblSummary.Text = LanguageManager.GetString("IniScan.NoFilesPrompt", "Select or drop one or more INI/Map files above to scan string references.");
                grid.Rows.Clear();
                UpdateButtonStates();
                return;
            }

            HashSet<string> sessionKeySet = null;
            if (_session != null && _session.Documents.Count > 0)
            {
                sessionKeySet = new HashSet<string>(
                    _session.Documents.SelectMany(d => d.Document.Labels).Select(l => l.Name),
                    StringComparer.OrdinalIgnoreCase
                );
            }

            foreach (var item in _allResults)
            {
                if (!item.IsNostrInline)
                {
                    item.ExistsInCsf = (sessionKeySet != null && sessionKeySet.Contains(item.KeyName));
                    item.ExistsInRefCsf = (_refCsfKeySet != null && _refCsfKeySet.Contains(item.KeyName));
                }
                else
                {
                    item.ExistsInCsf = true;
                    item.ExistsInRefCsf = false;
                }
            }

            int missingCount = _allResults.Count(r => !r.ExistsInCsf && !r.ExistsInRefCsf && !r.IsNostrInline);
            int nostrCount = _allResults.Count(r => r.IsNostrInline);
            int existCount = _allResults.Count(r => r.ExistsInCsf && !r.IsNostrInline);
            int refCsfCount = _allResults.Count(r => !r.ExistsInCsf && r.ExistsInRefCsf && !r.IsNostrInline);

            int uniqueMissingCount = _allResults
                .Where(r => !r.ExistsInCsf && !r.ExistsInRefCsf && !r.IsNostrInline)
                .Select(r => r.KeyName)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count();
            int uniqueTotalCount = _allResults
                .Select(r => r.KeyName)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count();

            if (_refCsfKeySet != null && _refCsfKeySet.Count > 0)
            {
                lblSummary.Text = string.Format(
                    LanguageManager.GetString("IniScan.SummaryWithRefCsfFormat", "Total INI references: {0:N0} ({1:N0} unique keys)   |   🟢 In Current CSF: {2:N0}   |   🟢 In Base CSF: {3:N0}   |   🔹 Inline NOSTR: {4:N0}   |   🔴 Missing: {5:N0} ({6:N0} unique keys to add)"),
                    _allResults.Count, uniqueTotalCount, existCount, refCsfCount, nostrCount, missingCount, uniqueMissingCount);
            }
            else
            {
                lblSummary.Text = string.Format(
                    LanguageManager.GetString("IniScan.SummaryWithUniqueFormat", "Total INI references: {0:N0} ({1:N0} unique keys)   |   🟢 Exist in CSF: {2:N0}   |   🔹 Inline NOSTR: {3:N0}   |   🔴 Missing in CSF: {4:N0} ({5:N0} unique keys to add)"),
                    _allResults.Count, uniqueTotalCount, existCount, nostrCount, missingCount, uniqueMissingCount);
            }

            bool showAll = chkShowExisting != null && chkShowExisting.Checked;
            bool uniqueOnly = chkUniqueKeysOnly != null && chkUniqueKeysOnly.Checked;

            grid.Rows.Clear();
            var seenKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var refCounts = _allResults
                .GroupBy(r => r.KeyName, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);

            foreach (var item in _allResults)
            {
                bool isMissing = !item.ExistsInCsf && !item.ExistsInRefCsf && !item.IsNostrInline;

                if (!showAll && !isMissing) continue;

                if (uniqueOnly)
                {
                    if (seenKeys.Contains(item.KeyName)) continue;
                    seenKeys.Add(item.KeyName);
                }

                string statusText = item.IsNostrInline
                    ? LanguageManager.GetString("IniScan.StatusInlineNostr", "🟢 INLINE (NOSTR)")
                    : (item.ExistsInCsf
                        ? LanguageManager.GetString("IniScan.StatusExists", "🟢 EXISTS")
                        : (item.ExistsInRefCsf
                            ? LanguageManager.GetString("IniScan.StatusBaseCsf", "🟢 IN BASE CSF")
                            : LanguageManager.GetString("IniScan.StatusMissing", "🔴 MISSING")));

                int rowIndex;
                if (_isMultiFile)
                {
                    rowIndex = grid.Rows.Add(
                        item.SourceIniFile,
                        item.IniSection,
                        item.IniPropertyName,
                        item.KeyName,
                        statusText
                    );
                }
                else
                {
                    rowIndex = grid.Rows.Add(
                        item.IniSection,
                        item.IniPropertyName,
                        item.KeyName,
                        statusText
                    );
                }

                var row = grid.Rows[rowIndex];
                row.Tag = item;

                if (refCounts.TryGetValue(item.KeyName, out int count) && count > 1)
                {
                    int keyColIdx = _isMultiFile ? 3 : 2;
                    row.Cells[keyColIdx].ToolTipText = string.Format(
                        LanguageManager.GetString("IniScan.KeyRefCountTooltipFormat", "Referenced {0} times across scanned INI/MAP files."), count);
                }

                if (item.IsNostrInline)
                {
                    row.DefaultCellStyle.BackColor = Color.FromArgb(248, 245, 255);
                }
                else if (item.ExistsInRefCsf)
                {
                    row.DefaultCellStyle.BackColor = Color.FromArgb(240, 248, 255);
                }
                else if (!item.ExistsInCsf)
                {
                    row.DefaultCellStyle.BackColor = Color.FromArgb(255, 240, 240);
                }
            }

            grid.ClearSelection();
            UpdateButtonStates();
        }

        private void AddMissingKeysToSession(bool isAllMode)
        {
            if (_session == null || _session.Documents.Count == 0) return;

            List<string> targetKeys;
            int totalRefsCount;

            if (isAllMode)
            {
                var allMissingItems = _allResults.Where(r => !r.ExistsInCsf && !r.ExistsInRefCsf && !r.IsNostrInline).ToList();
                totalRefsCount = allMissingItems.Count;
                targetKeys = allMissingItems.Select(r => r.KeyName).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

                if (targetKeys.Count == 0)
                {
                    MessageBox.Show(
                        LanguageManager.GetString("Msg.NoMissingKeysToAdd", "No missing keys found to add."),
                        LanguageManager.GetString("Title.Information", "Information"),
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                    return;
                }
            }
            else
            {
                var selectedMissingItems = new List<IniScanResult>();
                if (grid.SelectedRows.Count > 0)
                {
                    foreach (DataGridViewRow row in grid.SelectedRows)
                    {
                        if (row.Tag is IniScanResult item && !item.ExistsInCsf && !item.ExistsInRefCsf && !item.IsNostrInline)
                        {
                            selectedMissingItems.Add(item);
                        }
                    }
                }

                totalRefsCount = selectedMissingItems.Count;
                targetKeys = selectedMissingItems.Select(r => r.KeyName).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

                if (targetKeys.Count == 0)
                {
                    MessageBox.Show(
                        LanguageManager.GetString("Msg.SelectMissingKeyRowToAdd", "Please select at least one missing key row in the table to add."),
                        LanguageManager.GetString("Title.NoSelection", "No Selection"),
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                    return;
                }
            }

            int docCount = _session.Documents.Count;
            int uniqueCount = targetKeys.Count;

            string modeTitle = isAllMode
                ? LanguageManager.GetString("Title.ConfirmAddAllMissingKeys", "Confirm Add ALL Missing Keys")
                : LanguageManager.GetString("Title.ConfirmAddSelectedKeys", "Confirm Add Selected Keys");

            string countDetails = (totalRefsCount > uniqueCount)
                ? string.Format(LanguageManager.GetString("IniScan.ScannedRefsDetailsFormat", "{0:N0} scanned INI reference(s) ({1:N0} unique key names)"), totalRefsCount, uniqueCount)
                : string.Format(LanguageManager.GetString("IniScan.UniqueMissingKeysDetailsFormat", "{0:N0} unique missing key name(s)"), uniqueCount);

            string confirmMsg = docCount > 1
                ? string.Format(LanguageManager.GetString("Msg.ConfirmAddKeysMultiDocFormat", "Do you want to add {0} across all {1} open CSF files?\n\n(Only unique missing keys that do not already exist will be created)."), countDetails, docCount)
                : string.Format(LanguageManager.GetString("Msg.ConfirmAddKeysSingleDocFormat", "Do you want to add {0} to the open CSF file?\n\n(Only unique missing keys that do not already exist will be created)."), countDetails);

            var dialogResult = MessageBox.Show(confirmMsg, modeTitle, MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (dialogResult != DialogResult.Yes) return;

            int createdTotal = 0;

            foreach (var sDoc in _session.Documents)
            {
                var doc = sDoc.Document;
                var existingKeys = new HashSet<string>(doc.Labels.Select(l => l.Name), StringComparer.OrdinalIgnoreCase);

                int addedForThisDoc = 0;
                foreach (var keyName in targetKeys)
                {
                    if (!existingKeys.Contains(keyName))
                    {
                        doc.Labels.Add(new CsfLabel(keyName, string.Empty));
                        existingKeys.Add(keyName);
                        addedForThisDoc++;
                    }
                }

                if (addedForThisDoc > 0)
                {
                    sDoc.IsModified = true;
                    createdTotal += addedForThisDoc;
                }
            }

            if (createdTotal > 0)
            {
                AnyKeysAdded = true;
                NewlyAddedKeys.AddRange(targetKeys);
            }

            var targetKeysSet = new HashSet<string>(targetKeys, StringComparer.OrdinalIgnoreCase);
            foreach (var item in _allResults)
            {
                if (targetKeysSet.Contains(item.KeyName))
                {
                    item.ExistsInCsf = true;
                }
            }

            string successMsg = docCount > 1
                ? string.Format(LanguageManager.GetString("Msg.KeysAddedMultiDocFormat", "Successfully added {0:N0} unique key(s) across all {1} open CSF files."), uniqueCount, docCount)
                : string.Format(LanguageManager.GetString("Msg.KeysAddedSingleDocFormat", "Successfully added {0:N0} unique key(s) to the open CSF file."), uniqueCount);

            MessageBox.Show(
                successMsg,
                LanguageManager.GetString("Title.KeysAdded", "Keys Added"),
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            PopulateAllData();
        }

        private List<IniScanResult> GetTargetMissingItems()
        {
            var selectedMissingItems = new List<IniScanResult>();
            if (grid.SelectedRows.Count > 0)
            {
                foreach (DataGridViewRow row in grid.SelectedRows)
                {
                    if (row.Tag is IniScanResult item && !item.ExistsInCsf && !item.ExistsInRefCsf && !item.IsNostrInline)
                    {
                        selectedMissingItems.Add(item);
                    }
                }
            }

            if (selectedMissingItems.Count > 0)
            {
                return selectedMissingItems;
            }

            return _allResults.Where(r => !r.ExistsInCsf && !r.ExistsInRefCsf && !r.IsNostrInline).ToList();
        }

        public static string GenerateMissingKeysExportText(IEnumerable<IniScanResult> items)
        {
            if (items == null) return string.Empty;
            var missingList = items.Where(r => !r.ExistsInCsf && !r.ExistsInRefCsf && !r.IsNostrInline).ToList();
            if (missingList.Count == 0) return string.Empty;

            var sb = new StringBuilder();

            // Example header
            sb.AppendLine("; ==============================================================================");
            sb.AppendLine("; CSF STUDIO - MISSING CSF KEYS EXPORT");
            sb.AppendLine("; ==============================================================================");
            sb.AppendLine("; Example format for CSF Studio TXT Import:");
            sb.AppendLine("; [SOME_ID]");
            sb.AppendLine("; Value=");
            sb.AppendLine(";");
            sb.AppendLine("; Fill in your translated text after 'Value=' for each entry.");
            sb.AppendLine("; Once completed, you can import this file via 'File -> Import -> Import from TXT...'.");
            sb.AppendLine("; ==============================================================================");
            sb.AppendLine();

            var seenKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var item in missingList)
            {
                if (string.IsNullOrWhiteSpace(item.KeyName)) continue;
                if (seenKeys.Contains(item.KeyName)) continue;
                seenKeys.Add(item.KeyName);

                string sourceInfo = !string.IsNullOrEmpty(item.SourceIniFile)
                    ? $"[{item.IniSection}] {item.IniPropertyName} ({item.SourceIniFile})"
                    : $"[{item.IniSection}] {item.IniPropertyName}";

                sb.AppendLine($"; Source: {sourceInfo}");
                sb.AppendLine($"[{item.KeyName}]");
                sb.AppendLine("Value=");
                sb.AppendLine();
            }

            return sb.ToString();
        }

        private void CopyMissingKeysToClipboard()
        {
            var targets = GetTargetMissingItems();
            string text = GenerateMissingKeysExportText(targets);
            if (string.IsNullOrEmpty(text))
            {
                MessageBox.Show(
                    LanguageManager.GetString("Msg.NoMissingKeysToExport", "No missing keys found to export."),
                    LanguageManager.GetString("Title.NoData", "No Data"),
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            try
            {
                Clipboard.SetText(text);
                if (btnCopyMissing != null)
                {
                    string origText = btnCopyMissing.Text;
                    btnCopyMissing.Text = "✓ " + LanguageManager.GetString("Common.Copied", "Copied!");
                    var t = new System.Windows.Forms.Timer { Interval = 1200 };
                    t.Tick += (s, e) =>
                    {
                        t.Stop();
                        t.Dispose();
                        if (!btnCopyMissing.IsDisposed) btnCopyMissing.Text = origText;
                    };
                    t.Start();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, LanguageManager.GetString("Title.Error", "Error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void SaveMissingKeysAsTxt()
        {
            var targets = GetTargetMissingItems();
            string text = GenerateMissingKeysExportText(targets);
            if (string.IsNullOrEmpty(text))
            {
                MessageBox.Show(
                    LanguageManager.GetString("Msg.NoMissingKeysToExport", "No missing keys found to export."),
                    LanguageManager.GetString("Title.NoData", "No Data"),
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            int count = targets.Select(r => r.KeyName).Distinct(StringComparer.OrdinalIgnoreCase).Count();

            using (var sfd = new SaveFileDialog())
            {
                sfd.Filter = LanguageManager.GetString("Filter.TxtFilesOnly", "Text File (*.txt)|*.txt");
                sfd.FileName = "Missing_CSF_Keys.txt";
                sfd.Title = LanguageManager.GetString("IniScan.SaveKeysDialogTitle", "Save Missing CSF Keys as...");

                if (sfd.ShowDialog(this) == DialogResult.OK)
                {
                    try
                    {
                        File.WriteAllText(sfd.FileName, text, new UTF8Encoding(false));
                        MessageBox.Show(
                            string.Format(LanguageManager.GetString("IniScan.KeysSaveSuccessFormat", "Successfully saved {0:N0} unique missing CSF key(s) to:\n{1}"), count, sfd.FileName),
                            LanguageManager.GetString("Title.Success", "Success"),
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(ex.Message, LanguageManager.GetString("Title.Error", "Error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        public static string GenerateNostrExportText(IEnumerable<IniScanResult> items)
        {
            if (items == null) return string.Empty;
            var nostrList = items.Where(r => r.IsNostrInline).ToList();
            if (nostrList.Count == 0) return string.Empty;

            var sb = new StringBuilder();

            // Example header
            sb.AppendLine("; ==============================================================================");
            sb.AppendLine("; CSF STUDIO - INLINE NOSTR: EXPORT");
            sb.AppendLine("; ==============================================================================");
            sb.AppendLine("; Example format for CSF Studio TXT Import:");
            sb.AppendLine("; [SOME_ID]");
            sb.AppendLine("; Value=");
            sb.AppendLine(";");
            sb.AppendLine("; Fill in your desired key names inside the empty brackets [] for each entry.");
            sb.AppendLine("; (You can use or copy the '; Suggested: ...' hints provided above each entry).");
            sb.AppendLine("; Once saved, you can import this file via 'File -> Import -> Import from TXT...'.");
            sb.AppendLine("; ==============================================================================");
            sb.AppendLine();

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var item in nostrList)
            {
                string uniqueToken = $"{item.SourceIniFile}:{item.IniSection}:{item.IniPropertyName}:{item.NostrValue}";
                if (seen.Contains(uniqueToken)) continue;
                seen.Add(uniqueToken);

                string sourceInfo = !string.IsNullOrEmpty(item.SourceIniFile)
                    ? $"[{item.IniSection}] {item.IniPropertyName} ({item.SourceIniFile})"
                    : $"[{item.IniSection}] {item.IniPropertyName}";

                string suggestedKey = !string.IsNullOrWhiteSpace(item.CandidateKey)
                    ? item.CandidateKey
                    : (string.Equals(item.IniPropertyName, "UIName", StringComparison.OrdinalIgnoreCase) || string.Equals(item.IniPropertyName, "Name", StringComparison.OrdinalIgnoreCase)
                        ? (!string.IsNullOrWhiteSpace(item.IniSection) ? $"Name:{item.IniSection}" : "Name:UNKNOWN")
                        : (string.Equals(item.IniPropertyName, "UIDescription", StringComparison.OrdinalIgnoreCase)
                            ? (!string.IsNullOrWhiteSpace(item.IniSection) ? $"UID:{item.IniSection}" : "UID:UNKNOWN")
                            : (!string.IsNullOrWhiteSpace(item.IniSection) ? $"{item.IniSection}_{item.IniPropertyName}" : item.IniPropertyName)));

                if (!string.IsNullOrWhiteSpace(item.InlineComment) &&
                    !string.Equals(item.InlineComment.Trim(), suggestedKey, StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(item.InlineComment.Trim(), $"[{suggestedKey}]", StringComparison.OrdinalIgnoreCase))
                {
                    sourceInfo += $" ; {item.InlineComment.Trim()}";
                }

                sb.AppendLine($"; Source: {sourceInfo}");
                if (!string.IsNullOrWhiteSpace(suggestedKey))
                {
                    sb.AppendLine($"; Suggested: {suggestedKey}");
                }
                sb.AppendLine("[]");
                sb.AppendLine($"Value={item.NostrValue ?? string.Empty}");
                sb.AppendLine();
            }

            return sb.ToString();
        }

        private void CopyNostrToClipboard()
        {
            string text = GenerateNostrExportText(_allResults);
            if (string.IsNullOrEmpty(text)) return;

            try
            {
                Clipboard.SetText(text);
                if (btnCopyNostr != null)
                {
                    string origText = btnCopyNostr.Text;
                    btnCopyNostr.Text = "✓ " + LanguageManager.GetString("Common.Copied", "Copied!");
                    var t = new System.Windows.Forms.Timer { Interval = 1200 };
                    t.Tick += (s, e) =>
                    {
                        t.Stop();
                        t.Dispose();
                        if (!btnCopyNostr.IsDisposed) btnCopyNostr.Text = origText;
                    };
                    t.Start();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, LanguageManager.GetString("Title.Error", "Error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void SaveNostrStringsAsTxt()
        {
            string text = GenerateNostrExportText(_allResults);
            if (string.IsNullOrEmpty(text)) return;

            using (var sfd = new SaveFileDialog())
            {
                sfd.Filter = LanguageManager.GetString("Filter.TxtFilesOnly", "Text File (*.txt)|*.txt");
                sfd.FileName = "nostr_strings.txt";
                sfd.Title = LanguageManager.GetString("IniScan.SaveNostrDialogTitle", "Save NOSTR strings as...");

                if (sfd.ShowDialog(this) == DialogResult.OK)
                {
                    try
                    {
                        File.WriteAllText(sfd.FileName, text, new UTF8Encoding(false));
                        int count = _allResults.Count(r => r.IsNostrInline);
                        MessageBox.Show(
                            string.Format(LanguageManager.GetString("IniScan.NostrSaveSuccessFormat", "Successfully saved {0:N0} NOSTR entries to:\n{1}"), count, sfd.FileName),
                            LanguageManager.GetString("Title.Success", "Success"),
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(ex.Message, LanguageManager.GetString("Title.Error", "Error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        private void ShowNostrTemplateDialog()
        {
            var nostrItems = _allResults.Where(r => r.IsNostrInline).ToList();
            if (nostrItems.Count == 0) return;

            using (var dlg = new NostrTemplateViewerDialog(nostrItems))
            {
                dlg.ShowDialog(this);
            }
        }
    }

    public class NostrTemplateViewerDialog : Form
    {
        private readonly List<IniScanResult> _nostrItems;

        private TextBox txtContent;
        private Button btnCopy;
        private Button btnSave;
        private Button btnClose;
        private Label lblHeader;

        public NostrTemplateViewerDialog(List<IniScanResult> nostrItems)
        {
            _nostrItems = nostrItems ?? new List<IniScanResult>();

            InitializeComponent();
            txtContent.Text = IniScanResultDialog.GenerateNostrExportText(_nostrItems);
            txtContent.SelectionStart = 0;
            txtContent.SelectionLength = 0;
        }

        private void InitializeComponent()
        {
            this.Text = LanguageManager.GetString("IniScan.NostrDialogTitle", "Inline NOSTR: Literals - Export Preview");
            this.Size = new Size(840, 620);
            this.MinimumSize = new Size(680, 480);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.Sizable;
            this.ShowIcon = false;
            this.ShowInTaskbar = false;

            var mainTable = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                Padding = new Padding(12)
            };
            mainTable.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            mainTable.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            mainTable.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            // Top panel
            var topPanel = new Panel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 8)
            };

            lblHeader = new Label
            {
                Text = string.Format(LanguageManager.GetString("IniScan.NostrDialogHeaderFormat", "📄 {0:N0} inline NOSTR: literal(s) detected in scanned INI/Map files."), _nostrItems.Count),
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = Color.FromArgb(30, 30, 30),
                Dock = DockStyle.Top,
                AutoSize = true
            };

            topPanel.Controls.Add(lblHeader);

            // Middle TextBox
            txtContent = new TextBox
            {
                Multiline = true,
                ScrollBars = ScrollBars.Both,
                WordWrap = false,
                Dock = DockStyle.Fill,
                Font = new Font("Consolas", 10F),
                BackColor = Color.FromArgb(254, 254, 255),
                ForeColor = Color.FromArgb(30, 30, 30),
                Margin = new Padding(0, 0, 0, 8)
            };

            // Bottom FlowLayoutPanel (Right to left)
            var bottomPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                AutoSize = true,
                Margin = new Padding(0, 4, 0, 0)
            };

            btnClose = new Button
            {
                Text = LanguageManager.GetString("Common.Close", "Close"),
                Size = new Size(95, 32),
                UseVisualStyleBackColor = true
            };
            btnClose.Click += (s, e) => this.Close();

            btnSave = new Button
            {
                Text = LanguageManager.GetString("IniScan.NostrDialogSave", "💾 Save to TXT..."),
                Size = new Size(150, 32),
                UseVisualStyleBackColor = true
            };
            btnSave.Click += BtnSave_Click;

            btnCopy = new Button
            {
                Text = LanguageManager.GetString("IniScan.NostrDialogCopy", "📋 Copy All"),
                Size = new Size(125, 32),
                UseVisualStyleBackColor = true
            };
            btnCopy.Click += BtnCopy_Click;

            bottomPanel.Controls.Add(btnClose);
            bottomPanel.Controls.Add(btnSave);
            bottomPanel.Controls.Add(btnCopy);

            mainTable.Controls.Add(topPanel, 0, 0);
            mainTable.Controls.Add(txtContent, 0, 1);
            mainTable.Controls.Add(bottomPanel, 0, 2);

            this.Controls.Add(mainTable);
        }

        private void BtnCopy_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(txtContent.Text)) return;
            try
            {
                Clipboard.SetText(txtContent.Text);
                if (btnCopy != null)
                {
                    string origText = btnCopy.Text;
                    btnCopy.Text = "✓ " + LanguageManager.GetString("Common.Copied", "Copied!");
                    var t = new System.Windows.Forms.Timer { Interval = 1200 };
                    t.Tick += (s, ev) =>
                    {
                        t.Stop();
                        t.Dispose();
                        if (!btnCopy.IsDisposed) btnCopy.Text = origText;
                    };
                    t.Start();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, LanguageManager.GetString("Title.Error", "Error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnSave_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(txtContent.Text)) return;

            using (var sfd = new SaveFileDialog())
            {
                sfd.Filter = LanguageManager.GetString("Filter.TxtFilesOnly", "Text File (*.txt)|*.txt");
                sfd.FileName = "nostr_strings.txt";
                sfd.Title = LanguageManager.GetString("IniScan.SaveNostrDialogTitle", "Save NOSTR strings as...");

                if (sfd.ShowDialog(this) == DialogResult.OK)
                {
                    try
                    {
                        File.WriteAllText(sfd.FileName, txtContent.Text, new UTF8Encoding(false));
                        MessageBox.Show(
                            string.Format(LanguageManager.GetString("IniScan.NostrDialogSaveSuccess", "Template saved successfully to:\n{0}"), sfd.FileName),
                            LanguageManager.GetString("Title.Success", "Success"),
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(ex.Message, LanguageManager.GetString("Title.Error", "Error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }
    }
}
