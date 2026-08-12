namespace CsfStudio.UI
{
    partial class FindReplaceDialog
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        private void InitializeComponent()
        {
            this.lblFind = new System.Windows.Forms.Label();
            this.lblReplace = new System.Windows.Forms.Label();
            this.cboFind = new System.Windows.Forms.ComboBox();
            this.cboReplace = new System.Windows.Forms.ComboBox();
            this.chkMatchCase = new System.Windows.Forms.CheckBox();
            this.chkUseRegex = new System.Windows.Forms.CheckBox();
            this.lnkRegexHelp = new System.Windows.Forms.LinkLabel();
            this.chkSearchKey = new System.Windows.Forms.CheckBox();
            this.chkSearchValue = new System.Windows.Forms.CheckBox();
            this.btnFindNext = new System.Windows.Forms.Button();
            this.btnReplace = new System.Windows.Forms.Button();
            this.btnReplaceAll = new System.Windows.Forms.Button();
            this.btnClose = new System.Windows.Forms.Button();
            this.grpScope = new System.Windows.Forms.GroupBox();
            this.grpScope.SuspendLayout();
            this.SuspendLayout();
            // 
            // lblFind
            // 
            this.lblFind.AutoSize = true;
            this.lblFind.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblFind.Location = new System.Drawing.Point(15, 18);
            this.lblFind.Name = "lblFind";
            this.lblFind.Size = new System.Drawing.Size(57, 15);
            this.lblFind.TabIndex = 0;
            this.lblFind.Text = "Find text:";
            // 
            // lblReplace
            // 
            this.lblReplace.AutoSize = true;
            this.lblReplace.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblReplace.Location = new System.Drawing.Point(15, 48);
            this.lblReplace.Name = "lblReplace";
            this.lblReplace.Size = new System.Drawing.Size(77, 15);
            this.lblReplace.TabIndex = 2;
            this.lblReplace.Text = "Replace with:";
            // 
            // cboFind
            // 
            this.cboFind.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.cboFind.Location = new System.Drawing.Point(105, 15);
            this.cboFind.Name = "cboFind";
            this.cboFind.Size = new System.Drawing.Size(245, 23);
            this.cboFind.TabIndex = 1;
            // 
            // cboReplace
            // 
            this.cboReplace.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.cboReplace.Location = new System.Drawing.Point(105, 45);
            this.cboReplace.Name = "cboReplace";
            this.cboReplace.Size = new System.Drawing.Size(245, 23);
            this.cboReplace.TabIndex = 3;
            // 
            // chkMatchCase
            // 
            this.chkMatchCase.AutoSize = true;
            this.chkMatchCase.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.chkMatchCase.Location = new System.Drawing.Point(105, 77);
            this.chkMatchCase.Name = "chkMatchCase";
            this.chkMatchCase.Size = new System.Drawing.Size(87, 19);
            this.chkMatchCase.TabIndex = 4;
            this.chkMatchCase.Text = "Match case";
            this.chkMatchCase.UseVisualStyleBackColor = true;
            // 
            // chkUseRegex
            // 
            this.chkUseRegex.AutoSize = true;
            this.chkUseRegex.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.chkUseRegex.Location = new System.Drawing.Point(215, 77);
            this.chkUseRegex.Name = "chkUseRegex";
            this.chkUseRegex.Size = new System.Drawing.Size(81, 19);
            this.chkUseRegex.TabIndex = 5;
            this.chkUseRegex.Text = "Use RegEx";
            this.chkUseRegex.UseVisualStyleBackColor = true;
            // 
            // lnkRegexHelp
            // 
            this.lnkRegexHelp.AutoSize = true;
            this.lnkRegexHelp.Font = new System.Drawing.Font("Segoe UI", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lnkRegexHelp.Location = new System.Drawing.Point(232, 97);
            this.lnkRegexHelp.Name = "lnkRegexHelp";
            this.lnkRegexHelp.Size = new System.Drawing.Size(76, 13);
            this.lnkRegexHelp.TabIndex = 6;
            this.lnkRegexHelp.TabStop = true;
            this.lnkRegexHelp.Text = "(Online Help)";
            this.lnkRegexHelp.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.lnkRegexHelp_LinkClicked);
            // 
            // grpScope
            // 
            this.grpScope.Controls.Add(this.chkSearchKey);
            this.grpScope.Controls.Add(this.chkSearchValue);
            this.grpScope.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.grpScope.Location = new System.Drawing.Point(15, 118);
            this.grpScope.Name = "grpScope";
            this.grpScope.Size = new System.Drawing.Size(335, 52);
            this.grpScope.TabIndex = 7;
            this.grpScope.TabStop = false;
            this.grpScope.Text = "Search Scope";
            // 
            // chkSearchKey
            // 
            this.chkSearchKey.AutoSize = true;
            this.chkSearchKey.Checked = true;
            this.chkSearchKey.CheckState = System.Windows.Forms.CheckState.Checked;
            this.chkSearchKey.Location = new System.Drawing.Point(15, 22);
            this.chkSearchKey.Name = "chkSearchKey";
            this.chkSearchKey.Size = new System.Drawing.Size(85, 19);
            this.chkSearchKey.TabIndex = 0;
            this.chkSearchKey.Text = "Label (Key)";
            this.chkSearchKey.UseVisualStyleBackColor = true;
            // 
            // chkSearchValue
            // 
            this.chkSearchValue.AutoSize = true;
            this.chkSearchValue.Checked = true;
            this.chkSearchValue.CheckState = System.Windows.Forms.CheckState.Checked;
            this.chkSearchValue.Location = new System.Drawing.Point(145, 22);
            this.chkSearchValue.Name = "chkSearchValue";
            this.chkSearchValue.Size = new System.Drawing.Size(78, 19);
            this.chkSearchValue.TabIndex = 1;
            this.chkSearchValue.Text = "Text Value";
            this.chkSearchValue.UseVisualStyleBackColor = true;
            // 
            // btnFindNext
            // 
            this.btnFindNext.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.btnFindNext.Location = new System.Drawing.Point(365, 14);
            this.btnFindNext.Name = "btnFindNext";
            this.btnFindNext.Size = new System.Drawing.Size(100, 26);
            this.btnFindNext.TabIndex = 8;
            this.btnFindNext.Text = "Find Next";
            this.btnFindNext.UseVisualStyleBackColor = true;
            this.btnFindNext.Click += new System.EventHandler(this.btnFindNext_Click);
            // 
            // btnReplace
            // 
            this.btnReplace.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.btnReplace.Location = new System.Drawing.Point(365, 44);
            this.btnReplace.Name = "btnReplace";
            this.btnReplace.Size = new System.Drawing.Size(100, 26);
            this.btnReplace.TabIndex = 9;
            this.btnReplace.Text = "Replace";
            this.btnReplace.UseVisualStyleBackColor = true;
            this.btnReplace.Click += new System.EventHandler(this.btnReplace_Click);
            // 
            // btnReplaceAll
            // 
            this.btnReplaceAll.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.btnReplaceAll.Location = new System.Drawing.Point(365, 74);
            this.btnReplaceAll.Name = "btnReplaceAll";
            this.btnReplaceAll.Size = new System.Drawing.Size(100, 26);
            this.btnReplaceAll.TabIndex = 10;
            this.btnReplaceAll.Text = "Replace All";
            this.btnReplaceAll.UseVisualStyleBackColor = true;
            this.btnReplaceAll.Click += new System.EventHandler(this.btnReplaceAll_Click);
            // 
            // btnClose
            // 
            this.btnClose.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnClose.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.btnClose.Location = new System.Drawing.Point(365, 144);
            this.btnClose.Name = "btnClose";
            this.btnClose.Size = new System.Drawing.Size(100, 26);
            this.btnClose.TabIndex = 11;
            this.btnClose.Text = "Close";
            this.btnClose.UseVisualStyleBackColor = true;
            this.btnClose.Click += new System.EventHandler(this.btnClose_Click);
            // 
            // FindReplaceDialog
            // 
            this.AcceptButton = this.btnFindNext;
            this.CancelButton = this.btnClose;
            this.ClientSize = new System.Drawing.Size(480, 184);
            this.Controls.Add(this.lnkRegexHelp);
            this.Controls.Add(this.btnClose);
            this.Controls.Add(this.btnReplaceAll);
            this.Controls.Add(this.btnReplace);
            this.Controls.Add(this.btnFindNext);
            this.Controls.Add(this.grpScope);
            this.Controls.Add(this.chkUseRegex);
            this.Controls.Add(this.chkMatchCase);
            this.Controls.Add(this.cboReplace);
            this.Controls.Add(this.cboFind);
            this.Controls.Add(this.lblReplace);
            this.Controls.Add(this.lblFind);
            this.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "FindReplaceDialog";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Find & Replace";
            this.ShowIcon = false;
            this.grpScope.ResumeLayout(false);
            this.grpScope.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        #endregion

        private System.Windows.Forms.Label lblFind;
        private System.Windows.Forms.Label lblReplace;
        private System.Windows.Forms.ComboBox cboFind;
        private System.Windows.Forms.ComboBox cboReplace;
        private System.Windows.Forms.CheckBox chkMatchCase;
        private System.Windows.Forms.CheckBox chkUseRegex;
        private System.Windows.Forms.LinkLabel lnkRegexHelp;
        private System.Windows.Forms.GroupBox grpScope;
        private System.Windows.Forms.CheckBox chkSearchKey;
        private System.Windows.Forms.CheckBox chkSearchValue;
        private System.Windows.Forms.Button btnFindNext;
        private System.Windows.Forms.Button btnReplace;
        private System.Windows.Forms.Button btnReplaceAll;
        private System.Windows.Forms.Button btnClose;
    }
}
