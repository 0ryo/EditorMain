using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class CurriculumGraphService : MonoBehaviour
{
    const string StartNodeId = "start-0001";
    const string EndNodeId = "end-0001";
    const int MaxConditionsPerStep = 3;

    public Curriculum curriculum = new Curriculum();

    public event Action GraphChanged;

    int stepSequence = 0;
    int conditionSequence = 0;

    void Awake()
    {
        EnsureGraphInitialized();
    }

    public bool ExecuteCommand(string label, Func<bool> mutation)
    {
        if (mutation == null) return false;

        string before = CaptureCommandSnapshot();
        bool succeeded;
        try
        {
            succeeded = mutation();
        }
        catch (Exception ex)
        {
            RestoreAfterFailedMutation(before);
            Debug.LogException(ex);
            return false;
        }

        string after = CaptureCommandSnapshot();
        if (!succeeded)
        {
            RestoreAfterFailedMutation(before, after);
            return false;
        }

        if (string.Equals(before, after, StringComparison.Ordinal)) return false;

        NotifyGraphChanged();
        var command = new CurriculumGraphSnapshotCommand(this, label, before, after);
        if (CommandService.I != null && CommandService.I.Stack != null)
        {
            return CommandService.I.Stack.RecordApplied(command);
        }

        Debug.LogWarning("[CurriculumGraphService] Graph edit applied without undo because CommandService is missing.");
        return true;
    }

    internal string CaptureCommandSnapshot()
    {
        EnsureGraphInitialized();
        return JsonUtility.ToJson(curriculum);
    }

    internal bool RestoreCommandSnapshot(string snapshot)
    {
        if (string.IsNullOrWhiteSpace(snapshot)) return false;

        var restored = JsonUtility.FromJson<Curriculum>(snapshot);
        if (restored == null) return false;

        curriculum = restored;
        EnsureGraphInitialized();
        NotifyGraphChanged();
        return true;
    }

    void RestoreAfterFailedMutation(string before, string current = null)
    {
        current ??= CaptureCommandSnapshot();
        if (string.Equals(before, current, StringComparison.Ordinal)) return;
        RestoreCommandSnapshot(before);
    }

    void NotifyGraphChanged()
    {
        var handlers = GraphChanged;
        if (handlers == null) return;

        foreach (Action handler in handlers.GetInvocationList())
        {
            try
            {
                handler();
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }
    }

    public void EnsureGraphInitialized()
    {
        if (curriculum == null)
        {
            curriculum = new Curriculum();
        }

        if (curriculum.nodes == null)
        {
            curriculum.nodes = new List<ScenarioNode>();
        }

        if (curriculum.edges == null)
        {
            curriculum.edges = new List<ScenarioEdge>();
        }

        EnsureTerminalNode(ScenarioNodeType.Start, StartNodeId);
        EnsureTerminalNode(ScenarioNodeType.End, EndNodeId);
        NormalizeNodeDataDefaults();
        RebuildSequences();
    }

    void EnsureTerminalNode(ScenarioNodeType nodeType, string defaultNodeId)
    {
        bool exists = curriculum.nodes.Any(n => n != null && n.nodeType == nodeType);
        if (exists) return;

        curriculum.nodes.Add(new ScenarioNode
        {
            nodeId = defaultNodeId,
            nodeType = nodeType,
            step = new StepNodeData(),
            condition = new ConditionNodeData()
        });
    }

    void NormalizeNodeDataDefaults()
    {
        foreach (var node in curriculum.nodes.Where(n => n != null))
        {
            if (node.step == null)
            {
                node.step = new StepNodeData();
            }

            if (node.condition == null)
            {
                node.condition = new ConditionNodeData();
            }

            if (node.nodeType == ScenarioNodeType.Condition &&
                string.IsNullOrWhiteSpace(node.condition.title))
            {
                node.condition.title = ConditionNodeData.DefaultTitle;
            }
        }
    }

    void RebuildSequences()
    {
        stepSequence = ExtractSequence(curriculum.nodes, ScenarioNodeType.Step, "step-");
        conditionSequence = ExtractSequence(curriculum.nodes, ScenarioNodeType.Condition, "cond-");
    }

    static int ExtractSequence(IEnumerable<ScenarioNode> nodes, ScenarioNodeType nodeType, string prefix)
    {
        int max = 0;
        foreach (var node in nodes)
        {
            if (node == null || node.nodeType != nodeType || string.IsNullOrWhiteSpace(node.nodeId)) continue;
            if (!node.nodeId.StartsWith(prefix)) continue;

            string suffix = node.nodeId.Substring(prefix.Length);
            if (!int.TryParse(suffix, out int parsed)) continue;
            if (parsed > max) max = parsed;
        }

        return max;
    }

    public ScenarioNode AddStep()
    {
        EnsureGraphInitialized();

        var step = new ScenarioNode
        {
            nodeId = "step-" + (++stepSequence).ToString("D4"),
            nodeType = ScenarioNodeType.Step,
            step = new StepNodeData
            {
                title = $"\u624B\u9806 {stepSequence}"
            },
            condition = new ConditionNodeData()
        };

        curriculum.nodes.Add(step);
        return step;
    }

    public ScenarioNode AddCondition()
    {
        EnsureGraphInitialized();

        var condition = new ScenarioNode
        {
            nodeId = "cond-" + (++conditionSequence).ToString("D4"),
            nodeType = ScenarioNodeType.Condition,
            step = new StepNodeData(),
            condition = new ConditionNodeData
            {
                title = ConditionNodeData.DefaultTitle
            }
        };

        curriculum.nodes.Add(condition);
        return condition;
    }

    public void RemoveNode(string nodeId)
    {
        EnsureGraphInitialized();

        var node = FindNode(nodeId);
        if (node == null) return;
        if (node.nodeType == ScenarioNodeType.Start || node.nodeType == ScenarioNodeType.End) return;

        curriculum.nodes.Remove(node);
        curriculum.edges.RemoveAll(e => e.fromNodeId == nodeId || e.toNodeId == nodeId);
    }

    public ScenarioNode FindNode(string nodeId)
    {
        EnsureGraphInitialized();
        return curriculum.nodes.FirstOrDefault(n => n != null && n.nodeId == nodeId);
    }

    public ScenarioNode GetStartNode()
    {
        EnsureGraphInitialized();
        return curriculum.nodes.FirstOrDefault(n => n != null && n.nodeType == ScenarioNodeType.Start);
    }

    public ScenarioNode GetEndNode()
    {
        EnsureGraphInitialized();
        return curriculum.nodes.FirstOrDefault(n => n != null && n.nodeType == ScenarioNodeType.End);
    }

    public List<ScenarioNode> GetNodes(ScenarioNodeType nodeType)
    {
        EnsureGraphInitialized();
        return curriculum.nodes
            .Where(n => n != null && n.nodeType == nodeType)
            .OrderBy(n => n.nodeId)
            .ToList();
    }

    public void AddEdge(string fromNodeId, string toNodeId)
    {
        if (!TryAddEdge(fromNodeId, toNodeId, out var reason))
        {
            Debug.LogWarning($"[CurriculumGraphService] AddEdge rejected from={fromNodeId} to={toNodeId} reason={reason}");
        }
    }

    public bool TryAddEdge(string fromNodeId, string toNodeId, out string reason)
    {
        reason = null;
        EnsureGraphInitialized();

        if (string.IsNullOrWhiteSpace(fromNodeId) || string.IsNullOrWhiteSpace(toNodeId))
        {
            reason = "CONNECT_EMPTY_ID";
            return false;
        }

        if (fromNodeId == toNodeId)
        {
            reason = "CONNECT_SELF";
            return false;
        }

        var fromNode = FindNode(fromNodeId);
        var toNode = FindNode(toNodeId);
        if (fromNode == null || toNode == null)
        {
            reason = "CONNECT_NODE_NOT_FOUND";
            return false;
        }

        if (!TryInferEdgeType(fromNode, toNode, out var edgeType))
        {
            reason = "CONNECT_INVALID_ROUTE";
            return false;
        }

        bool duplicate = curriculum.edges.Any(e =>
            e.edgeType == edgeType &&
            e.fromNodeId == fromNodeId &&
            e.toNodeId == toNodeId);
        if (duplicate)
        {
            reason = "CONNECT_DUPLICATE";
            return false;
        }

        if (!CanAddEdge(edgeType, fromNode, toNode, out reason))
        {
            return false;
        }

        curriculum.edges.Add(new ScenarioEdge
        {
            fromNodeId = fromNodeId,
            toNodeId = toNodeId,
            edgeType = edgeType
        });

        return true;
    }

    static bool TryInferEdgeType(ScenarioNode fromNode, ScenarioNode toNode, out ScenarioEdgeType edgeType)
    {
        edgeType = ScenarioEdgeType.StepFlow;

        bool fromStepFlow = fromNode.nodeType == ScenarioNodeType.Start || fromNode.nodeType == ScenarioNodeType.Step;
        bool toStepFlow = toNode.nodeType == ScenarioNodeType.Step || toNode.nodeType == ScenarioNodeType.End;
        if (fromStepFlow && toStepFlow)
        {
            edgeType = ScenarioEdgeType.StepFlow;
            return true;
        }

        if (fromNode.nodeType == ScenarioNodeType.Condition && toNode.nodeType == ScenarioNodeType.Step)
        {
            edgeType = ScenarioEdgeType.ConditionBind;
            return true;
        }

        return false;
    }

    bool CanAddEdge(ScenarioEdgeType edgeType, ScenarioNode fromNode, ScenarioNode toNode, out string reason)
    {
        reason = null;

        if (edgeType == ScenarioEdgeType.StepFlow)
        {
            int outCount = curriculum.edges.Count(e => e.edgeType == ScenarioEdgeType.StepFlow && e.fromNodeId == fromNode.nodeId);
            if (outCount >= 1)
            {
                reason = "STEPFLOW_OUT_LIMIT";
                return false;
            }

            int inCount = curriculum.edges.Count(e => e.edgeType == ScenarioEdgeType.StepFlow && e.toNodeId == toNode.nodeId);
            if (toNode.nodeType == ScenarioNodeType.Step && inCount >= 1)
            {
                reason = "STEPFLOW_IN_LIMIT";
                return false;
            }

            if (toNode.nodeType == ScenarioNodeType.End && inCount >= 1)
            {
                reason = "END_IN_LIMIT";
                return false;
            }

            if (CreatesStepFlowCycle(fromNode.nodeId, toNode.nodeId))
            {
                reason = "STEPFLOW_CYCLE";
                return false;
            }

            return true;
        }

        int conditionOutCount = curriculum.edges.Count(e =>
            e.edgeType == ScenarioEdgeType.ConditionBind &&
            e.fromNodeId == fromNode.nodeId);
        if (conditionOutCount >= 1)
        {
            reason = "CONDITION_BIND_LIMIT";
            return false;
        }

        int stepConditionCount = curriculum.edges.Count(e =>
            e.edgeType == ScenarioEdgeType.ConditionBind &&
            e.toNodeId == toNode.nodeId);
        if (stepConditionCount >= MaxConditionsPerStep)
        {
            reason = "STEP_CONDITION_MAX";
            return false;
        }

        return true;
    }

    bool CreatesStepFlowCycle(string fromNodeId, string toNodeId)
    {
        var adjacency = curriculum.edges
            .Where(e => e.edgeType == ScenarioEdgeType.StepFlow)
            .GroupBy(e => e.fromNodeId)
            .ToDictionary(g => g.Key, g => g.Select(e => e.toNodeId).ToList());

        if (!adjacency.TryGetValue(fromNodeId, out var targets))
        {
            targets = new List<string>();
            adjacency[fromNodeId] = targets;
        }
        targets.Add(toNodeId);

        var stack = new Stack<string>();
        var visited = new HashSet<string>();
        stack.Push(toNodeId);

        while (stack.Count > 0)
        {
            var current = stack.Pop();
            if (!visited.Add(current)) continue;
            if (current == fromNodeId) return true;

            if (!adjacency.TryGetValue(current, out var nextNodes)) continue;
            foreach (var next in nextNodes)
            {
                stack.Push(next);
            }
        }

        return false;
    }

    public void RemoveEdge(string fromNodeId, string toNodeId)
    {
        EnsureGraphInitialized();
        curriculum.edges.RemoveAll(e => e.fromNodeId == fromNodeId && e.toNodeId == toNodeId);
    }

    public void RemoveEdge(string fromNodeId, string toNodeId, ScenarioEdgeType edgeType)
    {
        EnsureGraphInitialized();
        curriculum.edges.RemoveAll(e =>
            e.fromNodeId == fromNodeId &&
            e.toNodeId == toNodeId &&
            e.edgeType == edgeType);
    }

    public string GetConditionBoundStepNodeId(string conditionNodeId)
    {
        EnsureGraphInitialized();
        if (string.IsNullOrWhiteSpace(conditionNodeId)) return null;

        var edge = curriculum.edges.FirstOrDefault(e =>
            e.edgeType == ScenarioEdgeType.ConditionBind &&
            e.fromNodeId == conditionNodeId);
        return edge != null ? edge.toNodeId : null;
    }

    public bool IsConditionBoundToStep(string conditionNodeId)
    {
        return !string.IsNullOrWhiteSpace(GetConditionBoundStepNodeId(conditionNodeId));
    }

    public bool TryUnbindConditionFromStep(string conditionNodeId)
    {
        EnsureGraphInitialized();
        if (string.IsNullOrWhiteSpace(conditionNodeId)) return false;

        var conditionNode = FindNode(conditionNodeId);
        if (conditionNode == null || conditionNode.nodeType != ScenarioNodeType.Condition) return false;

        int removed = curriculum.edges.RemoveAll(e =>
            e.edgeType == ScenarioEdgeType.ConditionBind &&
            e.fromNodeId == conditionNodeId);
        return removed > 0;
    }

    public bool TryBindConditionToStep(string conditionNodeId, string stepNodeId, out string reason)
    {
        reason = null;
        EnsureGraphInitialized();

        if (string.IsNullOrWhiteSpace(conditionNodeId) || string.IsNullOrWhiteSpace(stepNodeId))
        {
            reason = "CONNECT_EMPTY_ID";
            return false;
        }

        var conditionNode = FindNode(conditionNodeId);
        var stepNode = FindNode(stepNodeId);
        if (conditionNode == null || stepNode == null)
        {
            reason = "CONNECT_NODE_NOT_FOUND";
            return false;
        }

        if (conditionNode.nodeType != ScenarioNodeType.Condition || stepNode.nodeType != ScenarioNodeType.Step)
        {
            reason = "CONNECT_INVALID_ROUTE";
            return false;
        }

        var existing = curriculum.edges
            .Where(e => e.edgeType == ScenarioEdgeType.ConditionBind && e.fromNodeId == conditionNodeId)
            .ToList();
        if (existing.Any(e => e.toNodeId == stepNodeId))
        {
            return true;
        }

        curriculum.edges.RemoveAll(e =>
            e.edgeType == ScenarioEdgeType.ConditionBind &&
            e.fromNodeId == conditionNodeId);

        if (TryAddEdge(conditionNodeId, stepNodeId, out reason))
        {
            return true;
        }

        // Restore old bind edge if new bind failed.
        curriculum.edges.AddRange(existing);
        return false;
    }

    public List<string> GetParents(string nodeId)
    {
        EnsureGraphInitialized();
        return curriculum.edges
            .Where(e => e.edgeType == ScenarioEdgeType.StepFlow && e.toNodeId == nodeId)
            .Select(e => e.fromNodeId)
            .Distinct()
            .ToList();
    }

    public bool RepairBrokenReferences()
    {
        EnsureGraphInitialized();

        bool changed = RemoveEdgesPointingToMissingNodes();
        var placedObjectIds = CollectPlacedObjectIds();
        bool referenceChanged = false;

        foreach (var conditionNode in curriculum.nodes.Where(n => n != null && n.nodeType == ScenarioNodeType.Condition))
        {
            if (!string.IsNullOrEmpty(conditionNode.condition.objectAId) &&
                !placedObjectIds.Contains(conditionNode.condition.objectAId))
            {
                conditionNode.condition.objectAId = null;
                referenceChanged = true;
            }

            if (!string.IsNullOrEmpty(conditionNode.condition.objectBId) &&
                !placedObjectIds.Contains(conditionNode.condition.objectBId))
            {
                conditionNode.condition.objectBId = null;
                referenceChanged = true;
            }
        }

        return changed || referenceChanged;
    }

    bool RemoveEdgesPointingToMissingNodes()
    {
        var nodeIds = curriculum.nodes
            .Where(n => n != null && !string.IsNullOrWhiteSpace(n.nodeId))
            .Select(n => n.nodeId)
            .ToHashSet();

        int before = curriculum.edges.Count;
        curriculum.edges.RemoveAll(e =>
            string.IsNullOrWhiteSpace(e.fromNodeId) ||
            string.IsNullOrWhiteSpace(e.toNodeId) ||
            !nodeIds.Contains(e.fromNodeId) ||
            !nodeIds.Contains(e.toNodeId));

        return before != curriculum.edges.Count;
    }

    HashSet<string> CollectPlacedObjectIds()
    {
        var allPlaced = FindObjectsByType<PlacedObject>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        var ids = new HashSet<string>();
        foreach (var placed in allPlaced)
        {
            if (placed == null) continue;
            placed.EnsureHasId();
            if (string.IsNullOrWhiteSpace(placed.id)) continue;
            ids.Add(placed.id);
        }

        return ids;
    }

    public List<ScenarioNode> GetConditionNodesForStep(string stepNodeId)
    {
        EnsureGraphInitialized();
        return curriculum.edges
            .Where(e => e.edgeType == ScenarioEdgeType.ConditionBind && e.toNodeId == stepNodeId)
            .Select(e => FindNode(e.fromNodeId))
            .Where(n => n != null && n.nodeType == ScenarioNodeType.Condition)
            .OrderBy(n => n.nodeId)
            .ToList();
    }

    public int GetConditionCountForStep(string stepNodeId)
    {
        return GetConditionNodesForStep(stepNodeId).Count;
    }

    public bool IsConditionConfigured(ScenarioNode conditionNode)
    {
        if (conditionNode == null || conditionNode.nodeType != ScenarioNodeType.Condition) return false;
        return !string.IsNullOrWhiteSpace(conditionNode.condition.objectAId) &&
               !string.IsNullOrWhiteSpace(conditionNode.condition.objectBId);
    }

    public bool HasUnconfiguredConditions(ScenarioNode stepNode)
    {
        if (stepNode == null || stepNode.nodeType != ScenarioNodeType.Step) return false;

        var conditions = GetConditionNodesForStep(stepNode.nodeId);
        if (conditions.Count <= 0 || conditions.Count > MaxConditionsPerStep) return true;
        return conditions.Any(c => !IsConditionConfigured(c));
    }

    public List<ScenarioNode> GetDisplayOrderedSteps()
    {
        EnsureGraphInitialized();

        var ordered = new List<ScenarioNode>();
        var visited = new HashSet<string>();

        if (TryBuildLinearStepSequence(out var linear, out _))
        {
            foreach (var step in linear)
            {
                ordered.Add(step);
                visited.Add(step.nodeId);
            }
        }

        foreach (var step in GetNodes(ScenarioNodeType.Step))
        {
            if (visited.Contains(step.nodeId)) continue;
            ordered.Add(step);
        }

        return ordered;
    }

    public Dictionary<string, int> BuildStepIndexMap()
    {
        var map = new Dictionary<string, int>();
        var steps = GetDisplayOrderedSteps();
        for (int i = 0; i < steps.Count; i++)
        {
            if (steps[i] == null || string.IsNullOrWhiteSpace(steps[i].nodeId)) continue;
            map[steps[i].nodeId] = i + 1;
        }

        return map;
    }

    public bool TryBuildLinearStepSequence(out List<ScenarioNode> orderedSteps, out string reason)
    {
        orderedSteps = new List<ScenarioNode>();
        reason = null;
        EnsureGraphInitialized();

        var start = GetStartNode();
        var end = GetEndNode();
        if (start == null)
        {
            reason = "Start node is missing.";
            return false;
        }
        if (string.IsNullOrWhiteSpace(start.nodeId))
        {
            reason = "Start nodeId is missing.";
            return false;
        }

        if (end == null)
        {
            reason = "End node is missing.";
            return false;
        }
        if (string.IsNullOrWhiteSpace(end.nodeId))
        {
            reason = "End nodeId is missing.";
            return false;
        }

        string cursor = start.nodeId;
        var visited = new HashSet<string>();

        while (true)
        {
            var outEdges = curriculum.edges
                .Where(e => e.edgeType == ScenarioEdgeType.StepFlow && e.fromNodeId == cursor)
                .ToList();
            if (outEdges.Count != 1)
            {
                reason = $"StepFlow edge count must be 1 from node '{cursor}' (actual={outEdges.Count}).";
                return false;
            }

            var nextNode = FindNode(outEdges[0].toNodeId);
            if (nextNode == null)
            {
                reason = $"Target node '{outEdges[0].toNodeId}' not found.";
                return false;
            }

            if (nextNode.nodeType == ScenarioNodeType.End)
            {
                break;
            }

            if (nextNode.nodeType != ScenarioNodeType.Step)
            {
                reason = $"StepFlow target must be Step/End (actual={nextNode.nodeType}).";
                return false;
            }

            if (!visited.Add(nextNode.nodeId))
            {
                reason = $"Cycle detected at node '{nextNode.nodeId}'.";
                return false;
            }

            orderedSteps.Add(nextNode);
            cursor = nextNode.nodeId;
        }

        if (orderedSteps.Count != GetNodes(ScenarioNodeType.Step).Count)
        {
            reason = "Not all step nodes are included in the Start->...->End chain.";
            return false;
        }

        return true;
    }

    public GraphValidationResult ValidateGraph()
    {
        EnsureGraphInitialized();
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
            if (string.IsNullOrWhiteSpace(edge.fromNodeId) || string.IsNullOrWhiteSpace(edge.toNodeId))
            {
                result.AddError("E-11", "Edge has empty from/to nodeId.");
                continue;
            }

            var fromNode = FindNode(edge.fromNodeId);
            var toNode = FindNode(edge.toNodeId);
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

        var stepFlowEdges = curriculum.edges.Where(e => e.edgeType == ScenarioEdgeType.StepFlow).ToList();
        var conditionBindEdges = curriculum.edges.Where(e => e.edgeType == ScenarioEdgeType.ConditionBind).ToList();

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
            if (inCount != 1)
            {
                result.AddError("E-05", $"End must have exactly one incoming StepFlow edge (actual={inCount}).", endNodes[0].nodeId);
            }
        }

        foreach (var step in stepNodes)
        {
            int inCount = stepFlowEdges.Count(e => e.toNodeId == step.nodeId);
            int outCount = stepFlowEdges.Count(e => e.fromNodeId == step.nodeId);
            if (inCount > 1 || outCount > 1)
            {
                result.AddError("E-04", $"Step '{step.nodeId}' has multiple incoming or outgoing StepFlow edges.", step.nodeId);
            }
        }

        if (stepNodes.Count <= 0)
        {
            result.AddError("E-04", "No Step node exists.");
        }
        else if (!TryBuildLinearStepSequence(out _, out var linearReason))
        {
            result.AddError("E-04", $"Step chain is invalid: {linearReason}");
        }

        var placedObjects = FindObjectsByType<PlacedObject>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
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
            int bindCount = conditionBindEdges.Count(e => e.fromNodeId == condition.nodeId);
            if (bindCount != 1)
            {
                result.AddError("E-07", $"Condition '{condition.nodeId}' must bind to exactly one Step (actual={bindCount}).", condition.nodeId);
            }

            if (string.IsNullOrWhiteSpace(condition.condition.objectAId) ||
                string.IsNullOrWhiteSpace(condition.condition.objectBId))
            {
                result.AddError("E-08", $"Condition '{condition.nodeId}' has unassigned A/B object.", condition.nodeId);
            }
            else
            {
                if (condition.condition.objectAId == condition.condition.objectBId)
                {
                    result.AddError("E-09", $"Condition '{condition.nodeId}' cannot use the same object for A and B.", condition.nodeId);
                }

                if (!placedObjectIds.Contains(condition.condition.objectAId) ||
                    !placedObjectIds.Contains(condition.condition.objectBId))
                {
                    result.AddError("E-10", $"Condition '{condition.nodeId}' references missing placed object id.", condition.nodeId);
                }
            }
        }

        foreach (var step in stepNodes)
        {
            var conditions = GetConditionNodesForStep(step.nodeId);
            if (conditions.Count <= 0 || conditions.Count > MaxConditionsPerStep)
            {
                result.AddError("E-06", $"Step '{step.nodeId}' condition count out of range (actual={conditions.Count}, allowed=1..{MaxConditionsPerStep}).", step.nodeId);
            }

            if (conditions.Count == MaxConditionsPerStep)
            {
                result.AddWarning("W-01", $"Step '{step.nodeId}' reached max condition count.", step.nodeId);
            }

            var duplicateKeys = conditions
                .Select(c => $"{c.condition.type}|{c.condition.objectAId}|{c.condition.objectBId}")
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
    public ScenarioExport BuildScenarioExport()
    {
        EnsureGraphInitialized();

        if (!TryBuildLinearStepSequence(out var orderedSteps, out var reason))
        {
            throw new System.InvalidOperationException("Scenario export failed: " + reason);
        }

        var export = new ScenarioExport
        {
            version = 2,
            projectName = string.IsNullOrWhiteSpace(curriculum.projectName) ? "VRCourseEditor" : curriculum.projectName,
            scenarioSettings = new ScenarioSettingsExport
            {
                holdSeconds = curriculum.rules != null ? curriculum.rules.holdSeconds : 1.0f,
                snapDistance_m = curriculum.rules != null ? curriculum.rules.proximityDistance : 0.1f
            }
        };

        for (int i = 0; i < orderedSteps.Count; i++)
        {
            var step = orderedSteps[i];
            var action = new RequiredActionExport
            {
                id = $"act-{(i + 1).ToString("D3")}",
                name = $"\u624B\u9806 {i + 1}"
            };

            var conditions = GetConditionNodesForStep(step.nodeId);
            foreach (var condition in conditions.OrderBy(c => c.nodeId))
            {
                action.conditions.Add(new ConditionExport
                {
                    type = "SnapHold",
                    aObjectId = condition.condition.objectAId,
                    bObjectId = condition.condition.objectBId,
                    holdSeconds = export.scenarioSettings.holdSeconds
                });
            }

            export.requiredActions.Add(action);
        }

        var placed = FindObjectsByType<PlacedObject>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
            .OrderBy(p => p.id)
            .ToList();
        foreach (var po in placed)
        {
            if (po == null) continue;
            po.EnsureHasId();

            export.objects.Add(new PlacementExportObject
            {
                id = po.id,
                typeId = po.typeId,
                position = po.transform.position,
                rotation = po.transform.rotation,
                scale = po.transform.localScale
            });
        }

        return export;
    }
}

sealed class CurriculumGraphSnapshotCommand : IEditorCommand
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

