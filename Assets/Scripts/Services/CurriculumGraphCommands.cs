using System;
using UnityEngine;

internal static class CurriculumGraphCommandProcessor
{
    public static bool Execute(CurriculumGraphService graph, string label, Func<bool> mutation)
    {
        if (mutation == null) return false;

        string before = graph.CaptureCommandSnapshot();
        bool succeeded;
        try
        {
            succeeded = mutation();
        }
        catch (Exception ex)
        {
            RestoreAfterFailedMutation(graph, before);
            Debug.LogException(ex);
            return false;
        }

        string after = graph.CaptureCommandSnapshot();
        if (!succeeded)
        {
            RestoreAfterFailedMutation(graph, before, after);
            return false;
        }

        if (string.Equals(before, after, StringComparison.Ordinal)) return false;

        graph.NotifyGraphChanged();
        var command = new CurriculumGraphSnapshotCommand(graph, label, before, after);
        if (CommandService.I != null && CommandService.I.Stack != null)
        {
            return CommandService.I.Stack.RecordApplied(command);
        }

        Debug.LogWarning("[CurriculumGraphService] Graph edit applied without undo because CommandService is missing.");
        return true;
    }

    static void RestoreAfterFailedMutation(
        CurriculumGraphService graph,
        string before,
        string current = null)
    {
        current ??= graph.CaptureCommandSnapshot();
        if (string.Equals(before, current, StringComparison.Ordinal)) return;
        graph.RestoreCommandSnapshot(before);
    }
}

internal sealed class CurriculumGraphSnapshotCommand : IEditorCommand
{
    readonly CurriculumGraphService graph;
    readonly string before;
    readonly string after;
    readonly string label;

    public string Label => label;

    public CurriculumGraphSnapshotCommand(
        CurriculumGraphService graph,
        string label,
        string before,
        string after)
    {
        this.graph = graph;
        this.label = string.IsNullOrWhiteSpace(label) ? "Edit scenario" : label;
        this.before = before;
        this.after = after;
    }

    public bool Do()
    {
        return graph != null && graph.RestoreCommandSnapshot(after);
    }

    public bool Undo()
    {
        return graph != null && graph.RestoreCommandSnapshot(before);
    }
}
