using System;
using System.Globalization;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ConditionNodeUI : MonoBehaviour
{
    public const float PreferredHeight = 284f;
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
    public TMP_Text nodeIdText;
    public TMP_InputField titleInput;
    public GameObject warningIcon;
    public ConditionRowUI conditionRow;
    public TMP_Dropdown conditionTypeDropdown;
    public TMP_Text distanceLabel;
    public TMP_Text holdSecondsLabel;
    public TMP_InputField distanceInput;
    public TMP_InputField holdSecondsInput;

    [Header("Connectors")]
    public Button outputConnector;
    public Button deleteButton;

    ScenarioNode conditionNode;
    CurriculumGraphService graphService;
    string currentOptionSignature = string.Empty;
    CommandStack optionCommandStack;
    PlacementController optionPlacementController;

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
        EnsureConditionEditorControls();
        ConfigureConditionEditorControls();
        ConfigureConnectorDragHandlers();
        ConfigureDeleteButton();
        ApplyTask2VisualLayout();
        BindOptionChangeSources();
        RefreshConditionOptionsIfNeeded(force: true);
        ApplyTask2VisualLayout();
        UpdateConditionLabels();
        RefreshWarning();
    }

    void OnEnable()
    {
        BindOptionChangeSources();
    }

    void OnDisable()
    {
        UnbindOptionChangeSources();
    }

    void BindOptionChangeSources()
    {
        var nextStack = CommandService.I != null ? CommandService.I.Stack : null;
        if (nextStack != optionCommandStack)
        {
            if (optionCommandStack != null) optionCommandStack.HistoryChanged -= HandleOptionSourceChanged;
            optionCommandStack = nextStack;
            if (optionCommandStack != null) optionCommandStack.HistoryChanged += HandleOptionSourceChanged;
        }

        var nextPlacement = FindFirstObjectByType<PlacementController>();
        if (nextPlacement != optionPlacementController)
        {
            if (optionPlacementController != null) optionPlacementController.ObjectPlaced -= HandleObjectPlaced;
            optionPlacementController = nextPlacement;
            if (optionPlacementController != null) optionPlacementController.ObjectPlaced += HandleObjectPlaced;
        }
    }

    void UnbindOptionChangeSources()
    {
        if (optionCommandStack != null) optionCommandStack.HistoryChanged -= HandleOptionSourceChanged;
        if (optionPlacementController != null) optionPlacementController.ObjectPlaced -= HandleObjectPlaced;
        optionCommandStack = null;
        optionPlacementController = null;
    }

    void HandleObjectPlaced(PlacedObject _, string __)
    {
        HandleOptionSourceChanged();
    }

    void HandleOptionSourceChanged()
    {
        if (!isActiveAndEnabled || conditionNode == null || graphService == null) return;
        RefreshConditionOptionsIfNeeded();
        RefreshWarning();
    }

    void EnsureConditionEditorControls()
    {
        var root = transform as RectTransform;
        if (root != null && root.sizeDelta.y < PreferredHeight)
        {
            var size = root.sizeDelta;
            size.y = PreferredHeight;
            root.sizeDelta = size;
        }

        if (conditionTypeDropdown == null)
        {
            conditionTypeDropdown = transform.Find("Dropdown_ConditionType")?.GetComponent<TMP_Dropdown>();
        }
        if (conditionTypeDropdown == null && conditionRow != null && conditionRow.dropdownA != null)
        {
            conditionTypeDropdown = Instantiate(conditionRow.dropdownA, transform);
            conditionTypeDropdown.gameObject.name = "Dropdown_ConditionType";
        }

        distanceInput = EnsureParameterInput(distanceInput, "Input_Distance", "距離 (m)");
        holdSecondsInput = EnsureParameterInput(holdSecondsInput, "Input_HoldSeconds", "保持 (秒)");
        distanceLabel = EnsureParameterLabel(distanceLabel, "Text_DistanceLabel", "距離 (m)");
        holdSecondsLabel = EnsureParameterLabel(holdSecondsLabel, "Text_HoldSecondsLabel", "保持 (秒)");
    }

    TMP_Text EnsureParameterLabel(TMP_Text current, string objectName, string value)
    {
        if (current == null) current = transform.Find(objectName)?.GetComponent<TMP_Text>();
        if (current == null)
        {
            var go = new GameObject(objectName, typeof(RectTransform), typeof(TextMeshProUGUI));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(transform, false);
            current = go.GetComponent<TextMeshProUGUI>();
        }

        current.text = value;
        current.fontSize = DesignTokens.FontSizeCaption;
        current.color = DesignTokens.TextSecondary;
        current.alignment = TextAlignmentOptions.MidlineLeft;
        current.raycastTarget = false;
        return current;
    }

    TMP_InputField EnsureParameterInput(TMP_InputField current, string objectName, string placeholder)
    {
        if (current == null) current = transform.Find(objectName)?.GetComponent<TMP_InputField>();
        if (current == null && titleInput != null)
        {
            current = Instantiate(titleInput, transform);
            current.gameObject.name = objectName;
        }
        if (current == null) return null;

        current.gameObject.SetActive(true);
        current.readOnly = false;
        current.interactable = true;
        current.contentType = TMP_InputField.ContentType.DecimalNumber;
        current.lineType = TMP_InputField.LineType.SingleLine;
        if (current.placeholder is TMP_Text placeholderText)
        {
            placeholderText.text = placeholder;
            placeholderText.color = DesignTokens.TextTertiary;
        }
        return current;
    }

    void ConfigureConditionEditorControls()
    {
        if (conditionNode?.condition == null) return;
        ConditionTypeCatalog.Normalize(conditionNode.condition, graphService != null ? graphService.curriculum.rules : null);

        if (conditionTypeDropdown != null)
        {
            var definitions = ConditionTypeCatalog.Definitions.ToList();
            if (ConditionTypeCatalog.Find(conditionNode.condition.type) == null)
            {
                definitions.Insert(0, new ConditionTypeCatalog.Definition
                {
                    id = conditionNode.condition.type,
                    label = $"未対応: {conditionNode.condition.type}",
                    parameters = Array.Empty<ConditionTypeCatalog.ParameterDefinition>()
                });
            }
            conditionTypeDropdown.onValueChanged.RemoveAllListeners();
            conditionTypeDropdown.ClearOptions();
            conditionTypeDropdown.AddOptions(definitions.Select(item => item.label).ToList());
            int selectedIndex = 0;
            for (int i = 0; i < definitions.Count; i++)
            {
                if (definitions[i].id == conditionNode.condition.type)
                {
                    selectedIndex = i;
                    break;
                }
            }
            conditionTypeDropdown.SetValueWithoutNotify(selectedIndex);
            conditionTypeDropdown.RefreshShownValue();
            conditionTypeDropdown.onValueChanged.AddListener(index =>
            {
                if (index < 0 || index >= definitions.Count) return;
                string type = definitions[index].id;
                if (conditionNode.condition.type == type) return;
                if (!ExecuteConditionEdit("Set condition type", data =>
                {
                    data.type = type;
                    ConditionTypeCatalog.Normalize(data, graphService.curriculum.rules);
                })) return;
                onChanged?.Invoke();
            });
        }

        BindNumberParameter(
            distanceInput,
            ConditionTypeCatalog.DistanceKey,
            "Set condition distance");
        BindNumberParameter(
            holdSecondsInput,
            ConditionTypeCatalog.HoldSecondsKey,
            "Set condition hold duration");

        var activeDefinition = ConditionTypeCatalog.Find(conditionNode.condition.type);
        bool usesDistance = activeDefinition?.parameters
            .Any(item => item.key == ConditionTypeCatalog.DistanceKey) == true;
        bool usesHold = activeDefinition?.parameters
            .Any(item => item.key == ConditionTypeCatalog.HoldSecondsKey) == true;
        if (distanceInput != null) distanceInput.gameObject.SetActive(usesDistance);
        if (distanceLabel != null) distanceLabel.gameObject.SetActive(usesDistance);
        if (holdSecondsInput != null) holdSecondsInput.gameObject.SetActive(usesHold);
        if (holdSecondsLabel != null) holdSecondsLabel.gameObject.SetActive(usesHold);
        UpdateConditionLabels();
    }

    void BindNumberParameter(TMP_InputField input, string key, string commandLabel)
    {
        if (input == null || conditionNode?.condition == null) return;
        float current = ConditionTypeCatalog.GetNumber(conditionNode.condition, key);
        input.onEndEdit.RemoveAllListeners();
        input.SetTextWithoutNotify(current.ToString("0.###", CultureInfo.InvariantCulture));
        input.onEndEdit.AddListener(value =>
        {
            if (!float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) &&
                !float.TryParse(value, out parsed))
            {
                input.SetTextWithoutNotify(current.ToString("0.###", CultureInfo.InvariantCulture));
                return;
            }

            if (Mathf.Approximately(current, parsed)) return;
            if (!ExecuteConditionEdit(commandLabel, data => ConditionTypeCatalog.SetNumber(data, key, parsed)))
            {
                float normalized = ConditionTypeCatalog.GetNumber(conditionNode.condition, key, current);
                input.SetTextWithoutNotify(normalized.ToString("0.###", CultureInfo.InvariantCulture));
                return;
            }
            onChanged?.Invoke();
        });
    }

    void UpdateConditionLabels()
    {
        if (conditionRow?.textAfterB == null || conditionNode?.condition == null) return;
        conditionRow.textAfterB.text = conditionNode.condition.type == ConditionTypeCatalog.SnapHold
            ? "に近づけて保持"
            : "に近づける";
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
                if (string.Equals(conditionNode.condition.objectAId, newId, StringComparison.Ordinal)) return;
                if (!ExecuteConditionEdit("Set condition object A", data => data.objectAId = newId)) return;
                UpdateNodeLabel();
                RefreshWarning();
                onChanged?.Invoke();
            },
            onBChanged: newId =>
            {
                if (string.Equals(conditionNode.condition.objectBId, newId, StringComparison.Ordinal)) return;
                if (!ExecuteConditionEdit("Set condition object B", data => data.objectBId = newId)) return;
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
            nodeIdText.fontStyle = FontStyles.Bold;
            nodeIdText.fontSize = DesignTokens.FontSizeBody;
            nodeIdText.alignment = TextAlignmentOptions.MidlineLeft;
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

        if (conditionTypeDropdown != null)
        {
            SetTopStretchRect(conditionTypeDropdown.transform as RectTransform, AreaLeft, AreaRight, -76f, -40f);
        }

        if (distanceInput != null)
        {
            SetTopStretchRect(distanceInput.transform as RectTransform, AreaLeft, -202f, -136f, -102f);
        }

        if (holdSecondsInput != null)
        {
            SetTopStretchRect(holdSecondsInput.transform as RectTransform, 202f, AreaRight, -136f, -102f);
        }

        if (distanceLabel != null)
        {
            SetTopStretchRect(distanceLabel.rectTransform, AreaLeft, -202f, -100f, -80f);
        }

        if (holdSecondsLabel != null)
        {
            SetTopStretchRect(holdSecondsLabel.rectTransform, 202f, AreaRight, -100f, -80f);
        }

        if (conditionRow == null) return;
        var conditionArea = conditionRow.transform.parent as RectTransform;
        if (conditionArea == null) return;

        var areaLayout = conditionArea.GetComponent<VerticalLayoutGroup>();
        if (areaLayout != null) areaLayout.enabled = false;
        var areaFitter = conditionArea.GetComponent<ContentSizeFitter>();
        if (areaFitter != null) areaFitter.enabled = false;

        SetStretchRect(conditionArea, AreaLeft, AreaRight, AreaBottom, -142f);
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

    static void LayoutConditionLine(RectTransform lineRt, TMP_Dropdown dropdown, TMP_Text suffix, float suffixLeft, string suffixText)
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
            suffix.fontStyle = FontStyles.Bold;
            suffix.alignment = TextAlignmentOptions.MidlineLeft;
        }
    }

    string BuildHeaderLabel()
    {
        int index = ExtractTrailingNumber(conditionNode != null ? conditionNode.nodeId : null);
        if (index <= 0) index = 1;
        return $"\u6761\u4EF6 {index}";
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

    /// <summary>
    /// 埋め込みモードに切り替える。出力コネクタを非表示にし、連番ラベルを設定する。
    /// </summary>
    public void EnterEmbeddedMode(int sequentialIndex)
    {
        if (outputConnector != null)
            outputConnector.gameObject.SetActive(false);

        if (nodeIdText != null)
            nodeIdText.text = $"\u6761\u4EF6 {sequentialIndex}";
    }

    void EnsureTitleInputReference()
    {
        if (titleInput != null) return;

        var titleTransform = transform.Find("Input_Title");
        if (titleTransform != null)
        {
            titleInput = titleTransform.GetComponent<TMP_InputField>();
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
            string normalized = NormalizeConditionTitle(value);
            if (string.Equals(conditionNode.condition.title, normalized, StringComparison.Ordinal)) return;
            if (!ExecuteConditionEdit("Rename condition", data => data.title = normalized)) return;
            titleInput.SetTextWithoutNotify(normalized);
            onChanged?.Invoke();
        });
    }

    bool ExecuteConditionEdit(string label, Action<ConditionNodeData> mutation)
    {
        if (graphService == null || conditionNode == null || mutation == null) return false;

        string nodeId = conditionNode.nodeId;
        return graphService.ExecuteCommand(label, () =>
        {
            var target = graphService.FindNode(nodeId);
            if (target == null || target.nodeType != ScenarioNodeType.Condition) return false;
            if (target.condition == null) target.condition = new ConditionNodeData();
            mutation(target.condition);
            return true;
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
