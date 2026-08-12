using System;
using System.Windows.Forms;
using CsfStudio.Core;

namespace CsfStudio.UI
{
    public partial class FindReplaceDialog : Form
    {
        private AppConfig _config;

        public string FindText => cboFind.Text;
        public string ReplaceText => cboReplace.Text;
        public bool MatchCase => chkMatchCase.Checked;
        public bool UseRegex => chkUseRegex.Checked;
        public bool SearchKey => chkSearchKey.Checked;
        public bool SearchValue => chkSearchValue.Checked;

        public event EventHandler OnFindNext;
        public event EventHandler OnReplace;
        public event EventHandler OnReplaceAll;

        public FindReplaceDialog()
        {
            InitializeComponent();
            _config = ConfigManager.LoadConfig();

            ToolTipHelper.SetToolTip(cboFind, "Enter search pattern or text to locate.");
            ToolTipHelper.SetToolTip(cboReplace, "Enter replacement text for matching items.");
            ToolTipHelper.SetToolTip(chkMatchCase, "Enforce strict uppercase and lowercase matching.");
            ToolTipHelper.SetToolTip(chkUseRegex, "Enables standard .NET Regular Expression pattern matching.\n\nFormat Examples:\n• ^GUI_.* : Matches keys starting with GUI_\n• \\b(Unit|Building)\\b : Matches exact words\n• Replace with $1 : Uses capture groups\n\nClick (Online Help) for complete documentation.");
            ToolTipHelper.SetToolTip(lnkRegexHelp, "Open Microsoft .NET Regular Expressions documentation in web browser.");
            ToolTipHelper.SetToolTip(chkSearchKey, "Search inside label key names.");
            ToolTipHelper.SetToolTip(chkSearchValue, "Search inside label string values.");
            ToolTipHelper.SetToolTip(btnFindNext, "Find the next matching string entry.");
            ToolTipHelper.SetToolTip(btnReplace, "Replace current match and advance to next.");
            ToolTipHelper.SetToolTip(btnReplaceAll, "Replace all matching occurrences across the document.");

            LoadConfigToUI();
            chkUseRegex.CheckedChanged += ChkUseRegex_CheckedChanged;
        }

        private void LoadConfigToUI()
        {
            if (_config == null) return;

            chkMatchCase.Checked = _config.FindMatchCase;
            chkUseRegex.Checked = _config.FindUseRegex;
            chkSearchKey.Checked = _config.FindSearchKey;
            chkSearchValue.Checked = _config.FindSearchValue;

            RefreshComboItems(cboFind, chkUseRegex.Checked ? _config.FindHistoryRegex : _config.FindHistoryPlain, string.Empty);
            RefreshComboItems(cboReplace, chkUseRegex.Checked ? _config.ReplaceHistoryRegex : _config.ReplaceHistoryPlain, string.Empty);
        }

        private void ChkUseRegex_CheckedChanged(object sender, EventArgs e)
        {
            cboFind.Text = string.Empty;
            cboReplace.Text = string.Empty;
            RefreshComboItems(cboFind, chkUseRegex.Checked ? _config.FindHistoryRegex : _config.FindHistoryPlain, string.Empty);
            RefreshComboItems(cboReplace, chkUseRegex.Checked ? _config.ReplaceHistoryRegex : _config.ReplaceHistoryPlain, string.Empty);
        }

        private void RefreshComboItems(ComboBox cbo, System.Collections.Generic.List<string> items, string currentText)
        {
            if (cbo == null) return;
            cbo.Items.Clear();
            if (items != null)
            {
                foreach (var item in items) cbo.Items.Add(item);
            }
            cbo.Text = currentText ?? string.Empty;
        }

        private void SaveCurrentSearchState()
        {
            string fText = cboFind.Text.Trim();
            if (!string.IsNullOrEmpty(fText))
            {
                var targetFindList = chkUseRegex.Checked ? _config.FindHistoryRegex : _config.FindHistoryPlain;
                ConfigManager.AddHistoryItem(targetFindList, fText, _config.MaxSearchHistoryItems);
            }

            string rText = cboReplace.Text.Trim();
            if (!string.IsNullOrEmpty(rText))
            {
                var targetReplaceList = chkUseRegex.Checked ? _config.ReplaceHistoryRegex : _config.ReplaceHistoryPlain;
                ConfigManager.AddHistoryItem(targetReplaceList, rText, _config.MaxSearchHistoryItems);
            }

            _config.FindMatchCase = chkMatchCase.Checked;
            _config.FindUseRegex = chkUseRegex.Checked;
            _config.FindSearchKey = chkSearchKey.Checked;
            _config.FindSearchValue = chkSearchValue.Checked;

            ConfigManager.SaveConfig(_config);
        }

        private void lnkRegexHelp_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            try
            {
                System.Diagnostics.Process.Start("https://learn.microsoft.com/dotnet/standard/base-types/regular-expressions");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Unable to open web browser:\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void btnFindNext_Click(object sender, EventArgs e)
        {
            SaveCurrentSearchState();
            OnFindNext?.Invoke(this, EventArgs.Empty);
        }

        private void btnReplace_Click(object sender, EventArgs e)
        {
            SaveCurrentSearchState();
            OnReplace?.Invoke(this, EventArgs.Empty);
        }

        private void btnReplaceAll_Click(object sender, EventArgs e)
        {
            SaveCurrentSearchState();
            OnReplaceAll?.Invoke(this, EventArgs.Empty);
        }

        private void btnClose_Click(object sender, EventArgs e)
        {
            SaveCurrentSearchState();
            Close();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            base.OnFormClosing(e);
            SaveCurrentSearchState();
        }
    }
}
