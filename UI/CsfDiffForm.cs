using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using CsfStudio.Core;

namespace CsfStudio.UI
{
    public class CsfDiffForm : Form
    {
        private CsfSession _session;
        private CsfDocument _docA;
        private CsfDocument _docB;
        private string _filePathA;
        private string _filePathB;

        private CsfDiffResult _diffResult;

        private ComboBox cboFileA;
        private Button btnBrowseA;
        private ComboBox cboFileB;
        private Button btnBrowseB;

        private Button btnPrevDiff;
        private Button btnNextDiff;
        private Label lblDiffCounter;

        private Button btnFilterAll;
        private Button btnFilterModified;
        private Button btnFilterAdded;
        private Button btnFilterRemoved;

        private Button btnCopyBToA;
        private Button btnCopyAToB;
        private Button btnSaveA;
        private Button btnSaveB;

        private DataGridView gridDiff;
        private StatusStrip statusStrip;
        private ToolStripStatusLabel lblStatusInfo;

        private CsfDiffStatus? _activeStatusFilter = null; // null = show all
        private List<CsfDiffItem> _filteredItems = new List<CsfDiffItem>();

        public CsfDiffForm(CsfSession session, CsfDocument initialDocA = null, CsfDocument initialDocB = null)
        {
            _session = session;
            _docA = initialDocA;
            _docB = initialDocB;

            InitializeComponent();
            PopulateFileDropdowns();

            if (_docA != null && _docB != null)
            {
                SelectDocumentInCombo(cboFileA, _docA);
                SelectDocumentInCombo(cboFileB, _docB);
                RunComparison();
            }
        }

        private void InitializeComponent()
        {
            this.Text = "🔀 CSF Diff & Merge";
            this.Size = new Size(1050, 700);
            this.MinimumSize = new Size(850, 520);
            this.StartPosition = FormStartPosition.CenterParent;
            this.ShowIcon = false;
            this.KeyPreview = true;

            // Top Panel (File Selectors)
            var pnlTop = new Panel { Dock = DockStyle.Top, Height = 75, BackColor = Color.FromArgb(243, 244, 246), Padding = new Padding(12, 8, 12, 8) };

            var lblA = new Label { Text = "📄 CSF de Referencia:", Location = new Point(12, 12), AutoSize = true, Font = new Font(FontFamily.GenericSansSerif, 8.5f, FontStyle.Bold) };
            cboFileA = new ComboBox { Location = new Point(165, 9), Size = new Size(300, 23), DropDownStyle = ComboBoxStyle.DropDownList };
            btnBrowseA = new Button { Text = "📁 Browse...", Location = new Point(472, 8), Size = new Size(80, 25) };

            var lblB = new Label { Text = "🎯 CSF Externo:", Location = new Point(12, 42), AutoSize = true, Font = new Font(FontFamily.GenericSansSerif, 8.5f, FontStyle.Bold) };
            cboFileB = new ComboBox { Location = new Point(165, 39), Size = new Size(300, 23), DropDownStyle = ComboBoxStyle.DropDownList };
            btnBrowseB = new Button { Text = "📁 Browse...", Location = new Point(472, 38), Size = new Size(80, 25) };

            var btnCompare = new Button
            {
                Text = "⚡ Compare Now",
                Location = new Point(565, 8),
                Size = new Size(120, 55),
                Font = new Font(FontFamily.GenericSansSerif, 9f, FontStyle.Bold),
                BackColor = Color.FromArgb(37, 99, 235),
                ForeColor = Color.White
            };

            ToolTipHelper.SetToolTip(cboFileA, "Select the Reference CSF document from the active session");
            ToolTipHelper.SetToolTip(btnBrowseA, "Load an external CSF file as the Reference document");
            ToolTipHelper.SetToolTip(cboFileB, "Select the target CSF document to compare from session or external file");
            ToolTipHelper.SetToolTip(btnBrowseB, "Load an external CSF file to compare");
            ToolTipHelper.SetToolTip(btnCompare, "Execute detailed comparison between both CSF files");

            pnlTop.Controls.Add(lblA);
            pnlTop.Controls.Add(cboFileA);
            pnlTop.Controls.Add(btnBrowseA);
            pnlTop.Controls.Add(lblB);
            pnlTop.Controls.Add(cboFileB);
            pnlTop.Controls.Add(btnBrowseB);
            pnlTop.Controls.Add(btnCompare);

            // Toolbar Panel
            var pnlToolbar = new Panel { Dock = DockStyle.Top, Height = 45, BackColor = Color.FromArgb(249, 250, 251), Padding = new Padding(12, 6, 12, 6) };

            btnPrevDiff = new Button { Text = "⬆️ Prev (F7)", Location = new Point(12, 8), Size = new Size(90, 28), Font = new Font(FontFamily.GenericSansSerif, 8f, FontStyle.Bold) };
            ToolTipHelper.SetToolTip(btnPrevDiff, "Jump to previous difference (Shortcut: F7)");

            btnNextDiff = new Button { Text = "⬇️ Next (F8)", Location = new Point(108, 8), Size = new Size(90, 28), Font = new Font(FontFamily.GenericSansSerif, 8f, FontStyle.Bold) };
            ToolTipHelper.SetToolTip(btnNextDiff, "Jump to next difference (Shortcut: F8)");

            lblDiffCounter = new Label { Text = "Diff: 0 of 0", Location = new Point(205, 14), AutoSize = true, Font = new Font(FontFamily.GenericSansSerif, 8.5f, FontStyle.Bold), ForeColor = Color.FromArgb(55, 65, 81) };

            int filterX = 295;
            btnFilterAll = new Button { Text = "All (0)", Location = new Point(filterX, 8), Size = new Size(65, 28), BackColor = Color.FromArgb(229, 231, 235) };
            ToolTipHelper.SetToolTip(btnFilterAll, "Show all keys from both CSF files");

            btnFilterModified = new Button { Text = "Modified (0)", Location = new Point(filterX + 70, 8), Size = new Size(95, 28) };
            ToolTipHelper.SetToolTip(btnFilterModified, "Show only modified keys with differing text");

            btnFilterAdded = new Button { Text = "Added (0)", Location = new Point(filterX + 170, 8), Size = new Size(85, 28) };
            ToolTipHelper.SetToolTip(btnFilterAdded, "Show only new keys present in external file");

            btnFilterRemoved = new Button { Text = "Removed (0)", Location = new Point(filterX + 260, 8), Size = new Size(95, 28) };
            ToolTipHelper.SetToolTip(btnFilterRemoved, "Show only keys missing in external file");

            // Right-aligned Actions Panel
            var pnlActions = new Panel
            {
                Location = new Point(610, 4),
                Size = new Size(420, 36),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                BackColor = Color.Transparent
            };

            btnCopyBToA = new Button { Text = "← External to Ref", Location = new Point(0, 4), Size = new Size(110, 28), Font = new Font(FontFamily.GenericSansSerif, 8f, FontStyle.Bold) };
            ToolTipHelper.SetToolTip(btnCopyBToA, "Copy selected values from External CSF to Reference CSF");

            btnCopyAToB = new Button { Text = "→ Ref to External", Location = new Point(115, 4), Size = new Size(110, 28), Font = new Font(FontFamily.GenericSansSerif, 8f, FontStyle.Bold) };
            ToolTipHelper.SetToolTip(btnCopyAToB, "Copy selected values from Reference CSF to External CSF");

            btnSaveA = new Button { Text = "💾 Save Ref", Location = new Point(230, 4), Size = new Size(90, 28), Font = new Font(FontFamily.GenericSansSerif, 8f, FontStyle.Bold), BackColor = Color.FromArgb(220, 252, 231) };
            ToolTipHelper.SetToolTip(btnSaveA, "Save all changes made to Reference CSF");

            btnSaveB = new Button { Text = "💾 Save Ext", Location = new Point(325, 4), Size = new Size(90, 28), Font = new Font(FontFamily.GenericSansSerif, 8f, FontStyle.Bold), BackColor = Color.FromArgb(220, 252, 231) };
            ToolTipHelper.SetToolTip(btnSaveB, "Save all changes made to External CSF");

            pnlActions.Controls.Add(btnCopyBToA);
            pnlActions.Controls.Add(btnCopyAToB);
            pnlActions.Controls.Add(btnSaveA);
            pnlActions.Controls.Add(btnSaveB);

            pnlToolbar.Controls.Add(btnPrevDiff);
            pnlToolbar.Controls.Add(btnNextDiff);
            pnlToolbar.Controls.Add(lblDiffCounter);
            pnlToolbar.Controls.Add(btnFilterAll);
            pnlToolbar.Controls.Add(btnFilterModified);
            pnlToolbar.Controls.Add(btnFilterAdded);
            pnlToolbar.Controls.Add(btnFilterRemoved);
            pnlToolbar.Controls.Add(pnlActions);

            // DataGridView
            gridDiff = new DataGridView
            {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                ReadOnly = true,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = true,
                AutoGenerateColumns = false,
                RowHeadersVisible = false,
                GridColor = Color.FromArgb(229, 231, 235),
                BackgroundColor = Color.White
            };

            gridDiff.Columns.Add(new DataGridViewTextBoxColumn { Name = "colStatus", HeaderText = "ST", Width = 40, DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter, Font = new Font(FontFamily.GenericSansSerif, 10f, FontStyle.Bold) } });
            gridDiff.Columns.Add(new DataGridViewTextBoxColumn { Name = "colKey", HeaderText = "Key Name", Width = 220, DefaultCellStyle = new DataGridViewCellStyle { Font = new Font(FontFamily.GenericSansSerif, 8.5f, FontStyle.Bold) } });
            gridDiff.Columns.Add(new DataGridViewTextBoxColumn { Name = "colValA", HeaderText = "Texto CSF Referencia", Width = 380 });
            gridDiff.Columns.Add(new DataGridViewTextBoxColumn { Name = "colValB", HeaderText = "Texto CSF Externo", Width = 380 });

            gridDiff.CellFormatting += GridDiff_CellFormatting;
            gridDiff.SelectionChanged += GridDiff_SelectionChanged;
            gridDiff.CellContextMenuStripNeeded += GridDiff_CellContextMenuStripNeeded;

            // Status Strip
            statusStrip = new StatusStrip();
            lblStatusInfo = new ToolStripStatusLabel { Text = "Select two CSF documents and click 'Compare Now'." };
            statusStrip.Items.Add(lblStatusInfo);

            this.Controls.Add(gridDiff);
            this.Controls.Add(pnlToolbar);
            this.Controls.Add(pnlTop);
            this.Controls.Add(statusStrip);

            // Event Subscriptions
            btnBrowseA.Click += (s, e) => BrowseAndLoadFile(true);
            btnBrowseB.Click += (s, e) => BrowseAndLoadFile(false);
            btnCompare.Click += (s, e) => RunComparison();
            btnPrevDiff.Click += (s, e) => JumpToPrevDiff();
            btnNextDiff.Click += (s, e) => JumpToNextDiff();

            btnFilterAll.Click += (s, e) => SetFilter(null);
            btnFilterModified.Click += (s, e) => SetFilter(CsfDiffStatus.Modified);
            btnFilterAdded.Click += (s, e) => SetFilter(CsfDiffStatus.Added);
            btnFilterRemoved.Click += (s, e) => SetFilter(CsfDiffStatus.Removed);

            btnCopyBToA.Click += (s, e) => CopySelectedRows(fromBtoA: true);
            btnCopyAToB.Click += (s, e) => CopySelectedRows(fromBtoA: false);
            btnSaveA.Click += (s, e) => SaveFile(true);
            btnSaveB.Click += (s, e) => SaveFile(false);
        }

        private void PopulateFileDropdowns()
        {
            cboFileA.Items.Clear();
            cboFileB.Items.Clear();

            if (_session != null && _session.Documents.Count > 0)
            {
                foreach (var sDoc in _session.Documents)
                {
                    if (sDoc.Document != null)
                    {
                        var comboItem = new ComboDocItem { Doc = sDoc.Document, Path = sDoc.FilePath, Title = sDoc.ToString() };
                        cboFileA.Items.Add(comboItem);
                        cboFileB.Items.Add(comboItem);
                    }
                }

                if (cboFileA.Items.Count > 0) cboFileA.SelectedIndex = 0;
                if (cboFileB.Items.Count > 1) cboFileB.SelectedIndex = 1;
                else if (cboFileB.Items.Count > 0) cboFileB.SelectedIndex = 0;
            }
        }

        private void SelectDocumentInCombo(ComboBox cbo, CsfDocument targetDoc)
        {
            if (targetDoc == null) return;
            for (int i = 0; i < cbo.Items.Count; i++)
            {
                if (cbo.Items[i] is ComboDocItem item && item.Doc == targetDoc)
                {
                    cbo.SelectedIndex = i;
                    return;
                }
            }
        }

        private ComboDocItem GetSelectedComboItem(ComboBox cbo)
        {
            return cbo.SelectedItem as ComboDocItem;
        }

        private void BrowseAndLoadFile(bool isFileA)
        {
            using (var dlg = new OpenFileDialog())
            {
                dlg.Filter = "CSF Files (*.csf)|*.csf|All Files (*.*)|*.*";
                dlg.Title = isFileA ? "Select Base CSF File (File A)" : "Select Target CSF File (File B)";
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    try
                    {
                        var doc = CsfFileHandler.Load(dlg.FileName);
                        var comboItem = new ComboDocItem { Doc = doc, Path = dlg.FileName, Title = $"[External] {Path.GetFileName(dlg.FileName)}" };

                        ComboBox cbo = isFileA ? cboFileA : cboFileB;
                        cbo.Items.Add(comboItem);
                        cbo.SelectedItem = comboItem;
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Failed to load CSF file:\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        private void RunComparison()
        {
            var itemA = GetSelectedComboItem(cboFileA);
            var itemB = GetSelectedComboItem(cboFileB);

            _docA = itemA?.Doc;
            _filePathA = itemA?.Path;

            _docB = itemB?.Doc;
            _filePathB = itemB?.Path;

            if (_docA == null || _docB == null)
            {
                MessageBox.Show("Please select two valid CSF documents to compare.", "Comparison Required", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            _diffResult = CsfDiffEngine.Compare(_docA, _docB);
            UpdateFilterButtons();
            ApplyFilterAndRefreshGrid();

            lblStatusInfo.Text = $"Comparison complete: {_diffResult.TotalCount} total keys, {_diffResult.ModifiedCount} modified, {_diffResult.AddedCount} added, {_diffResult.RemovedCount} removed.";
        }

        private void UpdateFilterButtons()
        {
            if (_diffResult == null) return;
            btnFilterAll.Text = $"All ({_diffResult.TotalCount})";
            btnFilterModified.Text = $"Modified ({_diffResult.ModifiedCount})";
            btnFilterAdded.Text = $"Added ({_diffResult.AddedCount})";
            btnFilterRemoved.Text = $"Removed ({_diffResult.RemovedCount})";
        }

        private void SetFilter(CsfDiffStatus? status)
        {
            _activeStatusFilter = status;

            btnFilterAll.BackColor = SystemColors.Control;
            btnFilterModified.BackColor = SystemColors.Control;
            btnFilterAdded.BackColor = SystemColors.Control;
            btnFilterRemoved.BackColor = SystemColors.Control;

            if (status == null) btnFilterAll.BackColor = Color.FromArgb(229, 231, 235);
            else if (status == CsfDiffStatus.Modified) btnFilterModified.BackColor = Color.FromArgb(254, 240, 138);
            else if (status == CsfDiffStatus.Added) btnFilterAdded.BackColor = Color.FromArgb(187, 247, 208);
            else if (status == CsfDiffStatus.Removed) btnFilterRemoved.BackColor = Color.FromArgb(254, 202, 202);

            ApplyFilterAndRefreshGrid();
        }

        private void ApplyFilterAndRefreshGrid()
        {
            if (_diffResult == null) return;

            if (_activeStatusFilter.HasValue)
            {
                _filteredItems = _diffResult.Items.Where(i => i.Status == _activeStatusFilter.Value).ToList();
            }
            else
            {
                _filteredItems = _diffResult.Items.ToList();
            }

            gridDiff.Rows.Clear();
            foreach (var item in _filteredItems)
            {
                string statusSymbol = "";
                switch (item.Status)
                {
                    case CsfDiffStatus.Added: statusSymbol = "+"; break;
                    case CsfDiffStatus.Modified: statusSymbol = "~"; break;
                    case CsfDiffStatus.Removed: statusSymbol = "-"; break;
                    default: statusSymbol = " "; break;
                }

                int rowIdx = gridDiff.Rows.Add(statusSymbol, item.Key, item.ValueA ?? "<Missing>", item.ValueB ?? "<Missing>");
                gridDiff.Rows[rowIdx].Tag = item;
            }

            UpdateDiffCounter();
        }

        private void GridDiff_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0 || e.RowIndex >= gridDiff.Rows.Count) return;
            var item = gridDiff.Rows[e.RowIndex].Tag as CsfDiffItem;
            if (item == null) return;

            switch (item.Status)
            {
                case CsfDiffStatus.Added:
                    e.CellStyle.BackColor = Color.FromArgb(236, 253, 245);
                    break;
                case CsfDiffStatus.Modified:
                    e.CellStyle.BackColor = Color.FromArgb(254, 252, 232);
                    break;
                case CsfDiffStatus.Removed:
                    e.CellStyle.BackColor = Color.FromArgb(254, 242, 242);
                    break;
                default:
                    e.CellStyle.BackColor = Color.White;
                    break;
            }
        }

        private void GridDiff_SelectionChanged(object sender, EventArgs e)
        {
            UpdateDiffCounter();
        }

        private void UpdateDiffCounter()
        {
            if (gridDiff.CurrentRow == null || _diffResult == null)
            {
                lblDiffCounter.Text = $"Diff: 0 of {_diffResult?.TotalDifferences ?? 0}";
                return;
            }

            int diffIndex = 0;
            int totalDiffs = _diffResult.TotalDifferences;

            int currentGridIdx = gridDiff.CurrentRow.Index;
            for (int i = 0; i <= currentGridIdx && i < gridDiff.Rows.Count; i++)
            {
                if (gridDiff.Rows[i].Tag is CsfDiffItem item && item.Status != CsfDiffStatus.Unchanged)
                {
                    diffIndex++;
                }
            }

            lblDiffCounter.Text = $"Diff: {diffIndex} of {totalDiffs}";
        }

        private void JumpToNextDiff()
        {
            if (gridDiff.Rows.Count == 0) return;
            int startIdx = gridDiff.CurrentRow != null ? gridDiff.CurrentRow.Index + 1 : 0;

            for (int i = startIdx; i < gridDiff.Rows.Count; i++)
            {
                if (gridDiff.Rows[i].Tag is CsfDiffItem item && item.Status != CsfDiffStatus.Unchanged)
                {
                    gridDiff.CurrentCell = gridDiff.Rows[i].Cells[1];
                    gridDiff.Rows[i].Selected = true;
                    return;
                }
            }

            // Loop back to start
            for (int i = 0; i < startIdx; i++)
            {
                if (gridDiff.Rows[i].Tag is CsfDiffItem item && item.Status != CsfDiffStatus.Unchanged)
                {
                    gridDiff.CurrentCell = gridDiff.Rows[i].Cells[1];
                    gridDiff.Rows[i].Selected = true;
                    return;
                }
            }
        }

        private void JumpToPrevDiff()
        {
            if (gridDiff.Rows.Count == 0) return;
            int startIdx = gridDiff.CurrentRow != null ? gridDiff.CurrentRow.Index - 1 : gridDiff.Rows.Count - 1;

            for (int i = startIdx; i >= 0; i--)
            {
                if (gridDiff.Rows[i].Tag is CsfDiffItem item && item.Status != CsfDiffStatus.Unchanged)
                {
                    gridDiff.CurrentCell = gridDiff.Rows[i].Cells[1];
                    gridDiff.Rows[i].Selected = true;
                    return;
                }
            }

            // Loop back to end
            for (int i = gridDiff.Rows.Count - 1; i > startIdx; i--)
            {
                if (gridDiff.Rows[i].Tag is CsfDiffItem item && item.Status != CsfDiffStatus.Unchanged)
                {
                    gridDiff.CurrentCell = gridDiff.Rows[i].Cells[1];
                    gridDiff.Rows[i].Selected = true;
                    return;
                }
            }
        }

        private void CopySelectedRows(bool fromBtoA)
        {
            if (_docA == null || _docB == null || gridDiff.SelectedRows.Count == 0) return;

            foreach (DataGridViewRow row in gridDiff.SelectedRows)
            {
                if (row.Tag is CsfDiffItem item)
                {
                    if (fromBtoA)
                    {
                        if (item.ExistsInB)
                        {
                            _docA.SetString(item.Key, item.ValueB ?? "", item.ExtraValueB);
                            item.ValueA = item.ValueB;
                            item.ExtraValueA = item.ExtraValueB;
                            item.ExistsInA = true;
                            item.Status = (item.ValueA == item.ValueB && item.ExtraValueA == item.ExtraValueB) ? CsfDiffStatus.Unchanged : CsfDiffStatus.Modified;
                        }
                    }
                    else
                    {
                        if (item.ExistsInA)
                        {
                            _docB.SetString(item.Key, item.ValueA ?? "", item.ExtraValueA);
                            item.ValueB = item.ValueA;
                            item.ExtraValueB = item.ExtraValueA;
                            item.ExistsInB = true;
                            item.Status = (item.ValueA == item.ValueB && item.ExtraValueA == item.ExtraValueB) ? CsfDiffStatus.Unchanged : CsfDiffStatus.Modified;
                        }
                    }

                    row.Cells["colValA"].Value = item.ValueA ?? "<Missing>";
                    row.Cells["colValB"].Value = item.ValueB ?? "<Missing>";
                    row.Cells["colStatus"].Value = item.Status == CsfDiffStatus.Unchanged ? " " : (item.Status == CsfDiffStatus.Added ? "+" : (item.Status == CsfDiffStatus.Modified ? "~" : "-"));
                }
            }

            UpdateFilterButtons();
            gridDiff.Invalidate();
            lblStatusInfo.Text = fromBtoA ? "Merged selected entries from Target CSF → Base CSF." : "Merged selected entries from Base CSF → Target CSF.";
        }

        private void GridDiff_CellContextMenuStripNeeded(object sender, DataGridViewCellContextMenuStripNeededEventArgs e)
        {
            if (e.RowIndex < 0) return;
            gridDiff.Rows[e.RowIndex].Selected = true;

            var menu = new ContextMenuStrip();
            menu.Items.Add("⬅️ Copy Target Value → Base CSF", null, (s, ev) => CopySelectedRows(fromBtoA: true));
            menu.Items.Add("➡️ Copy Base Value → Target CSF", null, (s, ev) => CopySelectedRows(fromBtoA: false));

            e.ContextMenuStrip = menu;
        }

        private void SaveFile(bool saveA)
        {
            CsfDocument doc = saveA ? _docA : _docB;
            string targetPath = saveA ? _filePathA : _filePathB;

            if (doc == null) return;

            if (string.IsNullOrWhiteSpace(targetPath))
            {
                using (var dlg = new SaveFileDialog())
                {
                    dlg.Filter = "CSF Files (*.csf)|*.csf";
                    if (dlg.ShowDialog(this) == DialogResult.OK)
                    {
                        targetPath = dlg.FileName;
                        if (saveA) _filePathA = targetPath;
                        else _filePathB = targetPath;
                    }
                    else
                    {
                        return;
                    }
                }
            }

            try
            {
                CsfFileHandler.Save(doc, targetPath);
                MessageBox.Show($"File successfully saved to:\n{targetPath}", "File Saved", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save CSF file:\n{ex.Message}", "Save Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.F7)
            {
                JumpToPrevDiff();
                return true;
            }
            if (keyData == Keys.F8)
            {
                JumpToNextDiff();
                return true;
            }
            if (keyData == (Keys.Control | Keys.S))
            {
                SaveFile(true);
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        private class ComboDocItem
        {
            public CsfDocument Doc { get; set; }
            public string Path { get; set; }
            public string Title { get; set; }
            public override string ToString() => Title;
        }
    }
}
