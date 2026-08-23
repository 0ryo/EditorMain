using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class ViewportOutliner : MonoBehaviour
{
    const string PanelName = "Panel_Outliner";
    const string TabsName = "Tabs_CatalogMode";

    [SerializeField] RectTransform catalogPanel;
    [SerializeField] RectTransform tabsRoot;
    [SerializeField] Button catalogTabButton;
    [SerializeField] Button outlinerTabButton;
    [SerializeField] TMP_InputField searchInput;
    [SerializeField] TMP_Text countText;
    [SerializeField] RectTransform listRoot;

    RectTransform panelRect;
    CanvasGroup panelCanvasGroup;
    SelectionService selectionService;
    PlacementController placementController;
    CommandStack commandStack;
    bool displayNameBound;
    bool showingOutliner;
    int lastObjectSignature;
    float nextSignatureCheck;

    public static ViewportOutliner Ensure(Transform uiRoot)
    {
        if (uiRoot == null) return null;

        var catalog = uiRoot.Find("Panel_Catalog") as RectTransform;
        if (catalog == null) return null;

        var found = catalog.Find(PanelName);
        var outliner = found != null ? found.GetComponent<ViewportOutliner>() : null;
        if (outliner == null) outliner = Build(catalog);

        outliner.catalogPanel = catalog;
        outliner.ResolveReferences();
        outliner.WireUi();
        outliner.ApplyCatalogLayout();
        outliner.RefreshTabContent();
        outliner.RebuildList();
        return outliner;
    }

    void Awake()
    {
        panelRect = transform as RectTransform;
        panelCanvasGroup = GetComponent<CanvasGroup>();
        if (catalogPanel == null) catalogPanel = transform.parent as RectTransform;
        ResolveReferences();
        WireUi();
    }

    void Start()
    {
        ResolveReferences();
        ApplyCatalogLayout();
        RefreshTabContent();
        RebuildList();
    }

    void LateUpdate()
    {
        ResolveReferences();
        ApplyCatalogLayout();
        RefreshTabContent();

        if (!showingOutliner || Time.unscaledTime < nextSignatureCheck) return;
        nextSignatureCheck = Time.unscaledTime + 0.5f;
        int signature = CalculateObjectSignature();
        if (signature != lastObjectSignature) RebuildList();
    }

    void OnDestroy()
    {
        UnbindSelection();
        UnbindPlacement();
        UnbindCommandStack();
        if (displayNameBound)
        {
            PlacedObject.OnDisplayNameChanged -= HandleDisplayNameChanged;
            displayNameBound = false;
        }
    }

    void ResolveReferences()
    {
        if (panelRect == null) panelRect = transform as RectTransform;
        if (panelCanvasGroup == null) panelCanvasGroup = GetComponent<CanvasGroup>();
        if (catalogPanel == null) catalogPanel = transform.parent as RectTransform;

        if (tabsRoot == null && catalogPanel != null)
        {
            tabsRoot = catalogPanel.Find(TabsName) as RectTransform;
        }
        if (catalogTabButton == null && tabsRoot != null)
        {
            catalogTabButton = tabsRoot.Find("Tab_Place")?.GetComponent<Button>();
        }
        if (outlinerTabButton == null && tabsRoot != null)
        {
            outlinerTabButton = tabsRoot.Find("Tab_Outliner")?.GetComponent<Button>();
        }

        var nextSelection = FindFirstObjectByType<SelectionService>();
        if (nextSelection != selectionService)
        {
            UnbindSelection();
            selectionService = nextSelection;
            if (selectionService != null) selectionService.OnSelectionChanged += HandleSelectionChanged;
        }

        var nextPlacement = FindFirstObjectByType<PlacementController>();
        if (nextPlacement != placementController)
        {
            UnbindPlacement();
            placementController = nextPlacement;
            if (placementController != null) placementController.ObjectPlaced += HandleObjectPlaced;
        }

        var nextStack = CommandService.I != null ? CommandService.I.Stack : null;
        if (nextStack != commandStack)
        {
            UnbindCommandStack();
            commandStack = nextStack;
            if (commandStack != null) commandStack.HistoryChanged += HandleHistoryChanged;
        }

        if (!displayNameBound)
        {
            PlacedObject.OnDisplayNameChanged += HandleDisplayNameChanged;
            displayNameBound = true;
        }
    }

    void UnbindSelection()
    {
        if (selectionService != null) selectionService.OnSelectionChanged -= HandleSelectionChanged;
    }

    void UnbindPlacement()
    {
        if (placementController != null) placementController.ObjectPlaced -= HandleObjectPlaced;
    }

    void UnbindCommandStack()
    {
        if (commandStack != null) commandStack.HistoryChanged -= HandleHistoryChanged;
    }

    void WireUi()
    {
        if (catalogTabButton != null)
        {
            catalogTabButton.onClick.RemoveListener(ShowCatalog);
            catalogTabButton.onClick.AddListener(ShowCatalog);
        }

        if (outlinerTabButton != null)
        {
            outlinerTabButton.onClick.RemoveListener(ShowOutliner);
            outlinerTabButton.onClick.AddListener(ShowOutliner);
        }

        if (searchInput != null)
        {
            searchInput.onValueChanged.RemoveListener(HandleSearchChanged);
            searchInput.onValueChanged.AddListener(HandleSearchChanged);
        }
    }

    void ShowCatalog()
    {
        showingOutliner = false;
        RefreshTabContent();
    }

    void ShowOutliner()
    {
        showingOutliner = true;
        RebuildList();
        RefreshTabContent();
    }

    void RefreshTabContent()
    {
        if (catalogPanel == null) return;

        SetCatalogElementActive("Header", false);
        SetCatalogElementActive("SearchRow", !showingOutliner);
        SetCatalogElementActive("SearchRow_Runtime", !showingOutliner);
        SetCatalogElementActive("Text_Status", !showingOutliner);
        SetCatalogElementActive("Scroll_Catalog", !showingOutliner);
        SetCatalogElementActive("Button_AddObjectBottom", !showingOutliner);
        SetCatalogElementActive("Button_AddObjectBottom_Runtime", !showingOutliner);

        if (panelCanvasGroup != null)
        {
            panelCanvasGroup.alpha = showingOutliner ? 1f : 0f;
            panelCanvasGroup.interactable = showingOutliner;
            panelCanvasGroup.blocksRaycasts = showingOutliner;
        }

        ApplyTabVisual(catalogTabButton, !showingOutliner);
        ApplyTabVisual(outlinerTabButton, showingOutliner);
        if (tabsRoot != null) tabsRoot.SetAsLastSibling();
    }

    void SetCatalogElementActive(string objectName, bool active)
    {
        var target = catalogPanel != null ? catalogPanel.Find(objectName) : null;
        if (target != null && target.gameObject.activeSelf != active) target.gameObject.SetActive(active);
    }

    static void ApplyTabVisual(Button button, bool active)
    {
        if (button == null) return;
        var image = button.GetComponent<Image>();
        if (image != null) image.color = active ? DesignTokens.Surface : DesignTokens.BgSecondary;
        var label = button.GetComponentInChildren<TMP_Text>(true);
        if (label != null) label.color = active ? DesignTokens.Accent : DesignTokens.TextSecondary;
        var outline = button.GetComponent<Outline>();
        if (outline != null) outline.effectColor = active ? DesignTokens.Accent : DesignTokens.Divider;
    }

    void ApplyCatalogLayout()
    {
        if (catalogPanel == null || panelRect == null) return;

        if (tabsRoot != null)
        {
            SetRect(tabsRoot, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(10f, -50f), new Vector2(-10f, -10f));
        }

        PositionCatalogElement("SearchRow", new Vector2(10f, -98f), new Vector2(-10f, -58f));
        PositionCatalogElement("SearchRow_Runtime", new Vector2(10f, -98f), new Vector2(-10f, -58f));
        PositionCatalogElement("Text_Status", new Vector2(14f, -122f), new Vector2(-14f, -102f));

        var scroll = catalogPanel.Find("Scroll_Catalog") as RectTransform;
        if (scroll != null)
        {
            SetRect(scroll, Vector2.zero, Vector2.one, new Vector2(8f, 56f), new Vector2(-8f, -128f));
        }

        SetRect(panelRect, Vector2.zero, Vector2.one, new Vector2(8f, 8f), new Vector2(-8f, -58f));
    }

    void PositionCatalogElement(string objectName, Vector2 offsetMin, Vector2 offsetMax)
    {
        var rect = catalogPanel != null ? catalogPanel.Find(objectName) as RectTransform : null;
        if (rect != null) SetRect(rect, new Vector2(0f, 1f), new Vector2(1f, 1f), offsetMin, offsetMax);
    }

    void HandleSearchChanged(string _)
    {
        RebuildList();
    }

    void HandleSelectionChanged(PlacedObject _)
    {
        RebuildList();
    }

    void HandleObjectPlaced(PlacedObject _, string __)
    {
        RebuildList();
    }

    void HandleHistoryChanged()
    {
        RebuildList();
    }

    void HandleDisplayNameChanged(PlacedObject _)
    {
        RebuildList();
    }

    void RebuildList()
    {
        if (listRoot == null) return;

        for (int i = listRoot.childCount - 1; i >= 0; i--)
        {
            var child = listRoot.GetChild(i);
            child.gameObject.SetActive(false);
            Destroy(child.gameObject);
        }

        var placedObjects = new List<PlacedObject>(
            FindObjectsByType<PlacedObject>(FindObjectsInactive.Exclude, FindObjectsSortMode.None));
        placedObjects.RemoveAll(placed => placed == null || !placed.gameObject.scene.IsValid());
        placedObjects.Sort(ComparePlacedObjects);

        string query = searchInput != null ? searchInput.text?.Trim() : string.Empty;
        int matchCount = 0;
        foreach (var placed in placedObjects)
        {
            if (!MatchesSearch(placed, query)) continue;
            CreateObjectRow(placed);
            matchCount++;
        }

        if (countText != null)
        {
            countText.text = string.IsNullOrWhiteSpace(query)
                ? $"配置済み {placedObjects.Count}件"
                : $"検索結果 {matchCount}/{placedObjects.Count}件";
        }

        if (matchCount == 0)
        {
            var empty = CreateText("Text_Empty", listRoot, "該当するオブジェクトはありません", DesignTokens.FontSizeCaption, DesignTokens.TextSecondary);
            empty.alignment = TextAlignmentOptions.Center;
            var element = empty.gameObject.AddComponent<LayoutElement>();
            element.minHeight = 48f;
            element.preferredHeight = 48f;
        }

        lastObjectSignature = CalculateObjectSignature();
    }

    void CreateObjectRow(PlacedObject placed)
    {
        var row = CreateRect("Row_" + SafeName(placed.Id), listRoot);
        var rowImage = row.gameObject.AddComponent<Image>();
        bool selected = selectionService != null && selectionService.Current == placed;
        rowImage.color = selected ? DesignTokens.BadgeBg(DesignTokens.Accent) : DesignTokens.Surface;

        var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(4, 4, 4, 4);
        layout.spacing = 4f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = true;

        var rowElement = row.gameObject.AddComponent<LayoutElement>();
        rowElement.minHeight = 44f;
        rowElement.preferredHeight = 44f;

        var editState = placed.GetComponent<PlacedObjectEditState>();
        if (editState == null) editState = placed.gameObject.AddComponent<PlacedObjectEditState>();

        string prefix = editState.Hidden ? "○  " : editState.Locked ? "◆  " : "●  ";
        string displayName = placed.GetDisplayName();
        if (string.IsNullOrWhiteSpace(displayName)) displayName = placed.Id;
        if (string.IsNullOrWhiteSpace(displayName)) displayName = placed.name;

        var selectButton = CreateListButton(row, "Button_Select", prefix + displayName, 0f, true);
        selectButton.interactable = !editState.Hidden && !editState.Locked;
        selectButton.onClick.AddListener(() => SelectPlacedObject(placed, editState));

        var visibilityButton = CreateListButton(row, "Button_Visibility", editState.Hidden ? "表示" : "隠す", 48f, false);
        visibilityButton.onClick.AddListener(() => ToggleVisibility(placed, editState));

        var lockButton = CreateListButton(row, "Button_Lock", editState.Locked ? "解除" : "固定", 48f, false);
        lockButton.onClick.AddListener(() => ToggleLock(placed, editState));

        UiRoundedTheme.ApplyToHierarchy(row, DesignTokens.CornerRadius);
    }

    void SelectPlacedObject(PlacedObject placed, PlacedObjectEditState editState)
    {
        if (placed == null || editState == null || editState.Hidden || editState.Locked) return;
        selectionService?.Select(placed);
    }

    void ToggleVisibility(PlacedObject placed, PlacedObjectEditState editState)
    {
        if (placed == null || editState == null) return;

        bool willHide = !editState.Hidden;
        if (willHide && selectionService != null && selectionService.Current == placed)
        {
            selectionService.Select(null);
        }
        editState.SetVisible(!willHide);
        RebuildList();
    }

    void ToggleLock(PlacedObject placed, PlacedObjectEditState editState)
    {
        if (placed == null || editState == null) return;

        bool willLock = !editState.Locked;
        if (willLock && selectionService != null && selectionService.Current == placed)
        {
            selectionService.Select(null);
        }
        editState.SetLocked(willLock);
        RebuildList();
    }

    int CalculateObjectSignature()
    {
        var placedObjects = FindObjectsByType<PlacedObject>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        unchecked
        {
            int signature = placedObjects.Length;
            foreach (var placed in placedObjects)
            {
                if (placed == null || !placed.gameObject.scene.IsValid()) continue;
                int itemSignature = placed.GetInstanceID();
                var state = placed.GetComponent<PlacedObjectEditState>();
                if (state != null)
                {
                    itemSignature = itemSignature * 397 ^ (state.Hidden ? 1 : 0);
                    itemSignature = itemSignature * 397 ^ (state.Locked ? 1 : 0);
                }
                signature ^= itemSignature;
            }
            return signature;
        }
    }

    static int ComparePlacedObjects(PlacedObject a, PlacedObject b)
    {
        string aName = a != null ? a.GetDisplayName() : string.Empty;
        string bName = b != null ? b.GetDisplayName() : string.Empty;
        int displayComparison = string.Compare(aName, bName, StringComparison.CurrentCultureIgnoreCase);
        if (displayComparison != 0) return displayComparison;
        return string.Compare(a?.Id, b?.Id, StringComparison.OrdinalIgnoreCase);
    }

    static bool MatchesSearch(PlacedObject placed, string query)
    {
        if (placed == null || string.IsNullOrWhiteSpace(query)) return true;
        return ContainsIgnoreCase(placed.GetDisplayName(), query)
            || ContainsIgnoreCase(placed.Id, query)
            || ContainsIgnoreCase(placed.TypeId, query);
    }

    static bool ContainsIgnoreCase(string source, string query)
    {
        return !string.IsNullOrEmpty(source)
            && source.IndexOf(query, StringComparison.CurrentCultureIgnoreCase) >= 0;
    }

    static string SafeName(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "Object";
        return value.Replace('/', '_').Replace('\\', '_').Replace(' ', '_');
    }

    static ViewportOutliner Build(RectTransform catalog)
    {
        var tabs = CreateRect(TabsName, catalog);
        var tabsLayout = tabs.gameObject.AddComponent<HorizontalLayoutGroup>();
        tabsLayout.spacing = 8f;
        tabsLayout.childControlWidth = true;
        tabsLayout.childControlHeight = true;
        tabsLayout.childForceExpandWidth = true;
        tabsLayout.childForceExpandHeight = true;

        var placeTab = CreateButton("Tab_Place", tabs, "配置", DesignTokens.Surface);
        var placeTabLayout = placeTab.gameObject.AddComponent<LayoutElement>();
        placeTabLayout.flexibleWidth = 1f;
        placeTab.gameObject.AddComponent<Outline>().effectDistance = new Vector2(1f, -1f);

        var outlinerTab = CreateButton("Tab_Outliner", tabs, "一覧", DesignTokens.BgSecondary);
        var outlinerTabLayout = outlinerTab.gameObject.AddComponent<LayoutElement>();
        outlinerTabLayout.flexibleWidth = 1f;
        outlinerTab.gameObject.AddComponent<Outline>().effectDistance = new Vector2(1f, -1f);

        var panel = CreateRect(PanelName, catalog);
        var panelImage = panel.gameObject.AddComponent<Image>();
        panelImage.color = DesignTokens.BgPrimary;
        panelImage.raycastTarget = false;
        var canvasGroup = panel.gameObject.AddComponent<CanvasGroup>();
        panel.gameObject.AddComponent<EditorUiInputBlocker>();

        var count = CreateText("Text_Count", panel, "配置済み 0件", DesignTokens.FontSizeCaption, DesignTokens.TextSecondary);
        SetRect(count.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(8f, -34f), new Vector2(-8f, -6f));

        var search = CreateSearchInput(panel);
        SetRect(search.transform as RectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -82f), new Vector2(0f, -42f));

        var list = CreateScrollList(panel);
        SetRect(list, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(0f, -92f));

        var outliner = panel.gameObject.AddComponent<ViewportOutliner>();
        outliner.catalogPanel = catalog;
        outliner.panelRect = panel;
        outliner.panelCanvasGroup = canvasGroup;
        outliner.tabsRoot = tabs;
        outliner.catalogTabButton = placeTab;
        outliner.outlinerTabButton = outlinerTab;
        outliner.searchInput = search;
        outliner.countText = count;
        outliner.listRoot = list.Find("Viewport/Content") as RectTransform;

        UiRoundedTheme.ApplyToHierarchy(tabs, DesignTokens.CornerRadius);
        UiRoundedTheme.ApplyToHierarchy(panel, DesignTokens.CornerRadius);
        return outliner;
    }

    static TMP_InputField CreateSearchInput(Transform parent)
    {
        var root = CreateRect("Input_OutlinerSearch", parent);
        var image = root.gameObject.AddComponent<Image>();
        image.color = DesignTokens.Surface;

        var viewport = CreateRect("Text Area", root);
        viewport.gameObject.AddComponent<RectMask2D>();
        SetRect(viewport, Vector2.zero, Vector2.one, new Vector2(12f, 4f), new Vector2(-12f, -4f));

        var placeholder = CreateText("Placeholder", viewport, "配置済みを検索...", DesignTokens.FontSizeBody, DesignTokens.TextTertiary);
        placeholder.fontStyle = FontStyles.Italic;
        SetRect(placeholder.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        var value = CreateText("Text", viewport, string.Empty, DesignTokens.FontSizeBody, DesignTokens.TextPrimary);
        SetRect(value.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        var input = root.gameObject.AddComponent<TMP_InputField>();
        input.targetGraphic = image;
        input.textViewport = viewport;
        input.textComponent = value;
        input.placeholder = placeholder;
        input.lineType = TMP_InputField.LineType.SingleLine;
        input.richText = false;
        UiRoundedTheme.ApplyToHierarchy(root, DesignTokens.CornerRadius);
        return input;
    }

    static RectTransform CreateScrollList(Transform parent)
    {
        var root = CreateRect("Scroll_Outliner", parent);
        var rootImage = root.gameObject.AddComponent<Image>();
        rootImage.color = DesignTokens.BgPrimary;
        var scroll = root.gameObject.AddComponent<ScrollRect>();

        var viewport = CreateRect("Viewport", root);
        var viewportImage = viewport.gameObject.AddComponent<Image>();
        viewportImage.color = Color.clear;
        viewportImage.raycastTarget = true;
        viewport.gameObject.AddComponent<RectMask2D>();
        SetRect(viewport, Vector2.zero, Vector2.one, new Vector2(2f, 2f), new Vector2(-2f, -2f));

        var content = CreateRect("Content", viewport);
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.offsetMin = Vector2.zero;
        content.offsetMax = Vector2.zero;

        var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 4f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scroll.viewport = viewport;
        scroll.content = content;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.scrollSensitivity = 24f;
        return root;
    }

    static Button CreateListButton(RectTransform parent, string objectName, string labelValue, float width, bool flexible)
    {
        var button = CreateButton(objectName, parent, labelValue, DesignTokens.BgSecondary);
        var element = button.gameObject.AddComponent<LayoutElement>();
        element.minHeight = 36f;
        if (flexible)
        {
            element.minWidth = 72f;
            element.flexibleWidth = 1f;
        }
        else
        {
            element.minWidth = width;
            element.preferredWidth = width;
        }

        var label = button.GetComponentInChildren<TMP_Text>(true);
        if (label != null && flexible)
        {
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.overflowMode = TextOverflowModes.Ellipsis;
        }
        return button;
    }

    static RectTransform CreateRect(string objectName, Transform parent)
    {
        var go = new GameObject(objectName, typeof(RectTransform));
        var rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        return rect;
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

    static Button CreateButton(string objectName, Transform parent, string labelValue, Color color)
    {
        var rect = CreateRect(objectName, parent);
        var image = rect.gameObject.AddComponent<Image>();
        image.color = color;
        var button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;

        var label = CreateText("Label", rect, labelValue, DesignTokens.FontSizeCaption, DesignTokens.TextPrimary);
        label.alignment = TextAlignmentOptions.Center;
        SetRect(label.rectTransform, Vector2.zero, Vector2.one, new Vector2(6f, 0f), new Vector2(-6f, 0f));
        return button;
    }

    static void SetRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
    }
}
