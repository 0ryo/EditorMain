using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public class StepNodeUI : MonoBehaviour
{
    const float BaseHeight = 180f;
    const float EmbeddedCardHeight = 108f;
    const float EmbeddedCardSpacing = 14f;
    const float EmbeddedListBottom = 18f;
    const float EmbeddedListSide = 16f;
    const float EmbeddedVisibleSlotHeight = 76f;

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
    readonly List<EmbeddedConditionRowBinding> runtimeConditionRows = new List<EmbeddedConditionRowBinding>();
    string currentOptionSignature = string.Empty;
    string currentConditionNodeSignature = string.Empty;
    float nextOptionPollTime;

    class EmbeddedConditionRowBinding
    {
        public string conditionNodeId;
        public ConditionRowUI row;
        public Text titleText;
        public Image dividerImage;
    }

    public Action<string> onClickInputConnector;
    public Action<string> onClickOutputConnector;
    public Action<string, Vector2> onBeginOutputConnectorDrag;
    public Action<string, Vector2> onOutputConnectorDrag;
    public Action<string, string> onCompleteConnectorDrag;
    public Action onCancelConnectorDrag;
    public Action<string> onClickDelete;
    public Action<string> onClickEmbeddedConditionDelete;
    public Action onChanged;

    public void Bind(CurriculumGraphService graph, ScenarioNode targetStep, int stepDisplayIndex)
    {
        graphService = graph;
        stepNode = targetStep;
        currentOptionSignature = string.Empty;
        currentConditionNodeSignature = string.Empty;
        nextOptionPollTime = 0f;
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
        RefreshEmbeddedConditions();
        RefreshConditionSummary();
        RefreshWarning();
    }

    void Update()
    {
        if (!isActiveAndEnabled || graphService == null || stepNode == null) return;
        if (Time.unscaledTime < nextOptionPollTime) return;

        nextOptionPollTime = Time.unscaledTime + 0.25f;
        var conditions = graphService.GetConditionNodesForStep(stepNode.nodeId);
        var signature = BuildConditionNodeSignature(conditions);
        if (signature != currentConditionNodeSignature)
        {
            RefreshEmbeddedConditions();
            return;
        }

        RefreshEmbeddedConditionOptionsIfNeeded();
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
        conditionSummaryText.text = $"\u6761\u4EF6: {conditionCount}";
    }

    void RefreshEmbeddedConditions()
    {
        ClearRuntimeConditionRows();

        if (conditionListRoot == null || conditionRowTemplate == null || graphService == null || stepNode == null)
        {
            ResizeForEmbeddedCount(0);
            return;
        }

        if (conditionRowTemplate.gameObject.activeSelf)
        {
            conditionRowTemplate.gameObject.SetActive(false);
        }

        var conditions = graphService.GetConditionNodesForStep(stepNode.nodeId);
        if (conditions.Count <= 0)
        {
            conditionListRoot.gameObject.SetActive(false);
            currentOptionSignature = string.Empty;
            currentConditionNodeSignature = string.Empty;
            ResizeForEmbeddedCount(0);
            return;
        }

        conditionListRoot.gameObject.SetActive(true);
        ConfigureEmbeddedListLayout();

        var options = PlacedObjectOptionProvider.GetOptions();
        currentOptionSignature = PlacedObjectOptionProvider.BuildSignature(options);
        currentConditionNodeSignature = BuildConditionNodeSignature(conditions);
        for (int i = 0; i < conditions.Count; i++)
        {
            var condition = conditions[i];
            if (condition == null || condition.condition == null) continue;

            bool showDivider = conditions.Count > 1 && i < conditions.Count - 1;
            var card = CreateEmbeddedConditionCard(condition.nodeId, i + 1, showDivider);
            if (card == null || card.rowHost == null) continue;

            var row = Instantiate(conditionRowTemplate, card.rowHost);
            row.gameObject.name = $"ConditionRow_{condition.nodeId}";
            row.gameObject.SetActive(true);
            var rowRt = row.transform as RectTransform;
            if (rowRt != null)
            {
                rowRt.anchorMin = Vector2.zero;
                rowRt.anchorMax = Vector2.one;
                rowRt.offsetMin = Vector2.zero;
                rowRt.offsetMax = Vector2.zero;
            }

            var binding = new EmbeddedConditionRowBinding
            {
                conditionNodeId = condition.nodeId,
                row = row,
                titleText = card.titleText,
                dividerImage = card.dividerImage
            };
            BindEmbeddedConditionRow(binding, options);
            EnsureEmbeddedDeleteButton(row, condition.nodeId);
            runtimeConditionRows.Add(binding);
        }

        ResizeForEmbeddedCount(runtimeConditionRows.Count);
    }

    void BindEmbeddedConditionRow(EmbeddedConditionRowBinding binding, List<PlacedObjectOptionProvider.Option> options)
    {
        if (binding == null || binding.row == null || graphService == null) return;

        var conditionNode = graphService.FindNode(binding.conditionNodeId);
        if (conditionNode == null || conditionNode.condition == null) return;

        binding.row.Bind(
            options,
            conditionNode.condition.objectAId,
            conditionNode.condition.objectBId,
            onAChanged: newId =>
            {
                var target = graphService.FindNode(binding.conditionNodeId);
                if (target == null || target.condition == null) return;
                target.condition.objectAId = newId;
                RefreshWarning();
                onChanged?.Invoke();
            },
            onBChanged: newId =>
            {
                var target = graphService.FindNode(binding.conditionNodeId);
                if (target == null || target.condition == null) return;
                target.condition.objectBId = newId;
                RefreshWarning();
                onChanged?.Invoke();
            }
        );
    }

    void RefreshEmbeddedConditionOptionsIfNeeded()
    {
        if (runtimeConditionRows.Count <= 0 || graphService == null) return;

        var options = PlacedObjectOptionProvider.GetOptions();
        var signature = PlacedObjectOptionProvider.BuildSignature(options);
        if (signature == currentOptionSignature) return;
        currentOptionSignature = signature;

        foreach (var binding in runtimeConditionRows)
        {
            BindEmbeddedConditionRow(binding, options);
        }
    }

    void ConfigureEmbeddedListLayout()
    {
        if (conditionListRoot == null) return;

        var layout = conditionListRoot.GetComponent<VerticalLayoutGroup>();
        if (layout != null)
        {
            layout.spacing = EmbeddedCardSpacing;
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;
        }
    }

    void ClearRuntimeConditionRows()
    {
        runtimeConditionRows.Clear();

        if (conditionListRoot == null) return;
        for (int i = conditionListRoot.childCount - 1; i >= 0; i--)
        {
            var child = conditionListRoot.GetChild(i);
            if (conditionRowTemplate != null && child == conditionRowTemplate.transform) continue;
            Destroy(child.gameObject);
        }
    }

    void ResizeForEmbeddedCount(int embeddedCount)
    {
        float embeddedHeight = 0f;
        if (embeddedCount > 0)
        {
            embeddedHeight = (embeddedCount * EmbeddedCardHeight) + ((embeddedCount - 1) * EmbeddedCardSpacing);
        }

        var root = transform as RectTransform;
        if (root != null)
        {
            float extraHeight = Mathf.Max(0f, embeddedHeight - EmbeddedVisibleSlotHeight);
            var size = root.sizeDelta;
            size.y = BaseHeight + extraHeight;
            root.sizeDelta = size;
        }

        if (conditionListRoot != null)
        {
            conditionListRoot.anchorMin = new Vector2(0f, 0f);
            conditionListRoot.anchorMax = new Vector2(1f, 0f);
            conditionListRoot.offsetMin = new Vector2(EmbeddedListSide, EmbeddedListBottom);
            conditionListRoot.offsetMax = new Vector2(-EmbeddedListSide, EmbeddedListBottom + embeddedHeight);
        }
    }

    class EmbeddedConditionCard
    {
        public RectTransform rowHost;
        public Text titleText;
        public Image dividerImage;
    }

    static string BuildConditionNodeSignature(List<ScenarioNode> conditions)
    {
        if (conditions == null || conditions.Count == 0) return string.Empty;
        return string.Join("|", conditions
            .Where(c => c != null && !string.IsNullOrWhiteSpace(c.nodeId))
            .Select(c => c.nodeId));
    }

    EmbeddedConditionCard CreateEmbeddedConditionCard(string conditionNodeId, int displayIndex, bool showDivider)
    {
        if (conditionListRoot == null) return null;

        var cardGo = new GameObject($"EmbeddedCondition_{conditionNodeId}", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        var cardRt = cardGo.GetComponent<RectTransform>();
        cardRt.SetParent(conditionListRoot, false);
        cardRt.anchorMin = new Vector2(0f, 1f);
        cardRt.anchorMax = new Vector2(1f, 1f);
        cardRt.offsetMin = Vector2.zero;
        cardRt.offsetMax = Vector2.zero;

        var cardImage = cardGo.GetComponent<Image>();
        if (cardImage != null)
        {
            cardImage.color = new Color(1f, 0.99f, 0.93f, 1f);
        }

        var layout = cardGo.GetComponent<LayoutElement>();
        if (layout != null)
        {
            layout.minHeight = EmbeddedCardHeight;
            layout.preferredHeight = EmbeddedCardHeight;
        }

        var titleGo = new GameObject("Text_Title", typeof(RectTransform), typeof(Text));
        var titleRt = titleGo.GetComponent<RectTransform>();
        titleRt.SetParent(cardRt, false);
        titleRt.anchorMin = new Vector2(0f, 1f);
        titleRt.anchorMax = new Vector2(1f, 1f);
        titleRt.offsetMin = new Vector2(10f, -22f);
        titleRt.offsetMax = new Vector2(-10f, -4f);

        var titleText = titleGo.GetComponent<Text>();
        titleText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        titleText.fontSize = 13;
        titleText.alignment = TextAnchor.MiddleLeft;
        titleText.color = new Color(0.24f, 0.24f, 0.24f, 1f);
        titleText.text = $"Condition{displayIndex}";
        titleText.raycastTarget = false;

        var rowHostGo = new GameObject("RowHost", typeof(RectTransform));
        var rowHostRt = rowHostGo.GetComponent<RectTransform>();
        rowHostRt.SetParent(cardRt, false);
        rowHostRt.anchorMin = new Vector2(0f, 0f);
        rowHostRt.anchorMax = new Vector2(1f, 1f);
        rowHostRt.offsetMin = new Vector2(8f, 8f);
        rowHostRt.offsetMax = new Vector2(-8f, -24f);

        var dividerGo = new GameObject("Divider", typeof(RectTransform), typeof(Image));
        var dividerRt = dividerGo.GetComponent<RectTransform>();
        dividerRt.SetParent(cardRt, false);
        dividerRt.anchorMin = new Vector2(0f, 0f);
        dividerRt.anchorMax = new Vector2(1f, 0f);
        dividerRt.offsetMin = new Vector2(8f, 2f);
        dividerRt.offsetMax = new Vector2(-8f, 3f);

        var dividerImage = dividerGo.GetComponent<Image>();
        dividerImage.color = new Color(0.80f, 0.80f, 0.80f, 1f);
        dividerGo.SetActive(showDivider);

        return new EmbeddedConditionCard
        {
            rowHost = rowHostRt,
            titleText = titleText,
            dividerImage = dividerImage
        };
    }

    void EnsureEmbeddedDeleteButton(ConditionRowUI row, string conditionNodeId)
    {
        if (row == null || string.IsNullOrWhiteSpace(conditionNodeId)) return;

        var existing = row.transform.Find("Button_DeleteEmbedded");
        Button button = null;
        if (existing != null)
        {
            button = existing.GetComponent<Button>();
            if (button == null) button = existing.gameObject.AddComponent<Button>();
        }

        if (button == null)
        {
            var buttonGo = new GameObject("Button_DeleteEmbedded", typeof(RectTransform), typeof(Image), typeof(Button));
            var rt = buttonGo.GetComponent<RectTransform>();
            rt.SetParent(row.transform, false);
            rt.anchorMin = new Vector2(1f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 1f);
            rt.sizeDelta = new Vector2(20f, 20f);
            rt.anchoredPosition = new Vector2(-6f, -6f);

            var labelGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
            var labelRt = labelGo.GetComponent<RectTransform>();
            labelRt.SetParent(rt, false);
            labelRt.anchorMin = Vector2.zero;
            labelRt.anchorMax = Vector2.one;
            labelRt.offsetMin = Vector2.zero;
            labelRt.offsetMax = Vector2.zero;

            var label = labelGo.GetComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.text = "X";
            label.fontSize = 12;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = new Color(0.2f, 0.2f, 0.2f, 1f);
            label.raycastTarget = false;

            button = buttonGo.GetComponent<Button>();
        }

        var image = button.GetComponent<Image>();
        if (image != null)
        {
            image.color = new Color(0.82f, 0.82f, 0.82f, 1f);
        }

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => onClickEmbeddedConditionDelete?.Invoke(conditionNodeId));
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
