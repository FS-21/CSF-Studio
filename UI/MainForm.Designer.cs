namespace CsfStudio.UI
{
    partial class MainForm
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
            this.tabBackups = new System.Windows.Forms.TabPage();
            this.tabKeyEditor = new System.Windows.Forms.TabPage();
            this.colUnsavedTime = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.menuStrip1 = new System.Windows.Forms.MenuStrip();
            this.menuFile = new System.Windows.Forms.ToolStripMenuItem();
            this.menuNew = new System.Windows.Forms.ToolStripMenuItem();
            this.menuOpen = new System.Windows.Forms.ToolStripMenuItem();
            this.menuOpenSession = new System.Windows.Forms.ToolStripMenuItem();
            this.menuRecentSessions = new System.Windows.Forms.ToolStripMenuItem();
            this.sep1 = new System.Windows.Forms.ToolStripSeparator();
            this.menuSave = new System.Windows.Forms.ToolStripMenuItem();
            this.menuSaveSingleFile = new System.Windows.Forms.ToolStripMenuItem();
            this.menuSaveAs = new System.Windows.Forms.ToolStripMenuItem();
            this.sep2 = new System.Windows.Forms.ToolStripSeparator();
            this.menuExportTxt = new System.Windows.Forms.ToolStripMenuItem();
            this.menuExportKeysOnly = new System.Windows.Forms.ToolStripMenuItem();
            this.menuImportTxt = new System.Windows.Forms.ToolStripMenuItem();
            this.sep3 = new System.Windows.Forms.ToolStripSeparator();
            this.menuExit = new System.Windows.Forms.ToolStripMenuItem();
            this.menuEdit = new System.Windows.Forms.ToolStripMenuItem();
            this.menuUndo = new System.Windows.Forms.ToolStripMenuItem();
            this.menuRedo = new System.Windows.Forms.ToolStripMenuItem();
            this.sepUndo = new System.Windows.Forms.ToolStripSeparator();
            this.menuCut = new System.Windows.Forms.ToolStripMenuItem();
            this.menuCopy = new System.Windows.Forms.ToolStripMenuItem();
            this.menuPaste = new System.Windows.Forms.ToolStripMenuItem();
            this.menuSelectAll = new System.Windows.Forms.ToolStripMenuItem();
            this.menuInvertSelection = new System.Windows.Forms.ToolStripMenuItem();
            this.sepClipboard = new System.Windows.Forms.ToolStripSeparator();
            this.menuAddLabel = new System.Windows.Forms.ToolStripMenuItem();
            this.menuDeleteLabel = new System.Windows.Forms.ToolStripMenuItem();
            this.menuBatchRename = new System.Windows.Forms.ToolStripMenuItem();
            this.menuTrimSpaces = new System.Windows.Forms.ToolStripMenuItem();
            this.sep4 = new System.Windows.Forms.ToolStripSeparator();
            this.menuRenameFileLabel = new System.Windows.Forms.ToolStripMenuItem();
            this.menuChangeHeaderLangId = new System.Windows.Forms.ToolStripMenuItem();
            this.menuSetTranslationContentLang = new System.Windows.Forms.ToolStripMenuItem();
            this.menuDuplicateKey = new System.Windows.Forms.ToolStripMenuItem();
            this.menuCapitalization = new System.Windows.Forms.ToolStripMenuItem();
            this.menuUpper = new System.Windows.Forms.ToolStripMenuItem();
            this.menuLower = new System.Windows.Forms.ToolStripMenuItem();
            this.menuTitle = new System.Windows.Forms.ToolStripMenuItem();
            this.menuSentence = new System.Windows.Forms.ToolStripMenuItem();
            this.menuMoveUp = new System.Windows.Forms.ToolStripMenuItem();
            this.menuMoveDown = new System.Windows.Forms.ToolStripMenuItem();
            this.sepMoveKeys = new System.Windows.Forms.ToolStripSeparator();
            this.menuJumpNextEmpty = new System.Windows.Forms.ToolStripMenuItem();
            this.menuJumpPrevEmpty = new System.Windows.Forms.ToolStripMenuItem();
            this.sepNavigation = new System.Windows.Forms.ToolStripSeparator();
            this.menuFindReplace = new System.Windows.Forms.ToolStripMenuItem();
            this.sepOptions = new System.Windows.Forms.ToolStripSeparator();
            this.menuOptions = new System.Windows.Forms.ToolStripMenuItem();
            this.sepTools1 = new System.Windows.Forms.ToolStripSeparator();
            this.sepTools2 = new System.Windows.Forms.ToolStripSeparator();
            this.menuSyncKeys = new System.Windows.Forms.ToolStripMenuItem();
            this.menuSyncAudioWavs = new System.Windows.Forms.ToolStripMenuItem();
            this.menuSortBinary = new System.Windows.Forms.ToolStripMenuItem();
            this.menuTools = new System.Windows.Forms.ToolStripMenuItem();
            this.menuScanIni = new System.Windows.Forms.ToolStripMenuItem();
            this.menuConvertAnsi = new System.Windows.Forms.ToolStripMenuItem();
            this.menuClearValuesKeepKeys = new System.Windows.Forms.ToolStripMenuItem();
            this.menuHelp = new System.Windows.Forms.ToolStripMenuItem();
            this.menuGitHubRepo = new System.Windows.Forms.ToolStripMenuItem();
            this.sepHelp1 = new System.Windows.Forms.ToolStripSeparator();
            this.menuAbout = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStrip1 = new System.Windows.Forms.ToolStrip();
            this.btnAddKeyToolbar = new System.Windows.Forms.ToolStripButton();
            this.btnDeleteKeyToolbar = new System.Windows.Forms.ToolStripButton();
            this.btnDuplicateKeyToolbar = new System.Windows.Forms.ToolStripButton();
            this.btnApplyRename = new System.Windows.Forms.Button();
            this.sep6 = new System.Windows.Forms.ToolStripSeparator();
            this.btnKeyFilterMode = new System.Windows.Forms.ToolStripButton();
            this.cboSearchKey = new System.Windows.Forms.ToolStripComboBox();
            this.btnValFilterMode = new System.Windows.Forms.ToolStripButton();
            this.cboSearchValue = new System.Windows.Forms.ToolStripComboBox();
            this.btnFilterLogic = new System.Windows.Forms.ToolStripButton();
            this.sep7 = new System.Windows.Forms.ToolStripSeparator();
            this.cboStatusFilter = new System.Windows.Forms.ToolStripComboBox();
            this.statusStrip1 = new System.Windows.Forms.StatusStrip();
            this.lblStatusCount = new System.Windows.Forms.ToolStripStatusLabel();
            this.lblSessionMode = new System.Windows.Forms.ToolStripStatusLabel();
            this.lblSaveNotification = new System.Windows.Forms.ToolStripStatusLabel();
            this.tabControlMain = new System.Windows.Forms.TabControl();
            this.tabMaster = new System.Windows.Forms.TabPage();
            this.splitMain = new System.Windows.Forms.SplitContainer();
            this.tvCategories = new System.Windows.Forms.TreeView();
            this.splitMasterDetail = new System.Windows.Forms.SplitContainer();
            this.gridLabels = new System.Windows.Forms.DataGridView();
            this.pnlDetailHeader = new System.Windows.Forms.Panel();
            this.lblCurrentKey = new System.Windows.Forms.Label();
            this.txtCurrentKeyName = new System.Windows.Forms.TextBox();
            this.lblCurrentWav = new System.Windows.Forms.Label();
            this.txtCurrentExtraWav = new System.Windows.Forms.TextBox();
            this.pnlLanguageEditors = new System.Windows.Forms.Panel();
            this.tabUnsaved = new System.Windows.Forms.TabPage();
            this.gridUnsaved = new System.Windows.Forms.DataGridView();
            this.colUnsavedCheck = new System.Windows.Forms.DataGridViewCheckBoxColumn();
            this.colUnsavedKey = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colUnsavedCat = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colUnsavedState = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.tabRecent = new System.Windows.Forms.TabPage();
            this.gridRecent = new System.Windows.Forms.DataGridView();
            this.colRecentKey = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colRecentCat = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colRecentTime = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.tabCoverage = new System.Windows.Forms.TabPage();
            this.pnlCoverageHeader = new System.Windows.Forms.Panel();
            this.gridCoverage = new System.Windows.Forms.DataGridView();
            this.colCovKey = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colCovStatus = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colCovPercent = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.menuStrip1.SuspendLayout();
            this.toolStrip1.SuspendLayout();
            this.statusStrip1.SuspendLayout();
            this.tabControlMain.SuspendLayout();
            this.tabMaster.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitMain)).BeginInit();
            this.splitMain.Panel1.SuspendLayout();
            this.splitMain.Panel2.SuspendLayout();
            this.splitMain.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitMasterDetail)).BeginInit();
            this.splitMasterDetail.Panel1.SuspendLayout();
            this.splitMasterDetail.Panel2.SuspendLayout();
            this.splitMasterDetail.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.gridLabels)).BeginInit();
            this.pnlDetailHeader.SuspendLayout();
            this.tabUnsaved.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.gridUnsaved)).BeginInit();
            this.tabRecent.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.gridRecent)).BeginInit();
            this.SuspendLayout();
            // 
            // menuStrip1
            // 
            this.menuStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.menuFile,
            this.menuEdit,
            this.menuTools,
            this.menuHelp});
            this.menuStrip1.Location = new System.Drawing.Point(0, 0);
            this.menuStrip1.Name = "menuStrip1";
            this.menuStrip1.Size = new System.Drawing.Size(1020, 24);
            this.menuStrip1.TabIndex = 0;
            // 
            // menuFile
            // 
            this.menuFile.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.menuNew,
            this.menuOpen,
            this.menuOpenSession,
            this.menuRecentSessions,
            this.sep1,
            this.menuSave,
            this.menuSaveSingleFile,
            this.menuSaveAs,
            this.sep2,
            this.menuExportTxt,
            this.menuExportKeysOnly,
            this.menuImportTxt,
            this.sep3,
            this.menuExit});
            this.menuFile.Name = "menuFile";
            this.menuFile.Size = new System.Drawing.Size(37, 20);
            this.menuFile.Text = "&File";
            // 
            // menuNew
            // 
            this.menuNew.Name = "menuNew";
            this.menuNew.ShortcutKeys = ((System.Windows.Forms.Keys)(((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.Shift) 
            | System.Windows.Forms.Keys.N)));
            this.menuNew.Size = new System.Drawing.Size(250, 22);
            this.menuNew.Text = "&New CSF";
            this.menuNew.Click += new System.EventHandler(this.menuNew_Click);
            // 
            // menuOpen
            // 
            this.menuOpen.Name = "menuOpen";
            this.menuOpen.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.O)));
            this.menuOpen.Size = new System.Drawing.Size(250, 22);
            this.menuOpen.Text = "&Open CSF...";
            this.menuOpen.Click += new System.EventHandler(this.menuOpen_Click);
            // 
            // menuOpenSession
            // 
            this.menuOpenSession.Name = "menuOpenSession";
            this.menuOpenSession.ShortcutKeys = ((System.Windows.Forms.Keys)(((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.Shift) 
            | System.Windows.Forms.Keys.O)));
            this.menuOpenSession.Size = new System.Drawing.Size(250, 22);
            this.menuOpenSession.Text = "Open &Multiple CSF...";
            this.menuOpenSession.Click += new System.EventHandler(this.menuOpenSession_Click);
            // 
            // menuRecentSessions
            // 
            this.menuRecentSessions.Name = "menuRecentSessions";
            this.menuRecentSessions.Size = new System.Drawing.Size(250, 22);
            this.menuRecentSessions.Text = "Open &Recent Sessions";
            // 
            // sep1
            // 
            this.sep1.Name = "sep1";
            this.sep1.Size = new System.Drawing.Size(247, 6);
            // 
            // menuSave
            // 
            this.menuSave.Name = "menuSave";
            this.menuSave.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.S)));
            this.menuSave.Size = new System.Drawing.Size(250, 22);
            this.menuSave.Text = "&Save All";
            this.menuSave.Click += new System.EventHandler(this.menuSave_Click);
            // 
            // menuSaveSingleFile
            // 
            this.menuSaveSingleFile.Name = "menuSaveSingleFile";
            this.menuSaveSingleFile.Size = new System.Drawing.Size(250, 22);
            this.menuSaveSingleFile.Text = "Save &Specific File...";
            // 
            // menuSaveAs
            // 
            this.menuSaveAs.Name = "menuSaveAs";
            this.menuSaveAs.Size = new System.Drawing.Size(250, 22);
            this.menuSaveAs.Text = "Save &As...";
            this.menuSaveAs.Click += new System.EventHandler(this.menuSaveAs_Click);
            // 
            // sep2
            // 
            this.sep2.Name = "sep2";
            this.sep2.Size = new System.Drawing.Size(247, 6);
            // 
            // menuExportTxt
            // 
            this.menuExportTxt.Name = "menuExportTxt";
            this.menuExportTxt.Size = new System.Drawing.Size(250, 22);
            this.menuExportTxt.Text = "Export to Plain Text UTF-8...";
            this.menuExportTxt.Click += new System.EventHandler(this.menuExportTxt_Click);
            // 
            // menuExportKeysOnly
            // 
            this.menuExportKeysOnly.Name = "menuExportKeysOnly";
            this.menuExportKeysOnly.Size = new System.Drawing.Size(250, 22);
            this.menuExportKeysOnly.Text = "Export Key Structure Only (Keys Without Text)...";
            this.menuExportKeysOnly.Click += new System.EventHandler(this.menuExportKeysOnly_Click);
            // 
            // menuImportTxt
            // 
            this.menuImportTxt.Name = "menuImportTxt";
            this.menuImportTxt.Size = new System.Drawing.Size(250, 22);
            this.menuImportTxt.Text = "Import from Plain Text UTF-8...";
            this.menuImportTxt.Click += new System.EventHandler(this.menuImportTxt_Click);
            // 
            // sep3
            // 
            this.sep3.Name = "sep3";
            this.sep3.Size = new System.Drawing.Size(247, 6);
            // 
            // menuExit
            // 
            this.menuExit.Name = "menuExit";
            this.menuExit.Size = new System.Drawing.Size(250, 22);
            this.menuExit.Text = "E&xit";
            this.menuExit.Click += new System.EventHandler(this.menuExit_Click);
            // 
            // menuEdit
            // 
            this.menuEdit.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.menuUndo,
            this.menuRedo,
            this.sepUndo,
            this.menuCut,
            this.menuCopy,
            this.menuPaste,
            this.menuSelectAll,
            this.menuInvertSelection,
            this.sepClipboard,
            this.menuAddLabel,
            this.menuDuplicateKey,
            this.menuDeleteLabel,
            this.sep4,
            this.menuMoveUp,
            this.menuMoveDown,
            this.menuRenameFileLabel,
            this.menuChangeHeaderLangId,
            this.menuSetTranslationContentLang,
            this.sepMoveKeys,
            this.menuCapitalization,
            this.menuTrimSpaces,
            this.sepNavigation,
            this.menuFindReplace,
            this.menuJumpNextEmpty,
            this.menuJumpPrevEmpty,
            this.sepOptions,
            this.menuOptions});
            this.menuEdit.Name = "menuEdit";
            this.menuEdit.Size = new System.Drawing.Size(39, 20);
            this.menuEdit.Text = "&Edit";
            // 
            // menuUndo
            // 
            this.menuUndo.Name = "menuUndo";
            this.menuUndo.ShortcutKeyDisplayString = "Ctrl+Z";
            this.menuUndo.Size = new System.Drawing.Size(235, 22);
            this.menuUndo.Text = "↩️ &Undo";
            this.menuUndo.Click += new System.EventHandler(this.menuUndo_Click);
            // 
            // menuRedo
            // 
            this.menuRedo.Name = "menuRedo";
            this.menuRedo.ShortcutKeyDisplayString = "Ctrl+Y";
            this.menuRedo.Size = new System.Drawing.Size(235, 22);
            this.menuRedo.Text = "↪️ &Redo";
            this.menuRedo.Click += new System.EventHandler(this.menuRedo_Click);
            // 
            // sepUndo
            // 
            this.sepUndo.Name = "sepUndo";
            this.sepUndo.Size = new System.Drawing.Size(232, 6);
            // 
            // menuCut
            // 
            this.menuCut.Name = "menuCut";
            this.menuCut.ShortcutKeyDisplayString = "Ctrl+X";
            this.menuCut.Size = new System.Drawing.Size(235, 22);
            this.menuCut.Text = "✂️ Cu&t";
            this.menuCut.Click += new System.EventHandler(this.menuCut_Click);
            // 
            // menuCopy
            // 
            this.menuCopy.Name = "menuCopy";
            this.menuCopy.ShortcutKeyDisplayString = "Ctrl+C";
            this.menuCopy.Size = new System.Drawing.Size(235, 22);
            this.menuCopy.Text = "📋 &Copy";
            this.menuCopy.Click += new System.EventHandler(this.menuCopy_Click);
            // 
            // menuPaste
            // 
            this.menuPaste.Name = "menuPaste";
            this.menuPaste.ShortcutKeyDisplayString = "Ctrl+V";
            this.menuPaste.Size = new System.Drawing.Size(235, 22);
            this.menuPaste.Text = "📋 &Paste";
            this.menuPaste.Click += new System.EventHandler(this.menuPaste_Click);
            // 
            // menuSelectAll
            // 
            this.menuSelectAll.Name = "menuSelectAll";
            this.menuSelectAll.ShortcutKeyDisplayString = "Ctrl+A";
            this.menuSelectAll.Size = new System.Drawing.Size(260, 22);
            this.menuSelectAll.Text = "☑️ Select &All";
            this.menuSelectAll.Click += new System.EventHandler(this.menuSelectAll_Click);
            // 
            // menuInvertSelection
            // 
            this.menuInvertSelection.Name = "menuInvertSelection";
            this.menuInvertSelection.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.I)));
            this.menuInvertSelection.Size = new System.Drawing.Size(260, 22);
            this.menuInvertSelection.Text = "🔄 &Invert Selection";
            this.menuInvertSelection.Click += new System.EventHandler(this.menuInvertSelection_Click);
            // 
            // sepClipboard
            // 
            this.sepClipboard.Name = "sepClipboard";
            this.sepClipboard.Size = new System.Drawing.Size(232, 6);
            // 
            // menuAddLabel
            // 
            this.menuAddLabel.Name = "menuAddLabel";
            this.menuAddLabel.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.N)));
            this.menuAddLabel.Size = new System.Drawing.Size(260, 22);
            this.menuAddLabel.Text = "➕ &Add New Key";
            this.menuAddLabel.Click += new System.EventHandler(this.menuAddLabel_Click);
            // 
            // menuDuplicateKey
            // 
            this.menuDuplicateKey.Name = "menuDuplicateKey";
            this.menuDuplicateKey.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.D)));
            this.menuDuplicateKey.Size = new System.Drawing.Size(260, 22);
            this.menuDuplicateKey.Text = "📋 &Duplicate Key(s)...";
            this.menuDuplicateKey.Click += new System.EventHandler(this.menuDuplicateKey_Click);
            // 
            // menuDeleteLabel
            // 
            this.menuDeleteLabel.Name = "menuDeleteLabel";
            this.menuDeleteLabel.ShortcutKeys = System.Windows.Forms.Keys.Delete;
            this.menuDeleteLabel.Size = new System.Drawing.Size(260, 22);
            this.menuDeleteLabel.Text = "❌ &Delete Key";
            this.menuDeleteLabel.Click += new System.EventHandler(this.menuDeleteLabel_Click);
            // 
            // menuBatchRename
            // 
            this.menuBatchRename.Name = "menuBatchRename";
            this.menuBatchRename.Size = new System.Drawing.Size(260, 22);
            this.menuBatchRename.Text = "✏️ Batch Rename Keys...";
            this.menuBatchRename.Click += new System.EventHandler(this.menuBatchRename_Click);
            // 
            // menuCapitalization
            // 
            this.menuCapitalization.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.menuUpper,
            this.menuLower,
            this.menuTitle,
            this.menuSentence});
            this.menuCapitalization.Name = "menuCapitalization";
            this.menuCapitalization.Size = new System.Drawing.Size(260, 22);
            this.menuCapitalization.Text = "🔤 &Key Capitalization";
            // 
            // menuUpper
            // 
            this.menuUpper.Name = "menuUpper";
            this.menuUpper.Size = new System.Drawing.Size(260, 22);
            this.menuUpper.Text = "UPPERCASE (GUI:BUILD)";
            this.menuUpper.Click += (s, e) => PerformCapitalization("UPPER");
            // 
            // menuLower
            // 
            this.menuLower.Name = "menuLower";
            this.menuLower.Size = new System.Drawing.Size(260, 22);
            this.menuLower.Text = "lowercase (gui:build)";
            this.menuLower.Click += (s, e) => PerformCapitalization("LOWER");
            // 
            // menuTitle
            // 
            this.menuTitle.Name = "menuTitle";
            this.menuTitle.Size = new System.Drawing.Size(260, 22);
            this.menuTitle.Text = "Title Case (Gui:Build)";
            this.menuTitle.Click += (s, e) => PerformCapitalization("TITLE");
            // 
            // menuSentence
            // 
            this.menuSentence.Name = "menuSentence";
            this.menuSentence.Size = new System.Drawing.Size(260, 22);
            this.menuSentence.Text = "Sentence case (Gui: build. Text here.)";
            this.menuSentence.Click += (s, e) => PerformCapitalization("SENTENCE");
            // 
            // menuTrimSpaces
            // 
            this.menuTrimSpaces.Name = "menuTrimSpaces";
            this.menuTrimSpaces.Size = new System.Drawing.Size(260, 22);
            this.menuTrimSpaces.Text = "✂️ Trim Spaces";
            this.menuTrimSpaces.Click += new System.EventHandler(this.menuTrimSpaces_Click);
            // 
            // sep4
            // 
            this.sep4.Name = "sep4";
            this.sep4.Size = new System.Drawing.Size(257, 6);
            // 
            // menuRenameFileLabel
            // 
            this.menuRenameFileLabel.Name = "menuRenameFileLabel";
            this.menuRenameFileLabel.Size = new System.Drawing.Size(260, 22);
            this.menuRenameFileLabel.Text = "🏷️ Rename Active File Label / Title...";
            this.menuRenameFileLabel.Click += new System.EventHandler(this.menuRenameFileLabel_Click);
            // 
            // menuChangeHeaderLangId
            // 
            this.menuChangeHeaderLangId.Name = "menuChangeHeaderLangId";
            this.menuChangeHeaderLangId.Size = new System.Drawing.Size(260, 22);
            this.menuChangeHeaderLangId.Text = "🌐 Change Header Language ID (Offset 0x14)...";
            this.menuChangeHeaderLangId.Click += new System.EventHandler(this.menuChangeHeaderLangId_Click);
            // 
            // menuSetTranslationContentLang
            // 
            this.menuSetTranslationContentLang.Name = "menuSetTranslationContentLang";
            this.menuSetTranslationContentLang.Size = new System.Drawing.Size(260, 22);
            this.menuSetTranslationContentLang.Text = "🌍 &Set Translation Content Language...";
            // 
            // menuMoveUp
            // 
            this.menuMoveUp.Name = "menuMoveUp";
            this.menuMoveUp.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Alt | System.Windows.Forms.Keys.Up)));
            this.menuMoveUp.Size = new System.Drawing.Size(260, 22);
            this.menuMoveUp.Text = "⬆️ Move Key Up";
            this.menuMoveUp.Click += (s, e) => PerformMoveKey(-1);
            // 
            // menuMoveDown
            // 
            this.menuMoveDown.Name = "menuMoveDown";
            this.menuMoveDown.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Alt | System.Windows.Forms.Keys.Down)));
            this.menuMoveDown.Size = new System.Drawing.Size(260, 22);
            this.menuMoveDown.Text = "⬇️ Move Key Down";
            this.menuMoveDown.Click += (s, e) => PerformMoveKey(1);
            // 
            // sepMoveKeys
            // 
            this.sepMoveKeys.Name = "sepMoveKeys";
            this.sepMoveKeys.Size = new System.Drawing.Size(257, 6);
            // 
            // menuJumpNextEmpty
            // 
            this.menuJumpNextEmpty.Name = "menuJumpNextEmpty";
            this.menuJumpNextEmpty.ShortcutKeys = ((System.Windows.Forms.Keys)(((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.Shift) | System.Windows.Forms.Keys.Down)));
            this.menuJumpNextEmpty.Size = new System.Drawing.Size(260, 22);
            this.menuJumpNextEmpty.Text = "⏭️ Jump to Next Empty Key";
            this.menuJumpNextEmpty.Click += (s, e) => JumpToNextEmptyKey(true);
            // 
            // menuJumpPrevEmpty
            // 
            this.menuJumpPrevEmpty.Name = "menuJumpPrevEmpty";
            this.menuJumpPrevEmpty.ShortcutKeys = ((System.Windows.Forms.Keys)(((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.Shift) | System.Windows.Forms.Keys.Up)));
            this.menuJumpPrevEmpty.Size = new System.Drawing.Size(260, 22);
            this.menuJumpPrevEmpty.Text = "⏮️ Jump to Previous Empty Key";
            this.menuJumpPrevEmpty.Click += (s, e) => JumpToNextEmptyKey(false);
            // 
            // sepNavigation
            // 
            this.sepNavigation.Name = "sepNavigation";
            this.sepNavigation.Size = new System.Drawing.Size(257, 6);
            // 
            // menuFindReplace
            // 
            this.menuFindReplace.Name = "menuFindReplace";
            this.menuFindReplace.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.F)));
            this.menuFindReplace.Size = new System.Drawing.Size(260, 22);
            this.menuFindReplace.Text = "&Find && Replace...";
            this.menuFindReplace.Click += new System.EventHandler(this.menuFindReplace_Click);
            // 
            // sepOptions
            // 
            this.sepOptions.Name = "sepOptions";
            this.sepOptions.Size = new System.Drawing.Size(257, 6);
            // 
            // menuOptions
            // 
            this.menuOptions.Name = "menuOptions";
            this.menuOptions.Size = new System.Drawing.Size(260, 22);
            this.menuOptions.Text = "⚙️ &Options...";
            this.menuOptions.Click += new System.EventHandler(this.menuOptions_Click);
            // 
            // menuSyncKeys
            // 
            this.menuSyncKeys.Name = "menuSyncKeys";
            this.menuSyncKeys.Size = new System.Drawing.Size(275, 22);
            this.menuSyncKeys.Text = "⚡ Synchronize Keys Across All Files";
            this.menuSyncKeys.Click += new System.EventHandler(this.menuSyncKeys_Click);
            // 
            // menuSyncAudioWavs
            // 
            this.menuSyncAudioWavs.Name = "menuSyncAudioWavs";
            this.menuSyncAudioWavs.Size = new System.Drawing.Size(275, 22);
            this.menuSyncAudioWavs.Text = "🎵 Synchronize Audio WAVs from Main CSF file";
            this.menuSyncAudioWavs.Click += new System.EventHandler(this.menuSyncAudioWavs_Click);
            // 
            // sepTools1
            // 
            this.sepTools1.Name = "sepTools1";
            this.sepTools1.Size = new System.Drawing.Size(272, 6);
            // 
            // sepTools2
            // 
            this.sepTools2.Name = "sepTools2";
            this.sepTools2.Size = new System.Drawing.Size(272, 6);
            // 
            // menuSortBinary
            // 
            this.menuSortBinary.Name = "menuSortBinary";
            this.menuSortBinary.Size = new System.Drawing.Size(275, 22);
            this.menuSortBinary.Text = "🔢 Reorder Keys by Main CSF File Sequence";
            this.menuSortBinary.Click += new System.EventHandler(this.menuSortBinary_Click);
            // 
            // menuTools
            // 
            this.menuTools.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.menuBatchRename,
            this.menuSortBinary,
            this.menuConvertAnsi,
            this.sepTools1,
            this.menuSyncKeys,
            this.menuSyncAudioWavs,
            this.menuScanIni,
            this.sepTools2,
            this.menuClearValuesKeepKeys});
            this.menuTools.Name = "menuTools";
            this.menuTools.Size = new System.Drawing.Size(46, 20);
            this.menuTools.Text = "&Tools";
            // 
            // menuScanIni
            // 
            this.menuScanIni.Name = "menuScanIni";
            this.menuScanIni.Size = new System.Drawing.Size(275, 22);
            this.menuScanIni.Text = "🔍 Scan Mod INI / MAP Files...";
            this.menuScanIni.Click += new System.EventHandler(this.menuScanIni_Click);

            // 
            // menuConvertAnsi
            // 
            this.menuConvertAnsi.Name = "menuConvertAnsi";
            this.menuConvertAnsi.Size = new System.Drawing.Size(275, 22);
            this.menuConvertAnsi.Text = "🔤 Convert ANSI / Codepage Text to Unicode...";
            this.menuConvertAnsi.Click += new System.EventHandler(this.menuConvertAnsi_Click);
            // 
            // menuClearValuesKeepKeys
            // 
            this.menuClearValuesKeepKeys.Name = "menuClearValuesKeepKeys";
            this.menuClearValuesKeepKeys.Size = new System.Drawing.Size(275, 22);
            this.menuClearValuesKeepKeys.Text = "🧹 Clear Text && Audio (Keep Key Structure)...";
            this.menuClearValuesKeepKeys.Click += new System.EventHandler(this.menuClearValuesKeepKeys_Click);
            // 
            // menuHelp
            // 
            this.menuHelp.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.menuGitHubRepo,
            this.sepHelp1,
            this.menuAbout});
            this.menuHelp.Name = "menuHelp";
            this.menuHelp.Size = new System.Drawing.Size(44, 20);
            this.menuHelp.Text = "&Help";
            // 
            // menuGitHubRepo
            // 
            this.menuGitHubRepo.Name = "menuGitHubRepo";
            this.menuGitHubRepo.Size = new System.Drawing.Size(240, 22);
            this.menuGitHubRepo.Text = "🌐 GitHub Repository...";
            this.menuGitHubRepo.Click += new System.EventHandler(this.menuGitHubRepo_Click);
            // 
            // sepHelp1
            // 
            this.sepHelp1.Name = "sepHelp1";
            this.sepHelp1.Size = new System.Drawing.Size(237, 6);
            // 
            // menuAbout
            // 
            this.menuAbout.Name = "menuAbout";
            this.menuAbout.Size = new System.Drawing.Size(240, 22);
            this.menuAbout.Text = "ℹ️ &About CSF Studio...";
            this.menuAbout.Click += new System.EventHandler(this.menuAbout_Click);
            // 
            // toolStrip1
            // 
            this.toolStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.cboStatusFilter,
            this.sep7,
            this.btnKeyFilterMode,
            this.cboSearchKey,
            this.btnFilterLogic,
            this.btnValFilterMode,
            this.cboSearchValue});
            this.toolStrip1.GripStyle = System.Windows.Forms.ToolStripGripStyle.Hidden;
            this.toolStrip1.Location = new System.Drawing.Point(0, 24);
            this.toolStrip1.Name = "toolStrip1";
            this.toolStrip1.Padding = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.toolStrip1.Size = new System.Drawing.Size(1020, 26);
            this.toolStrip1.TabIndex = 1;
            // 
            // toolStrip2 (Key Actions toolbar)
            // 
            this.toolStrip2 = new System.Windows.Forms.ToolStrip();
            this.sepToolbarNav = new System.Windows.Forms.ToolStripSeparator();
            this.btnJumpPrevEmptyToolbar = new System.Windows.Forms.ToolStripButton();
            this.btnJumpNextEmptyToolbar = new System.Windows.Forms.ToolStripButton();
            this.toolStrip2.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.btnAddKeyToolbar,
            this.btnDuplicateKeyToolbar,
            this.btnDeleteKeyToolbar,
            this.sepToolbarNav,
            this.btnJumpPrevEmptyToolbar,
            this.btnJumpNextEmptyToolbar});
            this.toolStrip2.GripStyle = System.Windows.Forms.ToolStripGripStyle.Hidden;
            this.toolStrip2.Location = new System.Drawing.Point(0, 50);
            this.toolStrip2.Name = "toolStrip2";
            this.toolStrip2.Padding = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.toolStrip2.Size = new System.Drawing.Size(1020, 26);
            this.toolStrip2.TabIndex = 15;
            // 
            // btnAddKeyToolbar
            // 
            this.btnAddKeyToolbar.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.btnAddKeyToolbar.Name = "btnAddKeyToolbar";
            this.btnAddKeyToolbar.Size = new System.Drawing.Size(75, 22);
            this.btnAddKeyToolbar.Text = "➕ Add Key";
            this.btnAddKeyToolbar.Click += new System.EventHandler(this.menuAddLabel_Click);
            // 
            // btnDuplicateKeyToolbar
            // 
            this.btnDuplicateKeyToolbar.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.btnDuplicateKeyToolbar.Name = "btnDuplicateKeyToolbar";
            this.btnDuplicateKeyToolbar.Size = new System.Drawing.Size(95, 22);
            this.btnDuplicateKeyToolbar.Text = "📋 Duplicate Key";
            this.btnDuplicateKeyToolbar.Click += new System.EventHandler(this.menuDuplicateKey_Click);
            // 
            // btnDeleteKeyToolbar
            // 
            this.btnDeleteKeyToolbar.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.btnDeleteKeyToolbar.Name = "btnDeleteKeyToolbar";
            this.btnDeleteKeyToolbar.Size = new System.Drawing.Size(85, 22);
            this.btnDeleteKeyToolbar.Text = "❌ Delete Key";
            this.btnDeleteKeyToolbar.Click += new System.EventHandler(this.menuDeleteLabel_Click);
            // 
            // sepToolbarNav
            // 
            this.sepToolbarNav.Name = "sepToolbarNav";
            this.sepToolbarNav.Size = new System.Drawing.Size(6, 25);
            // 
            // btnJumpPrevEmptyToolbar
            // 
            this.btnJumpPrevEmptyToolbar.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.btnJumpPrevEmptyToolbar.Name = "btnJumpPrevEmptyToolbar";
            this.btnJumpPrevEmptyToolbar.Size = new System.Drawing.Size(155, 22);
            this.btnJumpPrevEmptyToolbar.Text = "⏮️ Previous Empty Key";
            this.btnJumpPrevEmptyToolbar.ToolTipText = "Jump to Previous Empty Key (Ctrl+Shift+Up)";
            this.btnJumpPrevEmptyToolbar.Click += (s, e) => JumpToNextEmptyKey(false);
            // 
            // btnJumpNextEmptyToolbar
            // 
            this.btnJumpNextEmptyToolbar.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.btnJumpNextEmptyToolbar.Name = "btnJumpNextEmptyToolbar";
            this.btnJumpNextEmptyToolbar.Size = new System.Drawing.Size(140, 22);
            this.btnJumpNextEmptyToolbar.Text = "⏭️ Next Empty Key";
            this.btnJumpNextEmptyToolbar.ToolTipText = "Jump to Next Empty Key (Ctrl+Shift+Down)";
            this.btnJumpNextEmptyToolbar.Click += (s, e) => JumpToNextEmptyKey(true);
            // 
            // sep6
            // 
            this.sep6.Name = "sep6";
            this.sep6.Size = new System.Drawing.Size(6, 25);
            // 
            // btnKeyFilterMode
            // 
            this.btnKeyFilterMode.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.btnKeyFilterMode.Name = "btnKeyFilterMode";
            this.btnKeyFilterMode.Size = new System.Drawing.Size(95, 22);
            this.btnKeyFilterMode.Text = "🔍 Key Filter:";
            this.btnKeyFilterMode.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold);
            this.btnKeyFilterMode.Click += new System.EventHandler(this.btnKeyFilterMode_Click);
            // 
            // cboSearchKey
            // 
            this.cboSearchKey.Name = "cboSearchKey";
            this.cboSearchKey.Size = new System.Drawing.Size(130, 26);
            // 
            // btnValFilterMode
            // 
            this.btnValFilterMode.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.btnValFilterMode.Name = "btnValFilterMode";
            this.btnValFilterMode.Size = new System.Drawing.Size(100, 22);
            this.btnValFilterMode.Text = "🔍 Text Filter:";
            this.btnValFilterMode.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold);
            this.btnValFilterMode.Click += new System.EventHandler(this.btnValFilterMode_Click);
            // 
            // cboSearchValue
            // 
            this.cboSearchValue.Name = "cboSearchValue";
            this.cboSearchValue.Size = new System.Drawing.Size(150, 26);
            // 
            // btnFilterLogic
            // 
            this.btnFilterLogic.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.btnFilterLogic.Name = "btnFilterLogic";
            this.btnFilterLogic.Size = new System.Drawing.Size(42, 22);
            this.btnFilterLogic.Text = "OR";
            this.btnFilterLogic.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold);
            this.btnFilterLogic.Click += new System.EventHandler(this.btnFilterLogic_Click);
            // 
            // sep7
            // 
            this.sep7.Name = "sep7";
            this.sep7.Size = new System.Drawing.Size(6, 25);
            // 
            // cboStatusFilter
            // 
            this.cboStatusFilter.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cboStatusFilter.Items.AddRange(new object[] {
            "🎯 All Statuses",
            "🔴 Missing Keys Only",
            "🟡 Empty Strings Only",
            "🟢 Complete Keys Only"});
            this.cboStatusFilter.Name = "cboStatusFilter";
            this.cboStatusFilter.Size = new System.Drawing.Size(150, 26);
            // 
            // statusStrip1
            // 
            this.statusStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.lblStatusCount,
            this.lblSessionMode,
            this.lblSaveNotification});
            this.statusStrip1.Location = new System.Drawing.Point(0, 628);
            this.statusStrip1.Name = "statusStrip1";
            this.statusStrip1.Size = new System.Drawing.Size(1020, 22);
            this.statusStrip1.TabIndex = 2;
            // 
            // lblStatusCount
            // 
            this.lblStatusCount.Name = "lblStatusCount";
            this.lblStatusCount.Size = new System.Drawing.Size(100, 17);
            this.lblStatusCount.Text = "Loaded keys: 0";
            // 
            // lblSessionMode
            // 
            this.lblSessionMode.Name = "lblSessionMode";
            this.lblSessionMode.Size = new System.Drawing.Size(118, 17);
            this.lblSessionMode.Text = "Mode: Multi-File";
            // 
            // lblSaveNotification
            // 
            this.lblSaveNotification.Name = "lblSaveNotification";
            this.lblSaveNotification.Spring = true;
            this.lblSaveNotification.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.lblSaveNotification.Text = "";
            this.lblSessionMode.Size = new System.Drawing.Size(130, 17);
            this.lblSessionMode.Text = "Mode: Single-CSF (1 File)";
            // 
            // splitMasterDetail
            // 
            this.splitMasterDetail.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitMasterDetail.Location = new System.Drawing.Point(0, 49);
            this.splitMasterDetail.Name = "splitMasterDetail";
            this.splitMasterDetail.Orientation = System.Windows.Forms.Orientation.Horizontal;
            this.splitMasterDetail.Panel1.Controls.Add(this.tabControlMain);
            this.splitMasterDetail.Panel2.Controls.Add(this.pnlLanguageEditors);
            this.splitMasterDetail.Panel2.Controls.Add(this.pnlDetailHeader);
            this.splitMasterDetail.Panel2Collapsed = true;
            this.splitMasterDetail.Size = new System.Drawing.Size(1020, 554);
            this.splitMasterDetail.TabIndex = 3;
            // 
            // tabControlMain
            // 
            this.tabControlMain.Controls.Add(this.tabMaster);
            this.tabControlMain.Controls.Add(this.tabKeyEditor);
            this.tabControlMain.Controls.Add(this.tabCoverage);
            this.tabControlMain.Controls.Add(this.tabUnsaved);
            this.tabControlMain.Controls.Add(this.tabRecent);
            this.tabControlMain.Controls.Add(this.tabBackups);
            this.tabControlMain.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tabControlMain.Location = new System.Drawing.Point(0, 0);
            this.tabControlMain.Name = "tabControlMain";
            this.tabControlMain.SelectedIndex = 0;
            this.tabControlMain.Size = new System.Drawing.Size(1020, 554);
            this.tabControlMain.TabIndex = 0;
            // 
            // tabMaster
            // 
            this.tabMaster.Controls.Add(this.splitMain);
            this.tabMaster.Location = new System.Drawing.Point(4, 22);
            this.tabMaster.Name = "tabMaster";
            this.tabMaster.Padding = new System.Windows.Forms.Padding(3);
            this.tabMaster.Size = new System.Drawing.Size(1012, 528);
            this.tabMaster.TabIndex = 0;
            this.tabMaster.Text = "Master Keys View";
            this.tabMaster.UseVisualStyleBackColor = true;
            // 
            // tabKeyEditor
            // 
            this.tabKeyEditor.Location = new System.Drawing.Point(4, 22);
            this.tabKeyEditor.Name = "tabKeyEditor";
            this.tabKeyEditor.Padding = new System.Windows.Forms.Padding(3);
            this.tabKeyEditor.Size = new System.Drawing.Size(1012, 528);
            this.tabKeyEditor.TabIndex = 1;
            this.tabKeyEditor.Text = "Plain Keys View";
            this.tabKeyEditor.UseVisualStyleBackColor = true;
            // 
            // splitMain
            // 
            this.splitMain.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitMain.Location = new System.Drawing.Point(3, 3);
            this.splitMain.Name = "splitMain";
            // 
            // splitMain.Panel1
            // 
            this.splitMain.Panel1.Controls.Add(this.tvCategories);
            // 
            // splitMain.Panel2
            // 
            this.splitMain.Panel2.Controls.Add(this.gridLabels);
            this.splitMain.Size = new System.Drawing.Size(1006, 522);
            this.splitMain.SplitterDistance = 175;
            this.splitMain.TabIndex = 0;
            // 
            // tvCategories
            // 
            this.tvCategories.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tvCategories.Location = new System.Drawing.Point(0, 0);
            this.tvCategories.Name = "tvCategories";
            this.tvCategories.Size = new System.Drawing.Size(155, 522);
            this.tvCategories.TabIndex = 0;
            this.tvCategories.AfterSelect += (s, e) =>
            {
                _selectedCategory = e.Node?.Tag as string ?? "[All Labels]";
                if (_isRebuildingTree || _isSyncingSelection) return; // RebuildCategoryTreeAndGrid populates the grid itself right after
                PopulateMasterGrid();
            };
            // 
            // gridLabels
            // 
            this.gridLabels.AllowUserToAddRows = false;
            this.gridLabels.AllowUserToDeleteRows = false;
            this.gridLabels.AllowUserToResizeRows = false;
            this.gridLabels.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
            this.gridLabels.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            this.gridLabels.Dock = System.Windows.Forms.DockStyle.Fill;
            this.gridLabels.Location = new System.Drawing.Point(0, 0);
            this.gridLabels.MultiSelect = false;
            this.gridLabels.Name = "gridLabels";
            this.gridLabels.ReadOnly = true;
            this.gridLabels.RowHeadersVisible = false;
            this.gridLabels.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.gridLabels.Size = new System.Drawing.Size(802, 280);
            this.gridLabels.TabIndex = 0;
            // 
            // pnlDetailHeader
            // 
            this.pnlDetailHeader.Controls.Add(this.lblCurrentKey);
            this.pnlDetailHeader.Controls.Add(this.txtCurrentKeyName);
            this.pnlDetailHeader.Controls.Add(this.btnApplyRename);
            this.pnlDetailHeader.Controls.Add(this.lblCurrentWav);
            this.pnlDetailHeader.Controls.Add(this.txtCurrentExtraWav);
            this.pnlDetailHeader.Dock = System.Windows.Forms.DockStyle.Top;
            this.pnlDetailHeader.Location = new System.Drawing.Point(0, 0);
            this.pnlDetailHeader.Name = "pnlDetailHeader";
            this.pnlDetailHeader.Size = new System.Drawing.Size(802, 35);
            this.pnlDetailHeader.TabIndex = 0;
            // 
            // lblCurrentKey
            // 
            this.lblCurrentKey.AutoSize = true;
            this.lblCurrentKey.Location = new System.Drawing.Point(10, 10);
            this.lblCurrentKey.Name = "lblCurrentKey";
            this.lblCurrentKey.Size = new System.Drawing.Size(28, 13);
            this.lblCurrentKey.TabIndex = 0;
            this.lblCurrentKey.Text = "Key:";
            // 
            // txtCurrentKeyName
            // 
            this.txtCurrentKeyName.Location = new System.Drawing.Point(45, 7);
            this.txtCurrentKeyName.Name = "txtCurrentKeyName";
            this.txtCurrentKeyName.ReadOnly = false;
            this.txtCurrentKeyName.Size = new System.Drawing.Size(220, 20);
            this.txtCurrentKeyName.TabIndex = 1;
            // 
            // btnApplyRename
            // 
            this.btnApplyRename.Location = new System.Drawing.Point(272, 5);
            this.btnApplyRename.Name = "btnApplyRename";
            this.btnApplyRename.Size = new System.Drawing.Size(85, 24);
            this.btnApplyRename.TabIndex = 4;
            this.btnApplyRename.Text = "✏️ Rename";
            this.btnApplyRename.UseVisualStyleBackColor = true;
            this.btnApplyRename.Click += new System.EventHandler(this.btnApplyRename_Click);
            // 
            // lblCurrentWav
            // 
            this.lblCurrentWav.AutoSize = true;
            this.lblCurrentWav.Location = new System.Drawing.Point(375, 10);
            this.lblCurrentWav.Name = "lblCurrentWav";
            this.lblCurrentWav.Size = new System.Drawing.Size(107, 13);
            this.lblCurrentWav.TabIndex = 2;
            this.lblCurrentWav.Text = "Extra Audio (STRW):";
            // 
            // txtCurrentExtraWav
            // 
            this.txtCurrentExtraWav.Location = new System.Drawing.Point(485, 7);
            this.txtCurrentExtraWav.Name = "txtCurrentExtraWav";
            this.txtCurrentExtraWav.Size = new System.Drawing.Size(180, 20);
            this.txtCurrentExtraWav.TabIndex = 3;
            // 
            // pnlLanguageEditors
            // 
            this.pnlLanguageEditors.AutoScroll = true;
            this.pnlLanguageEditors.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pnlLanguageEditors.Location = new System.Drawing.Point(0, 35);
            this.pnlLanguageEditors.Name = "pnlLanguageEditors";
            this.pnlLanguageEditors.Size = new System.Drawing.Size(802, 203);
            this.pnlLanguageEditors.TabIndex = 1;
            // 
            // tabUnsaved
            // 
            this.tabUnsaved.Controls.Add(this.gridUnsaved);
            this.tabUnsaved.Location = new System.Drawing.Point(4, 22);
            this.tabUnsaved.Name = "tabUnsaved";
            this.tabUnsaved.Padding = new System.Windows.Forms.Padding(3);
            this.tabUnsaved.Size = new System.Drawing.Size(1012, 528);
            this.tabUnsaved.TabIndex = 3;
            this.tabUnsaved.Text = "✏️ Unsaved Changes";
            this.tabUnsaved.UseVisualStyleBackColor = true;
            // 
            // gridUnsaved
            // 
            this.gridUnsaved.AllowUserToAddRows = false;
            this.gridUnsaved.AllowUserToDeleteRows = false;
            this.gridUnsaved.AllowUserToResizeRows = false;
            this.gridUnsaved.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
            this.gridUnsaved.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            this.gridUnsaved.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.colUnsavedKey,
            this.colUnsavedCat,
            this.colUnsavedState,
            this.colUnsavedTime});
            this.gridUnsaved.Dock = System.Windows.Forms.DockStyle.Fill;
            this.gridUnsaved.Location = new System.Drawing.Point(3, 3);
            this.gridUnsaved.Name = "gridUnsaved";
            this.gridUnsaved.RowHeadersVisible = false;
            this.gridUnsaved.Size = new System.Drawing.Size(1006, 522);
            this.gridUnsaved.TabIndex = 0;
            // 
            // colUnsavedKey
            // 
            this.colUnsavedKey.HeaderText = "Key";
            this.colUnsavedKey.Name = "colUnsavedKey";
            this.colUnsavedKey.ReadOnly = true;
            // 
            // colUnsavedCat
            // 
            this.colUnsavedCat.HeaderText = "Category";
            this.colUnsavedCat.Name = "colUnsavedCat";
            this.colUnsavedCat.ReadOnly = true;
            // 
            // colUnsavedState
            // 
            this.colUnsavedState.HeaderText = "Change Status";
            this.colUnsavedState.Name = "colUnsavedState";
            this.colUnsavedState.ReadOnly = true;
            // 
            // colUnsavedTime
            // 
            this.colUnsavedTime.HeaderText = "Modification Time";
            this.colUnsavedTime.Name = "colUnsavedTime";
            this.colUnsavedTime.ReadOnly = true;
            // 
            // tabRecent
            // 
            this.tabRecent.Controls.Add(this.gridRecent);
            this.tabRecent.Location = new System.Drawing.Point(4, 22);
            this.tabRecent.Name = "tabRecent";
            this.tabRecent.Padding = new System.Windows.Forms.Padding(3);
            this.tabRecent.Size = new System.Drawing.Size(1012, 528);
            this.tabRecent.TabIndex = 4;
            this.tabRecent.Text = "🕒 Recently Edited Keys";
            this.tabRecent.UseVisualStyleBackColor = true;
            // 
            // gridRecent
            // 
            this.gridRecent.AllowUserToAddRows = false;
            this.gridRecent.AllowUserToDeleteRows = false;
            this.gridRecent.AllowUserToResizeRows = false;
            this.gridRecent.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
            this.gridRecent.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            this.gridRecent.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.colRecentKey,
            this.colRecentCat,
            this.colRecentTime});
            this.gridRecent.Dock = System.Windows.Forms.DockStyle.Fill;
            this.gridRecent.Location = new System.Drawing.Point(3, 3);
            this.gridRecent.MultiSelect = false;
            this.gridRecent.Name = "gridRecent";
            this.gridRecent.RowHeadersVisible = false;
            this.gridRecent.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.gridRecent.Size = new System.Drawing.Size(1006, 522);
            this.gridRecent.TabIndex = 0;
            // 
            // colRecentKey
            // 
            this.colRecentKey.HeaderText = "Key Name";
            this.colRecentKey.Name = "colRecentKey";
            this.colRecentKey.ReadOnly = true;
            // 
            // colRecentCat
            // 
            this.colRecentCat.HeaderText = "Category";
            this.colRecentCat.Name = "colRecentCat";
            this.colRecentCat.ReadOnly = true;
            // 
            // colRecentTime
            // 
            this.colRecentTime.HeaderText = "Last Modified Time";
            this.colRecentTime.Name = "colRecentTime";
            this.colRecentTime.ReadOnly = true;
            // 
            // tabCoverage
            // 
            this.tabCoverage.Controls.Add(this.gridCoverage);
            this.tabCoverage.Controls.Add(this.pnlCoverageHeader);
            this.tabCoverage.Location = new System.Drawing.Point(4, 22);
            this.tabCoverage.Name = "tabCoverage";
            this.tabCoverage.Padding = new System.Windows.Forms.Padding(3);
            this.tabCoverage.Size = new System.Drawing.Size(1012, 528);
            this.tabCoverage.TabIndex = 2;
            this.tabCoverage.Text = "Matrix Coverage";
            this.tabCoverage.UseVisualStyleBackColor = true;
            // 
            // pnlCoverageHeader
            // 
            this.pnlCoverageHeader.AutoScroll = true;
            this.pnlCoverageHeader.Dock = System.Windows.Forms.DockStyle.Top;
            this.pnlCoverageHeader.Location = new System.Drawing.Point(3, 3);
            this.pnlCoverageHeader.Name = "pnlCoverageHeader";
            this.pnlCoverageHeader.Size = new System.Drawing.Size(1006, 85);
            this.pnlCoverageHeader.TabIndex = 0;
            // 
            // tabBackups
            // 
            this.tabBackups.Location = new System.Drawing.Point(4, 22);
            this.tabBackups.Name = "tabBackups";
            this.tabBackups.Padding = new System.Windows.Forms.Padding(3);
            this.tabBackups.Size = new System.Drawing.Size(1012, 528);
            this.tabBackups.TabIndex = 5;
            this.tabBackups.Text = "Backups & History";
            this.tabBackups.UseVisualStyleBackColor = true;
            // 
            // gridCoverage
            // 
            this.gridCoverage.AllowUserToAddRows = false;
            this.gridCoverage.AllowUserToDeleteRows = false;
            this.gridCoverage.AllowUserToResizeRows = false;
            this.gridCoverage.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
            this.gridCoverage.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            this.gridCoverage.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.colCovKey,
            this.colCovStatus,
            this.colCovPercent});
            this.gridCoverage.Dock = System.Windows.Forms.DockStyle.Fill;
            this.gridCoverage.Location = new System.Drawing.Point(3, 88);
            this.gridCoverage.MultiSelect = false;
            this.gridCoverage.Name = "gridCoverage";
            this.gridCoverage.RowHeadersVisible = false;
            this.gridCoverage.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.gridCoverage.Size = new System.Drawing.Size(1006, 437);
            this.gridCoverage.TabIndex = 1;
            // 
            // 
            // colCovKey
            // 
            this.colCovKey.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.AllCells;
            this.colCovKey.HeaderText = "Key Name";
            this.colCovKey.MinimumWidth = 220;
            this.colCovKey.Name = "colCovKey";
            this.colCovKey.ReadOnly = true;
            // 
            // colCovStatus
            // 
            this.colCovStatus.HeaderText = "Coverage Status per CSF File";
            this.colCovStatus.Name = "colCovStatus";
            this.colCovStatus.ReadOnly = true;
            // 
            // colCovPercent
            // 
            this.colCovPercent.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.AllCells;
            this.colCovPercent.HeaderText = "Completion %";
            this.colCovPercent.Name = "colCovPercent";
            this.colCovPercent.ReadOnly = true;
            // 
            // MainForm
            // 
            this.ClientSize = new System.Drawing.Size(1280, 720);
            this.Controls.Add(this.splitMasterDetail);
            this.Controls.Add(this.statusStrip1);
            this.Controls.Add(this.toolStrip2);
            this.Controls.Add(this.toolStrip1);
            this.Controls.Add(this.menuStrip1);
            this.MainMenuStrip = this.menuStrip1;
            this.Name = "MainForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = CsfStudio.AppInfo.WindowTitle;
            this.menuStrip1.ResumeLayout(false);
            this.menuStrip1.PerformLayout();
            this.toolStrip1.ResumeLayout(false);
            this.toolStrip1.PerformLayout();
            this.statusStrip1.ResumeLayout(false);
            this.statusStrip1.PerformLayout();
            this.tabControlMain.ResumeLayout(false);
            this.tabMaster.ResumeLayout(false);
            this.splitMain.Panel1.ResumeLayout(false);
            this.splitMain.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitMain)).EndInit();
            this.splitMain.ResumeLayout(false);
            this.splitMasterDetail.Panel1.ResumeLayout(false);
            this.splitMasterDetail.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitMasterDetail)).EndInit();
            this.splitMasterDetail.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.gridLabels)).EndInit();
            this.pnlDetailHeader.ResumeLayout(false);
            this.pnlDetailHeader.PerformLayout();
            this.tabUnsaved.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.gridUnsaved)).EndInit();
            this.tabRecent.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.gridRecent)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        #endregion

        private System.Windows.Forms.MenuStrip menuStrip1;
        private System.Windows.Forms.ToolStripMenuItem menuFile;
        private System.Windows.Forms.ToolStripMenuItem menuNew;
        private System.Windows.Forms.ToolStripMenuItem menuOpen;
        private System.Windows.Forms.ToolStripMenuItem menuOpenSession;
        private System.Windows.Forms.ToolStripMenuItem menuRecentSessions;
        private System.Windows.Forms.ToolStripSeparator sep1;
        private System.Windows.Forms.ToolStripMenuItem menuSave;
        private System.Windows.Forms.ToolStripMenuItem menuSaveSingleFile;
        private System.Windows.Forms.ToolStripMenuItem menuSaveAs;
        private System.Windows.Forms.ToolStripSeparator sep2;
        private System.Windows.Forms.ToolStripMenuItem menuExportTxt;
        private System.Windows.Forms.ToolStripMenuItem menuExportKeysOnly;
        private System.Windows.Forms.ToolStripMenuItem menuImportTxt;
        private System.Windows.Forms.ToolStripSeparator sep3;
        private System.Windows.Forms.ToolStripMenuItem menuExit;
        private System.Windows.Forms.ToolStripMenuItem menuEdit;
        private System.Windows.Forms.ToolStripMenuItem menuAddLabel;
        private System.Windows.Forms.ToolStripMenuItem menuUndo;
        private System.Windows.Forms.ToolStripMenuItem menuRedo;
        private System.Windows.Forms.ToolStripSeparator sepUndo;
        private System.Windows.Forms.ToolStripMenuItem menuCut;
        private System.Windows.Forms.ToolStripMenuItem menuCopy;
        private System.Windows.Forms.ToolStripMenuItem menuPaste;
        private System.Windows.Forms.ToolStripMenuItem menuSelectAll;
        private System.Windows.Forms.ToolStripMenuItem menuInvertSelection;
        private System.Windows.Forms.ToolStripSeparator sepClipboard;
        private System.Windows.Forms.ToolStripMenuItem menuDeleteLabel;
        private System.Windows.Forms.ToolStripMenuItem menuDuplicateKey;
        private System.Windows.Forms.ToolStripMenuItem menuBatchRename;
        private System.Windows.Forms.ToolStripMenuItem menuCapitalization;
        private System.Windows.Forms.ToolStripMenuItem menuUpper;
        private System.Windows.Forms.ToolStripMenuItem menuLower;
        private System.Windows.Forms.ToolStripMenuItem menuTitle;
        private System.Windows.Forms.ToolStripMenuItem menuSentence;
        private System.Windows.Forms.ToolStripMenuItem menuTrimSpaces;
        private System.Windows.Forms.ToolStripSeparator sep4;
        private System.Windows.Forms.ToolStripMenuItem menuRenameFileLabel;
        private System.Windows.Forms.ToolStripMenuItem menuChangeHeaderLangId;
        private System.Windows.Forms.ToolStripMenuItem menuSetTranslationContentLang;
        private System.Windows.Forms.ToolStripMenuItem menuMoveUp;
        private System.Windows.Forms.ToolStripMenuItem menuMoveDown;
        private System.Windows.Forms.ToolStripSeparator sepMoveKeys;
        private System.Windows.Forms.ToolStripMenuItem menuJumpNextEmpty;
        private System.Windows.Forms.ToolStripMenuItem menuJumpPrevEmpty;
        private System.Windows.Forms.ToolStripSeparator sepNavigation;
        private System.Windows.Forms.ToolStripMenuItem menuFindReplace;
        private System.Windows.Forms.ToolStripSeparator sepOptions;
        private System.Windows.Forms.ToolStripMenuItem menuOptions;
        private System.Windows.Forms.ToolStripSeparator sepTools1;
        private System.Windows.Forms.ToolStripSeparator sepTools2;
        private System.Windows.Forms.ToolStripMenuItem menuSyncKeys;
        private System.Windows.Forms.ToolStripMenuItem menuSyncAudioWavs;
        private System.Windows.Forms.ToolStripMenuItem menuSortBinary;
        private System.Windows.Forms.ToolStripMenuItem menuTools;
        private System.Windows.Forms.ToolStripMenuItem menuScanIni;
        private System.Windows.Forms.ToolStripMenuItem menuConvertAnsi;
        private System.Windows.Forms.ToolStripMenuItem menuClearValuesKeepKeys;
        private System.Windows.Forms.ToolStripMenuItem menuHelp;
        private System.Windows.Forms.ToolStripMenuItem menuAbout;
        private System.Windows.Forms.ToolStrip toolStrip1;
        private System.Windows.Forms.ToolStrip toolStrip2;
        private System.Windows.Forms.ToolStripButton btnAddKeyToolbar;
        private System.Windows.Forms.ToolStripButton btnDeleteKeyToolbar;
        private System.Windows.Forms.ToolStripButton btnDuplicateKeyToolbar;
        private System.Windows.Forms.Button btnApplyRename;
        private System.Windows.Forms.ToolStripSeparator sep6;
        private System.Windows.Forms.ToolStripButton btnKeyFilterMode;
        private System.Windows.Forms.ToolStripComboBox cboSearchKey;
        private System.Windows.Forms.ToolStripButton btnValFilterMode;
        private System.Windows.Forms.ToolStripComboBox cboSearchValue;
        private System.Windows.Forms.ToolStripButton btnFilterLogic;
        private System.Windows.Forms.ToolStripSeparator sep7;
        private System.Windows.Forms.ToolStripComboBox cboStatusFilter;
        private System.Windows.Forms.StatusStrip statusStrip1;
        private System.Windows.Forms.ToolStripStatusLabel lblStatusCount;
        private System.Windows.Forms.ToolStripStatusLabel lblSessionMode;
        private System.Windows.Forms.ToolStripStatusLabel lblSaveNotification;
        private System.Windows.Forms.TabControl tabControlMain;
        private System.Windows.Forms.TabPage tabMaster;
        private System.Windows.Forms.SplitContainer splitMain;
        private System.Windows.Forms.TreeView tvCategories;
        private System.Windows.Forms.SplitContainer splitMasterDetail;
        private System.Windows.Forms.DataGridView gridLabels;
        private System.Windows.Forms.Panel pnlDetailHeader;
        private System.Windows.Forms.Label lblCurrentKey;
        private System.Windows.Forms.TextBox txtCurrentKeyName;
        private System.Windows.Forms.Label lblCurrentWav;
        private System.Windows.Forms.TextBox txtCurrentExtraWav;
        private System.Windows.Forms.Panel pnlLanguageEditors;
        private System.Windows.Forms.TabPage tabUnsaved;
        private System.Windows.Forms.DataGridView gridUnsaved;
        private System.Windows.Forms.DataGridViewCheckBoxColumn colUnsavedCheck;
        private System.Windows.Forms.DataGridViewTextBoxColumn colUnsavedKey;
        private System.Windows.Forms.DataGridViewTextBoxColumn colUnsavedCat;
        private System.Windows.Forms.DataGridViewTextBoxColumn colUnsavedState;
        private System.Windows.Forms.DataGridViewTextBoxColumn colUnsavedTime;
        private System.Windows.Forms.TabPage tabRecent;
        private System.Windows.Forms.DataGridView gridRecent;
        private System.Windows.Forms.DataGridViewTextBoxColumn colRecentKey;
        private System.Windows.Forms.DataGridViewTextBoxColumn colRecentCat;
        private System.Windows.Forms.DataGridViewTextBoxColumn colRecentTime;
        private System.Windows.Forms.TabPage tabCoverage;
        private System.Windows.Forms.Panel pnlCoverageHeader;
        private System.Windows.Forms.DataGridView gridCoverage;
        private System.Windows.Forms.DataGridViewTextBoxColumn colCovKey;
        private System.Windows.Forms.DataGridViewTextBoxColumn colCovStatus;
        private System.Windows.Forms.DataGridViewTextBoxColumn colCovPercent;
        private System.Windows.Forms.TabPage tabBackups;
        private System.Windows.Forms.TabPage tabKeyEditor;
        private System.Windows.Forms.ToolStripSeparator sepToolbarNav;
        private System.Windows.Forms.ToolStripButton btnJumpPrevEmptyToolbar;
        private System.Windows.Forms.ToolStripButton btnJumpNextEmptyToolbar;
        private System.Windows.Forms.ToolStripMenuItem menuGitHubRepo;
        private System.Windows.Forms.ToolStripSeparator sepHelp1;
    }
}
