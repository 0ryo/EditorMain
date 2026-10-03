using System;
using System.Collections.Generic;
using System.Linq;

public partial class CurriculumGraphService
{
    // Fixed authoring UI supports a single chain. Never flatten an existing branch.
    public bool TryRemoveLinearStep(string stepNodeId, out string reason, Action<bool> appliedStateChanged = null)
    {
        reason = null;
        if (FindNode(stepNodeId)?.nodeType != ScenarioNodeType.Step)
        {
            reason = "削除する手順が見つかりません。";
            return false;
        }
        if (!TryBuildLinearStepSequence(out var steps, out reason)) return false;

        var nodes = curriculum.nodes.Where(node => node != null).ToList();
        if (nodes.Any(node => string.IsNullOrWhiteSpace(node.nodeId)) ||
            nodes.Select(node => node.nodeId).Distinct(StringComparer.Ordinal).Count() != nodes.Count ||
            nodes.Count(node => node.nodeType == ScenarioNodeType.Start) != 1 ||
            nodes.Count(node => node.nodeType == ScenarioNodeType.End) != 1 ||
            curriculum.edges.Count(edge => edge != null && edge.edgeType == ScenarioEdgeType.StepFlow) != steps.Count + 1)
        {
            reason = "一本道以外の接続、または不正なノードIDがあります。";
            return false;
        }

        var conditions = GetConditionNodesForStep(stepNodeId);
        var deletingIds = new HashSet<string>(conditions.Select(condition => condition.nodeId), StringComparer.Ordinal);
        if (curriculum.edges.Any(edge => edge != null && edge.edgeType == ScenarioEdgeType.ConditionBind &&
            deletingIds.Contains(edge.fromNodeId) && edge.toNodeId != stepNodeId))
        {
            reason = "他の手順にも接続された条件があります。";
            return false;
        }
        deletingIds.Add(stepNodeId);

        int index = steps.FindIndex(step => step.nodeId == stepNodeId);
        string previous = index > 0 ? steps[index - 1].nodeId : GetStartNode().nodeId;
        string next = index + 1 < steps.Count ? steps[index + 1].nodeId : GetEndNode().nodeId;
        return ExecuteCommand("Delete step", () =>
        {
            curriculum.nodes.RemoveAll(node => node != null && deletingIds.Contains(node.nodeId));
            curriculum.edges.RemoveAll(edge => edge != null &&
                (deletingIds.Contains(edge.fromNodeId) || deletingIds.Contains(edge.toNodeId)));
            curriculum.edges.Add(new ScenarioEdge
            {
                fromNodeId = previous,
                toNodeId = next,
                edgeType = ScenarioEdgeType.StepFlow
            });
            return true;
        }, appliedStateChanged);
    }
}
