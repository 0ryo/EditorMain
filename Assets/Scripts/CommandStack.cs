using System;
using System.Collections.Generic;
using UnityEngine;

public interface IEditorCommand
{
    bool Do();
    bool Undo();
    string Label { get; }
}

public interface IDiscardableEditorCommand
{
    void Discard();
}

/// <summary>利用者向けの日本語の説明（例：部品「ジャッキ」の移動）。元に戻す／やり直しの通知に使う。Label は内部用。</summary>
public interface IDescribedEditorCommand
{
    string Description { get; }
}

/// <summary>部品を対象にするコマンド。複数まとめたとき「3個の部品の…」と数えるために使う。</summary>
public interface ICommandSubject
{
    object SubjectKey { get; }
    string SubjectName { get; }
}

public static class CommandDescriptions
{
    const string Fallback = "編集";

    // 部品に対する操作の動詞。主語（部品「X」／N個の部品）が付く。
    static readonly Dictionary<string, string> PartVerbs = new()
    {
        ["Move"] = "移動", ["Move selection"] = "移動", ["Move 10 cm"] = "移動",
        ["Rotate"] = "回転", ["Rotate selection"] = "回転",
        ["Scale"] = "大きさの変更", ["Scale around center"] = "大きさの変更", ["Scale selection"] = "大きさの変更",
        ["Transform"] = "位置・向き・大きさの変更", ["Transform selection"] = "位置・向き・大きさの変更",
        ["Change object transform"] = "位置・向き・大きさの変更",
        ["Delete selection"] = "削除", ["Duplicate selection"] = "複製", ["Paste selection"] = "貼り付け",
        ["Align selection"] = "整列", ["Distribute selection"] = "等間隔の配置",
        ["Group objects"] = "グループ化", ["Ungroup objects"] = "グループ解除",
        ["Edit object details"] = "名前・説明の変更",
    };

    static readonly Dictionary<string, string> Whole = new()
    {
        ["Rename project"] = "教材名の変更",
        ["Add step"] = "手順の追加", ["Delete step"] = "手順の削除", ["Edit step"] = "手順の編集",
        ["Reorder steps"] = "手順の並べ替え",
        ["Add condition"] = "完了条件の追加", ["Edit condition"] = "完了条件の変更",
        ["Set condition type"] = "完了条件の種類の変更",
        ["Bind condition"] = "完了条件の割り当て", ["Unbind condition"] = "完了条件の割り当ての解除",
        ["Delete scenario node"] = "手順または完了条件の削除", ["Delete scenario selection"] = "手順の削除",
        ["Paste scenario nodes"] = "手順の貼り付け",
        ["Connect scenario nodes"] = "手順のつながりの追加", ["Delete scenario connection"] = "手順のつながりの削除",
        ["Move scenario selection"] = "手順図の配置の変更", ["Align scenario selection"] = "手順図の整列",
        ["Auto layout scenario nodes"] = "手順図の自動整列",
        ["Edit scenario"] = "教材の編集",
    };

    public static string Describe(IEditorCommand command)
    {
        if (command == null) return Fallback;
        if (command is IDescribedEditorCommand described)
        {
            string text = described.Description;
            if (!string.IsNullOrWhiteSpace(text)) return text;
        }
        return ForLabel(command.Label);
    }

    public static string ForLabel(string label)
    {
        return ForParts(label, null);
    }

    /// <summary>label の動作を、names の部品に対して行った説明にする。names が空なら動詞だけ（または全文）を返す。</summary>
    public static string ForParts(string label, IReadOnlyList<string> names)
    {
        if (string.IsNullOrEmpty(label)) return Fallback;
        string verb = null;
        if (!PartVerbs.TryGetValue(label, out verb))
        {
            if (label.StartsWith("Place ")) verb = "配置";
            else if (label.StartsWith("Duplicate ")) verb = "複製";
            else if (label.StartsWith("Delete ") && !Whole.ContainsKey(label)) verb = "削除";
        }
        if (verb == null) return Whole.TryGetValue(label, out var whole) ? whole : Fallback;

        int count = names != null ? names.Count : 0;
        if (count == 0) return "部品の" + verb;
        if (count == 1 && !string.IsNullOrWhiteSpace(names[0])) return $"部品「{Shorten(names[0])}」の{verb}";
        return $"{count}個の部品の{verb}";
    }

    public static string Shorten(string text, int max = 16)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;
        text = text.Replace('\n', ' ').Trim();
        return text.Length <= max ? text : text.Substring(0, max) + "…";
    }
}

public sealed class CompositeEditorCommand : IEditorCommand, IDescribedEditorCommand, IDiscardableEditorCommand
{
    readonly List<IEditorCommand> commands = new();
    readonly string label;

    public string Label => label;

    public string Description
    {
        get
        {
            var keys = new HashSet<object>();
            var names = new List<string>();
            foreach (var command in commands)
            {
                if (command is ICommandSubject subject && subject.SubjectKey != null && keys.Add(subject.SubjectKey))
                    names.Add(subject.SubjectName);
            }
            return CommandDescriptions.ForParts(label, names);
        }
    }

    public CompositeEditorCommand(string label, IEnumerable<IEditorCommand> commands)
    {
        this.label = string.IsNullOrWhiteSpace(label) ? "Transaction" : label;
        if (commands == null) return;

        foreach (var command in commands)
        {
            if (command != null) this.commands.Add(command);
        }
    }

    public bool Do()
    {
        if (commands.Count == 0) return false;

        int appliedCount = 0;
        try
        {
            foreach (var command in commands)
            {
                if (!command.Do())
                {
                    RollbackApplied(appliedCount);
                    return false;
                }

                appliedCount++;
            }

            return true;
        }
        catch
        {
            RollbackApplied(appliedCount);
            throw;
        }
    }

    public bool Undo()
    {
        int firstUndoneIndex = commands.Count;
        try
        {
            for (int i = commands.Count - 1; i >= 0; i--)
            {
                if (!commands[i].Undo())
                {
                    ReapplyUndone(firstUndoneIndex);
                    return false;
                }

                firstUndoneIndex = i;
            }

            return true;
        }
        catch
        {
            ReapplyUndone(firstUndoneIndex);
            throw;
        }
    }

    public void Discard()
    {
        foreach (var command in commands)
        {
            if (command is IDiscardableEditorCommand discardable)
            {
                try
                {
                    discardable.Discard();
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                }
            }
        }
    }

    void RollbackApplied(int appliedCount)
    {
        for (int i = appliedCount - 1; i >= 0; i--)
        {
            try
            {
                if (!commands[i].Undo())
                {
                    Debug.LogError($"[CommandStack] Transaction rollback failed: {commands[i].Label}");
                }
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }
    }

    void ReapplyUndone(int firstUndoneIndex)
    {
        for (int i = firstUndoneIndex; i < commands.Count; i++)
        {
            try
            {
                if (!commands[i].Do())
                {
                    Debug.LogError($"[CommandStack] Transaction recovery failed: {commands[i].Label}");
                }
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }
    }
}

public class CommandStack
{
    public const int DefaultHistoryLimit = 100;

    readonly List<IEditorCommand> undo = new();
    readonly List<IEditorCommand> redo = new();
    int historyLimit;

    public event Action HistoryChanged;
    /// <summary>元に戻した／やり直した直後（ボタンでもキーボードでも）。引数は「やり直し」か、操作の日本語の説明。</summary>
    public event Action<bool, string> HistoryApplied;
    public event Action<string, string, Exception> CommandFailed;

    public int UndoCount => undo.Count;
    public bool CanUndo => undo.Count > 0;
    public bool CanRedo => redo.Count > 0;
    public string UndoLabel => CanUndo ? undo[undo.Count - 1].Label : string.Empty;
    public string RedoLabel => CanRedo ? redo[redo.Count - 1].Label : string.Empty;
    public string UndoDescription => CanUndo ? CommandDescriptions.Describe(undo[undo.Count - 1]) : string.Empty;
    public string RedoDescription => CanRedo ? CommandDescriptions.Describe(redo[redo.Count - 1]) : string.Empty;

    public int HistoryLimit
    {
        get => historyLimit;
        set
        {
            int normalized = Math.Max(1, value);
            if (historyLimit == normalized) return;

            historyLimit = normalized;
            bool changed = TrimToLimit(undo) | TrimToLimit(redo);
            if (changed) NotifyHistoryChanged();
        }
    }

    public CommandStack() : this(DefaultHistoryLimit)
    {
    }

    public CommandStack(int historyLimit)
    {
        this.historyLimit = Math.Max(1, historyLimit);
    }

    public bool Execute(IEditorCommand command)
    {
        if (!TryRun(command, "Execute", c => c.Do())) return false;

        DiscardAll(redo);
        undo.Add(command);
        TrimToLimit(undo);
        NotifyHistoryChanged();
        return true;
    }

    public bool ExecuteTransaction(string label, params IEditorCommand[] commands)
    {
        return Execute(new CompositeEditorCommand(label, commands));
    }

    public bool RecordApplied(IEditorCommand command)
    {
        if (command == null)
        {
            ReportFailure("(null)", "Record", new ArgumentNullException(nameof(command)));
            return false;
        }

        DiscardAll(redo);
        undo.Add(command);
        TrimToLimit(undo);
        NotifyHistoryChanged();
        return true;
    }

    public bool Undo()
    {
        if (!CanUndo) return false;

        var command = undo[undo.Count - 1];
        if (!TryRun(command, "Undo", c => c.Undo())) return false;

        undo.RemoveAt(undo.Count - 1);
        redo.Add(command);
        NotifyHistoryChanged();
        NotifyHistoryApplied(false, command);
        return true;
    }

    public bool Redo()
    {
        if (!CanRedo) return false;

        var command = redo[redo.Count - 1];
        if (!TryRun(command, "Redo", c => c.Do())) return false;

        redo.RemoveAt(redo.Count - 1);
        undo.Add(command);
        NotifyHistoryChanged();
        NotifyHistoryApplied(true, command);
        return true;
    }

    public void Clear()
    {
        bool changed = undo.Count > 0 || redo.Count > 0;
        DiscardAll(undo);
        DiscardAll(redo);
        if (changed) NotifyHistoryChanged();
    }

    bool TryRun(IEditorCommand command, string operation, Func<IEditorCommand, bool> action)
    {
        if (command == null)
        {
            ReportFailure("(null)", operation, new ArgumentNullException(nameof(command)));
            return false;
        }

        try
        {
            if (action(command)) return true;

            ReportFailure(command.Label, operation, null);
            return false;
        }
        catch (Exception ex)
        {
            ReportFailure(command.Label, operation, ex);
            return false;
        }
    }

    bool TrimToLimit(List<IEditorCommand> commands)
    {
        bool changed = false;
        while (commands.Count > historyLimit)
        {
            var command = commands[0];
            commands.RemoveAt(0);
            Discard(command);
            changed = true;
        }

        return changed;
    }

    static void DiscardAll(List<IEditorCommand> commands)
    {
        foreach (var command in commands)
        {
            Discard(command);
        }
        commands.Clear();
    }

    static void Discard(IEditorCommand command)
    {
        var discardable = command as IDiscardableEditorCommand;
        if (discardable == null) return;

        try
        {
            discardable.Discard();
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
        }
    }

    void NotifyHistoryChanged()
    {
        try
        {
            HistoryChanged?.Invoke();
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
        }
    }

    void NotifyHistoryApplied(bool redone, IEditorCommand command)
    {
        try
        {
            HistoryApplied?.Invoke(redone, CommandDescriptions.Describe(command));
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
        }
    }

    void ReportFailure(string label, string operation, Exception exception)
    {
        string message = $"[CommandStack] {operation} failed: {label}";
        if (exception != null) Debug.LogException(exception);
        else Debug.LogError(message);

        try
        {
            CommandFailed?.Invoke(label, operation, exception);
        }
        catch (Exception eventException)
        {
            Debug.LogException(eventException);
        }
    }
}
