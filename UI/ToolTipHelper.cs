using System;
using System.Collections.Generic;
using System.Windows.Forms;
using CsfStudio.Core;

namespace CsfStudio.UI
{
    public static class ToolTipHelper
    {
        public static string WrapText(string text, int maxLineLength = 45, int maxLines = 15)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            var lines = text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            var wrappedLines = new List<string>();

            foreach (var rawLine in lines)
            {
                string line = rawLine;
                if (line.Length <= maxLineLength)
                {
                    wrappedLines.Add(line);
                }
                else
                {
                    while (line.Length > maxLineLength)
                    {
                        int splitIndex = line.LastIndexOf(' ', maxLineLength);
                        if (splitIndex <= 15) splitIndex = maxLineLength;

                        wrappedLines.Add(line.Substring(0, splitIndex).TrimEnd());
                        line = line.Substring(splitIndex).TrimStart();
                    }
                    if (line.Length > 0)
                    {
                        wrappedLines.Add(line);
                    }
                }

                if (wrappedLines.Count >= maxLines)
                {
                    wrappedLines.Add("... [Preview truncated]");
                    break;
                }
            }

            return string.Join(Environment.NewLine, wrappedLines);
        }

        private static ToolTip _sharedToolTip;

        public static void SetToolTip(Control control, string caption, int maxLineLength = 45)
        {
            if (control == null) return;
            if (_sharedToolTip == null)
            {
                _sharedToolTip = new ToolTip
                {
                    InitialDelay = 400,
                    ReshowDelay = 100,
                    AutoPopDelay = 8000,
                    ShowAlways = false
                };
            }
            _sharedToolTip.SetToolTip(control, WrapText(caption, maxLineLength));
        }

        public static void SetToolTip(ToolTip tt, Control control, string caption, int maxLineLength = 45)
        {
            if (tt == null || control == null) return;
            tt.SetToolTip(control, WrapText(caption, maxLineLength));
        }

        public static void SetToolTip(ToolStripItem item, string caption, int maxLineLength = 45)
        {
            if (item == null) return;
            item.ToolTipText = WrapText(caption, maxLineLength);
        }

        public static bool CheckAndPromptUnknownLanguage(CsfStudio.Core.CsfDocument doc, string filePath, IWin32Window owner = null)
        {
            if (doc == null || doc.Language != CsfStudio.Core.CsfLanguage.Unknown) return false;

            string fileName = string.IsNullOrEmpty(filePath) ? "CSF Document" : System.IO.Path.GetFileName(filePath);

            using (var dlg = new Form())
            {
                dlg.Text = "Unknown Header Language ID Detected";
                dlg.Size = new System.Drawing.Size(520, 275);
                dlg.StartPosition = owner != null ? FormStartPosition.CenterParent : FormStartPosition.CenterScreen;
                dlg.FormBorderStyle = FormBorderStyle.FixedDialog;
                dlg.MaximizeBox = false;
                dlg.MinimizeBox = false;
                dlg.ShowIcon = false;

                var lblIcon = new Label
                {
                    Text = "⚠️",
                    Font = new System.Drawing.Font("Segoe UI", 24f),
                    Location = new System.Drawing.Point(15, 15),
                    Size = new System.Drawing.Size(45, 45)
                };

                var lblMsg = new Label
                {
                    Text = $"The loaded file '{fileName}' contains an unrecognized or invalid binary Language ID (Offset 0x14) in its CSF header.\n\nPlease select a valid Language ID to assign to this document:",
                    Location = new System.Drawing.Point(65, 15),
                    Size = new System.Drawing.Size(425, 75),
                    Font = new System.Drawing.Font(System.Drawing.FontFamily.GenericSansSerif, 8.5f)
                };

                var lblComboPrompt = new Label
                {
                    Text = "Header Language ID:",
                    Location = new System.Drawing.Point(15, 95),
                    AutoSize = true,
                    Font = new System.Drawing.Font(System.Drawing.FontFamily.GenericSansSerif, 8.5f, System.Drawing.FontStyle.Bold)
                };

                var cmbLang = new ComboBox
                {
                    DropDownStyle = ComboBoxStyle.DropDownList,
                    Location = new System.Drawing.Point(170, 92),
                    Size = new System.Drawing.Size(315, 24),
                    Font = new System.Drawing.Font(System.Drawing.FontFamily.GenericSansSerif, 8.5f)
                };

                var langList = new List<CsfStudio.Core.CsfLanguage>();
                foreach (CsfStudio.Core.CsfLanguage lang in Enum.GetValues(typeof(CsfStudio.Core.CsfLanguage)))
                {
                    if (lang == CsfStudio.Core.CsfLanguage.Unknown) continue;
                    langList.Add(lang);
                    string extraInfo = lang == CsfStudio.Core.CsfLanguage.LanguageNeutral
                        ? "LanguageNeutral (-1 / 0xFFFFFFFF) [Requires Ares DLL]"
                        : $"{lang} ({(int)lang})";
                    cmbLang.Items.Add(extraInfo);
                }

                cmbLang.SelectedIndex = 0; // Default to EnglishUS (0)

                var lblAresNote = new Label
                {
                    Text = "Note: 'LanguageNeutral' requires the Ares Engine Expansion DLL in Red Alert 2 / Yuri's Revenge.",
                    Location = new System.Drawing.Point(65, 128),
                    Size = new System.Drawing.Size(425, 35),
                    Font = new System.Drawing.Font(System.Drawing.FontFamily.GenericSansSerif, 8f, System.Drawing.FontStyle.Italic),
                    ForeColor = System.Drawing.Color.DarkSlateGray
                };

                var btnOk = new Button
                {
                    Text = "Assign Language",
                    DialogResult = DialogResult.OK,
                    Location = new System.Drawing.Point(245, 180),
                    Size = new System.Drawing.Size(135, 32),
                    Font = new System.Drawing.Font(System.Drawing.FontFamily.GenericSansSerif, 8.5f, System.Drawing.FontStyle.Bold)
                };

                var btnCancel = new Button
                {
                    Text = "Keep Unknown",
                    DialogResult = DialogResult.Cancel,
                    Location = new System.Drawing.Point(390, 180),
                    Size = new System.Drawing.Size(100, 32)
                };

                dlg.Controls.Add(lblIcon);
                dlg.Controls.Add(lblMsg);
                dlg.Controls.Add(lblComboPrompt);
                dlg.Controls.Add(cmbLang);
                dlg.Controls.Add(lblAresNote);
                dlg.Controls.Add(btnOk);
                dlg.Controls.Add(btnCancel);
                dlg.AcceptButton = btnOk;
                dlg.CancelButton = btnCancel;

                if (dlg.ShowDialog(owner) == DialogResult.OK && cmbLang.SelectedIndex >= 0)
                {
                    doc.Language = langList[cmbLang.SelectedIndex];
                    return true;
                }
            }

            return false;
        }
    }
}
