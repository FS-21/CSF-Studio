using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using CsfStudio.Core;
using CsfStudio.Core.Translation;

namespace CsfStudio.UI
{
    public class TranslationSettingsForm : Form
    {
        private TextBox txtSystemPrompt;
        private TextBox txtDefaultSourceLang;
        private NumericUpDown numBatchSize;
        private NumericUpDown numDelayMs;

        private ListBox lstServices;
        private Button btnMoveUp;
        private Button btnMoveDown;
        private Button btnAddPreset;
        private Button btnDuplicate;
        private Button btnRemove;

        private TextBox txtDisplayName;
        private ComboBox cboProviderType;
        private TextBox txtApiKey;
        private TextBox txtEndpoint;
        private ComboBox cboModel;
        private Label lblModelField;
        private Button btnFetchModels;
        private TextBox txtUrlTemplate;
        private Label lblUrlTemplate;
        private CheckBox chkIsEnabled;
        private Button btnTestConnection;
        private Button btnSave;
        private Button btnCancel;

        private TranslationServiceConfig _selectedConfig;
        private readonly string _initialSectionName;

        public TranslationSettingsForm() : this(null)
        {
        }

        public TranslationSettingsForm(string initialSectionName)
        {
            _initialSectionName = initialSectionName;
            InitializeComponent();
            LoadData();
        }

        private void InitializeComponent()
        {
            this.Text = LanguageManager.GetString("TranslationSettings.Title", "⚙️ Translation & AI Services Settings");
            this.Size = new Size(840, 600);
            this.MinimumSize = new Size(840, 600);
            this.StartPosition = FormStartPosition.CenterParent;
            this.ShowIcon = false;

            var pnlTop = new Panel { Dock = DockStyle.Top, Height = 155, Padding = new Padding(10) };
            var lblGlobalPrompt = new Label { Text = LanguageManager.GetString("TranslationSettings.GlobalPrompt", "Global System Prompt for AI / LLM Models:"), Location = new Point(10, 10), AutoSize = true, Font = new Font(FontFamily.GenericSansSerif, 8.5f, FontStyle.Bold) };
            var btnResetPrompt = new Button { Text = LanguageManager.GetString("TranslationSettings.ResetPrompt", "🔄 Reset Default System Prompt"), Location = new Point(560, 5), Size = new Size(230, 24), Font = new Font(FontFamily.GenericSansSerif, 8f) };
            txtSystemPrompt = new TextBox { Location = new Point(10, 32), Size = new Size(780, 70), Multiline = true, ScrollBars = ScrollBars.Vertical };

            btnResetPrompt.Click += (s, e) =>
            {
                txtSystemPrompt.Text = LanguageManager.GetString("TranslationSettings.DefaultSystemPromptText", "You are an expert game localizer for Command & Conquer: Red Alert 2. Translate string table values accurately while preserving military tone, conciseness, and brevity. NEVER alter or translate formatting tags like \\n or variables like {0}.");
            };

            var lblSourceLang = new Label { Text = LanguageManager.GetString("TranslationSettings.DefaultSourceLang", "Default Source Language:"), Location = new Point(10, 115), AutoSize = true };
            txtDefaultSourceLang = new TextBox { Location = new Point(160, 112), Size = new Size(60, 23) };

            var lblBatch = new Label { Text = LanguageManager.GetString("TranslationSettings.BatchSize", "Batch Size:"), Location = new Point(240, 115), AutoSize = true };
            numBatchSize = new NumericUpDown { Location = new Point(315, 112), Size = new Size(60, 23), Minimum = 1, Maximum = 100, Value = 25 };

            var lblDelay = new Label { Text = LanguageManager.GetString("TranslationSettings.BatchDelay", "Batch Delay (ms):"), Location = new Point(395, 115), AutoSize = true };
            numDelayMs = new NumericUpDown { Location = new Point(505, 112), Size = new Size(70, 23), Minimum = 0, Maximum = 5000, Value = 300 };

            pnlTop.Controls.Add(lblGlobalPrompt);
            pnlTop.Controls.Add(btnResetPrompt);
            pnlTop.Controls.Add(txtSystemPrompt);
            pnlTop.Controls.Add(lblSourceLang);
            pnlTop.Controls.Add(txtDefaultSourceLang);
            pnlTop.Controls.Add(lblBatch);
            pnlTop.Controls.Add(numBatchSize);
            pnlTop.Controls.Add(lblDelay);
            pnlTop.Controls.Add(numDelayMs);

            var pnlLeft = new Panel { Dock = DockStyle.Left, Width = 290, Padding = new Padding(10) };
            var lblListTitle = new Label { Text = LanguageManager.GetString("TranslationSettings.ServicesTitle", "Configured Services / AI Models"), Location = new Point(10, 5), AutoSize = true, Font = new Font(FontFamily.GenericSansSerif, 8.5f, FontStyle.Bold) };
            lstServices = new ListBox { Location = new Point(10, 25), Size = new Size(270, 215), DrawMode = DrawMode.OwnerDrawFixed, ItemHeight = 22 };
            lstServices.DrawItem += LstServices_DrawItem;

            btnAddPreset = new Button { Text = LanguageManager.GetString("TranslationSettings.BtnAddService", "➕ Add New Service / AI Provider"), Location = new Point(10, 248), Size = new Size(270, 28), Font = new Font(FontFamily.GenericSansSerif, 8.5f, FontStyle.Bold), BackColor = Color.FromArgb(225, 240, 255) };
            btnDuplicate = new Button { Text = LanguageManager.GetString("TranslationSettings.BtnDuplicate", "📋 Duplicate Selected"), Location = new Point(10, 282), Size = new Size(132, 26), Font = new Font(FontFamily.GenericSansSerif, 8f, FontStyle.Bold) };
            btnRemove = new Button { Text = LanguageManager.GetString("TranslationSettings.BtnRemove", "❌ Remove"), Location = new Point(148, 282), Size = new Size(132, 26), ForeColor = Color.DarkRed, Font = new Font(FontFamily.GenericSansSerif, 8f) };
            btnMoveUp = new Button { Text = LanguageManager.GetString("TranslationSettings.BtnMoveUp", "▲ Move Up"), Location = new Point(10, 313), Size = new Size(132, 26), Font = new Font(FontFamily.GenericSansSerif, 8f) };
            btnMoveDown = new Button { Text = LanguageManager.GetString("TranslationSettings.BtnMoveDown", "▼ Move Down"), Location = new Point(148, 313), Size = new Size(132, 26), Font = new Font(FontFamily.GenericSansSerif, 8f) };

            pnlLeft.Controls.Add(lblListTitle);
            pnlLeft.Controls.Add(lstServices);
            pnlLeft.Controls.Add(btnAddPreset);
            pnlLeft.Controls.Add(btnDuplicate);
            pnlLeft.Controls.Add(btnRemove);
            pnlLeft.Controls.Add(btnMoveUp);
            pnlLeft.Controls.Add(btnMoveDown);

            var pnlRight = new GroupBox { Text = LanguageManager.GetString("TranslationSettings.PropertiesTitle", "Selected Service Properties"), Dock = DockStyle.Fill, Padding = new Padding(15) };
            int y = 25;
            Label AddField(string label, Control ctrl)
            {
                var lbl = new Label { Text = label, Location = new Point(15, y + 3), AutoSize = true };
                ctrl.Location = new Point(140, y);
                ctrl.Width = 350;
                pnlRight.Controls.Add(lbl);
                pnlRight.Controls.Add(ctrl);
                y += 35;
                return lbl;
            }

            txtDisplayName = new TextBox();
            cboProviderType = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
            cboProviderType.Items.AddRange(new object[] { "GoogleWeb", "OpenAICompatible", "DeepL" });
            txtApiKey = new TextBox { UseSystemPasswordChar = false };
            txtEndpoint = new TextBox();
            cboModel = new ComboBox { DropDownStyle = ComboBoxStyle.DropDown };
            txtUrlTemplate = new TextBox();

            chkIsEnabled = new CheckBox { Text = LanguageManager.GetString("TranslationSettings.ChkIsEnabled", "👁️ Active / Visible in Translation Dialogs & Menus"), AutoSize = true };
            chkIsEnabled.Location = new Point(140, y);
            pnlRight.Controls.Add(chkIsEnabled);
            y += 32;

            chkIsEnabled.CheckedChanged += (s, e) =>
            {
                if (_selectedConfig != null)
                {
                    _selectedConfig.IsEnabled = chkIsEnabled.Checked;
                    lstServices.Invalidate();
                }
            };

            AddField(LanguageManager.GetString("TranslationSettings.DisplayName", "Display Name:"), txtDisplayName);
            AddField(LanguageManager.GetString("TranslationSettings.ProviderType", "Provider Type:"), cboProviderType);
            AddField(LanguageManager.GetString("TranslationSettings.ApiKey", "API Key:"), txtApiKey);
            AddField(LanguageManager.GetString("TranslationSettings.EndpointUrl", "Endpoint URL:"), txtEndpoint);

            // Add Model Field with Fetch Button
            lblModelField = new Label { Text = LanguageManager.GetString("TranslationSettings.ModelName", "AI Model Name:"), Location = new Point(15, y + 3), AutoSize = true };
            cboModel.Location = new Point(140, y);
            cboModel.Width = 220;

            btnFetchModels = new Button
            {
                Text = LanguageManager.GetString("Translation.FetchModels", "🔄 Fetch Models"),
                Location = new Point(368, y - 1),
                Size = new Size(122, 26),
                Font = new Font(FontFamily.GenericSansSerif, 8f, FontStyle.Bold),
                BackColor = Color.FromArgb(235, 245, 255)
            };

            btnFetchModels.Click += async (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(txtEndpoint.Text))
                {
                    MessageBox.Show(
                        LanguageManager.GetString("Msg.EnterEndpointFirst", "Please enter an Endpoint URL first."),
                        LanguageManager.GetString("Title.FetchModels", "Fetch Models"),
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    return;
                }

                btnFetchModels.Enabled = false;
                btnFetchModels.Text = LanguageManager.GetString("TranslationSettings.Fetching", "⏳ Fetching...");

                try
                {
                    var models = await TranslationModelFetcher.FetchModelsAsync(txtEndpoint.Text, txtApiKey.Text);
                    if (models != null && models.Count > 0)
                    {
                        string currentText = cboModel.Text;
                        cboModel.Items.Clear();
                        foreach (var m in models) cboModel.Items.Add(m);
                        if (!string.IsNullOrWhiteSpace(currentText)) cboModel.Text = currentText;
                        else cboModel.SelectedIndex = 0;

                        MessageBox.Show(
                            string.Format(LanguageManager.GetString("Msg.ModelsDiscoveredFormat", "✅ Discovered {0} active models from server!"), models.Count),
                            LanguageManager.GetString("Title.ModelsDiscovered", "Models Discovered"),
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Information);
                    }
                    else
                    {
                        MessageBox.Show(
                            LanguageManager.GetString("Msg.NoModelsReturned", "No models were returned by the server /v1/models endpoint."),
                            LanguageManager.GetString("Title.FetchModels", "Fetch Models"),
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Warning);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show(
                        string.Format(LanguageManager.GetString("Msg.FetchModelsErrorFormat", "❌ Error fetching models from endpoint:\n\n{0}"), ex.Message),
                        LanguageManager.GetString("Title.FetchModelsError", "Fetch Models Error"),
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                }
                finally
                {
                    btnFetchModels.Enabled = true;
                    btnFetchModels.Text = LanguageManager.GetString("Translation.FetchModels", "🔄 Fetch Models");
                    UpdateTestButtonState();
                }
            };

            cboModel.TextChanged += (s, e) => UpdateTestButtonState();
            cboModel.SelectedIndexChanged += (s, e) => UpdateTestButtonState();

            pnlRight.Controls.Add(lblModelField);
            pnlRight.Controls.Add(cboModel);
            pnlRight.Controls.Add(btnFetchModels);
            y += 35;

            lblUrlTemplate = AddField("URL Template:", txtUrlTemplate);

            btnTestConnection = new Button
            {
                Text = LanguageManager.GetString("TranslationSettings.BtnTestConn", "🔍 Test / Validate Connection"),
                Location = new Point(140, y + 10),
                Size = new Size(220, 32),
                Font = new Font(FontFamily.GenericSansSerif, 8.5f, FontStyle.Bold),
                BackColor = Color.FromArgb(235, 245, 235),
                ForeColor = Color.DarkGreen
            };

            btnTestConnection.Click += async (s, e) =>
            {
                btnTestConnection.Enabled = false;
                btnTestConnection.Text = LanguageManager.GetString("TranslationSettings.Testing", "⏳ Testing...");

                var testConfig = new TranslationServiceConfig
                {
                    DisplayName = txtDisplayName.Text,
                    ProviderType = cboProviderType.SelectedItem?.ToString() ?? "OpenAICompatible",
                    ApiKey = txtApiKey.Text,
                    Endpoint = txtEndpoint.Text,
                    Model = cboModel.Text,
                    UrlTemplate = txtUrlTemplate.Text
                };

                try
                {
                    var provider = TranslationProviderFactory.CreateProvider(testConfig);
                    var testItems = new List<TranslationItem>
                    {
                        new TranslationItem { Key = "TestKey", SourceText = "Hello" }
                    };

                    using (var cts = new System.Threading.CancellationTokenSource(8000))
                    {
                        var res = await provider.TranslateBatchAsync(testItems, "en", "es", cts.Token);
                        if (res.Success && testItems.Count > 0 && !string.IsNullOrWhiteSpace(testItems[0].TranslatedText))
                        {
                            MessageBox.Show(
                                string.Format(LanguageManager.GetString("Msg.ValidationSuccessFormat", "✅ Connection and model validated successfully!\n\nTest result: 'Hello' -> '{0}'"), testItems[0].TranslatedText),
                                LanguageManager.GetString("Title.ValidationSuccessful", "Validation Successful"),
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Information);
                        }
                        else
                        {
                            string err = res.ErrorMessage ?? LanguageManager.GetString("Msg.NoResponseFromService", "No response received from service endpoint.");
                            MessageBox.Show(
                                string.Format(LanguageManager.GetString("Msg.ValidationFailedFormat", "❌ Connection validation failed:\n\n{0}"), err),
                                LanguageManager.GetString("Title.ConnectionError", "Connection Error"),
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Error);
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show(
                        string.Format(LanguageManager.GetString("Msg.NetworkConfigErrorFormat", "❌ Network or configuration error:\n\n{0}"), ex.Message),
                        LanguageManager.GetString("Title.ConnectionError", "Connection Error"),
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                }
                finally
                {
                    btnTestConnection.Enabled = true;
                    btnTestConnection.Text = LanguageManager.GetString("TranslationSettings.BtnTestConn", "🔍 Test / Validate Connection");
                }
            };

            pnlRight.Controls.Add(btnTestConnection);

            var pnlBottom = new Panel { Dock = DockStyle.Bottom, Height = 45, Padding = new Padding(10) };
            btnSave = new Button { Text = LanguageManager.GetString("Button.SaveSettings", "💾 Save Settings"), DialogResult = DialogResult.OK, Location = new Point(570, 8), Size = new Size(130, 30), Font = new Font(FontFamily.GenericSansSerif, 9, FontStyle.Bold), BackColor = Color.FromArgb(35, 130, 215), ForeColor = Color.White };
            btnCancel = new Button { Text = LanguageManager.GetString("Button.Cancel", "Cancel"), DialogResult = DialogResult.Cancel, Location = new Point(710, 8), Size = new Size(90, 30) };
            pnlBottom.Controls.Add(btnSave);
            pnlBottom.Controls.Add(btnCancel);

            this.Controls.Add(pnlRight);
            this.Controls.Add(pnlLeft);
            this.Controls.Add(pnlTop);
            this.Controls.Add(pnlBottom);

            lstServices.SelectedIndexChanged += LstServices_SelectedIndexChanged;
            btnMoveUp.Click += BtnMoveUp_Click;
            btnMoveDown.Click += BtnMoveDown_Click;
            btnDuplicate.Click += BtnDuplicate_Click;
            btnRemove.Click += BtnRemove_Click;
            btnAddPreset.Click += BtnAddPreset_Click;
            btnSave.Click += BtnSave_Click;
            cboProviderType.SelectedIndexChanged += (s, e) => UpdateProviderFieldVisibility();
            UpdateProviderFieldVisibility();
        }

        private void LoadData()
        {
            TranslationConfigManager.LoadConfig();
            txtSystemPrompt.Text = TranslationConfigManager.GlobalSettings.DefaultSystemPrompt;
            txtDefaultSourceLang.Text = TranslationConfigManager.GlobalSettings.DefaultSourceLanguage;
            numBatchSize.Value = TranslationConfigManager.GlobalSettings.BatchSize;
            numDelayMs.Value = TranslationConfigManager.GlobalSettings.DelayBetweenBatchesMs;

            RefreshServiceList();
            if (lstServices.Items.Count > 0)
            {
                int selectedIndex = 0;
                if (!string.IsNullOrWhiteSpace(_initialSectionName))
                {
                    for (int i = 0; i < TranslationConfigManager.ConfiguredServices.Count; i++)
                    {
                        if (string.Equals(TranslationConfigManager.ConfiguredServices[i].SectionName, _initialSectionName, StringComparison.OrdinalIgnoreCase))
                        {
                            selectedIndex = i;
                            break;
                        }
                    }
                }
                lstServices.SelectedIndex = selectedIndex;
            }
        }

        private void RefreshServiceList()
        {
            lstServices.Items.Clear();
            foreach (var s in TranslationConfigManager.ConfiguredServices)
            {
                lstServices.Items.Add(s);
            }
        }

        private void LstServices_SelectedIndexChanged(object sender, EventArgs e)
        {
            SaveCurrentFormToSelected();
            _selectedConfig = lstServices.SelectedItem as TranslationServiceConfig;
            if (_selectedConfig == null) return;

            txtDisplayName.Text = _selectedConfig.DisplayName;
            cboProviderType.SelectedItem = _selectedConfig.ProviderType;
            txtApiKey.Text = _selectedConfig.ApiKey;
            txtEndpoint.Text = _selectedConfig.Endpoint;
            cboModel.Text = _selectedConfig.Model ?? "";
            txtUrlTemplate.Text = _selectedConfig.UrlTemplate;
            chkIsEnabled.Checked = _selectedConfig.IsEnabled;
            UpdateProviderFieldVisibility();
        }

        private void UpdateProviderFieldVisibility()
        {
            string providerType = cboProviderType?.SelectedItem?.ToString() ?? string.Empty;
            bool isAi = providerType.Equals("OpenAICompatible", StringComparison.OrdinalIgnoreCase);
            bool isWeb = providerType.Equals("GoogleWeb", StringComparison.OrdinalIgnoreCase);

            if (lblModelField != null) lblModelField.Visible = isAi;
            if (cboModel != null) cboModel.Visible = isAi;
            if (btnFetchModels != null) btnFetchModels.Visible = isAi;
            if (lblUrlTemplate != null) lblUrlTemplate.Visible = isWeb;
            if (txtUrlTemplate != null) txtUrlTemplate.Visible = isWeb;

            UpdateTestButtonState();
        }

        private void UpdateTestButtonState()
        {
            if (btnTestConnection == null) return;

            string providerType = cboProviderType?.SelectedItem?.ToString() ?? string.Empty;
            bool isAi = providerType.Equals("OpenAICompatible", StringComparison.OrdinalIgnoreCase);

            if (isAi)
            {
                bool hasModel = !string.IsNullOrWhiteSpace(cboModel?.Text);
                btnTestConnection.Enabled = hasModel;
                if (cboProviderType.SelectedItem?.ToString() == "OpenAICompatible" && string.IsNullOrWhiteSpace(cboModel.Text))
                {
                    ToolTipHelper.SetToolTip(btnTestConnection, LanguageManager.GetString("ToolTip.TranslationSettings.FetchFirst", "Please click 'Fetch Models' to discover available models or select a model first before testing."));
                }
                else
                {
                    ToolTipHelper.SetToolTip(btnTestConnection, LanguageManager.GetString("ToolTip.TranslationSettings.TestModel", "Click to test and validate connection and model with the service provider."));
                }
            }
            else
            {
                btnTestConnection.Enabled = true;
                ToolTipHelper.SetToolTip(btnTestConnection, LanguageManager.GetString("ToolTip.TranslationSettings.TestConnection", "Click to test and validate connection with the service provider."));
            }
        }

        private void SaveCurrentFormToSelected()
        {
            if (_selectedConfig == null) return;
            _selectedConfig.DisplayName = txtDisplayName.Text;
            _selectedConfig.ProviderType = cboProviderType.SelectedItem?.ToString() ?? "OpenAICompatible";
            _selectedConfig.ApiKey = txtApiKey.Text;
            _selectedConfig.Endpoint = txtEndpoint.Text;
            _selectedConfig.Model = cboModel.Text;
            _selectedConfig.UrlTemplate = txtUrlTemplate.Text;
            _selectedConfig.IsEnabled = chkIsEnabled.Checked;
        }

        private void LstServices_DrawItem(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0 || e.Index >= lstServices.Items.Count) return;

            var config = lstServices.Items[e.Index] as TranslationServiceConfig;
            if (config == null) return;

            e.DrawBackground();

            bool isSelected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
            bool isEnabled = config.IsEnabled;

            Color textColor;
            if (isSelected)
            {
                textColor = e.ForeColor;
            }
            else if (!isEnabled)
            {
                textColor = Color.Gray;
            }
            else
            {
                textColor = Color.FromArgb(25, 25, 25);
            }

            string displayText = config.ToString();

            using (var brush = new SolidBrush(textColor))
            {
                e.Graphics.DrawString(displayText, e.Font, brush, e.Bounds.X + 4, e.Bounds.Y + 3);
            }

            e.DrawFocusRectangle();
        }

        private void BtnMoveUp_Click(object sender, EventArgs e)
        {
            int idx = lstServices.SelectedIndex;
            if (idx > 0)
            {
                var item = TranslationConfigManager.ConfiguredServices[idx];
                TranslationConfigManager.ConfiguredServices.RemoveAt(idx);
                TranslationConfigManager.ConfiguredServices.Insert(idx - 1, item);
                RefreshServiceList();
                lstServices.SelectedIndex = idx - 1;
            }
        }

        private void BtnMoveDown_Click(object sender, EventArgs e)
        {
            int idx = lstServices.SelectedIndex;
            if (idx >= 0 && idx < lstServices.Items.Count - 1)
            {
                var item = TranslationConfigManager.ConfiguredServices[idx];
                TranslationConfigManager.ConfiguredServices.RemoveAt(idx);
                TranslationConfigManager.ConfiguredServices.Insert(idx + 1, item);
                RefreshServiceList();
                lstServices.SelectedIndex = idx + 1;
            }
        }

        private void BtnRemove_Click(object sender, EventArgs e)
        {
            int idx = lstServices.SelectedIndex;
            if (idx >= 0)
            {
                TranslationConfigManager.ConfiguredServices.RemoveAt(idx);
                RefreshServiceList();
                if (lstServices.Items.Count > 0)
                {
                    lstServices.SelectedIndex = Math.Min(idx, lstServices.Items.Count - 1);
                }
            }
        }

        private void BtnDuplicate_Click(object sender, EventArgs e)
        {
            int idx = lstServices.SelectedIndex;
            if (idx >= 0 && idx < TranslationConfigManager.ConfiguredServices.Count)
            {
                SaveCurrentFormToSelected();
                var source = TranslationConfigManager.ConfiguredServices[idx];
                string defaultProvider = LanguageManager.GetString("TranslationSettings.DefaultProviderName", "Provider");
                string baseName = source.DisplayName ?? defaultProvider;
                string copyName = string.Format(LanguageManager.GetString("TranslationSettings.CopySuffixFormat", "{0} (Copy)"), baseName);

                var newConfig = new TranslationServiceConfig
                {
                    SectionName = "Provider_" + Guid.NewGuid().ToString("N").Substring(0, 8),
                    DisplayName = copyName,
                    ProviderType = source.ProviderType,
                    ApiKey = source.ApiKey,
                    Endpoint = source.Endpoint,
                    Model = source.Model,
                    UrlTemplate = source.UrlTemplate,
                    HttpMethod = source.HttpMethod,
                    UserAgent = source.UserAgent,
                    MaxTokens = source.MaxTokens,
                    Temperature = source.Temperature,
                    SystemPrompt = source.SystemPrompt,
                    ExtraParams = source.ExtraParams != null ? new Dictionary<string, string>(source.ExtraParams) : new Dictionary<string, string>()
                };

                TranslationConfigManager.ConfiguredServices.Insert(idx + 1, newConfig);
                RefreshServiceList();
                lstServices.SelectedIndex = idx + 1;
            }
        }

        private void BtnAddPreset_Click(object sender, EventArgs e)
        {
            var menu = new ContextMenuStrip();

            // --- CATEGORY 1: TRANSLATION SERVICES ---
            var itemTransHeader = new ToolStripMenuItem(LanguageManager.GetString("TranslationSettings.HeaderServices", "── 🌐 Translation Services ──")) { Enabled = false, Font = new Font(FontFamily.GenericSansSerif, 8.5f, FontStyle.Bold) };
            menu.Items.Add(itemTransHeader);

            menu.Items.Add("🌐 Google Translate (Web Free)", null, (s, ev) => AddPreset("Google Translate", "GoogleWeb", "", "", "", "https://translate.googleapis.com/translate_a/single?client=gtx&sl={src}&tl={tgt}&dt=t&q={text}"));
            menu.Items.Add("🌐 MyMemory Translate (Web Free)", null, (s, ev) => AddPreset("MyMemory Translate", "GoogleWeb", "", "", "", "https://api.mymemory.translated.net/get?q={text}&langpair={src}|{tgt}"));
            menu.Items.Add("🌐 Lingva Translate (Google Mirror Free)", null, (s, ev) => AddPreset("Lingva Translate", "GoogleWeb", "", "", "", "https://lingva.ml/api/v1/{src}/{tgt}/{text}"));
            menu.Items.Add("🌐 DeepL API (Free / Pro)", null, (s, ev) => AddPreset("DeepL API", "DeepL", "", "https://api-free.deepl.com/v2/translate", ""));
            menu.Items.Add("🌐 Microsoft Translator (Azure)", null, (s, ev) => AddPreset("Microsoft Translator", "MicrosoftTranslator", "", "https://api.cognitive.microsofttranslator.com/translate?api-version=3.0", ""));
            menu.Items.Add("🌐 LibreTranslate (Open Source Server)", null, (s, ev) => AddPreset("LibreTranslate", "OpenAICompatible", "", "https://libretranslate.com/translate", ""));

            menu.Items.Add(new ToolStripSeparator());

            // --- CATEGORY 2: AI / LLM MODEL PROVIDERS ---
            var itemAiHeader = new ToolStripMenuItem(LanguageManager.GetString("TranslationSettings.HeaderAiProviders", "── 🤖 AI Model Providers (LLM) ──")) { Enabled = false, Font = new Font(FontFamily.GenericSansSerif, 8.5f, FontStyle.Bold) };
            menu.Items.Add(itemAiHeader);

            menu.Items.Add("[AI] OpenCode Go (High-Speed)", null, (s, ev) => AddPreset("[AI] OpenCode Go", "OpenAICompatible", "", "https://opencode.ai/zen/go/v1/chat/completions", ""));
            menu.Items.Add("[AI] OpenCode Zen (Advanced)", null, (s, ev) => AddPreset("[AI] OpenCode Zen", "OpenAICompatible", "", "https://opencode.ai/zen/v1/chat/completions", ""));
            menu.Items.Add("[AI] DeepSeek API", null, (s, ev) => AddPreset("[AI] DeepSeek API", "OpenAICompatible", "", "https://api.deepseek.com/v1/chat/completions", ""));
            menu.Items.Add("[AI] OpenAI API", null, (s, ev) => AddPreset("[AI] OpenAI API", "OpenAICompatible", "", "https://api.openai.com/v1/chat/completions", ""));
            menu.Items.Add("[AI] Anthropic Claude API", null, (s, ev) => AddPreset("[AI] Anthropic API", "OpenAICompatible", "", "https://api.anthropic.com/v1/messages", ""));
            menu.Items.Add("[AI] Google Gemini API", null, (s, ev) => AddPreset("[AI] Google Gemini API", "OpenAICompatible", "", "https://generativelanguage.googleapis.com/v1beta/openai/chat/completions", ""));
            menu.Items.Add("[AI] Groq Cloud API", null, (s, ev) => AddPreset("[AI] Groq Cloud API", "OpenAICompatible", "", "https://api.groq.com/openai/v1/chat/completions", ""));
            menu.Items.Add("[AI] OpenRouter API", null, (s, ev) => AddPreset("[AI] OpenRouter API", "OpenAICompatible", "", "https://openrouter.ai/api/v1/chat/completions", ""));
            menu.Items.Add("[AI] Mistral AI API", null, (s, ev) => AddPreset("[AI] Mistral AI API", "OpenAICompatible", "", "https://api.mistral.ai/v1/chat/completions", ""));
            menu.Items.Add("[AI] Together AI API", null, (s, ev) => AddPreset("[AI] Together AI API", "OpenAICompatible", "", "https://api.together.xyz/v1/chat/completions", ""));

            menu.Items.Add(new ToolStripSeparator());

            // --- CATEGORY 3: LOCAL AI & CUSTOM ENDPOINTS ---
            var itemLocalHeader = new ToolStripMenuItem(LanguageManager.GetString("TranslationSettings.HeaderLocalAi", "── 🖥️ Local AI & Custom Endpoints ──")) { Enabled = false, Font = new Font(FontFamily.GenericSansSerif, 8.5f, FontStyle.Bold) };
            menu.Items.Add(itemLocalHeader);

            menu.Items.Add("[AI] Ollama Local (http://localhost:11434)", null, (s, ev) => AddPreset("[AI] Ollama Local", "OpenAICompatible", "", "http://localhost:11434/v1/chat/completions", ""));
            menu.Items.Add("[AI] LM Studio Local (http://localhost:1234)", null, (s, ev) => AddPreset("[AI] LM Studio Local", "OpenAICompatible", "", "http://localhost:1234/v1/chat/completions", ""));
            menu.Items.Add("[AI] Custom OpenAI-Compatible Endpoint...", null, (s, ev) => AddPreset("[AI] Custom AI Provider", "OpenAICompatible", "", "http://localhost:8000/v1/chat/completions", ""));

            menu.Show(btnAddPreset, new Point(0, btnAddPreset.Height));
        }

        private void AddPreset(string name, string type, string apiKey, string endpoint, string model, string urlTemplate = "")
        {
            var newConfig = new TranslationServiceConfig
            {
                SectionName = "Provider_" + Guid.NewGuid().ToString("N").Substring(0, 8),
                DisplayName = name,
                ProviderType = type,
                ApiKey = apiKey,
                Endpoint = endpoint,
                Model = model ?? string.Empty,
                UrlTemplate = urlTemplate
            };
            TranslationConfigManager.ConfiguredServices.Add(newConfig);
            RefreshServiceList();
            lstServices.SelectedIndex = lstServices.Items.Count - 1;
        }

        private void BtnSave_Click(object sender, EventArgs e)
        {
            SaveCurrentFormToSelected();
            TranslationConfigManager.GlobalSettings.DefaultSystemPrompt = txtSystemPrompt.Text;
            TranslationConfigManager.GlobalSettings.DefaultSourceLanguage = txtDefaultSourceLang.Text;
            TranslationConfigManager.GlobalSettings.BatchSize = (int)numBatchSize.Value;
            TranslationConfigManager.GlobalSettings.DelayBetweenBatchesMs = (int)numDelayMs.Value;

            TranslationConfigManager.SaveConfig();
        }
    }
}
