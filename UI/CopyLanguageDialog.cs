using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using CsfStudio.Core;

namespace CsfStudio.UI
{
    public class CopyLanguageDialog : Form
    {
        private ComboBox cboSourceLang;
        private ComboBox cboTargetLang;
        private CheckBox chkOnlyEmpty;
        private Button btnOk;
        private Button btnCancel;

        public string SourceLanguageTag => cboSourceLang.SelectedItem as string;
        public string TargetLanguageTag => cboTargetLang.SelectedItem as string;
        public bool OnlyEmptyKeys => chkOnlyEmpty.Checked;

        public CopyLanguageDialog(IEnumerable<string> availableLanguages)
        {
            InitializeComponent();
            foreach (var lang in availableLanguages)
            {
                cboSourceLang.Items.Add(lang);
                cboTargetLang.Items.Add(lang);
            }

            if (cboSourceLang.Items.Count > 0) cboSourceLang.SelectedIndex = 0;
            if (cboTargetLang.Items.Count > 1) cboTargetLang.SelectedIndex = 1;
            else if (cboTargetLang.Items.Count > 0) cboTargetLang.SelectedIndex = 0;
        }

        private void InitializeComponent()
        {
            this.Text = LanguageManager.GetString("CopyLanguage.Title", "Copy String Values Between Open Files");
            this.Size = new Size(420, 220);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.ShowIcon = false;

            var lblSource = new Label { Text = LanguageManager.GetString("CopyLanguage.SourceFile", "Source File (A):"), Location = new Point(20, 20), AutoSize = true };
            cboSourceLang = new ComboBox { Location = new Point(160, 17), Width = 210, DropDownStyle = ComboBoxStyle.DropDownList };

            var lblTarget = new Label { Text = LanguageManager.GetString("CopyLanguage.TargetFile", "Target File (B):"), Location = new Point(20, 55), AutoSize = true };
            cboTargetLang = new ComboBox { Location = new Point(160, 52), Width = 210, DropDownStyle = ComboBoxStyle.DropDownList };

            chkOnlyEmpty = new CheckBox
            {
                Text = LanguageManager.GetString("CopyLanguage.OnlyEmptyKeys", "Copy ONLY to empty / missing keys in target file"),
                Location = new Point(20, 95),
                AutoSize = true,
                Checked = true
            };

            btnOk = new Button { Text = LanguageManager.GetString("CopyLanguage.BtnCopy", "Copy Values"), DialogResult = DialogResult.OK, Location = new Point(170, 135), Size = new Size(130, 28) };
            btnCancel = new Button { Text = LanguageManager.GetString("Button.Cancel", "Cancel"), DialogResult = DialogResult.Cancel, Location = new Point(310, 135), Size = new Size(80, 28) };

            this.Controls.Add(lblSource);
            this.Controls.Add(cboSourceLang);
            this.Controls.Add(lblTarget);
            this.Controls.Add(cboTargetLang);
            this.Controls.Add(chkOnlyEmpty);
            this.Controls.Add(btnOk);
            this.Controls.Add(btnCancel);
        }
    }
}
