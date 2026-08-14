using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using CsfStudio.Core;

namespace CsfStudio.UI
{
    public class BackupInspectorDialog : Form
    {
        private ComboBox cboBackupFiles;
        private DataGridView gridDiff;
        private Button btnRestoreSelected;
        private Button btnRestoreAll;
        private Button btnClose;
        private Label lblBackupInfo;

        private CsfDocument _currentDoc;
        private string _currentFilePath;
        private CsfDocument _selectedBackupDoc;

        public BackupInspectorDialog(CsfDocument currentDoc, string currentFilePath)
        {
            _currentDoc = currentDoc;
            _currentFilePath = currentFilePath;
            InitializeComponent();
            LoadBackupFileList();
        }

        private void InitializeComponent()
        {
            this.Text = LanguageManager.GetString("BackupInspector.Title", "Timestamped Backup (.bak) History & Inspector");
            this.Size = new Size(880, 500);
            this.StartPosition = FormStartPosition.CenterParent;
            this.ShowIcon = false;

            var panelTop = new Panel { Dock = DockStyle.Top, Height = 50, Padding = new Padding(10) };
            var lblSelect = new Label { Text = LanguageManager.GetString("BackupInspector.SelectBackup", "Select Backup File:"), Location = new Point(10, 15), AutoSize = true };
            cboBackupFiles = new ComboBox { Location = new Point(130, 12), Width = 410, DropDownStyle = ComboBoxStyle.DropDownList };
            cboBackupFiles.SelectedIndexChanged += CboBackupFiles_SelectedIndexChanged;

            lblBackupInfo = new Label { Location = new Point(560, 15), AutoSize = true, Font = new Font(FontFamily.GenericSansSerif, 8.5f, FontStyle.Bold) };

            panelTop.Controls.Add(lblSelect);
            panelTop.Controls.Add(cboBackupFiles);
            panelTop.Controls.Add(lblBackupInfo);

            gridDiff = new DataGridView
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

            var colCheck = new DataGridViewCheckBoxColumn { HeaderText = LanguageManager.GetString("BackupInspector.ColRestore", "Restore"), Width = 70 };
            var colKey = new DataGridViewTextBoxColumn { HeaderText = LanguageManager.GetString("Grid.Column.Key", "Key Name"), ReadOnly = true, Width = 180 };
            var colStatus = new DataGridViewTextBoxColumn { HeaderText = LanguageManager.GetString("Grid.Column.Status", "Status"), ReadOnly = true, Width = 120 };
            var colCurrent = new DataGridViewTextBoxColumn { HeaderText = LanguageManager.GetString("Backups.ColCurrentText", "Current Text"), ReadOnly = true };
            var colBackup = new DataGridViewTextBoxColumn { HeaderText = LanguageManager.GetString("Backups.ColOldText", "Old Text"), ReadOnly = true };

            gridDiff.Columns.AddRange(colCheck, colKey, colStatus, colCurrent, colBackup);

            var panelBottom = new Panel { Dock = DockStyle.Bottom, Height = 50, Padding = new Padding(10) };
            btnRestoreSelected = new Button { Text = LanguageManager.GetString("BackupInspector.BtnRestoreSelected", "Restore Selected Keys"), Location = new Point(440, 10), Size = new Size(200, 30) };
            btnRestoreAll = new Button { Text = LanguageManager.GetString("BackupInspector.BtnRestoreAll", "Restore Full File"), Location = new Point(650, 10), Size = new Size(150, 30) };
            btnClose = new Button { Text = LanguageManager.GetString("Button.Close", "Close"), DialogResult = DialogResult.Cancel, Location = new Point(810, 10), Size = new Size(50, 30) };

            btnRestoreSelected.Click += BtnRestoreSelected_Click;
            btnRestoreAll.Click += BtnRestoreAll_Click;

            panelBottom.Controls.Add(btnRestoreSelected);
            panelBottom.Controls.Add(btnRestoreAll);

            this.Controls.Add(gridDiff);
            this.Controls.Add(panelTop);
            this.Controls.Add(panelBottom);
        }

        private void LoadBackupFileList()
        {
            cboBackupFiles.Items.Clear();
            if (string.IsNullOrEmpty(_currentFilePath)) return;

            string dir = Path.GetDirectoryName(_currentFilePath);
            string fname = Path.GetFileName(_currentFilePath);

            if (Directory.Exists(dir))
            {
                var files = Directory.GetFiles(dir, $"{fname}.*.bak")
                    .OrderByDescending(f => File.GetLastWriteTime(f))
                    .ToList();

                var cfg = ConfigManager.LoadConfig();
                string backupsFolder = BackupManager.GetBackupDirectory(_currentFilePath, cfg.BackupDirectoryPath);
                if (!string.IsNullOrEmpty(backupsFolder) && Directory.Exists(backupsFolder))
                {
                    files.AddRange(Directory.GetFiles(backupsFolder, "*", SearchOption.AllDirectories)
                        .Where(f => f.EndsWith(".bak", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".csf", StringComparison.OrdinalIgnoreCase))
                        .OrderByDescending(f => File.GetLastWriteTime(f)));
                }

                foreach (var file in files)
                {
                    cboBackupFiles.Items.Add(file);
                }
            }

            if (cboBackupFiles.Items.Count > 0)
            {
                cboBackupFiles.SelectedIndex = 0;
            }
            else
            {
                lblBackupInfo.Text = LanguageManager.GetString("BackupInspector.NoBackupsFound", "No backups found for this file.");
            }
        }

        private void CboBackupFiles_SelectedIndexChanged(object sender, EventArgs e)
        {
            string path = cboBackupFiles.SelectedItem as string;
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return;

            try
            {
                _selectedBackupDoc = CsfFileHandler.Load(path);
                lblBackupInfo.Text = string.Format(LanguageManager.GetString("BackupInspector.BackupDate", "Backup Date: {0}"), File.GetLastWriteTime(path).ToString("yyyy-MM-dd HH:mm:ss"));
                PopulateGrid();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    string.Format(LanguageManager.GetString("Msg.ErrorReadingBackup", "Error reading backup file:\n{0}"), ex.Message),
                    LanguageManager.GetString("Msg.ErrorTitle", "Error"),
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private void PopulateGrid()
        {
            gridDiff.Rows.Clear();
            if (_selectedBackupDoc == null || _currentDoc == null) return;

            var currMap = _currentDoc.Labels.ToDictionary(l => l.Name, StringComparer.OrdinalIgnoreCase);
            var bakMap = _selectedBackupDoc.Labels.ToDictionary(l => l.Name, StringComparer.OrdinalIgnoreCase);

            var allKeys = new HashSet<string>(currMap.Keys, StringComparer.OrdinalIgnoreCase);
            allKeys.UnionWith(bakMap.Keys);

            string statusDeleted = LanguageManager.GetString("BackupInspector.StatusDeleted", "🔴 Deleted (in .bak)");
            string statusNew = LanguageManager.GetString("BackupInspector.StatusNew", "🆕 New (not in .bak)");
            string statusEdited = LanguageManager.GetString("BackupInspector.StatusEdited", "⚡ Value Edited");
            string missingStr = LanguageManager.GetString("Grid.Status.Missing", "Missing");

            foreach (var key in allKeys.OrderBy(k => k))
            {
                bool inCurr = currMap.TryGetValue(key, out var currLbl);
                bool inBak = bakMap.TryGetValue(key, out var bakLbl);

                string currVal = inCurr ? currLbl.FirstValue : null;
                string bakVal = inBak ? bakLbl.FirstValue : null;

                if (currVal == bakVal) continue;

                string status;
                if (!inCurr && inBak) status = statusDeleted;
                else if (inCurr && !inBak) status = statusNew;
                else status = statusEdited;

                int idx = gridDiff.Rows.Add(
                    true,
                    key,
                    status,
                    currVal ?? $"({missingStr})",
                    bakVal ?? $"({missingStr})"
                );

                gridDiff.Rows[idx].Tag = key;
            }
        }

        private void BtnRestoreSelected_Click(object sender, EventArgs e)
        {
            if (_selectedBackupDoc == null) return;

            int restored = 0;
            var currMap = _currentDoc.Labels.ToDictionary(l => l.Name, StringComparer.OrdinalIgnoreCase);
            var bakMap = _selectedBackupDoc.Labels.ToDictionary(l => l.Name, StringComparer.OrdinalIgnoreCase);

            foreach (DataGridViewRow row in gridDiff.Rows)
            {
                bool isChecked = Convert.ToBoolean(row.Cells[0].Value);
                if (isChecked && row.Tag is string keyName)
                {
                    if (bakMap.TryGetValue(keyName, out var bakLbl))
                    {
                        if (currMap.TryGetValue(keyName, out var currLbl))
                        {
                            currLbl.Strings.Clear();
                            foreach (var s in bakLbl.Strings) currLbl.Strings.Add(s.Clone());
                        }
                        else
                        {
                            _currentDoc.Labels.Add(bakLbl.Clone());
                        }
                        restored++;
                    }
                }
            }

            MessageBox.Show(
                string.Format(LanguageManager.GetString("Msg.RestoredKeysCount", "Restored {0} keys from backup."), restored),
                LanguageManager.GetString("Msg.RestorationCompleteTitle", "Restoration Completed"),
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            this.DialogResult = DialogResult.OK;
            Close();
        }

        private void BtnRestoreAll_Click(object sender, EventArgs e)
        {
            if (_selectedBackupDoc == null) return;

            if (MessageBox.Show(
                LanguageManager.GetString("Msg.ConfirmReplaceFullFile", "Are you sure you want to replace the entire active file with this backup?"),
                LanguageManager.GetString("Msg.ConfirmRestoreTitle", "Confirm Full Restore"),
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning) == DialogResult.Yes)
            {
                _currentDoc.Labels.Clear();
                foreach (var lbl in _selectedBackupDoc.Labels)
                {
                    _currentDoc.Labels.Add(lbl.Clone());
                }
                _currentDoc.Version = _selectedBackupDoc.Version;
                _currentDoc.Language = _selectedBackupDoc.Language;

                MessageBox.Show(
                    LanguageManager.GetString("Msg.FileRestoredSuccess", "File restored completely."),
                    LanguageManager.GetString("Msg.SuccessTitle", "Success"),
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                this.DialogResult = DialogResult.OK;
                Close();
            }
        }
    }
}
