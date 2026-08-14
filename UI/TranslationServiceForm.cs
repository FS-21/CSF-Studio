using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using CsfStudio.Core;
using CsfStudio.Core.Translation;

namespace CsfStudio.UI
{
    public class TranslationServiceForm : Form
    {
        private CsfSession _session;
        private TranslationServiceConfig _serviceConfig;
        private List<string> _selectedKeysFilter;
        private CancellationTokenSource _cts;

        private ComboBox cboSourceCsf;
        private ComboBox cboSourceLang;
        private ComboBox cboTargetCsf;
        private ComboBox cboTargetLang;
        private Label lblModel;
        private ComboBox cboModelOverride;
        private Button btnFetchModels;
        private Button btnTranslateAll;
        private Button btnTranslateExistingOnly;
        private Button btnClose;

        private ProgressBar progressBar;
        private Label lblStatus;
        public event Action<List<string>> TranslationCompleted;
        public bool AddedMissingKeys { get; private set; }
        public BatchUndoCommand TranslationUndoBatch { get; private set; }

        public TranslationServiceForm(CsfSession session, TranslationServiceConfig serviceConfig, List<string> selectedKeysFilter = null)
        {
            _session = session;
            _serviceConfig = serviceConfig;
            _selectedKeysFilter = selectedKeysFilter;

            InitializeComponent();
            PopulateData();
            TryAutoFetchModels();
        }

        private void InitializeComponent()
        {
            bool hasSelection = _selectedKeysFilter != null && _selectedKeysFilter.Count > 0;
            int selCount = hasSelection ? _selectedKeysFilter.Count : 0;

            bool isAiModel = _serviceConfig != null &&
                             _serviceConfig.ProviderType != null &&
                             _serviceConfig.ProviderType.Equals("OpenAICompatible", StringComparison.OrdinalIgnoreCase);

            string serviceDisplayName = _serviceConfig != null && _serviceConfig.IsAiModel && !_serviceConfig.DisplayName.StartsWith("[AI]", StringComparison.OrdinalIgnoreCase)
                ? "[AI] " + _serviceConfig.DisplayName
                : (_serviceConfig != null ? _serviceConfig.DisplayName : "Translation Service");

            this.Text = hasSelection
                ? $"🎯 Translate Selection ({selCount} keys) — {serviceDisplayName}"
                : $"🌐 Translate Document — {serviceDisplayName}";

            this.ClientSize = new Size(650, isAiModel ? 475 : 435);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.StartPosition = FormStartPosition.CenterParent;
            this.ShowIcon = false;

            // --- HEADER BANNER ---
            var pnlHeader = new Panel
            {
                Dock = DockStyle.Top,
                Height = 65,
                BackColor = hasSelection ? Color.FromArgb(235, 243, 255) : Color.FromArgb(243, 244, 246),
                Padding = new Padding(15, 10, 15, 10)
            };

            var lblHeaderTitle = new Label
            {
                Text = hasSelection ? "🎯 Selected Entries Translation" : "🌐 Full Document Translation",
                Font = new Font(FontFamily.GenericSansSerif, 10f, FontStyle.Bold),
                ForeColor = hasSelection ? Color.FromArgb(30, 64, 175) : Color.FromArgb(31, 41, 55),
                Location = new Point(15, 10),
                AutoSize = true
            };

            var lblHeaderSubtitle = new Label
            {
                Text = hasSelection
                    ? $"Translating only the {selCount} selected entries from the active table into the target CSF file."
                    : "Translating all string entries in the source CSF file into the target CSF file.",
                Font = new Font(FontFamily.GenericSansSerif, 8.5f, FontStyle.Regular),
                ForeColor = Color.FromArgb(75, 85, 99),
                Location = new Point(15, 34),
                AutoSize = true
            };

            var lblScopeBadge = new Label
            {
                Text = hasSelection ? $"Scope: {selCount} Selected Entries" : "Scope: All Document Keys",
                Font = new Font(FontFamily.GenericSansSerif, 8.5f, FontStyle.Bold),
                ForeColor = hasSelection ? Color.White : Color.FromArgb(31, 41, 55),
                BackColor = hasSelection ? Color.FromArgb(37, 99, 235) : Color.FromArgb(229, 231, 235),
                Padding = new Padding(8, 3, 8, 3),
                AutoSize = true,
                Location = new Point(415, 12)
            };

            pnlHeader.Controls.Add(lblHeaderTitle);
            pnlHeader.Controls.Add(lblHeaderSubtitle);
            pnlHeader.Controls.Add(lblScopeBadge);

            // --- FOOTER PANEL ---
            var pnlFooter = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 46,
                BackColor = Color.FromArgb(248, 249, 250),
                Padding = new Padding(15, 8, 15, 8)
            };

            progressBar = new ProgressBar { Location = new Point(15, 13), Size = new Size(500, 20), Visible = false };
            lblStatus = new Label { Text = LanguageManager.GetString("Translation.StatusReady", "Ready to translate."), Location = new Point(15, 14), Size = new Size(500, 22), ForeColor = Color.DimGray, Font = new Font(FontFamily.GenericSansSerif, 8.5f, FontStyle.Regular) };
            btnClose = new Button { Text = LanguageManager.GetString("Button.Close", "Close"), Location = new Point(535, 8), Size = new Size(95, 30), DialogResult = DialogResult.Cancel, Font = new Font(FontFamily.GenericSansSerif, 8.5f, FontStyle.Regular) };

            pnlFooter.Controls.Add(progressBar);
            pnlFooter.Controls.Add(lblStatus);
            pnlFooter.Controls.Add(btnClose);

            // --- MAIN PANEL ---
            var pnlMain = new Panel { Dock = DockStyle.Fill, Padding = new Padding(20) };

            int yOffset = 15;

            var lblSource = new Label { Text = LanguageManager.GetString("Translation.SourceCsf", "📄 Source CSF Document:"), Location = new Point(20, yOffset + 3), AutoSize = true, Font = new Font(FontFamily.GenericSansSerif, 8.5f, FontStyle.Bold) };
            cboSourceCsf = new ComboBox { Location = new Point(240, yOffset), Size = new Size(380, 23), DropDownStyle = ComboBoxStyle.DropDownList };

            yOffset += 36;
            var lblSourceLang = new Label { Text = LanguageManager.GetString("Translation.SourceLang", "🌐 Source Language:"), Location = new Point(20, yOffset + 3), AutoSize = true, Font = new Font(FontFamily.GenericSansSerif, 8.5f, FontStyle.Bold) };
            cboSourceLang = new ComboBox { Location = new Point(240, yOffset), Size = new Size(380, 23), DropDownStyle = ComboBoxStyle.DropDown };

            var srcLangs = new List<object> { LanguageManager.GetString("LangName.AutoDetect", "Auto Detect [auto]") };
            srcLangs.AddRange(CsfStudio.Core.Translation.TranslationLanguageHelper.GetLanguageOptions().Cast<object>());
            cboSourceLang.Items.AddRange(srcLangs.ToArray());

            yOffset += 36;
            var lblTargetFile = new Label { Text = LanguageManager.GetString("Translation.TargetCsf", "🎯 Target CSF Document:"), Location = new Point(20, yOffset + 3), AutoSize = true, Font = new Font(FontFamily.GenericSansSerif, 8.5f, FontStyle.Bold) };
            cboTargetCsf = new ComboBox { Location = new Point(240, yOffset), Size = new Size(380, 23), DropDownStyle = ComboBoxStyle.DropDownList };

            yOffset += 36;
            var lblTargetLang = new Label { Text = LanguageManager.GetString("Translation.TargetLang", "🌐 Target Language:"), Location = new Point(20, yOffset + 3), AutoSize = true, Font = new Font(FontFamily.GenericSansSerif, 8.5f, FontStyle.Bold) };
            cboTargetLang = new ComboBox { Location = new Point(240, yOffset), Size = new Size(380, 23), DropDownStyle = ComboBoxStyle.DropDown };

            cboTargetLang.Items.AddRange(CsfStudio.Core.Translation.TranslationLanguageHelper.GetLanguageOptions().Cast<object>().ToArray());

            yOffset += 36;
            lblModel = new Label { Text = LanguageManager.GetString("Translation.AiModel", "🤖 AI Model Selection:"), Location = new Point(20, yOffset + 3), AutoSize = true, Font = new Font(FontFamily.GenericSansSerif, 8.5f, FontStyle.Bold), Visible = isAiModel };
            cboModelOverride = new ComboBox { Location = new Point(240, yOffset), Size = new Size(250, 23), DropDownStyle = ComboBoxStyle.DropDown, Text = _serviceConfig?.Model ?? "", Visible = isAiModel };

            btnFetchModels = new Button
            {
                Text = LanguageManager.GetString("Translation.FetchModels", "🔄 Fetch Models"),
                Location = new Point(500, yOffset - 1),
                Size = new Size(120, 25),
                Font = new Font(FontFamily.GenericSansSerif, 8f, FontStyle.Bold),
                BackColor = Color.FromArgb(235, 245, 255),
                Visible = isAiModel
            };
            btnFetchModels.Click += async (s, e) => await FetchServerModelsAsync(showSuccessMsg: true);

            int btnY1 = isAiModel ? yOffset + 40 : yOffset + 4;
            int btnY2 = btnY1 + 42;

            string btnAllText = hasSelection
                ? string.Format(LanguageManager.GetString("Translation.TranslateSelectionAll", "⚡ Translate {0} Selected Keys (Create in Target if missing)"), selCount)
                : LanguageManager.GetString("Translation.TranslateAllKeys", "⚡ Translate ALL keys from Source CSF");

            string btnExistText = hasSelection
                ? string.Format(LanguageManager.GetString("Translation.TranslateSelectionExisting", "✏️ Translate {0} Selected Keys (Only if existing in Target)"), selCount)
                : LanguageManager.GetString("Translation.TranslateExistingKeys", "✏️ Translate ONLY EXISTING keys in Target CSF");

            btnTranslateAll = new Button
            {
                Text = btnAllText,
                Location = new Point(20, btnY1),
                Size = new Size(600, 36),
                Font = new Font(FontFamily.GenericSansSerif, 9f, FontStyle.Bold),
                BackColor = Color.FromArgb(230, 242, 255),
                ForeColor = Color.FromArgb(30, 64, 175)
            };

            btnTranslateExistingOnly = new Button
            {
                Text = btnExistText,
                Location = new Point(20, btnY2),
                Size = new Size(600, 36),
                Font = new Font(FontFamily.GenericSansSerif, 9f, FontStyle.Bold),
                BackColor = Color.FromArgb(245, 247, 250),
                ForeColor = Color.FromArgb(55, 65, 81)
            };

            pnlMain.Controls.Add(lblSource);
            pnlMain.Controls.Add(cboSourceCsf);
            pnlMain.Controls.Add(lblSourceLang);
            pnlMain.Controls.Add(cboSourceLang);
            pnlMain.Controls.Add(lblTargetFile);
            pnlMain.Controls.Add(cboTargetCsf);
            pnlMain.Controls.Add(lblTargetLang);
            pnlMain.Controls.Add(cboTargetLang);
            pnlMain.Controls.Add(lblModel);
            pnlMain.Controls.Add(cboModelOverride);
            pnlMain.Controls.Add(btnFetchModels);
            pnlMain.Controls.Add(btnTranslateAll);
            pnlMain.Controls.Add(btnTranslateExistingOnly);

            this.Controls.Add(pnlMain);
            this.Controls.Add(pnlFooter);
            this.Controls.Add(pnlHeader);

            cboSourceCsf.SelectedIndexChanged += CboSourceCsf_SelectedIndexChanged;
            cboTargetCsf.SelectedIndexChanged += CboTargetCsf_SelectedIndexChanged;
            btnTranslateAll.Click += async (s, e) => await StartTranslationAsync(translateAll: true);
            btnTranslateExistingOnly.Click += async (s, e) => await StartTranslationAsync(translateAll: false);
        }

        private void PopulateData()
        {
            cboSourceCsf.Items.Clear();
            cboTargetCsf.Items.Clear();

            if (_session != null && _session.Documents.Count > 0)
            {
                foreach (var doc in _session.Documents)
                {
                    cboSourceCsf.Items.Add(doc);
                    cboTargetCsf.Items.Add(doc);
                }

                cboSourceCsf.SelectedIndex = 0;
                if (cboTargetCsf.Items.Count > 1) cboTargetCsf.SelectedIndex = 1;
                else if (cboTargetCsf.Items.Count > 0) cboTargetCsf.SelectedIndex = 0;
            }

            CboSourceCsf_SelectedIndexChanged(null, null);
            CboTargetCsf_SelectedIndexChanged(null, null);

            if (_serviceConfig != null && !string.IsNullOrWhiteSpace(_serviceConfig.Model))
            {
                if (!cboModelOverride.Items.Contains(_serviceConfig.Model))
                {
                    cboModelOverride.Items.Add(_serviceConfig.Model);
                }
                cboModelOverride.Text = _serviceConfig.Model;
            }
        }

        private async void TryAutoFetchModels()
        {
            if (_serviceConfig != null &&
                !string.IsNullOrWhiteSpace(_serviceConfig.Endpoint) &&
                _serviceConfig.ProviderType != null &&
                _serviceConfig.ProviderType.Equals("OpenAICompatible", StringComparison.OrdinalIgnoreCase))
            {
                await FetchServerModelsAsync(showSuccessMsg: false);
            }
        }

        private async Task FetchServerModelsAsync(bool showSuccessMsg)
        {
            if (_serviceConfig == null || string.IsNullOrWhiteSpace(_serviceConfig.Endpoint)) return;

            btnFetchModels.Enabled = false;
            btnFetchModels.Text = LanguageManager.GetString("TranslationService.FetchingModels", "⏳ Fetching...");

            try
            {
                var models = await TranslationModelFetcher.FetchModelsAsync(_serviceConfig.Endpoint, _serviceConfig.ApiKey);
                if (models != null && models.Count > 0)
                {
                    string currentText = cboModelOverride.Text;
                    cboModelOverride.Items.Clear();
                    foreach (var m in models) cboModelOverride.Items.Add(m);

                    if (!string.IsNullOrWhiteSpace(currentText) && cboModelOverride.Items.Contains(currentText))
                    {
                        cboModelOverride.Text = currentText;
                    }
                    else if (cboModelOverride.Items.Count > 0)
                    {
                        cboModelOverride.SelectedIndex = 0;
                    }

                    if (showSuccessMsg)
                    {
                        MessageBox.Show(
                            string.Format(LanguageManager.GetString("Msg.DiscoveredModelsSuccessFormat", "✅ Discovered {0} active models from provider server!"), models.Count),
                            LanguageManager.GetString("Title.ModelsDiscovered", "Models Discovered"),
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Information);
                    }
                }
                else if (showSuccessMsg)
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
                if (showSuccessMsg)
                {
                    MessageBox.Show(
                        string.Format(LanguageManager.GetString("Msg.ErrorFetchingModelsFormat", "❌ Error fetching models from provider endpoint:\n\n{0}"), ex.Message),
                        LanguageManager.GetString("Title.FetchModelsError", "Fetch Models Error"),
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                }
            }
            finally
            {
                btnFetchModels.Enabled = true;
                btnFetchModels.Text = LanguageManager.GetString("Translation.FetchModels", "🔄 Fetch Models");
            }
        }

        private void CboSourceCsf_SelectedIndexChanged(object sender, EventArgs e)
        {
            var selectedDoc = cboSourceCsf.SelectedItem as CsfSessionDocument;
            if (selectedDoc != null)
            {
                string isoCode = TranslationLanguageHelper.Normalize(selectedDoc.TranslationContentLanguage);
                if (string.IsNullOrEmpty(isoCode))
                {
                    isoCode = TranslationLanguageHelper.GetIsoCode(selectedDoc.Document?.Language);
                }
                if (string.IsNullOrEmpty(isoCode))
                {
                    isoCode = TranslationLanguageHelper.Normalize(selectedDoc.LanguageTag);
                }
                if (string.IsNullOrEmpty(isoCode))
                {
                    isoCode = TranslationLanguageHelper.GetDefaultSourceLanguage();
                }

                string languageItem = cboSourceLang.Items.Cast<object>()
                    .Select(item => item.ToString())
                    .FirstOrDefault(item => item.IndexOf($"[{isoCode}]", StringComparison.OrdinalIgnoreCase) >= 0);

                if (string.IsNullOrEmpty(languageItem))
                {
                    languageItem = TranslationLanguageHelper.GetDisplayName(isoCode);
                }

                cboSourceLang.Text = !string.IsNullOrEmpty(languageItem) ? languageItem : isoCode;
            }
        }

        private void CboTargetCsf_SelectedIndexChanged(object sender, EventArgs e)
        {
            var selectedDoc = cboTargetCsf.SelectedItem as CsfSessionDocument;
            if (selectedDoc != null)
            {
                string isoCode = TranslationLanguageHelper.Normalize(selectedDoc.TranslationContentLanguage);
                if (string.IsNullOrEmpty(isoCode))
                {
                    isoCode = TranslationLanguageHelper.GetIsoCode(selectedDoc.Document?.Language);
                }
                if (string.IsNullOrEmpty(isoCode))
                {
                    isoCode = TranslationLanguageHelper.Normalize(selectedDoc.LanguageTag);
                }
                if (string.IsNullOrEmpty(isoCode))
                {
                    isoCode = TranslationLanguageHelper.GetDefaultSourceLanguage();
                }

                string languageItem = cboTargetLang.Items.Cast<object>()
                    .Select(item => item.ToString())
                    .FirstOrDefault(item => item.IndexOf($"[{isoCode}]", StringComparison.OrdinalIgnoreCase) >= 0);

                if (string.IsNullOrEmpty(languageItem))
                {
                    languageItem = TranslationLanguageHelper.GetDisplayName(isoCode);
                }

                cboTargetLang.Text = !string.IsNullOrEmpty(languageItem) ? languageItem : isoCode;
            }
        }

        private async Task StartTranslationAsync(bool translateAll)
        {
            var sourceDoc = cboSourceCsf.SelectedItem as CsfSessionDocument;
            var targetDoc = cboTargetCsf.SelectedItem as CsfSessionDocument;
            if (sourceDoc == null || targetDoc == null || _session == null || _session.Documents.Count == 0)
            {
                MessageBox.Show(
                    LanguageManager.GetString("Msg.SelectValidSourceTargetDocs", "Please select valid source and target CSF documents."),
                    LanguageManager.GetString("Title.Translation", "Translation"),
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            string enteredSourceLang = TranslationLanguageHelper.Normalize(cboSourceLang.Text);
            string sourceLanguage;
            if (!string.IsNullOrEmpty(enteredSourceLang))
            {
                sourceLanguage = enteredSourceLang;
                if (enteredSourceLang != "auto" && TranslationLanguageHelper.GetIsoCode(sourceDoc.Document?.Language) == string.Empty)
                {
                    sourceDoc.TranslationContentLanguage = enteredSourceLang;
                }
            }
            else
            {
                sourceLanguage = EnsureTranslationLanguage(sourceDoc);
                if (string.IsNullOrEmpty(sourceLanguage)) return;
            }

            string targetLanguage;
            if (TranslationLanguageHelper.GetIsoCode(targetDoc.Document?.Language) == string.Empty)
            {
                string enteredTargetLanguage = TranslationLanguageHelper.Normalize(cboTargetLang.Text);
                if (!string.IsNullOrEmpty(enteredTargetLanguage) && enteredTargetLanguage != "auto")
                {
                    targetDoc.TranslationContentLanguage = enteredTargetLanguage;
                }
                targetLanguage = EnsureTranslationLanguage(targetDoc);
                if (string.IsNullOrEmpty(targetLanguage)) return;
            }
            else
            {
                targetLanguage = TranslationLanguageHelper.Normalize(cboTargetLang.Text);
                if (string.IsNullOrEmpty(targetLanguage))
                    targetLanguage = TranslationLanguageHelper.GetIsoCode(targetDoc.Document?.Language);
            }

            if (cboModelOverride.Visible && !string.IsNullOrWhiteSpace(cboModelOverride.Text))
            {
                _serviceConfig.Model = cboModelOverride.Text.Trim();
            }

            bool hasSelection = _selectedKeysFilter != null && _selectedKeysFilter.Count > 0;
            int selCount = hasSelection ? _selectedKeysFilter.Count : 0;

            string scopeDescription = hasSelection
                ? string.Format(LanguageManager.GetString("Translation.ScopeSelectedOnlyFormat", "{0} Selected Entries Only"), selCount)
                : (translateAll ? LanguageManager.GetString("Translation.ScopeAllKeys", "ALL keys from selected Source CSF") : LanguageManager.GetString("Translation.ScopeExistingOnly", "ONLY EXISTING keys in Target CSF"));

            string actionDescription = translateAll
                ? (hasSelection ? string.Format(LanguageManager.GetString("Translation.ActionTranslateSelectionAllFormat", "Translate {0} selected keys (Add to Target if missing)"), selCount) : LanguageManager.GetString("Translation.ScopeAllKeys", "ALL keys from selected Source CSF"))
                : (hasSelection ? string.Format(LanguageManager.GetString("Translation.ActionTranslateSelectionExistingFormat", "Translate {0} selected keys (Only if existing in Target)"), selCount) : LanguageManager.GetString("Translation.ScopeExistingOnly", "ONLY EXISTING keys in Target CSF"));

            string modelInfo = cboModelOverride.Visible ? $"· Model Selected: {_serviceConfig.Model}\n" : "";
            string confirmMsg = string.Format(
                LanguageManager.GetString("Msg.ConfirmTranslationFormat", "Do you want to proceed with translation using '{0}'?\n\n· Source CSF: {1}\n· Target CSF: {2}\n· Scope: {3}\n· Action: {4}\n· Source Language: {5}\n· Target Language: {6}\n{7}"),
                _serviceConfig.DisplayName, sourceDoc.FileName, targetDoc.FileName, scopeDescription, actionDescription, sourceLanguage, targetLanguage, modelInfo);

            if (MessageBox.Show(
                confirmMsg,
                LanguageManager.GetString("Title.ConfirmTranslation", "Confirm Translation"),
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question) != DialogResult.Yes)
            {
                return;
            }

            btnTranslateAll.Enabled = false;
            btnTranslateExistingOnly.Enabled = false;
            progressBar.Visible = true;
            progressBar.Value = 0;
            lblStatus.Text = LanguageManager.GetString("Translation.PreparingKeys", "Preparing keys for translation...");

            _cts = new CancellationTokenSource();
            var provider = TranslationProviderFactory.CreateProvider(_serviceConfig);

            var itemsToTranslate = new List<TranslationItem>();

            foreach (var label in sourceDoc.Document.Labels)
            {
                if (_selectedKeysFilter != null && _selectedKeysFilter.Count > 0 && !_selectedKeysFilter.Contains(label.Name))
                {
                    continue;
                }

                string sourceText = label.Strings.FirstOrDefault()?.Value ?? "";
                if (string.IsNullOrWhiteSpace(sourceText)) continue;

                var existingTargetLabel = targetDoc.Document.Labels.FirstOrDefault(l => l.Name.Equals(label.Name, StringComparison.OrdinalIgnoreCase));

                if (!translateAll && existingTargetLabel == null)
                {
                    continue;
                }

                itemsToTranslate.Add(new TranslationItem
                {
                    Key = label.Name,
                    SourceText = sourceText
                });
            }

            if (itemsToTranslate.Count == 0)
            {
                MessageBox.Show(
                    LanguageManager.GetString("Msg.NoEligibleKeysToTranslate", "No eligible keys found for translation in the selected scope."),
                    LanguageManager.GetString("Title.Translation", "Translation"),
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                btnTranslateAll.Enabled = true;
                btnTranslateExistingOnly.Enabled = true;
                progressBar.Visible = false;
                return;
            }

            try
            {
                var result = await TranslationBatcher.ExecuteBatchTranslationAsync(
                    provider,
                    itemsToTranslate,
                    sourceLanguage,
                    targetLanguage,
                    (processed, total) =>
                    {
                        this.BeginInvoke((Action)(() =>
                        {
                            progressBar.Value = (int)((double)processed / total * 100);
                            lblStatus.Text = string.Format(LanguageManager.GetString("Translation.ProgressFormat", "Translating... ({0} / {1} keys)"), processed, total);
                        }));
                    },
                    _cts.Token
                );

                if (result.Success)
                {
                    int translatedCount = 0;
                    var updatedKeys = new List<string>();
                    var batchUndo = new BatchUndoCommand(string.Format(
                        LanguageManager.GetString("Undo.TranslateKeys", "Translate {0} keys ({1}->{2})"),
                        itemsToTranslate.Count,
                        sourceLanguage,
                        targetLanguage));

                    foreach (var item in itemsToTranslate)
                    {
                        if (!string.IsNullOrWhiteSpace(item.TranslatedText))
                        {
                            var targetLabel = targetDoc.Document.Labels.FirstOrDefault(l => l.Name.Equals(item.Key, StringComparison.OrdinalIgnoreCase));
                            string oldVal = "";
                            string oldExtra = null;

                            if (targetLabel == null)
                            {
                                targetLabel = new CsfLabel(item.Key);
                                targetDoc.Document.Labels.Add(targetLabel);
                                AddedMissingKeys = true;
                            }
                            else if (targetLabel.Strings.Count > 0)
                            {
                                oldVal = targetLabel.Strings[0].Value ?? "";
                                oldExtra = targetLabel.Strings[0].ExtraValue;
                            }

                            if (targetLabel.Strings.Count > 0)
                            {
                                targetLabel.Strings[0].Value = item.TranslatedText;
                            }
                            else
                            {
                                targetLabel.Strings.Add(new CsfStringEntry(item.TranslatedText));
                            }

                            targetDoc.IsModified = true;
                            translatedCount++;
                            updatedKeys.Add(item.Key);

                            batchUndo.AddCommand(new EditValueCommand(targetDoc.LanguageTag, item.Key, oldVal, item.TranslatedText, oldExtra, oldExtra));
                        }
                    }

                    if (batchUndo.Commands.Count > 0)
                    {
                        TranslationUndoBatch = batchUndo;
                    }

                    if (updatedKeys.Count > 0)
                    {
                        TranslationCompleted?.Invoke(updatedKeys);
                    }

                    lblStatus.Text = string.Format(LanguageManager.GetString("Translation.CompletedStatusFormat", "✅ Translation completed successfully! ({0} keys updated)"), translatedCount);
                    lblStatus.ForeColor = Color.DarkGreen;
                    MessageBox.Show(
                        string.Format(LanguageManager.GetString("Msg.TranslationSuccessFormat", "✅ Translation completed successfully!\n\nUpdated: {0} keys in {1}"), translatedCount, targetDoc.FileName),
                        LanguageManager.GetString("Title.TranslationFinished", "Translation Finished"),
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                }
                else
                {
                    lblStatus.Text = string.Format(LanguageManager.GetString("Translation.StoppedStatusFormat", "❌ Translation stopped: {0}"), result.ErrorMessage);
                    lblStatus.ForeColor = Color.Red;
                    MessageBox.Show(
                        string.Format(LanguageManager.GetString("Msg.TranslationStoppedFormat", "❌ Translation stopped:\n\n{0}"), result.ErrorMessage),
                        LanguageManager.GetString("Title.TranslationError", "Translation Error"),
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                }
            }
            catch (OperationCanceledException)
            {
                lblStatus.Text = LanguageManager.GetString("Translation.CancelledStatus", "Translation canceled by user.");
                lblStatus.ForeColor = Color.OrangeRed;
            }
            catch (Exception ex)
            {
                lblStatus.Text = LanguageManager.GetString("Translation.ErrorStatusPrefix", "Error during translation: ") + ex.Message;
                lblStatus.ForeColor = Color.Red;
                MessageBox.Show(
                    string.Format(LanguageManager.GetString("Msg.TranslationErrorFormat", "Translation Error: {0}"), ex.Message),
                    LanguageManager.GetString("Title.Error", "Error"),
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
            finally
            {
                btnTranslateAll.Enabled = true;
                btnTranslateExistingOnly.Enabled = true;
                progressBar.Visible = false;
            }
        }

        private string EnsureTranslationLanguage(CsfSessionDocument doc)
        {
            if (doc == null) return string.Empty;

            string currentLanguage = TranslationLanguageHelper.Normalize(doc.TranslationContentLanguage);
            if (!string.IsNullOrEmpty(currentLanguage) && currentLanguage != "auto") return currentLanguage;

            string headerLanguage = TranslationLanguageHelper.GetIsoCode(doc.Document?.Language);
            if (!string.IsNullOrEmpty(headerLanguage)) return headerLanguage;

            using (var dlg = new NeutralLanguageDialog(doc.FileName, currentLanguage))
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return string.Empty;
                doc.TranslationContentLanguage = dlg.SelectedLanguage;
                return doc.TranslationContentLanguage;
            }
        }
    }
}
