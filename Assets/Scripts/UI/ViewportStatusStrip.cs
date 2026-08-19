using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ViewportStatusStrip : MonoBehaviour
{
    const float StripHeight = 40f;
    const float StripTop = -64f;
    const float StripLeftMargin = 24f;
    const float StripMaxWidth = 720f;
    const float StripRightMargin = 24f;
    const float ToastDuration = 2.2f;

    [SerializeField] RectTransform stripRoot;
    [SerializeField] RectTransform catalogPanel;
    [SerializeField] RectTransform scenarioPanel;
    [SerializeField] TMP_Text modeText;
    [SerializeField] TMP_Text targetText;
    [SerializeField] TMP_Text toastText;

    PlacementController placementController;
    SelectionService selectionService;
    EditModeService editModeService;
    CatalogUI catalogUI;
    PlacedObject selectedObject;
    string lastPlacementTypeId;
    string toastMessage;
    float toastUntil;

    void Awake()
    {
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

        var layout = text.GetComponent<LayoutElement>();
        if (layout == null) layout = text.gameObject.AddComponent<LayoutElement>();
        layout.minHeight = DesignTokens.FontSizeBody + 4f;
        layout.preferredWidth = preferredWidth;
        layout.flexibleWidth = objectName == "Text_Target" ? 1f : 0f;
        return text;
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

        if (catalogUI == null) catalogUI = FindFirstObjectByType<CatalogUI>();

        var placement = FindFirstObjectByType<PlacementController>();
        if (placement != placementController)
        {
            UnbindPlacement();
            placementController = placement;
            BindPlacement();
        }

        var selection = FindFirstObjectByType<SelectionService>();
        if (selection != selectionService)
        {
            UnbindSelection();
            selectionService = selection;
            if (selectionService != null)
            {
                selectedObject = selectionService.Current;
                selectionService.OnSelectionChanged += OnSelectionChanged;
            }
        }

        var editMode = EditModeService.I != null ? EditModeService.I : FindFirstObjectByType<EditModeService>();
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
    }

    void UnbindEditMode()
    {
        if (editModeService == null) return;
        editModeService.ModeChanged -= OnModeChanged;
    }

    void OnPlacementTypeChanged(string typeId)
    {
        lastPlacementTypeId = typeId;
        RefreshStatus();
    }

    void OnObjectPlaced(PlacedObject placed, string typeId)
    {
        selectedObject = placed;
        string objectId = placed != null ? placed.Id : string.Empty;
        toastMessage = string.IsNullOrWhiteSpace(objectId)
            ? "\u914D\u7F6E\u3057\u307E\u3057\u305F"
            : $"\u914D\u7F6E\u3057\u307E\u3057\u305F: {objectId}";
        toastUntil = Time.unscaledTime + ToastDuration;
        RefreshStatus();
        RefreshToast();
    }

    void OnSelectionChanged(PlacedObject placed)
    {
        selectedObject = placed;
        RefreshStatus();
    }

    void OnModeChanged(EditMode _)
    {
        RefreshStatus();
    }

    void RefreshStatus()
    {
        if (modeText == null || targetText == null) return;

        if (!string.IsNullOrWhiteSpace(lastPlacementTypeId))
        {
            modeText.text = "\u914D\u7F6E\u4E2D";
            targetText.text = "\u914D\u7F6E: " + BuildTypeLabel(lastPlacementTypeId);
            return;
        }

        var mode = editModeService != null ? editModeService.Mode : EditMode.Browse;
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
            EditMode.Place => "\u914D\u7F6E\u4E2D",
            EditMode.Transform => "\u79FB\u52D5\u4E2D",
            EditMode.Scale => "\u30B9\u30B1\u30FC\u30EB\u8ABF\u6574",
            _ => "\u95B2\u89A7\u4E2D",
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
        float width = Mathf.Min(StripMaxWidth, Mathf.Max(320f, canvasWidth - left - StripRightMargin));
        stripRoot.sizeDelta = new Vector2(width, StripHeight);
        stripRoot.anchoredPosition = new Vector2(left, StripTop);
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
