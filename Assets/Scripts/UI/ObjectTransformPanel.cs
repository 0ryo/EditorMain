using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class ObjectTransformPanel : MonoBehaviour
{
    const string RootName = "Panel_TransformNumbers";
    const float RefreshInterval = 0.1f;

    [SerializeField] Button coordinateSpaceButton;
    [SerializeField] Button pivotModeButton;
    [SerializeField] TMP_InputField[] positionInputs;
    [SerializeField] TMP_InputField[] rotationInputs;
    [SerializeField] TMP_InputField[] scaleInputs;

    SelectionService selectionService;
    PlacedObject currentObject;
    float nextRefreshTime;

    public static ObjectTransformPanel Ensure(Transform uiRoot)
    {
        if (uiRoot == null) return null;

        var content = uiRoot.Find("Panel_Detail/Scroll_Detail/Viewport/Content") as RectTransform;
        if (content == null) return null;

        var found = content.Find(RootName);
        var panel = found != null ? found.GetComponent<ObjectTransformPanel>() : null;
        if (panel == null) panel = Build(content);

        panel.ResolveSelectionService();
        panel.WireUi();
        panel.PositionBeforeDescription(content);
        panel.RefreshFields(true);
        return panel;
    }

    void Awake()
    {
        ResolveSelectionService();
        WireUi();
    }

    void Start()
    {
        ResolveSelectionService();
        RefreshFields(true);
    }

    void LateUpdate()
    {
        ResolveSelectionService();
        if (Time.unscaledTime < nextRefreshTime) return;
        nextRefreshTime = Time.unscaledTime + RefreshInterval;
        RefreshFields(false);
    }

    void OnDestroy()
    {
        if (selectionService != null) selectionService.OnSelectionChanged -= HandleSelectionChanged;
    }

    void ResolveSelectionService()
    {
        var next = FindFirstObjectByType<SelectionService>();
        if (next == selectionService) return;

        if (selectionService != null) selectionService.OnSelectionChanged -= HandleSelectionChanged;
        selectionService = next;
        if (selectionService != null)
        {
            selectionService.OnSelectionChanged += HandleSelectionChanged;
            currentObject = selectionService.Current;
        }
        else
        {
            currentObject = null;
        }
    }

    void WireUi()
    {
        if (coordinateSpaceButton != null)
        {
            coordinateSpaceButton.onClick.RemoveListener(ToggleCoordinateSpace);
            coordinateSpaceButton.onClick.AddListener(ToggleCoordinateSpace);
        }

        if (pivotModeButton != null)
        {
            pivotModeButton.onClick.RemoveListener(TogglePivotMode);
            pivotModeButton.onClick.AddListener(TogglePivotMode);
        }

        WireInputs(positionInputs, ApplyPosition);
        WireInputs(rotationInputs, ApplyRotation);
        WireInputs(scaleInputs, ApplyScale);
    }

    static void WireInputs(TMP_InputField[] inputs, UnityEngine.Events.UnityAction<string> action)
    {
        if (inputs == null) return;
        foreach (var input in inputs)
        {
            if (input == null) continue;
            input.onEndEdit.RemoveListener(action);
            input.onEndEdit.AddListener(action);
        }
    }

    void HandleSelectionChanged(PlacedObject placed)
    {
        currentObject = placed;
        RefreshFields(true);
    }

    void ToggleCoordinateSpace()
    {
        var next = TransformToolSettings.CoordinateSpace == TransformCoordinateSpace.World
            ? TransformCoordinateSpace.Local
            : TransformCoordinateSpace.World;
        TransformToolSettings.SetCoordinateSpace(next);
        RefreshFields(true);
    }

    void TogglePivotMode()
    {
        var next = TransformToolSettings.PivotMode == TransformPivotMode.Center
            ? TransformPivotMode.Pivot
            : TransformPivotMode.Center;
        TransformToolSettings.SetPivotMode(next);
        RefreshToggleLabels();
    }

    void ApplyPosition(string _)
    {
        if (!TryReadVector(positionInputs, out var value))
        {
            RefreshFields(true);
            return;
        }

        ApplyTransform("Set position", (target, state) =>
        {
            if (TransformToolSettings.CoordinateSpace == TransformCoordinateSpace.Local)
            {
                state.localPosition = value;
            }
            else
            {
                state.localPosition = target.parent != null
                    ? target.parent.InverseTransformPoint(value)
                    : value;
            }
            return state;
        });
    }

    void ApplyRotation(string _)
    {
        if (!TryReadVector(rotationInputs, out var value))
        {
            RefreshFields(true);
            return;
        }

        ApplyTransform("Set rotation", (target, state) =>
        {
            Quaternion desired = Quaternion.Euler(value);
            if (TransformToolSettings.CoordinateSpace == TransformCoordinateSpace.Local)
            {
                state.localRotation = desired;
            }
            else
            {
                state.localRotation = target.parent != null
                    ? Quaternion.Inverse(target.parent.rotation) * desired
                    : desired;
            }
            return state;
        });
    }

    void ApplyScale(string _)
    {
        if (!TryReadVector(scaleInputs, out var value))
        {
            RefreshFields(true);
            return;
        }

        value = new Vector3(
            SanitizeScaleAxis(value.x),
            SanitizeScaleAxis(value.y),
            SanitizeScaleAxis(value.z));

        ApplyTransform("Set scale", (target, state) =>
        {
            if (TransformToolSettings.CoordinateSpace == TransformCoordinateSpace.Local || target.parent == null)
            {
                state.localScale = value;
            }
            else
            {
                Vector3 parentScale = target.parent.lossyScale;
                state.localScale = new Vector3(
                    DivideScale(value.x, parentScale.x),
                    DivideScale(value.y, parentScale.y),
                    DivideScale(value.z, parentScale.z));
            }
            return state;
        });
    }

    void ApplyTransform(
        string label,
        System.Func<Transform, TransformObjectCommand.State, TransformObjectCommand.State> buildAfter)
    {
        if (currentObject == null || buildAfter == null) return;

        var target = currentObject.transform;
        var before = TransformObjectCommand.State.Capture(target);
        var after = buildAfter(target, before);
        if (StatesEqual(before, after))
        {
            RefreshFields(true);
            return;
        }

        var command = new TransformObjectCommand(currentObject.gameObject, before, after, label);
        if (CommandService.I != null && CommandService.I.Stack != null)
        {
            CommandService.I.Stack.Execute(command);
        }
        else
        {
            command.Do();
            Debug.LogWarning("[ObjectTransformPanel] Transform applied without undo because CommandService is missing.");
        }

        RefreshFields(true);
    }

    void RefreshFields(bool force)
    {
        RefreshToggleLabels();
        bool hasSelection = currentObject != null;
        SetInputsInteractable(positionInputs, hasSelection);
        SetInputsInteractable(rotationInputs, hasSelection);
        SetInputsInteractable(scaleInputs, hasSelection);
        if (!hasSelection || (!force && IsEditingAnyInput())) return;

        var target = currentObject.transform;
        bool local = TransformToolSettings.CoordinateSpace == TransformCoordinateSpace.Local;
        SetVector(positionInputs, local ? target.localPosition : target.position);
        SetVector(rotationInputs, local ? target.localEulerAngles : target.eulerAngles);
        SetVector(scaleInputs, local ? target.localScale : target.lossyScale);
    }

    void RefreshToggleLabels()
    {
        SetButtonLabel(
            coordinateSpaceButton,
            TransformToolSettings.CoordinateSpace == TransformCoordinateSpace.World ? "座標: 世界" : "座標: ローカル");
        SetButtonLabel(
            pivotModeButton,
            TransformToolSettings.PivotMode == TransformPivotMode.Center ? "基準: 中心" : "基準: ピボット");
    }

    bool IsEditingAnyInput()
    {
        return IsAnyFocused(positionInputs) || IsAnyFocused(rotationInputs) || IsAnyFocused(scaleInputs);
    }

    static bool IsAnyFocused(TMP_InputField[] inputs)
    {
        if (inputs == null) return false;
        foreach (var input in inputs)
        {
            if (input != null && input.isFocused) return true;
        }
        return false;
    }

    static bool TryReadVector(TMP_InputField[] inputs, out Vector3 value)
    {
        value = Vector3.zero;
        if (inputs == null || inputs.Length < 3) return false;
        if (!TryParseFloat(inputs[0]?.text, out value.x)) return false;
        if (!TryParseFloat(inputs[1]?.text, out value.y)) return false;
        if (!TryParseFloat(inputs[2]?.text, out value.z)) return false;
        return true;
    }

    static bool TryParseFloat(string text, out float value)
    {
        if (float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value)) return float.IsFinite(value);
        if (float.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value)) return float.IsFinite(value);
        value = 0f;
        return false;
    }

    static void SetVector(TMP_InputField[] inputs, Vector3 value)
    {
        if (inputs == null || inputs.Length < 3) return;
        SetInputValue(inputs[0], value.x);
        SetInputValue(inputs[1], value.y);
        SetInputValue(inputs[2], value.z);
    }

    static void SetInputValue(TMP_InputField input, float value)
    {
        if (input != null) input.SetTextWithoutNotify(value.ToString("0.###", CultureInfo.InvariantCulture));
    }

    static void SetInputsInteractable(TMP_InputField[] inputs, bool interactable)
    {
        if (inputs == null) return;
        foreach (var input in inputs)
        {
            if (input != null) input.interactable = interactable;
        }
    }

    static bool StatesEqual(TransformObjectCommand.State a, TransformObjectCommand.State b)
    {
        return (a.localPosition - b.localPosition).sqrMagnitude <= 0.000001f
            && Quaternion.Angle(a.localRotation, b.localRotation) <= 0.001f
            && (a.localScale - b.localScale).sqrMagnitude <= 0.000001f;
    }

    static float SanitizeScaleAxis(float value)
    {
        if (Mathf.Abs(value) >= 0.001f) return value;
        return value < 0f ? -0.001f : 0.001f;
    }

    static float DivideScale(float value, float divisor)
    {
        if (Mathf.Abs(divisor) < 0.0001f) return value;
        return SanitizeScaleAxis(value / divisor);
    }

    void PositionBeforeDescription(RectTransform content)
    {
        if (content == null) return;
        var description = content.Find("Section_Description");
        if (description != null) transform.SetSiblingIndex(description.GetSiblingIndex());
    }

    static ObjectTransformPanel Build(RectTransform content)
    {
        var root = CreateRect(RootName, content);
        var rootImage = root.gameObject.AddComponent<Image>();
        rootImage.color = DesignTokens.Surface;
        rootImage.raycastTarget = false;

        var layout = root.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(16, 16, 8, 12);
        layout.spacing = 8f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        root.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var title = CreateText("Section_Transform", root, "Transform", DesignTokens.FontSizeSubheading, DesignTokens.TextPrimary);
        var titleLayout = title.gameObject.AddComponent<LayoutElement>();
        titleLayout.minHeight = 32f;
        titleLayout.preferredHeight = 32f;

        var toggleRow = CreateRect("Row_TransformSettings", root);
        var toggleLayout = toggleRow.gameObject.AddComponent<HorizontalLayoutGroup>();
        toggleLayout.spacing = 8f;
        toggleLayout.childControlWidth = true;
        toggleLayout.childControlHeight = true;
        toggleLayout.childForceExpandWidth = true;
        toggleLayout.childForceExpandHeight = true;
        var toggleRowLayout = toggleRow.gameObject.AddComponent<LayoutElement>();
        toggleRowLayout.minHeight = 36f;
        toggleRowLayout.preferredHeight = 36f;

        var coordinate = CreateButton("Button_CoordinateSpace", toggleRow, "座標: 世界");
        coordinate.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
        var pivot = CreateButton("Button_PivotMode", toggleRow, "基準: 中心");
        pivot.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;

        var position = CreateVectorRow(root, "Row_Position", "位置");
        var rotation = CreateVectorRow(root, "Row_Rotation", "回転");
        var scale = CreateVectorRow(root, "Row_Scale", "スケール");

        var panel = root.gameObject.AddComponent<ObjectTransformPanel>();
        panel.coordinateSpaceButton = coordinate;
        panel.pivotModeButton = pivot;
        panel.positionInputs = position;
        panel.rotationInputs = rotation;
        panel.scaleInputs = scale;

        root.gameObject.AddComponent<EditorUiInputBlocker>();
        UiRoundedTheme.ApplyToHierarchy(root, DesignTokens.CornerRadius);
        panel.PositionBeforeDescription(content);
        return panel;
    }

    static TMP_InputField[] CreateVectorRow(Transform parent, string objectName, string labelValue)
    {
        var row = CreateRect(objectName, parent);
        var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 4f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = true;
        var rowLayout = row.gameObject.AddComponent<LayoutElement>();
        rowLayout.minHeight = 40f;
        rowLayout.preferredHeight = 40f;

        var rowLabel = CreateText("Label", row, labelValue, DesignTokens.FontSizeCaption, DesignTokens.TextSecondary);
        var labelLayout = rowLabel.gameObject.AddComponent<LayoutElement>();
        labelLayout.minWidth = 48f;
        labelLayout.preferredWidth = 48f;

        var inputs = new TMP_InputField[3];
        string[] axisLabels = { "X", "Y", "Z" };
        for (int i = 0; i < 3; i++)
        {
            var axis = CreateText("Label_" + axisLabels[i], row, axisLabels[i], DesignTokens.FontSizeMicro, DesignTokens.TextSecondary);
            axis.alignment = TextAlignmentOptions.Center;
            var axisLayout = axis.gameObject.AddComponent<LayoutElement>();
            axisLayout.minWidth = 10f;
            axisLayout.preferredWidth = 10f;

            inputs[i] = CreateNumberInput("Input_" + axisLabels[i], row);
            var inputLayout = inputs[i].gameObject.AddComponent<LayoutElement>();
            inputLayout.minWidth = 38f;
            inputLayout.flexibleWidth = 1f;
        }
        return inputs;
    }

    static TMP_InputField CreateNumberInput(string objectName, Transform parent)
    {
        var root = CreateRect(objectName, parent);
        var image = root.gameObject.AddComponent<Image>();
        image.color = DesignTokens.BgSecondary;

        var viewport = CreateRect("Text Area", root);
        viewport.gameObject.AddComponent<RectMask2D>();
        SetRect(viewport, Vector2.zero, Vector2.one, new Vector2(6f, 2f), new Vector2(-6f, -2f));

        var placeholder = CreateText("Placeholder", viewport, "0", DesignTokens.FontSizeCaption, DesignTokens.TextTertiary);
        placeholder.alignment = TextAlignmentOptions.MidlineRight;
        SetRect(placeholder.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        var value = CreateText("Text", viewport, "0", DesignTokens.FontSizeCaption, DesignTokens.TextPrimary);
        value.alignment = TextAlignmentOptions.MidlineRight;
        SetRect(value.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        var input = root.gameObject.AddComponent<TMP_InputField>();
        input.targetGraphic = image;
        input.textViewport = viewport;
        input.textComponent = value;
        input.placeholder = placeholder;
        input.contentType = TMP_InputField.ContentType.DecimalNumber;
        input.lineType = TMP_InputField.LineType.SingleLine;
        input.richText = false;
        return input;
    }

    static Button CreateButton(string objectName, Transform parent, string labelValue)
    {
        var rect = CreateRect(objectName, parent);
        var image = rect.gameObject.AddComponent<Image>();
        image.color = DesignTokens.BgSecondary;
        var button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;

        var label = CreateText("Label", rect, labelValue, DesignTokens.FontSizeCaption, DesignTokens.TextPrimary);
        label.alignment = TextAlignmentOptions.Center;
        SetRect(label.rectTransform, Vector2.zero, Vector2.one, new Vector2(6f, 0f), new Vector2(-6f, 0f));
        return button;
    }

    static void SetButtonLabel(Button button, string value)
    {
        var label = button != null ? button.GetComponentInChildren<TMP_Text>(true) : null;
        if (label != null) label.text = value;
    }

    static TMP_Text CreateText(string objectName, Transform parent, string value, float fontSize, Color color)
    {
        var rect = CreateRect(objectName, parent);
        var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        text.text = value;
        text.fontSize = fontSize;
        text.color = color;
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.raycastTarget = false;
        return text;
    }

    static RectTransform CreateRect(string objectName, Transform parent)
    {
        var go = new GameObject(objectName, typeof(RectTransform));
        var rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        return rect;
    }

    static void SetRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
    }
}
