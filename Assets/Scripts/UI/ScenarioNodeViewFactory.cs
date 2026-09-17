using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

internal sealed class ScenarioNodeViewFactory
{
    public sealed class Callbacks
    {
        public Action<string> onClickInputConnector;
        public Action<string> onClickOutputConnector;
        public Action<string, Vector2> onBeginOutputConnectorDrag;
        public Action<string, Vector2> onOutputConnectorDrag;
        public Action<string, string> onCompleteConnectorDrag;
        public Action onCancelConnectorDrag;
        public Action<string> onClickDelete;
        public Action<string> onClickEmbeddedConditionDelete;
        public Action<string, bool> onDetailsExpandedChanged;
        public Action onChanged;
        public Action<ScenarioNode, RectTransform, RectTransform, RectTransform, Dictionary<string, Vector2>> registerNode;
    }

    readonly CurriculumGraphService graph;
    readonly Transform nodeParent;
    readonly StepNodeUI stepNodeTemplate;
    readonly ConditionNodeUI conditionNodeTemplate;
    readonly TerminalNodeUI startNodeTemplate;
    readonly TerminalNodeUI endNodeTemplate;
    readonly HashSet<string> expandedStepDetailNodeIds;
    readonly Callbacks callbacks;

    public ScenarioNodeViewFactory(
        CurriculumGraphService graph,
        Transform nodeParent,
        StepNodeUI stepNodeTemplate,
        ConditionNodeUI conditionNodeTemplate,
        TerminalNodeUI startNodeTemplate,
        TerminalNodeUI endNodeTemplate,
        HashSet<string> expandedStepDetailNodeIds,
        Callbacks callbacks)
    {
        this.graph = graph;
        this.nodeParent = nodeParent;
        this.stepNodeTemplate = stepNodeTemplate;
        this.conditionNodeTemplate = conditionNodeTemplate;
        this.startNodeTemplate = startNodeTemplate;
        this.endNodeTemplate = endNodeTemplate;
        this.expandedStepDetailNodeIds = expandedStepDetailNodeIds;
        this.callbacks = callbacks;
    }

    public void ClearNodes(RectTransform lineLayer)
    {
        foreach (Transform child in nodeParent)
        {
            if (child == lineLayer) continue;
            if (child == stepNodeTemplate.transform) continue;
            if (conditionNodeTemplate != null && child == conditionNodeTemplate.transform) continue;
            if (startNodeTemplate != null && child == startNodeTemplate.transform) continue;
            if (endNodeTemplate != null && child == endNodeTemplate.transform) continue;
            UnityEngine.Object.Destroy(child.gameObject);
        }
    }

    public void CreateNodes(Dictionary<string, Vector2> defaultPositions, Dictionary<string, int> stepIndexMap)
    {
        foreach (var node in graph.curriculum.nodes.Where(n => n != null).OrderBy(ScenarioGraphLayout.GetNodeSortOrder).ThenBy(n => n.nodeId))
        {
            if (string.IsNullOrWhiteSpace(node.nodeId)) continue;

            switch (node.nodeType)
            {
                case ScenarioNodeType.Start:
                    InstantiateStartNode(node, defaultPositions);
                    break;
                case ScenarioNodeType.End:
                    InstantiateEndNode(node, defaultPositions);
                    break;
                case ScenarioNodeType.Step:
                    InstantiateStepNode(node, stepIndexMap, defaultPositions);
                    break;
                case ScenarioNodeType.Condition:
                    if (!graph.IsConditionBoundToStep(node.nodeId))
                    {
                        InstantiateConditionNode(node, defaultPositions);
                    }
                    break;
            }
        }
    }

    void InstantiateStartNode(ScenarioNode node, Dictionary<string, Vector2> defaults)
    {
        if (startNodeTemplate == null) return;

        var ui = InstantiateNode(startNodeTemplate, node);
        ui.onClickOutputConnector = callbacks.onClickOutputConnector;
        ui.onBeginOutputConnectorDrag = callbacks.onBeginOutputConnectorDrag;
        ui.onOutputConnectorDrag = callbacks.onOutputConnectorDrag;
        ui.onCompleteConnectorDrag = callbacks.onCompleteConnectorDrag;
        ui.onCancelConnectorDrag = callbacks.onCancelConnectorDrag;
        ui.Bind(node, ScenarioNodeTemplateFactory.StartNodeLabel, allowInput: false, allowOutput: true);

        callbacks.registerNode(
            node,
            ui.transform as RectTransform,
            null,
            ui.outputConnector != null ? ui.outputConnector.GetComponent<RectTransform>() : null,
            defaults);
    }

    void InstantiateEndNode(ScenarioNode node, Dictionary<string, Vector2> defaults)
    {
        if (endNodeTemplate == null) return;

        var ui = InstantiateNode(endNodeTemplate, node);
        ui.onClickInputConnector = callbacks.onClickInputConnector;
        ui.Bind(node, ScenarioNodeTemplateFactory.EndNodeLabel, allowInput: true, allowOutput: false);

        callbacks.registerNode(
            node,
            ui.transform as RectTransform,
            ui.inputConnector != null ? ui.inputConnector.GetComponent<RectTransform>() : null,
            null,
            defaults);
    }

    void InstantiateStepNode(ScenarioNode node, Dictionary<string, int> stepIndexMap, Dictionary<string, Vector2> defaults)
    {
        if (stepNodeTemplate == null) return;

        var ui = InstantiateNode(stepNodeTemplate, node);
        ui.onClickInputConnector = callbacks.onClickInputConnector;
        ui.onClickOutputConnector = callbacks.onClickOutputConnector;
        ui.onBeginOutputConnectorDrag = callbacks.onBeginOutputConnectorDrag;
        ui.onOutputConnectorDrag = callbacks.onOutputConnectorDrag;
        ui.onCompleteConnectorDrag = callbacks.onCompleteConnectorDrag;
        ui.onCancelConnectorDrag = callbacks.onCancelConnectorDrag;
        ui.onClickDelete = callbacks.onClickDelete;
        ui.onClickEmbeddedConditionDelete = callbacks.onClickEmbeddedConditionDelete;
        ui.onDetailsExpandedChanged = callbacks.onDetailsExpandedChanged;
        ui.onChanged = callbacks.onChanged;
        ui.embeddedConditionTemplate = conditionNodeTemplate;

        int stepIndex = stepIndexMap.TryGetValue(node.nodeId, out var mapped) ? mapped : 0;
        ui.Bind(graph, node, stepIndex, expandedStepDetailNodeIds.Contains(node.nodeId));
        ui.RefreshConditionSummary();
        ui.RefreshWarning();

        callbacks.registerNode(
            node,
            ui.transform as RectTransform,
            ui.inputConnector != null ? ui.inputConnector.GetComponent<RectTransform>() : null,
            ui.outputConnector != null ? ui.outputConnector.GetComponent<RectTransform>() : null,
            defaults);
    }

    T InstantiateNode<T>(T template, ScenarioNode node) where T : Component
    {
        var ui = UnityEngine.Object.Instantiate(template, nodeParent);
        ui.gameObject.name = $"Node_{node.nodeId}";
        ui.gameObject.SetActive(true);
        return ui;
    }

    void InstantiateConditionNode(ScenarioNode node, Dictionary<string, Vector2> defaults)
    {
        if (conditionNodeTemplate == null) return;

        var ui = InstantiateNode(conditionNodeTemplate, node);
        ui.onClickOutputConnector = callbacks.onClickOutputConnector;
        ui.onBeginOutputConnectorDrag = callbacks.onBeginOutputConnectorDrag;
        ui.onOutputConnectorDrag = callbacks.onOutputConnectorDrag;
        ui.onCompleteConnectorDrag = callbacks.onCompleteConnectorDrag;
        ui.onCancelConnectorDrag = callbacks.onCancelConnectorDrag;
        ui.onClickDelete = callbacks.onClickDelete;
        ui.onChanged = callbacks.onChanged;
        ui.Bind(graph, node);

        callbacks.registerNode(
            node,
            ui.transform as RectTransform,
            null,
            ui.outputConnector != null ? ui.outputConnector.GetComponent<RectTransform>() : null,
            defaults);
    }
}
