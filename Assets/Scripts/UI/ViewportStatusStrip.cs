using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ViewportStatusStrip : MonoBehaviour
{
    const float StripHeight = 40f;
    const float StripTop = -12f;
    const float StripLeftMargin = 12f;
    const float StripMaxWidth = 1120f;
    const float StripRightMargin = 12f;
    const float ToastDuration = 2.2f;

    [SerializeField] RectTransform stripRoot;
    [SerializeField] RectTransform catalogPanel;
    [SerializeField] RectTransform scenarioPanel;
    [SerializeField] RectTransform editModePanel;
    [SerializeField] RectTransform hintButtonPanel;
    [SerializeField] TMP_Text modeText;
    [SerializeField] TMP_Text targetText;
    [SerializeField] TMP_Text toastText;
    [SerializeField] TMP_Text debugText;
    [SerializeField] UnityEngine.UI.Button cancelPlacementButton;

    PlacementController placementController;
    SelectionService selectionService;
    EditModeService editModeService;
    CatalogUI catalogUI;
    PlacedObject selectedObject;
    string lastPlacementTypeId;
    string toastMessage;
    float toastUntil;
    readonly Vector3[] worldCorners = new Vector3[4];
    float nextServiceResolveTime;

    void Awake()
    {
        if (GetComponentInChildren<SkillSyncDesignView>(true) != null) { enabled = false; return; }
        EnsureVisualTree();
        ResolveReferences();
        BindEvents();
        RefreshStatus();
    }

    void Start()
    {
        ResolveReferences();
        BindEvents();
        RefreshStatus();
    }

    void OnDestroy()
    {
        UnbindEvents();
    }

    void LateUpdate()
    {
        ResolveReferences();
        PositionStrip();
        RefreshToast();
        RefreshDebugLine();
    }

    void EnsureVisualTree()
    {
        if (stripRoot == null)
        {
            var found = transform.Find("ViewportStatusStrip") as RectTransform;
            if (found != null) stripRoot = found;
        }

        if (stripRoot == null)
        {
            var go = new GameObject("ViewportStatusStrip", typeof(RectTransform), typeof(Image), typeof(HorizontalLayoutGroup));
            stripRoot = go.GetComponent<RectTransform>();
            stripRoot.SetParent(transform, false);
        }

        stripRoot.anchorMin = new Vector2(0f, 1f);
        stripRoot.anchorMax = new Vector2(0f, 1f);
        stripRoot.pivot = new Vector2(0f, 1f);

        var image = stripRoot.GetComponent<Image>();
        if (image == null) image = stripRoot.gameObject.AddComponent<Image>();
        image.color = DesignTokens.Surface;
        image.raycastTarget = false;

        EnsureThinOutline(stripRoot);
        UiRoundedTheme.ApplyToHierarchy(stripRoot, DesignTokens.CornerRadius);

        var layout = stripRoot.GetComponent<HorizontalLayoutGroup>();
        if (layout == null) layout = stripRoot.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset((int)DesignTokens.SpaceMd, (int)DesignTokens.SpaceMd, (int)DesignTokens.SpaceSm, (int)DesignTokens.SpaceSm);
        layout.spacing = DesignTokens.SpaceMd;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = true;

        modeText = FindOrCreateText("Text_Mode", "\u95B2\u89A7\u4E2D", DesignTokens.Accent, 88f, TextAlignmentOptions.MidlineLeft);
        targetText = FindOrCreateText("Text_Target", "\u9078\u629E\u306A\u3057", DesignTokens.TextPrimary, 300f, TextAlignmentOptions.MidlineLeft);
        toastText = FindOrCreateText("Text_Toast", "", DesignTokens.TextSecondary, 240f, TextAlignmentOptions.MidlineLeft);
        debugText = FindOrCreateText("Text_Debug", "", DesignTokens.TextSecondary, 360f, TextAlignmentOptions.MidlineLeft);
        EnsureCancelPlacementButton();
        ViewportOutliner.Ensure(transform);
        ObjectTransformPanel.Ensure(transform);
        ViewportCameraToolbar.Ensure(transform);
        EditorProjectPanel.Ensure(transform);
        PositionStrip();
    }

    TMP_Text FindOrCreateText(string objectName, string value, Color color, float preferredWidth, TextAlignmentOptions alignment)
    {
        var found = stripRoot.Find(objectName);
        TMP_Text text = found != null ? found.GetComponent<TMP_Text>() : null;
        if (text == null)
        {
            var go = new GameObject(objectName, typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
            go.transform.SetParent(stripRoot, false);
            text = go.GetComponent<TMP_Text>();
        }

        text.text = value;
        text.fontSize = DesignTokens.FontSizeBody;
        text.color = color;
        text.alignment = alignment;
        text.raycastTarget = false;
        text.textWrappingMode = TMPro.TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Ellipsis;

        var layout = text.GetComponent<LayoutElement>();
        if (layout == null) layout = text.gameObject.AddComponent<LayoutElement>();
        layout.minHeight = DesignTokens.FontSizeBody + 4f;
        layout.preferredWidth = preferredWidth;
        layout.flexibleWidth = objectName == "Text_Target" ? 1f : 0f;
        return text;
    }

    void EnsureCancelPlacementButton()
    {
        if (cancelPlacementButton == null)
        {
            var existing = stripRoot.Find("Button_CancelPlacement");
            if (existing != null) cancelPlacementButton = existing.GetComponent<UnityEngine.UI.Button>();
        }

        if (cancelPlacementButton == null)
        {
            var go = new GameObject(
                "Button_CancelPlacement",
                typeof(RectTransform),
                typeof(Image),
                typeof(UnityEngine.UI.Button),
                typeof(LayoutElement));
            go.transform.SetParent(stripRoot, false);
            cancelPlacementButton = go.GetComponent<UnityEngine.UI.Button>();

            var labelGo = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            var labelRt = labelGo.GetComponent<RectTransform>();
            labelRt.SetParent(go.transform, false);
            labelRt.anchorMin = Vector2.zero;
            labelRt.anchorMax = Vector2.one;
            labelRt.offsetMin = new Vector2(4f, 0f);
            labelRt.offsetMax = new Vector2(-4f, 0f);

            var label = labelGo.GetComponent<TextMeshProUGUI>();
            label.text = "取消";
            label.fontSize = DesignTokens.FontSizeCaption;
            label.color = DesignTokens.TextPrimary;
            label.alignment = TextAlignmentOptions.Center;
            label.raycastTarget = false;
        }

        var image = cancelPlacementButton.GetComponent<Image>();
        if (image != null)
        {
            image.color = DesignTokens.BgSecondary;
            image.raycastTarget = true;
            cancelPlacementButton.targetGraphic = image;
            EnsureThinOutline(cancelPlacementButton.transform);
        }

        var colors = cancelPlacementButton.colors;
        colors.normalColor = DesignTokens.BgSecondary;
        colors.highlightedColor = DesignTokens.BgTertiary;
        colors.pressedColor = DesignTokens.Divider;
        colors.disabledColor = DesignTokens.BgSecondary;
        cancelPlacementButton.colors = colors;
        cancelPlacementButton.transition = UnityEngine.UI.Selectable.Transition.ColorTint;
        if (cancelPlacementButton.GetComponent<EditorUiInputBlocker>() == null)
            cancelPlacementButton.gameObject.AddComponent<EditorUiInputBlocker>();
        cancelPlacementButton.onClick.RemoveAllListeners();
        cancelPlacementButton.onClick.AddListener(CancelPlacementFromStrip);

        var layout = cancelPlacementButton.GetComponent<LayoutElement>();
        if (layout == null) layout = cancelPlacementButton.gameObject.AddComponent<LayoutElement>();
        layout.minWidth = 64f;
        layout.preferredWidth = 72f;
        layout.minHeight = DesignTokens.MinTouchTarget;
        layout.preferredHeight = DesignTokens.MinTouchTarget;
        UiAccessibilityMetrics.EnsureButtonTarget(cancelPlacementButton);
        cancelPlacementButton.gameObject.SetActive(false);
    }

    void CancelPlacementFromStrip()
    {
        ResolveReferences();
        if (placementController == null || string.IsNullOrWhiteSpace(placementController.CurrentTypeId)) return;

        placementController.CancelPlacement();
        if (editModeService != null && editModeService.Mode == EditMode.Place)
            editModeService.SetMode(EditMode.Browse);
        RefreshStatus();
    }

    void ResolveReferences()
    {
        if (catalogPanel == null)
        {
            var foundCatalog = transform.Find("Panel_Catalog") as RectTransform;
            if (foundCatalog != null) catalogPanel = foundCatalog;
        }

        if (scenarioPanel == null)
        {
            var foundScenario = transform.Find("Panel_ScenarioGraph") as RectTransform;
            if (foundScenario != null) scenarioPanel = foundScenario;
        }

        if (editModePanel == null)
        {
            editModePanel = transform.Find("EditModeRow") as RectTransform;
        }

        if (hintButtonPanel == null)
        {
            hintButtonPanel = transform.Find("Button_Hints") as RectTransform;
        }

        bool retryMissingServices = Time.unscaledTime >= nextServiceResolveTime;
        if (retryMissingServices) nextServiceResolveTime = Time.unscaledTime + 0.5f;
        if (catalogUI == null && retryMissingServices) catalogUI = FindFirstObjectByType<CatalogUI>();

        var placement = placementController != null ? placementController :
            retryMissingServices ? FindFirstObjectByType<PlacementController>() : null;
        if (placement != placementController)
        {
            UnbindPlacement();
            placementController = placement;
            BindPlacement();
        }

        var selection = selectionService != null ? selectionService :
            retryMissingServices ? FindFirstObjectByType<SelectionService>() : null;
        if (selection != selectionService)
        {
            UnbindSelection();
            selectionService = selection;
            if (selectionService != null)
            {
                selectedObject = selectionService.Current;
                selectionService.OnSelectionChanged += OnSelectionChanged;
                selectionService.OperationMessage += OnOperationMessage;
            }
        }

        var editMode = editModeService != null ? editModeService :
            EditModeService.I != null ? EditModeService.I :
            retryMissingServices ? FindFirstObjectByType<EditModeService>() : null;
        if (editMode != editModeService)
        {
            UnbindEditMode();
            editModeService = editMode;
            if (editModeService != null)
            {
                editModeService.ModeChanged += OnModeChanged;
            }
        }
    }

    void BindEvents()
    {
        BindPlacement();
        if (selectionService != null) selectionService.OnSelectionChanged -= OnSelectionChanged;
        if (selectionService != null) selectionService.OnSelectionChanged += OnSelectionChanged;
        if (selectionService != null) selectionService.OperationMessage -= OnOperationMessage;
        if (selectionService != null) selectionService.OperationMessage += OnOperationMessage;
        if (editModeService != null) editModeService.ModeChanged -= OnModeChanged;
        if (editModeService != null) editModeService.ModeChanged += OnModeChanged;
    }

    void UnbindEvents()
    {
        UnbindPlacement();
        UnbindSelection();
        UnbindEditMode();
    }

    void BindPlacement()
    {
        if (placementController == null) return;
        placementController.PlacementTypeChanged -= OnPlacementTypeChanged;
        placementController.PlacementTypeChanged += OnPlacementTypeChanged;
        placementController.ObjectPlaced -= OnObjectPlaced;
        placementController.ObjectPlaced += OnObjectPlaced;
        lastPlacementTypeId = placementController.CurrentTypeId;
    }

    void UnbindPlacement()
    {
        if (placementController == null) return;
        placementController.PlacementTypeChanged -= OnPlacementTypeChanged;
        placementController.ObjectPlaced -= OnObjectPlaced;
    }

    void UnbindSelection()
    {
        if (selectionService == null) return;
        selectionService.OnSelectionChanged -= OnSelectionChanged;
        selectionService.OperationMessage -= OnOperationMessage;
    }

    void UnbindEditMode()
    {
        if (editModeService == null) return;
        editModeService.ModeChanged -= OnModeChanged;
    }

    void OnPlacementTypeChanged(string typeId)
    {
        lastPlacementTypeId = typeId;
        if (!string.IsNullOrWhiteSpace(typeId))
        {
            toastMessage = string.Empty;
            toastUntil = 0f;
        }
        RefreshStatus();
    }

    void OnObjectPlaced(PlacedObject placed, string typeId)
    {
        selectedObject = placed;
        string objectId = placed != null ? placed.Id : string.Empty;
        if (PlacementOverlapDetector.TryFindOverlap(placed, out var overlapping))
        {
            string name = overlapping != null ? overlapping.GetDisplayName() : "別の配置物";
            toastMessage = $"重なりの可能性があります: {name}";
        }
        else
        {
            toastMessage = string.IsNullOrWhiteSpace(objectId)
                ? "\u914D\u7F6E\u3057\u307E\u3057\u305F"
                : $"\u914D\u7F6E\u3057\u307E\u3057\u305F: {objectId}";
        }
        toastUntil = Time.unscaledTime + ToastDuration;
        RefreshStatus();
        RefreshToast();
    }

    void OnSelectionChanged(PlacedObject placed)
    {
        selectedObject = placed;
        RefreshStatus();
    }

    void OnOperationMessage(string message)
    {
        toastMessage = message;
        toastUntil = Time.unscaledTime + ToastDuration;
        RefreshToast();
    }

    void OnModeChanged(EditMode _)
    {
        RefreshStatus();
    }

    void RefreshStatus()
    {
        if (modeText == null || targetText == null) return;

        if (selectionService != null && selectedObject != selectionService.Current)
        {
            selectedObject = selectionService.Current;
        }

        bool isPlacementPending = !string.IsNullOrWhiteSpace(lastPlacementTypeId);
        if (cancelPlacementButton != null)
            cancelPlacementButton.gameObject.SetActive(isPlacementPending);
        if (toastText != null) toastText.gameObject.SetActive(!isPlacementPending);
        if (debugText != null) debugText.gameObject.SetActive(!isPlacementPending);

        if (isPlacementPending)
        {
            modeText.text = "+ 配置中";
            targetText.text = $"配置対象: {BuildTypeLabel(lastPlacementTypeId)} / 3D空間をクリック";
            return;
        }

        var mode = editModeService != null ? editModeService.Mode : EditMode.Browse;
        if (mode == EditMode.Place) mode = EditMode.Browse;

        if (selectionService != null && selectionService.Selected.Count > 1)
        {
            modeText.text = BuildModeLabel(mode);
            targetText.text = $"{selectionService.Selected.Count}個を選択 / 基準: {selectedObject?.Id}";
            return;
        }
        modeText.text = BuildModeLabel(mode);
        targetText.text = selectedObject != null
            ? $"\u9078\u629E\u4E2D: {selectedObject.Id}"
            : "\u9078\u629E\u306A\u3057";
    }

    void RefreshToast()
    {
        if (toastText == null) return;

        if (!string.IsNullOrWhiteSpace(toastMessage) && Time.unscaledTime <= toastUntil)
        {
            toastText.text = toastMessage;
            toastText.color = DesignTokens.TextSecondary;
            return;
        }

        toastText.text = string.Empty;
    }

    void RefreshDebugLine()
    {
        if (debugText == null) return;

        float gridSize = placementController != null
            ? Mathf.Max(0.0001f, placementController.gridSize)
            : EditSnapSettings.GridSize;
        string snapText = !EditSnapSettings.Enabled
            ? "吸着 OFF"
            : EditSnapSettings.TemporarilyDisabled
                ? "吸着 一時解除 (Alt)"
                : $"位置 {gridSize:0.##}m / 回転 {EditSnapSettings.RotationDegrees:0.#}°";

        if (selectedObject != null)
        {
            debugText.text =
                $"{snapText} | Pos {FormatVector(selectedObject.transform.position)} | Scale {FormatVector(selectedObject.transform.localScale)}";
            return;
        }

        var cam = EditWorkspace.ResolveCamera();
        string cameraText = cam != null
            ? $"Cam {FormatVector(cam.transform.position)} / Zoom {(cam.orthographic ? cam.orthographicSize : cam.transform.position.magnitude):0.0}"
            : "Cam none";

        debugText.text = $"{snapText} | {cameraText}";
    }

    static string FormatVector(Vector3 value)
    {
        return $"({value.x:0.0},{value.y:0.0},{value.z:0.0})";
    }

    string BuildTypeLabel(string typeId)
    {
        if (catalogUI != null && catalogUI.TryGetTypeInfo(typeId, out var label, out _))
        {
            if (!string.IsNullOrWhiteSpace(label) && !string.Equals(label, typeId, System.StringComparison.Ordinal))
            {
                return $"{label} / {typeId}";
            }
        }

        return typeId;
    }

    static string BuildModeLabel(EditMode mode)
    {
        return mode switch
        {
            EditMode.Place => "+ 配置中",
            EditMode.Transform => "↔ 移動中",
            EditMode.Scale => "↕ スケール",
            _ => "○ 閲覧中",
        };
    }

    void PositionStrip()
    {
        if (stripRoot == null) return;

        var rootRt = transform as RectTransform;
        float canvasWidth = rootRt != null && rootRt.rect.width > 1f
            ? rootRt.rect.width
            : DesignTokens.ReferenceResolution.x;
        float catalogWidth = catalogPanel != null
            ? Mathf.Max(DesignTokens.CatalogMinWidth, catalogPanel.offsetMax.x)
            : DesignTokens.CatalogDefaultWidth;

        float left = catalogWidth + StripLeftMargin;
        if (TryGetHorizontalBounds(rootRt, editModePanel, out _, out var editModeRight))
        {
            left = editModeRight + StripLeftMargin;
        }

        float right = canvasWidth - StripRightMargin;
        if (TryGetHorizontalBounds(rootRt, hintButtonPanel, out var hintLeft, out _))
        {
            right = hintLeft - StripRightMargin;
        }

        float width = Mathf.Min(StripMaxWidth, Mathf.Max(0f, right - left));
        stripRoot.sizeDelta = new Vector2(width, StripHeight);
        stripRoot.anchoredPosition = new Vector2(left, StripTop);
    }

    bool TryGetHorizontalBounds(RectTransform rootRt, RectTransform target, out float left, out float right)
    {
        left = 0f;
        right = 0f;
        if (rootRt == null || target == null || !target.gameObject.activeInHierarchy) return false;

        target.GetWorldCorners(worldCorners);
        float rootLeft = rootRt.rect.xMin;
        left = rootRt.InverseTransformPoint(worldCorners[0]).x - rootLeft;
        right = rootRt.InverseTransformPoint(worldCorners[2]).x - rootLeft;
        return true;
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
