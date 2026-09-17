using System.Collections.Generic;
using System.Linq;

// Choice branches are outgoing StepFlow edges. A merge accepts the chosen route;
// it never waits for completion of routes that were not selected.
public static class ScenarioFlow
{
    public static List<ScenarioNode> Next(Curriculum curriculum, string nodeId)
    {
        return curriculum.edges.Where(e => e != null && e.edgeType == ScenarioEdgeType.StepFlow && e.fromNodeId == nodeId)
            .Select(e => curriculum.nodes.FirstOrDefault(n => n != null && n.nodeId == e.toNodeId))
            .Where(n => n != null).ToList();
    }

    public static bool TryOrder(Curriculum curriculum, out List<ScenarioNode> steps, out string reason)
    {
        steps = new List<ScenarioNode>();
        reason = null;
        var nodes = curriculum.nodes.Where(n => n != null && n.nodeType != ScenarioNodeType.Condition).ToList();
        if (nodes.Any(n => string.IsNullOrWhiteSpace(n.nodeId)) || nodes.Select(n => n.nodeId).Distinct().Count() != nodes.Count)
        { reason = "ノードIDが空または重複しています。"; return false; }
        var starts = nodes.Where(n => n.nodeType == ScenarioNodeType.Start).ToList();
        var ends = nodes.Where(n => n.nodeType == ScenarioNodeType.End).ToList();
        if (starts.Count != 1 || ends.Count != 1) { reason = "開始と終了を一つずつ配置してください。"; return false; }
        var edges = curriculum.edges.Where(e => e != null && e.edgeType == ScenarioEdgeType.StepFlow).ToList();
        var byId = nodes.ToDictionary(n => n.nodeId);
        var outgoing = nodes.ToDictionary(n => n.nodeId, n => new List<string>());
        var incoming = nodes.ToDictionary(n => n.nodeId, n => 0);
        foreach (var edge in edges)
        {
            if (!byId.ContainsKey(edge.fromNodeId ?? "") || !byId.ContainsKey(edge.toNodeId ?? "") ||
                byId[edge.fromNodeId].nodeType == ScenarioNodeType.End || byId[edge.toNodeId].nodeType == ScenarioNodeType.Start ||
                outgoing[edge.fromNodeId].Contains(edge.toNodeId))
            { reason = "接続先が不正、または接続が重複しています。"; return false; }
            outgoing[edge.fromNodeId].Add(edge.toNodeId);
            incoming[edge.toNodeId]++;
        }
        if (outgoing[starts[0].nodeId].Count != 1) { reason = "開始から最初の手順を一つ接続してください。"; return false; }
        foreach (var node in nodes)
        {
            if (node.nodeType != ScenarioNodeType.Start && incoming[node.nodeId] == 0)
            { reason = "開始から到達できないノードがあります: " + node.nodeId; return false; }
            if (node.nodeType != ScenarioNodeType.End && outgoing[node.nodeId].Count == 0)
            { reason = "終了につながらないノードがあります: " + node.nodeId; return false; }
        }
        var queue = new Queue<string>();
        queue.Enqueue(starts[0].nodeId);
        int visited = 0;
        while (queue.Count > 0)
        {
            string id = queue.Dequeue();
            visited++;
            if (byId[id].nodeType == ScenarioNodeType.Step) steps.Add(byId[id]);
            foreach (string next in outgoing[id]) if (--incoming[next] == 0) queue.Enqueue(next);
        }
        if (visited != nodes.Count || steps.Count == 0)
        { reason = "循環、孤立した経路、または手順のない経路があります。"; return false; }
        return true;
    }
}
