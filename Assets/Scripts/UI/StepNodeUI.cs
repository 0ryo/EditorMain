using System;
using UnityEngine;
using UnityEngine.UI;

public class StepNodeUI : MonoBehaviour
{
    [Header("Basic")]
    public Text stepIdText;
    public InputField titleInput;
    public Text conditionSummaryText;
    public GameObject warningIcon;

    [Header("Connectors")]
    public Button inputConnector;
    public Button outputConnector;

    [Header("Legacy (unused in MVP-4 graph node view)")]
    public RectTransform conditionListRoot;
    public ConditionRowUI conditionRowTemplate;

    ScenarioNode stepNode;
    CurriculumGraphService graphService;

    public Action<string> onClickInputConnector;
    public Action<string> onClickOutputConnector;
    public Action<string, Vector2> onBeginOutputConnectorDrag;
    public Action<string, Vector2> onOutputConnectorDrag;
    public Action<string, string> onCompleteConnectorDrag;
    public Action onCancelConnectorDrag;
    public Action onChanged;

    public void Bind(CurriculumGraphService graph, ScenarioNode targetStep, int stepDisplayIndex)
    {
        graphService = graph;
        stepNode = targetStep;
        if (stepNode == null) return;

        int displayIndex = stepDisplayIndex <= 0 ? 1 : stepDisplayIndex;
        string stepName = $"STEP {displayIndex}";

        if (stepIdText != null)
        {
            stepIdText.text = stepName;
        }

        if (titleInput != null)
        {
            titleInput.onEndEdit.RemoveAllListeners();
            titleInput.SetTextWithoutNotify(stepName);
            titleInput.readOnly = true;
            titleInput.interactable = false;
        }

        ConfigureConnectorDragHandlers();

        if (conditionListRoot != null)
        {
            conditionListRoot.gameObject.SetActive(false);
        }

        RefreshConditionSummary();
        RefreshWarning();
    }

    public void RefreshWarning()
    {
        bool hasWarning = graphService != null && graphService.HasUnconfiguredConditions(stepNode);
        if (warningIcon != null)
        {
            warningIcon.SetActive(hasWarning);
        }
    }

    public void RefreshConditionSummary()
    {
        if (conditionSummaryText == null || graphService == null || stepNode == null) return;
        int conditionCount = graphService.GetConditionCountForStep(stepNode.nodeId);
        if (conditionCount <= 0)
        {
            conditionSummaryText.text = "条件: 0";
            return;
        }

        conditionSummaryText.text = $"条件: {conditionCount}";
    }

    void ConfigureConnectorDragHandlers()
    {
        if (inputConnector != null)
        {
            var inputDrag = inputConnector.GetComponent<ConnectorDragHandler>();
            if (inputDrag == null) inputDrag = inputConnector.gameObject.AddComponent<ConnectorDragHandler>();
            inputDrag.ConfigureInput(stepNode.nodeId);

            inputConnector.onClick.RemoveAllListeners();
            inputConnector.onClick.AddListener(() => onClickInputConnector?.Invoke(stepNode.nodeId));
        }

        if (outputConnector != null)
        {
            var outputDrag = outputConnector.GetComponent<ConnectorDragHandler>();
            if (outputDrag == null) outputDrag = outputConnector.gameObject.AddComponent<ConnectorDragHandler>();
            outputDrag.ConfigureOutput(
                stepNode.nodeId,
                onBeginOutputConnectorDrag,
                onOutputConnectorDrag,
                onCompleteConnectorDrag,
                onCancelConnectorDrag
            );

            outputConnector.onClick.RemoveAllListeners();
            outputConnector.onClick.AddListener(() => onClickOutputConnector?.Invoke(stepNode.nodeId));
        }
    }
}
