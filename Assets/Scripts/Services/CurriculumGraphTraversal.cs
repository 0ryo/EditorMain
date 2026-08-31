using System.Collections.Generic;
using System.Linq;

internal static class CurriculumGraphTraversal
{
    public static List<ScenarioNode> GetDisplayOrderedSteps(Curriculum curriculum)
    {
        var ordered = new List<ScenarioNode>();
        var visited = new HashSet<string>();

        if (TryBuildLinearStepSequence(curriculum, out var linear, out _))
        {
            foreach (var step in linear)
            {
                ordered.Add(step);
                visited.Add(step.nodeId);
            }
        }

        foreach (var step in curriculum.nodes
                     .Where(n => n != null && n.nodeType == ScenarioNodeType.Step)
                     .OrderBy(n => n.nodeId))
        {
            if (visited.Contains(step.nodeId)) continue;
            ordered.Add(step);
        }

        return ordered;
    }

    public static bool TryBuildLinearStepSequence(
        Curriculum curriculum,
        out List<ScenarioNode> orderedSteps,
        out string reason)
    {
        orderedSteps = new List<ScenarioNode>();
        reason = null;

        var start = curriculum.nodes.FirstOrDefault(n => n != null && n.nodeType == ScenarioNodeType.Start);
        var end = curriculum.nodes.FirstOrDefault(n => n != null && n.nodeType == ScenarioNodeType.End);
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

            var nextNode = curriculum.nodes.FirstOrDefault(n => n != null && n.nodeId == outEdges[0].toNodeId);
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

        int stepCount = curriculum.nodes.Count(n => n != null && n.nodeType == ScenarioNodeType.Step);
        if (orderedSteps.Count != stepCount)
        {
            reason = "Not all step nodes are included in the Start->...->End chain.";
            return false;
        }

        return true;
    }
}
