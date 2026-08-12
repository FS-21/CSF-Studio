using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using CsfStudio.Core;

namespace CsfStudio.UI
{
    public class IniScanResultDialog : Form
    {
        private Button btnExportResults;
        private Button btnCreateMissing;
        private Button btnCreateAllMissing;
        private Button btnClose;
        private CheckBox chkShowExisting;
        private Label lblSummary;
        private Label lblFilePath;
        private DataGridView grid;

        private CsfSession _session;
        public Dictionary<string, List<IniScanResult>> ScannedFilesMap { get; private set; }
        public bool AnyKeysAdded { get; private set; } = false;
        private List<IniScanResult> _allResults = new List<IniScanResult>();
        private bool _isMultiFile;

        public IniScanResultDialog(Dictionary<string, List<IniScanResult>> scannedFilesMap, CsfSession session)
        {
            ScannedFilesMap = scannedFilesMap ?? new Dictionary<string, List<IniScanResult>>(StringComparer.OrdinalIgnoreCase);
            _session = session;

            _allResults = new List<IniScanResult>();
            foreach (var list in ScannedFilesMap.Values)
            {
                if (list != null) _allResults.AddRange(list);
            }

            _isMultiFile = ScannedFilesMap.Count > 1;

            InitializeComponent();
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
            this.Text = "C&C INI Reference Scanner Results";
            this.Size = new Size(980, 560);
            this.MinimumSize = new Size(820, 440);
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

            mainTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 78F));
            mainTable.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            mainTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 55F));

            // 1. TOP PANEL
            var panelTop = new Panel { Dock = DockStyle.Fill, Height = 78, Padding = new Padding(10), BackColor = Color.FromArgb(245, 247, 250), Margin = new Padding(0) };

            string headerText = _isMultiFile
                ? $"📄 Scanned Files ({ScannedFilesMap.Count} files): {string.Join(", ", ScannedFilesMap.Keys.Select(Path.GetFileName))}"
                : $"📄 Scanned INI File: {(ScannedFilesMap.Keys.FirstOrDefault() ?? "N/A")}";

            lblFilePath = new Label 
            { 
                Text = headerText,
                Location = new Point(10, 8), 
                AutoSize = true, 
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.FromArgb(40, 60, 90)
            };

            lblSummary = new Label 
            { 
                Text = "Calculating...",
                Location = new Point(10, 30), 
                AutoSize = true, 
                Font = new Font("Segoe UI", 9F, FontStyle.Regular),
                ForeColor = Color.Black
            };

            chkShowExisting = new CheckBox
            {
                Text = "☑ Show Existing Keys (🟢 EXISTS) & Inline Literals (🔹 NOSTR)",
                Location = new Point(10, 52),
                AutoSize = true,
                Checked = false, // Hidden by default
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.FromArgb(20, 60, 120),
                Cursor = Cursors.Hand
            };

            chkShowExisting.CheckedChanged += (s, e) => PopulateAllData();

            panelTop.Controls.Add(lblFilePath);
            panelTop.Controls.Add(lblSummary);
            panelTop.Controls.Add(chkShowExisting);

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

            var boldStyle = new DataGridViewCellStyle { Font = new Font("Segoe UI", 9F, FontStyle.Bold) };

            if (_isMultiFile)
            {
                var colFile = new DataGridViewTextBoxColumn { HeaderText = "INI File", ReadOnly = true, FillWeight = 20 };
                var colSection = new DataGridViewTextBoxColumn { HeaderText = "INI Section", ReadOnly = true, FillWeight = 20 };
                var colProperty = new DataGridViewTextBoxColumn { HeaderText = "INI Property", ReadOnly = true, FillWeight = 18 };
                var colKey = new DataGridViewTextBoxColumn { HeaderText = "Referenced Key", ReadOnly = true, FillWeight = 27, DefaultCellStyle = boldStyle };
                var colStatus = new DataGridViewTextBoxColumn { HeaderText = "CSF Status", ReadOnly = true, FillWeight = 15 };

                grid.Columns.AddRange(colFile, colSection, colProperty, colKey, colStatus);
            }
            else
            {
                var colSection = new DataGridViewTextBoxColumn { HeaderText = "INI Section", ReadOnly = true, FillWeight = 25 };
                var colProperty = new DataGridViewTextBoxColumn { HeaderText = "INI Property", ReadOnly = true, FillWeight = 20 };
                var colKey = new DataGridViewTextBoxColumn { HeaderText = "Referenced Key", ReadOnly = true, FillWeight = 35, DefaultCellStyle = boldStyle };
                var colStatus = new DataGridViewTextBoxColumn { HeaderText = "CSF Status", ReadOnly = true, FillWeight = 20 };

                grid.Columns.AddRange(colSection, colProperty, colKey, colStatus);
            }

            grid.CellToolTipTextNeeded += (s, e) =>
            {
                if (_isMultiFile && e.ColumnIndex == 0 && e.RowIndex >= 0 && e.RowIndex < grid.Rows.Count)
                {
                    var row = grid.Rows[e.RowIndex];
                    if (row.Tag is IniScanResult item && !string.IsNullOrEmpty(item.FullIniPath))
                    {
                        e.ToolTipText = $"Full Path: {item.FullIniPath}";
                    }
                }
            };

            grid.SelectionChanged += (s, e) => UpdateButtonStates();

            // 3. BOTTOM ACTION PANEL
            var panelBottom = new FlowLayoutPanel 
            { 
                Dock = DockStyle.Fill, 
                Height = 55, 
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
                Padding = new Padding(10, 10, 15, 10),
                Margin = new Padding(0),
                BackColor = Color.FromArgb(240, 242, 245) 
            };

            btnClose = new Button 
            { 
                Text = "Close", 
                Size = new Size(90, 32),
                Margin = new Padding(6, 0, 6, 0),
                UseVisualStyleBackColor = true
            };
            btnClose.Click += (s, e) =>
            {
                this.DialogResult = AnyKeysAdded ? DialogResult.OK : DialogResult.Cancel;
                this.Close();
            };

            btnCreateAllMissing = new Button 
            { 
                Text = "⚡ Add ALL Missing Keys to CSF", 
                Size = new Size(270, 32),
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Margin = new Padding(6, 0, 6, 0),
                Enabled = false,
                UseVisualStyleBackColor = true
            };

            btnCreateMissing = new Button 
            { 
                Text = "⚡ Add Selected Keys...", 
                Size = new Size(185, 32),
                Font = new Font("Segoe UI", 9F, FontStyle.Regular),
                Margin = new Padding(6, 0, 6, 0),
                Enabled = false,
                UseVisualStyleBackColor = true
            };

            btnExportResults = new Button 
            { 
                Text = "📋 Export Results...", 
                Size = new Size(170, 32),
                Font = new Font("Segoe UI", 9F, FontStyle.Regular),
                Margin = new Padding(6, 0, 6, 0),
                Enabled = false,
                UseVisualStyleBackColor = true
            };

            btnExportResults.Click += BtnExportResults_Click;
            btnCreateMissing.Click += (s, e) => AddMissingKeysToSession(isAllMode: false);
            btnCreateAllMissing.Click += (s, e) => AddMissingKeysToSession(isAllMode: true);

            panelBottom.Controls.Add(btnClose);
            panelBottom.Controls.Add(btnCreateAllMissing);
            panelBottom.Controls.Add(btnCreateMissing);
            panelBottom.Controls.Add(btnExportResults);

            mainTable.Controls.Add(panelTop, 0, 0);
            mainTable.Controls.Add(grid, 0, 1);
            mainTable.Controls.Add(panelBottom, 0, 2);

            this.Controls.Add(mainTable);

            ToolTipHelper.SetToolTip(btnExportResults, "Export Results: Export missing CSF key names to a plain text file or CSV spreadsheet using standard app exporters.");
            ToolTipHelper.SetToolTip(btnCreateAllMissing, "Add ALL Missing Keys: Automatically add ALL unique missing string key slots found in scan into open CSF files.");
            ToolTipHelper.SetToolTip(btnCreateMissing, "Add Selected Keys: Automatically add only selected missing string key slots into open CSF files.");
            ToolTipHelper.SetToolTip(btnClose, "Close window.");
        }

        private void UpdateButtonStates()
        {
            var selectedMissingItems = new List<IniScanResult>();
            if (grid.SelectedRows.Count > 0)
            {
                foreach (DataGridViewRow row in grid.SelectedRows)
                {
                    if (row.Tag is IniScanResult item && !item.ExistsInCsf && !item.IsNostrInline)
                    {
                        selectedMissingItems.Add(item);
                    }
                }
            }

            var distinctSelectedKeys = selectedMissingItems.Select(r => r.KeyName).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            bool hasSelectedMissing = distinctSelectedKeys.Count > 0;

            var allMissingItems = _allResults.Where(r => !r.ExistsInCsf && !r.IsNostrInline).ToList();
            var distinctAllMissingKeys = allMissingItems.Select(r => r.KeyName).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            bool hasAnyMissing = distinctAllMissingKeys.Count > 0;

            bool isMultiCsf = (_session != null && _session.Documents.Count > 1);
            bool hasSessionDocs = (_session != null && _session.Documents.Count > 0);

            btnCreateAllMissing.Enabled = hasAnyMissing && hasSessionDocs;
            btnCreateMissing.Enabled = hasSelectedMissing && hasSessionDocs;
            btnExportResults.Enabled = hasSelectedMissing || hasAnyMissing;

            if (hasAnyMissing)
            {
                btnCreateAllMissing.Text = isMultiCsf
                    ? $"⚡ Add ALL ({distinctAllMissingKeys.Count:N0} Unique) Keys to All CSFs"
                    : $"⚡ Add ALL ({distinctAllMissingKeys.Count:N0} Unique) Keys to CSF";
            }
            else
            {
                btnCreateAllMissing.Text = isMultiCsf
                    ? "⚡ Add ALL Missing Keys to All CSFs"
                    : "⚡ Add ALL Missing Keys to CSF";
            }

            if (hasSelectedMissing)
            {
                btnCreateMissing.Text = $"⚡ Add Selected ({distinctSelectedKeys.Count:N0})";
            }
            else
            {
                btnCreateMissing.Text = "⚡ Add Selected Keys...";
            }
        }

        private void PopulateAllData()
        {
            if (_session != null && _session.Documents.Count > 0)
            {
                var sessionKeySet = new HashSet<string>(
                    _session.Documents.SelectMany(d => d.Document.Labels).Select(l => l.Name),
                    StringComparer.OrdinalIgnoreCase
                );

                foreach (var item in _allResults)
                {
                    if (!item.IsNostrInline && sessionKeySet.Contains(item.KeyName))
                    {
                        item.ExistsInCsf = true;
                    }
                }
            }

            int missingCount = _allResults.Count(r => !r.ExistsInCsf && !r.IsNostrInline);
            int nostrCount = _allResults.Count(r => r.IsNostrInline);
            int existCount = _allResults.Count(r => r.ExistsInCsf && !r.IsNostrInline);

            lblSummary.Text = $"Total INI references: {_allResults.Count:N0}   |   🟢 Exist in CSF: {existCount:N0}   |   🔹 Inline NOSTR: {nostrCount:N0}   |   🔴 Missing in CSF: {missingCount:N0}";

            bool showAll = chkShowExisting != null && chkShowExisting.Checked;

            grid.Rows.Clear();

            foreach (var item in _allResults)
            {
                bool isMissing = !item.ExistsInCsf && !item.IsNostrInline;

                if (!showAll && !isMissing) continue;

                string statusText = item.IsNostrInline ? "🟢 INLINE (NOSTR)" : (item.ExistsInCsf ? "🟢 EXISTS" : "🔴 MISSING");

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

                if (item.IsNostrInline)
                {
                    row.DefaultCellStyle.BackColor = Color.FromArgb(235, 245, 255);
                }
                else if (!item.ExistsInCsf)
                {
                    row.DefaultCellStyle.BackColor = Color.FromArgb(255, 235, 235);
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
                var allMissingItems = _allResults.Where(r => !r.ExistsInCsf && !r.IsNostrInline).ToList();
                totalRefsCount = allMissingItems.Count;
                targetKeys = allMissingItems.Select(r => r.KeyName).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

                if (targetKeys.Count == 0)
                {
                    MessageBox.Show("No missing keys found to add.", "Information", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
                        if (row.Tag is IniScanResult item && !item.ExistsInCsf && !item.IsNostrInline)
                        {
                            selectedMissingItems.Add(item);
                        }
                    }
                }

                totalRefsCount = selectedMissingItems.Count;
                targetKeys = selectedMissingItems.Select(r => r.KeyName).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

                if (targetKeys.Count == 0)
                {
                    MessageBox.Show("Please select at least one missing key row in the table to add.", "No Selection", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
            }

            int docCount = _session.Documents.Count;
            int uniqueCount = targetKeys.Count;

            string modeTitle = isAllMode ? "Confirm Add ALL Missing Keys" : "Confirm Add Selected Keys";
            string countDetails = (totalRefsCount > uniqueCount)
                ? $"{totalRefsCount:N0} scanned INI reference(s) ({uniqueCount:N0} unique key names)"
                : $"{uniqueCount:N0} unique missing key name(s)";

            string confirmMsg = docCount > 1
                ? $"Do you want to add {countDetails} across all {docCount} open CSF files?\n\n(Only unique missing keys that do not already exist will be created)."
                : $"Do you want to add {countDetails} to the open CSF file?\n\n(Only unique missing keys that do not already exist will be created).";

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
                ? $"Successfully added {uniqueCount:N0} unique key(s) across all {docCount} open CSF files."
                : $"Successfully added {uniqueCount:N0} unique key(s) to the open CSF file.";

            MessageBox.Show(successMsg, "Keys Added", MessageBoxButtons.OK, MessageBoxIcon.Information);
            PopulateAllData();
        }

        private void BtnExportResults_Click(object sender, EventArgs e)
        {
            var selectedMissingItems = new List<IniScanResult>();
            if (grid.SelectedRows.Count > 0)
            {
                foreach (DataGridViewRow row in grid.SelectedRows)
                {
                    if (row.Tag is IniScanResult item && !item.ExistsInCsf && !item.IsNostrInline)
                    {
                        selectedMissingItems.Add(item);
                    }
                }
            }

            if (selectedMissingItems.Count == 0)
            {
                selectedMissingItems = _allResults.Where(r => !r.ExistsInCsf && !r.IsNostrInline).ToList();
            }

            var distinctSelectedKeys = selectedMissingItems.Select(r => r.KeyName).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if (distinctSelectedKeys.Count == 0)
            {
                MessageBox.Show("No missing keys found to export.", "No Data", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using (var sfd = new SaveFileDialog())
            {
                sfd.Filter = "Text File (*.txt)|*.txt";
                sfd.Title = "Export Missing CSF Keys List";
                sfd.FileName = "Missing_CSF_Keys.txt";

                if (sfd.ShowDialog() == DialogResult.OK)
                {
                    CsfTxtExporterImporter.ExportKeyStructureToTxt(distinctSelectedKeys, sfd.FileName);
                    MessageBox.Show($"Exported {distinctSelectedKeys.Count} missing CSF key(s) to '{Path.GetFileName(sfd.FileName)}'.", "Export Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
        }
    }
}
