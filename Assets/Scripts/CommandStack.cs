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

public sealed class CompositeEditorCommand : IEditorCommand, IDiscardableEditorCommand
{
    readonly List<IEditorCommand> commands = new();
    readonly string label;

    public string Label => label;

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
    public event Action<string, string, Exception> CommandFailed;

    public int UndoCount => undo.Count;
    public int RedoCount => redo.Count;
    public bool CanUndo => undo.Count > 0;
    public bool CanRedo => redo.Count > 0;
    public string UndoLabel => CanUndo ? undo[undo.Count - 1].Label : string.Empty;
    public string RedoLabel => CanRedo ? redo[redo.Count - 1].Label : string.Empty;

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
