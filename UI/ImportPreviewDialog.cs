using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using CsfStudio.Core;

namespace CsfStudio.UI
{
    public enum ImportContentMode
    {
        All = 0,
        KeysOnly = 1,
        TextOnly = 2,
        AudioOnly = 3
    }

    public class ImportPreviewDialog : Form
    {
        private DataGridView gridDiff;
        private Button btnImportSelected;
        private Button btnCancel;
        private Label lblSummary;
        private Label lblTargetScope;
        private ComboBox cmbTargetScope;
        private Label lblImportMode;
        private ComboBox cmbImportMode;
        private CheckBox chkOverwriteConflicts;
        private const int CheckColumnIndex = 1;

        private CsfSession _session;
        private CsfSessionDocument _defaultTargetDoc;
        private List<CsfLabel> _importedLabels;

        public List<ImportKeyDiff> DiffList { get; private set; }
        public CsfSessionDocument SelectedTargetDocument { get; private set; }
        public bool ImportToAllDocuments { get; private set; } = false;
        public ImportContentMode ContentMode { get; private set; } = ImportContentMode.All;
        public bool OverwriteConflicts { get; private set; } = false;

        public ImportPreviewDialog(List<ImportKeyDiff> diffList, CsfSession session = null, CsfSessionDocument defaultTargetDoc = null, List<CsfLabel> importedLabels = null)
        {
            DiffList = diffList ?? new List<ImportKeyDiff>();
            _session = session;
            _defaultTargetDoc = defaultTargetDoc;
            _importedLabels = importedLabels;
            InitializeComponent();
            PopulateTargetScopeCombo();
            RecomputeDiffsForSelectedTarget();
            if (_importedLabels == null) PopulateGrid();
        }

        private void InitializeComponent()
        {
            this.Text = "UTF-8 Text Import Preview & Diff";
            this.Size = new Size(880, 550);
            this.StartPosition = FormStartPosition.CenterParent;
            this.ShowIcon = false;

            var panelTop = new Panel { Dock = DockStyle.Top, Height = 45, Padding = new Padding(10) };
            lblSummary = new Label { Location = new Point(10, 12), AutoSize = true, Font = new Font(FontFamily.GenericSansSerif, 9, FontStyle.Bold) };

            var btnSelectNewOnly = new Button { Text = "🆕 New Keys Only", Location = new Point(430, 8), Size = new Size(115, 26) };
            btnSelectNewOnly.Click += (s, e) =>
            {
                foreach (DataGridViewRow row in gridDiff.Rows)
                {
                    if (row.Tag is ImportKeyDiff item) row.Cells[CheckColumnIndex].Value = item.IsNewKey;
                }
            };

            var btnSelectConflictsOnly = new Button { Text = "⚡ Conflicts Only", Location = new Point(550, 8), Size = new Size(110, 26) };
            btnSelectConflictsOnly.Click += (s, e) =>
            {
                foreach (DataGridViewRow row in gridDiff.Rows)
                {
                    if (row.Tag is ImportKeyDiff item) row.Cells[CheckColumnIndex].Value = !item.IsNewKey;
                }
            };

            var btnCheckAll = new Button { Text = "☑️ Check All", Location = new Point(665, 8), Size = new Size(90, 26) };
            btnCheckAll.Click += (s, e) =>
            {
                foreach (DataGridViewRow row in gridDiff.Rows) row.Cells[CheckColumnIndex].Value = true;
            };

            var btnUncheckAll = new Button { Text = "⬜ Uncheck All", Location = new Point(760, 8), Size = new Size(95, 26) };
            btnUncheckAll.Click += (s, e) =>
            {
                foreach (DataGridViewRow row in gridDiff.Rows) row.Cells[CheckColumnIndex].Value = false;
            };

            panelTop.Controls.Add(lblSummary);
            panelTop.Controls.Add(btnSelectNewOnly);
            panelTop.Controls.Add(btnSelectConflictsOnly);
            panelTop.Controls.Add(btnCheckAll);
            panelTop.Controls.Add(btnUncheckAll);

            gridDiff = new DataGridView
            {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = true,
                RowHeadersVisible = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
            };

            var colTarget = new DataGridViewTextBoxColumn { HeaderText = "Target CSF", ReadOnly = true, Width = 180 };
            var colCheck = new DataGridViewCheckBoxColumn { HeaderText = "Import", Width = 70 };
            var colKey = new DataGridViewTextBoxColumn { HeaderText = "Label (Key)", ReadOnly = true, Width = 160 };
            var colType = new DataGridViewTextBoxColumn { HeaderText = "Status", ReadOnly = true, Width = 100 };
            var colCurrent = new DataGridViewTextBoxColumn { HeaderText = "Current CSF Value", ReadOnly = true };
            var colImported = new DataGridViewTextBoxColumn { HeaderText = "Imported Value", ReadOnly = true };

            gridDiff.Columns.AddRange(colTarget, colCheck, colKey, colType, colCurrent, colImported);

            var panelBottom = new Panel { Dock = DockStyle.Bottom, Height = 80, Padding = new Padding(10) };
            
            lblTargetScope = new Label
            {
                Text = "Target File:",
                Location = new Point(10, 15),
                AutoSize = true,
                Font = new Font(FontFamily.GenericSansSerif, 8.5f, FontStyle.Bold)
            };

            cmbTargetScope = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Location = new Point(95, 12),
                Size = new Size(240, 24),
                Font = new Font(FontFamily.GenericSansSerif, 8.5f)
            };
            cmbTargetScope.SelectedIndexChanged += (s, e) => RecomputeDiffsForSelectedTarget();

            lblImportMode = new Label
            {
                Text = "Import Content:",
                Location = new Point(345, 15),
                AutoSize = true,
                Font = new Font(FontFamily.GenericSansSerif, 8.5f, FontStyle.Bold)
            };

            cmbImportMode = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Location = new Point(445, 12),
                Size = new Size(220, 24),
                Font = new Font(FontFamily.GenericSansSerif, 8.5f)
            };
            cmbImportMode.Items.Add("Everything");
            cmbImportMode.Items.Add("Keys Only");
            cmbImportMode.Items.Add("Text Values Only");
            cmbImportMode.Items.Add("Audio WAVs Only");
            cmbImportMode.SelectedIndex = 0;

            chkOverwriteConflicts = new CheckBox
            {
                Text = "⚡ Overwrite Existing Conflicts",
                Location = new Point(10, 48),
                AutoSize = true,
                Checked = false,
                Font = new Font(FontFamily.GenericSansSerif, 8.5f, FontStyle.Bold),
                ForeColor = Color.DarkRed
            };
            chkOverwriteConflicts.CheckedChanged += (s, e) =>
            {
                bool checkState = chkOverwriteConflicts.Checked;
                foreach (DataGridViewRow row in gridDiff.Rows)
                {
                    if (row.Tag is ImportKeyDiff item && !item.IsNewKey)
                    {
                        row.Cells[CheckColumnIndex].Value = checkState;
                    }
                }
            };

            btnImportSelected = new Button { Text = "Import Selected", DialogResult = DialogResult.OK, Location = new Point(590, 42), Size = new Size(150, 30), Font = new Font(FontFamily.GenericSansSerif, 8.5f, FontStyle.Bold) };
            btnCancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Location = new Point(750, 42), Size = new Size(90, 30) };

            btnImportSelected.Click += BtnImportSelected_Click;

            panelBottom.Controls.Add(lblTargetScope);
            panelBottom.Controls.Add(cmbTargetScope);
            panelBottom.Controls.Add(lblImportMode);
            panelBottom.Controls.Add(cmbImportMode);
            panelBottom.Controls.Add(chkOverwriteConflicts);
            panelBottom.Controls.Add(btnImportSelected);
            panelBottom.Controls.Add(btnCancel);

            this.Controls.Add(gridDiff);
            this.Controls.Add(panelTop);
            this.Controls.Add(panelBottom);
        }

        private void PopulateTargetScopeCombo()
        {
            cmbTargetScope.Items.Clear();
            if (_session == null || _session.Documents.Count == 0)
            {
                lblTargetScope.Visible = false;
                cmbTargetScope.Visible = false;
                return;
            }

            int selectedIdx = 0;
            for (int i = 0; i < _session.Documents.Count; i++)
            {
                var doc = _session.Documents[i];
                string isMain = doc == _session.BaseDocument ? " 📌 BASE" : "";
                string fileName = System.IO.Path.GetFileName(doc.FilePath);
                string itemText = $"[{doc.LanguageTag}]{isMain} - {fileName} - {doc.Document.Labels.Count:N0} keys";
                cmbTargetScope.Items.Add(itemText);

                if (_defaultTargetDoc != null && doc == _defaultTargetDoc)
                {
                    selectedIdx = i;
                }
            }

            if (_session.Documents.Count > 1)
            {
                cmbTargetScope.Items.Add("⚠️ ALL OPEN CSF FILES IN SESSION");
            }

            cmbTargetScope.SelectedIndex = selectedIdx;
        }

        // The diff must always be computed against the document chosen in Target File,
        // not against the base document. Recompute whenever the selection changes.
        private void RecomputeDiffsForSelectedTarget()
        {
            if (_importedLabels == null || _session == null || _session.Documents.Count == 0) return;
            if (cmbTargetScope.SelectedIndex < 0) return;

            if (_session.Documents.Count > 1 && cmbTargetScope.SelectedIndex == _session.Documents.Count)
            {
                // ALL scope: build one diff row per target document and key.
                DiffList = new List<ImportKeyDiff>();
                foreach (var document in _session.Documents)
                {
                    var documentDiffs = CsfTxtExporterImporter.CompareImportDiff(document.Document, _importedLabels);
                    foreach (var item in documentDiffs)
                    {
                        item.TargetDocument = document;
                        DiffList.Add(item);
                    }
                }
                PopulateGrid();
                return;
            }

            CsfSessionDocument targetDoc;
            if (cmbTargetScope.SelectedIndex < _session.Documents.Count)
            {
                targetDoc = _session.Documents[cmbTargetScope.SelectedIndex];
            }
            else
            {
                return;
            }

            if (targetDoc?.Document == null) return;

            DiffList = CsfTxtExporterImporter.CompareImportDiff(targetDoc.Document, _importedLabels);
            foreach (var item in DiffList) item.TargetDocument = targetDoc;
            PopulateGrid();
        }

        private void PopulateGrid()
        {
            gridDiff.Rows.Clear();
            int newCount = 0;
            int conflictCount = 0;

            foreach (var item in DiffList)
            {
                if (item.IsNewKey) newCount++;
                else conflictCount++;

                string targetDisplay = item.TargetDocument == null
                    ? string.Empty
                    : $"[{item.TargetDocument.LanguageTag}] {item.TargetDocument.FileName}";

                int rowIndex = gridDiff.Rows.Add(
                    targetDisplay,
                    item.ShouldImport,
                    item.KeyName,
                    item.IsNewKey ? "🆕 NEW" : "⚡ CONFLICT",
                    item.CurrentValue ?? "(Missing)",
                    item.ImportedValue
                );

                var row = gridDiff.Rows[rowIndex];
                row.Tag = item;

                if (item.IsNewKey)
                {
                    row.DefaultCellStyle.BackColor = Color.FromArgb(235, 247, 235);
                }
                else
                {
                    row.DefaultCellStyle.BackColor = Color.FromArgb(255, 245, 230);
                }
            }

            lblSummary.Text = $"Total keys to import: {DiffList.Count} | 🆕 New: {newCount} | ⚡ Conflicts: {conflictCount}";
        }

        private void BtnImportSelected_Click(object sender, EventArgs e)
        {
            ContentMode = (ImportContentMode)Math.Max(0, cmbImportMode.SelectedIndex);
            OverwriteConflicts = chkOverwriteConflicts != null && chkOverwriteConflicts.Checked;

            foreach (DataGridViewRow row in gridDiff.Rows)
            {
                if (row.Tag is ImportKeyDiff item)
                {
                    item.ShouldImport = Convert.ToBoolean(row.Cells[CheckColumnIndex].Value);
                }
            }

            if (_session != null && _session.Documents.Count > 0 && cmbTargetScope.SelectedIndex >= 0)
            {
                if (_session.Documents.Count > 1 && cmbTargetScope.SelectedIndex == _session.Documents.Count)
                {
                    ImportToAllDocuments = true;
                    SelectedTargetDocument = null;
                }
                else if (cmbTargetScope.SelectedIndex < _session.Documents.Count)
                {
                    ImportToAllDocuments = false;
                    SelectedTargetDocument = _session.Documents[cmbTargetScope.SelectedIndex];
                }
            }
            else
            {
                ImportToAllDocuments = false;
                SelectedTargetDocument = _defaultTargetDoc;
            }
        }
    }
}
