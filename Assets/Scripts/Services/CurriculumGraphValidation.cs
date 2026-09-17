using System.Collections.Generic;
using System.Linq;
using UnityEngine;

internal static class CurriculumGraphValidator
{
    public static GraphValidationResult Validate(CurriculumGraphService graph)
    {
        var curriculum = graph.curriculum;
        var result = new GraphValidationResult();

        var nodes = curriculum.nodes.Where(n => n != null).ToList();
        var nodeIds = nodes
            .Where(n => !string.IsNullOrWhiteSpace(n.nodeId))
            .Select(n => n.nodeId)
            .ToList();

        if (nodes.Any(n => string.IsNullOrWhiteSpace(n.nodeId)))
        {
            result.AddError("E-11", "Found node with empty nodeId.");
        }

        var duplicateIds = nodeIds.GroupBy(id => id).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        if (duplicateIds.Count > 0)
        {
            result.AddError("E-11", $"Duplicate nodeId detected: {string.Join(", ", duplicateIds)}", duplicateIds[0]);
        }

        var startNodes = nodes.Where(n => n.nodeType == ScenarioNodeType.Start).ToList();
        var endNodes = nodes.Where(n => n.nodeType == ScenarioNodeType.End).ToList();
        var stepNodes = nodes.Where(n => n.nodeType == ScenarioNodeType.Step).ToList();
        var conditionNodes = nodes.Where(n => n.nodeType == ScenarioNodeType.Condition).ToList();

        if (startNodes.Count != 1)
        {
            result.AddError("E-01", $"Start node count must be exactly 1 (actual={startNodes.Count}).");
        }

        if (endNodes.Count != 1)
        {
            result.AddError("E-02", $"End node count must be exactly 1 (actual={endNodes.Count}).");
        }

        foreach (var edge in curriculum.edges)
        {
            if (edge == null) { result.AddError("E-11", "Null edge."); continue; }
            if (string.IsNullOrWhiteSpace(edge.fromNodeId) || string.IsNullOrWhiteSpace(edge.toNodeId))
            {
                result.AddError("E-11", "Edge has empty from/to nodeId.");
                continue;
            }

            var fromNode = graph.FindNode(edge.fromNodeId);
            var toNode = graph.FindNode(edge.toNodeId);
            if (fromNode == null || toNode == null)
            {
                result.AddError("E-11", $"Edge points to missing node: {edge.fromNodeId} -> {edge.toNodeId}");
                continue;
            }

            if (edge.edgeType == ScenarioEdgeType.StepFlow)
            {
                bool valid = (fromNode.nodeType == ScenarioNodeType.Start || fromNode.nodeType == ScenarioNodeType.Step) &&
                             (toNode.nodeType == ScenarioNodeType.Step || toNode.nodeType == ScenarioNodeType.End);
                if (!valid)
                {
                    result.AddError("E-04", $"Invalid StepFlow route: {fromNode.nodeType} -> {toNode.nodeType}", fromNode.nodeId);
                }
            }
            else if (edge.edgeType == ScenarioEdgeType.ConditionBind)
            {
                bool valid = fromNode.nodeType == ScenarioNodeType.Condition && toNode.nodeType == ScenarioNodeType.Step;
                if (!valid)
                {
                    result.AddError("E-07", $"Invalid ConditionBind route: {fromNode.nodeType} -> {toNode.nodeType}", fromNode.nodeId);
                }
            }
        }

        var stepFlowEdges = curriculum.edges.Where(e => e != null && e.edgeType == ScenarioEdgeType.StepFlow).ToList();
        var conditionBindEdges = curriculum.edges.Where(e => e != null && e.edgeType == ScenarioEdgeType.ConditionBind).ToList();

        if (startNodes.Count == 1)
        {
            int outCount = stepFlowEdges.Count(e => e.fromNodeId == startNodes[0].nodeId);
            if (outCount != 1)
            {
                result.AddError("E-03", $"Start must have exactly one outgoing StepFlow edge (actual={outCount}).", startNodes[0].nodeId);
            }
        }

        if (endNodes.Count == 1)
        {
            int inCount = stepFlowEdges.Count(e => e.toNodeId == endNodes[0].nodeId);
            if (inCount < 1)
            {
                result.AddError("E-05", $"End must have an incoming StepFlow edge (actual={inCount}).", endNodes[0].nodeId);
            }
        }

        foreach (var step in stepNodes)
        {
            int inCount = stepFlowEdges.Count(e => e.toNodeId == step.nodeId);
            int outCount = stepFlowEdges.Count(e => e.fromNodeId == step.nodeId);
            if (inCount == 0 || outCount == 0)
            {
                result.AddError("E-04", $"Step '{step.nodeId}' has a missing incoming or outgoing StepFlow edge.", step.nodeId);
            }
        }

        if (stepNodes.Count <= 0)
        {
            result.AddError("E-04", "No Step node exists.");
        }
        else if (!ScenarioFlow.TryOrder(curriculum, out _, out var linearReason))
        {
            result.AddError("E-04", $"Step chain is invalid: {linearReason}");
        }

        var placedObjects = UnityEngine.Object.FindObjectsByType<PlacedObject>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (var placed in placedObjects)
        {
            if (placed == null) continue;
            placed.EnsureHasId();

            if (string.IsNullOrWhiteSpace(placed.id) || string.IsNullOrWhiteSpace(placed.typeId))
            {
                result.AddError("E-11", "PlacedObject has missing id or typeId.");
            }
        }

        var placedObjectIds = placedObjects
            .Where(p => p != null && !string.IsNullOrWhiteSpace(p.id))
            .Select(p => p.id)
            .ToHashSet();
        var perStepAUsage = new Dictionary<string, HashSet<string>>();

        foreach (var condition in conditionNodes)
        {
            if (ConditionTypeCatalog.Find(condition.condition.type) == null)
            {
                result.AddError("E-12", $"Condition '{condition.nodeId}' has unsupported type '{condition.condition.type}'.", condition.nodeId);
            }

            int bindCount = conditionBindEdges.Count(e => e.fromNodeId == condition.nodeId);
            if (bindCount != 1)
            {
                result.AddError("E-07", $"Condition '{condition.nodeId}' must bind to exactly one Step (actual={bindCount}).", condition.nodeId);
            }

            bool requiresB = ConditionTypeCatalog.RequiresObjectB(condition.condition.type);
            if (string.IsNullOrWhiteSpace(condition.condition.objectAId) ||
                (requiresB && string.IsNullOrWhiteSpace(condition.condition.objectBId)))
            {
                result.AddError("E-08", $"Condition '{condition.nodeId}' has unassigned A/B object.", condition.nodeId);
            }
            else
            {
                if (requiresB && condition.condition.objectAId == condition.condition.objectBId)
                {
                    result.AddError("E-09", $"Condition '{condition.nodeId}' cannot use the same object for A and B.", condition.nodeId);
                }

                if (!placedObjectIds.Contains(condition.condition.objectAId) ||
                    (requiresB && !placedObjectIds.Contains(condition.condition.objectBId)))
                {
                    result.AddError("E-10", $"Condition '{condition.nodeId}' references missing placed object id.", condition.nodeId);
                }
            }
        }

        foreach (var step in stepNodes)
        {
            var conditions = graph.GetConditionNodesForStep(step.nodeId);
            int maxConditions = graph.GetMaxConditionsPerStep();
            if (conditions.Count <= 0 || conditions.Count > maxConditions)
            {
                result.AddError("E-06", $"Step '{step.nodeId}' condition count out of range (actual={conditions.Count}, allowed=1..{maxConditions}).", step.nodeId);
            }

            if (conditions.Count == maxConditions)
            {
                result.AddWarning("W-01", $"このステップの条件数が設定上限（{maxConditions}件）に達しています。", step.nodeId);
            }

            var duplicateKeys = conditions
                .Select(c => $"{c.condition.type}|{c.condition.objectAId}|{(ConditionTypeCatalog.RequiresObjectB(c.condition.type) ? c.condition.objectBId : string.Empty)}|{ConditionTypeCatalog.BuildParameterSignature(c.condition)}")
                .GroupBy(k => k)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToList();
            if (duplicateKeys.Count > 0)
            {
                result.AddError("E-06", $"Step '{step.nodeId}' has duplicate condition definitions.", step.nodeId);
            }

            foreach (var condition in conditions)
            {
                string a = condition.condition.objectAId;
                if (string.IsNullOrWhiteSpace(a)) continue;
                if (!perStepAUsage.TryGetValue(a, out var stepSet))
                {
                    stepSet = new HashSet<string>();
                    perStepAUsage[a] = stepSet;
                }
                stepSet.Add(step.nodeId);
            }
        }

        foreach (var pair in perStepAUsage)
        {
            if (pair.Value.Count > 1)
            {
                result.AddWarning("W-02", $"ObjectA '{pair.Key}' is reused across multiple steps.");
            }
        }

        return result;
    }
}

public class GraphValidationResult
{
    public readonly List<GraphValidationIssue> errors = new List<GraphValidationIssue>();
    public readonly List<GraphValidationIssue> warnings = new List<GraphValidationIssue>();

    readonly HashSet<string> keys = new HashSet<string>();

    public bool CanExport => errors.Count == 0;

    public void AddError(string code, string message, string nodeId = null)
    {
        AddIssue(errors, code, message, nodeId);
    }

    public void AddWarning(string code, string message, string nodeId = null)
    {
        AddIssue(warnings, code, message, nodeId);
    }

    void AddIssue(List<GraphValidationIssue> target, string code, string message, string nodeId)
    {
        string key = code + "|" + message + "|" + nodeId;
        if (!keys.Add(key)) return;
        target.Add(new GraphValidationIssue(code, message, nodeId));
    }
}

public class GraphValidationIssue
{
    public readonly string code;
    public readonly string message;
    public readonly string nodeId;

    public GraphValidationIssue(string code, string message, string nodeId = null)
    {
        this.code = code;
        this.message = message;
        this.nodeId = nodeId;
    }
}
