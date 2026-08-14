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

        private Label lblUiLanguage;
        private ComboBox cmbUiLanguage;
        private Button btnGenerateTranslation;

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

        private GroupBox grpAssoc;
        private GroupBox grpDefaults;
        private GroupBox grpUX;
        private GroupBox grpPolicy;
        private GroupBox grpBackupFolder;
        private GroupBox grpScanner;

        private Label lblIniScanProps;
        private TextBox txtIniScanProps;
        private Button btnResetIniScanProps;

        private Button btnClearHistory;
        private Button btnOK;
        private Button btnCancel;

        private string _originalLanguageFile;
        public Action<string> OnLanguagePreviewChanged { get; set; }

        public AppConfig Config => _config;

        public OptionsDialog(AppConfig currentConfig)
        {
            _config = currentConfig ?? new AppConfig();
            _originalLanguageFile = _config.UiLanguage;
            InitializeComponent();
            LoadConfigToUI();
            ApplyOptionsLocalization();
            this.FormClosing += OptionsDialog_FormClosing;
        }

        private void OptionsDialog_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (this.DialogResult != DialogResult.OK && !string.Equals(_originalLanguageFile, _config.UiLanguage, StringComparison.OrdinalIgnoreCase))
            {
                _config.UiLanguage = _originalLanguageFile;
                LanguageManager.LoadLanguage(_originalLanguageFile);
                OnLanguagePreviewChanged?.Invoke(_originalLanguageFile);
            }
        }

        private void InitializeComponent()
        {
            this.Text = LanguageManager.GetString("Options.Title", "⚙️ Application Options");
            this.ClientSize = new Size(660, 515);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.StartPosition = FormStartPosition.CenterParent;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.ShowIcon = false;

            // --- TAB CONTROL ---
            tabControl = new TabControl
            {
                Location = new Point(12, 12),
                Size = new Size(636, 445),
                Font = new Font("Segoe UI", 9F, FontStyle.Regular)
            };

            tabGeneral = new TabPage(LanguageManager.GetString("Options.Tab.General", "⚙️ General & Defaults"));
            tabBackups = new TabPage(LanguageManager.GetString("Options.Tab.Backups", "🛡️ Backups & Security"));
            tabScanner = new TabPage(LanguageManager.GetString("Options.Tab.Scanner", "🔍 INI Scanner"));

            tabControl.TabPages.Add(tabGeneral);
            tabControl.TabPages.Add(tabBackups);
            tabControl.TabPages.Add(tabScanner);

            // ==========================================
            // TAB 1: GENERAL & DEFAULTS
            // ==========================================
            // 1. Default Document Settings Group
            grpDefaults = new GroupBox
            {
                Text = LanguageManager.GetString("Options.Group.Defaults", "🌐 Language & Defaults"),
                Location = new Point(12, 10),
                Size = new Size(606, 116),
                Font = new Font("Segoe UI", 9F, FontStyle.Bold)
            };

            lblUiLanguage = new Label
            {
                Text = LanguageManager.GetString("Options.UiLanguage", "UI Language:"),
                Location = new Point(15, 23),
                Size = new Size(215, 22),
                Font = new Font("Segoe UI", 9F, FontStyle.Regular)
            };

            cmbUiLanguage = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Location = new Point(235, 20),
                Size = new Size(355, 22),
                DropDownWidth = 355,
                Font = new Font("Segoe UI", 9F, FontStyle.Regular)
            };

            btnGenerateTranslation = new Button
            {
                Text = LanguageManager.GetString("Options.BtnGenerateTranslations", "⚡ Generate Default Translation File"),
                Location = new Point(235, 19),
                Size = new Size(355, 24),
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                Visible = false
            };
            btnGenerateTranslation.Click += BtnGenerateTranslation_Click;
            ToolTipHelper.SetToolTip(btnGenerateTranslation, LanguageManager.GetString("ToolTip.Options.GenerateTranslations", "Generate Translation File: Creates the default English translation file (en.txt) containing all registered UI strings."));

            lblDefaultLanguage = new Label
            {
                Text = LanguageManager.GetString("Options.DefaultLanguage", "Default Language Header ID:"),
                Location = new Point(15, 53),
                Size = new Size(215, 22),
                Font = new Font("Segoe UI", 9F, FontStyle.Regular)
            };

            cmbDefaultLanguage = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Location = new Point(235, 50),
                Size = new Size(355, 22),
                Font = new Font("Segoe UI", 9F, FontStyle.Regular)
            };

            foreach (CsfLanguage lang in Enum.GetValues(typeof(CsfLanguage)))
            {
                cmbDefaultLanguage.Items.Add(new LanguageComboItem { Language = lang });
            }

            string langTip = LanguageManager.GetString("ToolTip.Options.DefaultLanguage", "Default Language Header ID: Sets the default 32-bit binary language ID assigned at offset 0x14 when creating new CSF string tables (e.g. 0 - EnglishUS, 4 - Spanish).");
            ToolTipHelper.SetToolTip(cmbDefaultLanguage, langTip);
            ToolTipHelper.SetToolTip(lblDefaultLanguage, langTip);

            lblCategoryPrefix = new Label
            {
                Text = LanguageManager.GetString("Options.DefaultPrefix", "Default Key Prefix:"),
                Location = new Point(15, 83),
                Size = new Size(215, 22),
                Font = new Font("Segoe UI", 9F, FontStyle.Regular)
            };

            txtCategoryPrefix = new TextBox
            {
                Location = new Point(235, 80),
                Size = new Size(100, 22),
                CharacterCasing = CharacterCasing.Upper,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold)
            };

            grpDefaults.Controls.Add(lblUiLanguage);
            grpDefaults.Controls.Add(cmbUiLanguage);
            grpDefaults.Controls.Add(btnGenerateTranslation);
            grpDefaults.Controls.Add(lblDefaultLanguage);
            grpDefaults.Controls.Add(cmbDefaultLanguage);
            grpDefaults.Controls.Add(lblCategoryPrefix);
            grpDefaults.Controls.Add(txtCategoryPrefix);

            // 2. User Experience & History Group
            grpUX = new GroupBox
            {
                Text = LanguageManager.GetString("Options.Group.UX", "🖥️ Interface & Search History"),
                Location = new Point(12, 134),
                Size = new Size(606, 195),
                Font = new Font("Segoe UI", 9F, FontStyle.Bold)
            };

            lblToastSeconds = new Label
            {
                Text = LanguageManager.GetString("Options.ToastDuration", "Notification toast duration (s):"),
                Location = new Point(15, 23),
                Size = new Size(215, 20),
                Font = new Font("Segoe UI", 9F, FontStyle.Regular)
            };

            numToastSeconds = new NumericUpDown
            {
                Location = new Point(235, 21),
                Size = new Size(55, 22),
                Minimum = 1,
                Maximum = 30,
                Value = 5,
                Font = new Font("Segoe UI", 9F, FontStyle.Regular)
            };

            lblMaxUndo = new Label
            {
                Text = LanguageManager.GetString("Options.MaxUndo", "Max Undo levels (Ctrl+Z):"),
                Location = new Point(310, 23),
                Size = new Size(225, 20),
                Font = new Font("Segoe UI", 9F, FontStyle.Regular)
            };

            numMaxUndo = new NumericUpDown
            {
                Location = new Point(540, 21),
                Size = new Size(50, 22),
                Minimum = 10,
                Maximum = 500,
                Value = 100,
                Font = new Font("Segoe UI", 9F, FontStyle.Regular)
            };

            lblMaxHistory = new Label
            {
                Text = LanguageManager.GetString("Options.MaxHistory", "Max Search History items:"),
                Location = new Point(15, 49),
                Size = new Size(215, 20),
                Font = new Font("Segoe UI", 9F, FontStyle.Regular)
            };

            numMaxHistory = new NumericUpDown
            {
                Location = new Point(235, 47),
                Size = new Size(55, 22),
                Minimum = 1,
                Maximum = 100,
                Value = 10,
                Font = new Font("Segoe UI", 9F, FontStyle.Regular)
            };

            btnClearHistory = new Button
            {
                Text = LanguageManager.GetString("Options.ClearHistory", "🧹 Clear Search History"),
                Location = new Point(310, 45),
                Size = new Size(280, 26),
                Font = new Font("Segoe UI", 8.5F, FontStyle.Regular),
                UseVisualStyleBackColor = true
            };
            btnClearHistory.Click += BtnClearHistory_Click;

            lblMaxMultiDisplay = new Label
            {
                Text = LanguageManager.GetString("Options.MaxMultiDisplay", "Max Multi-Key Editors Displayed:"),
                Location = new Point(15, 77),
                Size = new Size(300, 20),
                Font = new Font("Segoe UI", 9F, FontStyle.Regular)
            };

            numMaxMultiDisplay = new NumericUpDown
            {
                Location = new Point(320, 75),
                Size = new Size(55, 22),
                Minimum = 1,
                Maximum = 100,
                Value = 10,
                Font = new Font("Segoe UI", 9F, FontStyle.Regular)
            };
            ToolTipHelper.SetToolTip(numMaxMultiDisplay, LanguageManager.GetString("ToolTip.Options.MaxMultiDisplay", "Sets the maximum number of key editors rendered simultaneously when selecting multiple keys."));

            lblDefaultStartupTab = new Label
            {
                Text = LanguageManager.GetString("Options.StartupTab", "Default Startup View Tab:"),
                Location = new Point(15, 103),
                Size = new Size(215, 20),
                Font = new Font("Segoe UI", 9F, FontStyle.Regular)
            };

            cmbDefaultStartupTab = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Location = new Point(235, 101),
                Size = new Size(355, 22),
                Font = new Font("Segoe UI", 9F, FontStyle.Regular)
            };
            cmbDefaultStartupTab.Items.Add(LanguageManager.GetString("Tab.MasterView", "📋 Master Keys View (Global Table)"));
            cmbDefaultStartupTab.Items.Add(LanguageManager.GetString("Tab.PlainKeyEditor", "📋 Plain Keys View (Vertical List)"));
            cmbDefaultStartupTab.Items.Add(LanguageManager.GetString("Options.RememberLastTab", "🕒 Remember Last Active Tab"));
            ToolTipHelper.SetToolTip(cmbDefaultStartupTab, LanguageManager.GetString("ToolTip.Options.DefaultStartupTab", "Configures which main view tab is automatically opened when launching the application."));

            chkRememberPanelLayoutPositions = new CheckBox
            {
                Text = LanguageManager.GetString("Options.RememberLayout", "💾 Remember panel splitter positions and layout dimensions on exit"),
                AutoSize = true,
                Location = new Point(15, 132),
                Font = new Font("Segoe UI", 9F, FontStyle.Regular)
            };
            ToolTipHelper.SetToolTip(chkRememberPanelLayoutPositions, LanguageManager.GetString("ToolTip.Options.RememberPanelLayout", "Remembers side panel widths and inspector heights across application restarts."));

            chkInspectorMultilineTabs = new CheckBox
            {
                Text = LanguageManager.GetString("Options.InspectorMultiline", "📑 Show file tabs in multiple rows when space is constrained in inspector"),
                AutoSize = true,
                Location = new Point(15, 158),
                Font = new Font("Segoe UI", 9F, FontStyle.Regular)
            };
            ToolTipHelper.SetToolTip(chkInspectorMultilineTabs, LanguageManager.GetString("ToolTip.Options.InspectorMultilineTabs", "When enabled, file tabs in the entry inspector wrap onto multiple rows if space is tight. When disabled, tabs stay on a single row with scroll buttons to preserve vertical editor height."));

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

            // 3. Windows File Association Group
            grpAssoc = new GroupBox
            {
                Text = LanguageManager.GetString("Options.Group.Association", "🔗 Windows File Association (.CSF)"),
                Location = new Point(12, 337),
                Size = new Size(606, 65),
                Font = new Font("Segoe UI", 9F, FontStyle.Bold)
            };

            btnAssociateCsf = new Button
            {
                Text = LanguageManager.GetString("Options.BtnAssociate", "🔗 Associate .CSF Files with CSF Studio"),
                Location = new Point(15, 22),
                Size = new Size(280, 28),
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                UseVisualStyleBackColor = true
            };
            btnAssociateCsf.Click += BtnAssociateCsf_Click;

            lblAssocStatus = new Label
            {
                Location = new Point(305, 27),
                Size = new Size(285, 20),
                Font = new Font("Segoe UI", 9F, FontStyle.Bold)
            };

            grpAssoc.Controls.Add(btnAssociateCsf);
            grpAssoc.Controls.Add(lblAssocStatus);

            tabGeneral.Controls.Add(grpDefaults);
            tabGeneral.Controls.Add(grpUX);
            tabGeneral.Controls.Add(grpAssoc);

            // ==========================================
            // TAB 2: BACKUPS & SECURITY
            // ==========================================
            grpPolicy = new GroupBox
            {
                Text = LanguageManager.GetString("Options.Group.Backups", "💾 Automatic Snapshot Backups"),
                Location = new Point(12, 10),
                Size = new Size(606, 115),
                Font = new Font("Segoe UI", 9F, FontStyle.Bold)
            };

            chkAutoCreateBackups = new CheckBox
            {
                Text = LanguageManager.GetString("Options.AutoBackups", "Enable automatic background backups on session changes"),
                Location = new Point(15, 25),
                Size = new Size(575, 22),
                Font = new Font("Segoe UI", 9F, FontStyle.Regular)
            };

            lblMaxSnapshots = new Label
            {
                Text = LanguageManager.GetString("Options.MaxSnapshots", "Max snapshots per session:"),
                Location = new Point(15, 60),
                Size = new Size(215, 22),
                Font = new Font("Segoe UI", 9F, FontStyle.Regular)
            };

            numMaxSnapshots = new NumericUpDown
            {
                Location = new Point(235, 58),
                Size = new Size(55, 22),
                Minimum = 1,
                Maximum = 100,
                Value = 10,
                Font = new Font("Segoe UI", 9F, FontStyle.Regular)
            };

            lblAutoDeleteDays = new Label
            {
                Text = LanguageManager.GetString("Options.AutoDeleteDays", "Auto-delete older than (days):"),
                Location = new Point(310, 60),
                Size = new Size(225, 22),
                Font = new Font("Segoe UI", 9F, FontStyle.Regular)
            };

            numAutoDeleteDays = new NumericUpDown
            {
                Location = new Point(540, 58),
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

            grpBackupFolder = new GroupBox
            {
                Text = LanguageManager.GetString("Options.BackupFolder", "Backup Storage Folder:"),
                Location = new Point(12, 135),
                Size = new Size(606, 85),
                Font = new Font("Segoe UI", 9F, FontStyle.Bold)
            };

            lblBackupDir = new Label
            {
                Text = LanguageManager.GetString("Options.BackupFolder", "Backup Storage Folder:"),
                Location = new Point(15, 28),
                Size = new Size(160, 22),
                Font = new Font("Segoe UI", 9F, FontStyle.Regular)
            };

            txtBackupDir = new TextBox
            {
                Location = new Point(180, 26),
                Size = new Size(325, 22),
                Font = new Font("Segoe UI", 9F, FontStyle.Regular)
            };

            btnBrowseBackupDir = new Button
            {
                Text = LanguageManager.GetString("Button.Browse", "Browse..."),
                Location = new Point(515, 24),
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
            grpScanner = new GroupBox
            {
                Text = LanguageManager.GetString("Options.Tab.Scanner", "🔍 INI Scanner"),
                Location = new Point(12, 10),
                Size = new Size(606, 400),
                Font = new Font("Segoe UI", 9F, FontStyle.Bold)
            };

            lblIniScanProps = new Label
            {
                Text = LanguageManager.GetString("Options.ScannerPropsInfo", "Properties and section tags scanned for CSF string table references (separated by semicolon ';'):"),
                Location = new Point(15, 20),
                Size = new Size(575, 34),
                Font = new Font("Segoe UI", 8.5F, FontStyle.Regular)
            };

            txtIniScanProps = new TextBox
            {
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                Location = new Point(15, 56),
                Size = new Size(575, 298),
                Font = new Font("Consolas", 9F, FontStyle.Regular)
            };

            btnResetIniScanProps = new Button
            {
                Text = LanguageManager.GetString("Options.ResetScannerProps", "🔄 Reset to Default Tags"),
                Location = new Point(390, 362),
                Size = new Size(200, 26),
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
                Text = LanguageManager.GetString("Button.OK", "OK"),
                DialogResult = DialogResult.OK,
                Location = new Point(460, 470),
                Size = new Size(85, 28),
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                UseVisualStyleBackColor = true
            };
            btnOK.Click += BtnOK_Click;

            btnCancel = new Button
            {
                Text = LanguageManager.GetString("Button.Cancel", "Cancel"),
                DialogResult = DialogResult.Cancel,
                Location = new Point(555, 470),
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

            if (cmbUiLanguage != null)
            {
                cmbUiLanguage.SelectedIndexChanged -= CmbUiLanguage_SelectedIndexChanged;
                cmbUiLanguage.Items.Clear();
                var availLangs = LanguageManager.GetAvailableLanguages(_config);

                int selectedIdx = -1;
                for (int i = 0; i < availLangs.Count; i++)
                {
                    cmbUiLanguage.Items.Add(availLangs[i]);
                    if (string.Equals(availLangs[i].FileName, _config.UiLanguage, StringComparison.OrdinalIgnoreCase))
                    {
                        selectedIdx = i;
                    }
                }
                if (cmbUiLanguage.Items.Count > 0)
                {
                    cmbUiLanguage.SelectedIndex = selectedIdx >= 0 ? selectedIdx : 0;
                }

                bool hasLanguages = (cmbUiLanguage.Items.Count > 0);
                cmbUiLanguage.Visible = hasLanguages;
                if (btnGenerateTranslation != null)
                {
                    btnGenerateTranslation.Visible = !hasLanguages;
                }

                cmbUiLanguage.SelectedIndexChanged += CmbUiLanguage_SelectedIndexChanged;
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

            UpdateAssociationStatus();
        }

        private void BtnAssociateCsf_Click(object sender, EventArgs e)
        {
            var result = MessageBox.Show(
                LanguageManager.GetString("Msg.AssociateConfirm", "Do you want to associate .CSF string table files with CSF Studio?\n\nThis will allow you to double-click any .CSF file in Windows Explorer to open it directly in this editor."),
                LanguageManager.GetString("Title.AssociateConfirm", "Associate .CSF File Extension"),
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (result == DialogResult.Yes)
            {
                if (FileAssociationManager.AssociateCsfExtension())
                {
                    MessageBox.Show(
                        LanguageManager.GetString("Msg.AssociateSuccess", ".CSF file extension successfully associated with CSF Studio!"),
                        LanguageManager.GetString("Title.AssociateSuccess", "Association Success"),
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                    UpdateAssociationStatus();
                }
            }
        }

        private void UpdateAssociationStatus()
        {
            bool isAssociated = FileAssociationManager.IsCsfAssociated();
            if (isAssociated)
            {
                lblAssocStatus.Text = LanguageManager.GetString("Options.AssocStatus.Associated", "Status: Associated ✔️");
                lblAssocStatus.ForeColor = Color.ForestGreen;
            }
            else
            {
                lblAssocStatus.Text = LanguageManager.GetString("Options.AssocStatus.NotAssociated", "Status: Not Associated");
                lblAssocStatus.ForeColor = Color.DimGray;
            }
        }

        private void BtnBrowseBackupDir_Click(object sender, EventArgs e)
        {
            using (var dlg = new FolderBrowserDialog())
            {
                dlg.Description = LanguageManager.GetString("Options.SelectBackupFolder", "Select Folder for Session Backups");
                dlg.ShowNewFolderButton = true;
                if (!string.IsNullOrWhiteSpace(txtBackupDir.Text))
                {
                    string resolved = ConfigManager.ResolveBackupDirectory(txtBackupDir.Text);
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
            MessageBox.Show(
                LanguageManager.GetString("Msg.SearchHistoryCleared", "Search and Find/Replace histories for all search modes have been cleared."),
                LanguageManager.GetString("Title.SearchHistoryCleared", "Search History Cleared"),
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        private void BtnOK_Click(object sender, EventArgs e)
        {
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

            if (cmbUiLanguage.SelectedItem is LanguageInfo selUiLang)
            {
                _config.UiLanguage = selUiLang.FileName;
            }
        }

        private void CmbUiLanguage_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (cmbUiLanguage.SelectedItem is LanguageInfo lang)
            {
                _config.UiLanguage = lang.FileName;
                LanguageManager.LoadLanguage(lang.FileName);
                ApplyOptionsLocalization();
                OnLanguagePreviewChanged?.Invoke(lang.FileName);
            }
        }

        private void BtnGenerateTranslation_Click(object sender, EventArgs e)
        {
            try
            {
                string translationsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Translations");
                if (!Directory.Exists(translationsDir))
                {
                    Directory.CreateDirectory(translationsDir);
                }

                string genPath = Path.Combine(translationsDir, "en.txt");
                LanguageManager.GenerateEnglishTranslationFile(genPath);

                if (_config != null)
                {
                    if (_config.Translations == null) _config.Translations = new System.Collections.Generic.List<string>();
                    if (!_config.Translations.Contains("en.txt"))
                    {
                        _config.Translations.Add("en.txt");
                    }
                    _config.UiLanguage = "en.txt";
                    _originalLanguageFile = "en.txt";
                    ConfigManager.SaveConfig(_config);
                }

                MessageBox.Show(
                    string.Format(LanguageManager.GetString("Msg.GenTransSuccessFormat", "Generated UI translation file successfully:\n\n{0}"), genPath),
                    LanguageManager.GetString("Title.GenTransSuccess", "Translation File Generated"),
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);

                LanguageManager.LoadLanguage("en.txt");
                ApplyOptionsLocalization();
                LoadConfigToUI();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    ex.Message,
                    LanguageManager.GetString("Title.Error", "Error"),
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        public void ApplyOptionsLocalization()
        {
            this.Text = LanguageManager.GetString("Options.Title", "⚙️ Application Options");
            if (tabGeneral != null) tabGeneral.Text = LanguageManager.GetString("Options.Tab.General", "⚙️ General & Defaults");
            if (tabBackups != null) tabBackups.Text = LanguageManager.GetString("Options.Tab.Backups", "🛡️ Backups & Security");
            if (tabScanner != null) tabScanner.Text = LanguageManager.GetString("Options.Tab.Scanner", "🔍 INI Scanner");

            if (grpAssoc != null) grpAssoc.Text = LanguageManager.GetString("Options.Group.Association", "🔗 Windows File Association (.CSF)");
            if (grpDefaults != null) grpDefaults.Text = LanguageManager.GetString("Options.Group.Defaults", "🌐 Language & Defaults");
            if (grpUX != null) grpUX.Text = LanguageManager.GetString("Options.Group.UX", "🖥️ Interface & Search History");
            if (grpPolicy != null) grpPolicy.Text = LanguageManager.GetString("Options.Group.Backups", "💾 Automatic Snapshot Backups");
            if (grpBackupFolder != null) grpBackupFolder.Text = LanguageManager.GetString("Options.BackupFolder", "Backup Storage Folder:");
            if (grpScanner != null) grpScanner.Text = LanguageManager.GetString("Options.Tab.Scanner", "🔍 INI Scanner");

            if (btnAssociateCsf != null) btnAssociateCsf.Text = LanguageManager.GetString("Options.BtnAssociate", "🔗 Associate .CSF Files with CSF Studio");
            if (lblUiLanguage != null) lblUiLanguage.Text = LanguageManager.GetString("Options.UiLanguage", "UI Language:");
            if (btnGenerateTranslation != null) btnGenerateTranslation.Text = LanguageManager.GetString("Options.BtnGenerateTranslations", "⚡ Generate Default Translation File");
            if (lblDefaultLanguage != null) lblDefaultLanguage.Text = LanguageManager.GetString("Options.DefaultLanguage", "Default Language Header ID:");
            if (lblCategoryPrefix != null) lblCategoryPrefix.Text = LanguageManager.GetString("Options.DefaultPrefix", "Default Key Prefix:");

            if (lblToastSeconds != null) lblToastSeconds.Text = LanguageManager.GetString("Options.ToastDuration", "Notification toast duration (s):");
            if (lblMaxUndo != null) lblMaxUndo.Text = LanguageManager.GetString("Options.MaxUndo", "Max Undo levels (Ctrl+Z):");
            if (lblMaxHistory != null) lblMaxHistory.Text = LanguageManager.GetString("Options.MaxHistory", "Max Search History items:");
            if (btnClearHistory != null) btnClearHistory.Text = LanguageManager.GetString("Options.ClearHistory", "🧹 Clear Search History");
            if (lblMaxMultiDisplay != null) lblMaxMultiDisplay.Text = LanguageManager.GetString("Options.MaxMultiDisplay", "Max Multi-Key Editors Displayed:");

            if (lblDefaultStartupTab != null) lblDefaultStartupTab.Text = LanguageManager.GetString("Options.StartupTab", "Default Startup View Tab:");
            if (cmbDefaultStartupTab != null)
            {
                int prevIdx = cmbDefaultStartupTab.SelectedIndex;
                cmbDefaultStartupTab.Items.Clear();
                cmbDefaultStartupTab.Items.Add(LanguageManager.GetString("Tab.MasterView", "📋 Master Keys View (Global Table)"));
                cmbDefaultStartupTab.Items.Add(LanguageManager.GetString("Tab.PlainKeyEditor", "📋 Plain Keys View (Vertical List)"));
                cmbDefaultStartupTab.Items.Add(LanguageManager.GetString("Options.RememberLastTab", "🕒 Remember Last Active Tab"));
                if (prevIdx >= 0 && prevIdx < cmbDefaultStartupTab.Items.Count)
                {
                    cmbDefaultStartupTab.SelectedIndex = prevIdx;
                }
            }

            if (chkRememberPanelLayoutPositions != null) chkRememberPanelLayoutPositions.Text = LanguageManager.GetString("Options.RememberLayout", "💾 Remember panel splitter positions and layout dimensions on exit");
            if (chkInspectorMultilineTabs != null) chkInspectorMultilineTabs.Text = LanguageManager.GetString("Options.InspectorMultiline", "📑 Show file tabs in multiple rows when space is constrained in inspector");

            if (chkAutoCreateBackups != null) chkAutoCreateBackups.Text = LanguageManager.GetString("Options.AutoBackups", "Enable automatic background backups on session changes");
            if (lblMaxSnapshots != null) lblMaxSnapshots.Text = LanguageManager.GetString("Options.MaxSnapshots", "Max snapshots per session:");
            if (lblAutoDeleteDays != null) lblAutoDeleteDays.Text = LanguageManager.GetString("Options.AutoDeleteDays", "Auto-delete older than (days):");

            if (lblBackupDir != null) lblBackupDir.Text = LanguageManager.GetString("Options.BackupFolder", "Backup Storage Folder:");
            if (btnBrowseBackupDir != null) btnBrowseBackupDir.Text = LanguageManager.GetString("Button.Browse", "Browse...");
            if (lblIniScanProps != null) lblIniScanProps.Text = LanguageManager.GetString("Options.ScannerPropsInfo", "Properties and section tags scanned for CSF string table references (separated by semicolon ';'):");
            if (btnResetIniScanProps != null) btnResetIniScanProps.Text = LanguageManager.GetString("Options.ResetScannerProps", "🔄 Reset to Default Tags");
            if (btnOK != null) btnOK.Text = LanguageManager.GetString("Button.OK", "OK");
            if (btnCancel != null) btnCancel.Text = LanguageManager.GetString("Button.Cancel", "Cancel");

            UpdateAssociationStatus();

            // Refresh Tooltips
            string langTip = LanguageManager.GetString("ToolTip.Options.DefaultLanguage", "Default Language Header ID: Sets the default 32-bit binary language ID assigned at offset 0x14 when creating new CSF string tables (e.g. 0 - EnglishUS, 4 - Spanish).");
            if (cmbDefaultLanguage != null) ToolTipHelper.SetToolTip(cmbDefaultLanguage, langTip);
            if (lblDefaultLanguage != null) ToolTipHelper.SetToolTip(lblDefaultLanguage, langTip);
            if (btnGenerateTranslation != null) ToolTipHelper.SetToolTip(btnGenerateTranslation, LanguageManager.GetString("ToolTip.Options.GenerateTranslations", "Generate Translation File: Creates the default English translation file (en.txt) containing all registered UI strings."));
            if (numMaxMultiDisplay != null) ToolTipHelper.SetToolTip(numMaxMultiDisplay, LanguageManager.GetString("ToolTip.Options.MaxMultiDisplay", "Sets the maximum number of key editors rendered simultaneously when selecting multiple keys."));
            if (cmbDefaultStartupTab != null) ToolTipHelper.SetToolTip(cmbDefaultStartupTab, LanguageManager.GetString("ToolTip.Options.DefaultStartupTab", "Configures which main view tab is automatically opened when launching the application."));
            if (chkRememberPanelLayoutPositions != null) ToolTipHelper.SetToolTip(chkRememberPanelLayoutPositions, LanguageManager.GetString("ToolTip.Options.RememberPanelLayout", "Remembers side panel widths and inspector heights across application restarts."));
            if (chkInspectorMultilineTabs != null) ToolTipHelper.SetToolTip(chkInspectorMultilineTabs, LanguageManager.GetString("ToolTip.Options.InspectorMultilineTabs", "When enabled, file tabs in the entry inspector wrap onto multiple rows if space is tight. When disabled, tabs stay on a single row with scroll buttons to preserve vertical editor height."));
        }
    }
}
