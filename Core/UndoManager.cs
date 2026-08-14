using System;
using System.Collections.Generic;
using System.Linq;

namespace CsfStudio.Core
{
    public interface IUndoCommand
    {
        string Description { get; }
        string TargetLanguageTag { get; }
        string TargetKeyName { get; }
        void Undo(CsfSession session);
        void Redo(CsfSession session);
    }

    public class UndoManager
    {
        private readonly Stack<IUndoCommand> _undoStack = new Stack<IUndoCommand>();
        private readonly Stack<IUndoCommand> _redoStack = new Stack<IUndoCommand>();
        private bool _isExecutingUndoRedo = false;

        public bool IsExecutingUndoRedo => _isExecutingUndoRedo;
        public bool CanUndo => _undoStack.Count > 0;
        public bool CanRedo => _redoStack.Count > 0;

        public string UndoDescription => CanUndo ? _undoStack.Peek().Description : "";
        public string RedoDescription => CanRedo ? _redoStack.Peek().Description : "";

        public IUndoCommand PeekUndo() => CanUndo ? _undoStack.Peek() : null;
        public IUndoCommand PeekRedo() => CanRedo ? _redoStack.Peek() : null;

        public void Execute(IUndoCommand command, CsfSession session, int maxUndoLevels = 100)
        {
            if (command == null || _isExecutingUndoRedo) return;
            int limit = Math.Max(10, Math.Min(1000, maxUndoLevels));

            _undoStack.Push(command);
            _redoStack.Clear();

            if (_undoStack.Count > limit)
            {
                var list = _undoStack.ToList();
                list.RemoveAt(list.Count - 1);
                _undoStack.Clear();
                for (int i = list.Count - 1; i >= 0; i--)
                {
                    _undoStack.Push(list[i]);
                }
            }
        }

        public IUndoCommand PerformUndo(CsfSession session)
        {
            if (!CanUndo || _isExecutingUndoRedo) return null;

            _isExecutingUndoRedo = true;
            try
            {
                var cmd = _undoStack.Pop();
                cmd.Undo(session);
                _redoStack.Push(cmd);
                return cmd;
            }
            finally
            {
                _isExecutingUndoRedo = false;
            }
        }

        public IUndoCommand PerformRedo(CsfSession session)
        {
            if (!CanRedo || _isExecutingUndoRedo) return null;

            _isExecutingUndoRedo = true;
            try
            {
                var cmd = _redoStack.Pop();
                cmd.Redo(session);
                _undoStack.Push(cmd);
                return cmd;
            }
            finally
            {
                _isExecutingUndoRedo = false;
            }
        }

        public void Clear()
        {
            _undoStack.Clear();
            _redoStack.Clear();
        }
    }

    public class EditValueCommand : IUndoCommand
    {
        public string Description => string.Format(
            LanguageManager.GetString("Undo.EditValue", "Edit '{0}' [{1}]"),
            TargetKeyName,
            TargetLanguageTag);
        public string TargetLanguageTag { get; }
        public string TargetKeyName { get; }
        public string OldValue { get; }
        public string NewValue { get; }
        public string OldExtra { get; }
        public string NewExtra { get; }

        public EditValueCommand(string langTag, string keyName, string oldValue, string newValue, string oldExtra = null, string newExtra = null)
        {
            TargetLanguageTag = langTag;
            TargetKeyName = keyName;
            OldValue = oldValue ?? string.Empty;
            NewValue = newValue ?? string.Empty;
            OldExtra = oldExtra;
            NewExtra = newExtra;
        }

        public void Undo(CsfSession session)
        {
            var sDoc = session.Documents.FirstOrDefault(d => string.Equals(d.LanguageTag, TargetLanguageTag, StringComparison.OrdinalIgnoreCase))
                       ?? session.BaseDocument
                       ?? session.Documents.FirstOrDefault();
            if (sDoc?.Document == null) return;

            var lbl = sDoc.Document.Labels.FirstOrDefault(l => string.Equals(l.Name, TargetKeyName, StringComparison.OrdinalIgnoreCase));
            if (lbl == null)
            {
                lbl = new CsfLabel(TargetKeyName);
                sDoc.Document.Labels.Add(lbl);
            }

            lbl.Strings.Clear();
            lbl.Strings.Add(new CsfStringEntry(OldValue, OldExtra));
            sDoc.IsModified = true;
        }

        public void Redo(CsfSession session)
        {
            var sDoc = session.Documents.FirstOrDefault(d => string.Equals(d.LanguageTag, TargetLanguageTag, StringComparison.OrdinalIgnoreCase))
                       ?? session.BaseDocument
                       ?? session.Documents.FirstOrDefault();
            if (sDoc?.Document == null) return;

            var lbl = sDoc.Document.Labels.FirstOrDefault(l => string.Equals(l.Name, TargetKeyName, StringComparison.OrdinalIgnoreCase));
            if (lbl == null)
            {
                lbl = new CsfLabel(TargetKeyName);
                sDoc.Document.Labels.Add(lbl);
            }

            lbl.Strings.Clear();
            lbl.Strings.Add(new CsfStringEntry(NewValue, NewExtra));
            sDoc.IsModified = true;
        }
    }

    public class AddKeyCommand : IUndoCommand
    {
        public string Description => string.Format(
            LanguageManager.GetString("Undo.AddKey", "Add key '{0}'"),
            TargetKeyName);
        public string TargetLanguageTag { get; }
        public string TargetKeyName { get; }

        public AddKeyCommand(string keyName, string firstLangTag = null)
        {
            TargetKeyName = keyName;
            TargetLanguageTag = firstLangTag;
        }

        public void Undo(CsfSession session)
        {
            foreach (var sDoc in session.Documents)
            {
                var lbl = sDoc.Document.Labels.FirstOrDefault(l => string.Equals(l.Name, TargetKeyName, StringComparison.OrdinalIgnoreCase));
                if (lbl != null)
                {
                    sDoc.Document.Labels.Remove(lbl);
                    sDoc.IsModified = true;
                }
            }
        }

        public void Redo(CsfSession session)
        {
            foreach (var sDoc in session.Documents)
            {
                var lbl = sDoc.Document.Labels.FirstOrDefault(l => string.Equals(l.Name, TargetKeyName, StringComparison.OrdinalIgnoreCase));
                if (lbl == null)
                {
                    sDoc.Document.Labels.Add(new CsfLabel(TargetKeyName, string.Empty));
                    sDoc.IsModified = true;
                }
            }
        }
    }

    public class DeleteKeyCommand : IUndoCommand
    {
        public class KeyDataBackup
        {
            public string KeyName { get; set; }
            public Dictionary<string, (string Value, string Extra)> ValuesPerLanguage { get; set; } = new Dictionary<string, (string, string)>(StringComparer.OrdinalIgnoreCase);
        }

        public string Description => KeysData.Count == 1
            ? string.Format(LanguageManager.GetString("Undo.DeleteKeySingle", "Delete key '{0}'"), KeysData[0].KeyName)
            : string.Format(LanguageManager.GetString("Undo.DeleteKeyPlural", "Delete {0} keys"), KeysData.Count);

        public string TargetLanguageTag => KeysData.FirstOrDefault()?.ValuesPerLanguage.Keys.FirstOrDefault();
        public string TargetKeyName => KeysData.FirstOrDefault()?.KeyName;

        public List<KeyDataBackup> KeysData { get; } = new List<KeyDataBackup>();

        public DeleteKeyCommand(List<KeyDataBackup> keysData)
        {
            if (keysData != null)
            {
                KeysData = keysData;
            }
        }

        public void Undo(CsfSession session)
        {
            foreach (var keyData in KeysData)
            {
                foreach (var sDoc in session.Documents)
                {
                    if (sDoc.Document == null) continue;
                    var lbl = sDoc.Document.Labels.FirstOrDefault(l => string.Equals(l.Name, keyData.KeyName, StringComparison.OrdinalIgnoreCase));
                    if (lbl == null)
                    {
                        lbl = new CsfLabel(keyData.KeyName);
                        sDoc.Document.Labels.Add(lbl);
                    }

                    if (keyData.ValuesPerLanguage.TryGetValue(sDoc.LanguageTag, out var valTuple))
                    {
                        lbl.Strings.Clear();
                        lbl.Strings.Add(new CsfStringEntry(valTuple.Value, valTuple.Extra));
                    }
                    sDoc.IsModified = true;
                }
            }
        }

        public void Redo(CsfSession session)
        {
            foreach (var keyData in KeysData)
            {
                foreach (var sDoc in session.Documents)
                {
                    if (sDoc.Document == null) continue;
                    var lbl = sDoc.Document.Labels.FirstOrDefault(l => string.Equals(l.Name, keyData.KeyName, StringComparison.OrdinalIgnoreCase));
                    if (lbl != null)
                    {
                        sDoc.Document.Labels.Remove(lbl);
                        sDoc.IsModified = true;
                    }
                }
            }
        }
    }

    public class RenameKeyCommand : IUndoCommand
    {
        public string Description => string.Format(
            LanguageManager.GetString("Undo.RenameKey", "Rename '{0}' -> '{1}'"),
            OldKeyName,
            NewKeyName);
        public string TargetLanguageTag { get; }
        public string TargetKeyName => NewKeyName;
        public string OldKeyName { get; }
        public string NewKeyName { get; }

        public RenameKeyCommand(string oldKeyName, string newKeyName, string langTag = null)
        {
            OldKeyName = oldKeyName;
            NewKeyName = newKeyName;
            TargetLanguageTag = langTag;
        }

        public void Undo(CsfSession session)
        {
            foreach (var sDoc in session.Documents)
            {
                if (sDoc.Document == null) continue;
                var lbl = sDoc.Document.Labels.FirstOrDefault(l => string.Equals(l.Name, NewKeyName, StringComparison.OrdinalIgnoreCase));
                if (lbl != null)
                {
                    lbl.Name = OldKeyName;
                    sDoc.IsModified = true;
                }
            }
        }

        public void Redo(CsfSession session)
        {
            foreach (var sDoc in session.Documents)
            {
                if (sDoc.Document == null) continue;
                var lbl = sDoc.Document.Labels.FirstOrDefault(l => string.Equals(l.Name, OldKeyName, StringComparison.OrdinalIgnoreCase));
                if (lbl != null)
                {
                    lbl.Name = NewKeyName;
                    sDoc.IsModified = true;
                }
            }
        }
    }

    public class BatchUndoCommand : IUndoCommand
    {
        public string Description { get; }
        public List<IUndoCommand> Commands { get; } = new List<IUndoCommand>();

        public string TargetLanguageTag => Commands.FirstOrDefault()?.TargetLanguageTag;
        public string TargetKeyName => Commands.FirstOrDefault()?.TargetKeyName;

        public BatchUndoCommand(string description) : this(description, null) { }

        public BatchUndoCommand(string description, IEnumerable<IUndoCommand> commands)
        {
            Description = description ?? LanguageManager.GetString("Undo.BatchOperation", "Batch Operation");
            if (commands != null)
            {
                Commands.AddRange(commands);
            }
        }

        public void AddCommand(IUndoCommand command)
        {
            if (command != null) Commands.Add(command);
        }

        public void Undo(CsfSession session)
        {
            for (int i = Commands.Count - 1; i >= 0; i--)
            {
                Commands[i].Undo(session);
            }
        }

        public void Redo(CsfSession session)
        {
            for (int i = 0; i < Commands.Count; i++)
            {
                Commands[i].Redo(session);
            }
        }
    }

    public class ReorderKeyCommand : IUndoCommand
    {
        public string Description => KeyNames != null && KeyNames.Count > 1
            ? string.Format(LanguageManager.GetString("Undo.MoveKeysPlural", "Move {0} keys"), KeyNames.Count)
            : string.Format(LanguageManager.GetString("Undo.MoveKeySingle", "Move '{0}'"), TargetKeyName);
        public string TargetLanguageTag => null;
        public string TargetKeyName => KeyNames != null && KeyNames.Count > 0 ? KeyNames[0] : null;
        public List<string> KeyNames { get; }
        public int Direction { get; }
        public Action<string, int, int> OnPositionChanged { get; set; }

        public ReorderKeyCommand(List<string> keyNames, int direction, Action<string, int, int> onPositionChanged = null)
        {
            KeyNames = keyNames != null ? new List<string>(keyNames) : new List<string>();
            Direction = direction;
            OnPositionChanged = onPositionChanged;
        }

        public void Undo(CsfSession session)
        {
            PerformMove(session, -Direction);
        }

        public void Redo(CsfSession session)
        {
            PerformMove(session, Direction);
        }

        private void PerformMove(CsfSession session, int dir)
        {
            if (session?.BaseDocument?.Document == null || KeyNames == null || KeyNames.Count == 0) return;
            var labels = session.BaseDocument.Document.Labels;

            var oldPositions = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var k in KeyNames)
            {
                int idx = labels.FindIndex(l => string.Equals(l.Name, k, StringComparison.OrdinalIgnoreCase));
                if (idx >= 0) oldPositions[k] = idx + 1;
            }

            var indexList = oldPositions.Values.Select(p => p - 1).ToList();
            if (indexList.Count == 0) return;
            indexList.Sort();

            int firstIdx = indexList[0];
            int lastIdx = indexList[indexList.Count - 1];

            if (dir < 0)
            {
                if (firstIdx <= 0) return;
                var itemAbove = labels[firstIdx - 1];
                labels.RemoveAt(firstIdx - 1);
                labels.Insert(lastIdx, itemAbove);
            }
            else if (dir > 0)
            {
                if (lastIdx >= labels.Count - 1) return;
                var itemBelow = labels[lastIdx + 1];
                labels.RemoveAt(lastIdx + 1);
                labels.Insert(firstIdx, itemBelow);
            }
            else
            {
                return;
            }

            session.BaseDocument.IsModified = true;

            if (OnPositionChanged != null)
            {
                foreach (var k in KeyNames)
                {
                    int newIdx = labels.FindIndex(l => string.Equals(l.Name, k, StringComparison.OrdinalIgnoreCase));
                    if (newIdx >= 0 && oldPositions.TryGetValue(k, out int oldPos))
                    {
                        OnPositionChanged(k, oldPos, newIdx + 1);
                    }
                }
            }
        }
    }
}
