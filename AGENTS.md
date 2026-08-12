# AGENTS.md

**CSF Studio** — .NET Framework 4.8 WinForms editor for C&C: Red Alert 2 / Yuri's Revenge `.csf` string-table files, with multi-language session compare and machine-translation integration.

## Build & verify

- Build: `& "C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe" CSFStudio.sln /p:Configuration=Debug` (or open `CSFStudio.sln` in Visual Studio). Old-style non-SDK csproj — do not use `dotnet build`.
- **Close any running `CSF Studio.exe` before building** — the output exe is locked while the app runs and the build fails at the copy step (MSB3021).
- There are **no tests, no CI, no linter** — verification = clean build + manual run of `bin\Debug\CSF Studio.exe`.
- This is **not a git repository**.

## Project mechanics (easy to get wrong)

- `CSFStudio.csproj` lists every source file explicitly — **new `.cs` files must be added to the csproj by hand**; there is no globbing. No NuGet packages; only framework references.
- Max language version is **C# 7.3** (non-SDK net48 default): no switch expressions, ranges, or nullable reference annotations.
- Assembly name is `CSF Studio` (with a space); root namespace is `CsfStudio`.
- `bin\Debug\Translations\` holds real user game assets (.csf/.mix/.xnb) used for manual testing — do not delete or "clean" `bin\` blindly.

## Architecture

- Entry: `Program.cs` → `UI/MainForm.cs`. Command-line args are file paths to open on startup (drag-drop / file association).
- `Core/` = UI-independent logic; `UI/` = forms. Keep this split.
- `UI/MainForm.cs` is ~8000 lines. Navigate it by `#region` (Session & Document Management, Master Grid & Filtering, Plain Key View, Inspector, Event Handlers, Backups, Translation) instead of reading it whole.
- Most dialogs hand-write `InitializeComponent()` inside their single `.cs` file. Only `MainForm` and `FindReplaceDialog` have `.Designer.cs` files — follow the existing per-file pattern; don't add Designer files to dialogs that don't have them.
- Session model (`Core/CsfSession.cs`): multiple open CSF documents, one per language; `Documents[0]` is always the base/master reference, and the master key list is the union of keys across all documents. Key names are compared case-insensitively.
- Undo (`Core/UndoManager.cs`) is command-pattern: implement `IUndoCommand` and push via `UndoManager.Execute`; never mutate documents directly on an undoable action.
- Translation (`Core/Translation/`): `ITranslationProvider` implementations + `TranslationProviderFactory` (Google web scrape, DeepL, OpenAI-compatible endpoints).
- `CsfSessionDocument.TranslationContentLanguage` is the per-session content-language override for `LanguageNeutral` CSF headers; it is persisted with recent sessions, not `settings.ini`. Do not use `LanguageTag` as the text language when this override is present.

## Performance layer (do not break these invariants)

- `MainForm` caches the master key list: `GetMasterRows()` / `GetMasterRowsMap()`. **Any structural change** (add/delete/rename/sync keys, import, reorder, session load) must go through `RebuildCategoryTreeAndGrid()` or fire `CsfSession.SessionChanged` — both call `InvalidateMasterRowsCache()`. In-place value edits (`entry.Value = ...`) need no invalidation: `MasterKeyRow` entries are shared by reference and `MasterKeyRow.Status` is a live computed property (read-only — do not add a setter).
- Tabs populate lazily: `RebuildCategoryTreeAndGrid` only fills the visible tab and sets `_unsavedDirty/_coverageDirty/_recentDirty/_keyEditorDirty`; `OnMainTabSelectedIndexChanged` populates on switch. If you add a mutation path that bypasses `RebuildCategoryTreeAndGrid`/`MarkKeyAsModified`, set the dirty flags yourself.
- `gridLabels` columns are recreated only when `_masterGridColumnSignature` changes; keep row cell order consistent with the existing column layout.
- `_backupScanValid/_backupScanBasePath` cache the on-disk snapshot scan; `PopulateBackupsTab()` resets it.
- Inspector/multi-key editor construction is **deferred**: `OnGridSelectionChanged`/`OnKeyEditorSelectionChanged` must call `ScheduleEditorBuild(...)` (25 ms timer, last request wins), never `BuildSideBySideEditors`/`BuildMultiKeyEditors` directly. Any path that clears/deselects must also null `_pendingEditorBuild` (see `ClearDetailInspector`).
- Single-entry instant translation updates **in place** via `UpdateGridRowAfterValueChange` (cached row + grid cell) and `MarkKeyAsModified` — never call `RebuildCategoryTreeAndGrid` or repopulate lists after a one-key edit; that is what broke grid selection.
- CSF file parsing is parallelized (`Parallel.For`) in session loads; parsing is stateless, but `ToolTipHelper.CheckAndPromptUnknownLanguage` and `_session.AddDocument` must stay on the UI thread.

## CSF binary format (Core/CsfFileHandler.cs)

- String values are stored obfuscated: each char written as **bitwise-NOT of its UTF-16LE code unit**. Label names and `STRW` extra values are plain ASCII.
- `STR ` record = value only; `STRW` record = value + extra ASCII string. Header language ID maps to the `CsfLanguage` enum.

## settings.ini gotchas

- Parsed/written by the hand-rolled `ConfigManager` (sections are hardcoded in a switch). Location is beside the exe or `%APPDATA%\CSF Studio` depending on `SaveInAppData`.
- On every save, a copy is also written to `<exe>\..\..\settings.ini` — the **repo-root `settings.ini` is a regenerated runtime artifact**, not source of truth; expect it to change after each app run.
- Translation provider sections (any section not in the known list) live in the same file and store **API keys in plaintext** — never commit or expose this file.
- History lists in the ini are delimited by `|||` (not pipes or commas).
