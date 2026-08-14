using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using CsfStudio.Core;

namespace CsfStudio.UI
{
    public class ConvertAnsiDialog : Form
    {
        public class CodepageOption
        {
            public int CodePage { get; set; }
            public string DisplayName { get; set; }
            public override string ToString() => DisplayName;
        }

        private readonly CsfSession _session;
        private List<CodepageOption> _allCodepages = new List<CodepageOption>();

        private TextBox txtSearchCodepage;
        private ComboBox cboCodepage;
        private CheckedListBox chkDocuments;
        private DataGridView gridPreview;
        private Label lblPreviewInfo;
        private Button btnSelectAll;
        private Button btnDeselectAll;
        private Button btnConvert;
        private Button btnCancel;

        public Encoding SelectedEncoding
        {
            get
            {
                if (cboCodepage.SelectedItem is CodepageOption opt)
                {
                    try { return Encoding.GetEncoding(opt.CodePage); } catch { }
                }
                return Encoding.GetEncoding(1252);
            }
        }

        public List<CsfSessionDocument> SelectedDocuments
        {
            get
            {
                var list = new List<CsfSessionDocument>();
                for (int i = 0; i < chkDocuments.Items.Count; i++)
                {
                    if (chkDocuments.GetItemChecked(i) && chkDocuments.Items[i] is DocumentOption opt)
                    {
                        list.Add(opt.Document);
                    }
                }
                return list;
            }
        }

        private class DocumentOption
        {
            public CsfSessionDocument Document { get; set; }
            public string DisplayText { get; set; }
            public override string ToString() => DisplayText;
        }

        public ConvertAnsiDialog(CsfSession session)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            InitializeComponent();
            PopulateData();
            UpdatePreview();
        }

        private void InitializeComponent()
        {
            this.Text = LanguageManager.GetString("ConvertAnsi.Title", "🔤 Convert CSF Text Encoding (ANSI / Codepage to Unicode)");
            this.Size = new Size(720, 560);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.ShowIcon = false;
            this.Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);

            // --- WARNING BANNER ---
            var panelWarning = new Panel
            {
                Location = new Point(12, 10),
                Size = new Size(680, 58),
                BackColor = Color.FromArgb(255, 248, 225),
                BorderStyle = BorderStyle.FixedSingle
            };

            var lblWarnIcon = new Label
            {
                Text = "⚠️",
                Font = new Font("Segoe UI", 16F, FontStyle.Bold),
                Location = new Point(8, 10),
                Size = new Size(35, 35),
                ForeColor = Color.DarkOrange
            };

            var lblWarnText = new Label
            {
                Text = LanguageManager.GetString("ConvertAnsi.WarnText", "ATTENTION: This operation re-encodes raw ANSI / multibyte text bytes into standard Unicode strings for all selected CSF files.\nSupports Russian, Chinese, Japanese, Korean, European, and all system encodings. Affected files will be marked as modified (*)."),
                Location = new Point(45, 8),
                Size = new Size(625, 42),
                ForeColor = Color.FromArgb(120, 70, 0)
            };

            panelWarning.Controls.Add(lblWarnIcon);
            panelWarning.Controls.Add(lblWarnText);

            // --- SOURCE CODEPAGE ---
            var lblCodepage = new Label { Text = LanguageManager.GetString("ConvertAnsi.SourceCodepage", "Source Codepage / Encoding:"), Location = new Point(12, 78), AutoSize = true, Font = new Font("Segoe UI", 9F, FontStyle.Bold) };
            
            txtSearchCodepage = new TextBox
            {
                Location = new Point(12, 98),
                Size = new Size(130, 22)
            };
            ToolTipHelper.SetToolTip(txtSearchCodepage, LanguageManager.GetString("ToolTip.ConvertAnsi.SearchCodepage", "Type to filter codepages (e.g. Russian, Chinese, 936, 1251, Shift, Polish, 866)"));

            bool isPlaceholderActive = true;
            string placeholderText = LanguageManager.GetString("ConvertAnsi.SearchPlaceholder", "🔍 Search...");

            void SetWatermarkState()
            {
                if (string.IsNullOrWhiteSpace(txtSearchCodepage.Text) || isPlaceholderActive)
                {
                    isPlaceholderActive = true;
                    txtSearchCodepage.Text = placeholderText;
                    txtSearchCodepage.ForeColor = Color.Gray;
                    txtSearchCodepage.Font = new Font(FontFamily.GenericSansSerif, 9, FontStyle.Italic);
                }
            }

            SetWatermarkState();

            txtSearchCodepage.GotFocus += (s, e) =>
            {
                if (isPlaceholderActive)
                {
                    isPlaceholderActive = false;
                    txtSearchCodepage.Text = string.Empty;
                    txtSearchCodepage.ForeColor = SystemColors.WindowText;
                    txtSearchCodepage.Font = new Font(FontFamily.GenericSansSerif, 9, FontStyle.Regular);
                }
            };

            txtSearchCodepage.LostFocus += (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(txtSearchCodepage.Text))
                {
                    isPlaceholderActive = true;
                    SetWatermarkState();
                    FilterCodepages(string.Empty);
                }
            };

            txtSearchCodepage.TextChanged += (s, e) =>
            {
                if (!isPlaceholderActive)
                {
                    FilterCodepages(txtSearchCodepage.Text);
                }
            };

            cboCodepage = new ComboBox
            {
                Location = new Point(147, 98),
                Width = 295,
                DropDownWidth = 415,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            cboCodepage.SelectedIndexChanged += (s, e) =>
            {
                UpdatePreview();
                SaveSelectedCodepageToConfig();
                UpdateCodepageToolTip();
            };

            // --- FILES SELECTOR ---
            var lblDocs = new Label { Text = LanguageManager.GetString("ConvertAnsi.TargetFiles", "Target Open CSF Files:"), Location = new Point(455, 78), AutoSize = true, Font = new Font("Segoe UI", 9F, FontStyle.Bold) };
            chkDocuments = new CheckedListBox
            {
                Location = new Point(455, 98),
                Size = new Size(237, 70),
                CheckOnClick = true
            };
            chkDocuments.ItemCheck += ChkDocuments_ItemCheck;

            btnSelectAll = new Button { Text = LanguageManager.GetString("ConvertAnsi.SelectAll", "Select All"), Location = new Point(455, 172), Size = new Size(110, 24) };
            btnSelectAll.Click += (s, e) => { SetAllDocsChecked(true); UpdatePreview(); };

            btnDeselectAll = new Button { Text = LanguageManager.GetString("ConvertAnsi.DeselectAll", "Deselect All"), Location = new Point(582, 172), Size = new Size(110, 24) };
            btnDeselectAll.Click += (s, e) => { SetAllDocsChecked(false); UpdatePreview(); };

            // --- LIVE PREVIEW ---
            lblPreviewInfo = new Label
            {
                Text = LanguageManager.GetString("ConvertAnsi.LivePreview", "Live Conversion Preview (Sample String Entries):"),
                Location = new Point(12, 180),
                AutoSize = true,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold)
            };

            gridPreview = new DataGridView
            {
                Location = new Point(12, 202),
                Size = new Size(680, 260),
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                BackgroundColor = Color.White
            };

            gridPreview.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = LanguageManager.GetString("ConvertAnsi.ColFile", "File"), Width = 130 });
            gridPreview.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = LanguageManager.GetString("Grid.Column.Key", "Key"), Width = 150 });
            gridPreview.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = LanguageManager.GetString("ConvertAnsi.ColOriginal", "Original (Current)"), Width = 195 });
            gridPreview.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = LanguageManager.GetString("ConvertAnsi.ColConverted", "Converted (Preview)"), AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });

            // --- BUTTONS ---
            btnConvert = new Button
            {
                Text = LanguageManager.GetString("ConvertAnsi.BtnConvert", "🔤 Convert Selected Files"),
                Location = new Point(410, 476),
                Size = new Size(200, 32),
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                BackColor = Color.FromArgb(230, 245, 230)
            };
            btnConvert.Click += BtnConvert_Click;

            btnCancel = new Button
            {
                Text = LanguageManager.GetString("Button.Cancel", "Cancel"),
                DialogResult = DialogResult.Cancel,
                Location = new Point(618, 476),
                Size = new Size(74, 32)
            };

            this.Controls.Add(panelWarning);
            this.Controls.Add(lblCodepage);
            this.Controls.Add(txtSearchCodepage);
            this.Controls.Add(cboCodepage);
            this.Controls.Add(lblDocs);
            this.Controls.Add(chkDocuments);
            this.Controls.Add(btnSelectAll);
            this.Controls.Add(btnDeselectAll);
            this.Controls.Add(lblPreviewInfo);
            this.Controls.Add(gridPreview);
            this.Controls.Add(btnConvert);
            this.Controls.Add(btnCancel);

            this.AcceptButton = btnConvert;
            this.CancelButton = btnCancel;
        }

        private void ChkDocuments_ItemCheck(object sender, ItemCheckEventArgs e)
        {
            if (this.IsHandleCreated)
            {
                this.BeginInvoke(new Action(UpdatePreview));
            }
            else
            {
                UpdatePreview();
            }
        }

        private void PopulateData()
        {
            var curatedList = new List<CodepageOption>
            {
                new CodepageOption { CodePage = 1252, DisplayName = LanguageManager.GetString("Codepage.1252", "Windows-1252 — Western European (Spanish / English / German / French)") },
                new CodepageOption { CodePage = 1251, DisplayName = LanguageManager.GetString("Codepage.1251", "Windows-1251 — Cyrillic (Russian / Ukrainian / Bulgarian)") },
                new CodepageOption { CodePage = 866,  DisplayName = LanguageManager.GetString("Codepage.866", "CP866 — MS-DOS Russian / Cyrillic") },
                new CodepageOption { CodePage = 20866, DisplayName = LanguageManager.GetString("Codepage.20866", "KOI8-R — Russian / Cyrillic Unix") },
                new CodepageOption { CodePage = 936,  DisplayName = LanguageManager.GetString("Codepage.936", "GBK / GB2312 (CP936) — Chinese Simplified (简体中文)") },
                new CodepageOption { CodePage = 950,  DisplayName = LanguageManager.GetString("Codepage.950", "Big5 (CP950) — Chinese Traditional (繁體中文)") },
                new CodepageOption { CodePage = 932,  DisplayName = LanguageManager.GetString("Codepage.932", "Shift-JIS / Windows-932 — Japanese (日本語)") },
                new CodepageOption { CodePage = 949,  DisplayName = LanguageManager.GetString("Codepage.949", "EUC-KR / Windows-949 — Korean (한국어)") },
                new CodepageOption { CodePage = 1250, DisplayName = LanguageManager.GetString("Codepage.1250", "Windows-1250 — Central European (Polish / Czech / Hungarian / Slovak)") },
                new CodepageOption { CodePage = 1254, DisplayName = LanguageManager.GetString("Codepage.1254", "Windows-1254 — Turkish") },
                new CodepageOption { CodePage = 1253, DisplayName = LanguageManager.GetString("Codepage.1253", "Windows-1253 — Greek") },
                new CodepageOption { CodePage = 1257, DisplayName = LanguageManager.GetString("Codepage.1257", "Windows-1257 — Baltic (Estonian / Latvian / Lithuanian)") },
                new CodepageOption { CodePage = 1256, DisplayName = LanguageManager.GetString("Codepage.1256", "Windows-1256 — Arabic") },
                new CodepageOption { CodePage = 1255, DisplayName = LanguageManager.GetString("Codepage.1255", "Windows-1255 — Hebrew") },
                new CodepageOption { CodePage = 1258, DisplayName = LanguageManager.GetString("Codepage.1258", "Windows-1258 — Vietnamese") },
                new CodepageOption { CodePage = 874,  DisplayName = LanguageManager.GetString("Codepage.874", "Windows-874 / TIS-620 — Thai") },
                new CodepageOption { CodePage = 28591, DisplayName = LanguageManager.GetString("Codepage.28591", "ISO-8859-1 — Latin-1 Western European") },
                new CodepageOption { CodePage = 65001, DisplayName = LanguageManager.GetString("Codepage.65001", "UTF-8 — Unicode UTF-8") }
            };

            var existingCodes = new HashSet<int>(curatedList.Select(c => c.CodePage));
            _allCodepages = new List<CodepageOption>(curatedList);

            try
            {
                foreach (var enc in Encoding.GetEncodings().OrderBy(e => e.CodePage))
                {
                    if (!existingCodes.Contains(enc.CodePage))
                    {
                        _allCodepages.Add(new CodepageOption
                        {
                            CodePage = enc.CodePage,
                            DisplayName = $"CP {enc.CodePage} — {enc.DisplayName} ({enc.Name})"
                        });
                        existingCodes.Add(enc.CodePage);
                    }
                }
            }
            catch { }

            FilterCodepages(string.Empty);

            var cfg = ConfigManager.LoadConfig();
            int targetCp = cfg != null ? cfg.LastSelectedCodepage : 1252;
            SelectCodepageById(targetCp);

            chkDocuments.ItemCheck -= ChkDocuments_ItemCheck;
            chkDocuments.Items.Clear();
            foreach (var sDoc in _session.Documents)
            {
                if (sDoc == null) continue;
                string fname = string.IsNullOrEmpty(sDoc.FileName) ? "strings.csf" : sDoc.FileName;
                string prefix = !string.IsNullOrWhiteSpace(sDoc.LanguageTag) ? $"[{sDoc.LanguageTag}] " : "";
                string display = $"{prefix}{fname}";
                chkDocuments.Items.Add(new DocumentOption { Document = sDoc, DisplayText = display }, true);
            }
            chkDocuments.ItemCheck += ChkDocuments_ItemCheck;
        }

        private void SelectCodepageById(int cpId)
        {
            for (int i = 0; i < cboCodepage.Items.Count; i++)
            {
                if (cboCodepage.Items[i] is CodepageOption opt && opt.CodePage == cpId)
                {
                    cboCodepage.SelectedIndex = i;
                    UpdateCodepageToolTip();
                    return;
                }
            }
            if (cboCodepage.Items.Count > 0)
            {
                cboCodepage.SelectedIndex = 0;
                UpdateCodepageToolTip();
            }
        }

        private void UpdateCodepageToolTip()
        {
            if (cboCodepage != null && cboCodepage.SelectedItem is CodepageOption opt)
            {
                ToolTipHelper.SetToolTip(cboCodepage, string.Format(LanguageManager.GetString("ConvertAnsi.CodepageToolTip", "Encoding Codepage {0}:\n{1}"), opt.CodePage, opt.DisplayName), 70);
            }
        }

        private void FilterCodepages(string filterText)
        {
            cboCodepage.BeginUpdate();
            cboCodepage.Items.Clear();

            string search = filterText?.Trim() ?? string.Empty;
            foreach (var opt in _allCodepages)
            {
                if (string.IsNullOrEmpty(search) ||
                    opt.DisplayName.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    opt.CodePage.ToString().Contains(search))
                {
                    cboCodepage.Items.Add(opt);
                }
            }

            cboCodepage.EndUpdate();
            if (cboCodepage.Items.Count > 0)
            {
                cboCodepage.SelectedIndex = 0;
            }
        }

        private void SaveSelectedCodepageToConfig()
        {
            try
            {
                if (cboCodepage.SelectedItem is CodepageOption opt)
                {
                    var cfg = ConfigManager.LoadConfig();
                    if (cfg != null && cfg.LastSelectedCodepage != opt.CodePage)
                    {
                        cfg.LastSelectedCodepage = opt.CodePage;
                        ConfigManager.SaveConfig(cfg);
                    }
                }
            }
            catch { }
        }

        private void SetAllDocsChecked(bool isChecked)
        {
            for (int i = 0; i < chkDocuments.Items.Count; i++)
            {
                chkDocuments.SetItemChecked(i, isChecked);
            }
        }

        private void UpdatePreview()
        {
            gridPreview.Rows.Clear();
            var selectedDocs = SelectedDocuments;
            if (selectedDocs.Count == 0)
            {
                lblPreviewInfo.Text = LanguageManager.GetString("ConvertAnsi.PreviewNoFiles", "Live Conversion Preview: (No files selected)");
                btnConvert.Enabled = false;
                return;
            }

            btnConvert.Enabled = true;
            Encoding enc = SelectedEncoding;
            int count = 0;

            foreach (var docItem in selectedDocs)
            {
                if (docItem == null) continue;
                string fname = string.IsNullOrEmpty(docItem.FileName) ? "strings.csf" : docItem.FileName;
                string tagPrefix = !string.IsNullOrWhiteSpace(docItem.LanguageTag) ? $"[{docItem.LanguageTag}] " : "";
                string docName = $"{tagPrefix}{fname}";

                if (docItem.Document == null) continue;

                foreach (var lbl in docItem.Document.Labels)
                {
                    foreach (var entry in lbl.Strings)
                    {
                        if (string.IsNullOrEmpty(entry.Value)) continue;

                        bool hasNonAscii = entry.Value.Any(c => (byte)(c & 0xFF) >= 0x80 || c >= 0x80);
                        if (hasNonAscii || count < 10)
                        {
                            string converted = ConvertAnsiToUnicode(entry.Value, enc);
                            int rowIdx = gridPreview.Rows.Add(docName, lbl.Name, entry.Value, converted);

                            if (string.Equals(entry.Value, converted, StringComparison.Ordinal))
                            {
                                gridPreview.Rows[rowIdx].Cells[3].Style.ForeColor = Color.DimGray;
                            }
                            else
                            {
                                gridPreview.Rows[rowIdx].Cells[3].Style.ForeColor = Color.DarkGreen;
                                gridPreview.Rows[rowIdx].Cells[3].Style.Font = new Font(gridPreview.Font, FontStyle.Bold);
                            }

                            count++;
                            if (count >= 15) break;
                        }
                    }
                    if (count >= 15) break;
                }
                if (count >= 15) break;
            }

            string encName = enc != null ? $"{enc.EncodingName} (CP {enc.CodePage})" : "Unknown";
            lblPreviewInfo.Text = count == 0
                ? LanguageManager.GetString("ConvertAnsi.PreviewNoStrings", "Live Conversion Preview: (No non-empty string entries found in selected files)")
                : string.Format(LanguageManager.GetString("ConvertAnsi.PreviewSampleFormat", "Live Conversion Preview (Showing sample entries converted using {0}):"), encName);
        }

        private void BtnConvert_Click(object sender, EventArgs e)
        {
            var selectedDocs = SelectedDocuments;
            if (selectedDocs.Count == 0)
            {
                MessageBox.Show(
                    LanguageManager.GetString("Msg.SelectCsfToConvert", "Please select at least one open CSF file to convert."),
                    LanguageManager.GetString("Title.NoSelection", "No Selection"),
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            Encoding enc = SelectedEncoding;
            SaveSelectedCodepageToConfig();

            var confirm = MessageBox.Show(
                string.Format(LanguageManager.GetString("Msg.ConfirmAnsiConvertFormat", "Are you sure you want to convert all string entries in the {0} selected CSF file(s) using codepage '{1}' (CP {2})?\n\nThis operation re-encodes raw ANSI/multibyte byte values into standard Unicode strings. Affected files will be marked as modified (*)."), selectedDocs.Count, enc.EncodingName, enc.CodePage),
                LanguageManager.GetString("Title.ConfirmEncodingConversion", "Confirm Encoding Conversion"),
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (confirm != DialogResult.Yes) return;

            this.DialogResult = DialogResult.OK;
            this.Close();
        }

        public static string ConvertAnsiToUnicode(string rawStr, Encoding sourceEncoding)
        {
            if (string.IsNullOrEmpty(rawStr) || sourceEncoding == null) return rawStr;

            byte[] bytes = new byte[rawStr.Length];
            bool hasNonAscii = false;

            for (int i = 0; i < rawStr.Length; i++)
            {
                char ch = rawStr[i];
                byte b = (byte)(ch & 0xFF);
                bytes[i] = b;
                if (b >= 0x80 || ch >= 0x80)
                {
                    hasNonAscii = true;
                }
            }

            if (!hasNonAscii) return rawStr;

            try
            {
                return sourceEncoding.GetString(bytes);
            }
            catch
            {
                return rawStr;
            }
        }
    }
}
