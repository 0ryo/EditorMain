using System;
using System.Linq;
using UnityEngine;

internal static class CurriculumGraphCommandProcessor
{
    public static bool Execute(CurriculumGraphService graph, string label, Func<bool> mutation, Action<bool> appliedStateChanged = null)
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

        NotifyAppliedState(appliedStateChanged, true);
        graph.NotifyGraphChanged();
        var command = new CurriculumGraphSnapshotCommand(graph, label, before, after, appliedStateChanged);
        if (CommandService.I != null && CommandService.I.Stack != null)
        {
            return CommandService.I.Stack.RecordApplied(command);
        }

        Debug.LogWarning("[CurriculumGraphService] Graph edit applied without undo because CommandService is missing.");
        return true;
    }

    // Presentation callbacks must not prevent recording or restoring the graph edit.
    internal static void NotifyAppliedState(Action<bool> callback, bool applied)
    {
        try { callback?.Invoke(applied); }
        catch (Exception ex) { Debug.LogException(ex); }
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

internal sealed class CurriculumGraphSnapshotCommand : IEditorCommand, IDescribedEditorCommand
{
    readonly CurriculumGraphService graph;
    readonly string before;
    readonly string after;
    readonly string label;
    readonly Action<bool> appliedStateChanged;

    public string Label => label;

    // 通知用。前後のスナップショットの差から、手順の名前まで含めて「手順「…」の追加」のように説明する。
    public string Description => Describe(label, before, after);

    internal static string Describe(string label, string before, string after)
    {
        string plain = CommandDescriptions.ForLabel(label);
        string verb;
        switch (label)
        {
            case "Add step": case "Add condition": verb = "追加"; break;
            case "Delete step": case "Delete scenario node": verb = "削除"; break;
            case "Edit step": verb = "編集"; break;
            case "Edit condition": verb = "変更"; break;
            case "Set condition type": verb = "種類の変更"; break;
            default: return plain;
        }

        try
        {
            var b = JsonUtility.FromJson<Curriculum>(before);
            var a = JsonUtility.FromJson<Curriculum>(after);
            if (a == null || b == null) return plain;
            bool added = label.StartsWith("Add");
            bool removed = label.StartsWith("Delete");
            var source = removed ? b : a;
            var other = removed ? a : b;
            var node = source.nodes.Where(n =>
            {
                var o = other.nodes.FirstOrDefault(x => x.nodeId == n.nodeId);
                return added || removed ? o == null : o != null && JsonUtility.ToJson(o) != JsonUtility.ToJson(n);
            }).OrderBy(n => (n.nodeType == ScenarioNodeType.Step) == label.EndsWith("step") ? 0 : 1).FirstOrDefault();
            if (node == null) return plain;
            if (node.nodeType == ScenarioNodeType.Condition) return "完了条件の" + verb;
            if (node.nodeType != ScenarioNodeType.Step) return plain;
            string title = CommandDescriptions.Shorten(node.step != null ? node.step.title : null);
            return string.IsNullOrWhiteSpace(title) ? "手順の" + verb : $"手順「{title}」の{verb}";
        }
        catch (Exception)
        {
            return plain;
        }
    }

    public CurriculumGraphSnapshotCommand(
        CurriculumGraphService graph,
        string label,
        string before,
        string after,
        Action<bool> appliedStateChanged = null)
    {
        this.graph = graph;
        this.label = string.IsNullOrWhiteSpace(label) ? "Edit scenario" : label;
        this.before = before;
        this.after = after;
        this.appliedStateChanged = appliedStateChanged;
    }

    public bool Do()
    {
        return graph != null && graph.RestoreCommandSnapshot(after,
            () => CurriculumGraphCommandProcessor.NotifyAppliedState(appliedStateChanged, true));
    }

    public bool Undo()
    {
        return graph != null && graph.RestoreCommandSnapshot(before,
            () => CurriculumGraphCommandProcessor.NotifyAppliedState(appliedStateChanged, false));
    }
}
