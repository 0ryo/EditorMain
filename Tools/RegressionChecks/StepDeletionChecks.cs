using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

static class StepDeletionChecks
{
    static int checks;
    public static void Run()
    {
        var previous = CommandService.I;
        try
        {
            CheckDeletion(3, 0);
            CheckDeletion(3, 1);
            CheckDeletion(3, 2);
            CheckDeletion(1, 0);
            CheckRejectedGraphs();
            CheckExistingEdits();
            Console.WriteLine($"[Regression] {checks} step deletion checks passed.");
        }
        finally { CommandService.I = previous; }
    }

    static CurriculumGraphService Fixture(int count)
    {
        CommandService.I = new CommandService();
        var graph = new CurriculumGraphService();
        for (int i = 0; i < count; i++)
        {
            Check(graph.TryAddStepAtEnd(out var step, out _), "Create linear step");
            Check(graph.UpdateStepData(step.nodeId, "Fixture description", data =>
            {
                data.title = "手順 " + i;
                data.body = "説明 " + i;
                data.supplement = "補足 " + i;
                data.caution = "注意 " + i;
                data.durationMinutes = i + 1;
            }), "Set all step metadata");
            for (int j = 0; j < 2; j++)
            {
                Check(graph.TryAddConditionToStep(step.nodeId, out var condition, out _), "Create bound condition");
                Check(graph.UpdateConditionData(condition.nodeId, "Fixture condition", data =>
                {
                    data.objectAId = "object-a-" + i + "-" + j;
                    data.objectBId = "object-b-" + i;
                    ConditionTypeCatalog.SetNumber(data, ConditionTypeCatalog.DistanceKey, .12f + j * .1f);
                }), "Set condition references and parameters");
            }
        }
        CommandService.I.Stack.Clear();
        return graph;
    }

    static void CheckDeletion(int count, int index)
    {
        var graph = Fixture(count);
        var stack = CommandService.I.Stack;
        var steps = graph.GetDisplayOrderedSteps();
        string deletedId = steps[index].nodeId;
        var ownedIds = graph.GetConditionNodesForStep(deletedId).Select(node => node.nodeId).ToHashSet();
        var remainingIds = steps.Where(step => step.nodeId != deletedId).Select(step => step.nodeId).ToArray();
        var unaffectedConditions = graph.curriculum.nodes.Where(node => node.nodeType == ScenarioNodeType.Condition && !ownedIds.Contains(node.nodeId))
            .ToDictionary(node => node.nodeId, node => JsonUtility.ToJson(node));
        string before = graph.CaptureCommandSnapshot();
        string selectedStep = deletedId;
        string selectedCondition = graph.GetConditionNodesForStep(deletedId)[1].nodeId;
        string originalCondition = selectedCondition;
        string nextStep = index + 1 < count ? steps[index + 1].nodeId : index > 0 ? steps[index - 1].nodeId : null;
        string nextCondition = nextStep != null ? graph.GetConditionNodesForStep(nextStep)[0].nodeId : null;
        int notifications = 0;
        bool notifiedWithValidSelection = true;
        graph.GraphChanged += () =>
        {
            notifications++;
            notifiedWithValidSelection &= selectedStep == null ? selectedCondition == null :
                graph.FindNode(selectedStep)?.nodeType == ScenarioNodeType.Step &&
                graph.GetConditionNodesForStep(selectedStep).Any(node => node.nodeId == selectedCondition);
        };
        Check(graph.TryRemoveLinearStep(deletedId, out var reason, applied =>
        {
            selectedStep = applied ? nextStep : deletedId;
            selectedCondition = applied ? nextCondition : originalCondition;
        }) && reason == null, "Delete selected step");
        Check(selectedStep == nextStep && selectedCondition == nextCondition, "Choose neighbor after deletion");
        Check(stack.UndoCount == 1 && stack.UndoLabel == "Delete step", "Deletion is one history entry");
        Check(graph.FindNode(deletedId) == null && ownedIds.All(id => graph.FindNode(id) == null), "Remove step and its conditions");
        Check(!graph.curriculum.edges.Any(edge => edge != null &&
            (edge.fromNodeId == deletedId || edge.toNodeId == deletedId || ownedIds.Contains(edge.fromNodeId) || ownedIds.Contains(edge.toNodeId))),
            "Remove all references to deleted nodes");
        Check(graph.TryBuildLinearStepSequence(out var ordered, out _) && ordered.Select(node => node.nodeId).SequenceEqual(remainingIds),
            "Reconnect predecessors and successors, including Start/End");
        Check(graph.curriculum.edges.Count(edge => edge.edgeType == ScenarioEdgeType.StepFlow) == count, "One continuous remaining chain");
        Check(unaffectedConditions.All(pair => JsonUtility.ToJson(graph.FindNode(pair.Key)) == pair.Value), "Preserve other conditions and placed object references");
        Check(graph.ValidateGraph().errors.All(issue => issue.code != "E-07" && issue.code != "E-11" &&
            (count == 1 || issue.code != "E-04")), "Deletion leaves no dangling bindings or broken step flow");
        if (count > 1)
        {
            var export = graph.BuildScenarioExport();
            Check(export.requiredActions.Select(action => action.sourceNodeId).SequenceEqual(remainingIds), "Export contains only remaining steps");
            Check(export.requiredActions.All(action => action.conditions.Count == 2), "Export excludes deleted conditions");
            Check(export.requiredActions[0].nextActionIds.SequenceEqual(new[] { "act-002" }) &&
                export.requiredActions[1].nextActionIds.SequenceEqual(new[] { ScenarioExport.EndActionId }), "Export reconnects action references");
        }
        else
        {
            Check(graph.GetDisplayOrderedSteps().Count == 0 && selectedStep == null && selectedCondition == null, "Deleting last step clears selection");
        }
        string after = graph.CaptureCommandSnapshot();
        for (int i = 0; i < 2; i++)
        {
            Check(stack.Undo() && graph.CaptureCommandSnapshot() == before, "Undo restores IDs, descriptions, connections and conditions exactly");
            Check(selectedStep == deletedId && selectedCondition == originalCondition, "Undo restores selected step and second condition");
            Check(stack.Redo() && graph.CaptureCommandSnapshot() == after, "Redo reapplies identical deletion");
            Check(selectedStep == nextStep && selectedCondition == nextCondition, "Redo restores neighboring selection");
        }
        Check(notifications == 5 && notifiedWithValidSelection, "Each graph notification observes consistent selection");
        Check(!graph.TryRemoveLinearStep(deletedId, out _) && stack.UndoCount == 1, "Repeated deletion is rejected without history");
        if (count == 1)
        {
            Check(graph.TryAddStepAtEnd(out var added, out _) && graph.TryAddConditionToStep(added.nodeId, out _, out _) &&
                graph.TryBuildLinearStepSequence(out ordered, out _) && ordered.Count == 1, "Can add a new step after deleting the last one");
        }
    }

    static void CheckRejectedGraphs()
    {
        var graph = Fixture(3);
        var steps = graph.GetDisplayOrderedSteps();
        Reject(graph, graph.GetStartNode().nodeId, "Start cannot be deleted");
        Reject(graph, graph.GetEndNode().nodeId, "End cannot be deleted");
        Reject(graph, graph.GetConditionNodesForStep(steps[0].nodeId)[0].nodeId, "Condition is not a step");
        Reject(graph, "missing", "Missing step is rejected");
        Check(graph.TryAddEdge(steps[0].nodeId, steps[2].nodeId, out _), "Create branch");
        Reject(graph, steps[1].nodeId, "Branches are retained");
        graph.RemoveEdge(steps[0].nodeId, steps[2].nodeId);
        graph.curriculum.edges.Add(new ScenarioEdge { fromNodeId = "missing", toNodeId = steps[1].nodeId });
        Reject(graph, steps[1].nodeId, "Stray incoming edges are retained");
        graph.curriculum.edges.RemoveAt(graph.curriculum.edges.Count - 1);
        var shared = graph.GetConditionNodesForStep(steps[1].nodeId)[0];
        graph.curriculum.edges.Add(new ScenarioEdge { fromNodeId = shared.nodeId, toNodeId = steps[2].nodeId, edgeType = ScenarioEdgeType.ConditionBind });
        Reject(graph, steps[1].nodeId, "Shared malformed condition is not deleted from another step");
        graph.curriculum.edges.RemoveAt(graph.curriculum.edges.Count - 1);
        graph.RemoveEdge(steps[1].nodeId, steps[2].nodeId);
        Reject(graph, steps[1].nodeId, "Disconnected draft is retained");
    }

    static void Reject(CurriculumGraphService graph, string nodeId, string label)
    {
        string before = graph.CaptureCommandSnapshot();
        int callbacks = 0, events = 0;
        Action listener = () => events++;
        graph.GraphChanged += listener;
        Check(!graph.TryRemoveLinearStep(nodeId, out var reason, _ => callbacks++) && !string.IsNullOrWhiteSpace(reason), label);
        graph.GraphChanged -= listener;
        Check(before == graph.CaptureCommandSnapshot() && callbacks == 0 && events == 0 && !CommandService.I.Stack.CanUndo, "Rejected deletion preserves graph and history");
    }

    static void CheckExistingEdits()
    {
        var graph = Fixture(3);
        var steps = graph.GetDisplayOrderedSteps();
        Check(graph.TryRemoveLinearStep(steps[1].nodeId, out _), "Delete before existing edits");
        var ids = graph.GetDisplayOrderedSteps().Select(step => step.nodeId).Reverse().ToArray();
        Check(graph.ReorderLinearSteps(ids) && graph.GetDisplayOrderedSteps().Select(step => step.nodeId).SequenceEqual(ids), "Reordering still works after deletion");
        Check(graph.UpdateStepData(ids[0], "Edit description", data => data.body = "更新した説明") && graph.FindNode(ids[0]).step.body == "更新した説明", "Description editing still works");
        Check(CommandService.I.Stack.Undo() && CommandService.I.Stack.Undo() && CommandService.I.Stack.Undo() && graph.GetDisplayOrderedSteps().Count == 3,
            "Undo existing edits then deletion restores full chain");
        // A conditionless draft can still be deleted; conditions are not a prerequisite.
        foreach (var condition in graph.GetConditionNodesForStep(steps[0].nodeId)) graph.RemoveNode(condition.nodeId);
        Check(graph.TryRemoveLinearStep(steps[0].nodeId, out _), "Step without conditions can be deleted");
        graph.curriculum.edges.Add(null);
        Check(graph.TryRemoveLinearStep(steps[1].nodeId, out _), "Null unrelated edge does not break deletion");
        Check(graph.curriculum.edges.Contains(null), "Unrelated malformed draft data is retained");
    }

    static void Check(bool value, string label)
    {
        if (!value) throw new Exception("Step deletion: " + label);
        checks++;
    }
}
