using System;
using UnityEngine;
using UnityEngine.UI;

public class ConditionNodeUI : MonoBehaviour
{
    const string LabelUnset = "未設定";

    [Header("Basic")]
    public Text nodeIdText;
    public GameObject warningIcon;
    public ConditionRowUI conditionRow;

    [Header("Connectors")]
    public Button outputConnector;

    ScenarioNode conditionNode;
    CurriculumGraphService graphService;
    string currentOptionSignature = string.Empty;
    float nextOptionPollTime;

    public Action<string> onClickOutputConnector;
    public Action<string, Vector2> onBeginOutputConnectorDrag;
    public Action<string, Vector2> onOutputConnectorDrag;
    public Action<string, string> onCompleteConnectorDrag;
    public Action onCancelConnectorDrag;
    public Action onChanged;

    public void Bind(CurriculumGraphService graph, ScenarioNode targetCondition)
    {
        graphService = graph;
        conditionNode = targetCondition;
        currentOptionSignature = string.Empty;
        nextOptionPollTime = 0f;

        if (conditionNode == null || conditionNode.nodeType != ScenarioNodeType.Condition)
        {
            Debug.LogError("[ConditionNodeUI] Invalid bind target.");
            return;
        }

        if (conditionNode.condition == null)
        {
            conditionNode.condition = new ConditionNodeData();
        }

        ConfigureConnectorDragHandlers();
        RefreshConditionOptionsIfNeeded(force: true);
        RefreshWarning();
    }

    void Update()
    {
        if (conditionNode == null || !isActiveAndEnabled || graphService == null) return;
        if (Time.unscaledTime < nextOptionPollTime) return;

        nextOptionPollTime = Time.unscaledTime + 0.2f;
        RefreshConditionOptionsIfNeeded();
        RefreshWarning();
    }

    void RefreshConditionOptionsIfNeeded(bool force = false)
    {
        if (conditionRow == null) return;

        var options = PlacedObjectOptionProvider.GetOptions();
        var signature = PlacedObjectOptionProvider.BuildSignature(options);
        if (!force && signature == currentOptionSignature) return;
        currentOptionSignature = signature;

        conditionRow.Bind(
            options,
            conditionNode.condition.objectAId,
            conditionNode.condition.objectBId,
            onAChanged: newId =>
            {
                conditionNode.condition.objectAId = newId;
                UpdateNodeLabel();
                RefreshWarning();
                onChanged?.Invoke();
            },
            onBChanged: newId =>
            {
                conditionNode.condition.objectBId = newId;
                UpdateNodeLabel();
                RefreshWarning();
                onChanged?.Invoke();
            }
        );

        UpdateNodeLabel();
    }

    public void RefreshWarning()
    {
        if (warningIcon == null || graphService == null || conditionNode == null) return;
        warningIcon.SetActive(!graphService.IsConditionConfigured(conditionNode));
    }

    void ConfigureConnectorDragHandlers()
    {
        if (outputConnector == null || conditionNode == null) return;

        var outputDrag = outputConnector.GetComponent<ConnectorDragHandler>();
        if (outputDrag == null) outputDrag = outputConnector.gameObject.AddComponent<ConnectorDragHandler>();
        outputDrag.ConfigureOutput(
            conditionNode.nodeId,
            onBeginOutputConnectorDrag,
            onOutputConnectorDrag,
            onCompleteConnectorDrag,
            onCancelConnectorDrag
        );

        outputConnector.onClick.RemoveAllListeners();
        outputConnector.onClick.AddListener(() => onClickOutputConnector?.Invoke(conditionNode.nodeId));
    }

    void UpdateNodeLabel()
    {
        if (nodeIdText == null || conditionNode == null) return;

        string a = string.IsNullOrWhiteSpace(conditionNode.condition.objectAId) ? LabelUnset : conditionNode.condition.objectAId;
        string b = string.IsNullOrWhiteSpace(conditionNode.condition.objectBId) ? LabelUnset : conditionNode.condition.objectBId;
        nodeIdText.text = $"\"{a}\" を \"{b}\" に近づける";
    }
}
