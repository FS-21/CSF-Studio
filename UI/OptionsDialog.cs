using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using CsfStudio.Core;

namespace CsfStudio.UI
{
    public class OptionsDialog : Form
    {
        private AppConfig _config;

        private TabControl tabControl;
        private TabPage tabGeneral;
        private TabPage tabBackups;
        private TabPage tabScanner;

        private CheckBox chkSaveInAppData;
        private Label lblLocationInfo;

        private Button btnAssociateCsf;
        private Label lblAssocStatus;

        private CheckBox chkAutoCreateBackups;
        private Label lblMaxSnapshots;
        private NumericUpDown numMaxSnapshots;
        private Label lblAutoDeleteDays;
        private NumericUpDown numAutoDeleteDays;

        private Label lblBackupDir;
        private TextBox txtBackupDir;
        private Button btnBrowseBackupDir;

        private Label lblToastSeconds;
        private NumericUpDown numToastSeconds;
        private Label lblMaxUndo;
        private NumericUpDown numMaxUndo;

        private Label lblCategoryPrefix;
        private TextBox txtCategoryPrefix;
        private Label lblDefaultLanguage;
        private ComboBox cmbDefaultLanguage;

        private class LanguageComboItem
        {
            public CsfLanguage Language { get; set; }
            public override string ToString() => $"{(int)Language} - {Language}";
        }
        private Label lblMaxHistory;
        private NumericUpDown numMaxHistory;

        private Label lblMaxMultiDisplay;
        private NumericUpDown numMaxMultiDisplay;

        private Label lblDefaultStartupTab;
        private ComboBox cmbDefaultStartupTab;
        private CheckBox chkRememberPanelLayoutPositions;
        private CheckBox chkInspectorMultilineTabs;

        private Label lblIniScanProps;
        private TextBox txtIniScanProps;
        private Button btnResetIniScanProps;

        private Button btnClearHistory;
        private Button btnOK;
        private Button btnCancel;

        public AppConfig Config => _config;

        public OptionsDialog(AppConfig currentConfig)
        {
            _config = currentConfig ?? new AppConfig();
            InitializeComponent();
            LoadConfigToUI();
        }

        private void InitializeComponent()
        {
            this.Text = "⚙️ Application Options";
            this.Size = new Size(540, 570);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.StartPosition = FormStartPosition.CenterParent;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.ShowIcon = false;

            // --- TAB CONTROL ---
            tabControl = new TabControl
            {
                Location = new Point(12, 12),
                Size = new Size(500, 470),
                Font = new Font("Segoe UI", 9F, FontStyle.Regular)
            };

            tabGeneral = new TabPage("⚙️ General & Defaults");
            tabBackups = new TabPage("🛡️ Backups & Security");
            tabScanner = new TabPage("🔍 INI Scanner");

            tabControl.TabPages.Add(tabGeneral);
            tabControl.TabPages.Add(tabBackups);
            tabControl.TabPages.Add(tabScanner);

            // ==========================================
            // TAB 1: GENERAL & DEFAULTS
            // ==========================================
            // 1. Storage Location Group
            var grpStorage = new GroupBox
            {
                Text = "💾 Configuration Storage Location",
                Location = new Point(12, 10),
                Size = new Size(470, 70),
                Font = new Font("Segoe UI", 9F, FontStyle.Bold)
            };

            chkSaveInAppData = new CheckBox
            {
                Text = "Save settings in AppData (%APPDATA%) instead of application folder",
                Location = new Point(15, 20),
                Size = new Size(440, 22),
                Font = new Font("Segoe UI", 9F, FontStyle.Regular)
            };
            chkSaveInAppData.CheckedChanged += (s, e) => UpdateLocationInfo();

            lblLocationInfo = new Label
            {
                Location = new Point(35, 43),
                Size = new Size(420, 20),
                ForeColor = Color.DimGray,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Italic)
            };

            grpStorage.Controls.Add(chkSaveInAppData);
            grpStorage.Controls.Add(lblLocationInfo);

            // 2. Windows File Association Group
            var grpAssoc = new GroupBox
            {
                Text = "🔗 Windows File Association (.CSF)",
                Location = new Point(12, 85),
                Size = new Size(470, 68),
                Font = new Font("Segoe UI", 9F, FontStyle.Bold)
            };

            btnAssociateCsf = new Button
            {
                Text = "🔗 Associate .CSF Files with CSF Studio",
                Location = new Point(15, 24),
                Size = new Size(260, 28),
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                UseVisualStyleBackColor = true
            };
            btnAssociateCsf.Click += BtnAssociateCsf_Click;

            lblAssocStatus = new Label
            {
                Location = new Point(285, 29),
                Size = new Size(175, 20),
                Font = new Font("Segoe UI", 9F, FontStyle.Bold)
            };

            grpAssoc.Controls.Add(btnAssociateCsf);
            grpAssoc.Controls.Add(lblAssocStatus);

            // 3. Default Document Settings Group
            var grpDefaults = new GroupBox
            {
                Text = "🌐 New Document Defaults",
                Location = new Point(12, 158),
                Size = new Size(470, 88),
                Font = new Font("Segoe UI", 9F, FontStyle.Bold)
            };

            lblDefaultLanguage = new Label
            {
                Text = "Default Language Header ID:",
                Location = new Point(15, 25),
                Size = new Size(165, 22),
                Font = new Font("Segoe UI", 9F, FontStyle.Regular)
            };

            cmbDefaultLanguage = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Location = new Point(180, 22),
                Size = new Size(270, 22),
                Font = new Font("Segoe UI", 9F, FontStyle.Regular)
            };

            foreach (CsfLanguage lang in Enum.GetValues(typeof(CsfLanguage)))
            {
                cmbDefaultLanguage.Items.Add(new LanguageComboItem { Language = lang });
            }

            string langTip = "Default Language Header ID: Sets the default 32-bit binary language ID assigned at offset 0x14 when creating new CSF string tables (e.g. 0 - EnglishUS, 4 - Spanish).";
            ToolTipHelper.SetToolTip(cmbDefaultLanguage, langTip);
            ToolTipHelper.SetToolTip(lblDefaultLanguage, langTip);

            lblCategoryPrefix = new Label
            {
                Text = "Default Key Prefix:",
                Location = new Point(15, 55),
                Size = new Size(165, 22),
                Font = new Font("Segoe UI", 9F, FontStyle.Regular)
            };

            txtCategoryPrefix = new TextBox
            {
                Location = new Point(180, 52),
                Size = new Size(100, 22),
                CharacterCasing = CharacterCasing.Upper,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold)
            };

            grpDefaults.Controls.Add(lblDefaultLanguage);
            grpDefaults.Controls.Add(cmbDefaultLanguage);
            grpDefaults.Controls.Add(lblCategoryPrefix);
            grpDefaults.Controls.Add(txtCategoryPrefix);

            // 4. User Experience & History Group
            var grpUX = new GroupBox
            {
                Text = "🖥️ Interface & Search History",
                Location = new Point(12, 252),
                Size = new Size(470, 130),
                Font = new Font("Segoe UI", 9F, FontStyle.Bold)
            };

            lblToastSeconds = new Label
            {
                Text = "Notification toast duration (s):",
                Location = new Point(15, 25),
                Size = new Size(175, 22),
                Font = new Font("Segoe UI", 9F, FontStyle.Regular)
            };

            numToastSeconds = new NumericUpDown
            {
                Location = new Point(195, 23),
                Size = new Size(55, 22),
                Minimum = 1,
                Maximum = 30,
                Value = 5,
                Font = new Font("Segoe UI", 9F, FontStyle.Regular)
            };

            lblMaxUndo = new Label
            {
                Text = "Max Undo levels (Ctrl+Z):",
                Location = new Point(265, 25),
                Size = new Size(140, 22),
                Font = new Font("Segoe UI", 9F, FontStyle.Regular)
            };

            numMaxUndo = new NumericUpDown
            {
                Location = new Point(405, 23),
                Size = new Size(55, 22),
                Minimum = 10,
                Maximum = 500,
                Value = 100,
                Font = new Font("Segoe UI", 9F, FontStyle.Regular)
            };

            lblMaxHistory = new Label
            {
                Text = "Max Search History items:",
                Location = new Point(15, 60),
                Size = new Size(175, 22),
                Font = new Font("Segoe UI", 9F, FontStyle.Regular)
            };

            numMaxHistory = new NumericUpDown
            {
                Location = new Point(195, 58),
                Size = new Size(55, 22),
                Minimum = 1,
                Maximum = 100,
                Value = 10,
                Font = new Font("Segoe UI", 9F, FontStyle.Regular)
            };

            btnClearHistory = new Button
            {
                Text = "🧹 Clear Search History",
                Location = new Point(265, 56),
                Size = new Size(195, 26),
                Font = new Font("Segoe UI", 8.5F, FontStyle.Regular),
                UseVisualStyleBackColor = true
            };
            btnClearHistory.Click += BtnClearHistory_Click;

            lblMaxMultiDisplay = new Label
            {
                Text = "Max Multi-Key Editors Displayed:",
                Location = new Point(15, 95),
                Size = new Size(185, 22),
                Font = new Font("Segoe UI", 9F, FontStyle.Regular)
            };

            numMaxMultiDisplay = new NumericUpDown
            {
                Location = new Point(205, 93),
                Size = new Size(55, 22),
                Minimum = 1,
                Maximum = 100,
                Value = 10,
                Font = new Font("Segoe UI", 9F, FontStyle.Regular)
            };
            ToolTipHelper.SetToolTip(numMaxMultiDisplay, "Sets the maximum number of key editors rendered simultaneously when selecting multiple keys.");

            lblDefaultStartupTab = new Label
            {
                Text = "Default Startup View Tab:",
                Location = new Point(15, 128),
                Size = new Size(185, 22),
                Font = new Font("Segoe UI", 9F, FontStyle.Regular)
            };

            cmbDefaultStartupTab = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Location = new Point(205, 126),
                Size = new Size(255, 22),
                Font = new Font("Segoe UI", 9F, FontStyle.Regular)
            };
            cmbDefaultStartupTab.Items.Add("📋 Master Keys View (Global Table)");
            cmbDefaultStartupTab.Items.Add("📋 Plain Keys View (Vertical List)");
            cmbDefaultStartupTab.Items.Add("🕒 Remember Last Active Tab");
            ToolTipHelper.SetToolTip(cmbDefaultStartupTab, "Configures which main view tab is automatically opened when launching the application.");

            chkRememberPanelLayoutPositions = new CheckBox
            {
                Text = "💾 Remember panel splitter positions and layout dimensions on exit",
                AutoSize = true,
                Location = new Point(15, 154),
                Font = new Font("Segoe UI", 9F, FontStyle.Regular)
            };
            ToolTipHelper.SetToolTip(chkRememberPanelLayoutPositions, "Remembers side panel widths and inspector heights across application restarts.");

            chkInspectorMultilineTabs = new CheckBox
            {
                Text = "📑 Show file tabs in multiple rows when space is constrained in inspector",
                AutoSize = true,
                Location = new Point(15, 180),
                Font = new Font("Segoe UI", 9F, FontStyle.Regular)
            };
            ToolTipHelper.SetToolTip(chkInspectorMultilineTabs, "When enabled, file tabs in the entry inspector wrap onto multiple rows if space is tight. When disabled, tabs stay on a single row with scroll buttons to preserve vertical editor height.");

            grpUX = new GroupBox
            {
                Text = "🖥️ Interface & Search History",
                Location = new Point(12, 230),
                Size = new Size(470, 210),
                Font = new Font("Segoe UI", 9F, FontStyle.Bold)
            };

            grpUX.Controls.Add(lblToastSeconds);
            grpUX.Controls.Add(numToastSeconds);
            grpUX.Controls.Add(lblMaxUndo);
            grpUX.Controls.Add(numMaxUndo);
            grpUX.Controls.Add(lblMaxHistory);
            grpUX.Controls.Add(numMaxHistory);
            grpUX.Controls.Add(btnClearHistory);
            grpUX.Controls.Add(lblMaxMultiDisplay);
            grpUX.Controls.Add(numMaxMultiDisplay);
            grpUX.Controls.Add(lblDefaultStartupTab);
            grpUX.Controls.Add(cmbDefaultStartupTab);
            grpUX.Controls.Add(chkRememberPanelLayoutPositions);
            grpUX.Controls.Add(chkInspectorMultilineTabs);

            tabGeneral.Controls.Add(grpStorage);
            tabGeneral.Controls.Add(grpAssoc);
            tabGeneral.Controls.Add(grpDefaults);
            tabGeneral.Controls.Add(grpUX);

            // ==========================================
            // TAB 2: BACKUPS & SECURITY
            // ==========================================
            var grpPolicy = new GroupBox
            {
                Text = "🛡️ Snapshot Retention Policy",
                Location = new Point(12, 10),
                Size = new Size(470, 115),
                Font = new Font("Segoe UI", 9F, FontStyle.Bold)
            };

            chkAutoCreateBackups = new CheckBox
            {
                Text = "Automatically create backup snapshots before saving CSF files",
                Location = new Point(15, 25),
                Size = new Size(440, 22),
                Font = new Font("Segoe UI", 9F, FontStyle.Regular)
            };

            lblMaxSnapshots = new Label
            {
                Text = "Max snapshots per session:",
                Location = new Point(15, 60),
                Size = new Size(160, 22),
                Font = new Font("Segoe UI", 9F, FontStyle.Regular)
            };

            numMaxSnapshots = new NumericUpDown
            {
                Location = new Point(180, 58),
                Size = new Size(55, 22),
                Minimum = 1,
                Maximum = 100,
                Value = 10,
                Font = new Font("Segoe UI", 9F, FontStyle.Regular)
            };

            lblAutoDeleteDays = new Label
            {
                Text = "Auto-delete older than (days):",
                Location = new Point(250, 60),
                Size = new Size(155, 22),
                Font = new Font("Segoe UI", 9F, FontStyle.Regular)
            };

            numAutoDeleteDays = new NumericUpDown
            {
                Location = new Point(405, 58),
                Size = new Size(50, 22),
                Minimum = 1,
                Maximum = 365,
                Value = 30,
                Font = new Font("Segoe UI", 9F, FontStyle.Regular)
            };

            grpPolicy.Controls.Add(chkAutoCreateBackups);
            grpPolicy.Controls.Add(lblMaxSnapshots);
            grpPolicy.Controls.Add(numMaxSnapshots);
            grpPolicy.Controls.Add(lblAutoDeleteDays);
            grpPolicy.Controls.Add(numAutoDeleteDays);

            var grpBackupFolder = new GroupBox
            {
                Text = "📁 Backup Storage Folder Location",
                Location = new Point(12, 135),
                Size = new Size(470, 85),
                Font = new Font("Segoe UI", 9F, FontStyle.Bold)
            };

            lblBackupDir = new Label
            {
                Text = "Backup Folder Path:",
                Location = new Point(15, 28),
                Size = new Size(120, 22),
                Font = new Font("Segoe UI", 9F, FontStyle.Regular)
            };

            txtBackupDir = new TextBox
            {
                Location = new Point(140, 26),
                Size = new Size(235, 22),
                Font = new Font("Segoe UI", 9F, FontStyle.Regular)
            };

            btnBrowseBackupDir = new Button
            {
                Text = "Browse...",
                Location = new Point(380, 24),
                Size = new Size(75, 26),
                Font = new Font("Segoe UI", 8.5F, FontStyle.Regular),
                UseVisualStyleBackColor = true
            };
            btnBrowseBackupDir.Click += BtnBrowseBackupDir_Click;

            grpBackupFolder.Controls.Add(lblBackupDir);
            grpBackupFolder.Controls.Add(txtBackupDir);
            grpBackupFolder.Controls.Add(btnBrowseBackupDir);

            tabBackups.Controls.Add(grpPolicy);
            tabBackups.Controls.Add(grpBackupFolder);

            // ==========================================
            // TAB 3: INI SCANNER
            // ==========================================
            var grpScanner = new GroupBox
            {
                Text = "🔍 INI & MAP Property Tags",
                Location = new Point(12, 10),
                Size = new Size(470, 365),
                Font = new Font("Segoe UI", 9F, FontStyle.Bold)
            };

            lblIniScanProps = new Label
            {
                Text = "Specify property tag names to extract string references from when scanning C&C game .INI or map files (separated by semicolons ';'):",
                Location = new Point(15, 22),
                Size = new Size(440, 36),
                Font = new Font("Segoe UI", 8.5F, FontStyle.Regular)
            };

            txtIniScanProps = new TextBox
            {
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                Location = new Point(15, 62),
                Size = new Size(440, 255),
                Font = new Font("Consolas", 9F, FontStyle.Regular)
            };

            btnResetIniScanProps = new Button
            {
                Text = "🔄 Reset to Default Tags",
                Location = new Point(280, 325),
                Size = new Size(175, 26),
                Font = new Font("Segoe UI", 8.5F, FontStyle.Regular),
                UseVisualStyleBackColor = true
            };
            btnResetIniScanProps.Click += (s, e) =>
            {
                txtIniScanProps.Text = new AppConfig().IniScanProperties;
            };

            grpScanner.Controls.Add(lblIniScanProps);
            grpScanner.Controls.Add(txtIniScanProps);
            grpScanner.Controls.Add(btnResetIniScanProps);

            tabScanner.Controls.Add(grpScanner);

            // ==========================================
            // BOTTOM DIALOG ACTIONS
            // ==========================================
            btnOK = new Button
            {
                Text = "OK",
                DialogResult = DialogResult.OK,
                Location = new Point(325, 492),
                Size = new Size(85, 28),
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                UseVisualStyleBackColor = true
            };
            btnOK.Click += BtnOK_Click;

            btnCancel = new Button
            {
                Text = "Cancel",
                DialogResult = DialogResult.Cancel,
                Location = new Point(417, 492),
                Size = new Size(85, 28),
                Font = new Font("Segoe UI", 9F, FontStyle.Regular),
                UseVisualStyleBackColor = true
            };

            this.Controls.Add(tabControl);
            this.Controls.Add(btnOK);
            this.Controls.Add(btnCancel);

            this.AcceptButton = btnOK;
            this.CancelButton = btnCancel;
        }

        private void LoadConfigToUI()
        {
            chkSaveInAppData.Checked = _config.SaveInAppData;
            chkAutoCreateBackups.Checked = _config.AutoCreateBackups;
            numMaxSnapshots.Value = Math.Max(1, Math.Min(100, _config.MaxBackupSnapshots));
            numAutoDeleteDays.Value = Math.Max(1, Math.Min(365, _config.AutoDeleteBackupDays));
            txtBackupDir.Text = string.IsNullOrWhiteSpace(_config.BackupDirectoryPath) ? "Backups" : _config.BackupDirectoryPath;

            numToastSeconds.Value = Math.Max(1, Math.Min(30, _config.NotificationToastDurationMs / 1000));
            numMaxUndo.Value = Math.Max(10, Math.Min(500, _config.MaxUndoLevels));

            txtCategoryPrefix.Text = string.IsNullOrWhiteSpace(_config.DefaultCategoryPrefix) ? "CSF_" : _config.DefaultCategoryPrefix.ToUpperInvariant();
            numMaxHistory.Value = Math.Max(1, Math.Min(100, _config.MaxSearchHistoryItems));
            numMaxMultiDisplay.Value = Math.Max(1, Math.Min(100, _config.MaxMultiKeyDisplayCount));
            if (chkRememberPanelLayoutPositions != null) chkRememberPanelLayoutPositions.Checked = _config.RememberPanelLayoutPositions;
            if (chkInspectorMultilineTabs != null) chkInspectorMultilineTabs.Checked = _config.InspectorMultilineTabs;
            txtIniScanProps.Text = string.IsNullOrWhiteSpace(_config.IniScanProperties) ? "" : _config.IniScanProperties;

            if (cmbDefaultStartupTab != null)
            {
                int sIdx = (int)_config.DefaultStartupMainTab;
                if (sIdx >= 0 && sIdx < cmbDefaultStartupTab.Items.Count)
                {
                    cmbDefaultStartupTab.SelectedIndex = sIdx;
                }
            }

            if (cmbDefaultLanguage != null)
            {
                for (int i = 0; i < cmbDefaultLanguage.Items.Count; i++)
                {
                    if ((cmbDefaultLanguage.Items[i] as LanguageComboItem)?.Language == _config.DefaultLanguage)
                    {
                        cmbDefaultLanguage.SelectedIndex = i;
                        break;
                    }
                }
            }

            txtIniScanProps.Text = string.IsNullOrWhiteSpace(_config.IniScanProperties) ? "" : _config.IniScanProperties;

            UpdateLocationInfo();
            UpdateAssociationStatus();
        }

        private void UpdateLocationInfo()
        {
            string targetPath = ConfigManager.GetActiveIniPath(chkSaveInAppData.Checked);
            lblLocationInfo.Text = $"Config Path: {targetPath}";
        }

        private void BtnAssociateCsf_Click(object sender, EventArgs e)
        {
            var result = MessageBox.Show(
                "Do you want to associate .CSF string table files with CSF Studio?\n\n" +
                "This will allow you to double-click any .CSF file in Windows Explorer to open it directly in this editor.",
                "Associate .CSF File Extension",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (result == DialogResult.Yes)
            {
                if (FileAssociationManager.AssociateCsfExtension())
                {
                    MessageBox.Show(".CSF file extension successfully associated with CSF Studio!", "Association Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    UpdateAssociationStatus();
                }
            }
        }

        private void UpdateAssociationStatus()
        {
            bool isAssociated = FileAssociationManager.IsCsfAssociated();
            if (isAssociated)
            {
                lblAssocStatus.Text = "Status: Associated ✔️";
                lblAssocStatus.ForeColor = Color.ForestGreen;
            }
            else
            {
                lblAssocStatus.Text = "Status: Not Associated";
                lblAssocStatus.ForeColor = Color.DimGray;
            }
        }

        private void BtnBrowseBackupDir_Click(object sender, EventArgs e)
        {
            using (var dlg = new FolderBrowserDialog())
            {
                dlg.Description = "Select Folder for Session Backups";
                dlg.ShowNewFolderButton = true;
                if (!string.IsNullOrWhiteSpace(txtBackupDir.Text))
                {
                    string resolved = ConfigManager.ResolveBackupDirectory(txtBackupDir.Text, chkSaveInAppData.Checked);
                    if (Directory.Exists(resolved)) dlg.SelectedPath = resolved;
                }

                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    txtBackupDir.Text = dlg.SelectedPath;
                }
            }
        }

        private void BtnClearHistory_Click(object sender, EventArgs e)
        {
            _config.KeySearchHistoryPlain.Clear();
            _config.KeySearchHistoryRegex.Clear();
            _config.ValueSearchHistoryPlain.Clear();
            _config.ValueSearchHistoryRegex.Clear();
            _config.FindHistoryPlain.Clear();
            _config.FindHistoryRegex.Clear();
            _config.ReplaceHistoryPlain.Clear();
            _config.ReplaceHistoryRegex.Clear();
            MessageBox.Show("Search and Find/Replace histories for all search modes have been cleared.", "Search History Cleared", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void BtnOK_Click(object sender, EventArgs e)
        {
            _config.SaveInAppData = chkSaveInAppData.Checked;
            _config.AutoCreateBackups = chkAutoCreateBackups.Checked;
            _config.MaxBackupSnapshots = (int)numMaxSnapshots.Value;
            _config.AutoDeleteBackupDays = (int)numAutoDeleteDays.Value;
            _config.BackupDirectoryPath = txtBackupDir.Text.Trim();

            _config.NotificationToastDurationMs = (int)numToastSeconds.Value * 1000;
            _config.MaxUndoLevels = (int)numMaxUndo.Value;

            _config.MaxSearchHistoryItems = (int)numMaxHistory.Value;
            _config.MaxMultiKeyDisplayCount = (int)numMaxMultiDisplay.Value;
            if (chkRememberPanelLayoutPositions != null) _config.RememberPanelLayoutPositions = chkRememberPanelLayoutPositions.Checked;
            if (chkInspectorMultilineTabs != null) _config.InspectorMultilineTabs = chkInspectorMultilineTabs.Checked;
            _config.IniScanProperties = txtIniScanProps.Text.Trim();

            if (cmbDefaultStartupTab != null && cmbDefaultStartupTab.SelectedIndex >= 0)
            {
                _config.DefaultStartupMainTab = (StartupMainTabOption)cmbDefaultStartupTab.SelectedIndex;
            }

            string prefix = txtCategoryPrefix.Text.Trim().ToUpperInvariant();
            if (string.IsNullOrWhiteSpace(prefix)) prefix = "CSF_";
            _config.DefaultCategoryPrefix = prefix;

            if (cmbDefaultLanguage.SelectedItem is LanguageComboItem selItem)
            {
                _config.DefaultLanguage = selItem.Language;
            }
        }
    }
}
