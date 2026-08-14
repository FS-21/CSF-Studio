using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using CsfStudio.Core;
using CsfStudio.Core.Translation;

namespace CsfStudio.UI
{
    public class SessionManagerDialog : Form
    {
        private Panel panelScrollContainer;
        private FlowLayoutPanel flowPanelFiles;
        private Button btnAddCardBottom;
        private Button btnApply;
        private Button btnCancel;
        private Label lblHeader;
        private ToolTip _toolTip = new ToolTip();

        private CsfSession _session;

        public class SessionCardItem
        {
            public string UserDefinedLabel { get; set; }
            public string TranslationContentLanguage { get; set; } = string.Empty;
            public string SuggestedPlaceholderLabel { get; set; }
            public string FilePath { get; set; }
            public CsfDocument Document { get; set; }
            public bool IsBaseReference { get; set; }
            public bool IsPlaceholderActive { get; set; }
            public Panel CardPanel { get; set; }
            public TextBox TagTextBox { get; set; }
            public RadioButton BaseRadioButton { get; set; }
            public Label PathLabel { get; set; }
            public Label KeysLabel { get; set; }
            public Label ErrorLabel { get; set; }
            public Label TranslationLanguageLabel { get; set; }
            public ComboBox TranslationLanguageComboBox { get; set; }

            public string EffectiveLabel
            {
                get
                {
                    if (IsPlaceholderActive || string.IsNullOrWhiteSpace(UserDefinedLabel))
                        return string.Empty;
                    return UserDefinedLabel.Trim();
                }
            }
        }

        public List<SessionCardItem> SessionItems { get; private set; } = new List<SessionCardItem>();

        public SessionManagerDialog(CsfSession currentSession)
        {
            _session = currentSession;
            InitializeComponent();
            LoadCurrentSessionData();
        }

        private void InitializeComponent()
        {
            this.Text = LanguageManager.GetString("SessionManager.Title", "Multi-CSF Session Manager & File Setup");
            this.Size = new Size(980, 600);
            this.MinimumSize = new Size(980, 500);
            this.StartPosition = FormStartPosition.CenterParent;
            this.ShowIcon = false;

            var panelTop = new Panel
            {
                Dock = DockStyle.Top,
                Height = 42,
                Padding = new Padding(12, 10, 12, 10),
                BackColor = SystemColors.Control
            };

            lblHeader = new Label
            {
                Text = LanguageManager.GetString("SessionManager.Header", "Configure loaded CSFs. Select one file as Primary CSF file. Label is optional (gray italic shows auto-assigned name):"),
                Dock = DockStyle.Fill,
                Font = new Font(FontFamily.GenericSansSerif, 8.5f, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft
            };

            panelTop.Controls.Add(lblHeader);

            panelScrollContainer = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = Color.FromArgb(242, 242, 242)
            };

            flowPanelFiles = new FlowLayoutPanel
            {
                Dock = DockStyle.None,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                WrapContents = true,
                Padding = new Padding(0, 10, 0, 20),
                BackColor = Color.Transparent
            };

            panelScrollContainer.Controls.Add(flowPanelFiles);
            panelScrollContainer.Resize += (s, e) => CenterFlowPanelCards();

            var panelBottom = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 55,
                Padding = new Padding(10),
                BackColor = SystemColors.Control
            };

            btnAddCardBottom = new Button
            {
                Text = LanguageManager.GetString("SessionManager.AddSlot", "➕ Add New CSF Slot"),
                Location = new Point(12, 11),
                Size = new Size(185, 33),
                Font = new Font(FontFamily.GenericSansSerif, 9f, FontStyle.Bold),
                UseVisualStyleBackColor = true
            };

            btnApply = new Button
            {
                Text = LanguageManager.GetString("SessionManager.OpenSession", "✔️ Open Session"),
                DialogResult = DialogResult.OK,
                Size = new Size(180, 33),
                Font = new Font(FontFamily.GenericSansSerif, 9.5f, FontStyle.Bold),
                BackColor = Color.FromArgb(35, 130, 215),
                ForeColor = Color.White
            };

            btnCancel = new Button
            {
                Text = LanguageManager.GetString("Button.Cancel", "Cancel"),
                DialogResult = DialogResult.Cancel,
                Size = new Size(90, 33)
            };

            void LayoutBottomPanel()
            {
                int pWidth = panelBottom.ClientSize.Width;
                btnCancel.Location = new Point(pWidth - 102, 11);
                btnApply.Location = new Point(pWidth - 102 - 190, 11);
            }

            panelBottom.Resize += (s, e) => LayoutBottomPanel();
            LayoutBottomPanel();

            btnAddCardBottom.Click += BtnAddCardBottom_Click;
            btnApply.Click += BtnApply_Click;

            _toolTip.AutoPopDelay = 10000;
            _toolTip.InitialDelay = 400;
            _toolTip.ReshowDelay = 200;
            _toolTip.ShowAlways = true;

            ToolTipHelper.SetToolTip(_toolTip, lblHeader, LanguageManager.GetString("ToolTip.SessionManager.Header", "Multi-CSF Session Setup: Configure all loaded .CSF string table files for your multi-language project workspace."));
            ToolTipHelper.SetToolTip(_toolTip, btnAddCardBottom, LanguageManager.GetString("ToolTip.SessionManager.AddCard", "Add CSF Slot: Add a new slot card to load another Command & Conquer .CSF file or edit a new language table."));
            ToolTipHelper.SetToolTip(_toolTip, btnApply, LanguageManager.GetString("ToolTip.SessionManager.Apply", "Open Session: Apply your file setup and open all loaded CSF tables in the main workspace editor."));
            ToolTipHelper.SetToolTip(_toolTip, btnCancel, LanguageManager.GetString("ToolTip.SessionManager.Cancel", "Cancel: Close this session manager dialog without saving configuration changes."));

            panelBottom.Controls.Add(btnAddCardBottom);
            panelBottom.Controls.Add(btnApply);
            panelBottom.Controls.Add(btnCancel);

            this.Controls.Add(panelScrollContainer);
            this.Controls.Add(panelBottom);
            this.Controls.Add(panelTop);
        }

        private void CenterFlowPanelCards()
        {
            if (panelScrollContainer == null || flowPanelFiles == null) return;

            int availW = panelScrollContainer.ClientSize.Width;
            int cardTotalW = 405 + 12; // 417px per card with margins
            
            int cols = Math.Max(1, availW / cardTotalW);
            if (SessionItems != null && SessionItems.Count > 0 && SessionItems.Count < cols)
            {
                cols = SessionItems.Count;
            }

            int targetWidth = cols * cardTotalW;
            flowPanelFiles.MinimumSize = new Size(targetWidth, 0);
            flowPanelFiles.MaximumSize = new Size(targetWidth, 0);

            int leftX = Math.Max(0, (availW - targetWidth) / 2);
            flowPanelFiles.Location = new Point(leftX, flowPanelFiles.Location.Y);
        }

        private void LoadCurrentSessionData()
        {
            SessionItems.Clear();
            if (_session != null && _session.Documents.Count > 0)
            {
                for (int i = 0; i < _session.Documents.Count; i++)
                {
                    var doc = _session.Documents[i];
                    SessionItems.Add(new SessionCardItem
                    {
                        UserDefinedLabel = doc.LanguageTag ?? string.Empty,
                        TranslationContentLanguage = doc.TranslationContentLanguage ?? string.Empty,
                        FilePath = doc.FilePath,
                        Document = doc.Document,
                        IsBaseReference = (i == 0)
                    });
                }
            }

            if (SessionItems.Count == 0)
            {
                var defaultLang = ConfigManager.LoadConfig().DefaultLanguage;
                SessionItems.Add(new SessionCardItem
                {
                    UserDefinedLabel = string.Empty,
                    FilePath = null,
                    Document = new CsfDocument { Language = defaultLang },
                    IsBaseReference = true
                });
            }

            RebuildFlowPanelCards();
        }

        private void RebuildFlowPanelCards()
        {
            flowPanelFiles.Controls.Clear();

            if (!SessionItems.Any(i => i.IsBaseReference) && SessionItems.Count > 0)
            {
                SessionItems[0].IsBaseReference = true;
            }

            // Calculate sequential preview placeholders (CSF_01, CSF_02...)
            for (int i = 0; i < SessionItems.Count; i++)
            {
                SessionItems[i].SuggestedPlaceholderLabel = $"CSF_{i + 1:D2}";
            }

            foreach (var item in SessionItems)
            {
                var card = CreateFileCardPanel(item);
                item.CardPanel = card;
                flowPanelFiles.Controls.Add(card);
            }

            CenterFlowPanelCards();
            ValidateUniqueLanguageTags();
        }

        private Panel CreateFileCardPanel(SessionCardItem item)
        {
            var card = new Panel
            {
                Size = new Size(405, 148),
                Margin = new Padding(6),
                Padding = new Padding(10),
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.White
            };

            var radioBase = new RadioButton
            {
                Text = item.IsBaseReference
                    ? LanguageManager.GetString("SessionManager.PrimaryCsf", "Primary CSF")
                    : LanguageManager.GetString("SessionManager.SetPrimaryCsf", "Set Primary CSF"),
                Checked = item.IsBaseReference,
                Location = new Point(6, 12),
                Size = new Size(115, 24),
                Font = new Font(FontFamily.GenericSansSerif, 8f, item.IsBaseReference ? FontStyle.Bold : FontStyle.Regular),
                ForeColor = item.IsBaseReference ? Color.DarkBlue : Color.DimGray
            };

            item.BaseRadioButton = radioBase;

            radioBase.CheckedChanged += (s, e) =>
            {
                if (radioBase.Checked)
                {
                    foreach (var other in SessionItems)
                    {
                        other.IsBaseReference = (other == item);
                    }
                    RebuildFlowPanelCards();
                }
            };

            var lblTagPrompt = new Label { Text = LanguageManager.GetString("SessionManager.FileLabel", "Label:"), Location = new Point(120, 15), AutoSize = true, Font = new Font(FontFamily.GenericSansSerif, 8.5f, FontStyle.Bold) };
            
            var txtTag = new TextBox
            {
                Location = new Point(168, 12),
                Size = new Size(146, 23)
            };

            item.TagTextBox = txtTag;

            var lblTranslationLanguage = new Label
            {
                Text = LanguageManager.GetString("SessionManager.TranslationLang", "Translation:"),
                Location = new Point(86, 46),
                AutoSize = true,
                Font = new Font(FontFamily.GenericSansSerif, 8.5f, FontStyle.Bold)
            };

            var cboTranslationLanguage = new ComboBox
            {
                Location = new Point(176, 43),
                Size = new Size(220, 23),
                DropDownStyle = ComboBoxStyle.DropDown,
                Font = new Font(FontFamily.GenericSansSerif, 8.5f)
            };
            foreach (var option in TranslationLanguageHelper.GetLanguageOptions())
            {
                cboTranslationLanguage.Items.Add(option);
            }

            item.TranslationLanguageLabel = lblTranslationLanguage;
            item.TranslationLanguageComboBox = cboTranslationLanguage;
            bool isTranslationLanguageSyncing = false;

            void UpdateTranslationLanguageControls()
            {
                // Initial value: explicit override (TranslationContentLanguage),
                // else the physical header (0x14) language, else default.
                string language = TranslationLanguageHelper.Normalize(item.TranslationContentLanguage);
                if (string.IsNullOrEmpty(language))
                {
                    language = TranslationLanguageHelper.GetIsoCode(item.Document?.Language);
                }
                if (string.IsNullOrEmpty(language))
                {
                    language = TranslationLanguageHelper.GetDefaultSourceLanguage();
                }

                item.TranslationContentLanguage = language;

                string display = TranslationLanguageHelper.GetDisplayName(language);
                isTranslationLanguageSyncing = true;
                try
                {
                    if (cboTranslationLanguage.Items.Contains(display))
                        cboTranslationLanguage.SelectedItem = display;
                    else
                        cboTranslationLanguage.Text = display;
                }
                finally
                {
                    isTranslationLanguageSyncing = false;
                }
            }

            cboTranslationLanguage.TextChanged += (s, e) =>
            {
                if (isTranslationLanguageSyncing || !cboTranslationLanguage.Visible) return;
                string language = TranslationLanguageHelper.Normalize(cboTranslationLanguage.Text);
                if (!string.IsNullOrEmpty(language) && language != "auto")
                {
                    item.TranslationContentLanguage = language;
                }
            };
            UpdateTranslationLanguageControls();

            // WATERMARK / PLACEHOLDER LOGIC IN LIGHT GRAY ITALIC
            void SetWatermarkState()
            {
                if (string.IsNullOrWhiteSpace(item.UserDefinedLabel))
                {
                    item.IsPlaceholderActive = true;
                    txtTag.Text = item.SuggestedPlaceholderLabel;
                    txtTag.ForeColor = Color.Gray;
                    txtTag.Font = new Font(FontFamily.GenericSansSerif, 9, FontStyle.Italic);
                }
                else
                {
                    item.IsPlaceholderActive = false;
                    txtTag.Text = item.UserDefinedLabel;
                    txtTag.ForeColor = SystemColors.WindowText;
                    txtTag.Font = new Font(FontFamily.GenericSansSerif, 9, FontStyle.Bold);
                }
            }

            SetWatermarkState();

            txtTag.GotFocus += (s, e) =>
            {
                if (item.IsPlaceholderActive)
                {
                    txtTag.Text = string.Empty;
                    txtTag.ForeColor = SystemColors.WindowText;
                    txtTag.Font = new Font(FontFamily.GenericSansSerif, 9, FontStyle.Bold);
                }
            };

            txtTag.LostFocus += (s, e) =>
            {
                item.UserDefinedLabel = txtTag.Text.Trim();
                SetWatermarkState();
                ValidateUniqueLanguageTags();
            };

            txtTag.TextChanged += (s, e) =>
            {
                if (!item.IsPlaceholderActive)
                {
                    item.UserDefinedLabel = txtTag.Text;
                    ValidateUniqueLanguageTags();
                }
            };

            var btnBrowse = new Button
            {
                Text = LanguageManager.GetString("Button.Browse", "📂 Browse"),
                Location = new Point(8, 43),
                Size = new Size(74, 24),
                Font = new Font(FontFamily.GenericSansSerif, 8.5f, FontStyle.Bold)
            };

            var btnRemove = new Button
            {
                Text = LanguageManager.GetString("SessionManager.ButtonRemove", "❌ Remove"),
                Location = new Point(320, 9),
                Size = new Size(76, 26),
                ForeColor = Color.DarkRed
            };

            string fullPathDisplay = string.IsNullOrEmpty(item.FilePath)
                ? LanguageManager.GetString("SessionManager.UnsavedInMemoryDoc", "Unsaved in-memory document")
                : item.FilePath;
            var lblPath = new Label
            {
                Text = fullPathDisplay,
                Location = new Point(10, 70),
                Size = new Size(385, 18),
                AutoEllipsis = true,
                ForeColor = Color.DimGray,
                Font = new Font(FontFamily.GenericSansSerif, 7.5f)
            };

            item.PathLabel = lblPath;

            int keysCount = item.Document?.Labels.Count ?? 0;
            var lblKeys = new Label
            {
                Text = string.Format(LanguageManager.GetString("SessionManager.TotalKeysFormat", "Total Keys: {0:N0}"), keysCount),
                Location = new Point(10, 95),
                AutoSize = true,
                Font = new Font(FontFamily.GenericSansSerif, 8, FontStyle.Italic)
            };

            item.KeysLabel = lblKeys;

            string defaultMarker = string.IsNullOrEmpty(item.FilePath)
                ? LanguageManager.GetString("SessionManager.DefaultMarker", " (Default)")
                : string.Empty;
            string detectedLangStr = item.Document != null ? item.Document.Language.ToString() : "EnglishUS";
            var lblDetectedLang = new Label
            {
                Text = string.Format(LanguageManager.GetString("SessionManager.HeaderLangIdFormat", "Header Lang ID: {0}{1}"), detectedLangStr, defaultMarker),
                Location = new Point(140, 95),
                AutoSize = true,
                Font = new Font(FontFamily.GenericSansSerif, 8, FontStyle.Regular),
                ForeColor = Color.DarkSlateGray
            };

            var lblError = new Label
            {
                Text = LanguageManager.GetString("SessionManager.DuplicateLabelError", "⚠️ Duplicate Label!"),
                Location = new Point(280, 95),
                AutoSize = true,
                ForeColor = Color.Red,
                Visible = false,
                Font = new Font(FontFamily.GenericSansSerif, 8, FontStyle.Bold)
            };

            item.ErrorLabel = lblError;

            btnBrowse.Click += (s, e) =>
            {
                using (var dlg = new OpenFileDialog())
                {
                    dlg.Filter = LanguageManager.GetString("Filter.CsfOpenFilter", "Command & Conquer String Tables (*.csf)|*.csf|All Files (*.*)|*.*");
                    dlg.Title = LanguageManager.GetString("SessionManager.SelectCsfTitle", "Select CSF File");
                    var cfg = ConfigManager.LoadConfig();
                    if (cfg != null && !string.IsNullOrWhiteSpace(cfg.LastOpenDirectory) && Directory.Exists(cfg.LastOpenDirectory))
                    {
                        dlg.InitialDirectory = cfg.LastOpenDirectory;
                    }
                    if (dlg.ShowDialog() == DialogResult.OK)
                    {
                        try
                        {
                            item.Document = CsfFileHandler.Load(dlg.FileName);
                            ToolTipHelper.CheckAndPromptUnknownLanguage(item.Document, dlg.FileName, this);
                            item.FilePath = dlg.FileName;
                            lblPath.Text = dlg.FileName;
                            ToolTipHelper.SetToolTip(_toolTip, lblPath, dlg.FileName);
                            lblKeys.Text = string.Format(LanguageManager.GetString("SessionManager.TotalKeysFormat", "Total Keys: {0:N0}"), item.Document.Labels.Count);
                            lblDetectedLang.Text = string.Format(LanguageManager.GetString("SessionManager.HeaderLangFormat", "Header Lang ID: {0}"), item.Document.Language);
                            UpdateTranslationLanguageControls();

                            string dir = Path.GetDirectoryName(dlg.FileName);
                            if (!string.IsNullOrWhiteSpace(dir) && cfg != null)
                            {
                                cfg.LastOpenDirectory = dir;
                                ConfigManager.SaveConfig(cfg);
                            }
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show(
                                string.Format(LanguageManager.GetString("Msg.ErrorLoadingCsfFileFormat", "Error loading CSF file:\n{0}"), ex.Message),
                                LanguageManager.GetString("Title.Error", "Error"),
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Error);
                        }
                    }
                }
            };

            btnRemove.Click += (s, e) =>
            {
                SessionItems.Remove(item);
                RebuildFlowPanelCards();
            };

            ToolTipHelper.SetToolTip(_toolTip, radioBase, LanguageManager.GetString("ToolTip.SessionManager.RadioBase", "Set Primary CSF: Designate this file as the Primary CSF reference. Other files will copy values and sync missing keys from this primary file."));
            ToolTipHelper.SetToolTip(_toolTip, lblTagPrompt, LanguageManager.GetString("ToolTip.SessionManager.TagPrompt", "File Label: Optional unique label identifier for this CSF file (e.g. ENG, SPA, FRE, GUI)."));
            ToolTipHelper.SetToolTip(_toolTip, txtTag, LanguageManager.GetString("ToolTip.SessionManager.TxtTag", "File Label: Optional unique label identifier for this CSF file (e.g. ENG, SPA, FRE, GUI). If left blank, an automatic placeholder label like CSF_01 is assigned."));
            ToolTipHelper.SetToolTip(_toolTip, btnBrowse, LanguageManager.GetString("ToolTip.SessionManager.BtnBrowse", "Browse File: Select and load an existing Command & Conquer .CSF file from your hard drive into this slot."));
            ToolTipHelper.SetToolTip(_toolTip, btnRemove, LanguageManager.GetString("ToolTip.SessionManager.BtnRemove", "Remove Slot: Remove this file slot card from the active session workspace."));
            ToolTipHelper.SetToolTip(_toolTip, lblPath, fullPathDisplay);
            ToolTipHelper.SetToolTip(_toolTip, lblKeys, LanguageManager.GetString("ToolTip.SessionManager.TotalKeys", "Total Keys Count: Total number of string key entries currently contained inside this .CSF file."));
            ToolTipHelper.SetToolTip(_toolTip, lblDetectedLang, LanguageManager.GetString("ToolTip.SessionManager.BinaryLangId", "Binary Language ID: The internal binary language DWORD detected in byte offset 0x14 of the CSF file header."));
            ToolTipHelper.SetToolTip(_toolTip, lblTranslationLanguage, LanguageManager.GetString("ToolTip.SessionManager.TranslationContentLang", "Translation content language used when this CSF has LanguageNeutral in its binary header."));
            ToolTipHelper.SetToolTip(_toolTip, cboTranslationLanguage, LanguageManager.GetString("ToolTip.SessionManager.CboTranslationLang", "Content language for translation. This does not change the CSF binary Language ID."));
            ToolTipHelper.SetToolTip(_toolTip, lblError, LanguageManager.GetString("ToolTip.SessionManager.DuplicateLabelError", "Duplicate Label Alert: Every file in a multi-CSF session must have a unique label tag name."));

            card.Controls.Add(radioBase);
            card.Controls.Add(lblTagPrompt);
            card.Controls.Add(txtTag);
            card.Controls.Add(lblTranslationLanguage);
            card.Controls.Add(cboTranslationLanguage);
            card.Controls.Add(btnBrowse);
            card.Controls.Add(btnRemove);
            card.Controls.Add(lblPath);
            card.Controls.Add(lblKeys);
            card.Controls.Add(lblDetectedLang);
            card.Controls.Add(lblError);

            return card;
        }

        private bool ValidateUniqueLanguageTags()
        {
            var tagCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            bool isValid = true;

            foreach (var item in SessionItems)
            {
                string tag = item.EffectiveLabel;
                if (!string.IsNullOrEmpty(tag))
                {
                    if (!tagCounts.ContainsKey(tag)) tagCounts[tag] = 0;
                    tagCounts[tag]++;
                }
            }

            foreach (var item in SessionItems)
            {
                string tag = item.EffectiveLabel;
                bool isDuplicate = !string.IsNullOrEmpty(tag) && tagCounts[tag] > 1;

                if (isDuplicate)
                {
                    item.CardPanel.BackColor = Color.FromArgb(255, 235, 235);
                    item.TagTextBox.BackColor = Color.Pink;
                    item.ErrorLabel.Text = LanguageManager.GetString("SessionManager.DuplicateLabelError", "⚠️ Duplicate Label!");
                    item.ErrorLabel.Visible = true;
                    isValid = false;
                }
                else
                {
                    item.CardPanel.BackColor = Color.White;
                    item.TagTextBox.BackColor = item.IsPlaceholderActive ? Color.White : Color.White;
                    item.ErrorLabel.Visible = false;
                }
            }

            btnApply.Enabled = isValid && SessionItems.Count > 0;
            return isValid;
        }

        private void BtnAddCardBottom_Click(object sender, EventArgs e)
        {
            var defaultLang = ConfigManager.LoadConfig().DefaultLanguage;
            var newItem = new SessionCardItem
            {
                UserDefinedLabel = string.Empty,
                FilePath = null,
                Document = new CsfDocument { Language = defaultLang },
                IsBaseReference = (SessionItems.Count == 0)
            };

            SessionItems.Add(newItem);
            RebuildFlowPanelCards();
        }

        private void BtnApply_Click(object sender, EventArgs e)
        {
            if (!ValidateUniqueLanguageTags())
            {
                MessageBox.Show(
                    LanguageManager.GetString("Msg.DuplicateFileLabelWarning", "Cannot open session: All specified File Labels must be UNIQUE."),
                    LanguageManager.GetString("Title.DuplicateFileLabelWarning", "Duplicate File Label Warning"),
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                this.DialogResult = DialogResult.None;
                return;
            }

            // If user left box empty/placeholder, assign suggested placeholder (lang_01, lang_02...)
            foreach (var item in SessionItems)
            {
                string finalTag = string.IsNullOrWhiteSpace(item.EffectiveLabel) ? item.SuggestedPlaceholderLabel : item.EffectiveLabel;
                item.UserDefinedLabel = finalTag;
            }

            var baseItem = SessionItems.FirstOrDefault(i => i.IsBaseReference);
            if (baseItem != null && SessionItems.IndexOf(baseItem) != 0)
            {
                SessionItems.Remove(baseItem);
                SessionItems.Insert(0, baseItem);
            }
        }
    }
}
