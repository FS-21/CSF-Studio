using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using CsfStudio.Core;

namespace CsfStudio.UI
{
    public class BatchRenameDialog : Form
    {
        private ComboBox cboFindPattern;
        private ComboBox cboReplacePattern;
        private CheckBox chkUseRegex;
        private Button btnPreview;
        private DataGridView gridPreview;
        private Button btnApply;
        private Button btnCancel;

        private List<string> _keysToRename;
        private AppConfig _config;

        public Dictionary<string, string> RenameMapping { get; private set; } = new Dictionary<string, string>();

        public BatchRenameDialog(List<string> keysToRename, AppConfig config = null)
        {
            _keysToRename = keysToRename ?? new List<string>();
            _config = config ?? ConfigManager.LoadConfig();
            InitializeComponent();
            SetupToolTips();
            LoadHistoryToCombos();
            GeneratePreview();
        }

        private void InitializeComponent()
        {
            this.Text = "Batch Key Rename with Live Preview";
            this.Size = new Size(720, 500);
            this.StartPosition = FormStartPosition.CenterParent;
            this.ShowIcon = false;

            var panelTop = new Panel { Dock = DockStyle.Top, Height = 95, Padding = new Padding(10) };

            var lblFind = new Label { Text = "Find in Name:", Location = new Point(10, 15), AutoSize = true, Font = new Font(FontFamily.GenericSansSerif, 8.5f, FontStyle.Bold) };
            cboFindPattern = new ComboBox
            {
                Location = new Point(120, 12),
                Width = 220,
                DropDownStyle = ComboBoxStyle.DropDown,
                AutoCompleteMode = AutoCompleteMode.SuggestAppend,
                AutoCompleteSource = AutoCompleteSource.ListItems
            };

            var lblReplace = new Label { Text = "Replace with:", Location = new Point(360, 15), AutoSize = true, Font = new Font(FontFamily.GenericSansSerif, 8.5f, FontStyle.Bold) };
            cboReplacePattern = new ComboBox
            {
                Location = new Point(460, 12),
                Width = 220,
                DropDownStyle = ComboBoxStyle.DropDown,
                AutoCompleteMode = AutoCompleteMode.SuggestAppend,
                AutoCompleteSource = AutoCompleteSource.ListItems
            };

            chkUseRegex = new CheckBox { Text = "Use Regular Expressions (RegEx)", Location = new Point(120, 48), AutoSize = true };
            chkUseRegex.CheckedChanged += (s, e) => LoadHistoryToCombos();

            btnPreview = new Button { Text = "🔍 Live Preview", Location = new Point(460, 45), Size = new Size(220, 28), Font = new Font(FontFamily.GenericSansSerif, 8.5f, FontStyle.Bold) };
            btnPreview.Click += (s, e) =>
            {
                SaveCurrentToHistory();
                GeneratePreview();
            };

            panelTop.Controls.Add(lblFind);
            panelTop.Controls.Add(cboFindPattern);
            panelTop.Controls.Add(lblReplace);
            panelTop.Controls.Add(cboReplacePattern);
            panelTop.Controls.Add(chkUseRegex);
            panelTop.Controls.Add(btnPreview);

            gridPreview = new DataGridView
            {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                RowHeadersVisible = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
            };

            var colOrig = new DataGridViewTextBoxColumn { HeaderText = "Original Key Name", ReadOnly = true };
            var colNew = new DataGridViewTextBoxColumn { HeaderText = "New Key Name", ReadOnly = true };
            var colStatus = new DataGridViewTextBoxColumn { HeaderText = "Status", ReadOnly = true, Width = 120 };

            gridPreview.Columns.AddRange(colOrig, colNew, colStatus);

            var panelBottom = new Panel { Dock = DockStyle.Bottom, Height = 50, Padding = new Padding(10) };
            btnApply = new Button { Text = "Apply Rename", DialogResult = DialogResult.OK, Location = new Point(450, 10), Size = new Size(150, 30), Font = new Font(FontFamily.GenericSansSerif, 8.5f, FontStyle.Bold) };
            btnCancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Location = new Point(610, 10), Size = new Size(90, 30) };

            btnApply.Click += (s, e) => SaveCurrentToHistory();

            panelBottom.Controls.Add(btnApply);
            panelBottom.Controls.Add(btnCancel);

            this.Controls.Add(gridPreview);
            this.Controls.Add(panelTop);
            this.Controls.Add(panelBottom);
        }

        private void SetupToolTips()
        {
            ToolTipHelper.SetToolTip(cboFindPattern, "Find in Name: Search string or RegEx pattern to match key names. Drop down for history.");
            ToolTipHelper.SetToolTip(cboReplacePattern, "Replace with: Replacement text for matched keys. Supports RegEx capture groups ($1, $2).");
            ToolTipHelper.SetToolTip(chkUseRegex, "Use RegEx: Enable Regular Expression pattern matching.");
            ToolTipHelper.SetToolTip(btnPreview, "Live Preview: Refresh the table preview of renamed key names.");
            ToolTipHelper.SetToolTip(gridPreview, "Preview Grid: Shows original vs renamed key names.");
            ToolTipHelper.SetToolTip(btnApply, "Apply Rename: Update key names across open CSF files.");
            ToolTipHelper.SetToolTip(btnCancel, "Cancel: Close window without changing key names.");
        }

        private void LoadHistoryToCombos()
        {
            string currentFind = cboFindPattern.Text;
            string currentReplace = cboReplacePattern.Text;

            var findList = chkUseRegex.Checked ? _config.BatchFindHistoryRegex : _config.BatchFindHistoryPlain;
            var replaceList = chkUseRegex.Checked ? _config.BatchReplaceHistoryRegex : _config.BatchReplaceHistoryPlain;

            PopulateComboItems(cboFindPattern, findList, currentFind);
            PopulateComboItems(cboReplacePattern, replaceList, currentReplace);
        }

        private void PopulateComboItems(ComboBox combo, List<string> history, string currentText)
        {
            combo.BeginUpdate();
            combo.Items.Clear();
            if (history != null)
            {
                foreach (var item in history)
                {
                    if (!string.IsNullOrWhiteSpace(item))
                    {
                        combo.Items.Add(item);
                    }
                }
            }
            combo.Text = currentText ?? string.Empty;
            combo.EndUpdate();
        }

        private void SaveCurrentToHistory()
        {
            if (_config == null) return;

            string findText = cboFindPattern.Text;
            string replaceText = cboReplacePattern.Text;

            if (!string.IsNullOrWhiteSpace(findText))
            {
                var findList = chkUseRegex.Checked ? _config.BatchFindHistoryRegex : _config.BatchFindHistoryPlain;
                ConfigManager.AddHistoryItem(findList, findText, _config.MaxSearchHistoryItems);
            }

            if (!string.IsNullOrWhiteSpace(replaceText))
            {
                var replaceList = chkUseRegex.Checked ? _config.BatchReplaceHistoryRegex : _config.BatchReplaceHistoryPlain;
                ConfigManager.AddHistoryItem(replaceList, replaceText, _config.MaxSearchHistoryItems);
            }

            ConfigManager.SaveConfig(_config);
            LoadHistoryToCombos();
        }

        private void GeneratePreview()
        {
            gridPreview.Rows.Clear();
            RenameMapping.Clear();

            string find = cboFindPattern.Text;
            string replace = cboReplacePattern.Text;

            Regex regex = null;
            if (chkUseRegex.Checked && !string.IsNullOrEmpty(find))
            {
                try
                {
                    regex = new Regex(find, RegexOptions.IgnoreCase);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"RegEx syntax error:\n{ex.Message}", "RegEx Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
            }

            int changedCount = 0;

            foreach (var orig in _keysToRename)
            {
                string newName = orig;

                if (!string.IsNullOrEmpty(find))
                {
                    if (regex != null)
                    {
                        newName = regex.Replace(orig, replace);
                    }
                    else
                    {
                        newName = orig.Replace(find, replace);
                    }
                }

                string status;
                Color bg;

                if (newName != orig)
                {
                    status = "✏️ Modified";
                    bg = Color.FromArgb(235, 247, 235);
                    RenameMapping[orig] = newName;
                    changedCount++;
                }
                else
                {
                    status = "Unchanged";
                    bg = Color.White;
                }

                int idx = gridPreview.Rows.Add(orig, newName, status);
                gridPreview.Rows[idx].DefaultCellStyle.BackColor = bg;
            }

            btnApply.Enabled = (changedCount > 0);
        }
    }
}
