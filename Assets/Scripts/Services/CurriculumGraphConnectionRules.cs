using System.Collections.Generic;
using System.Linq;
using UnityEngine;

internal static class CurriculumGraphConnectionRules
{
    public static bool TryInferEdgeType(
        ScenarioNode fromNode,
        ScenarioNode toNode,
        out ScenarioEdgeType edgeType)
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

    public static bool CanAddEdge(
        Curriculum curriculum,
        ScenarioEdgeType edgeType,
        ScenarioNode fromNode,
        ScenarioNode toNode,
        out string reason)
    {
        reason = null;

        if (edgeType == ScenarioEdgeType.StepFlow)
        {
            int outCount = curriculum.edges.Count(e => e != null && e.edgeType == ScenarioEdgeType.StepFlow && e.fromNodeId == fromNode.nodeId);
            if (fromNode.nodeType == ScenarioNodeType.Start && outCount >= 1)
            {
                reason = "STEPFLOW_OUT_LIMIT";
                return false;
            }

            if (CreatesStepFlowCycle(curriculum.edges, fromNode.nodeId, toNode.nodeId))
            {
                reason = "STEPFLOW_CYCLE";
                return false;
            }

            return true;
        }

        int conditionOutCount = curriculum.edges.Count(e =>
            e != null && e.edgeType == ScenarioEdgeType.ConditionBind &&
            e.fromNodeId == fromNode.nodeId);
        if (conditionOutCount >= 1)
        {
            reason = "CONDITION_BIND_LIMIT";
            return false;
        }

        int stepConditionCount = curriculum.edges.Count(e =>
            e != null && e.edgeType == ScenarioEdgeType.ConditionBind &&
            e.toNodeId == toNode.nodeId);
        int maxConditionsPerStep = Mathf.Clamp(curriculum.rules.maxConditionsPerStep, 1, 32);
        if (stepConditionCount >= maxConditionsPerStep)
        {
            reason = "STEP_CONDITION_MAX";
            return false;
        }

        return true;
    }

    static bool CreatesStepFlowCycle(
        IEnumerable<ScenarioEdge> edges,
        string fromNodeId,
        string toNodeId)
    {
        var adjacency = edges
            .Where(e => e != null && e.edgeType == ScenarioEdgeType.StepFlow)
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
}
