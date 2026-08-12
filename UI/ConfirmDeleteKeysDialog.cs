using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using CsfStudio.Core;

namespace CsfStudio.UI
{
    public class ConfirmDeleteKeysDialog : Form
    {
        private DataGridView gridKeys;
        private Button btnDelete;
        private Button btnCancel;
        private Label lblMessage;

        public ConfirmDeleteKeysDialog(List<MasterKeyRow> keysToDelete, CsfSession session)
        {
            InitializeComponent();
            PopulateData(keysToDelete, session);
        }

        private void InitializeComponent()
        {
            this.Text = "Confirm Key Deletion";
            this.Size = new Size(860, 540);
            this.MinimumSize = new Size(680, 400);
            this.StartPosition = FormStartPosition.CenterParent;
            this.ShowIcon = false;

            var panelTop = new Panel
            {
                Dock = DockStyle.Top,
                Height = 46,
                Padding = new Padding(12, 10, 12, 10),
                BackColor = Color.FromArgb(254, 238, 238)
            };

            lblMessage = new Label
            {
                Text = "⚠️ Are you sure you want to permanently delete the following key(s) from ALL open CSF files?",
                Dock = DockStyle.Fill,
                Font = new Font(FontFamily.GenericSansSerif, 9f, FontStyle.Bold),
                ForeColor = Color.DarkRed,
                TextAlign = ContentAlignment.MiddleLeft
            };

            panelTop.Controls.Add(lblMessage);

            gridKeys = new DataGridView
            {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
                ReadOnly = true,
                RowHeadersVisible = false,
                ShowCellToolTips = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None,
                DefaultCellStyle = new DataGridViewCellStyle { WrapMode = DataGridViewTriState.True },
                AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCellsExceptHeaders,
                Margin = new Padding(8),
                BackColor = Color.White
            };

            var colKey = new DataGridViewTextBoxColumn
            {
                HeaderText = "Key Name",
                Width = 220,
                ReadOnly = true
            };
            var colLang = new DataGridViewTextBoxColumn
            {
                HeaderText = "Label",
                Width = 140,
                ReadOnly = true
            };
            var colVal = new DataGridViewTextBoxColumn
            {
                HeaderText = "Full Entry Value to be Deleted",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                ReadOnly = true
            };

            gridKeys.Columns.AddRange(new DataGridViewColumn[] { colKey, colLang, colVal });

            var panelBottom = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 52,
                Padding = new Padding(10),
                BackColor = Color.FromArgb(245, 247, 250)
            };

            btnDelete = new Button
            {
                Text = "🗑️ Permanently Delete Key(s)",
                DialogResult = DialogResult.OK,
                Size = new Size(210, 33),
                Font = new Font(FontFamily.GenericSansSerif, 9f, FontStyle.Bold),
                BackColor = Color.Crimson,
                ForeColor = Color.White
            };

            btnCancel = new Button
            {
                Text = "Cancel",
                DialogResult = DialogResult.Cancel,
                Size = new Size(100, 33)
            };

            void LayoutBottom()
            {
                int w = panelBottom.ClientSize.Width;
                btnCancel.Location = new Point(w - 110, 10);
                btnDelete.Location = new Point(w - 110 - 220, 10);
            }

            panelBottom.Resize += (s, e) => LayoutBottom();
            LayoutBottom();

            panelBottom.Controls.Add(btnDelete);
            panelBottom.Controls.Add(btnCancel);

            this.Controls.Add(gridKeys);
            this.Controls.Add(panelTop);
            this.Controls.Add(panelBottom);
        }

        private void PopulateData(List<MasterKeyRow> keysToDelete, CsfSession session)
        {
            if (keysToDelete == null || keysToDelete.Count == 0 || session == null) return;

            lblMessage.Text = $"⚠️ Are you sure you want to delete {keysToDelete.Count} key(s) from ALL open CSF files?";

            var documents = session.Documents;
            var baseDoc = session.BaseDocument ?? documents.FirstOrDefault();

            for (int k = 0; k < keysToDelete.Count; k++)
            {
                var row = keysToDelete[k];
                bool firstLine = true;

                // Render Base document first
                if (baseDoc != null)
                {
                    string tag = baseDoc.LanguageTag;
                    string val = row.ValuesPerLanguage.TryGetValue(tag, out var v) ? v.Value : string.Empty;
                    string displayVal = string.IsNullOrEmpty(val) ? "(Empty / Missing)" : val;

                    int idx = gridKeys.Rows.Add(
                        $"🔑 {row.KeyName}",
                        $"📌 Base ({tag})",
                        displayVal
                    );

                    gridKeys.Rows[idx].DefaultCellStyle.BackColor = Color.FromArgb(245, 248, 255);
                    gridKeys.Rows[idx].DefaultCellStyle.Font = new Font(FontFamily.GenericSansSerif, 8.5f, FontStyle.Bold);
                    firstLine = false;
                }

                // Render all other target documents
                foreach (var doc in documents.Where(d => d != baseDoc))
                {
                    string tag = doc.LanguageTag;
                    string val = row.ValuesPerLanguage.TryGetValue(tag, out var v) ? v.Value : string.Empty;
                    string displayVal = string.IsNullOrEmpty(val) ? "(Empty / Missing)" : val;

                    int idx = gridKeys.Rows.Add(
                        firstLine ? $"🔑 {row.KeyName}" : string.Empty,
                        $"📄 {tag}",
                        displayVal
                    );

                    if (string.IsNullOrEmpty(val))
                    {
                        gridKeys.Rows[idx].Cells[2].Style.ForeColor = Color.DimGray;
                        gridKeys.Rows[idx].Cells[2].Style.Font = new Font(FontFamily.GenericSansSerif, 8.5f, FontStyle.Italic);
                    }

                    firstLine = false;
                }

                // Add visual separation between key groups if deleting multiple keys
                if (k < keysToDelete.Count - 1)
                {
                    int sepIdx = gridKeys.Rows.Add(string.Empty, string.Empty, string.Empty);
                    gridKeys.Rows[sepIdx].Height = 8;
                    gridKeys.Rows[sepIdx].DefaultCellStyle.BackColor = Color.FromArgb(235, 238, 242);
                }
            }
        }
    }
}
