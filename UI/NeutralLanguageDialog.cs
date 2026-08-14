using System;
using System.Drawing;
using System.Windows.Forms;
using CsfStudio.Core;
using CsfStudio.Core.Translation;

namespace CsfStudio.UI
{
    public class NeutralLanguageDialog : Form
    {
        private readonly ComboBox _languageCombo;

        public string SelectedLanguage { get; private set; }

        public NeutralLanguageDialog(string fileName, string currentLanguage = null, bool isNeutral = true)
        {
            Text = LanguageManager.GetString("NeutralLang.Title", "Configure Translation Content Language");
            Size = new Size(580, 230);
            MinimumSize = new Size(580, 230);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowIcon = false;

            string infoText = isNeutral
                ? string.Format(LanguageManager.GetString("NeutralLang.InfoNeutral", "'{0}' uses LanguageNeutral in its CSF header.\nSelect the language used by its text content. This does not change the binary header."), fileName)
                : string.Format(LanguageManager.GetString("NeutralLang.InfoExplicit", "Select the language used by the text content of '{0}'.\nThis is a translation setting and does not change the CSF binary header."), fileName);

            var info = new Label
            {
                Text = infoText,
                Location = new Point(15, 15),
                Size = new Size(535, 60),
                Font = new Font(FontFamily.GenericSansSerif, 8.5f)
            };

            var prompt = new Label
            {
                Text = LanguageManager.GetString("NeutralLang.ContentLangLabel", "Content Language:"),
                Location = new Point(15, 93),
                AutoSize = true,
                Font = new Font(FontFamily.GenericSansSerif, 8.5f, FontStyle.Bold)
            };

            _languageCombo = new ComboBox
            {
                Location = new Point(200, 90),
                Size = new Size(350, 24),
                DropDownStyle = ComboBoxStyle.DropDown
            };
            foreach (var option in TranslationLanguageHelper.GetLanguageOptions())
            {
                _languageCombo.Items.Add(option);
            }

            string initialLanguage = TranslationLanguageHelper.Normalize(currentLanguage);
            if (string.IsNullOrEmpty(initialLanguage))
            {
                initialLanguage = TranslationLanguageHelper.GetDefaultSourceLanguage();
            }
            string initialDisplay = TranslationLanguageHelper.GetDisplayName(initialLanguage);
            if (_languageCombo.Items.Contains(initialDisplay))
            {
                _languageCombo.SelectedItem = initialDisplay;
            }
            else
            {
                _languageCombo.Text = initialDisplay;
            }

            var btnOk = new Button
            {
                Text = LanguageManager.GetString("Button.OK", "OK"),
                Location = new Point(370, 140),
                Size = new Size(85, 28)
            };
            var btnCancel = new Button
            {
                Text = LanguageManager.GetString("Button.Cancel", "Cancel"),
                Location = new Point(465, 140),
                Size = new Size(85, 28),
                DialogResult = DialogResult.Cancel
            };

            btnOk.Click += (s, e) =>
            {
                string language = TranslationLanguageHelper.Normalize(_languageCombo.Text);
                if (string.IsNullOrEmpty(language) || language == "auto")
                {
                    MessageBox.Show(
                        LanguageManager.GetString("Msg.NeutralLangRequired", "Select an explicit content language for this neutral CSF."),
                        LanguageManager.GetString("Title.NeutralLangRequired", "Language Required"),
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    return;
                }

                SelectedLanguage = language;
                DialogResult = DialogResult.OK;
                Close();
            };

            Controls.Add(info);
            Controls.Add(prompt);
            Controls.Add(_languageCombo);
            Controls.Add(btnOk);
            Controls.Add(btnCancel);
            AcceptButton = btnOk;
            CancelButton = btnCancel;
        }
    }
}
