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
            lblStatus = new Label { Text = "Ready to translate.", Location = new Point(15, 14), Size = new Size(500, 22), ForeColor = Color.DimGray, Font = new Font(FontFamily.GenericSansSerif, 8.5f, FontStyle.Regular) };
            btnClose = new Button { Text = "Close", Location = new Point(535, 8), Size = new Size(95, 30), DialogResult = DialogResult.Cancel, Font = new Font(FontFamily.GenericSansSerif, 8.5f, FontStyle.Regular) };

            pnlFooter.Controls.Add(progressBar);
            pnlFooter.Controls.Add(lblStatus);
            pnlFooter.Controls.Add(btnClose);

            // --- MAIN PANEL ---
            var pnlMain = new Panel { Dock = DockStyle.Fill, Padding = new Padding(20) };

            int yOffset = 15;

            var lblSource = new Label { Text = "📄 Source CSF Document:", Location = new Point(20, yOffset + 3), AutoSize = true, Font = new Font(FontFamily.GenericSansSerif, 8.5f, FontStyle.Bold) };
            cboSourceCsf = new ComboBox { Location = new Point(240, yOffset), Size = new Size(380, 23), DropDownStyle = ComboBoxStyle.DropDownList };

            yOffset += 36;
            var lblSourceLang = new Label { Text = "🌐 Source Language:", Location = new Point(20, yOffset + 3), AutoSize = true, Font = new Font(FontFamily.GenericSansSerif, 8.5f, FontStyle.Bold) };
            cboSourceLang = new ComboBox { Location = new Point(240, yOffset), Size = new Size(380, 23), DropDownStyle = ComboBoxStyle.DropDown };

            cboSourceLang.Items.AddRange(new object[] {
                "Auto Detect [auto]",
                "English (US) [en]",
                "French [fr]",
                "German [de]",
                "Spanish [es]",
                "Italian [it]",
                "Russian [ru]",
                "Polish [pl]",
                "Japanese [ja]",
                "Korean [ko]",
                "Traditional Chinese [zh-Hant]",
                "Simplified Chinese [zh-Hans]"
            });

            yOffset += 36;
            var lblTargetFile = new Label { Text = "🎯 Target CSF Document:", Location = new Point(20, yOffset + 3), AutoSize = true, Font = new Font(FontFamily.GenericSansSerif, 8.5f, FontStyle.Bold) };
            cboTargetCsf = new ComboBox { Location = new Point(240, yOffset), Size = new Size(380, 23), DropDownStyle = ComboBoxStyle.DropDownList };

            yOffset += 36;
            var lblTargetLang = new Label { Text = "🌐 Target Language:", Location = new Point(20, yOffset + 3), AutoSize = true, Font = new Font(FontFamily.GenericSansSerif, 8.5f, FontStyle.Bold) };
            cboTargetLang = new ComboBox { Location = new Point(240, yOffset), Size = new Size(380, 23), DropDownStyle = ComboBoxStyle.DropDown };

            cboTargetLang.Items.AddRange(new object[] {
                "English (US) [en]",
                "French [fr]",
                "German [de]",
                "Spanish [es]",
                "Italian [it]",
                "Russian [ru]",
                "Polish [pl]",
                "Japanese [ja]",
                "Korean [ko]",
                "Traditional Chinese [zh-Hant]",
                "Simplified Chinese [zh-Hans]"
            });

            yOffset += 36;
            lblModel = new Label { Text = "🤖 AI Model Selection:", Location = new Point(20, yOffset + 3), AutoSize = true, Font = new Font(FontFamily.GenericSansSerif, 8.5f, FontStyle.Bold), Visible = isAiModel };
            cboModelOverride = new ComboBox { Location = new Point(240, yOffset), Size = new Size(250, 23), DropDownStyle = ComboBoxStyle.DropDown, Text = _serviceConfig?.Model ?? "", Visible = isAiModel };

            btnFetchModels = new Button
            {
                Text = "🔄 Fetch Models",
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
                ? $"⚡ Translate {selCount} Selected Keys (Create in Target if missing)"
                : "⚡ Translate ALL keys from Source CSF";

            string btnExistText = hasSelection
                ? $"✏️ Translate {selCount} Selected Keys (Only if existing in Target)"
                : "✏️ Translate ONLY EXISTING keys in Target CSF";

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
            btnFetchModels.Text = "⏳ Fetching...";

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
                        MessageBox.Show($"✅ Discovered {models.Count} active models from provider server!", "Models Discovered", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }
                else if (showSuccessMsg)
                {
                    MessageBox.Show("No models were returned by the server /v1/models endpoint.", "Fetch Models", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            catch (Exception ex)
            {
                if (showSuccessMsg)
                {
                    MessageBox.Show($"❌ Error fetching models from provider endpoint:\n\n{ex.Message}", "Fetch Models Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            finally
            {
                btnFetchModels.Enabled = true;
                btnFetchModels.Text = "🔄 Fetch Models";
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
                MessageBox.Show("Please select valid source and target CSF documents.", "Translation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
                ? $"{selCount} Selected Entries Only"
                : (translateAll ? "ALL keys from selected Source CSF" : "ONLY EXISTING keys in Target CSF");

            string actionDescription = translateAll
                ? (hasSelection ? $"Translate {selCount} selected keys (Add to Target if missing)" : "ALL keys from selected Source CSF")
                : (hasSelection ? $"Translate {selCount} selected keys (Only if existing in Target)" : "ONLY EXISTING keys in Target CSF");

            string modelInfo = cboModelOverride.Visible ? $"· Model Selected: {_serviceConfig.Model}\n" : "";
            string confirmMsg = $"Do you want to proceed with translation using '{_serviceConfig.DisplayName}'?\n\n" +
                               $"· Source CSF: {sourceDoc.FileName}\n" +
                               $"· Target CSF: {targetDoc.FileName}\n" +
                               $"· Scope: {scopeDescription}\n" +
                               $"· Action: {actionDescription}\n" +
                               $"· Source Language: {sourceLanguage}\n" +
                               $"· Target Language: {targetLanguage}\n" +
                               modelInfo;

            if (MessageBox.Show(confirmMsg, "Confirm Translation", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            {
                return;
            }

            btnTranslateAll.Enabled = false;
            btnTranslateExistingOnly.Enabled = false;
            progressBar.Visible = true;
            progressBar.Value = 0;
            lblStatus.Text = "Preparing keys for translation...";

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
                MessageBox.Show("No eligible keys found for translation in the selected scope.", "Translation", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
                            lblStatus.Text = $"Translating... ({processed} / {total} keys)";
                        }));
                    },
                    _cts.Token
                );

                if (result.Success)
                {
                    int translatedCount = 0;
                    var updatedKeys = new List<string>();
                    var batchUndo = new BatchUndoCommand($"Translate {itemsToTranslate.Count} keys ({sourceLanguage}->{targetLanguage})");

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

                    lblStatus.Text = $"✅ Translation completed successfully! ({translatedCount} keys updated)";
                    lblStatus.ForeColor = Color.DarkGreen;
                    MessageBox.Show($"✅ Translation completed successfully!\n\nUpdated: {translatedCount} keys in {targetDoc.FileName}", "Translation Finished", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    lblStatus.Text = $"❌ Translation stopped: {result.ErrorMessage}";
                    lblStatus.ForeColor = Color.Red;
                    MessageBox.Show($"❌ Translation stopped:\n\n{result.ErrorMessage}", "Translation Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            catch (OperationCanceledException)
            {
                lblStatus.Text = "Translation canceled by user.";
                lblStatus.ForeColor = Color.OrangeRed;
            }
            catch (Exception ex)
            {
                lblStatus.Text = "Error during translation: " + ex.Message;
                lblStatus.ForeColor = Color.Red;
                MessageBox.Show("Translation Error: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
