using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class CurriculumGraphService : MonoBehaviour
{
    const string StartNodeId = "start-0001";
    const string EndNodeId = "end-0001";

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
        return CurriculumGraphCommandProcessor.Execute(this, label, mutation);
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

    internal void NotifyGraphChanged()
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

        curriculum.schemaVersion = 4;
        curriculum.rules ??= new RuleSet();
        curriculum.rules.maxConditionsPerStep = Mathf.Clamp(
            curriculum.rules.maxConditionsPerStep <= 0 ? 8 : curriculum.rules.maxConditionsPerStep,
            1,
            32);

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

            node.step.title ??= string.Empty;
            node.step.body ??= string.Empty;
            node.step.supplement ??= string.Empty;
            node.step.caution ??= string.Empty;
            node.step.durationMinutes = Math.Max(0, node.step.durationMinutes);

            if (node.condition == null)
            {
                node.condition = new ConditionNodeData();
            }

            if (node.nodeType == ScenarioNodeType.Condition &&
                string.IsNullOrWhiteSpace(node.condition.title))
            {
                node.condition.title = ConditionNodeData.DefaultTitle;
            }

            if (node.nodeType == ScenarioNodeType.Condition)
            {
                ConditionTypeCatalog.Normalize(node.condition, curriculum.rules);
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
        if (!CanAddEdge(fromNodeId, toNodeId, out reason)) return false;

        var fromNode = FindNode(fromNodeId);
        var toNode = FindNode(toNodeId);
        CurriculumGraphConnectionRules.TryInferEdgeType(fromNode, toNode, out var edgeType);

        curriculum.edges.Add(new ScenarioEdge
        {
            fromNodeId = fromNodeId,
            toNodeId = toNodeId,
            edgeType = edgeType
        });

        return true;
    }

    public bool CanAddEdge(string fromNodeId, string toNodeId, out string reason)
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

        if (!CurriculumGraphConnectionRules.TryInferEdgeType(fromNode, toNode, out var edgeType))
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

        return CurriculumGraphConnectionRules.CanAddEdge(curriculum, edgeType, fromNode, toNode, out reason);
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

    public int GetMaxConditionsPerStep()
    {
        EnsureGraphInitialized();
        return Mathf.Clamp(curriculum.rules.maxConditionsPerStep, 1, 32);
    }

    public bool IsConditionConfigured(ScenarioNode conditionNode)
    {
        if (conditionNode == null || conditionNode.nodeType != ScenarioNodeType.Condition) return false;
        return ConditionTypeCatalog.Find(conditionNode.condition.type) != null &&
               !string.IsNullOrWhiteSpace(conditionNode.condition.objectAId) &&
               !string.IsNullOrWhiteSpace(conditionNode.condition.objectBId);
    }

    public bool HasUnconfiguredConditions(ScenarioNode stepNode)
    {
        if (stepNode == null || stepNode.nodeType != ScenarioNodeType.Step) return false;

        var conditions = GetConditionNodesForStep(stepNode.nodeId);
        if (conditions.Count <= 0 || conditions.Count > GetMaxConditionsPerStep()) return true;
        return conditions.Any(c => !IsConditionConfigured(c));
    }

    public List<ScenarioNode> GetDisplayOrderedSteps()
    {
        EnsureGraphInitialized();
        return CurriculumGraphTraversal.GetDisplayOrderedSteps(curriculum);
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
        EnsureGraphInitialized();
        return CurriculumGraphTraversal.TryBuildLinearStepSequence(curriculum, out orderedSteps, out reason);
    }

    public GraphValidationResult ValidateGraph()
    {
        EnsureGraphInitialized();
        return CurriculumGraphValidator.Validate(this);
    }
    public ScenarioExport BuildScenarioExport()
    {
        EnsureGraphInitialized();
        return ScenarioExportBuilder.Build(this);
    }
}

