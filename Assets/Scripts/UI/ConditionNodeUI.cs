using System;
using UnityEngine;
using UnityEngine.UI;

public class ConditionNodeUI : MonoBehaviour
{
    const float HeaderLeft = 12f;
    const float HeaderRight = -44f;
    const float HeaderBottom = -28f;
    const float HeaderTop = -8f;
    const float AreaLeft = 12f;
    const float AreaRight = -12f;
    const float AreaBottom = 16f;
    const float AreaTop = -34f;
    const float RowInset = 16f;
    const float RowGap = 8f;
    const float DropdownInsetY = 4f;
    const float RowVerticalInset = 6f;

    [Header("Basic")]
    public Text nodeIdText;
    public InputField titleInput;
    public GameObject warningIcon;
    public ConditionRowUI conditionRow;

    [Header("Connectors")]
    public Button outputConnector;
    public Button deleteButton;

    ScenarioNode conditionNode;
    CurriculumGraphService graphService;
    string currentOptionSignature = string.Empty;
    float nextOptionPollTime;

    public Action<string> onClickOutputConnector;
    public Action<string, Vector2> onBeginOutputConnectorDrag;
    public Action<string, Vector2> onOutputConnectorDrag;
    public Action<string, string> onCompleteConnectorDrag;
    public Action onCancelConnectorDrag;
    public Action<string> onClickDelete;
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
        conditionNode.condition.title = NormalizeConditionTitle(conditionNode.condition.title);

        EnsureTitleInputReference();
        ConfigureTitleInput();
        ConfigureConnectorDragHandlers();
        ConfigureDeleteButton();
        ApplyTask2VisualLayout();
        RefreshConditionOptionsIfNeeded(force: true);
        ApplyTask2VisualLayout();
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

    void ConfigureDeleteButton()
    {
        EnsureDeleteButton();
        if (deleteButton == null || conditionNode == null) return;

        deleteButton.gameObject.SetActive(true);
        deleteButton.onClick.RemoveAllListeners();
        deleteButton.onClick.AddListener(() => onClickDelete?.Invoke(conditionNode.nodeId));
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
            labelText.color = DesignTokens.Error;
            labelText.raycastTarget = false;
        }

        var image = deleteButton.GetComponent<Image>();
        if (image != null)
        {
            image.color = DesignTokens.Surface;
        }
        EnsureThinOutline(deleteButton.transform);

        var labelTextCurrent = deleteButton.GetComponentInChildren<Text>(true);
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

    static void EnsureThinOutline(Transform target)
    {
        if (target == null) return;
        if (target.GetComponent<Graphic>() == null) return;

        var outline = target.GetComponent<Outline>();
        if (outline == null) outline = target.gameObject.AddComponent<Outline>();
        outline.effectColor = DesignTokens.Divider;
        outline.effectDistance = new Vector2(0.5f, -0.5f);
        outline.useGraphicAlpha = false;
    }

    void UpdateNodeLabel()
    {
        if (nodeIdText == null) return;
        nodeIdText.text = BuildHeaderLabel();
    }

    void ApplyTask2VisualLayout()
    {
        if (titleInput != null)
        {
            titleInput.gameObject.SetActive(false);
        }

        if (nodeIdText != null)
        {
            nodeIdText.fontStyle = FontStyle.Bold;
            nodeIdText.fontSize = DesignTokens.FontSizeBody;
            nodeIdText.alignment = TextAnchor.MiddleLeft;
            nodeIdText.text = BuildHeaderLabel();
            SetTopStretchRect(nodeIdText.rectTransform, HeaderLeft, HeaderRight, HeaderBottom, HeaderTop);
        }

        var dragHandle = transform.Find("DragHandle") as RectTransform;
        if (dragHandle != null)
        {
            SetStretchRect(dragHandle, AreaLeft, AreaRight, AreaBottom, AreaTop);
            var image = dragHandle.GetComponent<Image>();
            if (image != null) image.color = DesignTokens.Surface;
            EnsureThinOutline(dragHandle);
        }

        if (conditionRow == null) return;
        var conditionArea = conditionRow.transform.parent as RectTransform;
        if (conditionArea == null) return;

        var areaLayout = conditionArea.GetComponent<VerticalLayoutGroup>();
        if (areaLayout != null) areaLayout.enabled = false;
        var areaFitter = conditionArea.GetComponent<ContentSizeFitter>();
        if (areaFitter != null) areaFitter.enabled = false;

        SetStretchRect(conditionArea, AreaLeft, AreaRight, AreaBottom, AreaTop);
        ClearContainerVisual(conditionArea);
        LayoutConditionRow(conditionRow);
    }

    static void LayoutConditionRow(ConditionRowUI row)
    {
        if (row == null) return;

        var rowRt = row.transform as RectTransform;
        if (rowRt == null) return;

        var rowLayout = rowRt.GetComponent<VerticalLayoutGroup>();
        if (rowLayout != null) rowLayout.enabled = false;

        SetStretchRect(rowRt, RowInset, -RowInset, RowInset, -RowInset);
        ClearContainerVisual(rowRt);

        var lineA = rowRt.Find("LineA") as RectTransform;
        var lineB = rowRt.Find("LineB") as RectTransform;
        if (lineA == null || lineB == null) return;
        ClearContainerVisual(lineA);
        ClearContainerVisual(lineB);

        float rowHeight = rowRt.rect.height > 1f ? rowRt.rect.height : 100f;
        float availableHeight = Mathf.Max(48f, rowHeight - (RowVerticalInset * 2f) - RowGap);
        float lineHeight = Mathf.Max(24f, availableHeight * 0.5f);
        float lineATop = -RowVerticalInset;
        float lineABottom = -(RowVerticalInset + lineHeight);
        float lineBTop = -(RowVerticalInset + lineHeight + RowGap);
        float lineBBottom = -(RowVerticalInset + lineHeight + RowGap + lineHeight);
        SetTopStretchRect(lineA, 0f, 0f, lineABottom, lineATop);
        SetTopStretchRect(lineB, 0f, 0f, lineBBottom, lineBTop);

        float rowWidth = rowRt.rect.width > 1f ? rowRt.rect.width : 300f;
        float suffixLeft = Mathf.Clamp(rowWidth * 0.66f, 170f, rowWidth - 96f);

        LayoutConditionLine(lineA, row.dropdownA, row.textAfterA, suffixLeft, "\u3092");
        LayoutConditionLine(lineB, row.dropdownB, row.textAfterB, suffixLeft, "\u306B\u8FD1\u3065\u3051\u308B");
    }

    static void ClearContainerVisual(RectTransform target)
    {
        if (target == null) return;

        var image = target.GetComponent<Image>();
        if (image != null)
        {
            // Keep container neutral so only dropdowns render visible frames.
            image.color = new Color(1f, 1f, 1f, 0f);
            image.raycastTarget = false;
        }

        var outline = target.GetComponent<Outline>();
        if (outline != null) outline.enabled = false;
    }

    static void LayoutConditionLine(RectTransform lineRt, Dropdown dropdown, Text suffix, float suffixLeft, string suffixText)
    {
        if (lineRt == null) return;

        var horizontal = lineRt.GetComponent<HorizontalLayoutGroup>();
        if (horizontal != null) horizontal.enabled = false;

        var dropdownRt = dropdown != null ? dropdown.GetComponent<RectTransform>() : null;
        if (dropdownRt != null)
        {
            dropdownRt.anchorMin = new Vector2(0f, 0f);
            dropdownRt.anchorMax = new Vector2(0f, 1f);
            dropdownRt.offsetMin = new Vector2(0f, DropdownInsetY);
            dropdownRt.offsetMax = new Vector2(suffixLeft - 8f, -DropdownInsetY);
        }

        if (suffix != null)
        {
            var suffixRt = suffix.rectTransform;
            suffixRt.anchorMin = new Vector2(0f, 0f);
            suffixRt.anchorMax = new Vector2(1f, 1f);
            suffixRt.offsetMin = new Vector2(suffixLeft, 0f);
            suffixRt.offsetMax = new Vector2(0f, 0f);
            suffix.text = suffixText;
            suffix.fontStyle = FontStyle.Bold;
            suffix.alignment = TextAnchor.MiddleLeft;
        }
    }

    string BuildHeaderLabel()
    {
        int index = ExtractTrailingNumber(conditionNode != null ? conditionNode.nodeId : null);
        if (index <= 0) index = 1;
        return $"\u624B\u9806 {index}";
    }

    static int ExtractTrailingNumber(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return 1;

        int end = value.Length - 1;
        while (end >= 0 && !char.IsDigit(value[end])) end--;
        if (end < 0) return 1;

        int start = end;
        while (start >= 0 && char.IsDigit(value[start])) start--;

        var digits = value.Substring(start + 1, end - start);
        return int.TryParse(digits, out var number) && number > 0 ? number : 1;
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

    void EnsureTitleInputReference()
    {
        if (titleInput != null) return;

        var titleTransform = transform.Find("Input_Title");
        if (titleTransform != null)
        {
            titleInput = titleTransform.GetComponent<InputField>();
        }
    }

    void ConfigureTitleInput()
    {
        if (titleInput == null || conditionNode == null || conditionNode.condition == null) return;

        titleInput.gameObject.SetActive(true);
        titleInput.onEndEdit.RemoveAllListeners();
        titleInput.readOnly = false;
        titleInput.interactable = true;
        titleInput.SetTextWithoutNotify(NormalizeConditionTitle(conditionNode.condition.title));
        titleInput.onEndEdit.AddListener(value =>
        {
            conditionNode.condition.title = NormalizeConditionTitle(value);
            titleInput.SetTextWithoutNotify(conditionNode.condition.title);
            onChanged?.Invoke();
        });
    }

    static string NormalizeConditionTitle(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return ConditionNodeData.DefaultTitle;
        }

        string trimmed = value.Trim();
        return string.IsNullOrEmpty(trimmed)
            ? ConditionNodeData.DefaultTitle
            : trimmed;
    }
}
