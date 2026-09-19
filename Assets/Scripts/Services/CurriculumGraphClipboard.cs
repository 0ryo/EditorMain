using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public partial class CurriculumGraphService
{
    public string CopyNodes(IEnumerable<string> selection)
    {
        var ids = new HashSet<string>(selection);
        // Copy embedded conditions with their owning step.
        foreach (var edge in curriculum.edges.Where(e => e.edgeType == ScenarioEdgeType.ConditionBind && ids.Contains(e.toNodeId)))
            ids.Add(edge.fromNodeId);
        var copy = new Curriculum();
        copy.nodes = curriculum.nodes.Where(n => ids.Contains(n.nodeId) &&
            (n.nodeType == ScenarioNodeType.Step || n.nodeType == ScenarioNodeType.Condition)).ToList();
        ids = new HashSet<string>(copy.nodes.Select(n => n.nodeId));
        copy.edges = curriculum.edges.Where(e => ids.Contains(e.fromNodeId) && ids.Contains(e.toNodeId)).ToList();
        return copy.nodes.Count == 0 ? null : JsonUtility.ToJson(copy);
    }

    public Dictionary<string, string> PasteNodes(string snapshot)
    {
        var result = new Dictionary<string, string>();
        if (string.IsNullOrEmpty(snapshot)) return result;
        var copy = JsonUtility.FromJson<Curriculum>(snapshot);
        if (copy?.nodes == null || copy.nodes.Count == 0) return result;
        if (!ExecuteCommand("Paste scenario nodes", () =>
        {
            foreach (var node in copy.nodes)
            {
                var created = node.nodeType == ScenarioNodeType.Step ? AddStep() : AddCondition();
                result.Add(node.nodeId, created.nodeId);
                created.step = node.step;
                created.condition = node.condition;
            }
            foreach (var edge in copy.edges)
                curriculum.edges.Add(new ScenarioEdge { fromNodeId = result[edge.fromNodeId], toNodeId = result[edge.toNodeId], edgeType = edge.edgeType });
            return true;
        })) result.Clear();
        return result;
    }

    public bool DeleteNodes(IEnumerable<string> selection)
    {
        var ids = selection.Where(id => FindNode(id)?.nodeType == ScenarioNodeType.Step ||
            FindNode(id)?.nodeType == ScenarioNodeType.Condition).Distinct().ToList();
        return ids.Count > 0 && ExecuteCommand("Delete scenario selection", () =>
        { foreach (var id in ids) RemoveNode(id); return true; });
    }
}
