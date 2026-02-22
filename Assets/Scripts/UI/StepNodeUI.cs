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
    public Button deleteButton;

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
    public Action<string> onClickDelete;
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
        ConfigureDeleteButton();

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

    void ConfigureDeleteButton()
    {
        EnsureDeleteButton();
        if (deleteButton == null || stepNode == null) return;

        deleteButton.gameObject.SetActive(true);
        deleteButton.onClick.RemoveAllListeners();
        deleteButton.onClick.AddListener(() => onClickDelete?.Invoke(stepNode.nodeId));
        deleteButton.transform.SetAsLastSibling();
    }

    void EnsureDeleteButton()
    {
        var dragHandle = transform.Find("DragHandle") as RectTransform;

        if (deleteButton == null)
        {
            var existing = transform.Find("Button_Delete");
            if (existing != null)
            {
                deleteButton = existing.GetComponent<Button>();
                if (deleteButton == null) deleteButton = existing.gameObject.AddComponent<Button>();
            }
        }

        if (deleteButton == null)
        {
            var buttonGo = new GameObject("Button_Delete", typeof(RectTransform), typeof(Image), typeof(Button));
            var rt = buttonGo.GetComponent<RectTransform>();
            rt.SetParent(dragHandle != null ? dragHandle : transform, false);

            deleteButton = buttonGo.GetComponent<Button>();

            var labelGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
            var labelRt = labelGo.GetComponent<RectTransform>();
            labelRt.SetParent(rt, false);
            labelRt.anchorMin = Vector2.zero;
            labelRt.anchorMax = Vector2.one;
            labelRt.offsetMin = Vector2.zero;
            labelRt.offsetMax = Vector2.zero;

            var labelText = labelGo.GetComponent<Text>();
            labelText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            labelText.text = "X";
            labelText.fontSize = 14;
            labelText.alignment = TextAnchor.MiddleCenter;
            labelText.color = new Color(0.45f, 0.08f, 0.08f, 1f);
            labelText.raycastTarget = false;
        }

        var image = deleteButton.GetComponent<Image>();
        if (image != null)
        {
            image.color = new Color(0.82f, 0.82f, 0.82f, 1f);
        }

        var labelTextCurrent = deleteButton.GetComponentInChildren<Text>(true);
        if (labelTextCurrent != null)
        {
            labelTextCurrent.color = new Color(0.2f, 0.2f, 0.2f, 1f);
        }

        var deleteRt = deleteButton.GetComponent<RectTransform>();
        if (deleteRt != null)
        {
            if (dragHandle != null && deleteRt.parent != dragHandle)
            {
                deleteRt.SetParent(dragHandle, false);
            }

            deleteRt.anchorMin = new Vector2(1f, 0.5f);
            deleteRt.anchorMax = new Vector2(1f, 0.5f);
            deleteRt.pivot = new Vector2(1f, 0.5f);
            deleteRt.sizeDelta = new Vector2(22f, 22f);
            deleteRt.anchoredPosition = new Vector2(-8f, 0f);
        }
    }
}
