using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using CsfStudio.Core;
using CsfStudio.Core.Translation;

namespace CsfStudio.UI
{
    public partial class MainForm : Form
    {
        private CsfSession _session = new CsfSession();
        private AppConfig _appConfig = ConfigManager.LoadConfig();
        private bool _filterLogicAnd = false; // false = OR (default), true = AND
        private bool _keyRegexMode = false;
        private bool _valRegexMode = false;
        private bool _sortByBinarySequence = true;
        private string _selectedCategory = "[All Labels]";
        private FindReplaceDialog _findReplaceDlg = null;
        private readonly UndoManager _undoManager = new UndoManager();

        private LinkedList<string> _recentEditedKeys = new LinkedList<string>();
        private HashSet<string> _keySearchHistory = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private HashSet<string> _valueSearchHistory = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private Dictionary<string, TextBox> _langTextEditors = new Dictionary<string, TextBox>(StringComparer.OrdinalIgnoreCase);
        private Dictionary<string, Label> _langLengthLabels = new Dictionary<string, Label>(StringComparer.OrdinalIgnoreCase);
        private Dictionary<string, Label> _langLinterLabels = new Dictionary<string, Label>(StringComparer.OrdinalIgnoreCase);
        private ToolTip _toolTip = new ToolTip { InitialDelay = 400, ReshowDelay = 100, AutoPopDelay = 8000, ShowAlways = false };
        private string _lastSelectedTargetLanguageTag = null;
        private HashSet<string> _unpinnedTargetLanguageTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private bool _isSyncingTabs = false;
        private HashSet<string> _modifiedKeyNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private HashSet<string> _modifiedKeyMap = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private HashSet<string> _addedKeyNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private HashSet<string> _deletedKeyNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private Dictionary<string, string> _reorderedKeyDetails = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private Timer _selectionDebounceTimer = new Timer { Interval = 45 };
        private DataGridView _pendingGridSelection = null;
        private MasterKeyRow _currentlyDisplayedSingleRow = null;
        private List<string> _currentlyRenderedMasterKeyNames = new List<string>();

        // --- Performance caches (RAM trade-off for speed) ---
        private List<MasterKeyRow> _cachedMasterRows = null;
        private Dictionary<string, MasterKeyRow> _cachedMasterMap = null;
        private CsfSessionDocument _labelIndexMapDoc = null;
        private Dictionary<string, int> _labelIndexMap = null;
        private string _masterGridColumnSignature = null;
        private bool _unsavedDirty = false;
        private bool _recentDirty = false;
        private bool _coverageDirty = false;
        private bool _keyEditorDirty = false;
        private Timer _searchDebounceTimer = null;
        private string _backupScanBasePath = null;
        private bool _backupScanHasSnapshots = false;
        private bool _backupScanValid = false;

        // Heavy editor-panel construction is deferred by a short timer so the grid/list
        // paints first, and repeated requests within the same UI chain collapse into one build.
        private Timer _deferredEditorBuildTimer = null;
        private Action _pendingEditorBuild = null;

        // Master grid row streaming: large result sets are appended in time-sliced chunks
        // so the first paint is immediate and the UI never freezes; a newer populate
        // cancels the in-flight stream via _rowStreamToken.
        private int _rowStreamToken = 0;
        private Timer _rowStreamTimer = null;
        private MasterRowStreamState _activeMasterRowStream = null;
        private int _pendingScrollRestoreAfterStream = -1;
        private bool _isRebuildingTree = false;

        private class MasterRowStreamState
        {
            public int Token;
            public List<MasterKeyRow> Rows;
            public int NextIndex;
            public int ChunkSize;
            public Func<MasterKeyRow, DataGridViewRow> BuildRow;
            public Action Finish;
        }

        private void PumpMasterRowStream()
        {
            var stream = _activeMasterRowStream;
            if (stream == null || stream.Token != _rowStreamToken)
            {
                _rowStreamTimer?.Stop();
                _activeMasterRowStream = null;
                return;
            }

            int end = Math.Min(stream.NextIndex + stream.ChunkSize, stream.Rows.Count);
            var chunk = new DataGridViewRow[end - stream.NextIndex];
            for (int i = stream.NextIndex; i < end; i++)
            {
                chunk[i - stream.NextIndex] = stream.BuildRow(stream.Rows[i]);
            }
            stream.NextIndex = end;

            if (chunk.Length > 0)
            {
                try { gridLabels.Rows.AddRange(chunk); } catch { }
            }

            if (stream.NextIndex >= stream.Rows.Count)
            {
                _rowStreamTimer.Stop();
                _activeMasterRowStream = null;
                stream.Finish();
            }
        }

        // Forces an in-flight row stream to finish synchronously. Required before any code
        // that scans gridLabels.Rows expecting the full result set (e.g. key selection).
        private void CompleteMasterRowStreamNow()
        {
            var stream = _activeMasterRowStream;
            if (stream == null) return;
            _rowStreamTimer?.Stop();
            _activeMasterRowStream = null;
            if (stream.Token != _rowStreamToken) return;

            if (stream.NextIndex < stream.Rows.Count)
            {
                int remaining = stream.Rows.Count - stream.NextIndex;
                var chunk = new DataGridViewRow[remaining];
                for (int i = 0; i < remaining; i++)
                {
                    chunk[i] = stream.BuildRow(stream.Rows[stream.NextIndex + i]);
                }
                try { gridLabels.Rows.AddRange(chunk); } catch { }
            }
            stream.Finish();
        }

        private void ScheduleEditorBuild(Action buildAction)
        {
            _pendingEditorBuild = buildAction;
            if (_deferredEditorBuildTimer == null)
            {
                _deferredEditorBuildTimer = new Timer { Interval = 25 };
                _deferredEditorBuildTimer.Tick += (s, e) =>
                {
                    _deferredEditorBuildTimer.Stop();
                    var action = _pendingEditorBuild;
                    _pendingEditorBuild = null;
                    action?.Invoke();
                };
            }
            _deferredEditorBuildTimer.Stop();
            _deferredEditorBuildTimer.Start();
        }

        private void Session_Changed(object sender, EventArgs e)
        {
            InvalidateMasterRowsCache();
            OnSessionUpdated();
        }

        private List<MasterKeyRow> _lastFilteredMasterRows = null;

        private void InvalidateMasterRowsCache()
        {
            _cachedMasterRows = null;
            _cachedMasterMap = null;
            _labelIndexMapDoc = null;
            _labelIndexMap = null;
            _positionsMatchCache = null;
            _lastFilteredMasterRows = null;
        }

        private List<MasterKeyRow> GetMasterRows()
        {
            if (_cachedMasterRows == null)
            {
                _cachedMasterRows = (_session != null && _session.Documents.Count > 0)
                    ? _session.BuildMasterKeyList(_sortByBinarySequence)
                    : new List<MasterKeyRow>();
                _cachedMasterMap = null;
            }
            return _cachedMasterRows;
        }

        private Dictionary<string, MasterKeyRow> GetMasterRowsMap()
        {
            if (_cachedMasterMap == null)
            {
                var rows = GetMasterRows();
                var map = new Dictionary<string, MasterKeyRow>(rows.Count, StringComparer.OrdinalIgnoreCase);
                foreach (var r in rows)
                {
                    if (r != null && !map.ContainsKey(r.KeyName)) map[r.KeyName] = r;
                }
                _cachedMasterMap = map;
            }
            return _cachedMasterMap;
        }

        private Dictionary<string, int> GetLabelIndexMapFor(CsfSessionDocument doc)
        {
            if (doc == null || doc.Document == null) return null;
            if (!ReferenceEquals(_labelIndexMapDoc, doc) || _labelIndexMap == null)
            {
                var labels = doc.Document.Labels;
                var map = new Dictionary<string, int>(labels.Count, StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < labels.Count; i++)
                {
                    if (!map.ContainsKey(labels[i].Name)) map[labels[i].Name] = i;
                }
                _labelIndexMapDoc = doc;
                _labelIndexMap = map;
            }
            return _labelIndexMap;
        }

        private Panel pnlDetailContainer;
        private Panel pnlPlainKeyRight;
        private Panel pnlKeyEditorEditors;
        private SplitContainer splitKeyEditor;
        private ListBox lstKeyEditorKeys;
        private List<MasterKeyRow> _keyEditorFilteredRows = new List<MasterKeyRow>();

        private ToolStripLabel lblFileFilter;
        private ToolStripComboBox cboFileFilter;
        private ToolStripSeparator fileFilterSeparator;
        private CsfSessionDocument _fileFilterSessionBaseDocument = null;

        private class DocumentFilterOption
        {
            public CsfSessionDocument Document { get; set; }
            public string DisplayName { get; set; }

            public override string ToString() => DisplayName;
        }

        private List<string> _initialCommandLineFiles = new List<string>();

        public MainForm(IEnumerable<string> initialFilePaths = null)
        {
            if (initialFilePaths != null)
            {
                _initialCommandLineFiles.AddRange(initialFilePaths);
            }
            InitializeComponent();
            ConfigManager.SaveConfig(_appConfig);
            this.Shown += MainForm_Shown;

            lblFileFilter = new ToolStripLabel { Text = "📄 File View:" };
            cboFileFilter = new ToolStripComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                AutoSize = false,
                Size = new System.Drawing.Size(200, 26),
                Width = 200,
                ToolTipText = "Select view mode: View all open CSF files side-by-side or focus on a single CSF file with missing key red highlighting."
            };

            // Add File View controls to toolStrip1 (filter toolbar) at the beginning
            toolStrip1.Items.Insert(0, cboFileFilter);
            toolStrip1.Items.Insert(0, lblFileFilter);
            fileFilterSeparator = new ToolStripSeparator { Visible = false };
            toolStrip1.Items.Insert(2, fileFilterSeparator);
            try
            {
                var asm = System.Reflection.Assembly.GetExecutingAssembly();
                using (var strm = asm.GetManifestResourceStream("CsfStudio.app_icon.ico"))
                {
                    if (strm != null)
                    {
                        this.Icon = new Icon(strm);
                    }
                    else
                    {
                        string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app_icon.ico");
                        if (File.Exists(iconPath))
                        {
                            this.Icon = new Icon(iconPath);
                        }
                        else
                        {
                            this.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
                        }
                    }
                }
            }
            catch { }
            EnableDoubleBuffering(tabControlMain);
            EnableDoubleBuffering(tabMaster);
            EnableDoubleBuffering(tabCoverage);
            EnableDoubleBuffering(tabKeyEditor);
            EnableDoubleBuffering(tabUnsaved);
            EnableDoubleBuffering(tabRecent);
            EnableDoubleBuffering(tabBackups);
            EnableDoubleBuffering(gridLabels);
            EnableDoubleBuffering(gridUnsaved);
            EnableDoubleBuffering(gridRecent);
            EnableDoubleBuffering(pnlLanguageEditors);

            gridLabels.DefaultCellStyle.WrapMode = DataGridViewTriState.False;
            gridLabels.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None;
            gridLabels.RowTemplate.Height = 22;

            gridUnsaved.DefaultCellStyle.WrapMode = DataGridViewTriState.False;
            gridUnsaved.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None;
            gridUnsaved.RowTemplate.Height = 22;

            gridRecent.DefaultCellStyle.WrapMode = DataGridViewTriState.False;
            gridRecent.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None;
            gridRecent.RowTemplate.Height = 22;

            gridCoverage.DefaultCellStyle.WrapMode = DataGridViewTriState.False;
            gridCoverage.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None;
            gridCoverage.RowTemplate.Height = 22;

            ToolTip _gridCellToolTip = new ToolTip
            {
                ShowAlways = true,
                InitialDelay = 300,
                ReshowDelay = 100,
                AutoPopDelay = 10000
            };

            int lastHoverRow = -1;
            int lastHoverCol = -1;

            void SetupGridToolTipHandler(DataGridView grid)
            {
                grid.ShowCellToolTips = false;

                grid.CellMouseEnter += (s, e) =>
                {
                    if (e.RowIndex >= 0 && e.ColumnIndex >= 0)
                    {
                        if (e.RowIndex == lastHoverRow && e.ColumnIndex == lastHoverCol) return;
                        lastHoverRow = e.RowIndex;
                        lastHoverCol = e.ColumnIndex;

                        var targetGrid = s as DataGridView;
                        if (targetGrid != null && e.RowIndex < targetGrid.Rows.Count && e.ColumnIndex < targetGrid.Columns.Count)
                        {
                            var cell = targetGrid.Rows[e.RowIndex].Cells[e.ColumnIndex];
                            string rawText = !string.IsNullOrEmpty(cell.ToolTipText)
                                ? cell.ToolTipText
                                : cell.Value?.ToString();

                            if (!string.IsNullOrEmpty(rawText) && rawText != "[MISSING]" && rawText != "[Missing Entry]" && !rawText.Contains("Missing Entry"))
                            {
                                string wrapped = WrapToolTipText(rawText, 45, 15);
                                Point pt = targetGrid.PointToClient(Cursor.Position);
                                _gridCellToolTip.Show(wrapped, targetGrid, pt.X + 12, pt.Y + 20, 8000);
                            }
                            else
                            {
                                _gridCellToolTip.Hide(targetGrid);
                            }
                        }
                    }
                };

                grid.CellMouseLeave += (s, e) =>
                {
                    lastHoverRow = -1;
                    lastHoverCol = -1;
                    var targetGrid = s as DataGridView;
                    if (targetGrid != null)
                    {
                        _gridCellToolTip.Hide(targetGrid);
                    }
                };
            }

            SetupGridToolTipHandler(gridLabels);
            SetupGridToolTipHandler(gridUnsaved);
            SetupGridToolTipHandler(gridRecent);
            SetupGridToolTipHandler(gridCoverage);

            gridLabels.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            gridLabels.MultiSelect = true;

            gridUnsaved.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            gridUnsaved.MultiSelect = true;

            gridRecent.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            gridRecent.MultiSelect = true;

            gridCoverage.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            gridCoverage.MultiSelect = true;

            gridCoverage.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
            if (colCovPercent != null)
            {
                colCovPercent.Width = 100;
                colCovPercent.AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
            }
            if (colCovStatus != null)
            {
                colCovStatus.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            }

            gridLabels.AllowUserToResizeRows = false;
            gridUnsaved.AllowUserToResizeRows = false;
            gridRecent.AllowUserToResizeRows = false;
            gridCoverage.AllowUserToResizeRows = false;

            EnableDoubleBuffering(gridCoverage);

            _selectionDebounceTimer.Tick += (s, e) =>
            {
                _selectionDebounceTimer.Stop();
                if (_pendingGridSelection != null)
                {
                    var g = _pendingGridSelection;
                    _pendingGridSelection = null;
                    OnGridSelectionChanged(g);
                }
            };

            gridLabels.SelectionChanged += (s, e) => TriggerGridSelectionChanged(gridLabels);
            gridUnsaved.SelectionChanged += (s, e) => TriggerGridSelectionChanged(gridUnsaved);
            gridRecent.SelectionChanged += (s, e) => TriggerGridSelectionChanged(gridRecent);



            SetupGridContextMenu();
            BuildTranslationSubmenus();

            gridUnsaved.CellDoubleClick += (s, e) =>
            {
                if (e.RowIndex >= 0)
                {
                    OnGridSelectionChanged(gridUnsaved);
                    _langTextEditors.Values.FirstOrDefault()?.Focus();
                }
            };

            gridRecent.CellDoubleClick += (s, e) =>
            {
                if (e.RowIndex >= 0)
                {
                    OnGridSelectionChanged(gridRecent);
                    _langTextEditors.Values.FirstOrDefault()?.Focus();
                }
            };

            gridCoverage.CellDoubleClick += (s, e) =>
            {
                if (e.RowIndex >= 0)
                {
                    OnGridSelectionChanged(gridCoverage);
                    _langTextEditors.Values.FirstOrDefault()?.Focus();
                }
            };

            tabControlMain.ShowToolTips = true;
            tabControlMain.DrawMode = TabDrawMode.OwnerDrawFixed;
            tabControlMain.Padding = new Point(18, 6);
            tabControlMain.DrawItem += (s, e) =>
            {
                if (tabControlMain == null || tabControlMain.TabPages == null || e.Index < 0 || e.Index >= tabControlMain.TabPages.Count) return;
                var g = e.Graphics;
                var tab = tabControlMain.TabPages[e.Index];
                var bounds = tabControlMain.GetTabRect(e.Index);
                if (bounds.Width <= 0 || bounds.Height <= 0) return;

                bool isSelected = (tabControlMain.SelectedIndex == e.Index);

                using (var backBrush = new SolidBrush(isSelected ? Color.FromArgb(245, 247, 250) : SystemColors.Control))
                {
                    g.FillRectangle(backBrush, bounds);
                }

                string cleanText = System.Text.RegularExpressions.Regex.Replace(tab.Text ?? string.Empty, @"[^\u0000-\u007F]+", "").Trim();

                FontStyle fontStyle = isSelected ? FontStyle.Bold : FontStyle.Regular;
                using (var font = new Font(tabControlMain.Font, fontStyle))
                using (var textBrush = new SolidBrush(isSelected ? Color.FromArgb(0, 51, 102) : SystemColors.ControlText))
                {
                    var sf = new StringFormat
                    {
                        Alignment = StringAlignment.Center,
                        LineAlignment = StringAlignment.Center,
                        Trimming = StringTrimming.EllipsisCharacter
                    };
                    g.DrawString(cleanText, font, textBrush, bounds, sf);
                }

                if (isSelected)
                {
                    using (var pen = new Pen(Color.FromArgb(0, 120, 215), 2))
                    {
                        g.DrawLine(pen, bounds.Left, bounds.Bottom - 1, bounds.Right, bounds.Bottom - 1);
                    }
                }
            };

            tabMaster.ToolTipText = "📋 Master String Table: View and edit all keys across loaded CSF files in a side-by-side or master grid table.";
            tabUnsaved.ToolTipText = "⚠️ Unsaved Changes: Inspector tab listing all modified string keys waiting to be saved to disk.";
            tabRecent.ToolTipText = "🕒 Recent Edits: History log of keys modified during the current editing session.";
            tabCoverage.ToolTipText = "📊 Coverage Matrix: Key completion percentage matrix across all open CSF files.";
            tabBackups.ToolTipText = "💾 Backups & History: Snapshot history of automatically created session backups (.bak) with diff inspection and restore capabilities.";

            SetupTabControlToolTips(tabControlMain);
            InitializeKeyEditorTab();

            pnlDetailContainer = new Panel { Dock = DockStyle.Fill };
            pnlDetailContainer.Controls.Add(pnlLanguageEditors);
            pnlDetailContainer.Controls.Add(pnlDetailHeader);
            splitMasterDetail.Panel2.Controls.Add(pnlDetailContainer);

            if (colCovBaseText != null) colCovBaseText.Visible = false;



            splitMasterDetail.Panel2Collapsed = true;

            splitMasterDetail.Resize += (s, e) =>
            {
                if (!splitMasterDetail.Panel2Collapsed && splitMasterDetail.Height > 200)
                {
                    int selectedCount = _lastActiveSelectedKeys?.Count ?? 0;
                    double ratio = selectedCount > 1 ? 0.40 : 0.50;
                    int panel1Height = (int)(splitMasterDetail.Height * ratio);
                    int min = Math.Max(50, splitMasterDetail.Panel1MinSize);
                    int max = Math.Min(splitMasterDetail.Height - 50, splitMasterDetail.Height - splitMasterDetail.Panel2MinSize);
                    if (panel1Height >= min && panel1Height <= max)
                    {
                        splitMasterDetail.SplitterDistance = panel1Height;
                    }
                }
            };

            gridRecent.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter && gridRecent.SelectedRows.Count > 0)
                {
                    e.SuppressKeyPress = true;
                    string keyName = gridRecent.SelectedRows[0].Cells[0].Value as string;
                    if (!string.IsNullOrEmpty(keyName))
                    {
                        tabControlMain.SelectedTab = tabMaster;
                        EnsureKeyVisibleAndSelected(keyName);
                    }
                }
            };

            gridLabels.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.F2 && gridLabels.SelectedRows.Count > 0)
                {
                    e.SuppressKeyPress = true;
                    txtCurrentKeyName.Focus();
                    txtCurrentKeyName.SelectAll();
                }
            };
            gridLabels.SelectionChanged += (s, e) => UpdateUIForSessionMode();
            gridLabels.CellPainting += (s, e) =>
            {
                if (e.RowIndex >= 0 && e.ColumnIndex >= 0)
                {
                    var col = gridLabels.Columns[e.ColumnIndex];
                    if (string.IsNullOrEmpty(col.HeaderText))
                    {
                        e.Paint(e.CellBounds, DataGridViewPaintParts.All & ~DataGridViewPaintParts.ContentForeground);
                        string cellVal = e.Value?.ToString() ?? "";
                        if (gridLabels.Rows[e.RowIndex].Tag is MasterKeyRow statusRow)
                        {
                            cellVal = GetMasterGridStatusKind(statusRow);
                        }
                        DrawStatusSphere(e.Graphics, e.CellBounds, cellVal);
                        e.Handled = true;
                    }
                }
            };
            gridCoverage.CellPainting += (s, e) =>
            {
                if (e.RowIndex >= 0 && e.ColumnIndex >= 0 && gridCoverage.Columns[e.ColumnIndex].Name == "colCovStatus")
                {
                    e.Paint(e.CellBounds, DataGridViewPaintParts.All & ~DataGridViewPaintParts.ContentForeground);

                    if (gridCoverage.Rows[e.RowIndex].Tag is MasterKeyRow row)
                    {
                        int sphereSize = 12;
                        int sphereY = e.CellBounds.Top + (e.CellBounds.Height - sphereSize) / 2;
                        int curX = e.CellBounds.Left + 6;

                        bool isSelected = e.State.HasFlag(DataGridViewElementStates.Selected);
                        Color textColor = isSelected ? e.CellStyle.SelectionForeColor : e.CellStyle.ForeColor;
                        Color sepColor = isSelected ? Color.FromArgb(200, 220, 255) : Color.FromArgb(160, 160, 160);

                        using (var sf = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center })
                        using (var textBrush = new SolidBrush(textColor))
                        using (var sepBrush = new SolidBrush(sepColor))
                        {
                            for (int i = 0; i < _session.Documents.Count; i++)
                            {
                                var sDoc = _session.Documents[i];
                                bool exists = row.ValuesPerLanguage.TryGetValue(sDoc.LanguageTag, out var entry);
                                bool hasText = exists && !string.IsNullOrEmpty(entry?.Value);
                                bool isModified = IsKeyModifiedInDoc(sDoc.LanguageTag, row.KeyName);
                                string statusKind = !exists ? "MISSING" : (isModified ? "MODIFIED" : (!hasText ? "EMPTY" : "COMPLETE"));

                                DrawStatusSphereAt(e.Graphics, curX, sphereY, sphereSize, statusKind);
                                curX += sphereSize + 4;

                                string tagText = sDoc.LanguageTag;
                                SizeF sz = e.Graphics.MeasureString(tagText, e.CellStyle.Font);
                                var textRect = new RectangleF(curX, e.CellBounds.Top, sz.Width + 2, e.CellBounds.Height);
                                e.Graphics.DrawString(tagText, e.CellStyle.Font, textBrush, textRect, sf);
                                curX += (int)sz.Width + 10;

                                if (i < _session.Documents.Count - 1)
                                {
                                    var sepRect = new RectangleF(curX, e.CellBounds.Top, 10, e.CellBounds.Height);
                                    e.Graphics.DrawString("|", e.CellStyle.Font, sepBrush, sepRect, sf);
                                    curX += 12;
                                }
                            }
                        }
                    }
                    e.Handled = true;
                }
            };
            if (gridUnsaved != null) gridUnsaved.SelectionChanged += (s, e) => UpdateUIForSessionMode();
            if (gridRecent != null) gridRecent.SelectionChanged += (s, e) => UpdateUIForSessionMode();
            if (gridCoverage != null) gridCoverage.SelectionChanged += (s, e) => UpdateUIForSessionMode();
            if (tabControlMain != null) tabControlMain.SelectedIndexChanged += (s, e) => OnMainTabSelectedIndexChanged();

            menuRecentSessions.DropDownOpening += (s, e) => PopulateRecentSessionsSubmenu();
            menuFile.DropDownOpening += (s, e) =>
            {
                PopulateRecentSessionsSubmenu();
                PopulateSaveSingleFileSubmenu();
                PopulateExportImportSubmenus();
                UpdateUIForSessionMode();
            };
            if (menuEdit != null)
            {
                menuEdit.DropDownOpening += (s, e) =>
                {
                    UpdateUIForSessionMode();
                    PopulateSetTranslationContentLangSubmenu();
                    PopulateChangeHeaderLangIdSubmenu();
                };
            }
            if (menuTools != null) menuTools.DropDownOpening += (s, e) => UpdateUIForSessionMode();

            PopulateRecentSessionsSubmenu();
            PopulateSaveSingleFileSubmenu();
            PopulateExportImportSubmenus();

            _session.SessionChanged += Session_Changed;

            _searchDebounceTimer = new Timer { Interval = 220 };
            _searchDebounceTimer.Tick += (s, e) =>
            {
                _searchDebounceTimer.Stop();
                PopulateMasterGrid();
            };
            InitializeControlToolTips();
            InitializeBackupsTabControls();
            SetupDragAndDrop();
            NewSingleDocument();
        }

        #region Drag & Drop Support

        private void SetupDragAndDrop()
        {
            this.AllowDrop = true;
            this.DragEnter += MainForm_DragEnter;
            this.DragDrop += MainForm_DragDrop;

            if (gridLabels != null)
            {
                gridLabels.AllowDrop = true;
                gridLabels.DragEnter += MainForm_DragEnter;
                gridLabels.DragDrop += MainForm_DragDrop;
            }

            if (tvCategories != null)
            {
                tvCategories.HideSelection = false;
                tvCategories.AllowDrop = true;
                tvCategories.DragEnter += MainForm_DragEnter;
                tvCategories.DragDrop += MainForm_DragDrop;
                tvCategories.AfterExpand += (s, e) => AdjustCategoryTreeSplitterWidth();
                tvCategories.AfterCollapse += (s, e) => AdjustCategoryTreeSplitterWidth();
            }

            if (tabControlMain != null)
            {
                tabControlMain.AllowDrop = true;
                tabControlMain.DragEnter += MainForm_DragEnter;
                tabControlMain.DragDrop += MainForm_DragDrop;
            }

            if (pnlLanguageEditors != null)
            {
                pnlLanguageEditors.AllowDrop = true;
                pnlLanguageEditors.DragEnter += MainForm_DragEnter;
                pnlLanguageEditors.DragDrop += MainForm_DragDrop;
            }
        }

        private void MainForm_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effect = DragDropEffects.Copy;
            }
            else
            {
                e.Effect = DragDropEffects.None;
            }
        }

        private void MainForm_DragDrop(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;

            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (files == null || files.Length == 0) return;

            var csfFiles = new List<string>();
            var iniFiles = new List<string>();
            var textImportFiles = new List<string>();

            foreach (var path in files)
            {
                if (File.Exists(path))
                {
                    CategorizeDroppedFile(path, csfFiles, iniFiles, textImportFiles);
                }
                else if (Directory.Exists(path))
                {
                    try
                    {
                        foreach (var innerFile in Directory.GetFiles(path, "*.*", SearchOption.AllDirectories))
                        {
                            CategorizeDroppedFile(innerFile, csfFiles, iniFiles, textImportFiles);
                        }
                    }
                    catch { }
                }
            }

            if (csfFiles.Count > 0)
            {
                if (!ConfirmSaveIfModified()) return;
                if (csfFiles.Count == 1)
                {
                    OpenSingleDocumentPath(csfFiles[0]);
                }
                else
                {
                    OpenMultipleDocumentsPaths(csfFiles);
                }
            }
            else if (iniFiles.Count > 0)
            {
                ScanIniFilesPaths(iniFiles);
            }
            else if (textImportFiles.Count > 0)
            {
                foreach (var txtFile in textImportFiles)
                {
                    ImportTextFilePath(txtFile);
                }
            }
        }

        private void CategorizeDroppedFile(string filePath, List<string> csfFiles, List<string> iniFiles, List<string> textImportFiles)
        {
            string ext = Path.GetExtension(filePath)?.ToLowerInvariant();
            if (ext == ".csf")
            {
                csfFiles.Add(filePath);
            }
            else if (ext == ".ini" || ext == ".map")
            {
                iniFiles.Add(filePath);
            }
            else if (ext == ".txt")
            {
                textImportFiles.Add(filePath);
            }
        }

        private void OpenSingleDocumentPath(string filePath)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath)) return;
            SaveLastOpenDirectory(filePath);
            try
            {
                var doc = CsfFileHandler.Load(filePath);
                ToolTipHelper.CheckAndPromptUnknownLanguage(doc, filePath, this);

                _session.SessionChanged -= Session_Changed;
                try
                {
                    ResetSessionState();
                    _session.AddDocument(null, doc, filePath);
                }
                finally
                {
                    _session.SessionChanged += Session_Changed;
                }
                UpdateUIForSessionMode();
                RebuildCategoryTreeAndGrid();
                var existingRecent = RecentSessionsManager.FindRecentSession(_session);
                RecentSessionsManager.AddRecentSession(_session, _appConfig.MaxRecentSessionsItems, existingRecent?.UnpinnedLanguageTags, existingRecent?.LastSelectedKeyName, existingRecent?.ActiveTabName, existingRecent?.ActivePinnedLanguageTag);
                RestoreSessionViewStateFromConfig();
                ShowSaveNotification($"📂 Opened '{Path.GetFileName(filePath)}'");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error opening file:\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void OpenMultipleDocumentsPaths(List<string> filePaths)
        {
            if (filePaths == null || filePaths.Count == 0) return;
            SaveLastOpenDirectory(filePaths[0]);
            try
            {
                // Parse CSF binaries in parallel (stateless reads); prompts and session adds stay on the UI thread.
                var validPaths = filePaths.Where(p => File.Exists(p)).ToList();
                var parsedDocs = new CsfDocument[validPaths.Count];
                try
                {
                    Parallel.For(0, validPaths.Count, i =>
                    {
                        parsedDocs[i] = CsfFileHandler.Load(validPaths[i]);
                    });
                }
                catch (AggregateException aex)
                {
                    throw aex.InnerException ?? aex;
                }

                _session.SessionChanged -= Session_Changed;
                try
                {
                    ResetSessionState();

                    for (int i = 0; i < validPaths.Count; i++)
                    {
                        ToolTipHelper.CheckAndPromptUnknownLanguage(parsedDocs[i], validPaths[i], this);
                        _session.AddDocument(null, parsedDocs[i], validPaths[i]);
                    }
                }
                finally
                {
                    _session.SessionChanged += Session_Changed;
                }

                UpdateUIForSessionMode();
                RebuildCategoryTreeAndGrid();
                var existingRecent = RecentSessionsManager.FindRecentSession(_session);
                RecentSessionsManager.AddRecentSession(_session, _appConfig.MaxRecentSessionsItems, existingRecent?.UnpinnedLanguageTags, existingRecent?.LastSelectedKeyName, existingRecent?.ActiveTabName, existingRecent?.ActivePinnedLanguageTag);
                RestoreSessionViewStateFromConfig();
                ShowSaveNotification($"📂 Opened multi-file session ({filePaths.Count} CSF files)");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error opening files:\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ScanIniFilesPaths(List<string> filePaths)
        {
            if (filePaths == null || filePaths.Count == 0) return;

            string customProps = !string.IsNullOrWhiteSpace(_appConfig?.IniScanProperties)
                ? _appConfig.IniScanProperties
                : null;

            var scannedFilesMap = new Dictionary<string, List<IniScanResult>>(StringComparer.OrdinalIgnoreCase);
            foreach (var iniPath in filePaths)
            {
                var resList = IniScanner.ScanIniFile(iniPath, _session, customProps);
                if (resList != null)
                {
                    scannedFilesMap[iniPath] = resList;
                }
            }

            using (var scanDlg = new IniScanResultDialog(scannedFilesMap, _session))
            {
                var dlgRes = scanDlg.ShowDialog(this);
                if (dlgRes == DialogResult.OK || scanDlg.AnyKeysAdded)
                {
                    UpdateUIForSessionMode();
                    RebuildCategoryTreeAndGrid();
                    PopulateMasterGrid();
                    UpdateFormTitle();
                    ShowSaveNotification($"⚡ INI Scan Complete: Reference keys processed");
                }
            }
        }

        private void ImportTextFilePath(string filePath, CsfSessionDocument preferredTarget = null)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath)) return;

            if (_session == null || _session.Documents.Count == 0)
            {
                NewSingleDocument();
            }

            var defaultDoc = preferredTarget ?? _session?.BaseDocument ?? _session?.Documents.FirstOrDefault();
            if (defaultDoc == null || defaultDoc.Document == null) return;

            List<CsfLabel> importedLabels = null;
            try
            {
                importedLabels = CsfTxtExporterImporter.ImportFromTxt(filePath);
            }
            catch
            {
                ShowSaveNotification($"⚠️ Ignored '{Path.GetFileName(filePath)}': Invalid or unsupported text format");
                return;
            }

            if (importedLabels == null || importedLabels.Count == 0)
            {
                ShowSaveNotification($"⚠️ Ignored '{Path.GetFileName(filePath)}': No valid CSF text entries found");
                return;
            }

            var diffs = CsfTxtExporterImporter.CompareImportDiff(defaultDoc.Document, importedLabels);
            if (diffs == null || diffs.Count == 0)
            {
                ShowSaveNotification($"ℹ️ '{Path.GetFileName(filePath)}': All entries are already up to date");
                return;
            }

            using (var previewDlg = new ImportPreviewDialog(diffs, _session, defaultDoc, importedLabels))
            {
                if (previewDlg.ShowDialog(this) == DialogResult.OK)
                {
                    var selectedItems = previewDlg.DiffList.Where(d => d.ShouldImport).ToList();
                    if (selectedItems.Count == 0)
                    {
                        ShowSaveNotification("ℹ️ Import cancelled (no entries selected)");
                        return;
                    }

                    int importedCount = 0;
                    var contentMode = previewDlg.ContentMode;

                    void ApplyItemsToDoc(CsfSessionDocument sDoc, List<ImportKeyDiff> items)
                    {
                        var targetMap = sDoc.Document.Labels.ToDictionary(l => l.Name, StringComparer.OrdinalIgnoreCase);

                        foreach (var item in items)
                        {
                            if (targetMap.TryGetValue(item.KeyName, out var existingLbl))
                            {
                                if (existingLbl.Strings.Count == 0)
                                {
                                    existingLbl.Strings.Add(new CsfStringEntry(string.Empty, null));
                                }

                                var firstStr = existingLbl.Strings[0];
                                switch (contentMode)
                                {
                                    case ImportContentMode.All:
                                        existingLbl.Strings.Clear();
                                        existingLbl.Strings.Add(new CsfStringEntry(item.ImportedValue, item.ImportedExtra));
                                        break;
                                    case ImportContentMode.KeysOnly:
                                        break;
                                    case ImportContentMode.TextOnly:
                                        firstStr.Value = item.ImportedValue;
                                        break;
                                    case ImportContentMode.AudioOnly:
                                        firstStr.ExtraValue = item.ImportedExtra;
                                        break;
                                }
                            }
                            else
                            {
                                switch (contentMode)
                                {
                                    case ImportContentMode.All:
                                        sDoc.Document.Labels.Add(new CsfLabel(item.KeyName, item.ImportedValue, item.ImportedExtra));
                                        break;
                                    case ImportContentMode.KeysOnly:
                                        sDoc.Document.Labels.Add(new CsfLabel(item.KeyName, string.Empty, null));
                                        break;
                                    case ImportContentMode.TextOnly:
                                        sDoc.Document.Labels.Add(new CsfLabel(item.KeyName, item.ImportedValue, null));
                                        break;
                                    case ImportContentMode.AudioOnly:
                                        sDoc.Document.Labels.Add(new CsfLabel(item.KeyName, string.Empty, item.ImportedExtra));
                                        break;
                                }
                            }
                            importedCount++;
                        }
                        sDoc.IsModified = true;
                    }

                    if (previewDlg.ImportToAllDocuments)
                    {
                        var selectedByDocument = selectedItems
                            .Where(item => item.TargetDocument != null)
                            .GroupBy(item => item.TargetDocument);

                        foreach (var documentGroup in selectedByDocument)
                        {
                            ApplyItemsToDoc(documentGroup.Key, documentGroup.ToList());
                        }
                        ShowSaveNotification($"⚡ Import Complete ({contentMode}): Applied {selectedItems.Count:N0} selected file/key changes across {selectedByDocument.Count():N0} CSF file(s)");
                    }
                    else
                    {
                        var targetDoc = previewDlg.SelectedTargetDocument ?? defaultDoc;
                        ApplyItemsToDoc(targetDoc, selectedItems);
                        ShowSaveNotification($"⚡ Import Complete ({contentMode}): Imported {selectedItems.Count:N0} keys into [{targetDoc.LanguageTag}]");
                    }

                    UpdateUIForSessionMode();
                    RebuildCategoryTreeAndGrid();
                    PopulateMasterGrid();
                    UpdateFormTitle();
                }
            }
        }

        #endregion

        private static string WrapToolTipText(string text, int maxLineLength = 45, int maxLines = 15)
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
                        // If no space or space is too far back (>15 chars), force hard wrap at maxLineLength (~450px)
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

        private static string NormalizeToWinFormsLineBreaks(string str)
        {
            if (string.IsNullOrEmpty(str)) return string.Empty;
            return str.Replace("\r\n", "\n").Replace("\r", "\n").Replace("\n", "\r\n");
        }

        private static void EnableDoubleBuffering(Control control)
        {
            var pi = typeof(Control).GetProperty("DoubleBuffered", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            pi?.SetValue(control, true, null);
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wp, IntPtr lp);

        private const int WM_SETREDRAW = 0x000B;

        private static void LockWindowUpdate(Control control, Action action)
        {
            if (control == null || control.IsDisposed || !control.IsHandleCreated)
            {
                action();
                return;
            }

            try
            {
                SendMessage(control.Handle, WM_SETREDRAW, IntPtr.Zero, IntPtr.Zero);
                action();
            }
            finally
            {
                SendMessage(control.Handle, WM_SETREDRAW, (IntPtr)1, IntPtr.Zero);
                control.Invalidate(true);
                control.Update();
            }
        }

        #region Session & Document Management

        private void ResetSessionState()
        {
            _session.Clear();
            InvalidateMasterRowsCache();
            _undoManager.Clear();
            UpdateUndoRedoMenuItems();

            _recentKeyTimestamps.Clear();
            _modifiedKeyNames.Clear();
            _modifiedKeyMap.Clear();
            _addedKeyNames.Clear();
            _deletedKeyNames.Clear();
            _reorderedKeyDetails.Clear();

            if (_lastActiveSelectedKeys != null) _lastActiveSelectedKeys.Clear();
            if (_gridContextMenuSelectedKeys != null) _gridContextMenuSelectedKeys.Clear();
            _lastSelectedTargetLanguageTag = null;
            _currentlyDisplayedSingleRow = null;
            if (_currentlyRenderedMasterKeyNames != null) _currentlyRenderedMasterKeyNames.Clear();

            if (gridRecent != null) gridRecent.Rows.Clear();
            if (gridUnsaved != null) gridUnsaved.Rows.Clear();
            if (gridCoverage != null) gridCoverage.Rows.Clear();
            if (gridLabels != null) gridLabels.Rows.Clear();
            if (tvCategories != null) tvCategories.Nodes.Clear();

            ClearDetailInspector();

            _unsavedDirty = true;
            _coverageDirty = true;
            _recentDirty = true;
            _keyEditorDirty = true;
        }

        private void NewSingleDocument()
        {
            if (!ConfirmSaveIfModified()) return;

            _session.SessionChanged -= Session_Changed;
            try
            {
                ResetSessionState();

                var defaultLang = _appConfig != null ? _appConfig.DefaultLanguage : CsfLanguage.EnglishUS;
                _session.AddDocument(string.Empty, new CsfDocument { Language = defaultLang }, null);
            }
            finally
            {
                _session.SessionChanged += Session_Changed;
            }

            UpdateUIForSessionMode();
            RebuildCategoryTreeAndGrid();
            UpdateFormTitle();
            if (lblStatusCount != null) lblStatusCount.Text = "Started a new blank CSF session.";
        }

        private void OpenSingleDocument()
        {
            if (!ConfirmSaveIfModified()) return;

            using (var dlg = new OpenFileDialog())
            {
                dlg.Filter = "Command & Conquer String Tables (*.csf)|*.csf|Plain Text UTF-8 (*.txt)|*.txt|All Files (*.*)|*.*";
                dlg.Title = "Open CSF or String Table File";
                InitFileDialogDirectory(dlg);
                if (dlg.ShowDialog() == DialogResult.OK)
                {
                    SaveLastOpenDirectory(dlg.FileName);
                    try
                    {
                        string ext = Path.GetExtension(dlg.FileName).ToLowerInvariant();
                        CsfDocument doc;
                        if (ext == ".txt")
                        {
                            var labels = CsfTxtExporterImporter.ImportFromTxt(dlg.FileName);
                            doc = new CsfDocument { Labels = labels };
                        }
                        else
                        {
                            doc = CsfFileHandler.Load(dlg.FileName);
                            ToolTipHelper.CheckAndPromptUnknownLanguage(doc, dlg.FileName, this);
                        }

                        _session.SessionChanged -= Session_Changed;
                        try
                        {
                            ResetSessionState();
                            _session.AddDocument(null, doc, dlg.FileName);
                        }
                        finally
                        {
                            _session.SessionChanged += Session_Changed;
                        }
                        UpdateUIForSessionMode();
                        RebuildCategoryTreeAndGrid();
                        RecentSessionsManager.AddRecentSession(_session, _appConfig.MaxRecentSessionsItems);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error opening file:\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        private void OpenMultiCsfSession()
        {
            OpenSessionManagerDialog();
        }

        private void OpenSessionManagerDialog()
        {
            using (var dlg = new SessionManagerDialog(_session))
            {
                if (dlg.ShowDialog() == DialogResult.OK)
                {
                    var selectedKeysBeforeSessionChange = CaptureSelectionForRefresh();
                    var existingRecent = RecentSessionsManager.FindRecentSession(_session);
                    string savedKeys = existingRecent?.LastSelectedKeyName ?? string.Empty;
                    string savedUnpinned = existingRecent?.UnpinnedLanguageTags ?? string.Empty;
                    string savedTab = existingRecent?.ActiveTabName ?? string.Empty;
                    string savedPinnedTag = existingRecent?.ActivePinnedLanguageTag ?? string.Empty;

                    var keysToRestore = selectedKeysBeforeSessionChange.Count > 0
                        ? selectedKeysBeforeSessionChange
                        : savedKeys.Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries)
                            .Select(k => k.Trim())
                            .Where(k => !string.IsNullOrEmpty(k))
                            .ToList();

                    // The selected keys can survive a session replacement, but the
                    // inspector controls belong to the old session and must be rebuilt.
                    ClearDetailInspector();

                    _session.SessionChanged -= Session_Changed;
                    try
                    {
                        ResetSessionState();
                        _lastActiveSelectedKeys = new List<string>(keysToRestore);

                        foreach (var item in dlg.SessionItems)
                        {
                            _session.AddDocument(item.UserDefinedLabel, item.Document, item.FilePath);
                            _session.Documents[_session.Documents.Count - 1].TranslationContentLanguage = item.TranslationContentLanguage ?? string.Empty;
                        }
                    }
                    finally
                    {
                        _session.SessionChanged += Session_Changed;
                    }

                    UpdateUIForSessionMode();
                    RebuildCategoryTreeAndGrid();
                    string selectedKeysForRecent = string.Join(";", keysToRestore);
                    RecentSessionsManager.AddRecentSession(_session, _appConfig.MaxRecentSessionsItems, savedUnpinned, selectedKeysForRecent, savedTab, savedPinnedTag);
                    RestoreSessionViewStateFromConfig();
                }
            }
        }

        private void PopulateRecentSessionsSubmenu()
        {
            menuRecentSessions.DropDownItems.Clear();
            var recentList = RecentSessionsManager.GetRecentSessions();

            if (recentList.Count == 0)
            {
                var emptyItem = new ToolStripMenuItem("(No recent sessions)") { Enabled = false };
                menuRecentSessions.DropDownItems.Add(emptyItem);
                return;
            }

            foreach (var item in recentList)
            {
                var menuItem = new ToolStripMenuItem
                {
                    Text = item.MenuDisplayText,
                    ToolTipText = item.ToolTipDetail,
                    Tag = item
                };

                menuItem.Click += (s, e) =>
                {
                    var sessItem = (s as ToolStripMenuItem)?.Tag as RecentSessionItem;
                    if (sessItem != null)
                    {
                        LoadRecentSession(sessItem);
                    }
                };

                menuRecentSessions.DropDownItems.Add(menuItem);
            }

            menuRecentSessions.DropDownItems.Add(new ToolStripSeparator());
            var clearItem = new ToolStripMenuItem("Clear Recent Sessions List");
            clearItem.Click += (s, e) =>
            {
                RecentSessionsManager.ClearRecentSessions();
                PopulateRecentSessionsSubmenu();
            };
            menuRecentSessions.DropDownItems.Add(clearItem);
        }

        private void PopulateSaveSingleFileSubmenu()
        {
            if (_session == null || _session.Documents.Count <= 1)
            {
                menuSaveSingleFile.Visible = false;
                return;
            }

            menuSaveSingleFile.Visible = true;
            menuSaveSingleFile.DropDownItems.Clear();

            foreach (var sDoc in _session.Documents)
            {
                bool isBase = sDoc == _session.BaseDocument;
                string basePrefix = isBase ? "📌 " : "📄 ";
                string modSuffix = sDoc.IsModified ? " *" : string.Empty;
                string title = $"{basePrefix}[{sDoc.LanguageTag}] {sDoc.FileName}{modSuffix}";

                var item = new ToolStripMenuItem(title);
                if (sDoc.IsModified)
                {
                    item.Font = new Font(item.Font, FontStyle.Bold);
                }
                var targetDoc = sDoc;
                item.Click += (s, e) => SaveSessionDocument(targetDoc, false);
                menuSaveSingleFile.DropDownItems.Add(item);
            }
        }

        private void PopulateExportImportSubmenus()
        {
            if (_session == null || _session.Documents.Count == 0) return;

            bool isMulti = _session.Documents.Count > 1;
            var baseDoc = _session.BaseDocument ?? _session.Documents.FirstOrDefault();

            // Export Submenu
            menuExportTxt.DropDownItems.Clear();
            if (isMulti)
            {
                foreach (var sDoc in _session.Documents)
                {
                    bool isBase = sDoc == baseDoc;
                    string prefix = isBase ? "📌 " : "📄 ";
                    string title = $"{prefix}[{sDoc.LanguageTag}] {sDoc.FileName}";
                    var item = new ToolStripMenuItem(title);
                    var target = sDoc;
                    item.Click += (s, e) => PerformExportForDoc(target);
                    menuExportTxt.DropDownItems.Add(item);
                }
            }

            // Export Keys Only Submenu
            menuExportKeysOnly.DropDownItems.Clear();
            if (isMulti)
            {
                foreach (var sDoc in _session.Documents)
                {
                    bool isBase = sDoc == baseDoc;
                    string prefix = isBase ? "📌 " : "📄 ";
                    string title = $"{prefix}[{sDoc.LanguageTag}] {sDoc.FileName}";
                    var item = new ToolStripMenuItem(title);
                    var target = sDoc;
                    item.Click += (s, e) => PerformExportKeyStructureForDoc(target);
                    menuExportKeysOnly.DropDownItems.Add(item);
                }
            }

            // Import Submenu
            menuImportTxt.DropDownItems.Clear();
            if (isMulti)
            {
                foreach (var sDoc in _session.Documents)
                {
                    bool isBase = sDoc == baseDoc;
                    string prefix = isBase ? "📌 " : "📄 ";
                    string title = $"{prefix}[{sDoc.LanguageTag}] {sDoc.FileName}";
                    var item = new ToolStripMenuItem(title);
                    var target = sDoc;
                    item.Click += (s, e) => PerformImportForDoc(target);
                    menuImportTxt.DropDownItems.Add(item);
                }
            }
        }

        private void LoadRecentSession(RecentSessionItem recentSession)
        {
            if (!ConfirmSaveIfModified()) return;
            if (recentSession == null || recentSession.Files.Count == 0) return;

            var missingFiles = recentSession.Files.Where(f => !File.Exists(f.FilePath)).ToList();
            if (missingFiles.Count > 0)
            {
                string missingList = string.Join("\n", missingFiles.Select(f => f.FilePath));
                MessageBox.Show($"Cannot open recent session. The following CSF file(s) no longer exist:\n{missingList}",
                    "File Not Found", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                string savedKeys = recentSession?.LastSelectedKeyName ?? string.Empty;
                string savedUnpinned = recentSession?.UnpinnedLanguageTags ?? string.Empty;
                string savedTab = recentSession?.ActiveTabName ?? string.Empty;
                string savedPinnedTag = recentSession?.ActivePinnedLanguageTag ?? string.Empty;

                // Parse CSF binaries in parallel (stateless reads); prompts and session adds stay on the UI thread.
                var parsedDocs = new CsfDocument[recentSession.Files.Count];
                try
                {
                    Parallel.For(0, recentSession.Files.Count, i =>
                    {
                        parsedDocs[i] = CsfFileHandler.Load(recentSession.Files[i].FilePath);
                    });
                }
                catch (AggregateException aex)
                {
                    throw aex.InnerException ?? aex;
                }

                _session.SessionChanged -= Session_Changed;
                try
                {
                    ResetSessionState();
                    for (int i = 0; i < recentSession.Files.Count; i++)
                    {
                        ToolTipHelper.CheckAndPromptUnknownLanguage(parsedDocs[i], recentSession.Files[i].FilePath, this);
                        _session.AddDocument(recentSession.Files[i].LanguageTag, parsedDocs[i], recentSession.Files[i].FilePath);
                        _session.Documents[_session.Documents.Count - 1].TranslationContentLanguage = recentSession.Files[i].TranslationContentLanguage ?? string.Empty;
                    }
                }
                finally
                {
                    _session.SessionChanged += Session_Changed;
                }

                UpdateUIForSessionMode();
                RebuildCategoryTreeAndGrid();
                RecentSessionsManager.AddRecentSession(_session, _appConfig.MaxRecentSessionsItems, savedUnpinned, savedKeys, savedTab, savedPinnedTag);
                RestoreSessionViewStateFromConfig();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error opening recent session:\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private Timer _saveNotifyTimer;

        public void ShowSaveNotification(string message)
        {
            if (string.IsNullOrEmpty(message)) return;

            if (_saveNotifyTimer == null)
            {
                _saveNotifyTimer = new Timer();
                _saveNotifyTimer.Interval = Math.Max(1000, _appConfig.NotificationToastDurationMs);
                _saveNotifyTimer.Tick += (s, e) =>
                {
                    _saveNotifyTimer.Stop();
                    if (lblSaveNotification != null)
                    {
                        lblSaveNotification.Text = "";
                        lblSaveNotification.BackColor = Color.Transparent;
                    }
                };
            }

            _saveNotifyTimer.Stop();
            _saveNotifyTimer.Interval = Math.Max(1000, _appConfig != null ? _appConfig.NotificationToastDurationMs : 5000);
            if (lblSaveNotification != null)
            {
                lblSaveNotification.Text = message;
                lblSaveNotification.BackColor = Color.FromArgb(220, 245, 220);
                lblSaveNotification.ForeColor = Color.DarkGreen;
                lblSaveNotification.Font = new Font(lblSaveNotification.Font.FontFamily, 9F, FontStyle.Bold);
            }
            _saveNotifyTimer.Start();
        }

        private bool SaveAllDocuments(bool saveAs = false)
        {
            if (_appConfig.AutoCreateBackups && _session.Documents.Any(d => d.IsModified))
            {
                BackupManager.CreateSessionSnapshot(_session, "Save All Documents", _appConfig.BackupDirectoryPath, _appConfig.SaveInAppData);
            }

            int count = 0;
            bool success = true;
            foreach (var sDoc in _session.Documents)
            {
                if (SaveSessionDocument(sDoc, saveAs, skipSessionBackup: true, suppressSingleNotification: true, deferUiRefresh: true))
                {
                    count++;
                }
                else
                {
                    success = false;
                }
            }
            if (success)
            {
                _modifiedKeyNames.Clear();
                _modifiedKeyMap.Clear();
                _addedKeyNames.Clear();
                _deletedKeyNames.Clear();
                _reorderedKeyDetails.Clear();
                _currentlyRenderedMasterKeyNames.Clear();
            }
            UpdateUIForSessionMode();
            RebuildCategoryTreeAndGrid();
            PopulateBackupsTab();
            RefreshActiveSelectionInspector();

            if (success)
            {
                ShowSaveNotification($"💾 Saved all {count} open CSF file(s) successfully! ({DateTime.Now:HH:mm:ss})");
            }
            return success;
        }

        private bool SaveSessionDocument(CsfSessionDocument sDoc, bool saveAs = false, bool skipSessionBackup = false, bool suppressSingleNotification = false, bool deferUiRefresh = false)
        {
            if (sDoc == null) return false;

            if (saveAs || string.IsNullOrEmpty(sDoc.FilePath))
            {
                using (var dlg = new SaveFileDialog())
                {
                    dlg.Filter = "Command & Conquer String Table (*.csf)|*.csf";
                    dlg.Title = $"Save CSF File [{sDoc.LanguageTag}]";
                    dlg.FileName = sDoc.FileName;
                    if (dlg.ShowDialog() != DialogResult.OK) return false;

                    sDoc.FilePath = dlg.FileName;
                }
            }

            try
            {
                if (_appConfig.AutoCreateBackups && !skipSessionBackup && File.Exists(sDoc.FilePath))
                {
                    try
                    {
                        BackupManager.CreateSessionSnapshot(_session, $"Save [{sDoc.LanguageTag}]", _appConfig.BackupDirectoryPath, _appConfig.SaveInAppData);
                    }
                    catch
                    {
                        // Backup failure is non-fatal — proceed with the actual save.
                    }
                }

                CsfFileHandler.Save(sDoc.Document, sDoc.FilePath);
                sDoc.IsModified = false;
                if (!string.IsNullOrEmpty(sDoc.LanguageTag))
                {
                    _modifiedKeyMap.RemoveWhere(k => k.StartsWith($"{sDoc.LanguageTag}:", StringComparison.OrdinalIgnoreCase));
                    _modifiedKeyNames.RemoveWhere(k => !_modifiedKeyMap.Any(m => m.EndsWith($":{k}", StringComparison.OrdinalIgnoreCase)));
                    _currentlyRenderedMasterKeyNames.Clear();
                }
                if (!_session.Documents.Any(d => d.IsModified))
                {
                    _modifiedKeyNames.Clear();
                    _modifiedKeyMap.Clear();
                    _addedKeyNames.Clear();
                    _deletedKeyNames.Clear();
                    _reorderedKeyDetails.Clear();
                }
                if (!deferUiRefresh)
                {
                    UpdateUIForSessionMode();
                    RebuildCategoryTreeAndGrid();
                    PopulateBackupsTab();
                    RefreshActiveSelectionInspector();
                }

                if (!suppressSingleNotification)
                {
                    string fname = string.IsNullOrEmpty(sDoc.FileName) ? "CSF file" : sDoc.FileName;
                    ShowSaveNotification($"💾 Saved [{sDoc.LanguageTag}] {fname} successfully! ({DateTime.Now:HH:mm:ss})");
                }
                return true;
            }
            catch (UnauthorizedAccessException ex)
            {
                // File is read-only or in a protected directory — offer Save As.
                var choice = MessageBox.Show(
                    $"Cannot save [{sDoc.LanguageTag}] — access denied:\n{sDoc.FilePath}\n\n{ex.Message}\n\nDo you want to save to a different location?",
                    "Save Error – Access Denied",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning);

                if (!deferUiRefresh) UpdateUIForSessionMode();

                if (choice == DialogResult.Yes)
                {
                    // Retry as Save As — clear the locked path so the dialog pre-fills the filename only.
                    string originalPath = sDoc.FilePath;
                    sDoc.FilePath = null;
                    bool result = SaveSessionDocument(sDoc, saveAs: true, skipSessionBackup, suppressSingleNotification, deferUiRefresh);
                    if (!result) sDoc.FilePath = originalPath; // restore if user cancels
                    return result;
                }
                return false;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving [{sDoc.LanguageTag}]:\n{ex.Message}", "Save Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                if (!deferUiRefresh) UpdateUIForSessionMode();
                return false;
            }
        }


        private bool ConfirmSaveIfModified()
        {
            bool anyModified = _session.Documents.Any(d => d.IsModified);
            if (!anyModified) return true;

            var res = MessageBox.Show("You have unsaved changes in the active session. Do you want to save them before continuing?",
                "Unsaved Changes", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);

            if (res == DialogResult.Yes) return SaveAllDocuments();
            return res == DialogResult.No;
        }

        private void UpdateTabVisibility(TabPage tabPage, bool shouldBeVisible, int preferredOrder)
        {
            if (tabPage == null || tabControlMain == null) return;

            if (shouldBeVisible)
            {
                if (!tabControlMain.TabPages.Contains(tabPage))
                {
                    int insertIndex = tabControlMain.TabPages.Count;
                    for (int i = 0; i < tabControlMain.TabPages.Count; i++)
                    {
                        var currentTab = tabControlMain.TabPages[i];
                        int currentOrder = 0;
                        if (currentTab == tabKeyEditor) currentOrder = 1;
                        else if (currentTab == tabCoverage) currentOrder = 2;
                        else if (currentTab == tabUnsaved) currentOrder = 3;
                        else if (currentTab == tabRecent) currentOrder = 4;
                        else if (currentTab == tabBackups) currentOrder = 5;

                        if (currentOrder > preferredOrder)
                        {
                            insertIndex = i;
                            break;
                        }
                    }
                    tabControlMain.TabPages.Insert(insertIndex, tabPage);
                }
            }
            else
            {
                if (tabControlMain.TabPages.Contains(tabPage))
                {
                    if (tabControlMain.SelectedTab == tabPage)
                    {
                        tabControlMain.SelectedTab = tabMaster;
                    }
                    tabControlMain.TabPages.Remove(tabPage);
                }
            }
        }

        private void UpdateTabVisibilityState()
        {
            if (tabControlMain == null) return;

            bool hasDocs = _session != null && _session.Documents.Count > 0;
            bool isMulti = _session != null && _session.Documents.Count > 1;
            bool anyModified = hasDocs && _session.Documents.Any(d => d.IsModified);
            bool hasRecentEdits = _recentKeyTimestamps != null && _recentKeyTimestamps.Count > 0;

            bool hasBackups = false;
            if (hasDocs && _session.BaseDocument != null)
            {
                // Scanning the backup folder hits the disk; cache the result and only
                // rescan when the base document changes or a backup is created/restored.
                string curBasePath = _session.BaseDocument.FilePath ?? string.Empty;
                if (!_backupScanValid || !string.Equals(_backupScanBasePath, curBasePath, StringComparison.OrdinalIgnoreCase))
                {
                    _backupScanValid = true;
                    _backupScanBasePath = curBasePath;
                    _backupScanHasSnapshots = false;
                    try
                    {
                        var snaps = BackupManager.GetAvailableSnapshots(_session.BaseDocument.FilePath, _appConfig.BackupDirectoryPath, _appConfig.SaveInAppData);
                        _backupScanHasSnapshots = snaps != null && snaps.Count > 0;
                    }
                    catch { }
                }
                hasBackups = _backupScanHasSnapshots;
            }

            UpdateTabVisibility(tabKeyEditor, true, 1);
            UpdateTabVisibility(tabCoverage, isMulti, 2);
            UpdateTabVisibility(tabUnsaved, anyModified, 3);
            UpdateTabVisibility(tabRecent, hasRecentEdits, 4);
            if (tabBackups != null)
            {
                UpdateTabVisibility(tabBackups, hasBackups, 5);
            }
        }

        private void UpdateUIForSessionMode()
        {
            bool hasDocs = _session != null && _session.Documents.Count > 0;
            bool isMulti = _session != null && _session.Documents.Count > 1;
            bool anyModified = hasDocs && _session.Documents.Any(d => d.IsModified);
            int totalKeys = hasDocs ? GetMasterRows().Count : 0;
            bool hasKeys = hasDocs && totalKeys > 0;

            var activeTab = tabControlMain != null ? tabControlMain.SelectedTab : null;
            bool hasRowSelected = false;
            if (activeTab == tabKeyEditor)
            {
                hasRowSelected = hasKeys && lstKeyEditorKeys != null && lstKeyEditorKeys.SelectedIndices.Count > 0;
            }
            else
            {
                var activeGrid = GetActiveGridForTab(activeTab) ?? gridLabels;
                hasRowSelected = hasKeys && activeGrid != null && activeGrid.SelectedRows.Count > 0;
            }

            // --- FILE MENU ---
            if (menuSave != null) menuSave.Enabled = anyModified;
            if (menuSaveSingleFile != null) menuSaveSingleFile.Enabled = anyModified;
            if (menuSaveAs != null) menuSaveAs.Enabled = hasDocs;
            if (menuExportTxt != null) menuExportTxt.Enabled = hasKeys;
            if (menuExportKeysOnly != null) menuExportKeysOnly.Enabled = hasKeys;
            if (menuImportTxt != null) menuImportTxt.Enabled = hasDocs;

            // --- EDIT MENU & TOOLBAR ---
            if (menuCut != null) menuCut.Enabled = hasRowSelected;
            if (menuCopy != null) menuCopy.Enabled = hasRowSelected;
            if (menuPaste != null) menuPaste.Enabled = hasDocs;
            if (menuAddLabel != null) menuAddLabel.Enabled = hasDocs;
            if (btnAddKeyToolbar != null) btnAddKeyToolbar.Enabled = hasDocs;
            if (menuDeleteLabel != null) menuDeleteLabel.Enabled = hasRowSelected;
            if (btnDeleteKeyToolbar != null) btnDeleteKeyToolbar.Enabled = hasRowSelected;
            if (menuDuplicateKey != null) menuDuplicateKey.Enabled = hasRowSelected;
            if (btnDuplicateKeyToolbar != null) btnDuplicateKeyToolbar.Enabled = hasRowSelected;
            if (menuTrimSpaces != null) menuTrimSpaces.Enabled = hasKeys;
            if (menuCapitalization != null) menuCapitalization.Enabled = hasKeys;
            if (menuUpper != null) menuUpper.Enabled = hasKeys;
            if (menuLower != null) menuLower.Enabled = hasKeys;
            if (menuTitle != null) menuTitle.Enabled = hasKeys;
            if (menuSentence != null) menuSentence.Enabled = hasKeys;
            if (menuMoveUp != null) menuMoveUp.Enabled = hasRowSelected;
            if (menuMoveDown != null) menuMoveDown.Enabled = hasRowSelected;
            if (menuJumpNextEmpty != null) menuJumpNextEmpty.Enabled = hasKeys;
            if (menuJumpPrevEmpty != null) menuJumpPrevEmpty.Enabled = hasKeys;
            if (menuFindReplace != null) menuFindReplace.Enabled = hasKeys;
            if (menuRenameFileLabel != null) menuRenameFileLabel.Enabled = hasDocs;
            if (menuChangeHeaderLangId != null) menuChangeHeaderLangId.Enabled = hasDocs;

            if (btnJumpPrevEmptyToolbar != null) btnJumpPrevEmptyToolbar.Enabled = hasKeys;
            if (btnJumpNextEmptyToolbar != null) btnJumpNextEmptyToolbar.Enabled = hasKeys;

            // --- TOOLS MENU ---
            if (menuBatchRename != null) menuBatchRename.Enabled = hasKeys;
            if (menuSortBinary != null) menuSortBinary.Enabled = hasKeys;
            if (menuSyncKeys != null) menuSyncKeys.Enabled = isMulti && hasKeys;
            if (menuSyncAudioWavs != null) menuSyncAudioWavs.Enabled = isMulti && hasKeys;
            if (menuConvertAnsi != null) menuConvertAnsi.Enabled = hasKeys;
            if (menuClearValuesKeepKeys != null) menuClearValuesKeepKeys.Enabled = hasKeys;
            if (menuScanIni != null) menuScanIni.Enabled = true;

            // --- TOOLBAR SEARCH & FILTERS ---
            if (cboSearchKey != null) cboSearchKey.Enabled = hasKeys;
            if (cboSearchValue != null) cboSearchValue.Enabled = hasKeys;
            if (btnKeyFilterMode != null) btnKeyFilterMode.Enabled = hasKeys;
            if (btnValFilterMode != null) btnValFilterMode.Enabled = hasKeys;
            if (btnFilterLogic != null) btnFilterLogic.Enabled = hasKeys;

            if (cboStatusFilter != null)
            {
                bool showStatusFilter = tabControlMain != null &&
                    (tabControlMain.SelectedTab == tabMaster || tabControlMain.SelectedTab == tabKeyEditor);
                cboStatusFilter.Visible = showStatusFilter;
                cboStatusFilter.Enabled = hasKeys;
                if (sep7 != null) sep7.Visible = showStatusFilter;
            }

            lblSessionMode.Text = !hasDocs ? "Mode: No Files Loaded" : (!isMulti ? "Mode: Single-CSF (1 File)" : $"Mode: Multi-CSF Session ({_session.Documents.Count} Files)");
            lblSessionMode.ForeColor = !hasDocs ? Color.DimGray : (!isMulti ? Color.DarkBlue : Color.DarkGreen);

            if (cboFileFilter != null)
            {
                bool showFileFilter = tabControlMain != null && tabControlMain.SelectedTab == tabMaster;
                lblFileFilter.Visible = showFileFilter;
                cboFileFilter.Visible = showFileFilter;
                if (fileFilterSeparator != null) fileFilterSeparator.Visible = showFileFilter;

                var currentBaseDocument = hasDocs ? _session.BaseDocument : null;
                bool sessionBaseChanged = !ReferenceEquals(_fileFilterSessionBaseDocument, currentBaseDocument);
                var previousSelection = cboFileFilter.SelectedItem;
                cboFileFilter.SelectedIndexChanged -= OnFileFilterChanged;

                var prevSelectedDoc = (previousSelection as DocumentFilterOption)?.Document;
                cboFileFilter.Items.Clear();

                if (hasDocs)
                {
                    if (isMulti)
                    {
                        cboFileFilter.Items.Add("📁 All Open Files");

                        // A new session starts focused on Documents[0], the base CSF.
                        // Keep an existing explicit choice, including All Open Files.
                        int targetIdx = sessionBaseChanged
                            ? 1
                            : (previousSelection is string ? 0 : 1);
                        for (int i = 0; i < _session.Documents.Count; i++)
                        {
                            var sDoc = _session.Documents[i];
                            string fname = string.IsNullOrEmpty(sDoc.FileName) ? "strings.csf" : sDoc.FileName;
                            string prefixLabel = !string.IsNullOrWhiteSpace(sDoc.LanguageTag) ? $"[{sDoc.LanguageTag}] " : "";
                            string isMain = (sDoc == _session.BaseDocument) ? " 📌" : "";
                            var opt = new DocumentFilterOption { Document = sDoc, DisplayName = $"{prefixLabel}{fname}{isMain}" };
                            cboFileFilter.Items.Add(opt);

                            if (!sessionBaseChanged && prevSelectedDoc == sDoc)
                            {
                                targetIdx = i + 1;
                            }
                        }

                        cboFileFilter.SelectedIndex = (targetIdx < cboFileFilter.Items.Count) ? targetIdx : 0;
                        cboFileFilter.Enabled = true;
                        lblFileFilter.Enabled = true;
                    }
                    else
                    {
                        var sDoc = _session.Documents.FirstOrDefault();
                        string fname = string.IsNullOrEmpty(sDoc?.FileName) ? "strings.csf" : sDoc.FileName;
                        string prefixLabel = !string.IsNullOrWhiteSpace(sDoc?.LanguageTag) ? $"[{sDoc.LanguageTag}] " : "";
                        var opt = new DocumentFilterOption { Document = sDoc, DisplayName = $"{prefixLabel}{fname}" };
                        cboFileFilter.Items.Add(opt);
                        cboFileFilter.SelectedIndex = 0;
                        cboFileFilter.Enabled = false;
                        lblFileFilter.Enabled = false;
                    }

                    _fileFilterSessionBaseDocument = currentBaseDocument;

                    // Compute combo width based on longest item text + dropdown arrow only
                    int dropArrowWidth = SystemInformation.VerticalScrollBarWidth + 2;
                    Font font = cboFileFilter.Font ?? SystemFonts.DefaultFont;
                    int maxTextWidth = 0;

                    if (cboFileFilter.Items.Count > 0)
                    {
                        foreach (var item in cboFileFilter.Items)
                        {
                            int textW = TextRenderer.MeasureText(item.ToString(), font).Width;
                            if (textW > maxTextWidth) maxTextWidth = textW;
                        }
                    }

                    int fitWidth = Math.Max(90, Math.Min(210, maxTextWidth + dropArrowWidth));

                    cboFileFilter.AutoSize = false;
                    cboFileFilter.Size = new System.Drawing.Size(fitWidth, 26);
                    cboFileFilter.Width = fitWidth;
                    if (cboFileFilter.ComboBox != null)
                    {
                        cboFileFilter.ComboBox.Width = fitWidth;
                        cboFileFilter.DropDownWidth = Math.Max(fitWidth, maxTextWidth + dropArrowWidth + 5);
                    }
                }
                else
                {
                    cboFileFilter.Enabled = false;
                    lblFileFilter.Enabled = false;
                    _fileFilterSessionBaseDocument = null;
                }

                cboFileFilter.SelectedIndexChanged += OnFileFilterChanged;
            }

            UpdateTabVisibilityState();
            UpdateFormTitle();
        }

        private void OnFileFilterChanged(object sender, EventArgs e)
        {
            RebuildCategoryTreeAndGrid();
        }

        private void UpdateFormTitle()
        {
            bool hasDocs = _session != null && _session.Documents.Count > 0;
            bool anyModified = hasDocs && _session.Documents.Any(d => d.IsModified);

            string status = anyModified ? "*" : "";
            string docName = hasDocs ? Path.GetFileName(_session.Documents[0].FilePath) : null;

            if (!string.IsNullOrEmpty(docName))
            {
                this.Text = $"{status}{docName} - {CsfStudio.AppInfo.WindowTitle}";
            }
            else
            {
                this.Text = $"{status}{CsfStudio.AppInfo.WindowTitle}";
            }

            bool isSingleDoc = _session != null && _session.Documents.Count == 1;

            // Dynamically enable/disable Save actions based on unsaved modifications
            menuSave.Enabled = anyModified;
            menuSaveSingleFile.Enabled = anyModified;
            menuSaveAs.Enabled = hasDocs;

            // Show Rename file label in Edit menu ONLY when exactly 1 CSF file is open
            if (menuRenameFileLabel != null) menuRenameFileLabel.Visible = isSingleDoc;
            // Change Header Language ID is always visible; submenu is populated on DropDownOpening
            if (menuChangeHeaderLangId != null) menuChangeHeaderLangId.Visible = hasDocs;

            UpdateUndoRedoMenuItems();
        }

        private void OnSessionUpdated()
        {
            UpdateUIForSessionMode();
        }

        #endregion

        #region Master Grid & Filtering

        private struct ViewScrollState
        {
            public int GridLabelsScroll;
            public int ListKeyEditorTop;
            public int GridCoverageScroll;
            public int GridUnsavedScroll;
            public int GridRecentScroll;
            public Point MultiScrollPos;
            public List<string> SelectedKeyNames;
        }

        private ViewScrollState SaveCurrentViewScrollState()
        {
            return new ViewScrollState
            {
                GridLabelsScroll = (gridLabels != null && gridLabels.RowCount > 0) ? gridLabels.FirstDisplayedScrollingRowIndex : -1,
                ListKeyEditorTop = (lstKeyEditorKeys != null && lstKeyEditorKeys.Items.Count > 0) ? lstKeyEditorKeys.TopIndex : -1,
                GridCoverageScroll = (gridCoverage != null && gridCoverage.RowCount > 0) ? gridCoverage.FirstDisplayedScrollingRowIndex : -1,
                GridUnsavedScroll = (gridUnsaved != null && gridUnsaved.RowCount > 0) ? gridUnsaved.FirstDisplayedScrollingRowIndex : -1,
                GridRecentScroll = (gridRecent != null && gridRecent.RowCount > 0) ? gridRecent.FirstDisplayedScrollingRowIndex : -1,
                MultiScrollPos = (pnlLanguageEditors != null)
                    ? new Point(Math.Abs(pnlLanguageEditors.AutoScrollPosition.X), Math.Abs(pnlLanguageEditors.AutoScrollPosition.Y))
                    : Point.Empty,
                SelectedKeyNames = (_lastActiveSelectedKeys != null && _lastActiveSelectedKeys.Count > 0)
                    ? new List<string>(_lastActiveSelectedKeys)
                    : GetCurrentlySelectedKeyNames()
            };
        }

        private void RestoreViewScrollState(ViewScrollState state)
        {
            // 1. Restore scroll positions first so grid and list are positioned at saved scroll index
            if (state.GridLabelsScroll >= 0 && gridLabels != null)
            {
                // If a row stream is still filling the grid, re-apply when it completes.
                if (_activeMasterRowStream != null)
                {
                    _pendingScrollRestoreAfterStream = state.GridLabelsScroll;
                }
                if (state.GridLabelsScroll < gridLabels.RowCount)
                {
                    try { gridLabels.FirstDisplayedScrollingRowIndex = state.GridLabelsScroll; } catch { }
                }
            }
            if (state.ListKeyEditorTop >= 0 && lstKeyEditorKeys != null && state.ListKeyEditorTop < lstKeyEditorKeys.Items.Count)
            {
                try { lstKeyEditorKeys.TopIndex = state.ListKeyEditorTop; } catch { }
            }
            if (state.GridCoverageScroll >= 0 && gridCoverage != null && state.GridCoverageScroll < gridCoverage.RowCount)
            {
                try { gridCoverage.FirstDisplayedScrollingRowIndex = state.GridCoverageScroll; } catch { }
            }
            if (state.GridUnsavedScroll >= 0 && gridUnsaved != null && state.GridUnsavedScroll < gridUnsaved.RowCount)
            {
                try { gridUnsaved.FirstDisplayedScrollingRowIndex = state.GridUnsavedScroll; } catch { }
            }
            if (state.GridRecentScroll >= 0 && gridRecent != null && state.GridRecentScroll < gridRecent.RowCount)
            {
                try { gridRecent.FirstDisplayedScrollingRowIndex = state.GridRecentScroll; } catch { }
            }


            // 3. Re-enforce grid scroll index after selection sync
            if (state.GridLabelsScroll >= 0 && gridLabels != null && state.GridLabelsScroll < gridLabels.RowCount)
            {
                try { gridLabels.FirstDisplayedScrollingRowIndex = state.GridLabelsScroll; } catch { }
            }

            // 4. Restore multi-key editor scroll position
            if (state.MultiScrollPos.Y > 0 && pnlLanguageEditors != null)
            {
                pnlLanguageEditors.AutoScrollPosition = state.MultiScrollPos;
                pnlLanguageEditors.BeginInvoke((Action)(() =>
                {
                    try { pnlLanguageEditors.AutoScrollPosition = state.MultiScrollPos; } catch { }
                }));
            }
        }

        private void RefreshMultiKeyCardValuesInPlace(List<string> keyNames)
        {
            if (pnlLanguageEditors == null || pnlLanguageEditors.Controls.Count == 0 || keyNames == null || keyNames.Count == 0) return;

            var keySet = new HashSet<string>(keyNames, StringComparer.OrdinalIgnoreCase);

            foreach (Control ctrl in pnlLanguageEditors.Controls)
            {
                if (ctrl is GroupBox grpBox)
                {
                    string grpKeyName = (grpBox.Tag as MasterKeyRow)?.KeyName ?? (grpBox.Tag as string);
                    if (!string.IsNullOrEmpty(grpKeyName) && keySet.Contains(grpKeyName))
                    {
                        UpdateControlsInContainer(grpBox, grpKeyName);
                    }
                }
            }
        }

        private void UpdateControlsInContainer(Control parent, string keyName)
        {
            if (parent == null) return;
            foreach (Control c in parent.Controls)
            {
                if (c is TextBox txt && txt.Tag is TabPageTagInfo tagInfo)
                {
                    var doc = _session.Documents.FirstOrDefault(d => string.Equals(d.LanguageTag, tagInfo.LanguageTag, StringComparison.OrdinalIgnoreCase));
                    if (doc != null)
                    {
                        var lbl = doc.Document?.Labels?.FirstOrDefault(l => string.Equals(l.Name, keyName, StringComparison.OrdinalIgnoreCase));
                        string val = (lbl != null && lbl.Strings.Count > 0) ? (lbl.Strings[0].Value ?? "") : "";
                        string norm = NormalizeToWinFormsLineBreaks(val);
                        if (txt.Text != norm)
                        {
                            txt.Text = norm;
                            tagInfo.StatusKey = string.IsNullOrEmpty(val) ? "EMPTY" : "MODIFIED";
                            (txt.Parent as Control)?.Invalidate();
                            ((txt.Parent as TabPage)?.Parent as Control)?.Invalidate();
                        }
                    }
                }
                else if (c.HasChildren)
                {
                    UpdateControlsInContainer(c, keyName);
                }
            }
        }

        private List<string> CaptureSelectionForRefresh()
        {
            return _lastActiveSelectedKeys != null && _lastActiveSelectedKeys.Count > 0
                ? new List<string>(_lastActiveSelectedKeys)
                : new List<string>();
        }

        // Updates exactly one entry after an in-place value change (e.g. instant translation):
        // patches the cached master row and the visible grid cell, without rebuilding any list.
        private void UpdateGridRowAfterValueChange(string keyName, CsfSessionDocument changedDoc)
        {
            if (string.IsNullOrEmpty(keyName) || changedDoc == null || changedDoc.Document == null) return;

            if (GetMasterRowsMap().TryGetValue(keyName, out var masterRow))
            {
                var lbl = changedDoc.Document.Labels.FirstOrDefault(l => string.Equals(l.Name, keyName, StringComparison.OrdinalIgnoreCase));
                if (lbl != null && lbl.Strings.Count > 0)
                {
                    masterRow.ValuesPerLanguage[changedDoc.LanguageTag] = lbl.Strings[0];
                    masterRow.MissingLanguages.Remove(changedDoc.LanguageTag);
                }
            }

            if (gridLabels == null || gridLabels.Rows.Count == 0) return;

            bool showIndexCol = gridLabels.Columns.Count > 0 && gridLabels.Columns[0].HeaderText == "#";
            int statusColIdx = showIndexCol ? 1 : 0;
            int firstValueColIdx = showIndexCol ? 3 : 2;

            bool isSingleDocFocused = !_session.IsSingleFileMode && (cboFileFilter?.SelectedItem is DocumentFilterOption opt && opt.Document != null);
            CsfSessionDocument displayedDoc = isSingleDocFocused ? ((DocumentFilterOption)cboFileFilter.SelectedItem).Document : _session.BaseDocument;
            bool affectsDisplayedDoc = ReferenceEquals(displayedDoc, changedDoc) ||
                string.Equals(displayedDoc?.LanguageTag, changedDoc.LanguageTag, StringComparison.OrdinalIgnoreCase);

            foreach (DataGridViewRow r in gridLabels.Rows)
            {
                if (!(r.Tag is MasterKeyRow row) || !string.Equals(row.KeyName, keyName, StringComparison.OrdinalIgnoreCase)) continue;

                string statusIcon = "🟢";
                if (row.Status == KeySyncStatus.MissingInSome) statusIcon = "🔴";
                else if (row.Status == KeySyncStatus.UntranslatedOrEmpty) statusIcon = "🟡";
                if (r.Cells.Count > statusColIdx) r.Cells[statusColIdx].Value = statusIcon;

                if (_session.IsSingleFileMode || isSingleDocFocused)
                {
                    if (affectsDisplayedDoc)
                    {
                        bool exists = row.ValuesPerLanguage.TryGetValue(changedDoc.LanguageTag, out var entry) && entry != null;
                        string val = exists ? (entry.Value ?? string.Empty) : "[Missing Entry]";
                        for (int c = firstValueColIdx; c < gridLabels.Columns.Count; c++)
                        {
                            if (gridLabels.Columns[c].HeaderText == "String Value")
                            {
                                r.Cells[c].Value = val;
                                break;
                            }
                        }
                        // Clear the red "missing" row style once the entry exists with text.
                        if (exists && !string.IsNullOrEmpty(entry.Value) &&
                            r.DefaultCellStyle.BackColor == Color.FromArgb(255, 235, 235))
                        {
                            r.DefaultCellStyle = null;
                        }
                    }
                }
                else
                {
                    for (int c = 0; c < _session.Documents.Count; c++)
                    {
                        if (!ReferenceEquals(_session.Documents[c], changedDoc)) continue;
                        int colIdx = firstValueColIdx + c;
                        if (colIdx < r.Cells.Count)
                        {
                            var docLbl = changedDoc.Document?.Labels?.FirstOrDefault(l => string.Equals(l.Name, keyName, StringComparison.OrdinalIgnoreCase));
                            string cellVal = (docLbl != null && docLbl.Strings.Count > 0)
                                ? (docLbl.Strings[0].Value ?? string.Empty)
                                : "[Missing Entry]";
                            r.Cells[colIdx].Value = cellVal;
                        }
                        break;
                    }
                }

                try { gridLabels.InvalidateRow(r.Index); } catch { }
                break;
            }
        }

        private void RestoreSelectionAfterRefresh(List<string> selectedKeys)
        {
            _currentlyRenderedMasterKeyNames = null;
            _currentlyDisplayedSingleRow = null;

            if (selectedKeys == null || selectedKeys.Count == 0)
            {
                ClearDetailInspector();
                return;
            }

            _lastActiveSelectedKeys = new List<string>(selectedKeys);
            var activeTab = tabControlMain?.SelectedTab;
            if (activeTab == tabKeyEditor)
            {
                SyncSelectionToListBox(lstKeyEditorKeys, selectedKeys);
                OnKeyEditorSelectionChanged();
            }
            else
            {
                var activeGrid = GetActiveGridForTab(activeTab) ?? gridLabels;
                SyncSelectionToGrid(activeGrid, selectedKeys, preserveScrollPosition: true);
                OnGridSelectionChanged(activeGrid);
            }
        }

        public void RefreshDataAfterTextTranslation(List<string> keyNames = null)
        {
            if (_session == null || _session.Documents.Count == 0) return;

            var preservedSelectedKeys = CaptureSelectionForRefresh();

            var docsWithModifications = _session.Documents.Where(d => d.IsModified).ToList();
            if (keyNames != null && keyNames.Count > 0)
            {
                foreach (var k in keyNames)
                {
                    if (string.IsNullOrEmpty(k)) continue;
                    _modifiedKeyNames.Add(k);
                    _recentKeyTimestamps[k] = DateTime.Now;

                    foreach (var doc in docsWithModifications)
                    {
                        _modifiedKeyMap.Add($"{doc.LanguageTag}:{k}");
                    }
                }
            }
            else
            {
                var masterAll = GetMasterRows();
                foreach (var r in masterAll)
                {
                    foreach (var doc in docsWithModifications)
                    {
                        if (doc.Document.Labels.Any(l => string.Equals(l.Name, r.KeyName, StringComparison.OrdinalIgnoreCase)))
                        {
                            _modifiedKeyNames.Add(r.KeyName);
                            _modifiedKeyMap.Add($"{doc.LanguageTag}:{r.KeyName}");
                            _recentKeyTimestamps[r.KeyName] = DateTime.Now;
                        }
                    }
                }
            }

            _unsavedDirty = true;
            _coverageDirty = true;

            UpdateTabVisibilityState();
            UpdateFormTitle();

            var masterRows = GetMasterRows();

            if (gridLabels != null && gridLabels.Rows.Count > 0)
            {
                gridLabels.SuspendLayout();
                try
                {
                    bool filterByKeyList = (keyNames != null && keyNames.Count > 0 && keyNames.Count < 200);
                    var keySet = filterByKeyList ? new HashSet<string>(keyNames, StringComparer.OrdinalIgnoreCase) : null;

                    var masterRowsMap = masterRows.ToDictionary(r => r.KeyName, StringComparer.OrdinalIgnoreCase);

                    bool showIndexCol = (gridLabels.Columns.Count > 0 && gridLabels.Columns[0].HeaderText == "#");
                    int statusColIdx = showIndexCol ? 1 : 0;
                    int keyColIdx = showIndexCol ? 2 : 1;
                    int firstValueColIdx = showIndexCol ? 3 : 2;

                    bool isSingleDocFocused = !_session.IsSingleFileMode && (cboFileFilter?.SelectedItem is DocumentFilterOption opt && opt.Document != null);
                    CsfSessionDocument targetDoc = isSingleDocFocused ? ((DocumentFilterOption)cboFileFilter.SelectedItem).Document : _session.BaseDocument;

                    foreach (DataGridViewRow r in gridLabels.Rows)
                    {
                        string kName = null;
                        if (r.Tag is MasterKeyRow m) kName = m.KeyName;
                        else if (r.Cells.Count > keyColIdx && r.Cells[keyColIdx]?.Value != null) kName = r.Cells[keyColIdx].Value.ToString();

                        if (string.IsNullOrEmpty(kName)) continue;
                        if (keySet != null && !keySet.Contains(kName)) continue;

                        if (masterRowsMap.TryGetValue(kName, out var updatedRow))
                        {
                            r.Tag = updatedRow;

                            // Update Status Icon
                            string statusIcon = "🟢";
                            if (updatedRow.Status == KeySyncStatus.MissingInSome) statusIcon = "🔴";
                            else if (updatedRow.Status == KeySyncStatus.UntranslatedOrEmpty) statusIcon = "🟡";
                            if (r.Cells.Count > statusColIdx) r.Cells[statusColIdx].Value = statusIcon;

                            if (_session.IsSingleFileMode || isSingleDocFocused)
                            {
                                string val = null;
                                if (updatedRow.ValuesPerLanguage.TryGetValue(targetDoc?.LanguageTag ?? "", out var entry))
                                {
                                    val = entry?.Value;
                                }
                                for (int c = firstValueColIdx; c < gridLabels.Columns.Count; c++)
                                {
                                    string header = gridLabels.Columns[c].HeaderText;
                                    string colName = gridLabels.Columns[c].Name;
                                    if (header == "String Value" || colName == "colVal" || colName == "colValue")
                                    {
                                        r.Cells[c].Value = val ?? string.Empty;
                                        break;
                                    }
                                }
                            }
                            else
                            {
                                for (int c = 0; c < _session.Documents.Count; c++)
                                {
                                    var sDoc = _session.Documents[c];
                                    int colIdx = firstValueColIdx + c;
                                    if (colIdx < r.Cells.Count)
                                    {
                                        if (updatedRow.ValuesPerLanguage.TryGetValue(sDoc.LanguageTag, out var entry))
                                        {
                                            r.Cells[colIdx].Value = entry?.Value ?? string.Empty;
                                        }
                                        else
                                        {
                                            r.Cells[colIdx].Value = "[Missing Entry]";
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                finally
                {
                    gridLabels.ResumeLayout(true);
                    gridLabels.Invalidate();
                }
            }

            // The key editor list shows key names only; rebuild it only if the key set changed.
            bool keySetChanged = _keyEditorFilteredRows == null ||
                                 _keyEditorFilteredRows.Count != masterRows.Count ||
                                 !_keyEditorFilteredRows.Select(r => r.KeyName).SequenceEqual(masterRows.Select(r => r.KeyName), StringComparer.OrdinalIgnoreCase);
            if (keySetChanged)
            {
                PopulateKeyEditorList(masterRows);
                _keyEditorDirty = false;
            }
            else
            {
                _keyEditorFilteredRows = masterRows;
            }

            if (tabControlMain.SelectedTab == tabUnsaved) { PopulateUnsavedChangesTab(masterRows); _unsavedDirty = false; }
            else _unsavedDirty = true;

            if (tabCoverage != null && tabControlMain.SelectedTab == tabCoverage) { PopulateCoverageMatrixTab(masterRows); _coverageDirty = false; }
            else _coverageDirty = true;

            RestoreSelectionAfterRefresh(preservedSelectedKeys);

            SaveSessionViewStateToConfig();
        }

        private void UpdateCategoryTreeNodes(List<MasterKeyRow> searchFilteredRows)
        {
            if (tvCategories == null) return;

            _isRebuildingTree = true;
            tvCategories.BeginUpdate();
            try
            {
                string targetCategoryTag = _selectedCategory ?? "[All Labels]";

                var catGroups = searchFilteredRows
                    .Select(r => r.Category ?? string.Empty)
                    .GroupBy(c => c, StringComparer.OrdinalIgnoreCase)
                    .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase);

                tvCategories.Nodes.Clear();

                var rootNode = tvCategories.Nodes.Add($"📁 All Labels ({searchFilteredRows.Count})");
                rootNode.Tag = "[All Labels]";

                TreeNode nodeToSelect = rootNode;

                foreach (var group in catGroups)
                {
                    string displayName = group.First();
                    string catKey = group.Key;
                    int count = group.Count();

                    var catNode = rootNode.Nodes.Add($"📂 {displayName} ({count})");
                    catNode.Tag = catKey;

                    if (string.Equals(catKey, targetCategoryTag, StringComparison.OrdinalIgnoreCase))
                    {
                        nodeToSelect = catNode;
                    }
                }

                rootNode.Expand();
                tvCategories.SelectedNode = nodeToSelect;
                _selectedCategory = nodeToSelect.Tag as string ?? "[All Labels]";
            }
            finally
            {
                tvCategories.EndUpdate();
                _isRebuildingTree = false;
            }
            AdjustCategoryTreeSplitterWidth();
        }

        private void RebuildCategoryTreeAndGrid()
        {
            if (_session == null || _session.Documents.Count == 0)
            {
                InvalidateMasterRowsCache();
                ++_rowStreamToken; // cancel any in-flight row stream
                tvCategories.Nodes.Clear();
                gridLabels.Rows.Clear();
                gridLabels.Columns.Clear();
                _masterGridColumnSignature = null;
                lblStatusCount.Text = "No CSF file loaded";
                ClearDetailInspector();
                return;
            }

            InvalidateMasterRowsCache();
            var scrollState = SaveCurrentViewScrollState();

            _isSyncingSelection = true;
            try
            {
                var masterRows = GetMasterRows();

                // Only the visible tab is populated eagerly; the rest are marked dirty
                // and populated on demand when the user switches to them.
                var activeTab = tabControlMain?.SelectedTab;
                PopulateMasterGrid(masterRows);
                CompleteMasterRowStreamNow();

                if (activeTab == tabUnsaved) { PopulateUnsavedChangesTab(masterRows); _unsavedDirty = false; }
                else _unsavedDirty = true;

                if (activeTab == tabCoverage) { PopulateCoverageMatrixTab(masterRows); _coverageDirty = false; }
                else _coverageDirty = true;

                if (activeTab == tabRecent) { PopulateRecentGrid(); _recentDirty = false; }
                else _recentDirty = true;
            }
            finally
            {
                _isSyncingSelection = false;
            }

            ApplySelectionState(_lastActiveSelectedKeys);
            RestoreViewScrollState(scrollState);
        }

        private void ForcePreRenderAllTabs()
        {
            if (tabControlMain == null || tabControlMain.TabPages.Count == 0) return;
            try
            {
                bool prevSync = _isSyncingSelection;
                _isSyncingSelection = true;
                tabControlMain.SuspendLayout();
                try
                {
                    var origTab = tabControlMain.SelectedTab;
                    foreach (TabPage tab in tabControlMain.TabPages)
                    {
                        if (!tab.IsHandleCreated)
                        {
                            tab.CreateControl();
                        }
                        tabControlMain.SelectedTab = tab;
                        tab.Update();
                    }
                    if (origTab != null)
                    {
                        tabControlMain.SelectedTab = origTab;
                    }
                }
                finally
                {
                    tabControlMain.ResumeLayout(true);
                    _isSyncingSelection = prevSync;
                }
            }
            catch { }
        }

        private void AdjustCategoryTreeSplitterWidth()
        {
            if (splitMain == null || tvCategories == null || tvCategories.Nodes.Count == 0) return;

            try
            {
                int maxTextWidth = 0;

                void MeasureNodes(TreeNodeCollection nodes, int indentLevel)
                {
                    foreach (TreeNode node in nodes)
                    {
                        Size size = TextRenderer.MeasureText(node.Text, tvCategories.Font);
                        int requiredWidth = (indentLevel * 18) + size.Width + 36;
                        if (requiredWidth > maxTextWidth) maxTextWidth = requiredWidth;

                        if (node.IsExpanded && node.Nodes.Count > 0)
                        {
                            MeasureNodes(node.Nodes, indentLevel + 1);
                        }
                    }
                }

                MeasureNodes(tvCategories.Nodes, 0);

                // Keep Master Keys View category tree panel strictly compact (175px to 200px max)
                int targetDistance = Math.Max(175, Math.Min(200, maxTextWidth));

                int minDist = Math.Max(50, splitMain.Panel1MinSize);
                int maxDist = Math.Min(splitMain.Width - 50, splitMain.Width - splitMain.Panel2MinSize);

                splitMain.SplitterDistance = Math.Max(minDist, Math.Min(maxDist, targetDistance));
            }
            catch { }
        }

        private bool? _positionsMatchCache = null;

        private bool DoEntryPositionsMatchAcrossDocuments()
        {
            if (_session == null || _session.Documents.Count <= 1) return true;
            if (_positionsMatchCache.HasValue) return _positionsMatchCache.Value;

            var docs = _session.Documents;
            int firstCount = docs[0].Document.Labels.Count;
            for (int d = 1; d < docs.Count; d++)
            {
                if (docs[d].Document.Labels.Count != firstCount)
                {
                    _positionsMatchCache = false;
                    return false;
                }
            }

            for (int i = 0; i < firstCount; i++)
            {
                string name0 = docs[0].Document.Labels[i].Name;
                for (int d = 1; d < docs.Count; d++)
                {
                    if (!string.Equals(docs[d].Document.Labels[i].Name, name0, StringComparison.OrdinalIgnoreCase))
                    {
                        _positionsMatchCache = false;
                        return false;
                    }
                }
            }
            _positionsMatchCache = true;
            return true;
        }

        private void PopulateMasterGrid(List<MasterKeyRow> masterRows = null)
        {
            if (_rowStreamTimer != null)
            {
                _rowStreamTimer.Stop();
            }
            _activeMasterRowStream = null;

            if (masterRows == null)
            {
                masterRows = GetMasterRows();
            }

            string searchKey = cboSearchKey != null ? cboSearchKey.Text.Trim() : string.Empty;
            string searchValue = cboSearchValue != null ? cboSearchValue.Text.Trim() : string.Empty;

            Regex regexKey = null;
            Regex regexValue = null;
            bool keyRegexInvalid = false;
            bool valRegexInvalid = false;

            if (_keyRegexMode && !string.IsNullOrEmpty(searchKey))
            {
                try
                {
                    regexKey = new Regex(searchKey, RegexOptions.IgnoreCase);
                    if (cboSearchKey != null) cboSearchKey.BackColor = Color.White;
                }
                catch
                {
                    keyRegexInvalid = true;
                    if (cboSearchKey != null) cboSearchKey.BackColor = Color.FromArgb(255, 230, 230);
                }
            }
            else
            {
                if (cboSearchKey != null) cboSearchKey.BackColor = Color.White;
            }

            if (_valRegexMode && !string.IsNullOrEmpty(searchValue))
            {
                try
                {
                    regexValue = new Regex(searchValue, RegexOptions.IgnoreCase);
                    if (cboSearchValue != null) cboSearchValue.BackColor = Color.White;
                }
                catch
                {
                    valRegexInvalid = true;
                    if (cboSearchValue != null) cboSearchValue.BackColor = Color.FromArgb(255, 230, 230);
                }
            }
            else
            {
                if (cboSearchValue != null) cboSearchValue.BackColor = Color.White;
            }

            bool hasKeyFilter = !string.IsNullOrEmpty(searchKey) && (!_keyRegexMode || !keyRegexInvalid);
            bool hasValFilter = !string.IsNullOrEmpty(searchValue) && (!_valRegexMode || !valRegexInvalid);

            int statusFilterIdx = cboStatusFilter != null ? cboStatusFilter.SelectedIndex : 0;
            // 0 = All, 1 = Missing, 2 = Empty, 3 = Complete
            bool isSingleDocFocused = !_session.IsSingleFileMode && (cboFileFilter?.SelectedItem is DocumentFilterOption opt && opt.Document != null);
            CsfSessionDocument focusedDoc = isSingleDocFocused ? ((DocumentFilterOption)cboFileFilter.SelectedItem).Document : null;

            // 1) Filter masterRows by Status, Search Key, Search Text, and File Focus
            var searchFilteredRows = masterRows.Where(row =>
            {
                if (statusFilterIdx > 0)
                {
                    if (isSingleDocFocused && focusedDoc != null)
                    {
                        bool existsInFocused = row.ValuesPerLanguage.TryGetValue(focusedDoc.LanguageTag, out var ent) && ent != null;
                        bool hasTextInFocused = existsInFocused && !string.IsNullOrEmpty(ent.Value);

                        if (statusFilterIdx == 1 && existsInFocused) return false;           // Missing only
                        if (statusFilterIdx == 2 && (!existsInFocused || hasTextInFocused)) return false; // Empty only
                        if (statusFilterIdx == 3 && (!existsInFocused || !hasTextInFocused)) return false; // Complete only
                    }
                    else
                    {
                        if (statusFilterIdx == 1 && row.Status != KeySyncStatus.MissingInSome) return false;
                        if (statusFilterIdx == 2 && row.Status != KeySyncStatus.UntranslatedOrEmpty) return false;
                        if (statusFilterIdx == 3 && row.Status != KeySyncStatus.Complete) return false;
                    }
                }

                bool matchKey = true;
                bool matchValue = true;

                if (hasKeyFilter)
                {
                    if (_keyRegexMode)
                    {
                        matchKey = regexKey != null && regexKey.IsMatch(row.KeyName);
                    }
                    else
                    {
                        matchKey = row.KeyName.IndexOf(searchKey, StringComparison.OrdinalIgnoreCase) >= 0;
                    }
                }

                if (hasValFilter)
                {
                    if (_valRegexMode)
                    {
                        matchValue = row.ValuesPerLanguage.Values.Any(v =>
                            v != null && regexValue != null && regexValue.IsMatch(v.Value ?? string.Empty));
                    }
                    else
                    {
                        matchValue = row.ValuesPerLanguage.Values.Any(v =>
                            v != null && v.Value != null && v.Value.IndexOf(searchValue, StringComparison.OrdinalIgnoreCase) >= 0);
                    }
                }

                if (hasKeyFilter && hasValFilter)
                {
                    return _filterLogicAnd ? (matchKey && matchValue) : (matchKey || matchValue);
                }
                else if (hasKeyFilter)
                {
                    return matchKey;
                }
                else if (hasValFilter)
                {
                    return matchValue;
                }

                return true;
            }).ToList();

            // 2) Update category tree nodes & counts dynamically to reflect searchFilteredRows
            UpdateCategoryTreeNodes(searchFilteredRows);

            // 3) Filter searchFilteredRows by the selected category node
            List<MasterKeyRow> filteredRows;
            if (string.Equals(_selectedCategory, "[All Labels]", StringComparison.OrdinalIgnoreCase))
            {
                filteredRows = searchFilteredRows;
            }
            else
            {
                filteredRows = searchFilteredRows
                    .Where(r => string.Equals(r.Category, _selectedCategory, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            _lastFilteredMasterRows = filteredRows;

            bool positionsMatch = DoEntryPositionsMatchAcrossDocuments();
            bool showIndexCol = _session.IsSingleFileMode || (isSingleDocFocused && positionsMatch);

            // Columns are only rebuilt when the layout structure actually changes;
            // recreating them on every filter keystroke is a major repaint cost.
            var sigBuilder = new System.Text.StringBuilder(64);
            sigBuilder.Append(showIndexCol ? '1' : '0').Append('|')
                      .Append(_session.IsSingleFileMode ? "S" : (isSingleDocFocused ? "F" : "M")).Append('|')
                      .Append(_session.Documents.Count);
            foreach (var d in _session.Documents)
            {
                sigBuilder.Append('|').Append(d.LanguageTag).Append(';').Append(d.FileName);
            }
            string colSignature = sigBuilder.ToString();

            if (!string.Equals(_masterGridColumnSignature, colSignature, StringComparison.Ordinal))
            {
                _masterGridColumnSignature = colSignature;
                try { if (gridLabels.RowCount > 0) gridLabels.FirstDisplayedScrollingRowIndex = 0; } catch { }
                gridLabels.ClearSelection();
                gridLabels.CurrentCell = null;
                gridLabels.Rows.Clear();
                gridLabels.Columns.Clear();

            var colStatus = new DataGridViewTextBoxColumn
            {
                HeaderText = "",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.None,
                Width = 22,
                Resizable = DataGridViewTriState.False,
                ToolTipText = "Synchronization & Text Status:\n🟢 Complete (Valid text)\n🟡 Empty (Blank text in some/all files)\n🔴 Missing (Key missing in some files)",
                ReadOnly = true
            };
            colStatus.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;

            var colKey = new DataGridViewTextBoxColumn
            {
                HeaderText = "Key",
                Width = 220,
                MinimumWidth = 220,
                AutoSizeMode = DataGridViewAutoSizeColumnMode.None,
                ReadOnly = true
            };

            if (_session.IsSingleFileMode || isSingleDocFocused)
            {
                var targetDoc = isSingleDocFocused ? focusedDoc : _session.BaseDocument;
                string fname = targetDoc?.FileName ?? "strings.csf";
                string prefixLabel = !string.IsNullOrWhiteSpace(targetDoc?.LanguageTag) ? $"[{targetDoc.LanguageTag}] " : "";
                string targetDisplayName = $"{prefixLabel}{fname}";

                if (showIndexCol)
                {
                    var colIndex = new DataGridViewTextBoxColumn
                    {
                        HeaderText = "#",
                        Width = 45,
                        AutoSizeMode = DataGridViewAutoSizeColumnMode.None,
                        ReadOnly = true,
                        ToolTipText = $"Index position of entry in {targetDisplayName} (1-based)"
                    };
                    colIndex.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
                    gridLabels.Columns.Add(colIndex);
                }

                gridLabels.Columns.Add(colStatus);
                gridLabels.Columns.Add(colKey);

                var colVal = new DataGridViewTextBoxColumn
                {
                    HeaderText = "String Value",
                    ToolTipText = $"String text value in {targetDisplayName}",
                    AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                    ReadOnly = true
                };
                var colExtra = new DataGridViewTextBoxColumn
                {
                    HeaderText = "Extra Sound",
                    ToolTipText = $"Extra sound filename/data in {targetDisplayName}",
                    Width = 110,
                    AutoSizeMode = DataGridViewAutoSizeColumnMode.None,
                    ReadOnly = true
                };
                gridLabels.Columns.Add(colVal);
                gridLabels.Columns.Add(colExtra);
            }
            else
            {
                gridLabels.Columns.Add(colStatus);
                gridLabels.Columns.Add(colKey);

                foreach (var sDoc in _session.Documents)
                {
                    string fname = string.IsNullOrEmpty(sDoc.FileName) ? "strings.csf" : sDoc.FileName;
                    string prefixLabel = !string.IsNullOrWhiteSpace(sDoc.LanguageTag) ? $"[{sDoc.LanguageTag}] " : "";
                    string headerTitle = $"{prefixLabel}{fname}";
                    string info = string.IsNullOrEmpty(sDoc.FilePath) ? $"File: {headerTitle}" : $"File: {headerTitle}\nPath: {sDoc.FilePath}";
                    gridLabels.Columns.Add(new DataGridViewTextBoxColumn
                    {
                        HeaderText = headerTitle,
                        ToolTipText = info,
                        ReadOnly = true
                    });
                }
            }
            }



            CsfSessionDocument indexTargetDoc = (_session.IsSingleFileMode || isSingleDocFocused)
                ? (isSingleDocFocused ? focusedDoc : _session.BaseDocument)
                : null;
            var labelIndexMap = (showIndexCol && indexTargetDoc != null) ? GetLabelIndexMapFor(indexTargetDoc) : null;

            // Cheap string scan for the Key column width (replaces AutoSizeMode.AllCells).
            string longestKeyText = null;
            int longestKeyLen = 0;
            foreach (var r in filteredRows)
            {
                if (r.KeyName != null && r.KeyName.Length > longestKeyLen)
                {
                    longestKeyLen = r.KeyName.Length;
                    longestKeyText = r.KeyName;
                }
            }

            bool singleOrFocused = _session.IsSingleFileMode || isSingleDocFocused;
            int docCount = _session.Documents.Count;

            DataGridViewRow BuildGridRow(MasterKeyRow row)
            {
                string statusIcon = "🟢";
                if (row.Status == KeySyncStatus.MissingInSome) statusIcon = "🔴";
                else if (row.Status == KeySyncStatus.UntranslatedOrEmpty) statusIcon = "🟡";

                var gridRow = new DataGridViewRow();
                gridRow.Tag = row;

                if (singleOrFocused)
                {
                    var targetDoc = indexTargetDoc;
                    string targetTag = targetDoc?.LanguageTag;
                    int originalIdx = 0;
                    if (labelIndexMap != null && labelIndexMap.TryGetValue(row.KeyName, out int foundIdx))
                    {
                        originalIdx = foundIdx + 1;
                    }

                    string idxDisplay = originalIdx > 0 ? originalIdx.ToString() : "-";

                    CsfStringEntry entry = null;
                    bool existsInTarget = false;
                    if (!string.IsNullOrEmpty(targetTag))
                    {
                        existsInTarget = row.ValuesPerLanguage.TryGetValue(targetTag, out entry);
                    }
                    else
                    {
                        entry = row.ValuesPerLanguage.Values.FirstOrDefault();
                        existsInTarget = (entry != null);
                    }

                    if (existsInTarget && entry != null)
                    {
                        string val = entry.Value ?? string.Empty;
                        string extra = entry.ExtraValue ?? string.Empty;
                        string rowStatusIcon = string.IsNullOrEmpty(val) ? "🟡" : "🟢";

                        if (showIndexCol)
                        {
                            gridRow.CreateCells(gridLabels, idxDisplay, rowStatusIcon, row.KeyName, val, extra);
                        }
                        else
                        {
                            gridRow.CreateCells(gridLabels, rowStatusIcon, row.KeyName, val, extra);
                        }
                    }
                    else
                    {
                        string missingText = "[Missing Entry]";
                        if (showIndexCol)
                        {
                            gridRow.CreateCells(gridLabels, idxDisplay, "🔴", row.KeyName, missingText, "-");
                        }
                        else
                        {
                            gridRow.CreateCells(gridLabels, "🔴", row.KeyName, missingText, "-");
                        }
                        gridRow.DefaultCellStyle.BackColor = Color.FromArgb(255, 235, 235);
                        gridRow.DefaultCellStyle.ForeColor = Color.FromArgb(160, 0, 0);
                    }
                }
                else
                {
                    var cellValues = new object[2 + docCount];
                    cellValues[0] = statusIcon;
                    cellValues[1] = row.KeyName;
                    for (int d = 0; d < docCount; d++)
                    {
                        cellValues[d + 2] = row.ValuesPerLanguage.TryGetValue(_session.Documents[d].LanguageTag, out var entry)
                            ? (object)(entry?.Value ?? string.Empty)
                            : "[Missing Entry]";
                    }
                    gridRow.CreateCells(gridLabels, cellValues);
                }

                gridRow.Tag = row;
                return gridRow;
            }

            void FinishMasterGridPopulate()
            {
                gridLabels.ClearSelection();
                gridLabels.CurrentCell = null;

                int keyColIdx = showIndexCol ? 2 : 1;
                if (gridLabels.Columns.Count > keyColIdx)
                {
                    int keyWidth = 220;
                    if (longestKeyText != null)
                    {
                        int measured = TextRenderer.MeasureText(longestKeyText, gridLabels.Font).Width + 18;
                        keyWidth = Math.Max(220, Math.Min(700, measured));
                    }
                    gridLabels.Columns[keyColIdx].Width = keyWidth;
                }

                if (_pendingScrollRestoreAfterStream >= 0 && _pendingScrollRestoreAfterStream < gridLabels.RowCount)
                {
                    try { gridLabels.FirstDisplayedScrollingRowIndex = _pendingScrollRestoreAfterStream; } catch { }
                }
                _pendingScrollRestoreAfterStream = -1;

                ApplySelectionState(_lastActiveSelectedKeys);

                if (tabControlMain?.SelectedTab == tabKeyEditor)
                {
                    PopulateKeyEditorList(filteredRows);
                    _keyEditorDirty = false;
                }
                else
                {
                    _keyEditorDirty = true;
                }

                if (tabControlMain?.SelectedTab == tabMaster)
                {
                    OnGridSelectionChanged(gridLabels);
                }
            }

            lblStatusCount.Text = $"Visible keys: {filteredRows.Count} of {masterRows.Count}";

            // Any new populate cancels an in-flight stream.
            int streamToken = ++_rowStreamToken;
            _pendingScrollRestoreAfterStream = -1;
            const int StreamThreshold = 1500;

            gridLabels.Visible = true;
            try { if (gridLabels.RowCount > 0) gridLabels.FirstDisplayedScrollingRowIndex = 0; } catch { }
            gridLabels.ClearSelection();
            gridLabels.CurrentCell = null;
            try { gridLabels.Rows.Clear(); } catch { }

            if (filteredRows.Count <= StreamThreshold)
            {
                if (filteredRows.Count > 0)
                {
                    var all = new DataGridViewRow[filteredRows.Count];
                    for (int i = 0; i < filteredRows.Count; i++)
                    {
                        all[i] = BuildGridRow(filteredRows[i]);
                    }
                    try { gridLabels.Rows.AddRange(all); } catch { }
                }
                FinishMasterGridPopulate();
            }
            else
            {
                const int FirstChunkSize = 400;
                int firstChunk = Math.Min(FirstChunkSize, filteredRows.Count);
                var first = new DataGridViewRow[firstChunk];
                for (int i = 0; i < firstChunk; i++)
                {
                    first[i] = BuildGridRow(filteredRows[i]);
                }
                try { gridLabels.Rows.AddRange(first); } catch { }

                _activeMasterRowStream = new MasterRowStreamState
                {
                    Token = streamToken,
                    Rows = filteredRows,
                    NextIndex = firstChunk,
                    ChunkSize = 1500,
                    BuildRow = BuildGridRow,
                    Finish = FinishMasterGridPopulate
                };
                if (_rowStreamTimer == null)
                {
                    _rowStreamTimer = new Timer { Interval = 15 };
                    _rowStreamTimer.Tick += (s, e) => PumpMasterRowStream();
                }
                _rowStreamTimer.Stop();
                _rowStreamTimer.Start();
            }
        }

        private bool _isPopulatingInspector = false;

        private void ClearDetailInspector()
        {
            _pendingEditorBuild = null;
            _isPopulatingInspector = true;
            txtCurrentKeyName.Text = string.Empty;
            txtCurrentKeyName.Tag = null;
            txtCurrentExtraWav.Text = string.Empty;
            _isPopulatingInspector = false;
            _currentlyDisplayedSingleRow = null;
            _currentlyRenderedMasterKeyNames.Clear();
            pnlLanguageEditors.Controls.Clear();
            _langTextEditors.Clear();
            _langLengthLabels.Clear();
            _langLinterLabels.Clear();
            splitMasterDetail.Panel2Collapsed = true;
        }

        private DataGridView GetActiveGridForTab(TabPage tab)
        {
            if (tab == tabMaster) return gridLabels;
            if (tab == tabUnsaved) return gridUnsaved;
            if (tab == tabRecent) return gridRecent;
            if (tab == tabCoverage) return gridCoverage;
            return null;
        }

        private void OnCurrentExtraWavChanged()
        {
            if (_isPopulatingInspector) return;

            string keyName = txtCurrentKeyName.Tag as string ?? txtCurrentKeyName.Text.Trim();
            if (string.IsNullOrEmpty(keyName)) return;

            string newExtra = txtCurrentExtraWav.Text.Trim();
            if (string.IsNullOrEmpty(newExtra)) newExtra = null;

            bool changed = false;
            foreach (var sDoc in _session.Documents)
            {
                var lbl = sDoc.Document.Labels.FirstOrDefault(l => string.Equals(l.Name, keyName, StringComparison.OrdinalIgnoreCase));
                if (lbl != null && lbl.Strings.Count > 0)
                {
                    if (lbl.Strings[0].ExtraValue != newExtra)
                    {
                        lbl.Strings[0].ExtraValue = newExtra;
                        sDoc.IsModified = true;
                        changed = true;
                    }
                }
                else if (!string.IsNullOrEmpty(newExtra))
                {
                    if (lbl == null)
                    {
                        lbl = new CsfLabel(keyName);
                        sDoc.Document.Labels.Add(lbl);
                    }
                    if (lbl.Strings.Count == 0)
                    {
                        lbl.Strings.Add(new CsfStringEntry(string.Empty, newExtra));
                    }
                    else
                    {
                        lbl.Strings[0].ExtraValue = newExtra;
                    }
                    sDoc.IsModified = true;
                    changed = true;
                }
            }

            if (changed)
            {
                if (!_modifiedKeyNames.Contains(keyName)) _modifiedKeyNames.Add(keyName);
                AddRecentEditedKey(keyName);
            }
        }

        private void TriggerGridSelectionChanged(DataGridView activeGrid)
        {
            _selectionDebounceTimer.Stop();
            _pendingGridSelection = null;
            OnGridSelectionChanged(activeGrid);
        }

        private bool _isSyncingSelection = false;

        private void OnGridSelectionChanged(DataGridView activeGrid)
        {
            if (_isPopulatingInspector || _isSyncingSelection) return;
            if (activeGrid == null)
            {
                ClearDetailInspector();
                return;
            }

            var selectedGridRows = activeGrid.Rows.Cast<DataGridViewRow>().Where(r => r.Selected).ToList();
            var selectedRows = new List<MasterKeyRow>();
            var masterKeysMap = GetMasterRowsMap();

            if (selectedGridRows.Count > 0)
            {
                selectedGridRows.Sort((a, b) => a.Index.CompareTo(b.Index));

                foreach (var r in selectedGridRows)
                {
                    string kName = GetKeyNameFromRow(activeGrid, r);

                    if (!string.IsNullOrEmpty(kName) && masterKeysMap.TryGetValue(kName, out var freshRow))
                    {
                        r.Tag = freshRow;
                        selectedRows.Add(freshRow);
                    }
                }
            }

            if (selectedRows.Count == 0)
            {
                _lastActiveSelectedKeys.Clear();
                ClearDetailInspector();
                return;
            }

            _lastActiveSelectedKeys = selectedRows.Select(r => r.KeyName).ToList();
            SaveSessionViewStateToConfig();

            if (tabControlMain.SelectedTab == tabMaster || tabControlMain.SelectedTab == tabUnsaved || tabControlMain.SelectedTab == tabCoverage || tabControlMain.SelectedTab == tabRecent)
            {
                if (selectedRows.Count > 0)
                {
                    splitMasterDetail.Panel2Collapsed = false;

                    if (splitMasterDetail.Height > 100)
                    {
                        int targetPanel2Height = selectedRows.Count > 1 ? Math.Min(350, (int)(splitMasterDetail.Height * 0.50)) : (int)(splitMasterDetail.Height * 0.45);
                        int targetPanel1 = splitMasterDetail.Height - targetPanel2Height;

                        int min = Math.Max(50, splitMasterDetail.Panel1MinSize);
                        int max = Math.Min(splitMasterDetail.Height - 50, splitMasterDetail.Height - splitMasterDetail.Panel2MinSize);

                        try
                        {
                            splitMasterDetail.SplitterDistance = Math.Max(min, Math.Min(max, targetPanel1));
                        }
                        catch { }
                    }
                }
            }
            else
            {
                splitMasterDetail.Panel2Collapsed = true;
            }

            if (selectedRows.Count == 1)
            {
                var row = selectedRows[0];
                if (IsSelectionRenderedInContainer(new List<string> { row.KeyName }, _currentlyRenderedMasterKeyNames))
                {
                    RefreshRenderedTextValuesInPlace(new List<string> { row.KeyName });
                    return;
                }

                _currentlyRenderedMasterKeyNames = new List<string> { row.KeyName };
                pnlDetailHeader.Visible = true;
                _isPopulatingInspector = true;
                txtCurrentKeyName.Text = row.KeyName;
                txtCurrentKeyName.Tag = row.KeyName;
                txtCurrentExtraWav.Text = row.ValuesPerLanguage.Values.FirstOrDefault()?.ExtraValue ?? string.Empty;
                _isPopulatingInspector = false;

                var singleRow = row;
                ScheduleEditorBuild(() => LockWindowUpdate(pnlLanguageEditors, () =>
                {
                    pnlLanguageEditors.Controls.Clear();
                    _langTextEditors.Clear();
                    _langLengthLabels.Clear();
                    _langLinterLabels.Clear();
                    BuildSideBySideEditors(singleRow, pnlLanguageEditors);
                    _currentlyDisplayedSingleRow = singleRow;
                }));
            }
            else
            {
                var keyNames = selectedRows.Select(r => r.KeyName).ToList();
                if (IsSelectionRenderedInContainer(keyNames, _currentlyRenderedMasterKeyNames))
                {
                    RefreshRenderedTextValuesInPlace(keyNames);
                    return;
                }

                _currentlyRenderedMasterKeyNames = keyNames;
                pnlDetailHeader.Visible = false;
                _currentlyDisplayedSingleRow = null;
                var rowsToBuild = selectedRows;
                ScheduleEditorBuild(() => LockWindowUpdate(pnlLanguageEditors, () =>
                {
                    BuildMultiKeyEditors(rowsToBuild);
                }));
            }
        }

        private static bool IsSelectionEqualToRendered(List<string> selected, List<string> rendered)
        {
            if (selected == null || rendered == null) return false;
            if (selected.Count == 0 || rendered.Count == 0) return false;
            if (selected.Count != rendered.Count) return false;

            for (int i = 0; i < selected.Count; i++)
            {
                if (!string.Equals(selected[i], rendered[i], StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }
            return true;
        }

        private bool IsSelectionRenderedInContainer(List<string> selected, List<string> rendered, Panel container = null)
        {
            if (container == null) container = pnlLanguageEditors;
            if (container == null || container.Controls.Count == 0) return false;
            if (container.Controls.Count == 1 && container.Controls[0] is Label) return false;
            if (!IsSelectionEqualToRendered(selected, rendered)) return false;
            return true;
        }

        private void RefreshRenderedTextValuesInPlace(List<string> keyNames)
        {
            if (_session == null || keyNames == null || keyNames.Count == 0) return;

            _isPopulatingInspector = true;
            try
            {
                foreach (var keyName in keyNames)
                {
                    foreach (var sDoc in _session.Documents)
                    {
                        string tag = sDoc.LanguageTag;
                        var lbl = sDoc.Document?.Labels?.FirstOrDefault(l => string.Equals(l.Name, keyName, StringComparison.OrdinalIgnoreCase));
                        string textVal = (lbl != null && lbl.Strings.Count > 0) ? (lbl.Strings[0].Value ?? string.Empty) : string.Empty;
                        string normalized = NormalizeToWinFormsLineBreaks(textVal);

                        if (_langTextEditors.TryGetValue(tag, out var txt))
                        {
                            if (txt.Text != normalized)
                            {
                                txt.Text = normalized;
                            }
                        }
                    }
                }
            }
            finally
            {
                _isPopulatingInspector = false;
            }
        }

        private bool TryUpdateExistingSingleRowEditors(MasterKeyRow row)
        {
            if (_session == null || _session.Documents.Count == 0 || row == null) return false;
            if (_langTextEditors.Count == 0 || _currentlyDisplayedSingleRow == null) return false;
            if (pnlLanguageEditors.Controls.Count == 0) return false;

            var baseDoc = _session.BaseDocument ?? _session.Documents.FirstOrDefault();
            string baseLangTag = baseDoc.LanguageTag;

            if (!_langTextEditors.ContainsKey(baseLangTag)) return false;

            foreach (var doc in _session.Documents)
            {
                if (!_langTextEditors.ContainsKey(doc.LanguageTag)) return false;
            }

            _isPopulatingInspector = true;
            try
            {
                _currentlyDisplayedSingleRow = row;

                bool baseExists = row.ValuesPerLanguage.TryGetValue(baseLangTag, out var baseEntry);
                string baseText = baseExists ? (baseEntry?.Value ?? string.Empty) : string.Empty;

                if (_langTextEditors.TryGetValue(baseLangTag, out var txtBase))
                {
                    txtBase.Text = NormalizeToWinFormsLineBreaks(baseText);
                    txtBase.Tag = row;
                }

                foreach (var sDoc in _session.Documents)
                {
                    if (sDoc == baseDoc) continue;
                    string targetTag = sDoc.LanguageTag;
                    bool exists = row.ValuesPerLanguage.TryGetValue(targetTag, out var entry);
                    string targetText = exists ? (entry?.Value ?? string.Empty) : string.Empty;

                    if (_langTextEditors.TryGetValue(targetTag, out var txtTarget))
                    {
                        txtTarget.Text = NormalizeToWinFormsLineBreaks(targetText);
                        txtTarget.Tag = row;
                    }
                }

                return true;
            }
            finally
            {
                _isPopulatingInspector = false;
            }
        }

        private int _multiBuildToken = 0;

        private void BuildMultiKeyEditors(List<MasterKeyRow> selectedRows, Panel targetContainer = null)
        {
            if (targetContainer == null) targetContainer = pnlLanguageEditors;
            int currentToken = ++_multiBuildToken;

            _isPopulatingInspector = true;
            try
            {
                targetContainer.SuspendLayout();
                targetContainer.Controls.Clear();
                _langTextEditors.Clear();
                _langLengthLabels.Clear();
                _langLinterLabels.Clear();

                if (_session.Documents.Count == 0 || selectedRows == null || selectedRows.Count == 0)
                {
                    targetContainer.ResumeLayout(true);
                    return;
                }

                int maxDisplayCount = _appConfig != null ? _appConfig.MaxMultiKeyDisplayCount : 10;
                var displayRows = selectedRows.Take(maxDisplayCount).ToList();

                var baseDoc = _session.BaseDocument ?? _session.Documents.FirstOrDefault();
                string baseLangTag = baseDoc.LanguageTag;
                var targetDocs = _session.Documents.Where(d => d != baseDoc).ToList();
                var unpinnedDocs = targetDocs.Where(d => _unpinnedTargetLanguageTags.Contains(d.LanguageTag)).ToList();
                var pinnedDocs = targetDocs.Where(d => !_unpinnedTargetLanguageTags.Contains(d.LanguageTag)).ToList();

                if (pinnedDocs.Count > 0 && (string.IsNullOrEmpty(_lastSelectedTargetLanguageTag) || !pinnedDocs.Any(d => string.Equals(d.LanguageTag, _lastSelectedTargetLanguageTag, StringComparison.OrdinalIgnoreCase))))
                {
                    _lastSelectedTargetLanguageTag = pinnedDocs[0].LanguageTag;
                }

                var pnlMultiScroll = new Panel
                {
                    Dock = DockStyle.Fill,
                    Size = targetContainer.ClientSize,
                    AutoScroll = true,
                    AutoScrollMargin = new System.Drawing.Size(0, 15),
                    Visible = false
                };

                targetContainer.Controls.Add(pnlMultiScroll);

                var allKeyTabControls = new List<TabControl>();
                int initialTopOffset = selectedRows.Count > maxDisplayCount ? 32 : 18;
                int topOffset = initialTopOffset;

                int totalColumns = 1 + unpinnedDocs.Count + (pinnedDocs.Count > 0 ? 1 : 0);
                int initialWidth = targetContainer.ClientSize.Width > 100 ? targetContainer.ClientSize.Width - 25 : (targetContainer.Width > 100 ? targetContainer.Width - 25 : 800);
                int lastContainerWidth = Math.Max(350, initialWidth);

                pnlMultiScroll.Resize += (s, e) =>
                {
                    if (_isWindowResizing || pnlMultiScroll.IsDisposed || _multiBuildToken != currentToken) return;
                    int containerWidth = Math.Max(350, (pnlMultiScroll.ClientSize.Width > 100 ? pnlMultiScroll.ClientSize.Width : targetContainer.ClientSize.Width) - 25);

                    pnlMultiScroll.SuspendLayout();
                    foreach (Control ctrl in pnlMultiScroll.Controls)
                    {
                        if (ctrl is GroupBox grp)
                        {
                            grp.Width = containerWidth;
                        }
                        else if (ctrl is Panel banner)
                        {
                            banner.Width = containerWidth;
                        }
                    }
                    pnlMultiScroll.ResumeLayout(false);
                };

                pnlMultiScroll.SuspendLayout();
                var cardControls = new List<Control>();

                if (selectedRows.Count > maxDisplayCount)
                {
                    var pnlBanner = new Panel
                    {
                        Location = new Point(10, 2),
                        Width = lastContainerWidth,
                        Height = 26,
                        BackColor = Color.FromArgb(240, 244, 250),
                        Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
                    };
                    var lblBanner = new Label
                    {
                        Text = $"ℹ️ Displaying editors for the first {maxDisplayCount} of {selectedRows.Count} selected keys.",
                        Font = new Font(FontFamily.GenericSansSerif, 8.5f, FontStyle.Italic),
                        ForeColor = Color.DarkSlateGray,
                        AutoSize = true,
                        Location = new Point(8, 5)
                    };
                    pnlBanner.Controls.Add(lblBanner);
                    cardControls.Add(pnlBanner);
                }

                foreach (var row in displayRows)
                {
                    if (_multiBuildToken != currentToken) return;

                    var grpKey = CreateSingleMultiKeyGroup(row, topOffset, lastContainerWidth, pnlMultiScroll, baseDoc, unpinnedDocs, pinnedDocs, allKeyTabControls);
                    cardControls.Add(grpKey);
                    topOffset += grpKey.Height + 12;
                }

                pnlMultiScroll.Controls.AddRange(cardControls.ToArray());
                pnlMultiScroll.ResumeLayout(true);
                pnlMultiScroll.Visible = true;

                // Force layout pass with true visible container width
                int finalContainerWidth = Math.Max(350, (pnlMultiScroll.ClientSize.Width > 100 ? pnlMultiScroll.ClientSize.Width : targetContainer.ClientSize.Width) - 25);
                if (finalContainerWidth > 100)
                {
                    using (var sampleFont = (Font)Control.DefaultFont.Clone())
                    {
                        int curTop = initialTopOffset;
                        int approxColWidth = finalContainerWidth / Math.Max(1, totalColumns);
                        foreach (Control ctrl in pnlMultiScroll.Controls)
                        {
                            if (ctrl is GroupBox grp && grp.Tag is MasterKeyRow mRow)
                            {
                                int maxWrappedHeight = CalculateMaxWrappedTextHeightForKeyRow(mRow, sampleFont, approxColWidth);
                                int lineH = sampleFont.Height + 2;
                                int min3LinesHeight = lineH * 3 + 6;
                                int tightWrappedHeight = maxWrappedHeight + 12;
                                int editorTextHeight = Math.Max(min3LinesHeight, Math.Min(600, tightWrappedHeight));
                                grp.Height = editorTextHeight + 120;

                                grp.Location = new Point(10, curTop);
                                ForceCardLayoutUpdate(grp, finalContainerWidth);
                                curTop += grp.Height + 12;
                            }
                            else if (ctrl is Panel banner)
                            {
                                banner.Width = finalContainerWidth;
                            }
                        }
                    }
                }
            }
            finally
            {
                targetContainer.ResumeLayout(true);
                _isPopulatingInspector = false;
            }
        }

        private static void ForceCardLayoutUpdate(GroupBox grp, int targetWidth)
        {
            if (grp == null) return;
            grp.Width = targetWidth;

            foreach (Control c1 in grp.Controls)
            {
                if (c1 is Panel pnl)
                {
                    pnl.Width = grp.ClientSize.Width;
                    foreach (Control c2 in pnl.Controls)
                    {
                        if (c2 is TableLayoutPanel tbl)
                        {
                            tbl.Width = pnl.ClientSize.Width;
                            tbl.PerformLayout();
                            tbl.Invalidate();
                        }
                    }
                }
            }
            grp.PerformLayout();
        }

        private static int CalculateMaxWrappedTextHeightForKeyRow(MasterKeyRow row, Font font, int approxColumnWidth)
        {
            if (row == null || row.ValuesPerLanguage == null || row.ValuesPerLanguage.Count == 0)
                return 20;

            int maxMeasuredHeight = 20;
            // Account for: GroupBox border (~6px), column margin (4px), TabControl frame (~8px), TextBox internal padding (~6px), vertical scrollbar (~17px).
            int measureWidth = Math.Max(40, approxColumnWidth - 42);

            foreach (var kvp in row.ValuesPerLanguage)
            {
                string val = kvp.Value?.Value;
                if (!string.IsNullOrEmpty(val))
                {
                    Size measured = TextRenderer.MeasureText(val, font, new Size(measureWidth, 0), TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl);
                    if (measured.Height > maxMeasuredHeight)
                    {
                        maxMeasuredHeight = measured.Height;
                    }
                }
            }
            return maxMeasuredHeight;
        }

        private GroupBox CreateSingleMultiKeyGroup(MasterKeyRow row, int topOffset, int containerWidth, Panel parentContainer, CsfSessionDocument baseDoc, List<CsfSessionDocument> unpinnedDocs, List<CsfSessionDocument> pinnedDocs, List<TabControl> allKeyTabControls)
        {
            int totalColumns = 1 + unpinnedDocs.Count + (pinnedDocs.Count > 0 ? 1 : 0);
            int approxColWidth = containerWidth / Math.Max(1, totalColumns);

            int maxWrappedHeight = 20;
            using (var sampleFont = (Font)Control.DefaultFont.Clone())
            {
                maxWrappedHeight = CalculateMaxWrappedTextHeightForKeyRow(row, sampleFont, approxColWidth);
            }

            int lineH = Control.DefaultFont.Height + 2; // single line height in actual TextBox
            int min3LinesHeight = lineH * 3 + 6; // Ensures at least 3 visible lines
            int tightWrappedHeight = maxWrappedHeight + 12; // Comfortable fit (+12px) so bottom line is never cut off
            int editorTextHeight = Math.Max(min3LinesHeight, Math.Min(600, tightWrappedHeight));

            int maxTabHeaderRows = 1;
            if (_appConfig != null && _appConfig.InspectorMultilineTabs && pinnedDocs.Count > 0)
            {
                using (var font = new Font(FontFamily.GenericSansSerif, 8.5f, FontStyle.Regular))
                {
                    int totalTabWidths = pinnedDocs.Sum(d => TextRenderer.MeasureText(d.LanguageTag, font).Width + 48);
                    int availableWidthForTabs = Math.Max(50, approxColWidth - 10);
                    if (totalTabWidths > availableWidthForTabs)
                    {
                        maxTabHeaderRows = (int)Math.Ceiling((double)totalTabWidths / availableWidthForTabs);
                        maxTabHeaderRows = Math.Max(1, Math.Min(5, maxTabHeaderRows));
                    }
                }
            }
            int extraTabHeaderHeight = (maxTabHeaderRows - 1) * 24;
            int totalCardHeight = editorTextHeight + 120 + extraTabHeaderHeight;

            var grpKey = new GroupBox
            {
                Text = string.Empty,
                Location = new Point(10, topOffset),
                Width = containerWidth,
                Height = totalCardHeight,
                Tag = row,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            grpKey.SuspendLayout();

            var pnlKeyHead = new Panel
            {
                Dock = DockStyle.Top,
                Height = 32
            };

            var lblKey = new Label { Text = "Key:", Location = new Point(10, 8), AutoSize = true, Font = new Font(FontFamily.GenericSansSerif, 8.5f, FontStyle.Bold) };
            var txtKey = new TextBox { Text = row.KeyName, Location = new Point(45, 5), Width = 220, Font = new Font(FontFamily.GenericSansSerif, 8.5f, FontStyle.Bold) };
            var btnRen = new Button { Text = "✏️ Rename", Location = new Point(272, 4), Width = 80, Height = 23 };

            string oldKey = row.KeyName;
            Action doRename = () =>
            {
                string newKey = txtKey.Text.Trim();
                if (!string.IsNullOrEmpty(newKey) && !string.Equals(oldKey, newKey, StringComparison.OrdinalIgnoreCase))
                {
                    foreach (var sDoc in _session.Documents)
                    {
                        var lbl = sDoc.Document.Labels.FirstOrDefault(l => string.Equals(l.Name, oldKey, StringComparison.OrdinalIgnoreCase));
                        if (lbl != null)
                        {
                            lbl.Name = newKey;
                            sDoc.IsModified = true;
                        }
                    }
                    AddRecentEditedKey(newKey);
                    RebuildCategoryTreeAndGrid();
                }
            };

            btnRen.Click += (s, e) => doRename();
            txtKey.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter)
                {
                    doRename();
                    e.SuppressKeyPress = true;
                }
            };

            pnlKeyHead.Controls.Add(lblKey);
            pnlKeyHead.Controls.Add(txtKey);
            pnlKeyHead.Controls.Add(btnRen);

            var pnlEditors = new Panel
            {
                Dock = DockStyle.Fill
            };

            grpKey.Controls.Add(pnlEditors);
            grpKey.Controls.Add(pnlKeyHead);

            BuildSideBySideEditorsForMultiKeyRow(row, pnlEditors, baseDoc, unpinnedDocs, pinnedDocs, _lastSelectedTargetLanguageTag, allKeyTabControls, extraTabHeaderHeight, grpKey, editorTextHeight);

            grpKey.ResumeLayout(false);
            return grpKey;
        }

        private void BuildSideBySideEditorsForMultiKeyRow(MasterKeyRow row, Control targetContainer, CsfSessionDocument baseDoc, List<CsfSessionDocument> unpinnedDocs, List<CsfSessionDocument> pinnedDocs, string activePinnedTag, List<TabControl> allKeyTabControls, int extraTabHeaderHeight = 0, GroupBox parentCard = null, int baseEditorTextHeight = 0)
        {
            targetContainer.SuspendLayout();
            targetContainer.Controls.Clear();

            int totalColumns = 1 + unpinnedDocs.Count + (pinnedDocs.Count > 0 ? 1 : 0);

            var tblLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 1,
                ColumnCount = totalColumns
            };
            tblLayout.SuspendLayout();
            tblLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            for (int col = 0; col < totalColumns; col++)
            {
                tblLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / totalColumns));
            }

            int currentColIndex = 0;

            // --- COLUMN 0: Base Reference ⭐ (100% Editable with Pastel Status Header) ---
            string baseLangTag = baseDoc.LanguageTag;
            bool baseExists = row.ValuesPerLanguage.TryGetValue(baseLangTag, out var baseEntry);
            string baseText = baseExists ? baseEntry.Value : string.Empty;

            string baseStatusKey = !baseExists ? "MISSING" : (string.IsNullOrEmpty(baseText) ? "EMPTY" : (IsKeyModifiedInDoc(baseLangTag, row.KeyName) ? "MODIFIED" : "COMPLETE"));
            string currentBaseStatusKey = baseStatusKey;

            var pnlBaseContainer = new Panel
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(2)
            };

            var pnlBaseHeader = new Panel
            {
                Dock = DockStyle.Top,
                Height = 24
            };

            pnlBaseHeader.Resize += (s, e) => pnlBaseHeader.Invalidate();
            pnlBaseHeader.Paint += (s, e) =>
            {
                var g = e.Graphics;
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

                var (bgNorm, bgSel, textColor) = GetStatusTabColors(currentBaseStatusKey);

                var rect = pnlBaseHeader.ClientRectangle;
                if (rect.Width <= 0 || rect.Height <= 0) return;

                using (var bgBrush = new SolidBrush(bgNorm))
                {
                    g.FillRectangle(bgBrush, rect);
                }

                Color borderColor = Color.FromArgb(Math.Max(0, bgNorm.R - 35), Math.Max(0, bgNorm.G - 35), Math.Max(0, bgNorm.B - 35));
                using (var penBorder = new Pen(borderColor))
                {
                    g.DrawRectangle(penBorder, 0, 0, rect.Width - 1, rect.Height - 1);
                }

                string titleText = baseLangTag;
                using (var emojiFont = new Font("Segoe UI Emoji", 8.5f, FontStyle.Regular))
                using (var font = new Font("Segoe UI", 8.5f, FontStyle.Bold))
                using (var textBrush = new SolidBrush(textColor))
                {
                    g.DrawString("📌", emojiFont, textBrush, 8, 4);
                    g.DrawString(titleText, font, textBrush, 26, 4);
                }
            };

            pnlBaseHeader.MouseDown += (s, e) =>
            {
                if (e.Button == MouseButtons.Right)
                {
                    var ctx = CreateBaseContextMenu(baseDoc, row);
                    ctx.Show(pnlBaseHeader, e.Location);
                }
            };

            pnlBaseContainer.Controls.Add(pnlBaseHeader);

            string bToolTip = ToolTipHelper.WrapText($"Label: {baseLangTag}\nFile: {(string.IsNullOrEmpty(baseDoc.FilePath) ? "Unsaved In-Memory Document" : Path.GetFileName(baseDoc.FilePath))}\nPath: {(string.IsNullOrEmpty(baseDoc.FilePath) ? "-" : baseDoc.FilePath)}\nLength: {baseText.Length} chars", 45);
            _toolTip.SetToolTip(pnlBaseHeader, bToolTip);

            if (!baseExists)
            {
                var pnlBaseMissing = CreateMissingKeyActionPanel(baseDoc, row, pnlBaseContainer, onStatusChanged: (sk) => { currentBaseStatusKey = sk; pnlBaseHeader.Invalidate(); });
                pnlBaseMissing.Dock = DockStyle.Fill;
                pnlBaseContainer.Controls.Add(pnlBaseMissing);
                pnlBaseMissing.BringToFront();
            }
            else
            {
                var txtBase = new TextBox
                {
                    Multiline = true,
                    AcceptsReturn = true,
                    AcceptsTab = false,
                    ScrollBars = ScrollBars.Vertical,
                    Dock = DockStyle.Fill,
                    Text = NormalizeToWinFormsLineBreaks(baseText)
                };

                _toolTip.SetToolTip(txtBase, bToolTip);
                txtBase.ContextMenuStrip = CreateBaseContextMenu(baseDoc, row, txtBase);
                pnlBaseContainer.Controls.Add(txtBase);
                txtBase.BringToFront();

                var pnlBaseAudio = CreateDocAudioPanel(baseDoc, row, () => UpdateFormTitle());
                pnlBaseAudio.Dock = DockStyle.Bottom;
                pnlBaseContainer.Controls.Add(pnlBaseAudio);

                _langTextEditors[baseLangTag] = txtBase;

                string baseInitialVal = baseText;
                txtBase.GotFocus += (s, e) => { baseInitialVal = txtBase.Text; };
                txtBase.LostFocus += (s, e) =>
                {
                    if (baseInitialVal != null && txtBase.Text != baseInitialVal && !_undoManager.IsExecutingUndoRedo)
                    {
                        _undoManager.Execute(new EditValueCommand(baseLangTag, row.KeyName, baseInitialVal, txtBase.Text), _session);
                        UpdateUndoRedoMenuItems();
                        baseInitialVal = txtBase.Text;
                    }
                };

                txtBase.TextChanged += (s, e) =>
                {
                    if (_isPopulatingInspector || _isSyncingSelection) return;
                    if (baseExists && baseEntry != null && NormalizeToWinFormsLineBreaks(baseEntry.Value ?? string.Empty) != txtBase.Text)
                    {
                        baseEntry.Value = txtBase.Text;
                        baseDoc.IsModified = true;
                        currentBaseStatusKey = string.IsNullOrEmpty(txtBase.Text) ? "EMPTY" : "MODIFIED";
                        pnlBaseHeader.Invalidate();
                        MarkKeyAsModified(baseLangTag, row.KeyName);
                        UpdateGridRowAfterValueChange(row.KeyName, baseDoc);
                        UpdateFormTitle();
                    }
                };
            }

            tblLayout.Controls.Add(pnlBaseContainer, currentColIndex++, 0);

            // --- UNPINNED TARGET LANGUAGES (Separate fixed columns) ---
            foreach (var sDoc in unpinnedDocs)
            {
                string targetTag = sDoc.LanguageTag;
                bool exists = row.ValuesPerLanguage.TryGetValue(targetTag, out var entry);
                string targetText = exists ? entry.Value : string.Empty;

                string statusKey = !exists ? "MISSING" : (string.IsNullOrEmpty(targetText) ? "EMPTY" : (IsKeyModifiedInDoc(targetTag, row.KeyName) ? "MODIFIED" : "COMPLETE"));

                var pnlUnpinnedContainer = new Panel
                {
                    Dock = DockStyle.Fill,
                    Margin = new Padding(2)
                };

                var pnlHeader = new Panel
                {
                    Dock = DockStyle.Top,
                    Height = 24
                };

                string currentStatusKey = statusKey;

                pnlHeader.Resize += (s, e) => pnlHeader.Invalidate();
                pnlHeader.Paint += (s, e) =>
                {
                    var g = e.Graphics;
                    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

                    var (bgNorm, bgSel, textColor) = GetStatusTabColors(currentStatusKey);

                    var rect = pnlHeader.ClientRectangle;
                    if (rect.Width <= 0 || rect.Height <= 0) return;

                    using (var bgBrush = new SolidBrush(bgNorm))
                    {
                        g.FillRectangle(bgBrush, rect);
                    }

                    Color borderColor = Color.FromArgb(Math.Max(0, bgNorm.R - 35), Math.Max(0, bgNorm.G - 35), Math.Max(0, bgNorm.B - 35));
                    using (var penBorder = new Pen(borderColor))
                    {
                        g.DrawRectangle(penBorder, 0, 0, rect.Width - 1, rect.Height - 1);
                    }

                    string titleText = targetTag;
                    using (var font = new Font(FontFamily.GenericSansSerif, 8.5f, FontStyle.Bold))
                    using (var textBrush = new SolidBrush(textColor))
                    {
                        g.DrawString(titleText, font, textBrush, 8, 4);
                    }
                };

                pnlHeader.MouseDown += (s, e) =>
                {
                    if (e.Button == MouseButtons.Right)
                    {
                        var ctx = CreateTargetContextMenu(sDoc, targetTag, row);
                        ctx.Show(pnlHeader, e.Location);
                    }
                };

                pnlUnpinnedContainer.Controls.Add(pnlHeader);

                if (!exists)
                {
                    var pnlMissing = CreateMissingKeyActionPanel(sDoc, row, pnlUnpinnedContainer, onStatusChanged: (sk) => { currentStatusKey = sk; pnlHeader.Invalidate(); });
                    pnlMissing.Dock = DockStyle.Fill;
                    pnlUnpinnedContainer.Controls.Add(pnlMissing);
                    pnlMissing.BringToFront();
                }
                else
                {
                    var txtUnpinned = new TextBox
                    {
                        Multiline = true,
                        AcceptsReturn = true,
                        AcceptsTab = false,
                        ScrollBars = ScrollBars.Vertical,
                        Dock = DockStyle.Fill,
                        Text = NormalizeToWinFormsLineBreaks(targetText)
                    };

                    string unpinnedInitialVal = targetText;
                    txtUnpinned.GotFocus += (s, e) => { unpinnedInitialVal = txtUnpinned.Text; };
                    txtUnpinned.LostFocus += (s, e) =>
                    {
                        if (unpinnedInitialVal != null && txtUnpinned.Text != unpinnedInitialVal && !_undoManager.IsExecutingUndoRedo)
                        {
                            _undoManager.Execute(new EditValueCommand(targetTag, row.KeyName, unpinnedInitialVal, txtUnpinned.Text), _session);
                            UpdateUndoRedoMenuItems();
                            unpinnedInitialVal = txtUnpinned.Text;
                        }
                    };

                    txtUnpinned.TextChanged += (s, e) =>
                    {
                        if (_isPopulatingInspector || _isSyncingSelection) return;
                        if (exists && entry != null && NormalizeToWinFormsLineBreaks(entry.Value ?? string.Empty) != txtUnpinned.Text)
                        {
                            entry.Value = txtUnpinned.Text;
                            sDoc.IsModified = true;
                            currentStatusKey = string.IsNullOrEmpty(txtUnpinned.Text) ? "EMPTY" : "MODIFIED";
                            pnlHeader.Invalidate();
                            MarkKeyAsModified(targetTag, row.KeyName);
                            UpdateGridRowAfterValueChange(row.KeyName, sDoc);
                            UpdateFormTitle();
                        }
                    };

                    txtUnpinned.ContextMenuStrip = CreateTargetContextMenu(sDoc, targetTag, row, txtUnpinned);
                    pnlUnpinnedContainer.Controls.Add(txtUnpinned);
                    txtUnpinned.BringToFront();

                    var pnlUnpinnedAudio = CreateDocAudioPanel(sDoc, row, () => UpdateFormTitle());
                    pnlUnpinnedAudio.Dock = DockStyle.Bottom;
                    pnlUnpinnedContainer.Controls.Add(pnlUnpinnedAudio);
                }

                tblLayout.Controls.Add(pnlUnpinnedContainer, currentColIndex++, 0);
            }

            // --- LAST COLUMN: Synchronized Pinned Target Language TabControl ---
            if (pinnedDocs.Count > 0)
            {
                var tabTarget = new TabControl
                {
                    Dock     = DockStyle.Fill,
                    Font     = new Font(FontFamily.GenericSansSerif, 8.5f, FontStyle.Regular),
                    DrawMode = TabDrawMode.OwnerDrawFixed,
                    SizeMode = TabSizeMode.Normal,
                    Multiline = _appConfig != null ? _appConfig.InspectorMultilineTabs : false,
                    Padding  = new Point(24, 4),
                    ShowToolTips = false
                };

                tabTarget.DrawItem += (s, e) =>
                {
                    if (e.Index < 0 || e.Index >= tabTarget.TabPages.Count) return;
                    var page = tabTarget.TabPages[e.Index];
                    bool isSelected = tabTarget.SelectedIndex == e.Index;

                    var info2 = page.Tag as TabPageTagInfo;
                    string sk = info2?.StatusKey ?? "COMPLETE";
                    var (bgNorm, bgSel, tc) = GetStatusTabColors(sk);
                    Color bg = isSelected ? bgSel : bgNorm;

                    using (var bgBrush = new SolidBrush(bg))
                        e.Graphics.FillRectangle(bgBrush, e.Bounds);

                    int sphereSize = 9;
                    int sphereX = e.Bounds.Left + 6;
                    int sphereY = e.Bounds.Top + (e.Bounds.Height - sphereSize) / 2;
                    DrawStatusSphereAt(e.Graphics, sphereX, sphereY, sphereSize, sk);

                    string label = info2?.LanguageTag ?? page.Text;
                    var drawFont = isSelected ? new Font(e.Font, FontStyle.Bold) : e.Font;
                    int textX = sphereX + sphereSize + 5;
                    var textRect = new Rectangle(textX, e.Bounds.Top, Math.Max(10, e.Bounds.Width - (textX - e.Bounds.Left) - 4), e.Bounds.Height);
                    using (var tb = new SolidBrush(tc))
                    {
                        using (var tf = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center, FormatFlags = StringFormatFlags.NoWrap, Trimming = StringTrimming.EllipsisCharacter })
                        {
                            e.Graphics.DrawString(label, drawFont, tb, textRect, tf);
                        }
                    }
                };

                if (parentCard != null && baseEditorTextHeight > 0 && tabTarget.Multiline)
                {
                    tabTarget.Resize += (s, e) =>
                    {
                        if (tabTarget.TabPages.Count == 0 || parentCard.IsDisposed) return;
                        try
                        {
                            int actualHeaderHeight = tabTarget.GetTabRect(tabTarget.TabPages.Count - 1).Bottom;
                            if (actualHeaderHeight > 0)
                            {
                                int currentExtra = Math.Max(0, actualHeaderHeight - 24);
                                int desiredTotalHeight = baseEditorTextHeight + 120 + currentExtra;
                                if (parentCard.Height != desiredTotalHeight)
                                {
                                    parentCard.Height = desiredTotalHeight;
                                }
                            }
                        }
                        catch { }
                    };
                }

                allKeyTabControls.Add(tabTarget);

                foreach (var sDoc in pinnedDocs)
                {
                    string targetTag = sDoc.LanguageTag;
                    bool exists = row.ValuesPerLanguage.TryGetValue(targetTag, out var entry);
                    string targetText = exists ? entry.Value : string.Empty;

                    string statusKey = "COMPLETE";
                    string statusTip = "🟢 Complete Key: Valid text present in file";

                    if (!exists)
                    {
                        statusKey = "MISSING";
                        statusTip = $"🔴 Missing Key: Label '{row.KeyName}' does not exist in [{targetTag}]";
                    }
                    else if (string.IsNullOrEmpty(targetText))
                    {
                        statusKey = "EMPTY";
                        statusTip = $"🟡 Empty Text: Key exists in [{targetTag}], but string text is blank";
                    }
                    else if (IsKeyModifiedInDoc(targetTag, row.KeyName))
                    {
                        statusKey = "MODIFIED";
                        statusTip = $"🔵 Unsaved Changes in [{targetTag}]";
                    }

                    var tagInfo = new TabPageTagInfo { LanguageTag = targetTag, StatusKey = statusKey };

                    var targetPage = new TabPage
                    {
                        Text        = targetTag,
                        ToolTipText = $"{statusTip}\nFile: {sDoc.FileName}",
                        Tag         = tagInfo,
                        BackColor   = Color.White
                    };

                    if (!exists)
                    {
                        var pnlMissing = CreateMissingKeyActionPanel(sDoc, row, tabTarget: tabTarget, targetPage: targetPage);
                        targetPage.Controls.Add(pnlMissing);
                    }
                    else
                    {
                        var txtPinned = new TextBox
                        {
                            Multiline = true,
                            AcceptsReturn = true,
                            AcceptsTab = false,
                            ScrollBars = ScrollBars.Vertical,
                            Dock = DockStyle.Fill,
                            Text = NormalizeToWinFormsLineBreaks(targetText)
                        };

                        string pinnedInitialVal = targetText;
                        txtPinned.GotFocus += (s, e) => { pinnedInitialVal = txtPinned.Text; };
                        txtPinned.LostFocus += (s, e) =>
                        {
                            if (pinnedInitialVal != null && txtPinned.Text != pinnedInitialVal && !_undoManager.IsExecutingUndoRedo)
                            {
                                _undoManager.Execute(new EditValueCommand(targetTag, row.KeyName, pinnedInitialVal, txtPinned.Text), _session);
                                UpdateUndoRedoMenuItems();
                                pinnedInitialVal = txtPinned.Text;
                            }
                        };

                        txtPinned.TextChanged += (s, e) =>
                        {
                            if (_isPopulatingInspector || _isSyncingSelection) return;
                            if (exists && entry != null && NormalizeToWinFormsLineBreaks(entry.Value ?? string.Empty) != txtPinned.Text)
                            {
                                entry.Value = txtPinned.Text;
                                sDoc.IsModified = true;
                                if (tagInfo != null)
                                {
                                    tagInfo.StatusKey = string.IsNullOrEmpty(txtPinned.Text) ? "EMPTY" : "MODIFIED";
                                }
                                tabTarget.Invalidate();
                                MarkKeyAsModified(targetTag, row.KeyName);
                                UpdateFormTitle();
                            }
                        };

                        targetPage.Controls.Add(txtPinned);
                        txtPinned.ContextMenuStrip = CreateTargetContextMenu(sDoc, targetTag, row, txtPinned);
                        var pnlPinnedAudio = CreateDocAudioPanel(sDoc, row, () => UpdateFormTitle());
                        targetPage.Controls.Add(pnlPinnedAudio);


                    }

                    tabTarget.TabPages.Add(targetPage);
                }

                // Select matching tab
                int selIndex = 0;
                for (int i = 0; i < tabTarget.TabPages.Count; i++)
                {
                    string tagStr = (tabTarget.TabPages[i].Tag as TabPageTagInfo)?.LanguageTag ?? (tabTarget.TabPages[i].Tag as string);
                    if (string.Equals(tagStr, _lastSelectedTargetLanguageTag, StringComparison.OrdinalIgnoreCase))
                    {
                        selIndex = i;
                        break;
                    }
                }
                if (tabTarget.TabPages.Count > 0)
                {
                    tabTarget.SelectedIndex = selIndex;
                }

                // Synchronize tab selection across all key TabControls (shared flag prevents cascading)
                tabTarget.SelectedIndexChanged += (s, e) =>
                {
                    if (_isSyncingTabs) return;
                    string selTag = (tabTarget.SelectedTab?.Tag as TabPageTagInfo)?.LanguageTag ?? (tabTarget.SelectedTab?.Tag as string);
                    if (!string.IsNullOrEmpty(selTag))
                    {
                        _lastSelectedTargetLanguageTag = selTag;
                        SaveSessionViewStateToConfig();
                        _isSyncingTabs = true;
                        try
                        {
                            // Find the auto-scroll parent and save its scroll position
                            Panel scrollParent = null;
                            Point savedScroll = Point.Empty;
                            Control parent = tabTarget.Parent;
                            while (parent != null)
                            {
                                if (parent is Panel p && p.AutoScroll)
                                {
                                    scrollParent = p;
                                    savedScroll = p.AutoScrollPosition;
                                    break;
                                }
                                parent = parent.Parent;
                            }

                            pnlLanguageEditors.SuspendLayout();
                            foreach (var otherTab in allKeyTabControls)
                            {
                                if (otherTab != tabTarget && otherTab.TabPages.Count > 0)
                                {
                                    for (int p = 0; p < otherTab.TabPages.Count; p++)
                                    {
                                        string otherTagStr = (otherTab.TabPages[p].Tag as TabPageTagInfo)?.LanguageTag ?? (otherTab.TabPages[p].Tag as string);
                                        if (string.Equals(otherTagStr, selTag, StringComparison.OrdinalIgnoreCase))
                                        {
                                            if (otherTab.SelectedIndex != p)
                                            {
                                                otherTab.SelectedIndex = p;
                                            }
                                            break;
                                        }
                                    }
                                }
                            }

                            // Restore scroll position to prevent viewport jump
                            if (scrollParent != null)
                            {
                                scrollParent.AutoScrollPosition = new Point(Math.Abs(savedScroll.X), Math.Abs(savedScroll.Y));
                            }
                        }
                        finally
                        {
                            pnlLanguageEditors.ResumeLayout(true);
                            _isSyncingTabs = false;
                        }
                    }
                };

                // Right-click context menu on TabControl header tabs
                tabTarget.MouseDown += (s, e) =>
                {
                    if (e.Button == MouseButtons.Right)
                    {
                        for (int i = 0; i < tabTarget.TabPages.Count; i++)
                        {
                            if (tabTarget.GetTabRect(i).Contains(e.Location))
                            {
                                var targetPage = tabTarget.TabPages[i];
                                string langTag = (targetPage.Tag as TabPageTagInfo)?.LanguageTag ?? (targetPage.Tag as string);
                                var targetDoc = _session.Documents.FirstOrDefault(d => string.Equals(d.LanguageTag, langTag, StringComparison.OrdinalIgnoreCase));
                                var ctxMenu = CreateTargetContextMenu(targetDoc, langTag, row);
                                ctxMenu.Show(tabTarget, e.Location);
                                break;
                            }
                        }
                    }
                };

                SetupTabControlToolTips(tabTarget);
                tblLayout.Controls.Add(tabTarget, currentColIndex++, 0);
            }

            tblLayout.ResumeLayout(false);
            targetContainer.Controls.Add(tblLayout);
            targetContainer.ResumeLayout(false);
        }

        private void PopulateUnsavedChangesTab(List<MasterKeyRow> masterRows)
        {
            bool prevSync = _isSyncingSelection;
            _isSyncingSelection = true;
            try
            {
                gridUnsaved.Rows.Clear();
                if (!_session.Documents.Any(d => d.IsModified))
                {
                    _modifiedKeyNames.Clear();
                    _modifiedKeyMap.Clear();
                    _addedKeyNames.Clear();
                    _deletedKeyNames.Clear();
                    return;
                }

                if (masterRows != null)
                {
                    var unsavedRows = masterRows.Where(r => _addedKeyNames.Contains(r.KeyName) || _modifiedKeyNames.Contains(r.KeyName)).ToList();

                    foreach (var row in unsavedRows)
                    {
                        string changeStatus = _addedKeyNames.Contains(row.KeyName)
                            ? "Created"
                            : (_reorderedKeyDetails.TryGetValue(row.KeyName, out var reorderDesc) ? reorderDesc : "Modified");
                        string modTime = _recentKeyTimestamps.TryGetValue(row.KeyName, out var dt)
                            ? dt.ToString("yyyy-MM-dd HH:mm:ss")
                            : DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

                        int idx = gridUnsaved.Rows.Add(
                            row.KeyName,
                            row.Category,
                            changeStatus,
                            modTime
                        );
                        gridUnsaved.Rows[idx].Tag = row;
                    }
                }

                foreach (var deletedKey in _deletedKeyNames)
                {
                    string modTime = _recentKeyTimestamps.TryGetValue(deletedKey, out var dt)
                        ? dt.ToString("yyyy-MM-dd HH:mm:ss")
                        : DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

                    var dummyRow = new MasterKeyRow
                    {
                        KeyName = deletedKey,
                        Category = CsfSession.ExtractCategory(deletedKey)
                    };

                    int idx = gridUnsaved.Rows.Add(
                        deletedKey,
                        dummyRow.Category,
                        "Deleted",
                        modTime
                    );
                    gridUnsaved.Rows[idx].Tag = dummyRow;
                }
            }
            finally
            {
                _isSyncingSelection = prevSync;
            }

            if (_lastActiveSelectedKeys != null && _lastActiveSelectedKeys.Count > 0)
            {
                SyncSelectionToGrid(gridUnsaved, _lastActiveSelectedKeys, preserveScrollPosition: true);
            }
            else
            {
                gridUnsaved.ClearSelection();
                gridUnsaved.CurrentCell = null;
            }
        }

        private CheckBox _chkShowFullCoverage = null;
        private CheckBox _chkShowEmptyEntries = null;
        private Label _lblCoverageFilterInfo = null;
        private string _selectedCoverageFilterFileTag = null;

        private void PopulateCoverageMatrixTab(List<MasterKeyRow> masterRows = null)
        {
            tabCoverage.SuspendLayout();
            pnlCoverageHeader.SuspendLayout();
            bool prevSync = _isSyncingSelection;
            _isSyncingSelection = true;
            try
            {
                pnlCoverageHeader.Dock = DockStyle.Left;
                pnlCoverageHeader.Width = 245;
                pnlCoverageHeader.AutoScroll = true;
                gridCoverage.Dock = DockStyle.Fill;

                pnlCoverageHeader.Controls.Clear();
                gridCoverage.Rows.Clear();

                if (masterRows == null)
                {
                    masterRows = GetMasterRows();
                }

                if (masterRows.Count == 0) return;

                if (_chkShowFullCoverage == null)
                {
                    _chkShowFullCoverage = new CheckBox
                    {
                        Text = "Show 100% complete keys",
                        AutoSize = true,
                        Checked = false
                    };
                    _chkShowFullCoverage.CheckedChanged += (s, e) => PopulateCoverageMatrixTab();
                }

                if (_chkShowEmptyEntries == null)
                {
                    _chkShowEmptyEntries = new CheckBox
                    {
                        Text = "Show 0% empty entries",
                        AutoSize = true,
                        Checked = false
                    };
                    _chkShowEmptyEntries.CheckedChanged += (s, e) => PopulateCoverageMatrixTab();
                }

                bool showFullCoverage = _chkShowFullCoverage.Checked;
                bool showEmptyEntries = _chkShowEmptyEntries.Checked;

                var evaluatedMasterRows = new List<MasterKeyRow>();
                foreach (var row in masterRows)
                {
                    int presentInFiles = 0;
                    foreach (var sDoc in _session.Documents)
                    {
                        bool exists = row.ValuesPerLanguage.TryGetValue(sDoc.LanguageTag, out var entry);
                        bool hasText = exists && !string.IsNullOrEmpty(entry?.Value);
                        if (hasText) presentInFiles++;
                    }

                    bool is100Percent = (presentInFiles == _session.Documents.Count);
                    bool is0Percent = (presentInFiles == 0);
                    bool isKeyModified = IsKeyUnsaved(row.KeyName);

                    if (is100Percent && !showFullCoverage && !isKeyModified) continue;
                    if (is0Percent && !showEmptyEntries && !isKeyModified) continue;

                    evaluatedMasterRows.Add(row);
                }

                int totalKeysEvaluated = evaluatedMasterRows.Count;

                var flowCards = new FlowLayoutPanel
                {
                    Dock = DockStyle.Top,
                    AutoSize = true,
                    AutoSizeMode = AutoSizeMode.GrowAndShrink,
                    WrapContents = false,
                    FlowDirection = FlowDirection.TopDown,
                    Padding = new Padding(6),
                    BackColor = Color.FromArgb(245, 247, 250)
                };

                foreach (var sDoc in _session.Documents)
                {
                    string langTag = sDoc.LanguageTag;
                    int presentCount = evaluatedMasterRows.Count(r => r.ValuesPerLanguage.TryGetValue(langTag, out var v) && !string.IsNullOrEmpty(v?.Value));
                    int missingCount = totalKeysEvaluated - presentCount;
                    double pct = totalKeysEvaluated > 0 ? (presentCount * 100.0) / totalKeysEvaluated : 100.0;
                    bool isFilterActive = string.Equals(_selectedCoverageFilterFileTag, langTag, StringComparison.OrdinalIgnoreCase);

                    var grpCard = new GroupBox
                    {
                        Text = isFilterActive ? $"🔍 FILTER: {langTag}" : $"{langTag} {(sDoc == _session.BaseDocument ? "📌 Base CSF" : "")}",
                        Size = new Size(215, 68),
                        Margin = new Padding(4),
                        Cursor = Cursors.Hand,
                        BackColor = isFilterActive ? Color.FromArgb(210, 232, 255) : Color.White,
                        Font = new Font(FontFamily.GenericSansSerif, 8.5f, isFilterActive ? FontStyle.Bold : FontStyle.Regular)
                    };

                    var pBar = new ProgressBar
                    {
                        Location = new Point(10, 20),
                        Size = new Size(195, 16),
                        Minimum = 0,
                        Maximum = 100,
                        Value = Math.Max(0, Math.Min(100, (int)pct)),
                        Cursor = Cursors.Hand
                    };

                    var lblStats = new Label
                    {
                        Text = $"{presentCount}/{totalKeysEvaluated} ({pct:F1}%) | Missing: {missingCount}",
                        Location = new Point(10, 42),
                        AutoSize = true,
                        ForeColor = missingCount > 0 ? Color.DarkRed : Color.DarkGreen,
                        Cursor = Cursors.Hand
                    };

                    void ToggleFilter(object sender, EventArgs args)
                    {
                        if (string.Equals(_selectedCoverageFilterFileTag, langTag, StringComparison.OrdinalIgnoreCase))
                        {
                            _selectedCoverageFilterFileTag = null;
                        }
                        else
                        {
                            _selectedCoverageFilterFileTag = langTag;
                        }
                        PopulateCoverageMatrixTab();
                    }

                    grpCard.Click += ToggleFilter;
                    pBar.Click += ToggleFilter;
                    lblStats.Click += ToggleFilter;

                    grpCard.Controls.Add(pBar);
                    grpCard.Controls.Add(lblStats);
                    flowCards.Controls.Add(grpCard);
                }

                var pnlSubHeader = new Panel
                {
                    Dock = DockStyle.Top,
                    Height = 85,
                    Padding = new Padding(8, 6, 8, 6),
                    BackColor = Color.FromArgb(238, 242, 248)
                };

                _chkShowFullCoverage.Location = new Point(8, 6);
                _chkShowEmptyEntries.Location = new Point(8, 28);
                pnlSubHeader.Controls.Add(_chkShowFullCoverage);
                pnlSubHeader.Controls.Add(_chkShowEmptyEntries);

                if (_lblCoverageFilterInfo == null)
                {
                    _lblCoverageFilterInfo = new Label
                    {
                        AutoSize = true,
                        Font = new Font(FontFamily.GenericSansSerif, 8.25f, FontStyle.Bold),
                        ForeColor = Color.DarkBlue
                    };
                }

                if (!string.IsNullOrEmpty(_selectedCoverageFilterFileTag))
                {
                    _lblCoverageFilterInfo.Text = $"🔍 Missing in '{_selectedCoverageFilterFileTag}'";
                    _lblCoverageFilterInfo.Location = new Point(8, 54);
                    pnlSubHeader.Controls.Add(_lblCoverageFilterInfo);
                }

                pnlCoverageHeader.Controls.Add(flowCards);
                pnlCoverageHeader.Controls.Add(pnlSubHeader);

                var baseDoc = _session.BaseDocument ?? _session.Documents.FirstOrDefault();
                string baseTag = baseDoc != null ? baseDoc.LanguageTag : string.Empty;

                var coverageGridRows = new List<DataGridViewRow>(evaluatedMasterRows.Count);
                foreach (var row in evaluatedMasterRows)
                {
                    int presentInFiles = 0;
                    var fileBadges = new List<string>();

                    foreach (var sDoc in _session.Documents)
                    {
                        bool exists = row.ValuesPerLanguage.TryGetValue(sDoc.LanguageTag, out var entry);
                        bool hasText = exists && !string.IsNullOrEmpty(entry?.Value);
                        bool isLangModified = _modifiedKeyMap != null && _modifiedKeyMap.Contains($"{sDoc.LanguageTag}:{row.KeyName}");

                        if (hasText)
                        {
                            presentInFiles++;
                            if (isLangModified)
                            {
                                fileBadges.Add($"✏️ {sDoc.LanguageTag} (modified)");
                            }
                            else
                            {
                                fileBadges.Add(sDoc.LanguageTag);
                            }
                        }
                        else if (exists)
                        {
                            if (isLangModified)
                            {
                                fileBadges.Add($"✏️ {sDoc.LanguageTag} (empty, modified)");
                            }
                            else
                            {
                                fileBadges.Add($"{sDoc.LanguageTag} (empty)");
                            }
                        }
                        else
                        {
                            fileBadges.Add($"[Missing: {sDoc.LanguageTag}]");
                        }
                    }

                    if (!string.IsNullOrEmpty(_selectedCoverageFilterFileTag))
                    {
                        bool existsInFilteredFile = row.ValuesPerLanguage.TryGetValue(_selectedCoverageFilterFileTag, out var filterEntry);
                        bool hasTextInFilteredFile = existsInFilteredFile && !string.IsNullOrEmpty(filterEntry?.Value);
                        bool isUnsaved = IsKeyUnsaved(row.KeyName);
                        if (hasTextInFilteredFile && !isUnsaved)
                        {
                            continue;
                        }
                    }

                    string baseText = row.ValuesPerLanguage.TryGetValue(baseTag, out var bVal) ? bVal.Value : string.Empty;
                    if (!string.IsNullOrEmpty(baseText) && baseText.Length > 60)
                    {
                        baseText = baseText.Substring(0, 60) + "...";
                    }

                    double rowPct = (presentInFiles * 100.0) / Math.Max(1, _session.Documents.Count);
                    string statusStr = string.Join("  |  ", fileBadges);
                    string pctStr = $"{rowPct:F0}% ({presentInFiles}/{_session.Documents.Count} files)";

                    var gRow = new DataGridViewRow();
                    gRow.CreateCells(gridCoverage, row.KeyName, row.Category, baseText, statusStr, pctStr);
                    gRow.Tag = row;

                    coverageGridRows.Add(gRow);
                }

                gridCoverage.Rows.AddRange(coverageGridRows.ToArray());
                gridCoverage.ClearSelection();
                gridCoverage.CurrentCell = null;

                if (_lastActiveSelectedKeys != null && _lastActiveSelectedKeys.Count > 0)
                {
                    SyncSelectionToGrid(gridCoverage, _lastActiveSelectedKeys, preserveScrollPosition: true);
                }
                else
                {
                    gridCoverage.ClearSelection();
                    gridCoverage.CurrentCell = null;
                }
            }
            finally
            {
                _isSyncingSelection = prevSync;
                pnlCoverageHeader.ResumeLayout(true);
                tabCoverage.ResumeLayout(true);
            }
        }

        #endregion

        #region Plain Key View Tab (Vertical View)

        private void InitializeKeyEditorTab()
        {
            if (tabKeyEditor == null) return;
            tabKeyEditor.Text = "📋 Plain Keys View";
            tabKeyEditor.ToolTipText = "📋 Plain Keys View: Flat key list on the left with full-screen multi-language editors on the right.";

            splitKeyEditor = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                SplitterDistance = _appConfig?.PlainKeyViewPanelWidth > 0 ? _appConfig.PlainKeyViewPanelWidth : 220,
                FixedPanel = FixedPanel.Panel1,
                BorderStyle = BorderStyle.None
            };
            splitKeyEditor.SplitterMoved += (s, e) =>
            {
                if (_appConfig != null && splitKeyEditor != null && splitKeyEditor.SplitterDistance != _appConfig.PlainKeyViewPanelWidth)
                {
                    _appConfig.PlainKeyViewPanelWidth = splitKeyEditor.SplitterDistance;
                    ConfigManager.SaveConfig(_appConfig);
                }
            };

            lstKeyEditorKeys = new ListBox
            {
                Dock = DockStyle.Fill,
                SelectionMode = SelectionMode.MultiExtended,
                IntegralHeight = false,
                DrawMode = DrawMode.OwnerDrawFixed,
                ItemHeight = 22,
                Font = new Font("Segoe UI", 9f)
            };
            lstKeyEditorKeys.DrawItem += LstKeyEditorKeys_DrawItem;
            lstKeyEditorKeys.SelectedIndexChanged += (s, e) => OnKeyEditorSelectionChanged();

            splitKeyEditor.Panel1.Controls.Add(lstKeyEditorKeys);

            pnlPlainKeyRight = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(4)
            };

            pnlKeyEditorEditors = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true
            };
            EnableDoubleBuffering(pnlKeyEditorEditors);
            pnlPlainKeyRight.Controls.Add(pnlKeyEditorEditors);

            splitKeyEditor.Panel2.Controls.Add(pnlPlainKeyRight);
            tabKeyEditor.Controls.Clear();
            tabKeyEditor.Controls.Add(splitKeyEditor);
        }

        private string GetRowStatusKind(MasterKeyRow row)
        {
            if (row == null || _session == null || _session.Documents.Count == 0) return "COMPLETE";

            bool hasMissing = false;
            bool hasEmpty = false;

            foreach (var doc in _session.Documents)
            {
                if (!row.ValuesPerLanguage.TryGetValue(doc.LanguageTag, out var entry))
                {
                    hasMissing = true;
                }
                else if (string.IsNullOrEmpty(entry?.Value))
                {
                    hasEmpty = true;
                }
            }

            if (hasMissing) return "MISSING";
            if (hasEmpty) return "EMPTY";

            bool isModified = _modifiedKeyNames != null && _modifiedKeyNames.Contains(row.KeyName);
            if (isModified) return "MODIFIED";

            return "COMPLETE";
        }

        private void LstKeyEditorKeys_DrawItem(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0 || _keyEditorFilteredRows == null || e.Index >= _keyEditorFilteredRows.Count) return;

            bool isSelected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
            var row = _keyEditorFilteredRows[e.Index];

            string statusKind = GetRowStatusKind(row);
            var (bgNormal, _, textColor) = GetStatusTabColors(statusKind);

            Color bg = isSelected ? SystemColors.Highlight : (statusKind == "COMPLETE" ? Color.White : bgNormal);
            Color fg = isSelected ? SystemColors.HighlightText : (statusKind == "COMPLETE" ? SystemColors.ControlText : textColor);

            using (var bgBrush = new SolidBrush(bg))
            {
                e.Graphics.FillRectangle(bgBrush, e.Bounds);
            }

            bool positionsMatch = DoEntryPositionsMatchAcrossDocuments();
            bool showIndexCol = _session != null && (_session.IsSingleFileMode || positionsMatch);

            int textX = e.Bounds.Left + 6;

            if (showIndexCol)
            {
                int baseIdx = -1;
                var baseDoc = _session?.BaseDocument ?? _session?.Documents.FirstOrDefault();
                if (baseDoc != null)
                {
                    var idxMap = GetLabelIndexMapFor(baseDoc);
                    if (idxMap != null && idxMap.TryGetValue(row.KeyName, out int foundIdx))
                    {
                        baseIdx = foundIdx;
                    }
                }

                string idxStr = (baseIdx >= 0 ? baseIdx + 1 : e.Index + 1).ToString();

                Color idxColor = isSelected ? Color.FromArgb(210, 235, 255) : Color.FromArgb(120, 130, 150);
                using (var idxFont = new Font("Segoe UI", 8.5f, FontStyle.Regular))
                using (var idxBrush = new SolidBrush(idxColor))
                using (var sfRight = new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center })
                {
                    var idxRect = new Rectangle(e.Bounds.Left + 2, e.Bounds.Top, 36, e.Bounds.Height);
                    e.Graphics.DrawString(idxStr, idxFont, idxBrush, idxRect, sfRight);
                }
                textX += 44;
            }

            using (var keyBrush = new SolidBrush(fg))
            using (var sfLeft = new StringFormat(StringFormatFlags.NoWrap)
            {
                Alignment = StringAlignment.Near,
                LineAlignment = StringAlignment.Center,
                Trimming = StringTrimming.EllipsisCharacter
            })
            {
                var keyRect = new Rectangle(textX, e.Bounds.Top, Math.Max(10, e.Bounds.Width - textX - 2), e.Bounds.Height);
                e.Graphics.DrawString(row.KeyName, e.Font, keyBrush, keyRect, sfLeft);
            }

            e.DrawFocusRectangle();
        }

        private void AdjustKeyEditorSplitterDistance()
        {
            if (splitKeyEditor == null || lstKeyEditorKeys == null || _keyEditorFilteredRows == null || _keyEditorFilteredRows.Count == 0) return;

            try
            {
                bool positionsMatch = DoEntryPositionsMatchAcrossDocuments();
                bool showIndexCol = _session != null && (_session.IsSingleFileMode || positionsMatch);
                int indexColWidth = showIndexCol ? 48 : 0;

                var maxLenRow = _keyEditorFilteredRows
                    .Where(r => !string.IsNullOrEmpty(r?.KeyName))
                    .OrderByDescending(r => r.KeyName.Length)
                    .FirstOrDefault();

                int maxKeyWidth = 0;
                if (maxLenRow != null)
                {
                    using (var g = lstKeyEditorKeys.CreateGraphics())
                    {
                        Size sz = TextRenderer.MeasureText(g, maxLenRow.KeyName, lstKeyEditorKeys.Font);
                        maxKeyWidth = sz.Width;
                    }
                }

                int neededWidth = Math.Max(220, Math.Min(650, indexColWidth + maxKeyWidth + 44));
                splitKeyEditor.SplitterDistance = neededWidth;
            }
            catch { }
        }

        private void ShowPlainKeyEditorEmptyPlaceholder()
        {
            pnlDetailHeader.Visible = false;
            _currentlyDisplayedSingleRow = null;
            _currentlyRenderedMasterKeyNames.Clear();
            _pendingEditorBuild = null;
            if (pnlKeyEditorEditors != null)
            {
                pnlKeyEditorEditors.Controls.Clear();
                var lblEmpty = new Label
                {
                    Text = "👈 Select a key from the list on the left to view and edit string values.",
                    Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleCenter,
                    Font = new Font("Segoe UI", 10.5f, FontStyle.Italic),
                    ForeColor = Color.Gray
                };
                pnlKeyEditorEditors.Controls.Add(lblEmpty);
            }
        }

        private void PopulateKeyEditorList(List<MasterKeyRow> masterRows = null)
        {
            if (lstKeyEditorKeys == null || _session == null) return;

            if (masterRows == null)
            {
                masterRows = _lastFilteredMasterRows ?? GetMasterRows();
            }

            _keyEditorFilteredRows = masterRows;

            bool prevSync = _isSyncingSelection;
            _isSyncingSelection = true;
            try
            {
                lstKeyEditorKeys.BeginUpdate();
                lstKeyEditorKeys.Items.Clear();

                var keyNames = new string[_keyEditorFilteredRows.Count];
                for (int i = 0; i < _keyEditorFilteredRows.Count; i++)
                {
                    keyNames[i] = _keyEditorFilteredRows[i].KeyName;
                }
                lstKeyEditorKeys.Items.AddRange(keyNames);

                lstKeyEditorKeys.EndUpdate();
            }
            finally
            {
                _isSyncingSelection = prevSync;
            }

            AdjustKeyEditorSplitterDistance();

            if (_lastActiveSelectedKeys != null && _lastActiveSelectedKeys.Count > 0)
            {
                SyncSelectionToListBox(lstKeyEditorKeys, _lastActiveSelectedKeys);
                if (tabControlMain?.SelectedTab == tabKeyEditor)
                {
                    OnKeyEditorSelectionChanged();
                }
            }
            else if (_currentlyDisplayedSingleRow != null)
            {
                int idx = _keyEditorFilteredRows.FindIndex(r => string.Equals(r.KeyName, _currentlyDisplayedSingleRow.KeyName, StringComparison.OrdinalIgnoreCase));
                if (idx >= 0 && idx < lstKeyEditorKeys.Items.Count)
                {
                    lstKeyEditorKeys.SelectedIndex = idx;
                }
            }
        }

        private void OnKeyEditorSelectionChanged()
        {
            if (_isSyncingSelection || _isPopulatingInspector || lstKeyEditorKeys == null || _keyEditorFilteredRows == null) return;
            UpdateUIForSessionMode();

            var selectedIndices = lstKeyEditorKeys.SelectedIndices.Cast<int>().ToList();
            var selectedRows = selectedIndices
                .Where(i => i >= 0 && i < _keyEditorFilteredRows.Count)
                .Select(i => _keyEditorFilteredRows[i])
                .ToList();

            if (selectedRows.Count == 0)
            {
                _lastActiveSelectedKeys.Clear();
                ShowPlainKeyEditorEmptyPlaceholder();
                return;
            }

            var keyNames = selectedRows.Select(r => r.KeyName).ToList();
            _lastActiveSelectedKeys = keyNames;
            SaveSessionViewStateToConfig();
            splitMasterDetail.Panel2Collapsed = true;

            var container = pnlKeyEditorEditors;
            _currentlyRenderedMasterKeyNames = keyNames;

            if (selectedRows.Count == 1)
            {
                pnlDetailHeader.Visible = true;
                var row = selectedRows[0];
                _isPopulatingInspector = true;
                txtCurrentKeyName.Text = row.KeyName;
                txtCurrentKeyName.Tag = row.KeyName;
                txtCurrentExtraWav.Text = row.ValuesPerLanguage.Values.FirstOrDefault()?.ExtraValue ?? string.Empty;
                _isPopulatingInspector = false;

                var singleRow = row;
                ScheduleEditorBuild(() => LockWindowUpdate(container, () =>
                {
                    container.Controls.Clear();
                    _langTextEditors.Clear();
                    _langLengthLabels.Clear();
                    _langLinterLabels.Clear();
                    BuildSideBySideEditors(singleRow, container);
                    _currentlyDisplayedSingleRow = singleRow;
                }));
            }
            else
            {
                pnlDetailHeader.Visible = false;
                _currentlyDisplayedSingleRow = null;
                var rowsToBuild = selectedRows;
                ScheduleEditorBuild(() => LockWindowUpdate(container, () =>
                {
                    BuildMultiKeyEditors(rowsToBuild, container);
                }));
            }
        }

        #endregion

        #region Inspector & Dynamic Text Editing

        private void RefreshActiveSelectionInspector()
        {
            _currentlyRenderedMasterKeyNames.Clear();
            if (tabControlMain.SelectedTab == tabKeyEditor)
            {
                OnKeyEditorSelectionChanged();
            }
            else
            {
                var activeGrid = GetActiveGridForTab(tabControlMain?.SelectedTab) ?? gridLabels;
                OnGridSelectionChanged(activeGrid);
            }
        }

        private void gridLabels_SelectionChanged(object sender, EventArgs e)
        {
            OnGridSelectionChanged(gridLabels);
        }

        private Panel CreateMissingKeyActionPanel(
            CsfSessionDocument targetDoc,
            MasterKeyRow row,
            Control parentContainer = null,
            TabControl tabTarget = null,
            TabPage targetPage = null,
            Action<string> onStatusChanged = null)
        {
            var pnl = new Panel
            {
                Dock = DockStyle.Fill,
                Tag = "MissingKeyPanel",
                Padding = new Padding(6)
            };

            var otherDocsWithText = _session.Documents
                .Where(d => d != targetDoc && row.ValuesPerLanguage.TryGetValue(d.LanguageTag, out var entry) && !string.IsNullOrEmpty(entry?.Value))
                .ToList();

            var btnCopyMenu = new Button
            {
                Text = otherDocsWithText.Count > 0 ? $"📋 Add / Copy Key to {targetDoc.LanguageTag}... ▾" : $"➕ Add Blank Key to {targetDoc.LanguageTag}",
                Location = new Point(8, 5),
                Height = 28,
                AutoSize = true
            };

            var menu = new ContextMenuStrip();

            Action<CsfSessionDocument> performAddOrCopy = (srcDoc) =>
            {
                string copyText = string.Empty;
                if (srcDoc != null && row.ValuesPerLanguage.TryGetValue(srcDoc.LanguageTag, out var ent) && ent != null)
                {
                    copyText = ent.Value ?? string.Empty;
                }

                var existingLbl = targetDoc.Document.Labels.FirstOrDefault(l => string.Equals(l.Name, row.KeyName, StringComparison.OrdinalIgnoreCase));
                CsfStringEntry newEntry = null;

                if (existingLbl != null)
                {
                    existingLbl.Strings.Clear();
                    newEntry = new CsfStringEntry(copyText);
                    existingLbl.Strings.Add(newEntry);
                }
                else
                {
                    var newLbl = new CsfLabel(row.KeyName, copyText);
                    targetDoc.Document.Labels.Add(newLbl);
                    newEntry = newLbl.Strings[0];
                }
                targetDoc.IsModified = true;
                MarkKeyAsModified(targetDoc.LanguageTag, row.KeyName);

                if (row.ValuesPerLanguage != null)
                {
                    row.ValuesPerLanguage[targetDoc.LanguageTag] = newEntry;
                }

                UpdateFormTitle();

                string newStatusKey = string.IsNullOrEmpty(copyText) ? "EMPTY" : "MODIFIED";

                // IN-PLACE UI REPLACEMENT FOR MULTI-KEY TAB PAGE OR SINGLE-KEY CONTAINER
                if (targetPage != null)
                {
                    targetPage.Controls.Clear();

                    var txtPinned = new TextBox
                    {
                        Multiline = true,
                        AcceptsReturn = true,
                        AcceptsTab = false,
                        ScrollBars = ScrollBars.Vertical,
                        Dock = DockStyle.Fill,
                        Text = NormalizeToWinFormsLineBreaks(copyText)
                    };

                    string pinnedInitialVal = copyText;
                    txtPinned.GotFocus += (s, e) => { pinnedInitialVal = txtPinned.Text; };
                    txtPinned.LostFocus += (s, e) =>
                    {
                        if (pinnedInitialVal != null && txtPinned.Text != pinnedInitialVal && !_undoManager.IsExecutingUndoRedo)
                        {
                            _undoManager.Execute(new EditValueCommand(targetDoc.LanguageTag, row.KeyName, pinnedInitialVal, txtPinned.Text), _session);
                            UpdateUndoRedoMenuItems();
                            pinnedInitialVal = txtPinned.Text;
                        }
                    };

                    var tagInfo = targetPage.Tag as TabPageTagInfo;
                    if (tagInfo != null)
                    {
                        tagInfo.StatusKey = newStatusKey;
                    }

                    txtPinned.TextChanged += (s, e) =>
                    {
                        if (_isPopulatingInspector || _isSyncingSelection) return;
                        if (newEntry != null && NormalizeToWinFormsLineBreaks(newEntry.Value ?? string.Empty) != txtPinned.Text)
                        {
                            newEntry.Value = txtPinned.Text;
                            targetDoc.IsModified = true;
                            if (tagInfo != null)
                            {
                                tagInfo.StatusKey = string.IsNullOrEmpty(txtPinned.Text) ? "EMPTY" : "MODIFIED";
                            }
                            if (tabTarget != null) tabTarget.Invalidate();
                            MarkKeyAsModified(targetDoc.LanguageTag, row.KeyName);
                            UpdateFormTitle();
                        }
                    };

                    targetPage.Controls.Add(txtPinned);
                    txtPinned.ContextMenuStrip = CreateTargetContextMenu(targetDoc, targetDoc.LanguageTag, row, txtPinned);
                    var pnlPinnedAudio = CreateDocAudioPanel(targetDoc, row, () => UpdateFormTitle());
                    targetPage.Controls.Add(pnlPinnedAudio);

                    _langTextEditors[targetDoc.LanguageTag] = txtPinned;

                    if (tabTarget != null)
                    {
                        tabTarget.Invalidate();
                    }
                }
                else if (parentContainer != null)
                {
                    var pnlMissing = parentContainer.Controls.OfType<Panel>().FirstOrDefault(p => (string)p.Tag == "MissingKeyPanel" || p == pnl);
                    if (pnlMissing != null)
                    {
                        parentContainer.Controls.Remove(pnlMissing);
                    }

                    var txt = new TextBox
                    {
                        Multiline = true,
                        AcceptsReturn = true,
                        AcceptsTab = false,
                        ScrollBars = ScrollBars.Vertical,
                        Dock = DockStyle.Fill,
                        Text = NormalizeToWinFormsLineBreaks(copyText)
                    };

                    string initVal = copyText;
                    txt.GotFocus += (s, e) => { initVal = txt.Text; };
                    txt.LostFocus += (s, e) =>
                    {
                        if (initVal != null && txt.Text != initVal && !_undoManager.IsExecutingUndoRedo)
                        {
                            _undoManager.Execute(new EditValueCommand(targetDoc.LanguageTag, row.KeyName, initVal, txt.Text), _session);
                            UpdateUndoRedoMenuItems();
                            initVal = txt.Text;
                        }
                    };

                    txt.TextChanged += (s, e) =>
                    {
                        if (_isPopulatingInspector || _isSyncingSelection) return;
                        if (newEntry != null && NormalizeToWinFormsLineBreaks(newEntry.Value ?? string.Empty) != txt.Text)
                        {
                            newEntry.Value = txt.Text;
                            targetDoc.IsModified = true;
                            onStatusChanged?.Invoke(string.IsNullOrEmpty(txt.Text) ? "EMPTY" : "MODIFIED");
                            MarkKeyAsModified(targetDoc.LanguageTag, row.KeyName);
                            UpdateFormTitle();
                        }
                    };

                    txt.ContextMenuStrip = CreateTargetContextMenu(targetDoc, targetDoc.LanguageTag, row, txt);
                    parentContainer.Controls.Add(txt);
                    txt.BringToFront();

                    var pnlAudio = CreateDocAudioPanel(targetDoc, row, () => UpdateFormTitle());
                    pnlAudio.Dock = DockStyle.Bottom;
                    parentContainer.Controls.Add(pnlAudio);

                    _langTextEditors[targetDoc.LanguageTag] = txt;

                    onStatusChanged?.Invoke(newStatusKey);
                }

                // Refresh auxiliary grids without tearing down controls
                if (tabControlMain.SelectedTab == tabCoverage)
                {
                    PopulateCoverageMatrixTab();
                    SyncSelectionToGrid(gridCoverage, _lastActiveSelectedKeys, preserveScrollPosition: true);
                }
                else if (tabControlMain.SelectedTab == tabUnsaved)
                {
                    PopulateUnsavedChangesTab(GetMasterRows());
                    SyncSelectionToGrid(gridUnsaved, _lastActiveSelectedKeys, preserveScrollPosition: true);
                }
                else if (tabControlMain.SelectedTab == tabRecent)
                {
                    PopulateRecentGrid();
                    SyncSelectionToGrid(gridRecent, _lastActiveSelectedKeys, preserveScrollPosition: true);
                }
                else if (tabControlMain.SelectedTab == tabMaster)
                {
                    gridLabels.Invalidate();
                }
            };

            if (otherDocsWithText.Count > 0)
            {
                var lblHeader = new ToolStripMenuItem("Copy key text from open file:") { Enabled = false };
                menu.Items.Add(lblHeader);
                menu.Items.Add(new ToolStripSeparator());

                foreach (var srcDoc in otherDocsWithText)
                {
                    string val = row.ValuesPerLanguage[srcDoc.LanguageTag].Value;
                    string preview = val.Length > 40 ? val.Substring(0, 40) + "..." : val;
                    preview = preview.Replace("\r", "").Replace("\n", " ");

                    string langLabel = $"{srcDoc.LanguageTag}{(srcDoc == _session.BaseDocument ? " 📌 Base CSF" : "")}";
                    var itemCopy = new ToolStripMenuItem($"📋 Copy from {langLabel} (\"{preview}\")");

                    var docForAction = srcDoc;
                    itemCopy.Click += (s, e) => performAddOrCopy(docForAction);
                    menu.Items.Add(itemCopy);
                }

                menu.Items.Add(new ToolStripSeparator());
            }

            var itemAddBlank = new ToolStripMenuItem($"➕ Add Blank Key (Empty string)");
            itemAddBlank.Click += (s, e) => performAddOrCopy(null);
            menu.Items.Add(itemAddBlank);

            btnCopyMenu.Click += (s, e) =>
            {
                if (otherDocsWithText.Count > 0)
                {
                    menu.Show(btnCopyMenu, new Point(0, btnCopyMenu.Height));
                }
                else
                {
                    performAddOrCopy(null);
                }
            };

            pnl.Controls.Add(btnCopyMenu);
            return pnl;
        }

        private Panel CreateDocAudioPanel(CsfSessionDocument sDoc, MasterKeyRow row, Action onWavChanged)
        {
            var pnl = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 26,
                Padding = new Padding(2, 2, 2, 2)
            };

            var lblSound = new Label
            {
                Text = "🎵",
                AutoSize = true,
                Dock = DockStyle.Left,
                Padding = new Padding(2, 4, 2, 0)
            };

            string currentExtra = string.Empty;
            if (row != null && row.ValuesPerLanguage.TryGetValue(sDoc.LanguageTag, out var entry))
            {
                currentExtra = entry?.ExtraValue ?? string.Empty;
            }

            var txtAudio = new TextBox
            {
                Text = currentExtra,
                Dock = DockStyle.Fill,
                Font = new Font(FontFamily.GenericSansSerif, 8.25f, FontStyle.Regular)
            };
            _toolTip.SetToolTip(txtAudio, $"Extra sound WAV filename for [{sDoc.LanguageTag}]");

            txtAudio.TextChanged += (s, e) =>
            {
                if (row == null || _isPopulatingInspector) return;
                string newAudio = txtAudio.Text.Trim();
                if (string.IsNullOrEmpty(newAudio)) newAudio = null;

                if (sDoc.Document != null)
                {
                    var lbl = sDoc.Document.Labels.FirstOrDefault(l => string.Equals(l.Name, row.KeyName, StringComparison.OrdinalIgnoreCase));
                    if (lbl != null)
                    {
                        if (lbl.Strings.Count > 0)
                        {
                            lbl.Strings[0].ExtraValue = newAudio;
                        }
                        else
                        {
                            lbl.Strings.Add(new CsfStringEntry(string.Empty, newAudio));
                        }
                        sDoc.IsModified = true;
                        if (row.ValuesPerLanguage.TryGetValue(sDoc.LanguageTag, out var ent))
                        {
                            ent.ExtraValue = newAudio;
                        }
                        UpdateFormTitle();
                        onWavChanged?.Invoke();
                    }
                }
            };

            pnl.Controls.Add(txtAudio);
            pnl.Controls.Add(lblSound);

            return pnl;
        }

        private void PlayAudioWav(string wavFileName)
        {
            if (string.IsNullOrWhiteSpace(wavFileName))
            {
                MessageBox.Show("No audio WAV filename specified for this entry.", "Audio Playback", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            try
            {
                string path = wavFileName;
                if (!File.Exists(path) && _session.BaseDocument != null && !string.IsNullOrEmpty(_session.BaseDocument.FilePath))
                {
                    string dir = Path.GetDirectoryName(_session.BaseDocument.FilePath);
                    string candidate = Path.Combine(dir, wavFileName);
                    if (File.Exists(candidate)) path = candidate;
                }

                if (File.Exists(path))
                {
                    using (var player = new System.Media.SoundPlayer(path))
                    {
                        player.Play();
                    }
                }
                else
                {
                    System.Media.SystemSounds.Asterisk.Play();
                    MessageBox.Show($"Audio file '{wavFileName}' was not found on disk at:\n{path}", "File Not Found", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error playing audio file '{wavFileName}':\n{ex.Message}", "Playback Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BuildSideBySideEditors(MasterKeyRow row, Control targetContainer = null)
        {
            if (targetContainer == null)
            {
                targetContainer = pnlLanguageEditors;
            }

            try
            {
                targetContainer.SuspendLayout();
                targetContainer.Controls.Clear();
                _langTextEditors.Clear();
                _langLengthLabels.Clear();
                _langLinterLabels.Clear();

                if (_session.Documents.Count == 0) return;

                var baseDoc = _session.BaseDocument ?? _session.Documents.FirstOrDefault();
                string baseLangTag = baseDoc.LanguageTag;
                bool baseExists = row.ValuesPerLanguage.TryGetValue(baseLangTag, out var baseEntry);
                string baseText = baseExists ? baseEntry.Value : string.Empty;

                var targetDocs = _session.Documents.Where(d => d != baseDoc).ToList();

                // SINGLE-FILE MODE OR NO TARGET DOCUMENTS
                if (targetDocs.Count == 0)
                {
                    var grpSingle = new GroupBox
                    {
                        Text = $"📌 {baseLangTag}",
                        Dock = DockStyle.Fill
                    };

                    string bToolTip = ToolTipHelper.WrapText($"Label: {baseLangTag}\nFile: {(string.IsNullOrEmpty(baseDoc.FilePath) ? "Unsaved In-Memory Document" : Path.GetFileName(baseDoc.FilePath))}\nPath: {(string.IsNullOrEmpty(baseDoc.FilePath) ? "-" : baseDoc.FilePath)}\nLength: {baseText.Length} chars", 45);

                    var txtSingle = new TextBox
                    {
                        Multiline = true,
                        AcceptsReturn = true,
                        AcceptsTab = false,
                        ScrollBars = ScrollBars.Vertical,
                        Dock = DockStyle.Fill,
                        Text = NormalizeToWinFormsLineBreaks(baseText)
                    };

                    _toolTip.SetToolTip(txtSingle, bToolTip);

                    string singleInitialVal = baseText;
                    txtSingle.GotFocus += (s, e) => { singleInitialVal = txtSingle.Text; };
                    txtSingle.LostFocus += (s, e) =>
                    {
                        if (singleInitialVal != null && txtSingle.Text != singleInitialVal && !_undoManager.IsExecutingUndoRedo)
                        {
                            _undoManager.Execute(new EditValueCommand(baseLangTag, row.KeyName, singleInitialVal, txtSingle.Text), _session);
                            UpdateUndoRedoMenuItems();
                            singleInitialVal = txtSingle.Text;
                        }
                    };

                    txtSingle.TextChanged += (s, e) =>
                    {
                        if (_isPopulatingInspector || _isSyncingSelection) return;
                        if (baseExists && baseEntry != null && NormalizeToWinFormsLineBreaks(baseEntry.Value ?? string.Empty) != txtSingle.Text)
                        {
                            baseEntry.Value = txtSingle.Text;
                            baseDoc.IsModified = true;
                            AddRecentEditedKey(row.KeyName);
                            UpdateGridRowAfterValueChange(row.KeyName, baseDoc);
                            UpdateFormTitle();
                        }
                    };

                    grpSingle.Controls.Add(txtSingle);
                    var pnlSingleAudio = CreateDocAudioPanel(baseDoc, row, () => UpdateFormTitle());
                    grpSingle.Controls.Add(pnlSingleAudio);



                    grpSingle.MouseDown += (s, e) =>
                    {
                        if (e.Button == MouseButtons.Right)
                        {
                            var ctx = CreateBaseContextMenu(baseDoc, row);
                            ctx.Show(grpSingle, e.Location);
                        }
                    };

                    targetContainer.Controls.Add(grpSingle);
                    _langTextEditors[baseLangTag] = txtSingle;
                    return;
                }

                // MULTI-CSF SESSION MODE: Equal-Width Multi-Column Split View using TableLayoutPanel
                var unpinnedDocs = targetDocs.Where(d => _unpinnedTargetLanguageTags.Contains(d.LanguageTag)).ToList();
                var pinnedDocs = targetDocs.Where(d => !_unpinnedTargetLanguageTags.Contains(d.LanguageTag)).ToList();

                int totalColumns = 1 + unpinnedDocs.Count + (pinnedDocs.Count > 0 ? 1 : 0);

                int singleExtraTabHeaderHeight = 0;
                if (_appConfig != null && _appConfig.InspectorMultilineTabs && pinnedDocs.Count > 0)
                {
                    using (var font = new Font(FontFamily.GenericSansSerif, 8.5f, FontStyle.Regular))
                    {
                        int totalTabWidths = pinnedDocs.Sum(d => TextRenderer.MeasureText(d.LanguageTag, font).Width + 48);
                        int approxColW = targetContainer.Width / Math.Max(1, totalColumns);
                        int availableW = Math.Max(50, approxColW - 10);
                        if (totalTabWidths > availableW)
                        {
                            int rows = (int)Math.Ceiling((double)totalTabWidths / availableW);
                            rows = Math.Max(1, Math.Min(5, rows));
                            singleExtraTabHeaderHeight = (rows - 1) * 24;
                        }
                    }
                }

                var tblLayout = new TableLayoutPanel
                {
                    Dock = DockStyle.Fill,
                    RowCount = 1,
                    ColumnCount = totalColumns
                };
                tblLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
                for (int col = 0; col < totalColumns; col++)
                {
                    tblLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / totalColumns));
                }

                int currentColIndex = 0;

                // --- COLUMN 0: Base Reference ⭐ (100% Editable with Pastel Status Header) ---
                string baseStatusKey = !baseExists ? "MISSING" : (string.IsNullOrEmpty(baseText) ? "EMPTY" : (IsKeyModifiedInDoc(baseLangTag, row.KeyName) ? "MODIFIED" : "COMPLETE"));
                string currentBaseStatusKey = baseStatusKey;

                var pnlBaseContainer = new Panel
                {
                    Dock = DockStyle.Fill,
                    Margin = new Padding(2)
                };

                var pnlBaseHeader = new Panel
                {
                    Dock = DockStyle.Top,
                    Height = 24
                };

                pnlBaseHeader.Resize += (s, e) => pnlBaseHeader.Invalidate();
                pnlBaseHeader.Paint += (s, e) =>
                {
                    var g = e.Graphics;
                    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

                    var (bgNorm, bgSel, textColor) = GetStatusTabColors(currentBaseStatusKey);

                    var rect = pnlBaseHeader.ClientRectangle;
                    if (rect.Width <= 0 || rect.Height <= 0) return;

                    using (var bgBrush = new SolidBrush(bgNorm))
                    {
                        g.FillRectangle(bgBrush, rect);
                    }

                    Color borderColor = Color.FromArgb(Math.Max(0, bgNorm.R - 35), Math.Max(0, bgNorm.G - 35), Math.Max(0, bgNorm.B - 35));
                    using (var penBorder = new Pen(borderColor))
                    {
                        g.DrawRectangle(penBorder, 0, 0, rect.Width - 1, rect.Height - 1);
                    }

                    string titleText = baseLangTag;
                    using (var emojiFont = new Font("Segoe UI Emoji", 8.5f, FontStyle.Regular))
                    using (var font = new Font("Segoe UI", 8.5f, FontStyle.Bold))
                    using (var textBrush = new SolidBrush(textColor))
                    {
                        g.DrawString("📌", emojiFont, textBrush, 8, 4);
                        g.DrawString(titleText, font, textBrush, 26, 4);
                    }
                };

                pnlBaseHeader.MouseDown += (s, e) =>
                {
                    if (e.Button == MouseButtons.Right)
                    {
                        var ctx = CreateBaseContextMenu(baseDoc, row);
                        ctx.Show(pnlBaseHeader, e.Location);
                    }
                };

                pnlBaseContainer.Controls.Add(pnlBaseHeader);

                string baseToolTipText = ToolTipHelper.WrapText($"Label: {baseLangTag}\nFile: {(string.IsNullOrEmpty(baseDoc.FilePath) ? "Unsaved In-Memory Document" : Path.GetFileName(baseDoc.FilePath))}\nPath: {(string.IsNullOrEmpty(baseDoc.FilePath) ? "-" : baseDoc.FilePath)}\nLength: {baseText.Length} chars", 45);
                _toolTip.SetToolTip(pnlBaseHeader, baseToolTipText);

                if (!baseExists)
                {
                    var pnlBaseMissing = CreateMissingKeyActionPanel(baseDoc, row, pnlBaseContainer, onStatusChanged: (sk) => { currentBaseStatusKey = sk; pnlBaseHeader.Invalidate(); });
                    pnlBaseMissing.Dock = DockStyle.Fill;
                    pnlBaseContainer.Controls.Add(pnlBaseMissing);
                    pnlBaseMissing.BringToFront();
                }
                else
                {
                    var txtBase = new TextBox
                    {
                        Multiline = true,
                        AcceptsReturn = true,
                        AcceptsTab = false,
                        ScrollBars = ScrollBars.Vertical,
                        Dock = DockStyle.Fill,
                        Text = NormalizeToWinFormsLineBreaks(baseText)
                    };

                    _toolTip.SetToolTip(txtBase, baseToolTipText);
                    txtBase.ContextMenuStrip = CreateBaseContextMenu(baseDoc, row, txtBase);
                    pnlBaseContainer.Controls.Add(txtBase);
                    txtBase.BringToFront();

                    var pnlBaseAudio = CreateDocAudioPanel(baseDoc, row, () => UpdateFormTitle());
                    pnlBaseAudio.Dock = DockStyle.Bottom;
                    pnlBaseContainer.Controls.Add(pnlBaseAudio);

                    _langTextEditors[baseLangTag] = txtBase;

                    string baseInitialVal = baseText;
                    txtBase.GotFocus += (s, e) => { baseInitialVal = txtBase.Text; };
                    txtBase.LostFocus += (s, e) =>
                    {
                        if (baseInitialVal != null && txtBase.Text != baseInitialVal && !_undoManager.IsExecutingUndoRedo)
                        {
                            _undoManager.Execute(new EditValueCommand(baseLangTag, row.KeyName, baseInitialVal, txtBase.Text), _session);
                            UpdateUndoRedoMenuItems();
                            baseInitialVal = txtBase.Text;
                        }
                    };

                    txtBase.TextChanged += (s, e) =>
                    {
                        if (_isPopulatingInspector || _isSyncingSelection) return;
                        if (baseExists && baseEntry != null && NormalizeToWinFormsLineBreaks(baseEntry.Value ?? string.Empty) != txtBase.Text)
                        {
                            baseEntry.Value = txtBase.Text;
                            baseDoc.IsModified = true;
                            currentBaseStatusKey = string.IsNullOrEmpty(txtBase.Text) ? "EMPTY" : "MODIFIED";
                            pnlBaseHeader.Invalidate();
                            MarkKeyAsModified(baseLangTag, row.KeyName);
                            UpdateGridRowAfterValueChange(row.KeyName, baseDoc);
                            UpdateFormTitle();
                        }
                    };
                }

                tblLayout.Controls.Add(pnlBaseContainer, currentColIndex++, 0);

                // --- COLUMNS 1..K: Unpinned Target Language Columns ---
                foreach (var sDoc in unpinnedDocs)
                {
                    string targetTag = sDoc.LanguageTag;
                    bool exists = row.ValuesPerLanguage.TryGetValue(targetTag, out var entry);
                    string targetText = exists ? entry.Value : string.Empty;

                    string statusKey = !exists ? "MISSING" : (string.IsNullOrEmpty(targetText) ? "EMPTY" : (IsKeyModifiedInDoc(targetTag, row.KeyName) ? "MODIFIED" : "COMPLETE"));

                    var pnlUnpinnedContainer = new Panel
                    {
                        Dock = DockStyle.Fill,
                        Margin = new Padding(2)
                    };

                    var pnlHeader = new Panel
                    {
                        Dock = DockStyle.Top,
                        Height = 24
                    };

                    string currentStatusKey = statusKey;

                    pnlHeader.Resize += (s, e) => pnlHeader.Invalidate();
                    pnlHeader.DoubleClick += (s, e) =>
                    {
                        _unpinnedTargetLanguageTags.Remove(targetTag);
                        RefreshActiveSelectionInspector();
                    };

                    pnlHeader.Paint += (s, e) =>
                    {
                        var g = e.Graphics;
                        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

                        var (bgNorm, bgSel, textColor) = GetStatusTabColors(currentStatusKey);

                        var rect = pnlHeader.ClientRectangle;
                        if (rect.Width <= 0 || rect.Height <= 0) return;

                        using (var bgBrush = new SolidBrush(bgNorm))
                        {
                            g.FillRectangle(bgBrush, rect);
                        }

                        Color borderColor = Color.FromArgb(Math.Max(0, bgNorm.R - 35), Math.Max(0, bgNorm.G - 35), Math.Max(0, bgNorm.B - 35));
                        using (var penBorder = new Pen(borderColor))
                        {
                            g.DrawRectangle(penBorder, 0, 0, rect.Width - 1, rect.Height - 1);
                        }

                        string titleText = targetTag;
                        using (var font = new Font(FontFamily.GenericSansSerif, 8.5f, FontStyle.Bold))
                        using (var textBrush = new SolidBrush(textColor))
                        {
                            g.DrawString(titleText, font, textBrush, 8, 4);
                        }
                    };

                    pnlHeader.MouseDown += (s, e) =>
                    {
                        if (e.Button == MouseButtons.Right)
                        {
                            var ctx = CreateTargetContextMenu(sDoc, targetTag, row);
                            ctx.Show(pnlHeader, e.Location);
                        }
                    };

                    pnlUnpinnedContainer.Controls.Add(pnlHeader);

                    string tFilePath = string.IsNullOrEmpty(sDoc.FilePath) ? "Unsaved In-Memory Document" : sDoc.FilePath;
                    string linterDetail = CheckLinterStatusString(baseText, targetText);
                    string colToolTipText = ToolTipHelper.WrapText($"Label: {targetTag}\nFile: {Path.GetFileName(tFilePath)}\nPath: {tFilePath}\nLength: {targetText.Length} chars\nLinter: {linterDetail}\n(Double-click header to dock back to tab group)", 45);
                    _toolTip.SetToolTip(pnlHeader, colToolTipText);

                    if (!exists)
                    {
                        var pnlMissing = CreateMissingKeyActionPanel(sDoc, row, pnlUnpinnedContainer, onStatusChanged: (sk) => { currentStatusKey = sk; pnlHeader.Invalidate(); });
                        pnlMissing.Dock = DockStyle.Fill;
                        pnlUnpinnedContainer.Controls.Add(pnlMissing);
                        pnlMissing.BringToFront();
                    }
                    else
                    {
                        var txtUnpinned = new TextBox
                        {
                            Multiline = true,
                            AcceptsReturn = true,
                            AcceptsTab = false,
                            ScrollBars = ScrollBars.Vertical,
                            Dock = DockStyle.Fill,
                            Text = NormalizeToWinFormsLineBreaks(targetText),
                            Enabled = true
                        };

                        _toolTip.SetToolTip(txtUnpinned, colToolTipText);

                        string unpinnedInitialVal = targetText;
                        txtUnpinned.GotFocus += (s, e) => { unpinnedInitialVal = txtUnpinned.Text; };
                        txtUnpinned.LostFocus += (s, e) =>
                        {
                            if (unpinnedInitialVal != null && txtUnpinned.Text != unpinnedInitialVal && !_undoManager.IsExecutingUndoRedo)
                            {
                                _undoManager.Execute(new EditValueCommand(targetTag, row.KeyName, unpinnedInitialVal, txtUnpinned.Text), _session);
                                UpdateUndoRedoMenuItems();
                                unpinnedInitialVal = txtUnpinned.Text;
                            }
                        };

                        txtUnpinned.TextChanged += (s, e) =>
                        {
                            if (_isPopulatingInspector || _isSyncingSelection) return;
                            if (exists && entry != null && NormalizeToWinFormsLineBreaks(entry.Value ?? string.Empty) != txtUnpinned.Text)
                            {
                                entry.Value = txtUnpinned.Text;
                                sDoc.IsModified = true;
                                currentStatusKey = string.IsNullOrEmpty(txtUnpinned.Text) ? "EMPTY" : "MODIFIED";
                                pnlHeader.Invalidate();
                                MarkKeyAsModified(targetTag, row.KeyName);
                                UpdateGridRowAfterValueChange(row.KeyName, sDoc);
                                UpdateFormTitle();
                            }
                        };

                        txtUnpinned.ContextMenuStrip = CreateTargetContextMenu(sDoc, targetTag, row, txtUnpinned);
                        pnlUnpinnedContainer.Controls.Add(txtUnpinned);
                        txtUnpinned.BringToFront();

                        var pnlUnpinnedAudio = CreateDocAudioPanel(sDoc, row, () => UpdateFormTitle());
                        pnlUnpinnedAudio.Dock = DockStyle.Bottom;
                        pnlUnpinnedContainer.Controls.Add(pnlUnpinnedAudio);

                        _langTextEditors[targetTag] = txtUnpinned;
                    }

                    tblLayout.Controls.Add(pnlUnpinnedContainer, currentColIndex++, 0);
                }

                // --- LAST COLUMN: Target Languages TabControl for Pinned Targets ---
                if (pinnedDocs.Count > 0)
                {
                    var tabTarget = new TabControl
                    {
                        Dock     = DockStyle.Fill,
                        Font     = new Font(FontFamily.GenericSansSerif, 8.5f, FontStyle.Regular),
                        DrawMode = TabDrawMode.OwnerDrawFixed,
                        SizeMode = TabSizeMode.Normal,
                        Multiline = _appConfig != null ? _appConfig.InspectorMultilineTabs : false,
                        Padding  = new Point(24, 4),
                        ShowToolTips = false
                    };

                    tabTarget.DrawItem += (s, e) =>
                    {
                        if (e.Index < 0 || e.Index >= tabTarget.TabPages.Count) return;
                        var page = tabTarget.TabPages[e.Index];
                        bool isSelected = tabTarget.SelectedIndex == e.Index;

                        var info2 = page.Tag as TabPageTagInfo;
                        string sk = info2?.StatusKey ?? "COMPLETE";
                        var (bgNorm, bgSel, tc) = GetStatusTabColors(sk);
                        Color bg = isSelected ? bgSel : bgNorm;

                        using (var bgBrush = new SolidBrush(bg))
                            e.Graphics.FillRectangle(bgBrush, e.Bounds);

                        int sphereSize = 9;
                        int sphereX = e.Bounds.Left + 6;
                        int sphereY = e.Bounds.Top + (e.Bounds.Height - sphereSize) / 2;
                        DrawStatusSphereAt(e.Graphics, sphereX, sphereY, sphereSize, sk);

                        string label = info2?.LanguageTag ?? page.Text;
                        var drawFont = isSelected ? new Font(e.Font, FontStyle.Bold) : e.Font;
                        int textX = sphereX + sphereSize + 5;
                        var textRect = new Rectangle(textX, e.Bounds.Top, Math.Max(10, e.Bounds.Width - (textX - e.Bounds.Left) - 4), e.Bounds.Height);
                        using (var tb = new SolidBrush(tc))
                        {
                            using (var tf = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center, FormatFlags = StringFormatFlags.NoWrap, Trimming = StringTrimming.EllipsisCharacter })
                            {
                                e.Graphics.DrawString(label, drawFont, tb, textRect, tf);
                            }
                        }
                    };
                    TabPage activeTabPageToSelect = null;

                    foreach (var sDoc in pinnedDocs)
                    {
                        string targetTag = sDoc.LanguageTag;
                        bool exists = row.ValuesPerLanguage.TryGetValue(targetTag, out var entry);
                        string targetText = exists ? entry.Value : string.Empty;

                        string statusKey = "COMPLETE";
                        string statusTip = "🟢 Complete Key: Valid text present in file";

                        if (!exists)
                        {
                            statusKey = "MISSING";
                            statusTip = $"🔴 Missing Key: Label '{row.KeyName}' is missing in [{targetTag}]";
                        }
                        else if (string.IsNullOrEmpty(targetText))
                        {
                            statusKey = "EMPTY";
                            statusTip = $"🟡 Empty Text: Key exists in [{targetTag}], but text is blank";
                        }
                        else if (IsKeyModifiedInDoc(targetTag, row.KeyName))
                        {
                            statusKey = "MODIFIED";
                            statusTip = $"🔵 Unsaved Changes in [{targetTag}]";
                        }

                        string tFilePath = string.IsNullOrEmpty(sDoc.FilePath) ? "Unsaved In-Memory Document" : sDoc.FilePath;
                        string linterDetail = CheckLinterStatusString(baseText, targetText);
                        string tabToolTipText = $"{statusTip}\nLabel: {targetTag}\nFile: {Path.GetFileName(tFilePath)}\nPath: {tFilePath}\nLength: {targetText.Length} chars\nLinter: {linterDetail}";

                        var tagInfo = new TabPageTagInfo { LanguageTag = targetTag, StatusKey = statusKey };

                        var tabPg = new TabPage
                        {
                            Text        = targetTag,
                            ToolTipText = tabToolTipText,
                            Tag         = tagInfo
                        };

                        if (!exists)
                        {
                            var pnlTabMissing = CreateMissingKeyActionPanel(sDoc, row, tabTarget: tabTarget, targetPage: tabPg);
                            tabPg.Controls.Add(pnlTabMissing);
                        }
                        else
                        {
                            var txtTarget = new TextBox
                            {
                                Multiline = true,
                                AcceptsReturn = true,
                                AcceptsTab = false,
                                ScrollBars = ScrollBars.Vertical,
                                Dock = DockStyle.Fill,
                                Text = NormalizeToWinFormsLineBreaks(targetText),
                                Enabled = true
                            };

                            txtTarget.TextChanged += (s, e) =>
                            {
                                if (_isPopulatingInspector || _isSyncingSelection) return;
                                if (exists && entry != null && NormalizeToWinFormsLineBreaks(entry.Value ?? string.Empty) != txtTarget.Text)
                                {
                                    entry.Value = txtTarget.Text;
                                    sDoc.IsModified = true;
                                    tagInfo.StatusKey = string.IsNullOrEmpty(txtTarget.Text) ? "EMPTY" : "MODIFIED";
                                    string newStatusTip = string.IsNullOrEmpty(txtTarget.Text) ? "🟡 Empty Text: Key text is blank" : "🔵 Unsaved Changes in file";
                                    string newLinter = CheckLinterStatusString(baseText, txtTarget.Text);
                                    tabPg.ToolTipText = $"{newStatusTip}\nLabel: {targetTag}\nFile: {Path.GetFileName(tFilePath)}\nPath: {tFilePath}\nLength: {txtTarget.Text.Length} chars\nLinter: {newLinter}";
                                    tabTarget.Invalidate(); // repaint tab with new pastel color
                                    MarkKeyAsModified(targetTag, row.KeyName);
                                    UpdateGridRowAfterValueChange(row.KeyName, sDoc);
                                    UpdateFormTitle();
                                }
                            };

                            tabPg.Controls.Add(txtTarget);
                            var pnlTargetAudio = CreateDocAudioPanel(sDoc, row, () => UpdateFormTitle());
                            tabPg.Controls.Add(pnlTargetAudio);

                            _langTextEditors[targetTag] = txtTarget;
                        }
                        tabTarget.TabPages.Add(tabPg);

                        if (string.Equals(_lastSelectedTargetLanguageTag, targetTag, StringComparison.OrdinalIgnoreCase))
                        {
                            activeTabPageToSelect = tabPg;
                        }
                    }

                    if (activeTabPageToSelect != null)
                    {
                        tabTarget.SelectedTab = activeTabPageToSelect;
                    }

                    tabTarget.SelectedIndexChanged += (s, e) =>
                    {
                        if (tabTarget.SelectedTab != null)
                        {
                            var info = tabTarget.SelectedTab.Tag as TabPageTagInfo;
                            _lastSelectedTargetLanguageTag = info != null ? info.LanguageTag : (tabTarget.SelectedTab.Tag as string);
                        }
                    };

                    // Mouse Drag-to-Unpin and Right-click context menu on TabControl tabs
                    TabPage tabDragStartPage = null;
                    Point tabDragStartPoint = Point.Empty;

                    tabTarget.MouseDown += (s, e) =>
                    {
                        if (e.Button == MouseButtons.Left)
                        {
                            for (int i = 0; i < tabTarget.TabPages.Count; i++)
                            {
                                if (tabTarget.GetTabRect(i).Contains(e.Location))
                                {
                                    tabDragStartPage = tabTarget.TabPages[i];
                                    tabDragStartPoint = e.Location;
                                    break;
                                }
                            }
                        }
                        else if (e.Button == MouseButtons.Right)
                        {
                            for (int i = 0; i < tabTarget.TabPages.Count; i++)
                            {
                                if (tabTarget.GetTabRect(i).Contains(e.Location))
                                {
                                    var targetPage = tabTarget.TabPages[i];
                                    string langTag = (targetPage.Tag as TabPageTagInfo)?.LanguageTag ?? (targetPage.Tag as string);
                                    var targetDoc = _session.Documents.FirstOrDefault(d => string.Equals(d.LanguageTag, langTag, StringComparison.OrdinalIgnoreCase));
                                    var ctxMenu = CreateTargetContextMenu(targetDoc, langTag, row);
                                    ctxMenu.Show(tabTarget, e.Location);
                                    break;
                                }
                            }
                        }
                    };

                    tabTarget.MouseMove += (s, e) =>
                    {
                        if (e.Button == MouseButtons.Left && tabDragStartPage != null)
                        {
                            int dx = Math.Abs(e.X - tabDragStartPoint.X);
                            int dy = Math.Abs(e.Y - tabDragStartPoint.Y);
                            bool dragThresholdPassed = dx > SystemInformation.DragSize.Width || dy > SystemInformation.DragSize.Height;
                            bool draggedOutsideHeader = e.Y > 28 || e.Y < -5 || e.X < -5 || e.X > tabTarget.Width + 5;

                            if (dragThresholdPassed && draggedOutsideHeader)
                            {
                                string langTag = (tabDragStartPage.Tag as TabPageTagInfo)?.LanguageTag ?? (tabDragStartPage.Tag as string);
                                tabDragStartPage = null;
                                if (!string.IsNullOrEmpty(langTag))
                                {
                                    _unpinnedTargetLanguageTags.Add(langTag);
                                    RefreshActiveSelectionInspector();
                                }
                            }
                        }
                    };

                    tabTarget.MouseUp += (s, e) =>
                    {
                        tabDragStartPage = null;
                    };

                    SetupTabControlToolTips(tabTarget);
                    tblLayout.Controls.Add(tabTarget, currentColIndex++, 0);
                }

                targetContainer.Controls.Add(tblLayout);
            }
            finally
            {
                targetContainer.ResumeLayout(true);
            }
        }

        private void SetupTabControlToolTips(TabControl tabControl)
        {
            if (tabControl == null) return;
            tabControl.ShowToolTips = false; // Disable native tooltip to avoid double tooltips

            int lastTabHoverIndex = -1;
            tabControl.MouseMove += (s, e) =>
            {
                for (int i = 0; i < tabControl.TabCount; i++)
                {
                    if (tabControl.GetTabRect(i).Contains(e.Location))
                    {
                        if (lastTabHoverIndex == i) return;
                        lastTabHoverIndex = i;

                        TabPage page = tabControl.TabPages[i];
                        string tipText = page.ToolTipText;

                        if (!string.IsNullOrEmpty(tipText))
                        {
                            string wrapped = WrapToolTipText(tipText, 50, 12);
                            _toolTip.SetToolTip(tabControl, wrapped);
                            return;
                        }
                    }
                }

                if (lastTabHoverIndex != -1)
                {
                    lastTabHoverIndex = -1;
                    _toolTip.SetToolTip(tabControl, string.Empty);
                }
            };

            tabControl.MouseLeave += (s, e) =>
            {
                lastTabHoverIndex = -1;
                _toolTip.SetToolTip(tabControl, string.Empty);
            };
        }

        private void CopyValueToAllOtherDocs(string keyName, string srcLangTag, string srcText, string srcAudio, bool copyText, bool copyAudio)
        {
            foreach (var doc in _session.Documents.Where(d => !string.Equals(d.LanguageTag, srcLangTag, StringComparison.OrdinalIgnoreCase)))
            {
                var lbl = doc.Document.Labels.FirstOrDefault(l => string.Equals(l.Name, keyName, StringComparison.OrdinalIgnoreCase));
                if (lbl == null)
                {
                    lbl = new CsfLabel(keyName);
                    doc.Document.Labels.Add(lbl);
                }

                if (lbl.Strings.Count == 0)
                {
                    lbl.Strings.Add(new CsfStringEntry(copyText ? (srcText ?? string.Empty) : string.Empty, copyAudio ? srcAudio : null));
                }
                else
                {
                    if (copyText) lbl.Strings[0].Value = srcText ?? string.Empty;
                    if (copyAudio) lbl.Strings[0].ExtraValue = srcAudio;
                }
                doc.IsModified = true;
            }

            AddRecentEditedKey(keyName);
            RebuildCategoryTreeAndGrid();
        }

        private void MassSyncAudioFromDoc(CsfSessionDocument srcDoc)
        {
            if (srcDoc == null || srcDoc.Document == null) return;

            string srcLangTag = srcDoc.LanguageTag;
            int count = 0;
            var srcLabels = srcDoc.Document.Labels;

            foreach (var srcLbl in srcLabels)
            {
                string srcAudio = srcLbl.FirstExtraValue;
                if (string.IsNullOrEmpty(srcAudio)) continue;

                foreach (var targetDoc in _session.Documents.Where(d => d != srcDoc))
                {
                    var targetLbl = targetDoc.Document.Labels.FirstOrDefault(l => string.Equals(l.Name, srcLbl.Name, StringComparison.OrdinalIgnoreCase));
                    if (targetLbl != null)
                    {
                        if (targetLbl.Strings.Count > 0)
                        {
                            if (targetLbl.Strings[0].ExtraValue != srcAudio)
                            {
                                targetLbl.Strings[0].ExtraValue = srcAudio;
                                targetDoc.IsModified = true;
                                count++;
                            }
                        }
                        else
                        {
                            targetLbl.Strings.Add(new CsfStringEntry(string.Empty, srcAudio));
                            targetDoc.IsModified = true;
                            count++;
                        }
                    }
                    else
                    {
                        var newLbl = new CsfLabel(srcLbl.Name, string.Empty, srcAudio);
                        targetDoc.Document.Labels.Add(newLbl);
                        targetDoc.IsModified = true;
                        count++;
                    }
                }
            }

            RebuildCategoryTreeAndGrid();
            MessageBox.Show($"Massively synchronized extra audio WAV filenames for {count} entries from [{srcLangTag}] to all other open CSF files.", "Audio Synchronization Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void PromptRenameFileLabel(CsfSessionDocument sDoc)
        {
            if (sDoc == null) return;
            string oldLabel = sDoc.LanguageTag ?? string.Empty;

            using (var dlg = new Form())
            {
                dlg.Text = "Edit File Label / Tag";
                dlg.Size = new Size(380, 160);
                dlg.StartPosition = FormStartPosition.CenterParent;
                dlg.FormBorderStyle = FormBorderStyle.FixedDialog;
                dlg.MaximizeBox = false;
                dlg.MinimizeBox = false;
                dlg.ShowIcon = false;

                var lblPrompt = new Label
                {
                    Text = $"Enter a new Label identifier for '{sDoc.FileName}':",
                    Location = new Point(15, 15),
                    AutoSize = true
                };

                var txtLabel = new TextBox
                {
                    Text = oldLabel,
                    Location = new Point(15, 40),
                    Width = 330
                };

                var btnOk = new Button
                {
                    Text = "OK",
                    DialogResult = DialogResult.OK,
                    Location = new Point(175, 78),
                    Width = 80,
                    Height = 26
                };

                var btnCancel = new Button
                {
                    Text = "Cancel",
                    DialogResult = DialogResult.Cancel,
                    Location = new Point(265, 78),
                    Width = 80,
                    Height = 26
                };

                dlg.Controls.Add(lblPrompt);
                dlg.Controls.Add(txtLabel);
                dlg.Controls.Add(btnOk);
                dlg.Controls.Add(btnCancel);
                dlg.AcceptButton = btnOk;
                dlg.CancelButton = btnCancel;

                if (dlg.ShowDialog() == DialogResult.OK)
                {
                    string newLabel = txtLabel.Text.Trim();
                    if (string.IsNullOrWhiteSpace(newLabel))
                    {
                        MessageBox.Show("File Label cannot be blank.", "Invalid Label", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }

                    if (string.Equals(oldLabel, newLabel, StringComparison.OrdinalIgnoreCase))
                    {
                        return;
                    }

                    if (_session.Documents.Any(d => d != sDoc && string.Equals(d.LanguageTag, newLabel, StringComparison.OrdinalIgnoreCase)))
                    {
                        MessageBox.Show($"A file with the label '{newLabel}' already exists in this session. All file labels must be unique.",
                            "Duplicate File Label", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }

                    sDoc.LanguageTag = newLabel;
                    sDoc.IsModified = true;
                    OnSessionUpdated();
                    RebuildCategoryTreeAndGrid();
                    MessageBox.Show($"Updated file label to '{newLabel}'.", "Label Updated", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
        }

        private void PromptSelectLanguage(CsfSessionDocument sDoc)
        {
            if (sDoc == null) return;
            string oldTag = sDoc.LanguageTag ?? string.Empty;

            using (var dlg = new Form())
            {
                dlg.Text = $"Select Language - '{sDoc.FileName}'";
                dlg.Size = new Size(420, 185);
                dlg.StartPosition = FormStartPosition.CenterParent;
                dlg.FormBorderStyle = FormBorderStyle.FixedDialog;
                dlg.MaximizeBox = false;
                dlg.MinimizeBox = false;
                dlg.ShowIcon = false;

                var lblPrompt = new Label
                {
                    Text = $"Select Language for '{sDoc.FileName}' ({oldTag}):",
                    Location = new Point(15, 15),
                    AutoSize = true,
                    Font = new Font(FontFamily.GenericSansSerif, 8.5f, FontStyle.Bold)
                };

                var cboLang = new ComboBox
                {
                    DropDownStyle = ComboBoxStyle.DropDown,
                    Location = new Point(15, 42),
                    Width = 370,
                    Font = new Font(FontFamily.GenericSansSerif, 8.5f)
                };

                cboLang.Items.AddRange(new object[] {
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

                string matchedItem = cboLang.Items.Cast<object>()
                    .Select(o => o.ToString())
                    .FirstOrDefault(str => str.Contains($"[{oldTag}]") || str.Equals(oldTag, StringComparison.OrdinalIgnoreCase));

                if (!string.IsNullOrEmpty(matchedItem))
                    cboLang.SelectedItem = matchedItem;
                else if (!string.IsNullOrWhiteSpace(oldTag))
                    cboLang.Text = oldTag;
                else
                    cboLang.SelectedIndex = 0;

                var btnOk = new Button
                {
                    Text = "OK",
                    DialogResult = DialogResult.OK,
                    Location = new Point(215, 95),
                    Width = 80,
                    Height = 26
                };

                var btnCancel = new Button
                {
                    Text = "Cancel",
                    DialogResult = DialogResult.Cancel,
                    Location = new Point(305, 95),
                    Width = 80,
                    Height = 26
                };

                dlg.Controls.Add(lblPrompt);
                dlg.Controls.Add(cboLang);
                dlg.Controls.Add(btnOk);
                dlg.Controls.Add(btnCancel);
                dlg.AcceptButton = btnOk;
                dlg.CancelButton = btnCancel;

                if (dlg.ShowDialog() == DialogResult.OK)
                {
                    string comboText = cboLang.Text.Trim();
                    string newTag = comboText;
                    int s = comboText.IndexOf('[');
                    int e = comboText.IndexOf(']');
                    if (s >= 0 && e > s)
                    {
                        newTag = comboText.Substring(s + 1, e - s - 1).Trim();
                    }

                    if (string.IsNullOrWhiteSpace(newTag))
                    {
                        MessageBox.Show("Language tag cannot be blank.", "Invalid Language", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }

                    if (string.Equals(oldTag, newTag, StringComparison.OrdinalIgnoreCase))
                    {
                        return;
                    }

                    if (_session.Documents.Any(d => d != sDoc && string.Equals(d.LanguageTag, newTag, StringComparison.OrdinalIgnoreCase)))
                    {
                        MessageBox.Show($"A file with language tag '{newTag}' already exists in this session. All file language tags must be unique.",
                            "Duplicate Language Tag", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }

                    sDoc.LanguageTag = newTag;
                    if (sDoc.Document != null)
                    {
                        sDoc.Document.Language = MapTagToCsfLanguage(newTag);
                    }
                    sDoc.IsModified = true;

                    UpdateUIForSessionMode();
                    RebuildCategoryTreeAndGrid();
                    UpdateFormTitle();
                    MessageBox.Show($"Updated file language tag to '{newTag}'.", "Language Updated", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
        }

        private static CsfLanguage MapTagToCsfLanguage(string tag)
        {
            if (string.IsNullOrWhiteSpace(tag)) return CsfLanguage.EnglishUS;
            string t = tag.ToLowerInvariant();
            if (t.Contains("es") || t.Contains("spanish")) return CsfLanguage.Spanish;
            if (t.Contains("fr") || t.Contains("french")) return CsfLanguage.French;
            if (t.Contains("de") || t.Contains("german")) return CsfLanguage.German;
            if (t.Contains("it") || t.Contains("italian")) return CsfLanguage.Italian;
            if (t.Contains("ja") || t.Contains("jp") || t.Contains("japanese")) return CsfLanguage.Japanese;
            if (t.Contains("ko") || t.Contains("korean")) return CsfLanguage.Korean;
            if (t.Contains("zh") || t.Contains("chi") || t.Contains("chinese")) return CsfLanguage.Chinese;
            return CsfLanguage.EnglishUS;
        }

        private ToolStripMenuItem CreateCapitalizationSubMenu(TextBox txtEditor)
        {
            var menuCap = new ToolStripMenuItem("🔤 Capitalization");

            var itemUpper = new ToolStripMenuItem("UPPERCASE");
            itemUpper.Click += (s, e) =>
            {
                if (txtEditor != null && txtEditor.SelectionLength > 0)
                {
                    txtEditor.SelectedText = txtEditor.SelectedText.ToUpperInvariant();
                }
            };

            var itemLower = new ToolStripMenuItem("lowercase");
            itemLower.Click += (s, e) =>
            {
                if (txtEditor != null && txtEditor.SelectionLength > 0)
                {
                    txtEditor.SelectedText = txtEditor.SelectedText.ToLowerInvariant();
                }
            };

            var itemTitle = new ToolStripMenuItem("Title Case");
            itemTitle.Click += (s, e) =>
            {
                if (txtEditor != null && txtEditor.SelectionLength > 0)
                {
                    var textInfo = System.Globalization.CultureInfo.InvariantCulture.TextInfo;
                    txtEditor.SelectedText = textInfo.ToTitleCase(txtEditor.SelectedText.ToLowerInvariant());
                }
            };

            var itemSentence = new ToolStripMenuItem("Sentence case");
            itemSentence.Click += (s, e) =>
            {
                if (txtEditor != null && txtEditor.SelectionLength > 0)
                {
                    txtEditor.SelectedText = ToSentenceCase(txtEditor.SelectedText);
                }
            };

            menuCap.DropDownItems.Add(itemUpper);
            menuCap.DropDownItems.Add(itemLower);
            menuCap.DropDownItems.Add(itemTitle);
            menuCap.DropDownItems.Add(itemSentence);

            return menuCap;
        }

        private ContextMenuStrip CreateTargetContextMenu(CsfSessionDocument sDoc, string langTag, MasterKeyRow row, TextBox txtEditor = null)
        {
            var menu = new ContextMenuStrip();

            if (row != null)
            {
                bool exists = row.ValuesPerLanguage.TryGetValue(langTag, out var entry) && entry != null;
                string targetTextToTranslate = exists ? (entry.Value ?? string.Empty) : string.Empty;
                if (_langTextEditors.TryGetValue(langTag, out var txtSrcEditor) && !string.IsNullOrEmpty(txtSrcEditor.Text))
                {
                    targetTextToTranslate = txtSrcEditor.Text;
                }
                string isoCode = GetTranslationLanguageForDocument(sDoc, false);
                string languageLabel = GetTranslationLanguageMenuLabel(sDoc, langTag);
                menu.Items.Add(CreateTranslationSubMenu(targetTextToTranslate, isoCode, languageLabel, row?.KeyName, sDoc, exists: exists, targetFileLabel: langTag, targetTextBox: txtEditor));
                menu.Items.Add(new ToolStripSeparator());
            }

            if (txtEditor != null)
            {
                var itemCut = new ToolStripMenuItem("✂️ Cut", null, (s, e) => txtEditor.Cut()) { ShortcutKeyDisplayString = "Ctrl+X" };
                var itemCopy = new ToolStripMenuItem("📋 Copy", null, (s, e) => txtEditor.Copy()) { ShortcutKeyDisplayString = "Ctrl+C" };
                var itemPaste = new ToolStripMenuItem("📋 Paste", null, (s, e) => txtEditor.Paste()) { ShortcutKeyDisplayString = "Ctrl+V" };
                var itemSelectAll = new ToolStripMenuItem("🔠 Select All", null, (s, e) => txtEditor.SelectAll()) { ShortcutKeyDisplayString = "Ctrl+A" };
                var menuCap = CreateCapitalizationSubMenu(txtEditor);

                menu.Opening += (s, e) =>
                {
                    bool hasSelection = txtEditor.SelectionLength > 0;
                    itemCut.Enabled = hasSelection && !txtEditor.ReadOnly;
                    itemCopy.Enabled = hasSelection;
                    itemPaste.Enabled = !txtEditor.ReadOnly && Clipboard.ContainsText();
                    itemSelectAll.Enabled = txtEditor.TextLength > 0;
                    menuCap.Enabled = hasSelection;
                };

                menu.Items.Add(itemCut);
                menu.Items.Add(itemCopy);
                menu.Items.Add(itemPaste);
                menu.Items.Add(itemSelectAll);
                menu.Items.Add(new ToolStripSeparator());
                menu.Items.Add(menuCap);
                menu.Items.Add(new ToolStripSeparator());
            }

            var itemRenameLabel = new ToolStripMenuItem("🏷️ Rename / Edit File Label...");
            itemRenameLabel.Click += (s, e) => PromptRenameFileLabel(sDoc);
            menu.Items.Add(itemRenameLabel);

            var itemChangeLang = new ToolStripMenuItem("🌍 Set Translation Content Language...");
            itemChangeLang.Click += (s, e) => PromptTranslationContentLanguage(sDoc);
            menu.Items.Add(itemChangeLang);

            var itemChangeLangId = new ToolStripMenuItem($"🌐 Change Header Language ID (Offset 0x14)...");
            itemChangeLangId.Click += (s, e) => PromptChangeHeaderLanguage(sDoc);
            menu.Items.Add(itemChangeLangId);

            menu.Items.Add(new ToolStripSeparator());

            if (_session.Documents.Count > 1)
            {
                if (row != null)
                {
                    bool exists = row.ValuesPerLanguage.TryGetValue(langTag, out var entry) && entry != null;
                    string srcText = exists ? (entry.Value ?? string.Empty) : string.Empty;
                    string srcAudio = entry?.ExtraValue;

                    if (_langTextEditors.TryGetValue(langTag, out var txtSrcEditor) && !string.IsNullOrEmpty(txtSrcEditor.Text))
                    {
                        srcText = txtSrcEditor.Text;
                    }

                    var itemCopyText = new ToolStripMenuItem($"📋 Copy '{langTag}' Text to ALL other files")
                    {
                        Enabled = exists && !string.IsNullOrEmpty(srcText)
                    };
                    itemCopyText.Click += (s, e) => CopyValueToAllOtherDocs(row.KeyName, langTag, srcText, null, copyText: true, copyAudio: false);

                    var itemCopyAudio = new ToolStripMenuItem($"🎵 Copy '{langTag}' Audio WAV to ALL other files")
                    {
                        Enabled = exists && !string.IsNullOrEmpty(srcAudio)
                    };
                    itemCopyAudio.Click += (s, e) => CopyValueToAllOtherDocs(row.KeyName, langTag, null, srcAudio, copyText: false, copyAudio: true);

                    var itemCopyBoth = new ToolStripMenuItem($"🔄 Copy '{langTag}' BOTH Text & Audio to ALL other files")
                    {
                        Enabled = exists && (!string.IsNullOrEmpty(srcText) || !string.IsNullOrEmpty(srcAudio))
                    };
                    itemCopyBoth.Click += (s, e) => CopyValueToAllOtherDocs(row.KeyName, langTag, srcText, srcAudio, copyText: true, copyAudio: true);

                    menu.Items.Add(itemCopyText);
                    menu.Items.Add(itemCopyAudio);
                    menu.Items.Add(itemCopyBoth);
                    menu.Items.Add(new ToolStripSeparator());
                }

                var itemMassSyncAudio = new ToolStripMenuItem($"⚡ Mass Sync ALL Audio WAVs from [{langTag}] to ALL other files");
                var targetDoc = sDoc;
                itemMassSyncAudio.Click += (s, e) => MassSyncAudioFromDoc(targetDoc);
                menu.Items.Add(itemMassSyncAudio);
                menu.Items.Add(new ToolStripSeparator());
            }

            var itemOpenExplorer = new ToolStripMenuItem("📂 Open File Location in Explorer")
            {
                Enabled = sDoc != null && !string.IsNullOrEmpty(sDoc.FilePath) && File.Exists(sDoc.FilePath)
            };
            itemOpenExplorer.Click += (s, e) =>
            {
                if (sDoc != null && !string.IsNullOrEmpty(sDoc.FilePath) && File.Exists(sDoc.FilePath))
                {
                    try
                    {
                        System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{sDoc.FilePath}\"");
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error opening explorer:\n{ex.Message}");
                    }
                }
            };
            menu.Items.Add(itemOpenExplorer);

            var baseDoc = _session?.BaseDocument ?? _session?.Documents.FirstOrDefault();
            var targetDocs = _session?.Documents.Where(d => d != baseDoc).ToList() ?? new List<CsfSessionDocument>();

            if (targetDocs.Count >= 1)
            {
                menu.Items.Add(new ToolStripSeparator());

                bool isUnpinned = _unpinnedTargetLanguageTags.Contains(langTag);
                if (!isUnpinned)
                {
                    var itemSplit = new ToolStripMenuItem($"📖 Move '{langTag}' to Split View (Unpin Column)");
                    itemSplit.Click += (s, e) =>
                    {
                        _unpinnedTargetLanguageTags.Add(langTag);

                        // If after unpinning this column, only 1 target language remains pinned, auto-unpin that last remaining language as well
                        var bDoc = _session?.BaseDocument ?? _session?.Documents.FirstOrDefault();
                        var tDocs = _session?.Documents.Where(d => d != bDoc).ToList() ?? new List<CsfSessionDocument>();
                        var remainingPinned = tDocs.Where(d => !_unpinnedTargetLanguageTags.Contains(d.LanguageTag)).ToList();
                        if (remainingPinned.Count == 1)
                        {
                            _unpinnedTargetLanguageTags.Add(remainingPinned[0].LanguageTag);
                        }

                        RefreshActiveSelectionInspector();
                    };
                    menu.Items.Add(itemSplit);
                }
                else
                {
                    var itemDock = new ToolStripMenuItem($"📌 Dock '{langTag}' Back to Tab Group");
                    itemDock.Click += (s, e) =>
                    {
                        _unpinnedTargetLanguageTags.Remove(langTag);
                        RefreshActiveSelectionInspector();
                    };
                    menu.Items.Add(itemDock);
                }

                if (_unpinnedTargetLanguageTags.Count > 0)
                {
                    var itemDockAll = new ToolStripMenuItem("📌 Dock All Back to Tab Group");
                    itemDockAll.Click += (s, e) =>
                    {
                        _unpinnedTargetLanguageTags.Clear();
                        RefreshActiveSelectionInspector();
                    };
                    menu.Items.Add(itemDockAll);
                }
            }

            return menu;
        }

        private ContextMenuStrip CreateBaseContextMenu(CsfSessionDocument baseDoc, MasterKeyRow row, TextBox txtEditor = null)
        {
            var menu = new ContextMenuStrip();

            if (row != null && baseDoc != null)
            {
                string baseLangTag = baseDoc.LanguageTag;
                string baseTextToTranslate = row.ValuesPerLanguage.TryGetValue(baseLangTag, out var entry) ? entry.Value : string.Empty;
                if (_langTextEditors.TryGetValue(baseLangTag, out var txtBaseEditor) && !string.IsNullOrEmpty(txtBaseEditor.Text))
                {
                    baseTextToTranslate = txtBaseEditor.Text;
                }
                string baseIsoCode = GetTranslationLanguageForDocument(baseDoc, false);
                string baseLanguageLabel = GetTranslationLanguageMenuLabel(baseDoc, baseLangTag);
                menu.Items.Add(CreateTranslationSubMenu(baseTextToTranslate, baseIsoCode, baseLanguageLabel, row?.KeyName, baseDoc, exists: true, targetFileLabel: baseLangTag, targetTextBox: txtEditor));
                menu.Items.Add(new ToolStripSeparator());
            }

            if (txtEditor != null)
            {
                var itemCut = new ToolStripMenuItem("✂️ Cut", null, (s, e) => txtEditor.Cut()) { ShortcutKeyDisplayString = "Ctrl+X" };
                var itemCopy = new ToolStripMenuItem("📋 Copy", null, (s, e) => txtEditor.Copy()) { ShortcutKeyDisplayString = "Ctrl+C" };
                var itemPaste = new ToolStripMenuItem("📋 Paste", null, (s, e) => txtEditor.Paste()) { ShortcutKeyDisplayString = "Ctrl+V" };
                var itemSelectAll = new ToolStripMenuItem("🔠 Select All", null, (s, e) => txtEditor.SelectAll()) { ShortcutKeyDisplayString = "Ctrl+A" };
                var menuCap = CreateCapitalizationSubMenu(txtEditor);

                menu.Opening += (s, e) =>
                {
                    bool hasSelection = txtEditor.SelectionLength > 0;
                    itemCut.Enabled = hasSelection && !txtEditor.ReadOnly;
                    itemCopy.Enabled = hasSelection;
                    itemPaste.Enabled = !txtEditor.ReadOnly && Clipboard.ContainsText();
                    itemSelectAll.Enabled = txtEditor.TextLength > 0;
                    menuCap.Enabled = hasSelection;
                };

                menu.Items.Add(itemCut);
                menu.Items.Add(itemCopy);
                menu.Items.Add(itemPaste);
                menu.Items.Add(itemSelectAll);
                menu.Items.Add(new ToolStripSeparator());
                menu.Items.Add(menuCap);
                menu.Items.Add(new ToolStripSeparator());
            }

            if (baseDoc != null)
            {
                var itemRenameLabel = new ToolStripMenuItem("🏷️ Rename / Edit File Label...");
                itemRenameLabel.Click += (s, e) => PromptRenameFileLabel(baseDoc);
                menu.Items.Add(itemRenameLabel);

                var itemChangeLang = new ToolStripMenuItem("🌍 Set Translation Content Language...");
                itemChangeLang.Click += (s, e) => PromptTranslationContentLanguage(baseDoc);
                menu.Items.Add(itemChangeLang);

                var itemChangeLangId = new ToolStripMenuItem($"🌐 Change Header Language ID (Offset 0x14)...");
                itemChangeLangId.Click += (s, e) => PromptChangeHeaderLanguage(baseDoc);
                menu.Items.Add(itemChangeLangId);

                menu.Items.Add(new ToolStripSeparator());
            }

            if (_session.Documents.Count > 1)
            {
                if (row != null)
                {
                    string baseLangTag = baseDoc.LanguageTag;
                    string baseText = row.ValuesPerLanguage.TryGetValue(baseLangTag, out var entry) ? entry.Value : string.Empty;
                    string baseAudio = entry?.ExtraValue;

                    if (_langTextEditors.TryGetValue(baseLangTag, out var txtBaseEditor) && !string.IsNullOrEmpty(txtBaseEditor.Text))
                    {
                        baseText = txtBaseEditor.Text;
                    }

                    var itemCopyText = new ToolStripMenuItem("📋 Copy text from Main CSF file to ALL target files");
                    itemCopyText.Click += (s, e) => CopyValueToAllOtherDocs(row.KeyName, baseLangTag, baseText, null, copyText: true, copyAudio: false);

                    var itemCopyAudio = new ToolStripMenuItem("🎵 Copy audio WAV from Main CSF file to ALL target files");
                    itemCopyAudio.Click += (s, e) => CopyValueToAllOtherDocs(row.KeyName, baseLangTag, null, baseAudio, copyText: false, copyAudio: true);

                    var itemCopyBoth = new ToolStripMenuItem("🔄 Copy BOTH text & audio from Main CSF file to ALL target files");
                    itemCopyBoth.Click += (s, e) => CopyValueToAllOtherDocs(row.KeyName, baseLangTag, baseText, baseAudio, copyText: true, copyAudio: true);

                    menu.Items.Add(itemCopyText);
                    menu.Items.Add(itemCopyAudio);
                    menu.Items.Add(itemCopyBoth);
                    menu.Items.Add(new ToolStripSeparator());
                }

                var itemMassSyncAudio = new ToolStripMenuItem($"⚡ Mass sync ALL audio WAVs from Main CSF file [{baseDoc.LanguageTag}] to ALL target files");
                itemMassSyncAudio.Click += (s, e) => MassSyncAudioFromDoc(baseDoc);
                menu.Items.Add(itemMassSyncAudio);
                menu.Items.Add(new ToolStripSeparator());
            }

            var itemOpenExplorer = new ToolStripMenuItem("📂 Open File Location in Explorer")
            {
                Enabled = baseDoc != null && !string.IsNullOrEmpty(baseDoc.FilePath) && File.Exists(baseDoc.FilePath)
            };
            itemOpenExplorer.Click += (s, e) =>
            {
                if (baseDoc != null && !string.IsNullOrEmpty(baseDoc.FilePath) && File.Exists(baseDoc.FilePath))
                {
                    try
                    {
                        System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{baseDoc.FilePath}\"");
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error opening explorer:\n{ex.Message}");
                    }
                }
            };
            menu.Items.Add(itemOpenExplorer);

            return menu;
        }

        private void PromptChangeHeaderLanguage(CsfSessionDocument sDoc)
        {
            if (sDoc == null || sDoc.Document == null) return;

            using (var dlg = new Form())
            {
                dlg.Text = $"Change Binary Language ID - {sDoc.LanguageTag}";
                dlg.Size = new Size(510, 260);
                dlg.StartPosition = FormStartPosition.CenterParent;
                dlg.FormBorderStyle = FormBorderStyle.FixedDialog;
                dlg.MaximizeBox = false;
                dlg.MinimizeBox = false;
                dlg.ShowIcon = false;

                var lblInfo = new Label
                {
                    Text = "Sets the 32-bit Language ID stored at byte offset 0x14 in the CSF binary header.\n\n" +
                           "⚠️ Ares Engine Expansion: Setting 'LanguageNeutral' (-1 / 0xFFFFFFFF) ONLY works when using the Ares Engine Expansion DLL. Vanilla C&C YR / RA2 does NOT support language-neutral CSF tables.",
                    Location = new Point(15, 12),
                    Size = new Size(465, 75),
                    Font = new Font(FontFamily.GenericSansSerif, 8.5f)
                };

                var lblComboPrompt = new Label
                {
                    Text = "Header Language ID:",
                    Location = new Point(15, 102),
                    AutoSize = true,
                    Font = new Font(FontFamily.GenericSansSerif, 8.5f, FontStyle.Bold)
                };

                var cmbLang = new ComboBox
                {
                    DropDownStyle = ComboBoxStyle.DropDownList,
                    Location = new Point(170, 99),
                    Size = new Size(305, 24)
                };

                var allLangs = Enum.GetValues(typeof(CsfLanguage)).Cast<CsfLanguage>().ToArray();
                foreach (CsfLanguage lang in allLangs)
                {
                    string extraInfo = lang == CsfLanguage.LanguageNeutral
                        ? "LanguageNeutral (-1 / 0xFFFFFFFF) [Requires Ares DLL]"
                        : $"{lang} ({(int)lang})";
                    cmbLang.Items.Add(extraInfo);

                    if (lang == sDoc.Document.Language)
                    {
                        cmbLang.SelectedItem = extraInfo;
                    }
                }

                if (cmbLang.SelectedIndex < 0 && cmbLang.Items.Count > 0)
                {
                    cmbLang.SelectedIndex = 0;
                }

                var btnOk = new Button
                {
                    Text = "OK",
                    DialogResult = DialogResult.None,
                    Location = new Point(285, 145),
                    Size = new Size(85, 28)
                };

                var btnCancel = new Button
                {
                    Text = "Cancel",
                    DialogResult = DialogResult.Cancel,
                    Location = new Point(385, 145),
                    Size = new Size(85, 28)
                };

                var lblContentLanguage = new Label
                {
                    Text = "Translation Content Language:",
                    Location = new Point(15, 140),
                    AutoSize = true,
                    Font = new Font(FontFamily.GenericSansSerif, 8.5f, FontStyle.Bold),
                    Visible = false
                };

                var cboContentLanguage = new ComboBox
                {
                    Location = new Point(235, 137),
                    Size = new Size(240, 24),
                    DropDownStyle = ComboBoxStyle.DropDown,
                    Visible = false
                };
                foreach (var option in TranslationLanguageHelper.GetLanguageOptions())
                {
                    cboContentLanguage.Items.Add(option);
                }

                bool contentLanguageSyncing = false;
                Action updateNeutralLanguageControls = () =>
                {
                    bool isNeutral = cmbLang.SelectedIndex >= 0 &&
                                     cmbLang.SelectedIndex < allLangs.Length &&
                                     allLangs[cmbLang.SelectedIndex] == CsfLanguage.LanguageNeutral;
                    lblContentLanguage.Visible = isNeutral;
                    cboContentLanguage.Visible = isNeutral;
                    dlg.Height = isNeutral ? 300 : 260;
                    btnOk.Location = new Point(285, isNeutral ? 190 : 145);
                    btnCancel.Location = new Point(385, isNeutral ? 190 : 145);

                    if (!isNeutral) return;

                    string contentLanguage = TranslationLanguageHelper.Normalize(sDoc.TranslationContentLanguage);
                    if (string.IsNullOrEmpty(contentLanguage))
                    {
                        contentLanguage = TranslationLanguageHelper.GetDefaultSourceLanguage();
                    }
                    string display = TranslationLanguageHelper.GetDisplayName(contentLanguage);
                    contentLanguageSyncing = true;
                    try
                    {
                        if (cboContentLanguage.Items.Contains(display))
                            cboContentLanguage.SelectedItem = display;
                        else
                            cboContentLanguage.Text = display;
                    }
                    finally
                    {
                        contentLanguageSyncing = false;
                    }
                };

                cmbLang.SelectedIndexChanged += (s, e) => updateNeutralLanguageControls();
                updateNeutralLanguageControls();

                btnOk.Click += (s, e) =>
                {
                    bool isNeutral = cmbLang.SelectedIndex >= 0 &&
                                     cmbLang.SelectedIndex < allLangs.Length &&
                                     allLangs[cmbLang.SelectedIndex] == CsfLanguage.LanguageNeutral;
                    if (isNeutral)
                    {
                        string contentLanguage = TranslationLanguageHelper.Normalize(cboContentLanguage.Text);
                        if (string.IsNullOrEmpty(contentLanguage) || contentLanguage == "auto")
                        {
                            MessageBox.Show("Select the language used by the text content of this neutral CSF.", "Language Required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            return;
                        }
                    }

                    dlg.DialogResult = DialogResult.OK;
                    dlg.Close();
                };

                cboContentLanguage.TextChanged += (s, e) =>
                {
                    if (!contentLanguageSyncing && cboContentLanguage.Visible)
                    {
                        string language = TranslationLanguageHelper.Normalize(cboContentLanguage.Text);
                        if (!string.IsNullOrEmpty(language) && language != "auto")
                        {
                            sDoc.TranslationContentLanguage = language;
                        }
                    }
                };

                dlg.Controls.Add(lblInfo);
                dlg.Controls.Add(lblComboPrompt);
                dlg.Controls.Add(cmbLang);
                dlg.Controls.Add(lblContentLanguage);
                dlg.Controls.Add(cboContentLanguage);
                dlg.Controls.Add(btnOk);
                dlg.Controls.Add(btnCancel);
                dlg.AcceptButton = btnOk;
                dlg.CancelButton = btnCancel;

                if (dlg.ShowDialog() == DialogResult.OK)
                {
                    int selectedIdx = cmbLang.SelectedIndex;
                    if (selectedIdx >= 0 && selectedIdx < allLangs.Length)
                    {
                        CsfLanguage newLang = allLangs[selectedIdx];
                        bool headerChanged = sDoc.Document.Language != newLang;
                        bool contentLanguageChanged = false;

                        if (newLang == CsfLanguage.LanguageNeutral)
                        {
                            string contentLanguage = TranslationLanguageHelper.Normalize(cboContentLanguage.Text);
                            if (string.IsNullOrEmpty(contentLanguage) || contentLanguage == "auto") return;
                            contentLanguageChanged = !string.Equals(sDoc.TranslationContentLanguage, contentLanguage, StringComparison.OrdinalIgnoreCase);
                            sDoc.TranslationContentLanguage = contentLanguage;
                        }

                        if (headerChanged)
                        {
                            sDoc.Document.Language = newLang;
                            sDoc.IsModified = true;
                        }

                        if (headerChanged || contentLanguageChanged)
                        {
                            OnSessionUpdated();
                            RebuildCategoryTreeAndGrid();
                            SaveSessionViewStateToConfig();
                            string message = headerChanged
                                ? $"Updated binary Language ID for [{sDoc.LanguageTag}] to {newLang} ({(int)newLang})."
                                : $"Updated translation content language for [{sDoc.LanguageTag}] to {sDoc.TranslationContentLanguage}.";
                            MessageBox.Show(message, "Language Settings Updated", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        }
                    }
                }
            }
        }

        private void menuSyncAudioWavs_Click(object sender, EventArgs e)
        {
            if (_session.BaseDocument == null) return;
            MassSyncAudioFromDoc(_session.BaseDocument);
        }

        private static int CountLineBreaks(string text)
        {
            if (string.IsNullOrEmpty(text)) return 0;
            string normalized = text.Replace("\\n", "\n").Replace("\r\n", "\n").Replace("\r", "\n");
            int count = 0;
            for (int i = 0; i < normalized.Length; i++)
            {
                if (normalized[i] == '\n') count++;
            }
            return count;
        }

        private static int CountFormatSpecifiers(string text)
        {
            if (string.IsNullOrEmpty(text)) return 0;
            return Regex.Matches(text, @"%([0-9]+\$)?[-+0-9.]*[a-zA-Z@]").Count;
        }

        private static string CheckLinterStatusString(string baseText, string currentText)
        {
            if (string.IsNullOrEmpty(baseText) || string.IsNullOrEmpty(currentText)) return "OK";

            int baseS = CountFormatSpecifiers(baseText);
            int currS = CountFormatSpecifiers(currentText);

            int baseN = CountLineBreaks(baseText);
            int currN = CountLineBreaks(currentText);

            var issues = new List<string>();
            if (baseS != currS) issues.Add($"Format specifiers mismatch (Base: {baseS}, Current: {currS})");
            if (baseN != currN) issues.Add($"Line breaks mismatch (Base: {baseN}, Current: {currN})");

            return issues.Count == 0 ? "OK (All modifiers match base)" : $"⚠️ {string.Join(" | ", issues)}";
        }

        private void CheckLinterModifiers(string baseText, string currentText, Label lblLinter)
        {
            if (lblLinter == null) return;
            if (string.IsNullOrEmpty(baseText) || string.IsNullOrEmpty(currentText))
            {
                lblLinter.Visible = false;
                return;
            }

            int baseFormatCount = CountFormatSpecifiers(baseText);
            int currFormatCount = CountFormatSpecifiers(currentText);

            int baseLineBreaks = CountLineBreaks(baseText);
            int currLineBreaks = CountLineBreaks(currentText);

            if (baseFormatCount != currFormatCount || baseLineBreaks != currLineBreaks)
            {
                lblLinter.Text = "⚠️ Linter Alert: Format specifiers or line breaks mismatch with base language";
                lblLinter.ForeColor = Color.DarkOrange;
                lblLinter.Visible = true;
            }
            else
            {
                lblLinter.Visible = false;
            }
        }

        #endregion

        #region Event Handlers & Menu Actions

        private void btnKeyFilterMode_Click(object sender, EventArgs e)
        {
            _keyRegexMode = !_keyRegexMode;
            var targetList = _keyRegexMode ? _appConfig.KeySearchHistoryRegex : _appConfig.KeySearchHistoryPlain;
            RefreshSearchComboItems(cboSearchKey, targetList, string.Empty);
            UpdateFilterButtonsUI();
            PopulateMasterGrid();
        }

        private void btnValFilterMode_Click(object sender, EventArgs e)
        {
            _valRegexMode = !_valRegexMode;
            var targetList = _valRegexMode ? _appConfig.ValueSearchHistoryRegex : _appConfig.ValueSearchHistoryPlain;
            RefreshSearchComboItems(cboSearchValue, targetList, string.Empty);
            UpdateFilterButtonsUI();
            PopulateMasterGrid();
        }

        private void UpdateFilterButtonsUI()
        {
            if (btnKeyFilterMode != null)
            {
                btnKeyFilterMode.Checked = _keyRegexMode;
                btnKeyFilterMode.Text = _keyRegexMode ? "🔍 Key Filter (RegEx):" : "🔍 Key Filter:";
                ToolTipHelper.SetToolTip(btnKeyFilterMode, _keyRegexMode
                    ? "Key Filter Mode: Regular Expression (RegEx) active. Click to switch to Plain Text search."
                    : "Key Filter Mode: Plain Text search active. Click to switch to Regular Expression (RegEx) mode.");
            }

            if (btnValFilterMode != null)
            {
                btnValFilterMode.Checked = _valRegexMode;
                btnValFilterMode.Text = _valRegexMode ? "🔍 Text Filter (RegEx):" : "🔍 Text Filter:";
                ToolTipHelper.SetToolTip(btnValFilterMode, _valRegexMode
                    ? "Text Filter Mode: Regular Expression (RegEx) active. Click to switch to Plain Text search."
                    : "Text Filter Mode: Plain Text search active. Click to switch to Regular Expression (RegEx) mode.");
            }

            string keyBoxTip = _keyRegexMode
                ? "Filter Key Names (RegEx Mode): Type a Regular Expression pattern (e.g. ^GUI:.*). Case-insensitive."
                : "Filter Key Names (Plain Text Mode): Type plain text to filter (Case-insensitive). Click 'Key Filter' button to switch to RegEx mode.";

            string valBoxTip = _valRegexMode
                ? "Filter String Values (RegEx Mode): Type a Regular Expression pattern (e.g. \\bunit\\b). Case-insensitive."
                : "Filter String Values (Plain Text Mode): Type plain text to filter (Case-insensitive). Click 'Text Filter' button to switch to RegEx mode.";

            if (cboSearchKey != null)
            {
                ToolTipHelper.SetToolTip(cboSearchKey, keyBoxTip);
            }

            if (cboSearchValue != null)
            {
                ToolTipHelper.SetToolTip(cboSearchValue, valBoxTip);
            }

            UpdateFilterLogicToolTip();
        }

        private void UpdateFilterLogicToolTip()
        {
            if (btnFilterLogic == null) return;

            string keyModeStr = _keyRegexMode ? "RegEx" : "Plain Text";
            string valModeStr = _valRegexMode ? "RegEx" : "Plain Text";

            string tooltipText = _filterLogicAnd
                ? $"Filter Combination: AND Mode\n• Matches keys where Key ({keyModeStr}) AND Text ({valModeStr}) conditions are BOTH met."
                : $"Filter Combination: OR Mode\n• Matches keys where EITHER Key ({keyModeStr}) OR Text ({valModeStr}) condition is met.";

            ToolTipHelper.SetToolTip(btnFilterLogic, tooltipText);
        }

        private void menuDuplicateKey_Click(object sender, EventArgs e)
        {
            var selectedKeys = GetCurrentlySelectedKeyNames();
            if (selectedKeys == null || selectedKeys.Count == 0)
            {
                MessageBox.Show("Please select at least one key in the grid to duplicate.", "Duplicate Keys", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string suffix = Microsoft.VisualBasic.Interaction.InputBox(
                $"Enter a suffix to append to the duplicated key(s):\n(Duplicating {selectedKeys.Count} key(s))",
                "Duplicate Keys",
                "_Copy");

            if (string.IsNullOrWhiteSpace(suffix)) return;

            var batchCmd = new BatchUndoCommand("Duplicate Keys");
            int count = 0;
            string firstNewKey = null;

            foreach (var oldKey in selectedKeys)
            {
                string newKey = oldKey + suffix.Trim();
                if (_session.KeyExists(newKey))
                {
                    int counter = 1;
                    while (_session.KeyExists($"{newKey}_{counter}")) counter++;
                    newKey = $"{newKey}_{counter}";
                }

                if (firstNewKey == null) firstNewKey = newKey;

                foreach (var sDoc in _session.Documents)
                {
                    var oldLbl = sDoc.Document.Labels.FirstOrDefault(l => string.Equals(l.Name, oldKey, StringComparison.OrdinalIgnoreCase));
                    if (oldLbl != null)
                    {
                        var newLbl = new CsfLabel(newKey);
                        foreach (var str in oldLbl.Strings)
                        {
                            newLbl.Strings.Add(new CsfStringEntry(str.Value, str.ExtraValue));
                        }
                        sDoc.Document.Labels.Add(newLbl);
                        sDoc.IsModified = true;
                    }
                }

                batchCmd.AddCommand(new AddKeyCommand(newKey));
                count++;
            }

            _undoManager.Execute(batchCmd, _session);
            RebuildCategoryTreeAndGrid();
            if (firstNewKey != null) EnsureKeyVisibleAndSelected(firstNewKey);

            ShowSaveNotification($"Duplicated {count} key(s) successfully.");
        }

        private static string ToSentenceCase(string input)
        {
            if (string.IsNullOrEmpty(input)) return input;

            char[] chars = input.ToLowerInvariant().ToCharArray();
            bool capitalizeNext = true;

            for (int i = 0; i < chars.Length; i++)
            {
                char c = chars[i];

                if (capitalizeNext && char.IsLetter(c))
                {
                    chars[i] = char.ToUpperInvariant(c);
                    capitalizeNext = false;
                }
                else if (c == '.' || c == '!' || c == '?' || c == '\n' || c == '\r')
                {
                    capitalizeNext = true;
                }
            }

            return new string(chars);
        }

        private void PerformCapitalization(string mode)
        {
            var selectedKeys = GetCurrentlySelectedKeyNames();
            bool isAll = false;
            if (selectedKeys == null || selectedKeys.Count == 0)
            {
                selectedKeys = _session.BuildMasterKeyList().Select(r => r.KeyName).ToList();
                isAll = true;
            }

            if (selectedKeys.Count == 0) return;

            string targetScopeText = isAll ? $"ALL {selectedKeys.Count:N0} key names in the active session" : $"{selectedKeys.Count:N0} selected key name(s)";

            var confirmResult = MessageBox.Show(
                $"Are you sure you want to apply '{mode}' capitalization to {targetScopeText}?\n\nThis will rename key identifiers across open CSF documents. You can undo this action with Ctrl+Z.",
                "Confirm Key Capitalization",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (confirmResult != DialogResult.Yes) return;

            var batchCmd = new BatchUndoCommand($"Capitalize Key Names ({mode})");
            int count = 0;

            foreach (var oldKey in selectedKeys)
            {
                string newKey = oldKey;
                if (mode == "UPPER") newKey = oldKey.ToUpperInvariant();
                else if (mode == "LOWER") newKey = oldKey.ToLowerInvariant();
                else if (mode == "TITLE")
                {
                    var textInfo = System.Globalization.CultureInfo.InvariantCulture.TextInfo;
                    newKey = textInfo.ToTitleCase(oldKey.ToLowerInvariant());
                }
                else if (mode == "SENTENCE")
                {
                    newKey = ToSentenceCase(oldKey);
                }

                if (newKey != oldKey)
                {
                    if (_session.RenameKey(oldKey, newKey))
                    {
                        batchCmd.AddCommand(new RenameKeyCommand(oldKey, newKey));
                        count++;
                    }
                }
            }

            if (count > 0)
            {
                _undoManager.Execute(batchCmd, _session);
                RebuildCategoryTreeAndGrid();
                ShowSaveNotification($"Capitalized {count} key name(s) to {mode}.");
            }
        }

        private void MarkKeyAsReordered(string keyName, int oldPos, int newPos)
        {
            if (string.IsNullOrEmpty(keyName)) return;

            if (oldPos == newPos)
            {
                _reorderedKeyDetails.Remove(keyName);
            }
            else
            {
                _reorderedKeyDetails[keyName] = $"Position changed (#{oldPos} ➔ #{newPos})";
                if (!_addedKeyNames.Contains(keyName) && !_deletedKeyNames.Contains(keyName))
                {
                    _modifiedKeyNames.Add(keyName);
                }
                _recentKeyTimestamps[keyName] = DateTime.Now;
            }
            _unsavedDirty = true;
            _coverageDirty = true;
            _recentDirty = true;
        }

        private void PerformMoveKey(int direction) => MovePhysicalKey(direction);

        private void MovePhysicalKey(int direction)
        {
            if (_session == null || _session.BaseDocument?.Document == null) return;
            List<string> selectedKeyNames = GetCurrentlySelectedKeyNames();
            if (selectedKeyNames == null || selectedKeyNames.Count == 0) return;

            var cmd = new ReorderKeyCommand(selectedKeyNames, direction, (key, oldPos, newPos) => MarkKeyAsReordered(key, oldPos, newPos));
            _undoManager.Execute(cmd, _session);
            UpdateUndoRedoMenuItems();

            InvalidateMasterRowsCache();
            _keyEditorDirty = true;
            _unsavedDirty = true;
            _coverageDirty = true;
            _recentDirty = true;

            if (tabControlMain != null && tabControlMain.SelectedTab == tabKeyEditor)
            {
                PopulateKeyEditorList(GetMasterRows());
                if (lstKeyEditorKeys != null)
                {
                    lstKeyEditorKeys.BeginUpdate();
                    lstKeyEditorKeys.ClearSelected();
                    foreach (var k in selectedKeyNames)
                    {
                        int newIdx = lstKeyEditorKeys.Items.IndexOf(k);
                        if (newIdx >= 0)
                        {
                            lstKeyEditorKeys.SetSelected(newIdx, true);
                        }
                    }
                    lstKeyEditorKeys.EndUpdate();
                }
            }
            else
            {
                var activeGrid = GetActiveGridForTab(tabControlMain?.SelectedTab) ?? gridLabels;
                RebuildCategoryTreeAndGrid();
                SyncSelectionToGrid(activeGrid, selectedKeyNames, preserveScrollPosition: true);
                OnGridSelectionChanged(activeGrid);
            }
        }

        private bool IsKeyCompleteInAllDocuments(string keyName)
        {
            if (_session == null || _session.Documents.Count == 0 || string.IsNullOrEmpty(keyName)) return false;

            foreach (var doc in _session.Documents)
            {
                if (doc.Document?.Labels == null) return false;

                var lbl = doc.Document.Labels.FirstOrDefault(l => string.Equals(l.Name, keyName, StringComparison.OrdinalIgnoreCase));
                if (lbl == null) return false;

                if (lbl.Strings == null || lbl.Strings.Count == 0) return false;

                string textValue = lbl.Strings[0].Value;
                if (string.IsNullOrWhiteSpace(textValue)) return false;
            }

            return true;
        }

        private bool IsRowEmptyOrUntranslated(DataGridView grid, DataGridViewRow r)
        {
            if (r == null) return false;

            string keyName = GetKeyNameFromRow(grid, r);
            if (string.IsNullOrEmpty(keyName)) return false;

            return !IsKeyCompleteInAllDocuments(keyName);
        }

        private void JumpToNextEmptyKey(bool forward)
        {
            var activeTab = tabControlMain?.SelectedTab;
            var grid = GetActiveGridForTab(activeTab) ?? gridLabels;
            if (grid == null || grid.Rows.Count == 0) return;

            int count = grid.Rows.Count;
            int startIdx = grid.CurrentRow != null ? grid.CurrentRow.Index : (forward ? -1 : count);
            int step = forward ? 1 : -1;

            for (int i = 1; i <= count; i++)
            {
                int curr = (startIdx + (i * step)) % count;
                if (curr < 0) curr += count;

                var r = grid.Rows[curr];
                if (IsRowEmptyOrUntranslated(grid, r))
                {
                    string keyName = GetKeyNameFromRow(grid, r);
                    if (!string.IsNullOrEmpty(keyName))
                    {
                        _lastActiveSelectedKeys = new List<string> { keyName };
                    }

                    grid.ClearSelection();
                    r.Selected = true;

                    bool showIndexCol = (grid.Columns.Count > 0 && grid.Columns[0].HeaderText == "#");
                    int keyColIdx = showIndexCol ? 2 : 1;
                    if (keyColIdx < r.Cells.Count)
                    {
                        try { grid.CurrentCell = r.Cells[keyColIdx]; } catch { }
                    }
                    else if (r.Cells.Count > 0)
                    {
                        try { grid.CurrentCell = r.Cells[0]; } catch { }
                    }

                    grid.FirstDisplayedScrollingRowIndex = Math.Max(0, curr - 5);
                    OnGridSelectionChanged(grid);

                    ShowSaveNotification($"🔍 Jumped to empty/untranslated key: '{keyName}' (Row {curr + 1})");
                    return;
                }
            }

            ShowSaveNotification("ℹ️ No untranslated or missing keys found in document.");
        }

        private void btnFilterLogic_Click(object sender, EventArgs e)
        {
            _filterLogicAnd = !_filterLogicAnd;
            btnFilterLogic.Text = _filterLogicAnd ? "AND" : "OR";
            UpdateFilterLogicToolTip();
            PopulateMasterGrid();
        }

        private void menuOpenSession_Click(object sender, EventArgs e)
        {
            OpenMultiCsfSession();
        }

        private string GetKeyNameFromRow(DataGridView grid, DataGridViewRow r)
        {
            if (r == null) return null;
            if (r.Tag is MasterKeyRow mRow) return mRow.KeyName;

            if (grid != null)
            {
                if (grid.Columns.Contains("colKey") && r.Cells["colKey"] != null && r.Cells["colKey"].Value is string cKey && !string.IsNullOrEmpty(cKey))
                    return cKey;
                if (grid.Columns.Contains("colCovKey") && r.Cells["colCovKey"] != null && r.Cells["colCovKey"].Value is string cCovKey && !string.IsNullOrEmpty(cCovKey))
                    return cCovKey;
                if (grid.Columns.Contains("colUnsavedKey") && r.Cells["colUnsavedKey"] != null && r.Cells["colUnsavedKey"].Value is string cUnKey && !string.IsNullOrEmpty(cUnKey))
                    return cUnKey;
            }

            for (int c = 0; c < r.Cells.Count; c++)
            {
                string val = r.Cells[c].Value?.ToString();
                if (string.IsNullOrWhiteSpace(val)) continue;
                if (val == "🟢" || val == "🟡" || val == "🔴" || val == "[Missing Entry]" || val == "[MISSING]") continue;
                if (int.TryParse(val, out _)) continue;
                return val;
            }
            return null;
        }

        private List<string> GetCurrentlySelectedKeyNames()
        {
            if (tabControlMain != null && tabControlMain.SelectedTab == tabKeyEditor)
            {
                if (lstKeyEditorKeys != null && lstKeyEditorKeys.SelectedIndices.Count > 0)
                {
                    var keys = new List<string>();
                    foreach (int idx in lstKeyEditorKeys.SelectedIndices)
                    {
                        if (idx >= 0 && idx < _keyEditorFilteredRows.Count && _keyEditorFilteredRows[idx] != null)
                        {
                            keys.Add(_keyEditorFilteredRows[idx].KeyName);
                        }
                    }
                    if (keys.Count > 0) return keys;
                }
            }

            var activeGrid = GetActiveGridForTab(tabControlMain?.SelectedTab);
            if (activeGrid == null) return null;

            var selectedGridRows = activeGrid.Rows.Cast<DataGridViewRow>().Where(r => r.Selected).ToList();
            if (selectedGridRows.Count == 0) return null;

            var selectedKeys = new List<string>();

            selectedGridRows.Sort((a, b) => a.Index.CompareTo(b.Index));
            foreach (DataGridViewRow r in selectedGridRows)
            {
                string keyName = GetKeyNameFromRow(activeGrid, r);
                if (!string.IsNullOrEmpty(keyName) && !selectedKeys.Contains(keyName, StringComparer.OrdinalIgnoreCase))
                {
                    selectedKeys.Add(keyName);
                }
            }

            return selectedKeys.Count > 0 ? selectedKeys : null;
        }

        private void PerformExportForDoc(CsfSessionDocument sDoc, List<string> explicitSelectedKeys = null)
        {
            if (sDoc == null || sDoc.Document == null) return;

            var selectedKeys = explicitSelectedKeys ?? GetCurrentlySelectedKeyNames();

            using (var dlg = new SaveFileDialog())
            {
                dlg.Filter = "Plain Text UTF-8 (*.txt)|*.txt";
                dlg.Title = selectedKeys != null && selectedKeys.Count > 0
                    ? $"Export {selectedKeys.Count} Selected Keys from [{sDoc.LanguageTag}] to Plain Text UTF-8"
                    : $"Export All Keys from [{sDoc.LanguageTag}] to Plain Text UTF-8";
                dlg.FileName = Path.GetFileNameWithoutExtension(sDoc.FileName) + ".txt";

                if (dlg.ShowDialog() == DialogResult.OK)
                {
                    CsfTxtExporterImporter.ExportToTxt(sDoc.Document, dlg.FileName, selectedKeys);
                    string msg = selectedKeys != null && selectedKeys.Count > 0
                        ? $"Successfully exported {selectedKeys.Count} selected keys from [{sDoc.LanguageTag}] to plain text."
                        : $"Successfully exported all {sDoc.Document.Labels.Count} keys from [{sDoc.LanguageTag}] to plain text.";
                    MessageBox.Show(msg, "Export Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
        }

        private void PerformImportForDoc(CsfSessionDocument sDoc)
        {
            if (sDoc == null || sDoc.Document == null) return;

            using (var dlg = new OpenFileDialog())
            {
                dlg.Filter = "Plain Text UTF-8 (*.txt)|*.txt";
                dlg.Title = $"Import Plain Text UTF-8 into [{sDoc.LanguageTag}]";
                InitFileDialogDirectory(dlg);
                if (dlg.ShowDialog() == DialogResult.OK)
                {
                    SaveLastOpenDirectory(dlg.FileName);
                    ImportTextFilePath(dlg.FileName, sDoc);
                }
            }
        }

        private void PerformExportKeyStructureForDoc(CsfSessionDocument sDoc)
        {
            if (sDoc == null || sDoc.Document == null) return;

            var selectedKeys = GetCurrentlySelectedKeyNames();
            var keysToExport = selectedKeys != null && selectedKeys.Count > 0
                ? selectedKeys
                : sDoc.Document.Labels.Select(l => l.Name).ToList();

            using (var dlg = new SaveFileDialog())
            {
                dlg.Filter = "Plain Text UTF-8 (*.txt)|*.txt";
                dlg.Title = selectedKeys != null && selectedKeys.Count > 0
                    ? $"Export {selectedKeys.Count} Selected Key Names Only from [{sDoc.LanguageTag}] to Plain Text UTF-8"
                    : $"Export All Key Names Only from [{sDoc.LanguageTag}] to Plain Text UTF-8";
                dlg.FileName = Path.GetFileNameWithoutExtension(sDoc.FileName) + "_KeysOnly.txt";
                InitFileDialogDirectory(dlg);

                if (dlg.ShowDialog() == DialogResult.OK)
                {
                    SaveLastOpenDirectory(dlg.FileName);
                    CsfTxtExporterImporter.ExportKeyStructureToTxt(keysToExport, dlg.FileName);
                    string msg = selectedKeys != null && selectedKeys.Count > 0
                        ? $"Successfully exported {selectedKeys.Count} selected key names to plain text."
                        : $"Successfully exported all {keysToExport.Count} key names to plain text.";
                    MessageBox.Show(msg, "Export Key Structure Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
        }

        private void menuExportTxt_Click(object sender, EventArgs e)
        {
            if (_session.Documents.Count > 1) return;
            var targetDoc = _session.BaseDocument ?? _session.Documents.FirstOrDefault();
            PerformExportForDoc(targetDoc);
        }

        private void menuExportKeysOnly_Click(object sender, EventArgs e)
        {
            if (_session.Documents.Count > 1) return;
            var targetDoc = _session.BaseDocument ?? _session.Documents.FirstOrDefault();
            PerformExportKeyStructureForDoc(targetDoc);
        }

        private void menuImportTxt_Click(object sender, EventArgs e)
        {
            if (_session.Documents.Count > 1) return;
            var targetDoc = _session.BaseDocument ?? _session.Documents.FirstOrDefault();
            PerformImportForDoc(targetDoc);
        }

        private void menuSyncKeys_Click(object sender, EventArgs e)
        {
            int added = _session.SynchronizeAllMissingKeys(true);
            RebuildCategoryTreeAndGrid();
            MessageBox.Show($"Synchronization completed. Created {added} missing keys across open files.", "Synchronization Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void menuScanIni_Click(object sender, EventArgs e)
        {
            using (var dlg = new OpenFileDialog())
            {
                dlg.Filter = "C&C INI & Map Files (*.ini;*.map)|*.ini;*.map|C&C INI Files (*.ini)|*.ini|C&C Map Files (*.map)|*.map|All Files (*.*)|*.*";
                dlg.Title = "Select Mod INI or Map Files (rulesmd.ini, artmd.ini, mission.map, etc.)";
                dlg.Multiselect = true;
                InitFileDialogDirectory(dlg);

                if (dlg.ShowDialog() == DialogResult.OK && dlg.FileNames.Length > 0)
                {
                    SaveLastOpenDirectory(dlg.FileNames[0]);
                    var filesMap = new Dictionary<string, List<IniScanResult>>(StringComparer.OrdinalIgnoreCase);
                    foreach (var file in dlg.FileNames)
                    {
                        var results = IniScanner.ScanIniFile(file, _session);
                        filesMap[file] = results;
                    }

                    using (var scanDlg = new IniScanResultDialog(filesMap, _session))
                    {
                        var dlgRes = scanDlg.ShowDialog();
                        if (dlgRes == DialogResult.OK || scanDlg.AnyKeysAdded)
                        {
                            UpdateUIForSessionMode();
                            RebuildCategoryTreeAndGrid();
                            PopulateMasterGrid();
                            UpdateFormTitle();
                            ShowSaveNotification("⚡ Added missing keys from INI scan to session");
                        }
                    }
                }
            }
        }



        private void menuConvertAnsi_Click(object sender, EventArgs e)
        {
            if (_session == null || _session.Documents.Count == 0)
            {
                MessageBox.Show("No open CSF files available for conversion. Please load or create a CSF file first.",
                    "No Files Loaded", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using (var dlg = new ConvertAnsiDialog(_session))
            {
                if (dlg.ShowDialog() == DialogResult.OK)
                {
                    var selectedDocs = dlg.SelectedDocuments;
                    var encoding = dlg.SelectedEncoding;

                    if (selectedDocs == null || selectedDocs.Count == 0) return;

                    // 1. Create a safety session snapshot BEFORE conversion!
                    BackupManager.CreateSessionSnapshot(_session, $"Convert ANSI ({encoding.EncodingName})");

                    int totalConverted = 0;
                    foreach (var sDoc in selectedDocs)
                    {
                        if (sDoc?.Document == null) continue;

                        int docConverted = 0;
                        foreach (var lbl in sDoc.Document.Labels)
                        {
                            foreach (var entry in lbl.Strings)
                            {
                                if (string.IsNullOrEmpty(entry.Value)) continue;

                                string original = entry.Value;
                                string converted = ConvertAnsiDialog.ConvertAnsiToUnicode(original, encoding);
                                if (!string.Equals(original, converted, StringComparison.Ordinal))
                                {
                                    entry.Value = converted;
                                    docConverted++;
                                    totalConverted++;
                                }
                            }
                        }

                        if (docConverted > 0)
                        {
                            sDoc.IsModified = true;
                        }
                    }

                    UpdateUIForSessionMode();
                    RebuildCategoryTreeAndGrid();
                    PopulateBackupsTab();

                    MessageBox.Show(
                        $"Successfully converted {totalConverted} string entries across {selectedDocs.Count} file(s) using codepage '{encoding.EncodingName}'.\n\n" +
                        "The open files have been marked as modified (*). An automatic backup snapshot was saved to the Backups tab.",
                        "ANSI Conversion Complete",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                }
            }
        }

        #region Integrated Session Backups Tab (tabBackups)

        private Label _lblBackupEmptyState;
        private SplitContainer _splitBackupMasterDetail;
        private ListBox _lstBackupSnapshots;
        private TabControl _tabBackupFiles;
        private Button _btnCreateManualBackup;

        private void InitializeBackupsTabControls()
        {
            tabBackups.Controls.Clear();

            _lblBackupEmptyState = new Label
            {
                Text = "🛡️ No backup snapshots exist for this session yet.\n\nBackup snapshots are created automatically whenever you save changes to your CSF files.\nOld backups older than 30 days are automatically cleaned up.",
                TextAlign = ContentAlignment.MiddleCenter,
                Dock = DockStyle.Fill,
                Font = new Font(FontFamily.GenericSansSerif, 11f, FontStyle.Regular),
                ForeColor = Color.DimGray
            };

            _splitBackupMasterDetail = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                FixedPanel = FixedPanel.Panel1,
                SplitterDistance = 215,
                Visible = false
            };

            // LEFT PANEL: List of Snapshots
            var pnlLeft = new Panel { Dock = DockStyle.Fill, Padding = new Padding(6) };
            var lblSnapHeader = new Label
            {
                Text = "📅 Session Snapshots:",
                Dock = DockStyle.Top,
                Height = 25,
                Font = new Font(FontFamily.GenericSansSerif, 9f, FontStyle.Bold)
            };

            _lstBackupSnapshots = new ListBox
            {
                Dock = DockStyle.Fill,
                DisplayMember = "DisplayName"
            };
            _lstBackupSnapshots.SelectedIndexChanged += (s, e) => OnSnapshotSelected();
            _lstBackupSnapshots.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Delete && _lstBackupSnapshots.SelectedItem is SessionSnapshot snap)
                {
                    PerformDeleteSnapshot(snap);
                }
            };

            var ctxList = new ContextMenuStrip();
            var itemDelSingle = new ToolStripMenuItem("🗑️ Delete Selected Snapshot");
            itemDelSingle.Click += (s, e) => PerformDeleteSnapshot(_lstBackupSnapshots.SelectedItem as SessionSnapshot);
            var itemDelAll = new ToolStripMenuItem("🧹 Clear All Snapshots History");
            itemDelAll.Click += (s, e) => PerformClearAllSnapshots();
            ctxList.Items.Add(itemDelSingle);
            ctxList.Items.Add(new ToolStripSeparator());
            ctxList.Items.Add(itemDelAll);
            _lstBackupSnapshots.ContextMenuStrip = ctxList;

            var pnlLeftBottom = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 65,
                Padding = new Padding(0, 4, 0, 0)
            };

            _btnCreateManualBackup = new Button
            {
                Text = "⚡ Create Snapshot",
                Location = new Point(0, 4),
                Width = 203,
                Height = 28,
                Font = new Font(FontFamily.GenericSansSerif, 8f, FontStyle.Bold)
            };
            _btnCreateManualBackup.Click += (s, e) =>
            {
                var snap = BackupManager.CreateSessionSnapshot(_session, "Manual Snapshot", _appConfig.BackupDirectoryPath, _appConfig.SaveInAppData);
                if (snap != null)
                {
                    PopulateBackupsTab();
                    MessageBox.Show("Successfully created a backup snapshot for the active session.", "Backup Created", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            };

            var btnDeleteSnap = new Button
            {
                Text = "🗑️ Delete",
                Location = new Point(0, 34),
                Width = 99,
                Height = 26
            };
            btnDeleteSnap.Click += (s, e) => PerformDeleteSnapshot(_lstBackupSnapshots.SelectedItem as SessionSnapshot);

            var btnClearAll = new Button
            {
                Text = "🧹 Clear All",
                Location = new Point(104, 34),
                Width = 99,
                Height = 26
            };
            btnClearAll.Click += (s, e) => PerformClearAllSnapshots();

            pnlLeftBottom.Controls.Add(_btnCreateManualBackup);
            pnlLeftBottom.Controls.Add(btnDeleteSnap);
            pnlLeftBottom.Controls.Add(btnClearAll);

            pnlLeft.Controls.Add(_lstBackupSnapshots);
            pnlLeft.Controls.Add(lblSnapHeader);
            pnlLeft.Controls.Add(pnlLeftBottom);
            _splitBackupMasterDetail.Panel1.Controls.Add(pnlLeft);

            // RIGHT PANEL: TabControl of Files in selected Snapshot
            var pnlRight = new Panel { Dock = DockStyle.Fill, Padding = new Padding(6) };
            _tabBackupFiles = new TabControl
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                DrawMode = TabDrawMode.OwnerDrawFixed,
                Padding = new Point(26, 7)
            };
            _tabBackupFiles.DrawItem += (s, e) =>
            {
                if (_tabBackupFiles == null || _tabBackupFiles.TabPages == null || e.Index < 0 || e.Index >= _tabBackupFiles.TabPages.Count) return;
                var g = e.Graphics;
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                var tab = _tabBackupFiles.TabPages[e.Index];
                var bounds = _tabBackupFiles.GetTabRect(e.Index);
                if (bounds.Width <= 0 || bounds.Height <= 0) return;

                var meta = tab.Tag as BackupTabMeta;
                bool isBase = meta != null && meta.IsBase;
                bool isSelected = (_tabBackupFiles.SelectedIndex == e.Index);
                bool hasChanges = tab.Text.Contains("changes");

                using (var backBrush = new SolidBrush(isSelected ? Color.FromArgb(245, 247, 250) : SystemColors.Control))
                {
                    g.FillRectangle(backBrush, bounds);
                }

                int textLeftOffset = bounds.Left;
                int textWidth = bounds.Width;

                if (isBase)
                {
                    using (var emojiFont = new Font("Segoe UI Emoji", 8.5f, FontStyle.Regular))
                    using (var pinBrush = new SolidBrush(Color.FromArgb(180, 40, 40)))
                    {
                        g.DrawString("📌", emojiFont, pinBrush, bounds.Left + 5, bounds.Top + (bounds.Height - 14) / 2);
                    }
                    textLeftOffset += 14;
                    textWidth -= 14;
                }

                var textRect = new Rectangle(textLeftOffset, bounds.Top, textWidth, bounds.Height);

                FontStyle fontStyle = hasChanges ? FontStyle.Bold : FontStyle.Regular;
                using (var font = new Font(_tabBackupFiles.Font, fontStyle))
                using (var textBrush = new SolidBrush(isSelected ? Color.FromArgb(0, 51, 102) : (hasChanges ? Color.FromArgb(180, 100, 0) : SystemColors.ControlText)))
                {
                    var sf = new StringFormat
                    {
                        Alignment = StringAlignment.Center,
                        LineAlignment = StringAlignment.Center,
                        Trimming = StringTrimming.EllipsisCharacter
                    };
                    g.DrawString(tab.Text, font, textBrush, textRect, sf);
                }

                if (isSelected)
                {
                    using (var pen = new Pen(Color.FromArgb(0, 120, 215), 2))
                    {
                        g.DrawLine(pen, bounds.Left, bounds.Bottom - 1, bounds.Right, bounds.Bottom - 1);
                    }
                }
            };
            _tabBackupFiles.SelectedIndexChanged += (s, e) =>
            {
                PopulateActiveBackupFileTab();
                if (_tabBackupFiles.SelectedTab != null)
                {
                    foreach (Control c in _tabBackupFiles.SelectedTab.Controls)
                    {
                        if (c is DataGridView g)
                        {
                            g.ClearSelection();
                            g.CurrentCell = null;
                        }
                    }
                }
            };

            pnlRight.Controls.Add(_tabBackupFiles);
            _splitBackupMasterDetail.Panel2.Controls.Add(pnlRight);

            tabBackups.Controls.Add(_splitBackupMasterDetail);
            tabBackups.Controls.Add(_lblBackupEmptyState);
        }

        private void PerformDeleteSnapshot(SessionSnapshot snap)
        {
            if (snap == null)
            {
                MessageBox.Show("Please select a backup snapshot to delete.", "No Selection", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (MessageBox.Show($"Are you sure you want to delete the backup snapshot from '{snap.Manifest.CreatedAt:yyyy-MM-dd HH:mm:ss}'?",
                "Delete Snapshot", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
            {
                if (BackupManager.DeleteSnapshot(snap))
                {
                    PopulateBackupsTab();
                }
                else
                {
                    MessageBox.Show("Could not delete the snapshot directory.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void PerformClearAllSnapshots()
        {
            if (_session == null || _session.BaseDocument == null || string.IsNullOrEmpty(_session.BaseDocument.FilePath)) return;
            if (MessageBox.Show("Are you sure you want to delete ALL backup snapshots for this session history?",
                "Clear History", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
            {
                BackupManager.DeleteAllSnapshots(_session.BaseDocument.FilePath, _appConfig.BackupDirectoryPath, _appConfig.SaveInAppData);
                PopulateBackupsTab();
            }
        }

        private void AdjustBackupSplitterRatio()
        {
            if (_splitBackupMasterDetail != null)
            {
                try { _splitBackupMasterDetail.SplitterDistance = 215; } catch { }
            }
        }

        private bool _isPopulatingSnapshots = false;

        private void PopulateBackupsTab()
        {
            if (_session == null || _isPopulatingSnapshots) return;
            _isPopulatingSnapshots = true;
            try
            {
                _backupScanValid = false; // backup set may have changed; force rescan on next visibility check
                _lstBackupSnapshots.Items.Clear();
                _tabBackupFiles.TabPages.Clear();

                string baseFilePath = _session.BaseDocument?.FilePath;
                var snapshots = BackupManager.GetAvailableSnapshots(baseFilePath, _appConfig.BackupDirectoryPath, _appConfig.SaveInAppData);

                if (snapshots.Count == 0)
                {
                    _lblBackupEmptyState.Visible = true;
                    _splitBackupMasterDetail.Visible = false;
                    return;
                }

                _lblBackupEmptyState.Visible = false;
                _splitBackupMasterDetail.Visible = true;
                AdjustBackupSplitterRatio();

                int prevIdx = _lstBackupSnapshots.SelectedIndex;
                foreach (var snap in snapshots)
                {
                    _lstBackupSnapshots.Items.Add(snap);
                }

                int targetIdx = (prevIdx >= 0 && prevIdx < _lstBackupSnapshots.Items.Count) ? prevIdx : (snapshots.Count > 0 ? 0 : -1);
                _lstBackupSnapshots.SelectedIndex = targetIdx;
            }
            finally
            {
                _isPopulatingSnapshots = false;
            }

            OnSnapshotSelected();
        }

        private class BackupTabMeta
        {
            public SessionSnapshot Snapshot;
            public string FileName;
            public string SnapFilePath;
            public CsfSessionDocument CurrentSDoc;
            public CsfDocument BackupDoc;
            public List<BackupDiffItem> DiffItems;
            public string LangTag;
            public bool IsBase;
            public bool IsPopulated;
        }

        private void OnSnapshotSelected()
        {
            if (_isPopulatingSnapshots) return;
            string prevActiveLangTag = null;
            if (_tabBackupFiles.SelectedTab != null && _tabBackupFiles.SelectedTab.Tag is BackupTabMeta oldMeta)
            {
                prevActiveLangTag = oldMeta.LangTag;
            }

            _tabBackupFiles.TabPages.Clear();
            var snap = _lstBackupSnapshots.SelectedItem as SessionSnapshot;
            if (snap == null || !Directory.Exists(snap.SnapshotFolderPath)) return;

            // Filter snapshot files to those matching open documents in the current active session
            var relevantFileNames = snap.Manifest.FileNames.Where(fileName =>
            {
                return _session != null && _session.Documents.Any(d =>
                    string.Equals(d.FileName, fileName, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals($"{d.LanguageTag}_{d.FileName}", fileName, StringComparison.OrdinalIgnoreCase) ||
                    fileName.StartsWith($"{d.LanguageTag}_", StringComparison.OrdinalIgnoreCase) ||
                    (fileName.Contains("_") && string.Equals(fileName.Substring(fileName.IndexOf('_') + 1), d.FileName, StringComparison.OrdinalIgnoreCase)));
            }).ToList();

            foreach (var fileName in relevantFileNames)
            {
                string snapFilePath = Path.Combine(snap.SnapshotFolderPath, fileName);
                if (!File.Exists(snapFilePath)) continue;

                CsfDocument backupDoc = null;
                try { backupDoc = CsfFileHandler.Load(snapFilePath); } catch { continue; }

                var currentSDoc = _session?.Documents.FirstOrDefault(d =>
                    !string.IsNullOrEmpty(d.LanguageTag) && fileName.StartsWith($"{d.LanguageTag}_", StringComparison.OrdinalIgnoreCase))
                    ?? _session?.Documents.FirstOrDefault(d => string.Equals($"{d.LanguageTag}_{d.FileName}", fileName, StringComparison.OrdinalIgnoreCase))
                    ?? _session?.Documents.FirstOrDefault(d => string.Equals(d.FileName, fileName, StringComparison.OrdinalIgnoreCase));

                bool isBase = (currentSDoc != null && currentSDoc == _session?.BaseDocument);
                string langTag = currentSDoc?.LanguageTag ?? (fileName.Contains("_") ? fileName.Split('_')[0] : "CSF");
                string displayFileName = currentSDoc?.FileName ?? (fileName.Contains("_") ? fileName.Substring(fileName.IndexOf('_') + 1) : fileName);

                var diffItems = BackupManager.CompareSnapshotDocWithCurrent(currentSDoc?.Document, backupDoc);
                int changeCount = diffItems.Count(item => item.DiffType != BackupDiffType.Unchanged);
                bool hasChanges = changeCount > 0;
                string changeBadge = hasChanges ? $" ({changeCount} changes)" : string.Empty;

                var meta = new BackupTabMeta
                {
                    Snapshot = snap,
                    FileName = fileName,
                    SnapFilePath = snapFilePath,
                    CurrentSDoc = currentSDoc,
                    BackupDoc = backupDoc,
                    DiffItems = diffItems,
                    LangTag = langTag,
                    IsBase = isBase,
                    IsPopulated = false
                };

                var tabPg = new TabPage
                {
                    Text = $"[{langTag}] {displayFileName}{changeBadge}",
                    Tag = meta,
                    Padding = new Padding(3)
                };

                _tabBackupFiles.TabPages.Add(tabPg);
            }

            if (!string.IsNullOrEmpty(prevActiveLangTag))
            {
                var matchingTab = _tabBackupFiles.TabPages.Cast<TabPage>()
                    .FirstOrDefault(t => (t.Tag as BackupTabMeta)?.LangTag.Equals(prevActiveLangTag, StringComparison.OrdinalIgnoreCase) == true);
                if (matchingTab != null)
                {
                    _tabBackupFiles.SelectedTab = matchingTab;
                }
            }

            PopulateActiveBackupFileTab();
        }

        private void PopulateActiveBackupFileTab()
        {
            var tabPg = _tabBackupFiles?.SelectedTab;
            if (tabPg == null) return;
            var meta = tabPg.Tag as BackupTabMeta;
            if (meta == null || meta.IsPopulated || meta.BackupDoc == null || meta.DiffItems == null) return;

            meta.IsPopulated = true;

            // Top Toolbar for this file tab
            var pnlTabHead = new Panel { Dock = DockStyle.Top, Height = 35 };
            var btnRestoreFile = new Button
            {
                Text = $"Restore Entire [{meta.LangTag}] File",
                Location = new Point(5, 5),
                Width = 190,
                Height = 25
            };
            var btnRestoreSession = new Button
            {
                Text = "Restore Full Session",
                Location = new Point(205, 5),
                Width = 170,
                Height = 25
            };

            pnlTabHead.Controls.Add(btnRestoreFile);
            pnlTabHead.Controls.Add(btnRestoreSession);

            // Diff Grid
            var gridDiff = new DataGridView
            {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
                ReadOnly = true,
                MultiSelect = true,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                RowHeadersVisible = false,
                ShowCellToolTips = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None,
                AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None,
                RowTemplate = { Height = 24 }
            };

            gridDiff.DefaultCellStyle.WrapMode = DataGridViewTriState.True;

            var colStatus = new DataGridViewTextBoxColumn { HeaderText = "Status", Width = 125, AutoSizeMode = DataGridViewAutoSizeColumnMode.None };
            var colKey = new DataGridViewTextBoxColumn { HeaderText = "Key Name", Width = 180, AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells };
            var colCurrText = new DataGridViewTextBoxColumn { HeaderText = "Current Text", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill };
            var colBakText = new DataGridViewTextBoxColumn { HeaderText = "Old Text", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill };
            var colCurrWav = new DataGridViewTextBoxColumn { HeaderText = "Current Audio", Width = 70, AutoSizeMode = DataGridViewAutoSizeColumnMode.None };
            var colBakWav = new DataGridViewTextBoxColumn { HeaderText = "Old Audio", Width = 70, AutoSizeMode = DataGridViewAutoSizeColumnMode.None };

            gridDiff.Columns.AddRange(colStatus, colKey, colCurrText, colBakText, colCurrWav, colBakWav);

            gridDiff.SuspendLayout();
            try
            {
                foreach (var item in meta.DiffItems)
                {
                    if (item.DiffType == BackupDiffType.Unchanged) continue;

                    int rIdx = gridDiff.Rows.Add(
                        item.StatusDisplay,
                        item.KeyName,
                        item.CurrentValue,
                        item.BackupValue,
                        item.CurrentExtra ?? "-",
                        item.BackupExtra ?? "-"
                    );

                    gridDiff.Rows[rIdx].Tag = item;

                    // Fast O(1) row height calculation for multiline text without UI freezes
                    int maxLen = Math.Max(item.CurrentValue?.Length ?? 0, item.BackupValue?.Length ?? 0);
                    int lineBreaks = Math.Max(
                        item.CurrentValue?.Split('\n').Length ?? 1,
                        item.BackupValue?.Split('\n').Length ?? 1
                    );
                    int estimatedLines = Math.Max(lineBreaks, (maxLen / 65) + 1);
                    int calculatedHeight = Math.Min(220, Math.Max(26, estimatedLines * 18 + 6));
                    gridDiff.Rows[rIdx].Height = calculatedHeight;

                    if (item.DiffType == BackupDiffType.Modified)
                    {
                        gridDiff.Rows[rIdx].DefaultCellStyle.BackColor = Color.FromArgb(255, 250, 205);
                    }
                    else if (item.DiffType == BackupDiffType.AddedInMemory)
                    {
                        gridDiff.Rows[rIdx].DefaultCellStyle.BackColor = Color.FromArgb(225, 250, 225);
                    }
                    else if (item.DiffType == BackupDiffType.DeletedInMemory)
                    {
                        gridDiff.Rows[rIdx].DefaultCellStyle.BackColor = Color.FromArgb(255, 225, 225);
                    }
                    else if (item.DiffType == BackupDiffType.Renamed)
                    {
                        gridDiff.Rows[rIdx].DefaultCellStyle.BackColor = Color.FromArgb(240, 230, 255);
                    }
                }
            }
            finally
            {
                gridDiff.ResumeLayout();
            }

            gridDiff.VisibleChanged += (s, e) =>
            {
                if (gridDiff.Visible)
                {
                    gridDiff.ClearSelection();
                    gridDiff.CurrentCell = null;
                }
            };

            gridDiff.ClearSelection();
            gridDiff.CurrentCell = null;

            // Context Menu on Grid to Restore Selected Key Entry(ies)
            var ctxRow = new ContextMenuStrip();
            var itemRestoreKey = new ToolStripMenuItem("Restore Selected Key Entry(ies) from Backup");
            itemRestoreKey.Click += (s, e) =>
            {
                if (gridDiff.SelectedRows.Count == 0 || meta.CurrentSDoc == null) return;

                var selectedDiffItems = gridDiff.SelectedRows.Cast<DataGridViewRow>()
                    .Select(r => r.Tag as BackupDiffItem)
                    .Where(item => item != null)
                    .ToList();

                if (selectedDiffItems.Count == 0) return;

                string confirmMsg = selectedDiffItems.Count == 1
                    ? $"Are you sure you want to restore key '{selectedDiffItems[0].KeyName}' in [{meta.LangTag}] from this backup snapshot?"
                    : $"Are you sure you want to restore {selectedDiffItems.Count} selected keys in [{meta.LangTag}] from this backup snapshot?";

                if (MessageBox.Show(confirmMsg, "Restore Selected Keys", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                {
                    BackupManager.CreateSessionSnapshot(_session, $"Pre-Restore {selectedDiffItems.Count} Keys", _appConfig.BackupDirectoryPath, _appConfig.SaveInAppData);

                    int restoredCount = 0;
                    foreach (var diffItem in selectedDiffItems)
                    {
                        var bakLbl = meta.BackupDoc.Labels.FirstOrDefault(l => string.Equals(l.Name, diffItem.KeyName, StringComparison.OrdinalIgnoreCase));
                        var currLbl = meta.CurrentSDoc.Document.Labels.FirstOrDefault(l => string.Equals(l.Name, diffItem.KeyName, StringComparison.OrdinalIgnoreCase));

                        if (bakLbl != null)
                        {
                            if (currLbl == null)
                            {
                                currLbl = new CsfLabel(bakLbl.Name);
                                meta.CurrentSDoc.Document.Labels.Add(currLbl);
                            }
                            currLbl.Strings.Clear();
                            foreach (var st in bakLbl.Strings) currLbl.Strings.Add(st.Clone());
                            restoredCount++;
                        }
                    }

                    if (restoredCount > 0)
                    {
                        meta.CurrentSDoc.IsModified = true;
                        RebuildCategoryTreeAndGrid();
                        PopulateBackupsTab();
                        MessageBox.Show($"Restored {restoredCount} key(s) in [{meta.LangTag}] from backup snapshot.", "Keys Restored", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }
            };
            ctxRow.Items.Add(itemRestoreKey);
            gridDiff.ContextMenuStrip = ctxRow;

            // File Restoration Button
            btnRestoreFile.Click += (s, e) =>
            {
                if (meta.CurrentSDoc == null || meta.BackupDoc == null) return;
                if (MessageBox.Show($"Are you sure you want to restore the entire [{meta.LangTag}] file from this snapshot?", "Restore File", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                {
                    BackupManager.CreateSessionSnapshot(_session, $"Pre-Restore File [{meta.LangTag}]", _appConfig.BackupDirectoryPath, _appConfig.SaveInAppData);
                    meta.CurrentSDoc.Document.Labels.Clear();
                    foreach (var l in meta.BackupDoc.Labels) meta.CurrentSDoc.Document.Labels.Add(l.Clone());
                    meta.CurrentSDoc.Document.Version = meta.BackupDoc.Version;
                    meta.CurrentSDoc.Document.Language = meta.BackupDoc.Language;
                    meta.CurrentSDoc.IsModified = true;
                    RebuildCategoryTreeAndGrid();
                    PopulateBackupsTab();
                    MessageBox.Show($"Restored file [{meta.LangTag}] from snapshot.", "File Restored", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            };

            // Full Session Restoration Button
            btnRestoreSession.Click += (s, e) =>
            {
                if (MessageBox.Show("Are you sure you want to restore the ENTIRE SESSION (all files) to this backup snapshot?", "Restore Full Session", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
                {
                    BackupManager.CreateSessionSnapshot(_session, "Pre-Restore Full Session", _appConfig.BackupDirectoryPath, _appConfig.SaveInAppData);
                    foreach (var fName in meta.Snapshot.Manifest.FileNames)
                    {
                        string fPath = Path.Combine(meta.Snapshot.SnapshotFolderPath, fName);
                        if (File.Exists(fPath))
                        {
                            var bDoc = CsfFileHandler.Load(fPath);
                            var target = _session.Documents.FirstOrDefault(d => string.Equals(d.FileName, fName, StringComparison.OrdinalIgnoreCase));
                            if (target != null)
                            {
                                target.Document.Labels.Clear();
                                foreach (var l in bDoc.Labels) target.Document.Labels.Add(l.Clone());
                                target.Document.Version = bDoc.Version;
                                target.Document.Language = bDoc.Language;
                                target.IsModified = true;
                            }
                        }
                    }
                    RebuildCategoryTreeAndGrid();
                    PopulateBackupsTab();
                    MessageBox.Show("Restored all open CSF files in session from backup snapshot.", "Session Restored", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            };

            if (gridDiff.Rows.Count == 0)
            {
                var lblNoDiff = new Label
                {
                    Text = "🟢 No text or audio differences found for this file compared to the backup snapshot.",
                    Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleCenter,
                    Font = new Font(FontFamily.GenericSansSerif, 9.5f, FontStyle.Italic),
                    ForeColor = Color.DarkGreen
                };
                tabPg.Controls.Add(lblNoDiff);
                tabPg.Controls.Add(pnlTabHead);
            }
            else
            {
                tabPg.Controls.Add(gridDiff);
                tabPg.Controls.Add(pnlTabHead);
            }
        }

        #endregion

        private void menuBatchRename_Click(object sender, EventArgs e)
        {
            var keys = _session.BuildMasterKeyList(false).Select(r => r.KeyName).ToList();
            using (var dlg = new BatchRenameDialog(keys, _appConfig))
            {
                if (dlg.ShowDialog() == DialogResult.OK)
                {
                    int renamed = 0;
                    foreach (var sDoc in _session.Documents)
                    {
                        foreach (var lbl in sDoc.Document.Labels)
                        {
                            if (dlg.RenameMapping.TryGetValue(lbl.Name, out var newName))
                            {
                                lbl.Name = newName;
                                sDoc.IsModified = true;
                                renamed++;
                            }
                        }
                    }
                    RebuildCategoryTreeAndGrid();
                    MessageBox.Show($"Renamed {renamed} keys.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
        }

        private void menuTrimSpaces_Click(object sender, EventArgs e)
        {
            int trimmed = 0;
            foreach (var sDoc in _session.Documents)
            {
                foreach (var lbl in sDoc.Document.Labels)
                {
                    foreach (var str in lbl.Strings)
                    {
                        if (str.Value != null)
                        {
                            string oldVal = str.Value;
                            str.Value = str.Value.Trim();
                            if (oldVal != str.Value)
                            {
                                trimmed++;
                                sDoc.IsModified = true;
                            }
                        }
                    }
                }
            }
            RebuildCategoryTreeAndGrid();
            MessageBox.Show($"Trimmed leading/trailing spaces in {trimmed} strings.", "Trim Tool", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void menuRenameFileLabel_Click(object sender, EventArgs e)
        {
            var targetDoc = _session?.BaseDocument ?? _session?.Documents.FirstOrDefault();
            if (targetDoc == null)
            {
                MessageBox.Show("No CSF file is currently open.", "Rename File Label", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            PromptRenameFileLabel(targetDoc);
        }

        private void menuChangeHeaderLangId_Click(object sender, EventArgs e)
        {
            if (_session == null || _session.Documents == null || _session.Documents.Count == 0) return;

            // Single-doc: act directly. Multi-doc: the submenu items handle each doc.
            if (_session.Documents.Count == 1)
            {
                PromptChangeHeaderLanguage(_session.Documents[0]);
            }
        }

        private void PopulateChangeHeaderLangIdSubmenu()
        {
            if (menuChangeHeaderLangId == null) return;

            menuChangeHeaderLangId.Click -= menuChangeHeaderLangId_Click;
            menuChangeHeaderLangId.DropDownItems.Clear();

            if (_session == null || _session.Documents == null || _session.Documents.Count == 0)
            {
                menuChangeHeaderLangId.Enabled = false;
                return;
            }

            menuChangeHeaderLangId.Enabled = true;

            if (_session.Documents.Count == 1)
            {
                // Single doc: direct click fires the handler
                menuChangeHeaderLangId.Click += menuChangeHeaderLangId_Click;
            }
            else
            {
                // Multi-doc: one sub-item per document showing current header lang
                foreach (var doc in _session.Documents)
                {
                    string fileName = doc.FileName ?? (string.IsNullOrEmpty(doc.FilePath)
                        ? $"Document [{doc.LanguageTag}]"
                        : Path.GetFileName(doc.FilePath));
                    string currHeader = doc.Document != null
                        ? $"{doc.Document.Language} ({(int)doc.Document.Language})"
                        : "Unknown";
                    string itemText = $"📄 {fileName} [{doc.LanguageTag}] (Header: {currHeader})";

                    var subItem = new ToolStripMenuItem(itemText);
                    CsfSessionDocument targetDoc = doc;
                    subItem.Click += (s, ev) => PromptChangeHeaderLanguage(targetDoc);
                    menuChangeHeaderLangId.DropDownItems.Add(subItem);
                }
            }
        }

        private void menuSortBinary_Click(object sender, EventArgs e)
        {
            ReorderKeysByBaseSequence();
        }

        private void ReorderKeysByBaseSequence()
        {
            var baseDoc = _session.BaseDocument ?? _session.Documents.FirstOrDefault();
            if (baseDoc == null || baseDoc.Document == null)
            {
                MessageBox.Show("No active session or base reference file loaded.", "Reorder Keys Tool", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var baseSequence = baseDoc.Document.Labels.Select(l => l.Name).ToList();
            if (baseSequence.Count == 0)
            {
                MessageBox.Show("The base reference file contains no keys to sort by.", "Reorder Keys Tool", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var indexMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < baseSequence.Count; i++)
            {
                if (!indexMap.ContainsKey(baseSequence[i]))
                {
                    indexMap[baseSequence[i]] = i;
                }
            }

            int totalFilesReordered = 0;
            foreach (var sDoc in _session.Documents)
            {
                if (sDoc.Document == null || sDoc.Document.Labels.Count <= 1) continue;

                sDoc.Document.Labels = sDoc.Document.Labels
                    .OrderBy(l => indexMap.TryGetValue(l.Name, out int idx) ? idx : int.MaxValue)
                    .ThenBy(l => l.Name, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                sDoc.IsModified = true;
                totalFilesReordered++;
            }

            OnSessionUpdated();
            RebuildCategoryTreeAndGrid();
            MessageBox.Show($"Reordered physical key sequence in {totalFilesReordered} open file(s) matching the reference sequence of [{baseDoc.LanguageTag}] ({baseDoc.FileName}).",
                "Reorder Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void menuClearValuesKeepKeys_Click(object sender, EventArgs e)
        {
            if (_session.Documents.Count == 0)
            {
                MessageBox.Show("No CSF files are currently open in the session.", "Clear Values & Audio", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            CsfSessionDocument targetDoc = null;

            if (_session.Documents.Count == 1)
            {
                targetDoc = _session.Documents[0];
            }
            else
            {
                // Multi-document session: prompt user to select which file to clear, or ALL files
                using (var dlg = new Form())
                {
                    dlg.Text = "Select Target CSF File to Clear";
                    dlg.Size = new System.Drawing.Size(420, 190);
                    dlg.StartPosition = FormStartPosition.CenterParent;
                    dlg.FormBorderStyle = FormBorderStyle.FixedDialog;
                    dlg.MaximizeBox = false;
                    dlg.MinimizeBox = false;
                    dlg.ShowIcon = false;

                    var lblPrompt = new Label
                    {
                        Text = "Select the open CSF document from which to erase text values and audio references:",
                        Location = new System.Drawing.Point(15, 15),
                        Size = new System.Drawing.Size(375, 35),
                        Font = new System.Drawing.Font("Segoe UI", 9F)
                    };

                    var cmbDocs = new ComboBox
                    {
                        DropDownStyle = ComboBoxStyle.DropDownList,
                        Location = new System.Drawing.Point(15, 55),
                        Size = new System.Drawing.Size(375, 23),
                        Font = new System.Drawing.Font("Segoe UI", 9F)
                    };

                    foreach (var doc in _session.Documents)
                    {
                        string isMain = doc == _session.BaseDocument ? " 📌 [BASE]" : "";
                        cmbDocs.Items.Add($"[{doc.LanguageTag}]{isMain} - {Path.GetFileName(doc.FilePath)} ({doc.Document.Labels.Count:N0} keys)");
                    }
                    cmbDocs.Items.Add("⚠️ [ALL OPEN FILES IN SESSION]");
                    cmbDocs.SelectedIndex = 0;

                    var btnOk = new Button
                    {
                        Text = "Select & Continue",
                        DialogResult = DialogResult.OK,
                        Location = new System.Drawing.Point(155, 105),
                        Size = new System.Drawing.Size(130, 28),
                        Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold)
                    };

                    var btnCancel = new Button
                    {
                        Text = "Cancel",
                        DialogResult = DialogResult.Cancel,
                        Location = new System.Drawing.Point(295, 105),
                        Size = new System.Drawing.Size(95, 28)
                    };

                    dlg.Controls.Add(lblPrompt);
                    dlg.Controls.Add(cmbDocs);
                    dlg.Controls.Add(btnOk);
                    dlg.Controls.Add(btnCancel);
                    dlg.AcceptButton = btnOk;
                    dlg.CancelButton = btnCancel;

                    if (dlg.ShowDialog(this) != DialogResult.OK) return;

                    if (cmbDocs.SelectedIndex < _session.Documents.Count)
                    {
                        targetDoc = _session.Documents[cmbDocs.SelectedIndex];
                    }
                }
            }

            // Destructive Confirmation Dialog
            string targetDesc = targetDoc != null
                ? $"file [{targetDoc.LanguageTag}] ({Path.GetFileName(targetDoc.FilePath)})"
                : "ALL OPEN CSF FILES IN THE ACTIVE SESSION";

            int affectedKeys = targetDoc != null
                ? targetDoc.Document.Labels.Count
                : _session.Documents.Sum(d => d.Document.Labels.Count);

            var confirm = MessageBox.Show(
                $"⚠️ DESTRUCTIVE ACTION WARNING!\n\n" +
                $"You are about to erase all text values and audio references from:\n" +
                $"• Target: {targetDesc}\n" +
                $"• Affected Keys: {affectedKeys:N0}\n\n" +
                $"Actions that will be executed against the target file:\n" +
                $"1. All string text values will be emptied (Value = \"\").\n" +
                $"2. All audio WAV references (Sound) will be removed.\n" +
                $"3. Only the key label structure (Label Names) will be preserved.\n\n" +
                $"Do you really want to clear all values from {targetDesc}?",
                "Confirm Clear Text & Audio Values",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2);

            if (confirm != DialogResult.Yes) return;

            // Execute clear operation
            if (targetDoc != null)
            {
                ClearDocValuesAndAudio(targetDoc);
            }
            else
            {
                foreach (var doc in _session.Documents)
                {
                    ClearDocValuesAndAudio(doc);
                }
            }

            OnSessionUpdated();
            RebuildCategoryTreeAndGrid();

            MessageBox.Show($"Text values and audio references successfully cleared from {targetDesc}.\nThe key structure has been preserved.",
                "Operation Completed", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void ClearDocValuesAndAudio(CsfSessionDocument sDoc)
        {
            if (sDoc?.Document == null) return;
            foreach (var lbl in sDoc.Document.Labels)
            {
                lbl.Strings.Clear();
                lbl.Strings.Add(new CsfStringEntry(string.Empty, null));
            }
            sDoc.IsModified = true;
        }

        private Dictionary<string, DateTime> _recentKeyTimestamps = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
        private bool _isUpdatingRecentGrid = false;

        private void PopulateRecentGrid()
        {
            if (gridRecent == null || _isUpdatingRecentGrid || _session == null) return;
            bool prevSync = _isSyncingSelection;
            _isSyncingSelection = true;
            try
            {
                _isUpdatingRecentGrid = true;
                gridRecent.SuspendLayout();
                gridRecent.Rows.Clear();

                var masterMap = GetMasterRowsMap();

                foreach (var kvp in _recentKeyTimestamps.OrderByDescending(k => k.Value))
                {
                    masterMap.TryGetValue(kvp.Key, out var mRow);
                    string cat = mRow?.Category ?? (kvp.Key.Contains(":") ? kvp.Key.Substring(0, kvp.Key.IndexOf(':') + 1) : "Uncategorized");

                    int idx = gridRecent.Rows.Add(kvp.Key, cat, kvp.Value.ToString("yyyy-MM-dd HH:mm:ss"));
                    gridRecent.Rows[idx].Tag = mRow;
                }
            }
            catch { }
            finally
            {
                gridRecent.ResumeLayout(true);
                _isUpdatingRecentGrid = false;
                _isSyncingSelection = prevSync;
            }
        }

        private void AddRecentEditedKey(string keyName) => MarkKeyAsModified(null, keyName);

        private void MarkKeyAsCreated(string keyName)
        {
            if (string.IsNullOrEmpty(keyName)) return;

            _addedKeyNames.Add(keyName);
            _modifiedKeyNames.Remove(keyName);
            _deletedKeyNames.Remove(keyName);
            _recentKeyTimestamps[keyName] = DateTime.Now;
            _unsavedDirty = true;
            _coverageDirty = true;
            _recentDirty = true;

            if (_isSyncingSelection) return;

            if (gridLabels != null && !gridLabels.IsDisposed)
            {
                gridLabels.Invalidate();
            }
            if (gridRecent != null && gridRecent.IsHandleCreated)
            {
                gridRecent.BeginInvoke((Action)PopulateRecentGrid);
            }
            if (tabControlMain?.SelectedTab == tabCoverage)
            {
                PopulateCoverageMatrixTab();
                _coverageDirty = false;
            }
            UpdateUIForSessionMode();
        }

        private void MarkKeyAsDeleted(string keyName)
        {
            if (string.IsNullOrEmpty(keyName)) return;

            if (_addedKeyNames.Contains(keyName))
            {
                _addedKeyNames.Remove(keyName);
                _modifiedKeyNames.Remove(keyName);
                _recentKeyTimestamps.Remove(keyName);
            }
            else
            {
                _modifiedKeyNames.Remove(keyName);
                _deletedKeyNames.Add(keyName);
                _recentKeyTimestamps[keyName] = DateTime.Now;
            }

            _unsavedDirty = true;
            _coverageDirty = true;
            _recentDirty = true;

            if (_isSyncingSelection) return;

            if (gridLabels != null && !gridLabels.IsDisposed)
            {
                gridLabels.Invalidate();
            }
            if (gridRecent != null && gridRecent.IsHandleCreated)
            {
                gridRecent.BeginInvoke((Action)PopulateRecentGrid);
            }
            if (tabControlMain?.SelectedTab == tabCoverage)
            {
                PopulateCoverageMatrixTab();
                _coverageDirty = false;
            }
            UpdateUIForSessionMode();
        }

        private void MarkKeyAsRenamed(string oldKey, string newKey)
        {
            if (string.IsNullOrEmpty(oldKey) || string.IsNullOrEmpty(newKey)) return;

            if (_addedKeyNames.Contains(oldKey))
            {
                _addedKeyNames.Remove(oldKey);
                _addedKeyNames.Add(newKey);
            }
            if (_modifiedKeyNames.Contains(oldKey))
            {
                _modifiedKeyNames.Remove(oldKey);
                _modifiedKeyNames.Add(newKey);
            }
            if (_deletedKeyNames.Contains(oldKey))
            {
                _deletedKeyNames.Remove(oldKey);
                _deletedKeyNames.Add(newKey);
            }
            if (_recentKeyTimestamps.TryGetValue(oldKey, out var dt))
            {
                _recentKeyTimestamps.Remove(oldKey);
                _recentKeyTimestamps[newKey] = dt;
            }
            _modifiedKeyMap.RemoveWhere(k => k.EndsWith($":{oldKey}", StringComparison.OrdinalIgnoreCase));
        }

        private bool IsKeyUnsaved(string keyName)
        {
            if (string.IsNullOrEmpty(keyName)) return false;
            return (_addedKeyNames != null && _addedKeyNames.Contains(keyName)) ||
                   (_modifiedKeyNames != null && _modifiedKeyNames.Contains(keyName)) ||
                   (_deletedKeyNames != null && _deletedKeyNames.Contains(keyName));
        }

        private void SyncStateWithSession()
        {
            var masterRows = _session.BuildMasterKeyList();
            var currentKeys = new HashSet<string>(masterRows.Select(r => r.KeyName), StringComparer.OrdinalIgnoreCase);

            _deletedKeyNames.RemoveWhere(k => currentKeys.Contains(k));
            _addedKeyNames.RemoveWhere(k => !currentKeys.Contains(k));
            _modifiedKeyNames.RemoveWhere(k => !currentKeys.Contains(k));
        }

        private void MarkKeyAsModified(string langTag, string keyName)
        {
            if (string.IsNullOrEmpty(keyName)) return;

            if (!string.IsNullOrEmpty(langTag))
            {
                _modifiedKeyMap.Add($"{langTag}:{keyName}");
            }
            if (!_addedKeyNames.Contains(keyName) && !_deletedKeyNames.Contains(keyName))
            {
                _modifiedKeyNames.Add(keyName);
            }
            _recentKeyTimestamps[keyName] = DateTime.Now;
            _unsavedDirty = true;
            _coverageDirty = true;

            if (_isSyncingSelection) return;

            if (gridLabels != null && !gridLabels.IsDisposed)
            {
                gridLabels.Invalidate();
            }

            if (gridRecent != null && gridRecent.IsHandleCreated)
            {
                gridRecent.BeginInvoke((Action)PopulateRecentGrid);
            }

            if (tabControlMain?.SelectedTab == tabCoverage)
            {
                PopulateCoverageMatrixTab();
                _coverageDirty = false;
            }

            UpdateUIForSessionMode();
        }

        private bool IsKeyModifiedInDoc(string langTag, string keyName)
        {
            if (string.IsNullOrEmpty(langTag) || string.IsNullOrEmpty(keyName)) return false;
            return _modifiedKeyMap != null && _modifiedKeyMap.Contains($"{langTag}:{keyName}");
        }

        private void menuNew_Click(object sender, EventArgs e) => NewSingleDocument();
        private void menuOpen_Click(object sender, EventArgs e) => OpenSingleDocument();
        private void menuSave_Click(object sender, EventArgs e) => SaveAllDocuments(false);
        private void menuSaveAs_Click(object sender, EventArgs e) => SaveAllDocuments(true);
        private void menuExit_Click(object sender, EventArgs e) => Close();

        private void InitializeControlToolTips()
        {
            // --- TOP MENUS ---
            ToolTipHelper.SetToolTip(menuNew, "New CSF: Create a new session with a blank CSF string table file.");
            ToolTipHelper.SetToolTip(menuOpen, "Open CSF File: Load an existing Command & Conquer .CSF string table file from your computer.");
            ToolTipHelper.SetToolTip(menuOpenSession, "Open Session: Open a saved project session containing multiple associated CSF text tables.");
            ToolTipHelper.SetToolTip(menuRecentSessions, "Recent Sessions: Access recently opened CSF projects and session files.");
            ToolTipHelper.SetToolTip(menuSave, "Save: Save all unsaved text and extra audio modifications to disk for open CSF files.");
            ToolTipHelper.SetToolTip(menuSaveSingleFile, "Save Single File: Save changes for the currently active CSF file only.");
            ToolTipHelper.SetToolTip(menuSaveAs, "Save As: Save the currently selected CSF file under a new filename or location.");
            ToolTipHelper.SetToolTip(menuExportTxt, "Export to TXT: Export string entries to a plain text UTF-8 (.TXT) file readable in Notepad or external editors.");
            ToolTipHelper.SetToolTip(menuExportKeysOnly, "Export Key Structure Only: Export only the list of key label names without any text content to a plain text UTF-8 (.TXT) file.");
            ToolTipHelper.SetToolTip(menuImportTxt, "Import from TXT: Import string entries from a UTF-8 text file to update matching key entries in open CSFs.");
            ToolTipHelper.SetToolTip(menuExit, "Exit Application: Close and exit the CSF Studio editor.");

            ToolTipHelper.SetToolTip(menuAddLabel, "Add New Key: Create a new string key entry slot across all open CSF files in the active session.");
            ToolTipHelper.SetToolTip(menuDeleteLabel, "Delete Key: Permanently remove selected key entries from open CSF files.");
            ToolTipHelper.SetToolTip(menuBatchRename, "Batch Rename: Rename multiple key prefixes or replace text patterns in key names simultaneously across all open CSFs.");
            ToolTipHelper.SetToolTip(menuTrimSpaces, "Trim Spaces: Automatically trim leading and trailing spaces from text string values in all open files.");
            ToolTipHelper.SetToolTip(menuRenameFileLabel, "Rename File Label: Edit the display title or language label assigned to the active CSF file.");
            ToolTipHelper.SetToolTip(menuChangeHeaderLangId, "Change Header Language ID: Modify the 32-bit binary language ID stored at offset 0x14 in the CSF binary header.");
            ToolTipHelper.SetToolTip(menuJumpNextEmpty, "Jump to Next Empty Key: Move selection to the next key entry with empty or missing text content (Ctrl+Shift+Down).");
            ToolTipHelper.SetToolTip(menuJumpPrevEmpty, "Jump to Previous Empty Key: Move selection to the previous key entry with empty or missing text content (Ctrl+Shift+Up).");
            ToolTipHelper.SetToolTip(menuFindReplace, "Find & Replace: Search for specific text patterns or key names and replace them across open files.");

            ToolTipHelper.SetToolTip(menuScanIni, "Scan INI & MAP Files: Scan C&C game .INI configuration files or map files (.MAP) to detect missing CSF string references.");
            ToolTipHelper.SetToolTip(menuConvertAnsi, "Convert ANSI / Codepage Text to Unicode: Convert raw ANSI codepage text entries across open CSF files to standard Unicode characters.");
            ToolTipHelper.SetToolTip(menuSortBinary, "Reorder Keys by Main CSF File Sequence: Physically reorder key entries in all open CSF documents matching the exact sequence of the Main CSF file.");
            ToolTipHelper.SetToolTip(menuClearValuesKeepKeys, "Clear Text & Audio: Erase all string text values and audio references from a chosen CSF document while preserving the key structure.");
            ToolTipHelper.SetToolTip(menuAbout, "About CSF Studio: Display application version, format specification details, and credits.");

            // --- TOOLBAR BUTTONS & CONTROLS ---
            ToolTipHelper.SetToolTip(btnAddKeyToolbar, "Add Key: Create a new string entry slot across all open CSF files");
            ToolTipHelper.SetToolTip(btnDuplicateKeyToolbar, "Duplicate Key: Clone the selected key(s) with a new suffix");
            ToolTipHelper.SetToolTip(btnDeleteKeyToolbar, "Delete Key: Remove selected key entries from open CSF files");
            ToolTipHelper.SetToolTip(btnJumpPrevEmptyToolbar, "Jump to Previous Empty Key: Move selection to previous key with empty or missing text (Ctrl+Shift+Up)");
            ToolTipHelper.SetToolTip(btnJumpNextEmptyToolbar, "Jump to Next Empty Key: Move selection to next key with empty or missing text (Ctrl+Shift+Down)");
            btnFilterLogic.Text = "OR";
            ToolTipHelper.SetToolTip(btnFilterLogic, "Filter Combination (OR / AND): Toggle whether searching requires EITHER key OR text filter (OR default), or BOTH key AND text filters (AND)");
            ToolTipHelper.SetToolTip(cboStatusFilter, "Filter Grid Rows by Key Status:\n🎯 All Statuses (Show all string table entries)\n🔴 Missing Keys Only (Key missing in one or more open CSF files)\n🟡 Empty Strings Only (Key exists, but string text is blank)\n🟢 Complete Keys Only (Key exists in all open CSF files with valid text)");

            toolStrip1.GripStyle = ToolStripGripStyle.Hidden;
            toolStrip1.ShowItemToolTips = true;
            toolStrip2.GripStyle = ToolStripGripStyle.Hidden;
            toolStrip2.ShowItemToolTips = true;

            // --- LOAD CONFIG & INITIALIZE FILTER HISTORIES ---
            _appConfig = ConfigManager.LoadConfig();
            _keyRegexMode = _appConfig.KeyRegexMode;
            _valRegexMode = _appConfig.ValRegexMode;
            _filterLogicAnd = _appConfig.FilterLogicAnd;
            _sortByBinarySequence = _appConfig.SortByBinarySequence;

            if (_appConfig.IsMaximized)
            {
                this.WindowState = FormWindowState.Maximized;
            }
            else if (_appConfig.Width >= 600 && _appConfig.Height >= 400)
            {
                this.Width = _appConfig.Width;
                this.Height = _appConfig.Height;
            }

            if (cboStatusFilter != null)
            {
                if (cboStatusFilter.ComboBox != null)
                {
                    var combo = cboStatusFilter.ComboBox;
                    combo.DrawMode = DrawMode.OwnerDrawFixed;
                    combo.ItemHeight = 20;

                    combo.DrawItem += (s, e) =>
                    {
                        if (e.Index < 0) return;

                        bool isSelected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
                        Color backColor = isSelected ? SystemColors.Highlight : combo.BackColor;
                        Color textColor = isSelected ? SystemColors.HighlightText : combo.ForeColor;

                        using (var bgBrush = new SolidBrush(backColor))
                        {
                            e.Graphics.FillRectangle(bgBrush, e.Bounds);
                        }

                        string rawItem = combo.Items[e.Index]?.ToString() ?? "";
                        string cleanName = rawItem.Replace("🎯", "").Replace("🔴", "").Replace("🟡", "").Replace("🟢", "").Trim();

                        Rectangle sphereRect = new Rectangle(e.Bounds.X + 4, e.Bounds.Y + (e.Bounds.Height - 12) / 2, 12, 12);
                        DrawStatusSphere(e.Graphics, sphereRect, rawItem);

                        using (var textBrush = new SolidBrush(textColor))
                        {
                            var font = e.Font ?? combo.Font;
                            e.Graphics.DrawString(cleanName, font, textBrush, e.Bounds.X + 20, e.Bounds.Y + 2);
                        }

                        e.DrawFocusRectangle();
                    };
                }

                Timer _statusFilterTimer = new Timer { Interval = 50 };
                _statusFilterTimer.Tick += (ts, te) =>
                {
                    _statusFilterTimer.Stop();
                    if (_session != null) PopulateMasterGrid();
                };

                cboStatusFilter.SelectedIndexChanged += (s, e) =>
                {
                    _statusFilterTimer.Stop();
                    _statusFilterTimer.Start();
                };
                if (cboStatusFilter.ComboBox != null)
                {
                    cboStatusFilter.ComboBox.SelectionChangeCommitted += (s, e) =>
                    {
                        _statusFilterTimer.Stop();
                        _statusFilterTimer.Start();
                    };
                }

                if (cboStatusFilter.Items.Count > 0)
                {
                    int statusIdx = Math.Max(0, Math.Min(cboStatusFilter.Items.Count - 1, _appConfig.SelectedStatusFilterIndex));
                    cboStatusFilter.SelectedIndex = statusIdx;
                }
            }

            if (cboSearchKey != null)
            {
                var initialList = _keyRegexMode ? _appConfig.KeySearchHistoryRegex : _appConfig.KeySearchHistoryPlain;
                cboSearchKey.Items.Clear();
                foreach (var h in initialList) cboSearchKey.Items.Add(h);
                cboSearchKey.TextChanged += (s, e) => { _searchDebounceTimer.Stop(); _searchDebounceTimer.Start(); };
                cboSearchKey.SelectedIndexChanged += (s, e) => { _searchDebounceTimer.Stop(); PopulateMasterGrid(); };
                cboSearchKey.KeyDown += (s, e) =>
                {
                    if (e.KeyCode == Keys.Enter)
                    {
                        e.SuppressKeyPress = true;
                        _searchDebounceTimer.Stop();
                        string text = cboSearchKey.Text.Trim();
                        if (!string.IsNullOrEmpty(text))
                        {
                            var targetList = _keyRegexMode ? _appConfig.KeySearchHistoryRegex : _appConfig.KeySearchHistoryPlain;
                            ConfigManager.AddHistoryItem(targetList, text, _appConfig.MaxSearchHistoryItems);
                            RefreshSearchComboItems(cboSearchKey, targetList, text);
                            ConfigManager.SaveConfig(_appConfig);
                        }
                        PopulateMasterGrid();
                    }
                };
            }

            if (cboSearchValue != null)
            {
                var initialList = _valRegexMode ? _appConfig.ValueSearchHistoryRegex : _appConfig.ValueSearchHistoryPlain;
                cboSearchValue.Items.Clear();
                foreach (var h in initialList) cboSearchValue.Items.Add(h);
                cboSearchValue.TextChanged += (s, e) => { _searchDebounceTimer.Stop(); _searchDebounceTimer.Start(); };
                cboSearchValue.SelectedIndexChanged += (s, e) => { _searchDebounceTimer.Stop(); PopulateMasterGrid(); };
                cboSearchValue.KeyDown += (s, e) =>
                {
                    if (e.KeyCode == Keys.Enter)
                    {
                        e.SuppressKeyPress = true;
                        _searchDebounceTimer.Stop();
                        string text = cboSearchValue.Text.Trim();
                        if (!string.IsNullOrEmpty(text))
                        {
                            var targetList = _valRegexMode ? _appConfig.ValueSearchHistoryRegex : _appConfig.ValueSearchHistoryPlain;
                            ConfigManager.AddHistoryItem(targetList, text, _appConfig.MaxSearchHistoryItems);
                            RefreshSearchComboItems(cboSearchValue, targetList, text);
                            ConfigManager.SaveConfig(_appConfig);
                        }
                        PopulateMasterGrid();
                    }
                };
            }

            UpdateFilterButtonsUI();

            // --- DETAIL INSPECTOR & EDITORS ---
            ToolTipHelper.SetToolTip(_toolTip, lblCurrentKey, "Selected C&C CSF Label / Key Name");
            ToolTipHelper.SetToolTip(_toolTip, txtCurrentKeyName, "Key Name Field: Type a new key name and click Apply Rename (or press Enter) to update across all loaded CSFs");
            ToolTipHelper.SetToolTip(_toolTip, btnApplyRename, "Apply Rename: Update this key name across all open CSF files (checks for duplicate names)");
            ToolTipHelper.SetToolTip(_toolTip, tvCategories, "Category Tree: Click any category prefix (e.g. GUI, MISSION, No category) to filter keys by prefix");

            if (lblCurrentWav != null) lblCurrentWav.Visible = false;
            if (txtCurrentExtraWav != null) txtCurrentExtraWav.Visible = false;

            txtCurrentKeyName.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter)
                {
                    e.SuppressKeyPress = true;
                    btnApplyRename_Click(btnApplyRename, EventArgs.Empty);
                }
            };
        }

        private void btnApplyRename_Click(object sender, EventArgs e)
        {
            if (gridLabels.SelectedRows.Count == 0) return;
            var row = gridLabels.SelectedRows[0].Tag as MasterKeyRow;
            if (row == null) return;

            string oldKey = row.KeyName;
            string newKey = txtCurrentKeyName.Text.Trim();

            if (string.IsNullOrWhiteSpace(newKey))
            {
                MessageBox.Show("Key name cannot be empty or blank.", "Invalid Key Name", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtCurrentKeyName.Text = oldKey;
                return;
            }

            if (string.Equals(oldKey, newKey, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            // Duplicate check!
            var masterKeys = _session.BuildMasterKeyList();
            if (masterKeys.Any(k => string.Equals(k.KeyName, newKey, StringComparison.OrdinalIgnoreCase)))
            {
                MessageBox.Show($"⚠️ Cannot rename key:\n\nA key named '{newKey}' already exists in this session!\nKey names must be unique.",
                    "Duplicate Key Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                txtCurrentKeyName.Text = oldKey;
                txtCurrentKeyName.Focus();
                txtCurrentKeyName.SelectAll();
                return;
            }

            foreach (var sDoc in _session.Documents)
            {
                var label = sDoc.Document.Labels.FirstOrDefault(l => string.Equals(l.Name, oldKey, StringComparison.OrdinalIgnoreCase));
                if (label != null)
                {
                    label.Name = newKey;
                    sDoc.IsModified = true;
                }
            }

            _undoManager.Execute(new RenameKeyCommand(oldKey, newKey), _session);
            MarkKeyAsRenamed(oldKey, newKey);
            UpdateFormTitle();
            EnsureKeyVisibleAndSelected(newKey);
        }

        public void JumpToKeyAndLanguage(string keyName, string langTag = null)
        {
            if (string.IsNullOrEmpty(keyName)) return;

            // 1. Ensure tabMaster is active
            if (tabControlMain.SelectedTab != tabMaster)
            {
                tabControlMain.SelectedTab = tabMaster;
            }

            // 2. If langTag is specified and cboFileFilter exists, check if we need to adjust filter
            if (!string.IsNullOrEmpty(langTag) && cboFileFilter != null && cboFileFilter.Items.Count > 0)
            {
                if (cboFileFilter.SelectedItem is DocumentFilterOption opt)
                {
                    if (!string.Equals(opt.Document?.LanguageTag, langTag, StringComparison.OrdinalIgnoreCase))
                    {
                        cboFileFilter.SelectedIndex = 0; // Switch to All Files view
                    }
                }
            }

            EnsureKeyVisibleAndSelected(keyName);
        }

        private void UpdateUndoRedoMenuItems()
        {
            if (menuUndo != null)
            {
                menuUndo.Enabled = _undoManager.CanUndo;
                menuUndo.Text = _undoManager.CanUndo
                    ? $"↩️ Undo {_undoManager.UndoDescription} (Ctrl+Z)"
                    : "↩️ Undo (Ctrl+Z)";
            }

            if (menuRedo != null)
            {
                menuRedo.Enabled = _undoManager.CanRedo;
                menuRedo.Text = _undoManager.CanRedo
                    ? $"↪️ Redo {_undoManager.RedoDescription} (Ctrl+Y)"
                    : "↪️ Redo (Ctrl+Y)";
            }
        }

        private void menuUndo_Click(object sender, EventArgs e)
        {
            try
            {
                var cmd = _undoManager.PerformUndo(_session);
                if (cmd != null)
                {
                    SyncStateWithSession();
                    JumpToKeyAndLanguage(cmd.TargetKeyName, cmd.TargetLanguageTag);
                    UpdateUIForSessionMode();
                    RebuildCategoryTreeAndGrid();
                    UpdateUndoRedoMenuItems();
                    ShowSaveNotification($"↩️ Undid: {cmd.Description}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Undo Exception Handled: {ex.Message}");
            }
        }

        private void menuRedo_Click(object sender, EventArgs e)
        {
            try
            {
                var cmd = _undoManager.PerformRedo(_session);
                if (cmd != null)
                {
                    SyncStateWithSession();
                    JumpToKeyAndLanguage(cmd.TargetKeyName, cmd.TargetLanguageTag);
                    UpdateUIForSessionMode();
                    RebuildCategoryTreeAndGrid();
                    UpdateUndoRedoMenuItems();
                    ShowSaveNotification($"↪️ Redid: {cmd.Description}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Redo Exception Handled: {ex.Message}");
            }
        }

        private void menuCut_Click(object sender, EventArgs e)
        {
            if (ActiveControl is TextBox txt && !string.IsNullOrEmpty(txt.SelectedText))
            {
                txt.Cut();
            }
            else if (GetActiveGridForTab(tabControlMain.SelectedTab) is DataGridView grid && grid.SelectedCells.Count > 0)
            {
                menuCopy_Click(sender, e);
            }
        }

        private void menuCopy_Click(object sender, EventArgs e)
        {
            if (ActiveControl is TextBox txt && !string.IsNullOrEmpty(txt.SelectedText))
            {
                txt.Copy();
            }
            else
            {
                var selectedKeys = GetCurrentlySelectedKeyNames();
                if (selectedKeys != null && selectedKeys.Count > 0)
                {
                    Clipboard.SetText(string.Join(Environment.NewLine, selectedKeys));
                    ShowSaveNotification($"📋 Copied {selectedKeys.Count} key name(s) to clipboard");
                }
            }
        }

        private void menuPaste_Click(object sender, EventArgs e)
        {
            if (ActiveControl is TextBox txt && txt.CanFocus)
            {
                txt.Paste();
            }
        }

        private void EnsureKeyVisibleAndSelected(string keyName)
        {
            if (string.IsNullOrEmpty(keyName)) return;

            int colonIdx = keyName.IndexOf(':');
            string newCategory = colonIdx > 0 ? keyName.Substring(0, colonIdx + 1) : "Uncategorized";

            if (_selectedCategory != "[All Labels]" && !string.Equals(_selectedCategory, newCategory, StringComparison.OrdinalIgnoreCase))
            {
                _selectedCategory = "[All Labels]";
            }

            RebuildCategoryTreeAndGrid();
            SelectCategoryNodeInTree(_selectedCategory);
            SelectKeyInGrid(keyName);
        }

        private void SelectCategoryNodeInTree(string category)
        {
            foreach (TreeNode node in tvCategories.Nodes)
            {
                if (string.Equals(node.Tag as string, category, StringComparison.OrdinalIgnoreCase))
                {
                    tvCategories.SelectedNode = node;
                    return;
                }
                foreach (TreeNode child in node.Nodes)
                {
                    if (string.Equals(child.Tag as string, category, StringComparison.OrdinalIgnoreCase))
                    {
                        tvCategories.SelectedNode = child;
                        return;
                    }
                }
            }
        }

        private void SelectKeyInGrid(string keyName)
        {
            CompleteMasterRowStreamNow();
            if (gridLabels == null || gridLabels.IsDisposed) return;

            gridLabels.ClearSelection();
            foreach (DataGridViewRow r in gridLabels.Rows)
            {
                var row = r.Tag as MasterKeyRow;
                if (row != null && string.Equals(row.KeyName, keyName, StringComparison.OrdinalIgnoreCase))
                {
                    bool showIndexCol = (gridLabels.Columns.Count > 0 && gridLabels.Columns[0].HeaderText == "#");
                    int keyColIdx = showIndexCol ? 2 : 1;
                    if (keyColIdx < r.Cells.Count)
                    {
                        try
                        {
                            gridLabels.CurrentCell = r.Cells[keyColIdx];
                        }
                        catch { }
                    }
                    r.Selected = true;
                    try
                    {
                        gridLabels.FirstDisplayedScrollingRowIndex = Math.Max(0, r.Index);
                    }
                    catch { }
                    gridLabels_SelectionChanged(gridLabels, EventArgs.Empty);
                    break;
                }
            }
        }

        private void menuAddLabel_Click(object sender, EventArgs e)
        {
            var masterKeys = _session.BuildMasterKeyList();
            int candidateNumber = masterKeys.Count + 1;
            string prefix = string.IsNullOrWhiteSpace(_appConfig?.DefaultCategoryPrefix) ? "CSF_" : _appConfig.DefaultCategoryPrefix.Trim().ToUpperInvariant();
            if (!prefix.EndsWith("_") && !prefix.EndsWith(":")) prefix += "_";
            string keyName = $"{prefix}New_Key_{candidateNumber}";

            // Ensure keyName is unique and does not collide with any existing keys
            while (masterKeys.Any(k => string.Equals(k.KeyName, keyName, StringComparison.OrdinalIgnoreCase)))
            {
                candidateNumber++;
                keyName = $"{prefix}New_Key_{candidateNumber}";
            }

            if (_session.BaseDocument != null)
            {
                _session.BaseDocument.Document.Labels.Add(new CsfLabel(keyName, string.Empty));
                _session.BaseDocument.IsModified = true;

                _undoManager.Execute(new AddKeyCommand(keyName, _session.BaseDocument.LanguageTag), _session);
                UpdateUndoRedoMenuItems();
                MarkKeyAsCreated(keyName);

                // Ensure category tree updates and new key is visible and selected!
                EnsureKeyVisibleAndSelected(keyName);
                txtCurrentKeyName.Focus();
                txtCurrentKeyName.SelectAll();
            }
        }

        private void menuDeleteLabel_Click(object sender, EventArgs e)
        {
            var selectedKeyNames = GetCurrentlySelectedKeyNames();
            if (selectedKeyNames == null || selectedKeyNames.Count == 0) return;

            var masterMap = GetMasterRowsMap();
            var keysToDelete = new List<MasterKeyRow>();

            foreach (string keyName in selectedKeyNames)
            {
                if (masterMap.TryGetValue(keyName, out var mRow))
                {
                    keysToDelete.Add(mRow);
                }
                else
                {
                    var fallbackRow = new MasterKeyRow
                    {
                        KeyName = keyName,
                        Category = CsfSession.ExtractCategory(keyName)
                    };
                    foreach (var doc in _session.Documents)
                    {
                        var lbl = doc.Document.Labels.FirstOrDefault(l => string.Equals(l.Name, keyName, StringComparison.OrdinalIgnoreCase));
                        if (lbl != null && lbl.Strings.Count > 0)
                        {
                            fallbackRow.ValuesPerLanguage[doc.LanguageTag] = lbl.Strings[0];
                        }
                    }
                    keysToDelete.Add(fallbackRow);
                }
            }

            if (keysToDelete.Count == 0) return;

            using (var dlg = new ConfirmDeleteKeysDialog(keysToDelete, _session))
            {
                if (dlg.ShowDialog() == DialogResult.OK)
                {
                    var backupList = new List<DeleteKeyCommand.KeyDataBackup>();

                    foreach (var keyRow in keysToDelete)
                    {
                        MarkKeyAsDeleted(keyRow.KeyName);
                        var kBackup = new DeleteKeyCommand.KeyDataBackup { KeyName = keyRow.KeyName };
                        foreach (var sDoc in _session.Documents)
                        {
                            var lbl = sDoc.Document.Labels.FirstOrDefault(l => string.Equals(l.Name, keyRow.KeyName, StringComparison.OrdinalIgnoreCase));
                            if (lbl != null)
                            {
                                if (lbl.Strings.Count > 0)
                                {
                                    kBackup.ValuesPerLanguage[sDoc.LanguageTag] = (lbl.Strings[0].Value, lbl.Strings[0].ExtraValue);
                                }
                                sDoc.Document.Labels.Remove(lbl);
                                sDoc.IsModified = true;
                            }
                        }
                        backupList.Add(kBackup);
                    }

                    _undoManager.Execute(new DeleteKeyCommand(backupList), _session);
                    UpdateUndoRedoMenuItems();

                    var deletedKeySet = new HashSet<string>(keysToDelete.Select(k => k.KeyName), StringComparer.OrdinalIgnoreCase);
                    if (_lastActiveSelectedKeys != null)
                    {
                        _lastActiveSelectedKeys.RemoveAll(k => deletedKeySet.Contains(k));
                    }

                    ClearDetailInspector();
                    InvalidateMasterRowsCache();
                    RebuildCategoryTreeAndGrid();
                }
            }
        }

        private void menuFindReplace_Click(object sender, EventArgs e)
        {
            if (_findReplaceDlg == null || _findReplaceDlg.IsDisposed)
            {
                _findReplaceDlg = new FindReplaceDialog();
                _findReplaceDlg.OnFindNext += (s, args) => PerformFindReplace(false);
                _findReplaceDlg.OnReplaceAll += (s, args) => PerformFindReplace(true);
            }
            _findReplaceDlg.Show(this);
        }

        private void PerformFindReplace(bool replaceAll)
        {
            if (_findReplaceDlg == null) return;

            string find = _findReplaceDlg.FindText;
            string replace = _findReplaceDlg.ReplaceText;
            bool matchCase = _findReplaceDlg.MatchCase;
            bool useRegex = _findReplaceDlg.UseRegex;

            if (string.IsNullOrEmpty(find)) return;

            Regex regex = null;
            if (useRegex)
            {
                try { regex = new Regex(find, matchCase ? RegexOptions.None : RegexOptions.IgnoreCase); }
                catch (Exception ex) { MessageBox.Show($"Invalid RegEx syntax:\n{ex.Message}"); return; }
            }

            int count = 0;
            foreach (var sDoc in _session.Documents)
            {
                foreach (var lbl in sDoc.Document.Labels)
                {
                    if (_findReplaceDlg.SearchKey)
                    {
                        bool match = useRegex ? regex.IsMatch(lbl.Name) : (matchCase ? lbl.Name.Contains(find) : lbl.Name.IndexOf(find, StringComparison.OrdinalIgnoreCase) >= 0);
                        if (match && replaceAll)
                        {
                            lbl.Name = useRegex ? regex.Replace(lbl.Name, replace) : (matchCase ? lbl.Name.Replace(find, replace) : Regex.Replace(lbl.Name, find, replace, RegexOptions.IgnoreCase));
                            sDoc.IsModified = true;
                            count++;
                        }
                    }

                    if (_findReplaceDlg.SearchValue)
                    {
                        foreach (var str in lbl.Strings)
                        {
                            if (str.Value != null)
                            {
                                bool match = useRegex ? regex.IsMatch(str.Value) : (matchCase ? str.Value.Contains(find) : str.Value.IndexOf(find, StringComparison.OrdinalIgnoreCase) >= 0);
                                if (match && replaceAll)
                                {
                                    str.Value = useRegex ? regex.Replace(str.Value, replace) : (matchCase ? str.Value.Replace(find, replace) : Regex.Replace(str.Value, find, replace, RegexOptions.IgnoreCase));
                                    sDoc.IsModified = true;
                                    count++;
                                }
                            }
                        }
                    }
                }
            }

            RebuildCategoryTreeAndGrid();
            if (replaceAll)
            {
                MessageBox.Show($"Replaced {count} occurrences.", "Replace All", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void RefreshSearchComboItems(ToolStripComboBox cbo, List<string> history, string currentText)
        {
            if (cbo == null) return;
            cbo.Items.Clear();
            if (history != null)
            {
                foreach (var h in history) cbo.Items.Add(h);
            }
            cbo.Text = currentText ?? string.Empty;
        }

        private void menuOptions_Click(object sender, EventArgs e)
        {
            using (var dlg = new OptionsDialog(_appConfig))
            {
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    _appConfig = dlg.Config;
                    RefreshSearchComboItems(cboSearchKey, _keyRegexMode ? _appConfig.KeySearchHistoryRegex : _appConfig.KeySearchHistoryPlain, cboSearchKey?.Text);
                    RefreshSearchComboItems(cboSearchValue, _valRegexMode ? _appConfig.ValueSearchHistoryRegex : _appConfig.ValueSearchHistoryPlain, cboSearchValue?.Text);
                    ConfigManager.SaveConfig(_appConfig);
                    RefreshActiveSelectionInspector();
                    ShowSaveNotification("Application options saved successfully.");
                }
            }
        }

        private void menuGitHubRepo_Click(object sender, EventArgs e)
        {
            try
            {
                System.Diagnostics.Process.Start("https://github.com/FS-21/CSF-Studio");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not open browser link:\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void menuAbout_Click(object sender, EventArgs e)
        {
            using (var dlg = new AboutDialog())
            {
                dlg.ShowDialog(this);
            }
        }

        private void MainForm_Shown(object sender, EventArgs e)
        {
            if (_appConfig != null)
            {
                ConfigManager.SaveConfig(_appConfig);
            }
            if (_initialCommandLineFiles != null && _initialCommandLineFiles.Count > 0)
            {
                if (_initialCommandLineFiles.Count == 1)
                {
                    OpenSingleDocumentPath(_initialCommandLineFiles[0]);
                }
                else
                {
                    OpenMultipleDocumentsPaths(_initialCommandLineFiles);
                }
            }
            else
            {
                if (_appConfig != null)
                {
                    if (_appConfig.DefaultStartupMainTab == StartupMainTabOption.MasterKeysView)
                    {
                        tabControlMain.SelectedTab = tabMaster;
                    }
                    else if (_appConfig.DefaultStartupMainTab == StartupMainTabOption.PlainKeyEditor)
                    {
                        tabControlMain.SelectedTab = tabKeyEditor;
                    }
                    else if (_appConfig.DefaultStartupMainTab == StartupMainTabOption.RememberLastActive)
                    {
                        if (!string.IsNullOrEmpty(_appConfig.LastActiveMainTabName))
                        {
                            var savedTab = tabControlMain.TabPages.Cast<TabPage>()
                                .FirstOrDefault(t => string.Equals(t.Name, _appConfig.LastActiveMainTabName, StringComparison.OrdinalIgnoreCase));
                            if (savedTab != null && tabControlMain.TabPages.Contains(savedTab))
                            {
                                tabControlMain.SelectedTab = savedTab;
                            }
                        }
                    }
                }

                RestoreSessionViewStateFromConfig();
            }

            if (splitMain != null && _appConfig != null && _appConfig.MasterKeysViewPanelWidth >= 150)
            {
                splitMain.SplitterDistance = Math.Max(150, Math.Min(600, _appConfig.MasterKeysViewPanelWidth));
            }
        }

        private void InitFileDialogDirectory(FileDialog dlg)
        {
            if (dlg != null && _appConfig != null && !string.IsNullOrWhiteSpace(_appConfig.LastOpenDirectory) && Directory.Exists(_appConfig.LastOpenDirectory))
            {
                dlg.InitialDirectory = _appConfig.LastOpenDirectory;
            }
        }

        private void SaveLastOpenDirectory(string filePathOrDir)
        {
            if (string.IsNullOrWhiteSpace(filePathOrDir) || _appConfig == null) return;
            try
            {
                string dir = Directory.Exists(filePathOrDir) ? filePathOrDir : Path.GetDirectoryName(filePathOrDir);
                if (!string.IsNullOrWhiteSpace(dir) && Directory.Exists(dir))
                {
                    _appConfig.LastOpenDirectory = dir;
                    ConfigManager.SaveConfig(_appConfig);
                }
            }
            catch { }
        }

        private List<string> _lastActiveSelectedKeys = new List<string>();

        private void SyncSelectionToListBox(ListBox listBox, List<string> selectedKeyNames)
        {
            if (listBox == null || _keyEditorFilteredRows == null) return;

            bool prevSync = _isSyncingSelection;
            _isSyncingSelection = true;
            try
            {
                listBox.BeginUpdate();
                listBox.ClearSelected();
                if (selectedKeyNames == null || selectedKeyNames.Count == 0) return;

                int firstMatchedIdx = -1;
                for (int i = 0; i < _keyEditorFilteredRows.Count; i++)
                {
                    var row = _keyEditorFilteredRows[i];
                    if (row != null && selectedKeyNames.Contains(row.KeyName, StringComparer.OrdinalIgnoreCase))
                    {
                        if (i < listBox.Items.Count)
                        {
                            listBox.SetSelected(i, true);
                            if (firstMatchedIdx == -1) firstMatchedIdx = i;
                        }
                    }
                }
                if (firstMatchedIdx >= 0 && firstMatchedIdx < listBox.Items.Count)
                {
                    listBox.TopIndex = Math.Max(0, firstMatchedIdx - 2);
                }
            }
            finally
            {
                listBox.EndUpdate();
                _isSyncingSelection = prevSync;
            }
        }

        private void ApplySelectionState(List<string> targetKeys)
        {
            if (targetKeys != null && targetKeys.Count > 0)
            {
                _lastActiveSelectedKeys = new List<string>(targetKeys);
            }
            else
            {
                _lastActiveSelectedKeys.Clear();
            }

            var selectedTab = tabControlMain?.SelectedTab;

            if (_lastActiveSelectedKeys.Count > 0)
            {
                if (selectedTab == tabKeyEditor)
                {
                    if (lstKeyEditorKeys == null || lstKeyEditorKeys.Items.Count == 0 || _keyEditorFilteredRows == null)
                    {
                        PopulateKeyEditorList();
                    }
                    SyncSelectionToListBox(lstKeyEditorKeys, _lastActiveSelectedKeys);
                    if (lstKeyEditorKeys != null && lstKeyEditorKeys.SelectedIndices.Count > 0)
                    {
                        OnKeyEditorSelectionChanged();
                    }
                    else
                    {
                        ClearDetailInspector();
                    }
                }
                else
                {
                    var activeGrid = GetActiveGridForTab(selectedTab) ?? gridLabels;
                    SyncSelectionToGrid(activeGrid, _lastActiveSelectedKeys);
                    var activeSelectedRows = activeGrid.Rows.Cast<DataGridViewRow>().Where(r => r.Selected).ToList();
                    if (activeSelectedRows.Count > 0)
                    {
                        OnGridSelectionChanged(activeGrid);
                    }
                    else
                    {
                        ClearDetailInspector();
                    }
                }
            }
            else
            {
                _currentlyDisplayedSingleRow = null;
                ClearDetailInspector();
                if (selectedTab == tabKeyEditor)
                {
                    if (lstKeyEditorKeys == null || lstKeyEditorKeys.Items.Count == 0 || _keyEditorFilteredRows == null)
                    {
                        PopulateKeyEditorList();
                    }
                    if (lstKeyEditorKeys != null)
                    {
                        bool prevSync = _isSyncingSelection;
                        _isSyncingSelection = true;
                        try { lstKeyEditorKeys.ClearSelected(); } finally { _isSyncingSelection = prevSync; }
                    }
                    ShowPlainKeyEditorEmptyPlaceholder();
                }
                else
                {
                    var activeGrid = GetActiveGridForTab(selectedTab);
                    if (activeGrid != null)
                    {
                        bool prevSync = _isSyncingSelection;
                        _isSyncingSelection = true;
                        try
                        {
                            activeGrid.CurrentCell = null;
                            activeGrid.ClearSelection();
                        }
                        finally
                        {
                            _isSyncingSelection = prevSync;
                        }
                    }
                }
            }
        }

        private void SyncSelectionToGrid(DataGridView grid, List<string> selectedKeyNames, bool preserveScrollPosition = false)
        {
            if (grid == null) return;
            if (grid == gridLabels) CompleteMasterRowStreamNow();

            bool prevSync = _isSyncingSelection;
            _isSyncingSelection = true;
            try
            {
                grid.CurrentCell = null;
                grid.ClearSelection();
                if (selectedKeyNames == null || selectedKeyNames.Count == 0 || grid.Rows.Count == 0) return;

                int savedScroll = (preserveScrollPosition && grid.RowCount > 0) ? grid.FirstDisplayedScrollingRowIndex : -1;

                if (grid == gridLabels && tvCategories != null && tvCategories.Nodes.Count > 0)
                {
                    bool allFound = selectedKeyNames.All(k => gridLabels.Rows.Cast<DataGridViewRow>().Any(r =>
                        string.Equals(GetKeyNameFromRow(gridLabels, r), k, StringComparison.OrdinalIgnoreCase)
                    ));

                    if (!allFound && tvCategories.SelectedNode != tvCategories.Nodes[0])
                    {
                        tvCategories.SelectedNode = tvCategories.Nodes[0];
                    }
                }

                if (grid.Rows.Count == 0) return;

                int firstMatchedIdx = -1;
                for (int i = 0; i < grid.Rows.Count; i++)
                {
                    var r = grid.Rows[i];
                    string key = GetKeyNameFromRow(grid, r);

                    if (!string.IsNullOrEmpty(key) && selectedKeyNames.Contains(key, StringComparer.OrdinalIgnoreCase))
                    {
                        firstMatchedIdx = i;
                        break;
                    }
                }

                if (selectedKeyNames.Count == 1 && firstMatchedIdx >= 0 && firstMatchedIdx < grid.Rows.Count)
                {
                    int firstVisCol = 0;
                    for (int c = 0; c < grid.Columns.Count; c++)
                    {
                        if (grid.Columns[c].Visible) { firstVisCol = c; break; }
                    }
                    try
                    {
                        grid.CurrentCell = grid.Rows[firstMatchedIdx].Cells[firstVisCol];
                    }
                    catch { }
                }
                else
                {
                    grid.CurrentCell = null;
                }

                foreach (DataGridViewRow r in grid.Rows)
                {
                    string key = GetKeyNameFromRow(grid, r);

                    if (!string.IsNullOrEmpty(key) && selectedKeyNames.Contains(key, StringComparer.OrdinalIgnoreCase))
                    {
                        r.Selected = true;
                    }
                }

                if (savedScroll >= 0 && savedScroll < grid.Rows.Count)
                {
                    try { grid.FirstDisplayedScrollingRowIndex = savedScroll; } catch { }
                }
                else if (!preserveScrollPosition && firstMatchedIdx >= 0 && firstMatchedIdx < grid.Rows.Count)
                {
                    try { grid.FirstDisplayedScrollingRowIndex = Math.Max(0, firstMatchedIdx - 2); } catch { }
                }
            }
            finally
            {
                _isSyncingSelection = prevSync;
            }
        }

        private void OnMainTabSelectedIndexChanged()
        {
            if (_isSyncingSelection) return;
            UpdateUIForSessionMode();

            var selectedTab = tabControlMain?.SelectedTab;

            if (selectedTab != null && _appConfig != null)
            {
                _appConfig.LastActiveMainTabName = selectedTab.Name;
                System.Threading.Tasks.Task.Run(() => ConfigManager.SaveConfig(_appConfig));
            }

            if (_session == null || _session.Documents.Count == 0) return;

            _currentlyRenderedMasterKeyNames.Clear();
            _currentlyDisplayedSingleRow = null;

            // STEP 1: CARGAR (Populate tab contents)
            if (selectedTab == tabUnsaved)
            {
                if (_unsavedDirty)
                {
                    PopulateUnsavedChangesTab(GetMasterRows());
                    _unsavedDirty = false;
                }
            }
            else if (selectedTab == tabRecent)
            {
                if (_recentDirty || gridRecent == null || gridRecent.Rows.Count == 0)
                {
                    PopulateRecentGrid();
                    _recentDirty = false;
                }
            }
            else if (selectedTab == tabCoverage)
            {
                if (_coverageDirty)
                {
                    PopulateCoverageMatrixTab();
                    _coverageDirty = false;
                }
            }
            else if (selectedTab == tabBackups)
            {
                PopulateBackupsTab();
            }
            else if (selectedTab == tabKeyEditor)
            {
                if (_keyEditorDirty || lstKeyEditorKeys == null || lstKeyEditorKeys.Items.Count == 0 || _keyEditorFilteredRows == null)
                {
                    PopulateKeyEditorList();
                    _keyEditorDirty = false;
                }
                AdjustKeyEditorSplitterDistance();
            }

            // STEP 2 & 3: Apply selection and update inspector in a unified manner
            ApplySelectionState(_lastActiveSelectedKeys);

            SaveSessionViewStateToConfig();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (!ConfirmSaveIfModified())
            {
                e.Cancel = true;
                return;
            }

            base.OnFormClosing(e);
            if (_appConfig != null)
            {
                if (_appConfig.RememberPanelLayoutPositions)
                {
                    _appConfig.IsMaximized = (this.WindowState == FormWindowState.Maximized);
                    if (this.WindowState == FormWindowState.Normal)
                    {
                        _appConfig.Width = this.Width;
                        _appConfig.Height = this.Height;
                    }
                    _appConfig.MasterKeysViewPanelWidth = splitMain.SplitterDistance;
                    _appConfig.MasterKeysViewInspectorHeight = splitMasterDetail.SplitterDistance;
                }
                _appConfig.KeyRegexMode = _keyRegexMode;
                _appConfig.ValRegexMode = _valRegexMode;
                _appConfig.FilterLogicAnd = _filterLogicAnd;
                _appConfig.SortByBinarySequence = _sortByBinarySequence;
                _appConfig.SelectedStatusFilterIndex = cboStatusFilter != null ? cboStatusFilter.SelectedIndex : 0;
                ConfigManager.SaveConfig(_appConfig);
            }
            SaveSessionViewStateToConfig();
        }

        #endregion

        #region Translation & Context Menu Setup

        private ContextMenuStrip _gridContextMenu;
        private ToolStripMenuItem _menuTranslateGridSelection;
        private ToolStripMenuItem _menuExportSelectedKeys;
        private ToolStripMenuItem _menuCopyFrom;
        private ToolStripMenuItem _menuTranslateMain;
        private List<string> _gridContextMenuSelectedKeys = new List<string>();

        private bool IsLocalTranslationEndpoint(string endpoint)
        {
            if (string.IsNullOrWhiteSpace(endpoint)) return false;
            if (!Uri.TryCreate(endpoint.Trim(), UriKind.Absolute, out var uri)) return false;
            return uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
                   uri.Host.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase) ||
                   uri.Host.Equals("::1", StringComparison.OrdinalIgnoreCase);
        }

        private bool IsTranslationServiceConfigured(TranslationServiceConfig config)
        {
            if (config == null || string.IsNullOrWhiteSpace(config.ProviderType)) return false;

            if (config.ProviderType.Equals("GoogleWeb", StringComparison.OrdinalIgnoreCase))
            {
                return !string.IsNullOrWhiteSpace(config.UrlTemplate);
            }

            if (config.ProviderType.Equals("DeepL", StringComparison.OrdinalIgnoreCase))
            {
                return !string.IsNullOrWhiteSpace(config.Endpoint) && !string.IsNullOrWhiteSpace(config.ApiKey);
            }

            if (config.ProviderType.Equals("MicrosoftTranslator", StringComparison.OrdinalIgnoreCase))
            {
                return !string.IsNullOrWhiteSpace(config.Endpoint) && !string.IsNullOrWhiteSpace(config.ApiKey);
            }

            if (config.ProviderType.Equals("OpenAICompatible", StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(config.Endpoint) || string.IsNullOrWhiteSpace(config.Model)) return false;
                return IsLocalTranslationEndpoint(config.Endpoint) || !string.IsNullOrWhiteSpace(config.ApiKey);
            }

            return false;
        }

        private TranslationServiceConfig EnsureTranslationServiceConfigured(TranslationServiceConfig config)
        {
            if (IsTranslationServiceConfigured(config)) return config;

            using (var settings = new TranslationSettingsForm(config?.SectionName))
            {
                if (settings.ShowDialog(this) != DialogResult.OK) return null;
            }

            BuildTranslationSubmenus();
            return TranslationConfigManager.ConfiguredServices.FirstOrDefault(s =>
                string.Equals(s.SectionName, config?.SectionName, StringComparison.OrdinalIgnoreCase));
        }

        private void SetupGridContextMenu()
        {
            _gridContextMenu = new ContextMenuStrip();
            _menuTranslateGridSelection = new ToolStripMenuItem("🌐 Translate Selection...");
            _menuExportSelectedKeys = new ToolStripMenuItem("📤 Export Selected Keys to TXT");
            _menuCopyFrom = new ToolStripMenuItem("📋 Copy from...");

            _gridContextMenu.Items.Add(new ToolStripMenuItem("➕ Add Key", null, (s, e) => menuAddLabel_Click(s, e)));
            _gridContextMenu.Items.Add(new ToolStripMenuItem("📋 Duplicate Key", null, (s, e) => menuDuplicateKey_Click(s, e)));
            _gridContextMenu.Items.Add(new ToolStripMenuItem("❌ Delete Key", null, (s, e) => menuDeleteLabel_Click(s, e)));
            _gridContextMenu.Items.Add(new ToolStripSeparator());
            _gridContextMenu.Items.Add(_menuExportSelectedKeys);
            _gridContextMenu.Items.Add(_menuCopyFrom);
            _gridContextMenu.Items.Add(new ToolStripSeparator());
            _gridContextMenu.Items.Add(_menuTranslateGridSelection);

            gridLabels.ContextMenuStrip = _gridContextMenu;
            if (gridCoverage != null) gridCoverage.ContextMenuStrip = _gridContextMenu;
            if (gridUnsaved != null) gridUnsaved.ContextMenuStrip = _gridContextMenu;
            if (gridRecent != null) gridRecent.ContextMenuStrip = _gridContextMenu;

            _gridContextMenu.Opening += (s, e) =>
            {
                var selKeys = GetCurrentlySelectedKeyNames();
                _gridContextMenuSelectedKeys = selKeys != null
                    ? new List<string>(selKeys)
                    : new List<string>();
                bool hasSel = selKeys != null && selKeys.Count > 0;
                _menuTranslateGridSelection.Enabled = hasSel;

                _menuExportSelectedKeys.DropDownItems.Clear();
                _menuExportSelectedKeys.Enabled = hasSel && _session != null && _session.Documents.Count > 0;
                if (_menuExportSelectedKeys.Enabled)
                {
                    foreach (var sDoc in _session.Documents)
                    {
                        var target = sDoc;
                        string title = _session.Documents.Count > 1
                            ? $"From [{sDoc.LanguageTag}] {sDoc.FileName}"
                            : $"Export {selKeys.Count} selected key(s)...";
                        var item = new ToolStripMenuItem(title, null, (s2, e2) =>
                            PerformExportForDoc(target, new List<string>(_gridContextMenuSelectedKeys)));
                        _menuExportSelectedKeys.DropDownItems.Add(item);
                    }
                }

                _menuCopyFrom.DropDownItems.Clear();
                bool canCopy = hasSel && _session != null && _session.Documents.Count > 1;
                _menuCopyFrom.Enabled = canCopy;
                if (canCopy)
                {
                    foreach (var srcDoc in _session.Documents)
                    {
                        var sDoc = srcDoc;
                        var srcItem = new ToolStripMenuItem($"From [{sDoc.LanguageTag}] {sDoc.FileName}");

                        foreach (var tgtDoc in _session.Documents)
                        {
                            if (tgtDoc == sDoc) continue;
                            var tDoc = tgtDoc;

                            var tgtItem = new ToolStripMenuItem($"To [{tDoc.LanguageTag}] {tDoc.FileName}", null, (s2, e2) =>
                            {
                                PerformBatchCopyBetweenDocs(sDoc, tDoc, new List<string>(_gridContextMenuSelectedKeys));
                            });

                            srcItem.DropDownItems.Add(tgtItem);
                        }

                        _menuCopyFrom.DropDownItems.Add(srcItem);
                    }
                }
            };
        }

        private void PerformBatchCopyBetweenDocs(CsfSessionDocument srcDoc, CsfSessionDocument tgtDoc, List<string> selectedKeys)
        {
            if (srcDoc == null || tgtDoc == null || selectedKeys == null || selectedKeys.Count == 0) return;

            var batchCmd = new BatchUndoCommand($"Copy {selectedKeys.Count} key(s) from [{srcDoc.LanguageTag}] to [{tgtDoc.LanguageTag}]");
            int copiedCount = 0;

            bool prevSync = _isSyncingSelection;
            _isSyncingSelection = true;
            try
            {
                foreach (string keyName in selectedKeys)
                {
                    var srcLbl = srcDoc.Document.Labels.FirstOrDefault(l => string.Equals(l.Name, keyName, StringComparison.OrdinalIgnoreCase));
                    if (srcLbl == null) continue;

                    string srcVal = srcLbl.Strings.Count > 0 ? srcLbl.Strings[0].Value : string.Empty;
                    string srcExtra = srcLbl.Strings.Count > 0 ? srcLbl.Strings[0].ExtraValue : null;

                    var tgtLbl = tgtDoc.Document.Labels.FirstOrDefault(l => string.Equals(l.Name, keyName, StringComparison.OrdinalIgnoreCase));
                    if (tgtLbl == null)
                    {
                        tgtLbl = new CsfLabel(keyName, srcVal, srcExtra);
                        tgtDoc.Document.Labels.Add(tgtLbl);
                        tgtDoc.IsModified = true;
                        batchCmd.AddCommand(new AddKeyCommand(keyName, tgtDoc.LanguageTag));
                        MarkKeyAsCreated(keyName);
                        MarkKeyAsModified(tgtDoc.LanguageTag, keyName);
                        copiedCount++;
                    }
                    else
                    {
                        string oldVal = tgtLbl.Strings.Count > 0 ? tgtLbl.Strings[0].Value : string.Empty;
                        if (oldVal != srcVal)
                        {
                            tgtLbl.Strings.Clear();
                            tgtLbl.Strings.Add(new CsfStringEntry(srcVal, srcExtra));
                            tgtDoc.IsModified = true;
                            batchCmd.AddCommand(new EditValueCommand(tgtDoc.LanguageTag, keyName, oldVal, srcVal));
                            MarkKeyAsModified(tgtDoc.LanguageTag, keyName);
                            copiedCount++;
                        }
                    }
                }
            }
            finally
            {
                _isSyncingSelection = prevSync;
            }

            if (batchCmd.Commands.Count > 0)
            {
                _undoManager.Execute(batchCmd, _session);
                UpdateUndoRedoMenuItems();
            }

            _lastSelectedTargetLanguageTag = tgtDoc.LanguageTag;
            InvalidateMasterRowsCache();
            RebuildCategoryTreeAndGrid();
            RestoreSelectionAfterRefresh(selectedKeys);
            ShowSaveNotification($"📋 Copied {copiedCount} key(s) from [{srcDoc.LanguageTag}] to [{tgtDoc.LanguageTag}]");
        }

        private void OpenDiffStudio(CsfDocument initialDocA = null, CsfDocument initialDocB = null)
        {
            var dlg = new CsfStudio.UI.CsfDiffForm(_session, initialDocA, initialDocB);
            dlg.ShowDialog(this);
            InvalidateMasterRowsCache();
            RebuildCategoryTreeAndGrid();
        }

        private void BuildTranslationSubmenus()
        {
            CsfStudio.Core.Translation.TranslationConfigManager.LoadConfig();

            if (_menuTranslateMain == null)
            {
                _menuTranslateMain = new ToolStripMenuItem("🌐 Translate / AI Localizer");
                menuTools.DropDownItems.Insert(0, _menuTranslateMain);
                menuTools.DropDownItems.Insert(1, new ToolStripSeparator());

                var menuDiffTool = new ToolStripMenuItem("🔀 CSF Diff & Merge...", null, (s, e) => OpenDiffStudio(null, null));
                menuTools.DropDownItems.Insert(2, menuDiffTool);
            }

            _menuTranslateMain.DropDownItems.Clear();
            if (_menuTranslateGridSelection != null) _menuTranslateGridSelection.DropDownItems.Clear();

            foreach (var serviceConfig in CsfStudio.Core.Translation.TranslationConfigManager.ConfiguredServices.Where(s => s.IsEnabled))
            {
                var sConfig = serviceConfig;
                string iconStr = (sConfig.IsAiModel && !sConfig.DisplayName.StartsWith("[AI]", StringComparison.OrdinalIgnoreCase))
                    ? "[AI] "
                    : ((!sConfig.DisplayName.StartsWith("🌐") && !sConfig.DisplayName.StartsWith("[AI]")) ? "🌐 " : "");

                var itemMain = new ToolStripMenuItem($"{iconStr}{sConfig.DisplayName}", null, (s, e) =>
                {
                    var readyConfig = EnsureTranslationServiceConfigured(sConfig);
                    if (readyConfig == null) return;
                    var dlg = new CsfStudio.UI.TranslationServiceForm(_session, readyConfig, null);
                    dlg.ShowDialog(this);
                    if (dlg.TranslationUndoBatch != null && dlg.TranslationUndoBatch.Commands.Count > 0)
                    {
                        _undoManager.Execute(dlg.TranslationUndoBatch, _session);
                        UpdateUndoRedoMenuItems();
                    }
                    InvalidateMasterRowsCache();
                    RefreshDataAfterTextTranslation(null);
                });
                _menuTranslateMain.DropDownItems.Add(itemMain);

                if (_menuTranslateGridSelection != null)
                {
                    var itemGrid = new ToolStripMenuItem($"{iconStr}{sConfig.DisplayName}", null, (s, e) =>
                    {
                        var selKeys = _gridContextMenuSelectedKeys != null && _gridContextMenuSelectedKeys.Count > 0
                            ? new List<string>(_gridContextMenuSelectedKeys)
                            : GetCurrentlySelectedKeyNames();
                        var readyConfig = EnsureTranslationServiceConfigured(sConfig);
                        if (readyConfig == null) return;
                        var dlg = new CsfStudio.UI.TranslationServiceForm(_session, readyConfig, selKeys);
                        dlg.ShowDialog(this);
                        if (dlg.TranslationUndoBatch != null && dlg.TranslationUndoBatch.Commands.Count > 0)
                        {
                            _undoManager.Execute(dlg.TranslationUndoBatch, _session);
                            UpdateUndoRedoMenuItems();
                        }
                        InvalidateMasterRowsCache();
                        RefreshDataAfterTextTranslation(selKeys);
                    });
                    _menuTranslateGridSelection.DropDownItems.Add(itemGrid);
                }
            }

            _menuTranslateMain.DropDownItems.Add(new ToolStripSeparator());
            var menuSettings = new ToolStripMenuItem("⚙️ Translation & AI Settings...", null, (s, e) =>
            {
                var dlg = new CsfStudio.UI.TranslationSettingsForm();
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    BuildTranslationSubmenus();
                }
            });
            _menuTranslateMain.DropDownItems.Add(menuSettings);

            if (_menuTranslateGridSelection != null)
            {
                _menuTranslateGridSelection.DropDownItems.Add(new ToolStripSeparator());
                var menuGridSettings = new ToolStripMenuItem("⚙️ Translation & AI Settings...", null, (s, e) =>
                {
                    var dlg = new CsfStudio.UI.TranslationSettingsForm();
                    if (dlg.ShowDialog(this) == DialogResult.OK)
                    {
                        BuildTranslationSubmenus();
                    }
                });
                _menuTranslateGridSelection.DropDownItems.Add(menuGridSettings);
            }
        }

        private string GetMasterGridStatusKind(MasterKeyRow row)
        {
            if (row == null) return "COMPLETE";
            if (row.Status == KeySyncStatus.MissingInSome) return "MISSING";
            if (row.Status == KeySyncStatus.UntranslatedOrEmpty) return "EMPTY";
            if (_addedKeyNames != null && _addedKeyNames.Contains(row.KeyName)) return "CREATED";
            if (_modifiedKeyNames != null && _modifiedKeyNames.Contains(row.KeyName)) return "MODIFIED";
            return "COMPLETE";
        }

        private static void DrawStatusSphere(Graphics g, Rectangle bounds, string statusKind)
        {
            int size = 12;
            int x = bounds.X + (bounds.Width - size) / 2;
            int y = bounds.Y + (bounds.Height - size) / 2;
            DrawStatusSphereAt(g, x, y, size, statusKind);
        }

        private static void DrawStatusSphereAt(Graphics g, int x, int y, int size, string statusKind)
        {
            Rectangle rect = new Rectangle(x, y, size, size);
            Color colorTop, colorBottom, colorBorder;

            bool isGreen  = statusKind.Contains("🟢") || statusKind.IndexOf("COMPLETE", StringComparison.OrdinalIgnoreCase) >= 0;
            bool isYellow = statusKind.Contains("🟡") || statusKind.IndexOf("EMPTY",    StringComparison.OrdinalIgnoreCase) >= 0;
            bool isRed    = statusKind.Contains("🔴") || statusKind.IndexOf("MISSING",  StringComparison.OrdinalIgnoreCase) >= 0;
            bool isBlue   = statusKind.Contains("🔵") || statusKind.IndexOf("MODIFIED", StringComparison.OrdinalIgnoreCase) >= 0 || statusKind.IndexOf("CREATED", StringComparison.OrdinalIgnoreCase) >= 0;

            if (isGreen && !isYellow && !isRed && !isBlue)
            {
                colorTop    = Color.FromArgb(46, 204, 113);
                colorBottom = Color.FromArgb(34, 153, 84);
                colorBorder = Color.FromArgb(25, 111, 61);
            }
            else if (isYellow)
            {
                colorTop    = Color.FromArgb(241, 196, 15);
                colorBottom = Color.FromArgb(214, 137, 16);
                colorBorder = Color.FromArgb(156, 100, 12);
            }
            else if (isRed)
            {
                colorTop    = Color.FromArgb(231, 76, 60);
                colorBottom = Color.FromArgb(176, 40, 26);
                colorBorder = Color.FromArgb(120, 25, 18);
            }
            else if (isBlue)
            {
                colorTop    = Color.FromArgb(52, 152, 219);
                colorBottom = Color.FromArgb(31, 97, 141);
                colorBorder = Color.FromArgb(21, 67, 96);
            }
            else
            {
                colorTop    = Color.FromArgb(46, 204, 113);
                colorBottom = Color.FromArgb(34, 153, 84);
                colorBorder = Color.FromArgb(25, 111, 61);
            }

            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            using (var brush = new System.Drawing.Drawing2D.LinearGradientBrush(rect, colorTop, colorBottom, System.Drawing.Drawing2D.LinearGradientMode.Vertical))
            {
                g.FillEllipse(brush, rect);
            }

            using (var pen = new Pen(colorBorder, 1.0f))
            {
                g.DrawEllipse(pen, rect);
            }

            using (var specBrush = new SolidBrush(Color.FromArgb(180, 255, 255, 255)))
            {
                g.FillEllipse(specBrush, x + 2, y + 2, 3, 2);
            }
        }

        /// <summary>Returns a colored Unicode emoji for the given status key (COMPLETE/EMPTY/MISSING/MODIFIED).</summary>
        private static string GetStatusEmoji(string statusKey)
        {
            if (statusKey == null) return "🟢";
            if (statusKey.IndexOf("MISSING",  StringComparison.OrdinalIgnoreCase) >= 0) return "🔴";
            if (statusKey.IndexOf("EMPTY",    StringComparison.OrdinalIgnoreCase) >= 0) return "🟡";
            if (statusKey.IndexOf("MODIFIED", StringComparison.OrdinalIgnoreCase) >= 0) return "🔵";
            return "🟢"; // COMPLETE
        }

        /// <summary>Returns light pastel (bgNormal, bgSelected, textColor) for painting tab backgrounds by status.</summary>
        private static (Color bgNormal, Color bgSelected, Color textColor) GetStatusTabColors(string statusKey)
        {
            if (statusKey == null || statusKey.IndexOf("COMPLETE", StringComparison.OrdinalIgnoreCase) >= 0)
                return (Color.FromArgb(220, 245, 220), Color.FromArgb(195, 235, 195), Color.FromArgb(30, 90, 30));   // soft green
            if (statusKey.IndexOf("MISSING", StringComparison.OrdinalIgnoreCase) >= 0)
                return (Color.FromArgb(255, 220, 220), Color.FromArgb(255, 195, 195), Color.FromArgb(130, 30, 30));  // soft red/pink
            if (statusKey.IndexOf("EMPTY", StringComparison.OrdinalIgnoreCase) >= 0)
                return (Color.FromArgb(255, 248, 210), Color.FromArgb(255, 240, 175), Color.FromArgb(110, 80, 10));  // soft amber/yellow
            if (statusKey.IndexOf("MODIFIED", StringComparison.OrdinalIgnoreCase) >= 0)
                return (Color.FromArgb(215, 230, 255), Color.FromArgb(185, 210, 255), Color.FromArgb(25, 60, 130));  // soft blue
            return (Color.FromArgb(220, 245, 220), Color.FromArgb(195, 235, 195), Color.FromArgb(30, 90, 30));
        }

        private static string GetIsoCodeFromLangTag(string langTag)
        {
            if (string.IsNullOrEmpty(langTag)) return "es";
            string tagUpper = langTag.Trim('[', ']', ' ').ToUpperInvariant();
            if (tagUpper == "EN" || tagUpper.Contains("ENG") || tagUpper.Contains("US") || tagUpper.Contains("UK")) return "en";
            if (tagUpper == "ES" || tagUpper.Contains("ESP") || tagUpper.Contains("SPA")) return "es";
            if (tagUpper == "FR" || tagUpper.Contains("FRE") || tagUpper.Contains("FRA")) return "fr";
            if (tagUpper == "DE" || tagUpper.Contains("GER") || tagUpper.Contains("DEU")) return "de";
            if (tagUpper == "IT" || tagUpper.Contains("ITA")) return "it";
            if (tagUpper == "RU" || tagUpper.Contains("RUS")) return "ru";
            if (tagUpper.StartsWith("ZH") || tagUpper.Contains("CHI") || tagUpper.Contains("CHN") || tagUpper.Contains("ZHO")) return "zh-CN";
            if (tagUpper == "JA" || tagUpper.Contains("JPN") || tagUpper.Contains("JAP")) return "ja";
            if (tagUpper == "KO" || tagUpper.Contains("KOR")) return "ko";
            if (tagUpper == "PT" || tagUpper.Contains("POR") || tagUpper.Contains("BRA")) return "pt";
            return langTag.ToLowerInvariant();
        }

        private static string GetFriendlyLanguageDisplayName(string langTag)
        {
            if (string.IsNullOrEmpty(langTag)) return "Target Language";
            string tagUpper = langTag.Trim('[', ']', ' ').ToUpperInvariant();
            if (tagUpper == "EN" || tagUpper.Contains("ENG") || tagUpper.Contains("US") || tagUpper.Contains("UK")) return "English (en)";
            if (tagUpper == "ES" || tagUpper.Contains("ESP") || tagUpper.Contains("SPA")) return "Spanish (es)";
            if (tagUpper == "FR" || tagUpper.Contains("FRE") || tagUpper.Contains("FRA")) return "French (fr)";
            if (tagUpper == "DE" || tagUpper.Contains("GER") || tagUpper.Contains("DEU")) return "German (de)";
            if (tagUpper == "IT" || tagUpper.Contains("ITA")) return "Italian (it)";
            if (tagUpper == "RU" || tagUpper.Contains("RUS")) return "Russian (ru)";
            if (tagUpper.Contains("ZH-HANS") || tagUpper.Contains("SIMPLIFIED")) return "Chinese Simplified (zh-hans)";
            if (tagUpper.Contains("ZH-HANT") || tagUpper.Contains("TRADITIONAL")) return "Chinese Traditional (zh-hant)";
            if (tagUpper.StartsWith("ZH") || tagUpper.Contains("CHI") || tagUpper.Contains("CHN")) return "Chinese (zh)";
            if (tagUpper == "JA" || tagUpper.Contains("JPN") || tagUpper.Contains("JAP")) return "Japanese (ja)";
            if (tagUpper == "KO" || tagUpper.Contains("KOR")) return "Korean (ko)";
            if (tagUpper == "PT" || tagUpper.Contains("POR") || tagUpper.Contains("BRA")) return "Portuguese (pt)";

            return langTag;
        }

        private string GetTranslationLanguageForDocument(CsfSessionDocument doc, bool promptForNeutral)
        {
            if (doc == null) return string.Empty;

            string contentLanguage = TranslationLanguageHelper.Normalize(doc.TranslationContentLanguage);
            if (!string.IsNullOrEmpty(contentLanguage) && contentLanguage != "auto") return contentLanguage;

            string headerLanguage = TranslationLanguageHelper.GetIsoCode(doc.Document?.Language);
            if (!string.IsNullOrEmpty(headerLanguage)) return headerLanguage;
            if (!promptForNeutral) return string.Empty;

            using (var dlg = new NeutralLanguageDialog(doc.FileName, contentLanguage))
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return string.Empty;
                doc.TranslationContentLanguage = dlg.SelectedLanguage;
                SaveSessionViewStateToConfig();
                return doc.TranslationContentLanguage;
            }
        }

        private void PromptTranslationContentLanguage(CsfSessionDocument doc)
        {
            if (doc == null) return;

            string currentLanguage = TranslationLanguageHelper.Normalize(doc.TranslationContentLanguage);
            if (string.IsNullOrEmpty(currentLanguage))
            {
                currentLanguage = TranslationLanguageHelper.GetIsoCode(doc.Document?.Language);
            }

            using (var dlg = new NeutralLanguageDialog(
                doc.FileName,
                currentLanguage,
                doc.Document != null && doc.Document.Language == CsfLanguage.LanguageNeutral))
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return;

                string newLanguage = dlg.SelectedLanguage;
                if (string.Equals(doc.TranslationContentLanguage, newLanguage, StringComparison.OrdinalIgnoreCase)) return;

                doc.TranslationContentLanguage = newLanguage;
                OnSessionUpdated();
                SaveSessionViewStateToConfig();
            }
        }

        private void PopulateSetTranslationContentLangSubmenu()
        {
            if (menuSetTranslationContentLang == null) return;

            menuSetTranslationContentLang.Click -= MenuSetTranslationContentLang_Click;
            menuSetTranslationContentLang.DropDownItems.Clear();

            if (_session == null || _session.Documents == null || _session.Documents.Count == 0)
            {
                menuSetTranslationContentLang.Enabled = false;
                return;
            }

            menuSetTranslationContentLang.Enabled = true;

            if (_session.Documents.Count == 1)
            {
                menuSetTranslationContentLang.Click += MenuSetTranslationContentLang_Click;
            }
            else
            {
                foreach (var doc in _session.Documents)
                {
                    string fileName = doc.FileName ?? (string.IsNullOrEmpty(doc.FilePath) ? $"Document [{doc.LanguageTag}]" : Path.GetFileName(doc.FilePath));
                    string currLangDisplay = GetFriendlyLanguageDisplayName(doc.TranslationContentLanguage);
                    string itemText = $"📄 {fileName} [{doc.LanguageTag}] ({currLangDisplay})";

                    var subItem = new ToolStripMenuItem(itemText);
                    CsfSessionDocument targetDoc = doc;
                    subItem.Click += (s, e) => PromptTranslationContentLanguage(targetDoc);
                    menuSetTranslationContentLang.DropDownItems.Add(subItem);
                }
            }
        }

        private void MenuSetTranslationContentLang_Click(object sender, EventArgs e)
        {
            if (_session != null && _session.Documents != null && _session.Documents.Count == 1)
            {
                PromptTranslationContentLanguage(_session.Documents[0]);
            }
        }

        private string GetTranslationLanguageMenuLabel(CsfSessionDocument doc, string fallbackTag)
        {
            string language = GetTranslationLanguageForDocument(doc, false);
            if (!string.IsNullOrEmpty(language)) return GetFriendlyLanguageDisplayName(language);
            return $"Neutral ({fallbackTag}) - choose content language";
        }

        private ToolStripMenuItem CreateTranslationSubMenu(string textToTranslate, string targetIsoCode, string targetLanguageLabel, string keyName = null, CsfSessionDocument targetDoc = null, bool exists = true, string targetFileLabel = null, TextBox targetTextBox = null)
        {
            string langDisplay = GetFriendlyLanguageDisplayName(targetLanguageLabel);
            string menuLabel = string.IsNullOrWhiteSpace(targetFileLabel) ? targetLanguageLabel : targetFileLabel;
            string menuTitle = exists ? $"🌐 Translate '{menuLabel}' Entry..." : $"🌐 Translate '{menuLabel}' Entry (Key Missing)";
            var menuTranslate = new ToolStripMenuItem(menuTitle)
            {
                Enabled = exists
            };

            var configuredServices = CsfStudio.Core.Translation.TranslationConfigManager.ConfiguredServices?.Where(s => s.IsEnabled).ToList();
            if (configuredServices != null && configuredServices.Count > 0)
            {
                foreach (var serviceConfig in configuredServices)
                {
                    var sConfig = serviceConfig;
                    string iconStr = (sConfig.IsAiModel && !sConfig.DisplayName.StartsWith("[AI]", StringComparison.OrdinalIgnoreCase))
                        ? "[AI] "
                        : ((!sConfig.DisplayName.StartsWith("🌐") && !sConfig.DisplayName.StartsWith("[AI]")) ? "🌐 " : "");
                    var itemService = new ToolStripMenuItem($"{iconStr}... into '{langDisplay}' with {sConfig.DisplayName}", null, (s, e) =>
                    {
                        var readyConfig = EnsureTranslationServiceConfigured(sConfig);
                        if (readyConfig == null) return;
                        string effectiveTargetLanguage = targetDoc != null && string.IsNullOrWhiteSpace(targetIsoCode)
                            ? GetTranslationLanguageForDocument(targetDoc, true)
                            : TranslationLanguageHelper.Normalize(targetIsoCode);
                        if (!string.IsNullOrEmpty(effectiveTargetLanguage))
                        {
                            ExecuteInstantSingleEntryTranslation(readyConfig, textToTranslate, effectiveTargetLanguage, keyName, targetLanguageLabel, targetDoc, targetTextBox);
                        }
                    });
                    menuTranslate.DropDownItems.Add(itemService);
                }

                menuTranslate.DropDownItems.Add(new ToolStripSeparator());

                var itemBatch = new ToolStripMenuItem("📂 Batch Translate CSF Files (Dialog)...", null, (s, e) =>
                {
                    var keysToTranslate = !string.IsNullOrEmpty(keyName)
                        ? new List<string> { keyName }
                        : (_gridContextMenuSelectedKeys != null && _gridContextMenuSelectedKeys.Count > 0
                            ? new List<string>(_gridContextMenuSelectedKeys)
                            : GetCurrentlySelectedKeyNames());
                    var serviceConfig = EnsureTranslationServiceConfigured(configuredServices[0]);
                    if (serviceConfig == null) return;
                    var dlg = new CsfStudio.UI.TranslationServiceForm(_session, serviceConfig, keysToTranslate);
                    dlg.ShowDialog(this);
                    if (dlg.TranslationUndoBatch != null && dlg.TranslationUndoBatch.Commands.Count > 0)
                    {
                        _undoManager.Execute(dlg.TranslationUndoBatch, _session);
                        UpdateUndoRedoMenuItems();
                    }
                    InvalidateMasterRowsCache();
                    RefreshDataAfterTextTranslation(keysToTranslate);
                });
                menuTranslate.DropDownItems.Add(itemBatch);
            }

            var menuSettings = new ToolStripMenuItem("⚙️ Translation & AI Settings...", null, (s, e) =>
            {
                var dlg = new CsfStudio.UI.TranslationSettingsForm();
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    BuildTranslationSubmenus();
                }
            });
            menuTranslate.DropDownItems.Add(menuSettings);
            menuTranslate.DropDownItems.Add(new ToolStripSeparator());

            var menuWeb = new ToolStripMenuItem("🌐 Open in Web Browser...");
            var itemGoogle = new ToolStripMenuItem("🌐 ... in Google Translate (Web)");
            itemGoogle.Click += (s, e) =>
            {
                string effectiveTargetLanguage = targetDoc != null && string.IsNullOrWhiteSpace(targetIsoCode)
                    ? GetTranslationLanguageForDocument(targetDoc, true)
                    : TranslationLanguageHelper.Normalize(targetIsoCode);
                if (!string.IsNullOrEmpty(effectiveTargetLanguage))
                    OpenOnlineTranslator("Google", textToTranslate, effectiveTargetLanguage);
            };

            var itemDeepL = new ToolStripMenuItem("🌐 ... in DeepL Translator (Web)");
            itemDeepL.Click += (s, e) =>
            {
                string effectiveTargetLanguage = targetDoc != null && string.IsNullOrWhiteSpace(targetIsoCode)
                    ? GetTranslationLanguageForDocument(targetDoc, true)
                    : TranslationLanguageHelper.Normalize(targetIsoCode);
                if (!string.IsNullOrEmpty(effectiveTargetLanguage))
                    OpenOnlineTranslator("DeepL", textToTranslate, effectiveTargetLanguage);
            };
            var itemBing = new ToolStripMenuItem("🌐 ... in Bing / Microsoft Translator (Web)");
            itemBing.Click += (s, e) =>
            {
                string effectiveTargetLanguage = targetDoc != null && string.IsNullOrWhiteSpace(targetIsoCode)
                    ? GetTranslationLanguageForDocument(targetDoc, true)
                    : TranslationLanguageHelper.Normalize(targetIsoCode);
                if (!string.IsNullOrEmpty(effectiveTargetLanguage))
                    OpenOnlineTranslator("Bing", textToTranslate, effectiveTargetLanguage);
            };

            menuWeb.DropDownItems.Add(itemGoogle);
            menuWeb.DropDownItems.Add(itemDeepL);
            menuWeb.DropDownItems.Add(itemBing);

            menuTranslate.DropDownItems.Add(menuWeb);

            return menuTranslate;
        }

        private async void ExecuteInstantSingleEntryTranslation(CsfStudio.Core.Translation.TranslationServiceConfig sConfig, string textToTranslate, string targetIsoCode, string keyName, string langTagLabel, CsfSessionDocument targetDoc, TextBox targetTextBox = null)
        {
            if (sConfig == null || _session == null) return;

            if (targetDoc == null && !string.IsNullOrEmpty(langTagLabel))
            {
                targetDoc = _session.Documents.FirstOrDefault(d => string.Equals(d.LanguageTag, langTagLabel, StringComparison.OrdinalIgnoreCase));
            }
            if (targetDoc == null) targetDoc = _session.BaseDocument;
            if (targetDoc == null) return;

            if (string.IsNullOrWhiteSpace(targetIsoCode))
            {
                targetIsoCode = GetTranslationLanguageForDocument(targetDoc, true);
            }
            else
            {
                targetIsoCode = TranslationLanguageHelper.Normalize(targetIsoCode);
            }
            if (string.IsNullOrEmpty(targetIsoCode)) return;

            CsfSessionDocument sourceDoc = null;
            string sourceText = null;
            string sourceLanguage = null;

            if (_session.Documents != null && _session.Documents.Count > 0)
            {
                foreach (var doc in _session.Documents)
                {
                    if (ReferenceEquals(doc, targetDoc)) continue;

                    string docLang = GetTranslationLanguageForDocument(doc, true);
                    if (string.IsNullOrEmpty(docLang) || string.Equals(docLang, targetIsoCode, StringComparison.OrdinalIgnoreCase)) continue;

                    if (!string.IsNullOrEmpty(keyName) && doc.Document != null)
                    {
                        var lbl = doc.Document.Labels.FirstOrDefault(l => string.Equals(l.Name, keyName, StringComparison.OrdinalIgnoreCase));
                        if (lbl != null && lbl.Strings.Count > 0 && !string.IsNullOrWhiteSpace(lbl.Strings[0].Value))
                        {
                            sourceDoc = doc;
                            sourceText = lbl.Strings[0].Value;
                            sourceLanguage = docLang;
                            break;
                        }
                    }
                }

                if (string.IsNullOrWhiteSpace(sourceText) && _session.BaseDocument != null && !ReferenceEquals(_session.BaseDocument, targetDoc))
                {
                    string baseLang = GetTranslationLanguageForDocument(_session.BaseDocument, true);
                    if (!string.IsNullOrEmpty(baseLang) && !string.Equals(baseLang, targetIsoCode, StringComparison.OrdinalIgnoreCase))
                    {
                        if (!string.IsNullOrEmpty(keyName) && _session.BaseDocument.Document != null)
                        {
                            var lbl = _session.BaseDocument.Document.Labels.FirstOrDefault(l => string.Equals(l.Name, keyName, StringComparison.OrdinalIgnoreCase));
                            if (lbl != null && lbl.Strings.Count > 0 && !string.IsNullOrWhiteSpace(lbl.Strings[0].Value))
                            {
                                sourceDoc = _session.BaseDocument;
                                sourceText = lbl.Strings[0].Value;
                                sourceLanguage = baseLang;
                            }
                        }
                    }
                }
            }

            if (string.IsNullOrWhiteSpace(sourceText))
            {
                sourceText = textToTranslate;
            }

            if (string.IsNullOrEmpty(sourceLanguage))
            {
                sourceLanguage = GetTranslationLanguageForDocument(_session.BaseDocument, true);
                if (string.IsNullOrEmpty(sourceLanguage) || string.Equals(sourceLanguage, targetIsoCode, StringComparison.OrdinalIgnoreCase))
                {
                    sourceLanguage = "auto";
                }
            }

            if (string.IsNullOrWhiteSpace(sourceText))
            {
                MessageBox.Show("There is no reference text available in other open CSF files to translate this entry.", "Instant Translation", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            try
            {
                lblStatusCount.Text = $"⏳ Translating '{keyName}' from [{sourceLanguage}] to [{targetIsoCode}] via {sConfig.DisplayName}...";
                Cursor = Cursors.WaitCursor;

                var provider = CsfStudio.Core.Translation.TranslationProviderFactory.CreateProvider(sConfig);
                var item = new CsfStudio.Core.Translation.TranslationItem { Key = keyName ?? "SingleText", SourceText = sourceText };

                var result = await Task.Run(() => provider.TranslateBatchAsync(new List<CsfStudio.Core.Translation.TranslationItem> { item }, sourceLanguage, targetIsoCode, System.Threading.CancellationToken.None));

                Cursor = Cursors.Default;

                if (result != null && result.Items.Count > 0 && !string.IsNullOrEmpty(result.Items[0].TranslatedText))
                {
                    string translatedText = result.Items[0].TranslatedText;

                    if (targetDoc != null)
                    {
                        if (targetDoc.Document != null)
                        {
                            var lbl = targetDoc.Document.Labels.FirstOrDefault(l => string.Equals(l.Name, keyName, StringComparison.OrdinalIgnoreCase));
                            if (lbl == null)
                            {
                                lbl = new CsfLabel(keyName, translatedText);
                                targetDoc.Document.Labels.Add(lbl);
                            }
                            else
                            {
                                if (lbl.Strings.Count == 0) lbl.Strings.Add(new CsfStringEntry(translatedText));
                                else lbl.Strings[0].Value = translatedText;
                            }
                        }
                        targetDoc.IsModified = true;

                        if (targetTextBox != null && !targetTextBox.IsDisposed)
                        {
                            _isPopulatingInspector = true;
                            try
                            {
                                targetTextBox.Text = NormalizeToWinFormsLineBreaks(translatedText);
                            }
                            finally
                            {
                                _isPopulatingInspector = false;
                            }
                        }
                        else if (!string.IsNullOrEmpty(targetDoc.LanguageTag) && _langTextEditors.TryGetValue(targetDoc.LanguageTag, out var txtEditor) && txtEditor != null)
                        {
                            _isPopulatingInspector = true;
                            try
                            {
                                txtEditor.Text = NormalizeToWinFormsLineBreaks(translatedText);
                            }
                            finally
                            {
                                _isPopulatingInspector = false;
                            }
                        }

                        UpdateGridRowAfterValueChange(keyName, targetDoc);
                        MarkKeyAsModified(targetDoc.LanguageTag, keyName);
                        if (lstKeyEditorKeys != null && lstKeyEditorKeys.IsHandleCreated)
                        {
                            lstKeyEditorKeys.Invalidate();
                        }
                    }

                    lblStatusCount.Text = $"✅ Successfully translated '{keyName}' in [{targetDoc.LanguageTag}] via {sConfig.DisplayName}.";
                }
                else
                {
                    string err = !string.IsNullOrEmpty(result?.ErrorMessage)
                        ? result.ErrorMessage
                        : (result?.Items != null && result.Items.Count > 0 && !string.IsNullOrEmpty(result.Items[0].ErrorMessage)
                            ? result.Items[0].ErrorMessage
                            : "Translation service returned an empty response.");
                    MessageBox.Show($"Translation failed:\n{err}", "Instant Translation Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    lblStatusCount.Text = "❌ Instant translation failed.";
                }
            }
            catch (Exception ex)
            {
                Cursor = Cursors.Default;
                MessageBox.Show($"Error executing translation:\n{ex.Message}", "Translation Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                lblStatusCount.Text = "❌ Translation error.";
            }
        }

        private void OpenOnlineTranslator(string provider, string text, string targetIsoCode)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                MessageBox.Show("There is no text value in this entry to translate.", "Online Translation", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            try
            {
                string encodedText = Uri.EscapeDataString(text);
                string url = string.Empty;

                switch (provider.ToLowerInvariant())
                {
                    case "deepl":
                        url = $"https://www.deepl.com/translator#auto/{targetIsoCode}/{encodedText}";
                        break;
                    case "bing":
                        url = $"https://www.bing.com/translator?from=auto&to={targetIsoCode}&text={encodedText}";
                        break;
                    case "yandex":
                        url = $"https://translate.yandex.com/?lang=auto-{targetIsoCode}&text={encodedText}";
                        break;
                    case "google":
                    default:
                        url = $"https://translate.google.com/?sl=auto&tl={targetIsoCode}&text={encodedText}&op=translate";
                        break;
                }

                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not open {provider} in browser:\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void OpenAutoTranslationDialog(List<string> selectedKeys = null)
        {
            var configuredServices = CsfStudio.Core.Translation.TranslationConfigManager.ConfiguredServices?.Where(s => s.IsEnabled).ToList();
            if (configuredServices == null || configuredServices.Count == 0)
            {
                var settingsDlg = new CsfStudio.UI.TranslationSettingsForm();
                if (settingsDlg.ShowDialog(this) == DialogResult.OK)
                {
                    BuildTranslationSubmenus();
                }
                return;
            }

            var serviceConfig = EnsureTranslationServiceConfigured(configuredServices[0]);
            if (serviceConfig == null) return;
            var scrollState = SaveCurrentViewScrollState();
            var dlg = new CsfStudio.UI.TranslationServiceForm(_session, serviceConfig, selectedKeys);
            dlg.ShowDialog(this);
            if (dlg.TranslationUndoBatch != null && dlg.TranslationUndoBatch.Commands.Count > 0)
            {
                _undoManager.Execute(dlg.TranslationUndoBatch, _session);
                UpdateUndoRedoMenuItems();
            }
            InvalidateMasterRowsCache();
            RefreshDataAfterTextTranslation(selectedKeys);
            RestoreViewScrollState(scrollState);
        }

        private void SaveSessionViewStateToConfig()
        {
            if (_session == null || _session.Documents.Count == 0 || _appConfig == null) return;

            string unpinnedStr = string.Join(",", _unpinnedTargetLanguageTags);

            string lastKeyStr = (_lastActiveSelectedKeys != null && _lastActiveSelectedKeys.Count > 0)
                ? string.Join(";", _lastActiveSelectedKeys)
                : string.Empty;

            string activeTab = tabControlMain?.SelectedTab?.Name ?? "tabMaster";

            RecentSessionsManager.AddRecentSession(_session, _appConfig.MaxRecentSessionsItems, unpinnedStr, lastKeyStr, activeTab, _lastSelectedTargetLanguageTag ?? string.Empty);
        }

        private void RestoreSessionViewStateFromConfig()
        {
            if (_session == null || _session.Documents.Count == 0) return;

            var recent = RecentSessionsManager.FindRecentSession(_session);
            if (recent != null)
            {
                // 1. Restore unpinned split-view target language tags
                if (!string.IsNullOrEmpty(recent.UnpinnedLanguageTags))
                {
                    var tags = recent.UnpinnedLanguageTags
                        .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(t => t.Trim())
                        .Where(t => !string.IsNullOrEmpty(t));

                    _unpinnedTargetLanguageTags.Clear();
                    foreach (var tag in tags)
                    {
                        _unpinnedTargetLanguageTags.Add(tag);
                    }
                }

                if (!string.IsNullOrEmpty(recent.ActivePinnedLanguageTag))
                {
                    _lastSelectedTargetLanguageTag = recent.ActivePinnedLanguageTag;
                }

                // 2. Parse target keys FIRST
                List<string> targetKeys = null;
                if (!string.IsNullOrEmpty(recent.LastSelectedKeyName))
                {
                    targetKeys = recent.LastSelectedKeyName
                        .Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(k => k.Trim())
                        .Where(k => !string.IsNullOrEmpty(k))
                        .ToList();
                }

                // 3. Restore active tab without triggering premature selection wipes
                if (!string.IsNullOrEmpty(recent.ActiveTabName) && tabControlMain != null)
                {
                    var targetTab = tabControlMain.TabPages.Cast<TabPage>()
                        .FirstOrDefault(t => string.Equals(t.Name, recent.ActiveTabName, StringComparison.OrdinalIgnoreCase));
                    if (targetTab != null)
                    {
                        if (tabControlMain.SelectedTab != targetTab)
                        {
                            bool prevSync = _isSyncingSelection;
                            _isSyncingSelection = true;
                            try
                            {
                                tabControlMain.SelectedTab = targetTab;
                            }
                            finally
                            {
                                _isSyncingSelection = prevSync;
                            }
                        }
                    }
                }

                // 4. Force active tab population (populates panels & grids for restored tab)
                OnMainTabSelectedIndexChanged();

                // 5. Apply selection state via unified controller
                ApplySelectionState(targetKeys);
            }
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern IntPtr GetFocus();

        private Control GetFocusedControl()
        {
            IntPtr handle = GetFocus();
            if (handle != IntPtr.Zero)
            {
                Control c = Control.FromHandle(handle);
                if (c != null) return c;
            }
            var ctrl = ActiveControl;
            while (ctrl is ContainerControl container && container.ActiveControl != null)
            {
                ctrl = container.ActiveControl;
            }
            return ctrl;
        }

        private void menuSelectAll_Click(object sender, EventArgs e)
        {
            var focused = GetFocusedControl();
            if (focused is TextBoxBase txt)
            {
                txt.SelectAll();
                return;
            }
            if (focused is ComboBox cbo)
            {
                cbo.SelectAll();
                return;
            }

            if (tabControlMain != null && tabControlMain.SelectedTab == tabKeyEditor && lstKeyEditorKeys != null)
            {
                lstKeyEditorKeys.BeginUpdate();
                for (int i = 0; i < lstKeyEditorKeys.Items.Count; i++)
                {
                    lstKeyEditorKeys.SetSelected(i, true);
                }
                lstKeyEditorKeys.EndUpdate();
                return;
            }

            var grid = GetActiveGridForTab(tabControlMain?.SelectedTab) ?? gridLabels;
            if (grid != null && grid.Rows.Count > 0)
            {
                grid.SelectAll();
                OnGridSelectionChanged(grid);
            }
        }

        private void menuInvertSelection_Click(object sender, EventArgs e)
        {
            var focused = GetFocusedControl();
            if (focused is TextBoxBase || focused is ComboBox)
            {
                return;
            }

            if (tabControlMain != null && tabControlMain.SelectedTab == tabKeyEditor && lstKeyEditorKeys != null)
            {
                lstKeyEditorKeys.BeginUpdate();
                for (int i = 0; i < lstKeyEditorKeys.Items.Count; i++)
                {
                    lstKeyEditorKeys.SetSelected(i, !lstKeyEditorKeys.GetSelected(i));
                }
                lstKeyEditorKeys.EndUpdate();
                return;
            }

            var grid = GetActiveGridForTab(tabControlMain?.SelectedTab) ?? gridLabels;
            if (grid != null && grid.Rows.Count > 0)
            {
                grid.SuspendLayout();
                try
                {
                    foreach (DataGridViewRow r in grid.Rows)
                    {
                        r.Selected = !r.Selected;
                    }
                }
                finally
                {
                    grid.ResumeLayout();
                }
                OnGridSelectionChanged(grid);
            }
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            var focused = GetFocusedControl();
            bool isTextInputFocused = focused is TextBoxBase ||
                                     focused is ComboBox ||
                                     (focused != null && focused.Parent is ComboBox);

            if (keyData == Keys.Delete)
            {
                if (isTextInputFocused)
                {
                    // Allow text boxes and combo boxes to perform native character deletion without triggering Delete Key dialog
                    return false;
                }
                else if (menuDeleteLabel != null && menuDeleteLabel.Enabled)
                {
                    menuDeleteLabel_Click(this, EventArgs.Empty);
                    return true;
                }
            }
            else if (keyData == (Keys.Control | Keys.Z))
            {
                if (focused is TextBoxBase tb && !tb.ReadOnly)
                {
                    if (tb.CanUndo) tb.Undo();
                    return true;
                }
                else if (isTextInputFocused)
                {
                    return true;
                }
                else if (menuUndo != null && menuUndo.Enabled)
                {
                    menuUndo_Click(this, EventArgs.Empty);
                    return true;
                }
            }
            else if (keyData == (Keys.Control | Keys.Y))
            {
                if (isTextInputFocused)
                {
                    return true;
                }
                else if (menuRedo != null && menuRedo.Enabled)
                {
                    menuRedo_Click(this, EventArgs.Empty);
                    return true;
                }
            }
            else if (keyData == (Keys.Control | Keys.C))
            {
                if (focused is TextBoxBase tb)
                {
                    if (tb.SelectionLength > 0) Clipboard.SetText(tb.SelectedText);
                    else if (!string.IsNullOrEmpty(tb.Text)) Clipboard.SetText(tb.Text);
                    return true;
                }
                else if (focused is ComboBox cbo)
                {
                    if (cbo.SelectionLength > 0) Clipboard.SetText(cbo.SelectedText);
                    else if (!string.IsNullOrEmpty(cbo.Text)) Clipboard.SetText(cbo.Text);
                    return true;
                }
                else if (focused != null && focused.Parent is ComboBox pCbo)
                {
                    if (pCbo.SelectionLength > 0) Clipboard.SetText(pCbo.SelectedText);
                    else if (!string.IsNullOrEmpty(pCbo.Text)) Clipboard.SetText(pCbo.Text);
                    return true;
                }
                else if (focused is DataGridView dgv)
                {
                    string val = null;
                    if (dgv.CurrentCell != null && dgv.CurrentCell.Value != null)
                    {
                        val = dgv.CurrentCell.Value.ToString();
                    }
                    if (string.IsNullOrEmpty(val) && dgv.SelectedRows.Count > 0)
                    {
                        if (dgv.SelectedRows[0].Tag is MasterKeyRow mRow) val = mRow.KeyName;
                        else if (dgv.SelectedRows[0].Cells.Count > 0 && dgv.SelectedRows[0].Cells[0].Value != null) val = dgv.SelectedRows[0].Cells[0].Value.ToString();
                    }
                    if (!string.IsNullOrEmpty(val))
                    {
                        Clipboard.SetText(val);
                        return true;
                    }
                }
                else if (focused is ListBox lb && lb.SelectedItem != null)
                {
                    string val = lb.SelectedItem.ToString();
                    if (!string.IsNullOrEmpty(val))
                    {
                        Clipboard.SetText(val);
                        return true;
                    }
                }
            }
            else if (keyData == (Keys.Control | Keys.V))
            {
                if (Clipboard.ContainsText())
                {
                    string pasteText = Clipboard.GetText();

                    if (focused is TextBoxBase tb && !tb.ReadOnly)
                    {
                        tb.SelectedText = pasteText;
                        return true;
                    }
                    else if (focused is ComboBox cbo && cbo.DropDownStyle != ComboBoxStyle.DropDownList)
                    {
                        cbo.SelectedText = pasteText;
                        return true;
                    }
                    else if (focused != null && focused.Parent is ComboBox pCbo && pCbo.DropDownStyle != ComboBoxStyle.DropDownList)
                    {
                        pCbo.SelectedText = pasteText;
                        return true;
                    }
                }
            }
            else if (keyData == (Keys.Control | Keys.X))
            {
                if (focused is TextBoxBase tb && !tb.ReadOnly)
                {
                    if (tb.SelectionLength > 0)
                    {
                        Clipboard.SetText(tb.SelectedText);
                        tb.SelectedText = string.Empty;
                        return true;
                    }
                }
                else if (focused is ComboBox cbo && cbo.DropDownStyle != ComboBoxStyle.DropDownList)
                {
                    if (cbo.SelectionLength > 0)
                    {
                        Clipboard.SetText(cbo.SelectedText);
                        cbo.SelectedText = string.Empty;
                        return true;
                    }
                }
                else if (focused != null && focused.Parent is ComboBox pCbo && pCbo.DropDownStyle != ComboBoxStyle.DropDownList)
                {
                    if (pCbo.SelectionLength > 0)
                    {
                        Clipboard.SetText(pCbo.SelectedText);
                        pCbo.SelectedText = string.Empty;
                        return true;
                    }
                }
            }
            else if (keyData == (Keys.Control | Keys.A))
            {
                if (focused is TextBoxBase tb)
                {
                    tb.SelectAll();
                    return true;
                }
                else if (focused is ComboBox cbo)
                {
                    cbo.SelectAll();
                    return true;
                }
                else if (focused != null && focused.Parent is ComboBox pCbo)
                {
                    pCbo.SelectAll();
                    return true;
                }
                menuSelectAll_Click(this, EventArgs.Empty);
                return true;
            }
            else if (keyData == (Keys.Control | Keys.I))
            {
                menuInvertSelection_Click(this, EventArgs.Empty);
                return true;
            }

            return base.ProcessCmdKey(ref msg, keyData);
        }

        private const int WM_ENTERSIZEMOVE = 0x0231;
        private const int WM_EXITSIZEMOVE  = 0x0232;

        private bool _isWindowResizing = false;

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_ENTERSIZEMOVE)
            {
                _isWindowResizing = true;
                if (this.IsHandleCreated)
                {
                    SendMessage(this.Handle, WM_SETREDRAW, IntPtr.Zero, IntPtr.Zero);
                }
            }
            else if (m.Msg == WM_EXITSIZEMOVE)
            {
                _isWindowResizing = false;
                if (this.IsHandleCreated)
                {
                    PerformActiveInspectorResizePass();
                    SendMessage(this.Handle, WM_SETREDRAW, (IntPtr)1, IntPtr.Zero);
                    this.PerformLayout();
                    this.Refresh();
                }
            }
            base.WndProc(ref m);
        }

        private void PerformActiveInspectorResizePass()
        {
            if (pnlLanguageEditors == null || pnlLanguageEditors.Controls.Count == 0) return;
            foreach (Control c in pnlLanguageEditors.Controls)
            {
                if (c is Panel pnl && pnl.Visible)
                {
                    int containerWidth = Math.Max(350, pnl.ClientSize.Width - 25);
                    pnl.SuspendLayout();
                    foreach (Control ctrl in pnl.Controls)
                    {
                        if (ctrl is GroupBox grp) grp.Width = containerWidth;
                        else if (ctrl is Panel banner) banner.Width = containerWidth;
                    }
                    pnl.ResumeLayout(false);
                }
            }
        }

        #endregion
    }
}
