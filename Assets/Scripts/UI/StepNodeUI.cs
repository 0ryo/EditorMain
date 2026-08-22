using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class StepNodeUI : MonoBehaviour
{
    const float BaseHeight = 180f;
    const float EmbeddedWidthScale = 1.2f;
    const float FallbackNodeWidth = 390f;
    const float EmbeddedSpacing = 8f; // カード間のVLGスペーシング（区切り線の前後各8px）
    const float EmbeddedListBottom = 18f;
    const float EmbeddedListSide = 0f;
    const float EmbeddedVisibleSlotHeight = 76f;
    const float EmbeddedFallbackHeight = 180f;
    const float EmbeddedFallbackWidth = 390f;
    const float HeaderLeft = 12f;
    const float HeaderRight = -44f;
    const float DragAreaRight = -12f;
    const float DragAreaBottom = 16f;

    [Header("Basic")]
    public TMP_Text stepIdText;
    public TMP_InputField titleInput;
    public TMP_Text conditionSummaryText;
    public GameObject warningIcon;

    [Header("Connectors")]
    public Button inputConnector;
    public Button outputConnector;
    public Button deleteButton;

    [Header("Embedded Conditions")]
    public RectTransform conditionListRoot;
    public ConditionRowUI conditionRowTemplate; // legacy serialized field (kept for prefab compatibility)
    public ConditionNodeUI embeddedConditionTemplate;

    ScenarioNode stepNode;
    CurriculumGraphService graphService;
    readonly List<ConditionNodeUI> runtimeEmbeddedConditions = new List<ConditionNodeUI>();
    string currentConditionNodeSignature = string.Empty;
    float nextPollTime;
    float baseNodeWidth = -1f;

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
        currentConditionNodeSignature = string.Empty;
        nextPollTime = 0f;
        if (stepNode == null) return;

        int displayIndex = stepDisplayIndex <= 0 ? 1 : stepDisplayIndex;
        string stepName = $"\u624B\u9806 {displayIndex}";

        if (stepIdText != null)
        {
            stepIdText.text = stepName;
            stepIdText.fontStyle = FontStyles.Bold;
        }

        if (titleInput != null)
        {
            titleInput.onEndEdit.RemoveAllListeners();
            titleInput.SetTextWithoutNotify(stepName);
            titleInput.readOnly = true;
            titleInput.interactable = false;
            titleInput.gameObject.SetActive(false);
        }

        ApplyTask1VisualLayout();
        CacheBaseNodeWidth();
        ConfigureConnectorDragHandlers();
        ConfigureDeleteButton();
        RefreshEmbeddedConditions();
        RefreshConditionSummary();
        RefreshWarning();
    }

    void Update()
    {
        if (!isActiveAndEnabled || graphService == null || stepNode == null) return;
        if (Time.unscaledTime < nextPollTime) return;

        nextPollTime = Time.unscaledTime + 0.25f;
        var conditions = graphService.GetConditionNodesForStep(stepNode.nodeId);
        var signature = BuildConditionNodeSignature(conditions);
        if (signature != currentConditionNodeSignature)
        {
            RefreshEmbeddedConditions();
            return;
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
        conditionSummaryText.text = conditionCount <= 0
            ? "\u6761\u4EF6\u3092\u8FFD\u52A0\u3057\u3066\u304F\u3060\u3055\u3044"
            : $"\u6761\u4EF6: {conditionCount}";
        conditionSummaryText.color = conditionCount <= 0
            ? DesignTokens.TextTertiary
            : DesignTokens.TextSecondary;
    }

    void RefreshEmbeddedConditions()
    {
        ClearRuntimeEmbeddedConditions();

        if (conditionListRoot == null || graphService == null || stepNode == null || embeddedConditionTemplate == null)
        {
            if (conditionListRoot != null) conditionListRoot.gameObject.SetActive(false);
            currentConditionNodeSignature = string.Empty;
            ResizeForEmbeddedCount(0, GetEmbeddedNodeHeight());
            return;
        }

        if (conditionRowTemplate != null)
        {
            conditionRowTemplate.gameObject.SetActive(false);
        }

        var conditions = graphService.GetConditionNodesForStep(stepNode.nodeId);
        currentConditionNodeSignature = BuildConditionNodeSignature(conditions);
        if (conditions.Count <= 0)
        {
            conditionListRoot.gameObject.SetActive(false);
            ResizeForEmbeddedCount(0, GetEmbeddedNodeHeight());
            return;
        }

        conditionListRoot.gameObject.SetActive(true);
        ConfigureEmbeddedListLayout();

        float embeddedNodeHeight = GetEmbeddedNodeHeight();
        float embeddedNodeWidth = GetEmbeddedNodeWidth();

        // null/未設定条件を除外してから順番にインデックスを付ける
        var validConditions = conditions
            .Where(c => c != null && c.condition != null)
            .ToList();

        for (int displayIdx = 0; displayIdx < validConditions.Count; displayIdx++)
        {
            var condition = validConditions[displayIdx];

            var conditionUi = Instantiate(embeddedConditionTemplate, conditionListRoot);
            conditionUi.gameObject.name = $"EmbeddedCondition_{condition.nodeId}";
            conditionUi.gameObject.SetActive(true);

            var conditionRt = conditionUi.transform as RectTransform;
            if (conditionRt != null)
            {
                conditionRt.anchorMin = new Vector2(0.5f, 1f);
                conditionRt.anchorMax = new Vector2(0.5f, 1f);
                conditionRt.pivot = new Vector2(0.5f, 1f);
                conditionRt.sizeDelta = new Vector2(embeddedNodeWidth, embeddedNodeHeight);
                conditionRt.anchoredPosition = Vector2.zero;
            }

            var layout = conditionUi.GetComponent<LayoutElement>();
            if (layout == null) layout = conditionUi.gameObject.AddComponent<LayoutElement>();
            layout.minWidth = embeddedNodeWidth;
            layout.preferredWidth = embeddedNodeWidth;
            layout.flexibleWidth = 0f;
            layout.minHeight = embeddedNodeHeight;
            layout.preferredHeight = embeddedNodeHeight;
            layout.flexibleHeight = 0f;

            DisableEmbeddedNodeDrag(conditionUi);

            conditionUi.onClickOutputConnector = null;
            conditionUi.onBeginOutputConnectorDrag = null;
            conditionUi.onOutputConnectorDrag = null;
            conditionUi.onCompleteConnectorDrag = null;
            conditionUi.onCancelConnectorDrag = null;
            conditionUi.onClickDelete = onClickEmbeddedConditionDelete;
            conditionUi.onChanged = () =>
            {
                RefreshWarning();
                onChanged?.Invoke();
            };
            conditionUi.Bind(graphService, condition);
            conditionUi.EnterEmbeddedMode(displayIdx + 1); // 連番ラベル + 出力コネクタ非表示
            runtimeEmbeddedConditions.Add(conditionUi);

            // カード間の区切り線（最後のカードの後は追加しない）
            if (displayIdx < validConditions.Count - 1)
            {
                AddEmbeddedDivider(conditionListRoot);
            }
        }

        ResizeForEmbeddedCount(runtimeEmbeddedConditions.Count, embeddedNodeHeight);
    }

    void ConfigureEmbeddedListLayout()
    {
        if (conditionListRoot == null) return;

        var layout = conditionListRoot.GetComponent<VerticalLayoutGroup>();
        if (layout == null) layout = conditionListRoot.gameObject.AddComponent<VerticalLayoutGroup>();

        layout.spacing = EmbeddedSpacing;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlHeight = true;
        layout.childControlWidth = false;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = false;
    }

    void ClearRuntimeEmbeddedConditions()
    {
        runtimeEmbeddedConditions.Clear();
        if (conditionListRoot == null) return;

        for (int i = conditionListRoot.childCount - 1; i >= 0; i--)
        {
            var child = conditionListRoot.GetChild(i);
            if (conditionRowTemplate != null && child == conditionRowTemplate.transform)
            {
                child.gameObject.SetActive(false);
                continue;
            }

            Destroy(child.gameObject);
        }
    }

    void ResizeForEmbeddedCount(int embeddedCount, float embeddedNodeHeight)
    {
        float embeddedHeight = 0f;
        if (embeddedCount > 0)
        {
            // VLG内の並び: [C, D, C, D, ..., C] (Condition + Divider交互、最後はC)
            // totalItems = N_conditions + (N_conditions - 1) dividers = 2N - 1
            int numDividers = embeddedCount - 1;
            int totalItems = embeddedCount + numDividers;
            float totalSpacing = totalItems > 1 ? (totalItems - 1) * EmbeddedSpacing : 0f;
            embeddedHeight = (embeddedCount * embeddedNodeHeight)
                           + (numDividers * DesignTokens.DividerHeight)
                           + totalSpacing;
        }

        var root = transform as RectTransform;
        if (root != null)
        {
            float extraHeight = Mathf.Max(0f, embeddedHeight - EmbeddedVisibleSlotHeight);
            var size = root.sizeDelta;
            float widthBase = baseNodeWidth > 1f ? baseNodeWidth : FallbackNodeWidth;
            size.x = embeddedCount > 0 ? widthBase * EmbeddedWidthScale : widthBase;
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

    float GetEmbeddedNodeHeight()
    {
        if (embeddedConditionTemplate == null) return EmbeddedFallbackHeight;

        var templateRt = embeddedConditionTemplate.transform as RectTransform;
        if (templateRt == null) return EmbeddedFallbackHeight;
        if (templateRt.rect.height > 1f) return templateRt.rect.height;
        if (templateRt.sizeDelta.y > 1f) return templateRt.sizeDelta.y;
        return EmbeddedFallbackHeight;
    }

    float GetEmbeddedNodeWidth()
    {
        if (embeddedConditionTemplate == null) return EmbeddedFallbackWidth;

        var templateRt = embeddedConditionTemplate.transform as RectTransform;
        if (templateRt == null) return EmbeddedFallbackWidth;
        if (templateRt.rect.width > 1f) return templateRt.rect.width;
        if (templateRt.sizeDelta.x > 1f) return templateRt.sizeDelta.x;
        return EmbeddedFallbackWidth;
    }

    void CacheBaseNodeWidth()
    {
        if (baseNodeWidth > 1f) return;

        var root = transform as RectTransform;
        if (root == null || root.sizeDelta.x <= 1f)
        {
            baseNodeWidth = FallbackNodeWidth;
            return;
        }

        baseNodeWidth = root.sizeDelta.x;
    }

    static string BuildConditionNodeSignature(List<ScenarioNode> conditions)
    {
        if (conditions == null || conditions.Count == 0) return string.Empty;
        return string.Join("|", conditions
            .Where(c => c != null && !string.IsNullOrWhiteSpace(c.nodeId))
            .Select(c => c.nodeId));
    }

    static void AddEmbeddedDivider(RectTransform parent)
    {
        var divGo = new GameObject("Divider", typeof(RectTransform), typeof(Image));
        var divRt = divGo.GetComponent<RectTransform>();
        divRt.SetParent(parent, false);

        var divImage = divGo.GetComponent<Image>();
        divImage.color = DesignTokens.Divider;
        divImage.raycastTarget = false;

        var divLayout = divGo.AddComponent<LayoutElement>();
        divLayout.minHeight = DesignTokens.DividerHeight;
        divLayout.preferredHeight = DesignTokens.DividerHeight;
        divLayout.flexibleWidth = 1f;
    }

    static void DisableEmbeddedNodeDrag(ConditionNodeUI conditionUi)
    {
        if (conditionUi == null) return;

        var dragHandle = conditionUi.transform.Find("DragHandle");
        if (dragHandle == null) return;

        var drag = dragHandle.GetComponent<NodeDragHandler>();
        if (drag != null)
        {
            drag.enabled = false;
        }
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
        var nodeRoot = transform as RectTransform;

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
            rt.SetParent(nodeRoot != null ? nodeRoot : transform, false);

            deleteButton = buttonGo.GetComponent<Button>();

            var labelGo = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            var labelRt = labelGo.GetComponent<RectTransform>();
            labelRt.SetParent(rt, false);
            labelRt.anchorMin = Vector2.zero;
            labelRt.anchorMax = Vector2.one;
            labelRt.offsetMin = Vector2.zero;
            labelRt.offsetMax = Vector2.zero;

            var labelText = labelGo.GetComponent<TextMeshProUGUI>();
            labelText.text = "X";
            labelText.fontSize = 14;
            labelText.alignment = TextAlignmentOptions.Center;
            labelText.color = DesignTokens.Error;
            labelText.raycastTarget = false;
        }

        var image = deleteButton.GetComponent<Image>();
        if (image != null)
        {
            image.color = DesignTokens.Surface;
        }
        EnsureThinOutline(deleteButton.transform);

        var labelTextCurrent = deleteButton.GetComponentInChildren<TMP_Text>(true);
        if (labelTextCurrent != null)
        {
            labelTextCurrent.color = DesignTokens.TextPrimary;
        }

        var deleteRt = deleteButton.GetComponent<RectTransform>();
        if (deleteRt != null)
        {
            if (nodeRoot != null && deleteRt.parent != nodeRoot)
            {
                deleteRt.SetParent(nodeRoot, false);
            }

            deleteRt.anchorMin = new Vector2(1f, 1f);
            deleteRt.anchorMax = new Vector2(1f, 1f);
            deleteRt.pivot = new Vector2(1f, 0.5f);
            deleteRt.sizeDelta = new Vector2(22f, 22f);
            deleteRt.anchoredPosition = new Vector2(-12f, -19f);
        }
    }

    void ApplyTask1VisualLayout()
    {
        EnsureThinOutline(transform);

        if (stepIdText != null)
        {
            var stepRt = stepIdText.rectTransform;
            if (stepRt != null)
            {
                SetTopStretchRect(stepRt, HeaderLeft, HeaderRight, -28f, -8f);
            }
        }

        var dragHandle = transform.Find("DragHandle") as RectTransform;
        if (dragHandle != null)
        {
            SetStretchRect(dragHandle, HeaderLeft, DragAreaRight, DragAreaBottom, -34f);
            var dragImage = dragHandle.GetComponent<Image>();
            if (dragImage != null) dragImage.color = DesignTokens.Surface;
            EnsureThinOutline(dragHandle);
        }

        if (conditionSummaryText != null)
        {
            var summaryRt = conditionSummaryText.rectTransform;
            if (summaryRt != null)
            {
                SetTopStretchRect(summaryRt, HeaderLeft, HeaderRight, -92f, -72f);
            }
        }
    }

    static void SetTopStretchRect(RectTransform rt, float left, float right, float bottom, float top)
    {
        if (rt == null) return;
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.offsetMin = new Vector2(left, bottom);
        rt.offsetMax = new Vector2(right, top);
    }

    static void SetStretchRect(RectTransform rt, float left, float right, float bottom, float top)
    {
        if (rt == null) return;
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.offsetMin = new Vector2(left, bottom);
        rt.offsetMax = new Vector2(right, top);
    }

    static void EnsureThinOutline(Transform target)
    {
        if (target == null) return;
        if (target.GetComponent<Graphic>() == null) return;

        var outline = target.GetComponent<Outline>();
        if (outline == null) outline = target.gameObject.AddComponent<Outline>();
        outline.effectColor = DesignTokens.Divider;
        outline.effectDistance = new Vector2(1f, -1f);
        outline.useGraphicAlpha = false;
    }
}
