using System;
using System.Drawing;
using System.Windows.Forms;
using CsfStudio.Core.Translation;

namespace CsfStudio.UI
{
    public class NeutralLanguageDialog : Form
    {
        private readonly ComboBox _languageCombo;

        public string SelectedLanguage { get; private set; }

        public NeutralLanguageDialog(string fileName, string currentLanguage = null, bool isNeutral = true)
        {
            Text = "Configure Translation Content Language";
            Size = new Size(520, 230);
            MinimumSize = new Size(520, 230);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowIcon = false;

            string infoText = isNeutral
                ? $"'{fileName}' uses LanguageNeutral in its CSF header.\nSelect the language used by its text content. This does not change the binary header."
                : $"Select the language used by the text content of '{fileName}'.\nThis is a translation setting and does not change the CSF binary header.";

            var info = new Label
            {
                Text = infoText,
                Location = new Point(15, 15),
                Size = new Size(475, 55),
                Font = new Font(FontFamily.GenericSansSerif, 8.5f)
            };

            var prompt = new Label
            {
                Text = "Content Language:",
                Location = new Point(15, 93),
                AutoSize = true,
                Font = new Font(FontFamily.GenericSansSerif, 8.5f, FontStyle.Bold)
            };

            _languageCombo = new ComboBox
            {
                Location = new Point(155, 90),
                Size = new Size(335, 24),
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
                Text = "OK",
                Location = new Point(310, 140),
                Size = new Size(85, 28)
            };
            var btnCancel = new Button
            {
                Text = "Cancel",
                Location = new Point(405, 140),
                Size = new Size(85, 28),
                DialogResult = DialogResult.Cancel
            };

            btnOk.Click += (s, e) =>
            {
                string language = TranslationLanguageHelper.Normalize(_languageCombo.Text);
                if (string.IsNullOrEmpty(language) || language == "auto")
                {
                    MessageBox.Show("Select an explicit content language for this neutral CSF.", "Language Required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
