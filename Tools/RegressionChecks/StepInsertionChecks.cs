using System;
using System.Linq;
using UnityEngine;

static class StepInsertionChecks
{
    static int checks;
    static void Check(bool result, string message)
    {
        if (!result) throw new Exception("Step insertion: " + message);
        checks++;
    }

    public static void Run()
    {
        checks = 0;
        var previous = CommandService.I;
        try
        {
            CheckInsertAfterFirst();
            CheckAppend();
            CheckBranchedRejected();
            Console.WriteLine($"[Regression] {checks} step insertion checks passed.");
        }
        finally { CommandService.I = previous; }
    }

    static CurriculumGraphService Fixture(int count)
    {
        CommandService.I = new CommandService();
        var graph = new CurriculumGraphService();
        for (int i = 0; i < count; i++) Check(graph.TryAddStepAtEnd(out _, out _), "Create linear step");
        CommandService.I.Stack.Clear();
        return graph;
    }

    static void CheckInsertAfterFirst()
    {
        var graph = Fixture(3);
        var stack = CommandService.I.Stack;
        var ids = graph.GetDisplayOrderedSteps().Select(step => step.nodeId).ToArray();
        string before = graph.CaptureCommandSnapshot();
        Check(graph.TryInsertStepWithCondition(ids[0], out var step, out var condition, out _), "Insert after first step");
        Check(stack.UndoCount == 1, "Insertion is one history entry");
        var order = graph.GetDisplayOrderedSteps().Select(s => s.nodeId).ToArray();
        Check(order.SequenceEqual(new[] { ids[0], step.nodeId, ids[1], ids[2] }), "New step follows the first step");
        var bound = graph.GetConditionNodesForStep(step.nodeId);
        Check(bound.Count == 1 && bound[0].nodeId == condition.nodeId, "New step has exactly one bound condition");
        Check(graph.TryBuildLinearStepSequence(out _, out _), "Result stays linear");
        string after = graph.CaptureCommandSnapshot();
        Check(stack.Undo() && graph.CaptureCommandSnapshot() == before, "Undo restores the previous snapshot");
        Check(stack.Redo() && graph.CaptureCommandSnapshot() == after, "Redo reapplies the insertion");
    }

    static void CheckAppend()
    {
        var graph = Fixture(2);
        var ids = graph.GetDisplayOrderedSteps().Select(step => step.nodeId).ToArray();
        Check(graph.TryInsertStepWithCondition(null, out var step, out _, out _), "Null id appends");
        Check(graph.GetDisplayOrderedSteps().Select(s => s.nodeId).SequenceEqual(new[] { ids[0], ids[1], step.nodeId }), "Null id adds at the end");
        var empty = Fixture(0);
        Check(empty.TryInsertStepWithCondition(null, out var first, out _, out _) &&
              empty.GetDisplayOrderedSteps().Count == 1 && empty.GetConditionCountForStep(first.nodeId) == 1, "Insert into an empty graph");
    }

    static void CheckBranchedRejected()
    {
        var graph = Fixture(2);
        var ids = graph.GetDisplayOrderedSteps().Select(step => step.nodeId).ToArray();
        var extra = graph.AddStep();
        Check(graph.TryConnectNodesCommand(ids[0], extra.nodeId, out _), "Create a branch");
        CommandService.I.Stack.Clear();
        string before = graph.CaptureCommandSnapshot();
        Check(!graph.TryInsertStepWithCondition(ids[0], out var step, out var condition, out string reason) &&
              step == null && condition == null && !string.IsNullOrEmpty(reason), "Branched graph is rejected with a reason");
        Check(graph.CaptureCommandSnapshot() == before && !CommandService.I.Stack.CanUndo, "Rejected insertion leaves graph and history unchanged");
    }
}
