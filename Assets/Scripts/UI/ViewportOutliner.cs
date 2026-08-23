using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class ViewportOutliner : MonoBehaviour
{
    const string PanelName = "Panel_Outliner";
    const string OpenButtonName = "Button_Outliner";
    const float PanelWidth = 320f;
    const float PanelMaxHeight = 420f;
    const float DockGap = 8f;

    [SerializeField] Button openButton;
    [SerializeField] Button closeButton;
    [SerializeField] TMP_InputField searchInput;
    [SerializeField] TMP_Text countText;
    [SerializeField] RectTransform listRoot;

    RectTransform panelRect;
    CanvasGroup panelCanvasGroup;
    SelectionService selectionService;
    PlacementController placementController;
    CommandStack commandStack;
    bool displayNameBound;
    bool expanded;
    int lastObjectSignature;
    float nextSignatureCheck;
    readonly Vector3[] worldCorners = new Vector3[4];

    public static ViewportOutliner Ensure(Transform parent)
    {
        if (parent == null) return null;

        var found = parent.Find(PanelName);
        var outliner = found != null ? found.GetComponent<ViewportOutliner>() : null;
        if (outliner == null) outliner = Build(parent);

        outliner.ResolveReferences();
        outliner.WireUi();
        outliner.PositionDockedElements();
        outliner.RebuildList();
        return outliner;
    }

    void Awake()
    {
        panelRect = transform as RectTransform;
        panelCanvasGroup = GetComponent<CanvasGroup>();
        ResolveReferences();
        WireUi();
    }

    void Start()
    {
        ResolveReferences();
        RebuildList();
    }

    void LateUpdate()
    {
        ResolveReferences();
        PositionDockedElements();

        if (!expanded || Time.unscaledTime < nextSignatureCheck) return;
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

        var parent = transform.parent;
        if (openButton == null && parent != null)
        {
            openButton = parent.Find(OpenButtonName)?.GetComponent<Button>();
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
        if (openButton != null)
        {
            openButton.onClick.RemoveListener(ToggleExpanded);
            openButton.onClick.AddListener(ToggleExpanded);
        }

        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(Close);
            closeButton.onClick.AddListener(Close);
        }

        if (searchInput != null)
        {
            searchInput.onValueChanged.RemoveListener(HandleSearchChanged);
            searchInput.onValueChanged.AddListener(HandleSearchChanged);
        }
    }

    void ToggleExpanded()
    {
        SetExpanded(!expanded);
    }

    void Close()
    {
        SetExpanded(false);
    }

    void SetExpanded(bool value)
    {
        expanded = value;
        if (panelCanvasGroup != null)
        {
            panelCanvasGroup.alpha = expanded ? 1f : 0f;
            panelCanvasGroup.interactable = expanded;
            panelCanvasGroup.blocksRaycasts = expanded;
        }

        RefreshOpenButtonVisual();
        if (!expanded) return;

        RebuildList();
        transform.SetAsLastSibling();
    }

    void RefreshOpenButtonVisual()
    {
        var outline = openButton != null ? openButton.GetComponent<Outline>() : null;
        if (outline != null) outline.effectColor = expanded ? DesignTokens.Accent : DesignTokens.Divider;
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
        int visibleCount = 0;
        foreach (var placed in placedObjects)
        {
            if (!MatchesSearch(placed, query)) continue;
            CreateObjectRow(placed);
            visibleCount++;
        }

        if (countText != null)
        {
            countText.text = string.IsNullOrWhiteSpace(query)
                ? placedObjects.Count.ToString()
                : $"{visibleCount}/{placedObjects.Count}";
        }

        if (visibleCount == 0)
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

        string prefix = editState.Hidden ? "[非表示] " : editState.Locked ? "[固定] " : string.Empty;
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

    void PositionDockedElements()
    {
        var root = transform.parent as RectTransform;
        var openRect = openButton != null ? openButton.transform as RectTransform : null;
        if (root == null || panelRect == null || openRect == null) return;

        float left = root.rect.xMin + DesignTokens.CatalogDefaultWidth + 12f;
        float top = root.rect.yMax - 60f;
        var editModeRow = root.Find("EditModeRow") as RectTransform ?? root.Find("EditModeRow_Runtime") as RectTransform;
        if (TryGetVisibleBounds(root, editModeRow, out var modeLeft, out _, out var modeBottom, out _))
        {
            left = modeLeft;
            top = modeBottom - DockGap;
        }

        left = Mathf.Clamp(left, root.rect.xMin + 8f, root.rect.xMax - 88f);
        SetTopLeftRect(openRect, new Vector2(80f, 40f), new Vector2(left - root.rect.xMin, top - root.rect.yMax));

        float panelTop = top - 40f - DockGap;
        float bottomLimit = root.rect.yMin + 12f;
        var scenario = root.Find("Panel_ScenarioGraph") as RectTransform;
        if (TryGetVisibleBounds(root, scenario, out _, out _, out _, out var scenarioTop))
        {
            bottomLimit = Mathf.Max(bottomLimit, scenarioTop + 12f);
        }

        float height = Mathf.Clamp(panelTop - bottomLimit, 180f, PanelMaxHeight);
        float panelLeft = Mathf.Clamp(left, root.rect.xMin + 8f, root.rect.xMax - PanelWidth - 8f);
        SetTopLeftRect(panelRect, new Vector2(PanelWidth, height), new Vector2(panelLeft - root.rect.xMin, panelTop - root.rect.yMax));
    }

    bool TryGetVisibleBounds(
        RectTransform root,
        RectTransform target,
        out float left,
        out float right,
        out float bottom,
        out float top)
    {
        left = right = bottom = top = 0f;
        if (root == null || target == null || !target.gameObject.activeInHierarchy) return false;

        var canvasGroup = target.GetComponent<CanvasGroup>();
        if (canvasGroup != null && canvasGroup.alpha <= 0.01f) return false;

        target.GetWorldCorners(worldCorners);
        Vector3 bottomLeft = root.InverseTransformPoint(worldCorners[0]);
        Vector3 topRight = root.InverseTransformPoint(worldCorners[2]);
        left = bottomLeft.x;
        right = topRight.x;
        bottom = bottomLeft.y;
        top = topRight.y;
        return right > left && top > bottom;
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

    static void SetTopLeftRect(RectTransform rect, Vector2 size, Vector2 position)
    {
        if (rect == null) return;
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
    }

    static ViewportOutliner Build(Transform parent)
    {
        var panel = CreateRect(PanelName, parent);
        var panelImage = panel.gameObject.AddComponent<Image>();
        panelImage.color = DesignTokens.Surface;
        var outline = panel.gameObject.AddComponent<Outline>();
        outline.effectColor = DesignTokens.Divider;
        outline.effectDistance = new Vector2(1f, -1f);
        var canvasGroup = panel.gameObject.AddComponent<CanvasGroup>();
        panel.gameObject.AddComponent<EditorUiInputBlocker>();

        var title = CreateText("Text_Title", panel, "配置済みオブジェクト", DesignTokens.FontSizeSubheading, DesignTokens.TextPrimary);
        SetRect(title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(16f, -48f), new Vector2(-92f, -8f));

        var count = CreateText("Text_Count", panel, "0", DesignTokens.FontSizeCaption, DesignTokens.TextSecondary);
        count.alignment = TextAlignmentOptions.Center;
        SetRect(count.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-88f, -44f), new Vector2(-48f, -12f));

        var close = CreateButton("Button_Close", panel, "×", DesignTokens.BgSecondary);
        SetRect(close.transform as RectTransform, Vector2.one, Vector2.one, new Vector2(-44f, -44f), new Vector2(-8f, -8f));

        var search = CreateSearchInput(panel);
        SetRect(search.transform as RectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(16f, -100f), new Vector2(-16f, -60f));

        var list = CreateScrollList(panel);
        SetRect(list, Vector2.zero, Vector2.one, new Vector2(16f, 16f), new Vector2(-16f, -112f));

        var outliner = panel.gameObject.AddComponent<ViewportOutliner>();
        outliner.panelRect = panel;
        outliner.panelCanvasGroup = canvasGroup;
        outliner.openButton = CreateOpenButton(parent);
        outliner.closeButton = close;
        outliner.searchInput = search;
        outliner.countText = count;
        outliner.listRoot = list.Find("Viewport/Content") as RectTransform;

        UiRoundedTheme.ApplyToHierarchy(panel, DesignTokens.CornerRadius);
        outliner.SetExpanded(false);
        return outliner;
    }

    static Button CreateOpenButton(Transform parent)
    {
        var button = CreateButton(OpenButtonName, parent, "一覧", DesignTokens.Surface);
        var rect = button.transform as RectTransform;
        var outline = button.gameObject.AddComponent<Outline>();
        outline.effectColor = DesignTokens.Divider;
        outline.effectDistance = new Vector2(1f, -1f);
        button.gameObject.AddComponent<EditorUiInputBlocker>();
        UiRoundedTheme.ApplyToHierarchy(rect, DesignTokens.CornerRadius);
        return button;
    }

    static TMP_InputField CreateSearchInput(Transform parent)
    {
        var root = CreateRect("Input_OutlinerSearch", parent);
        var image = root.gameObject.AddComponent<Image>();
        image.color = DesignTokens.BgSecondary;

        var viewport = CreateRect("Text Area", root);
        viewport.gameObject.AddComponent<RectMask2D>();
        SetRect(viewport, Vector2.zero, Vector2.one, new Vector2(12f, 4f), new Vector2(-12f, -4f));

        var placeholder = CreateText("Placeholder", viewport, "名前・IDで検索", DesignTokens.FontSizeBody, DesignTokens.TextTertiary);
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
        SetRect(viewport, Vector2.zero, Vector2.one, new Vector2(4f, 4f), new Vector2(-4f, -4f));

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
        UiRoundedTheme.ApplyToHierarchy(root, DesignTokens.CornerRadius);
        return root;
    }

    static Button CreateListButton(RectTransform parent, string objectName, string labelValue, float width, bool flexible)
    {
        var button = CreateButton(objectName, parent, labelValue, DesignTokens.BgSecondary);
        var element = button.gameObject.AddComponent<LayoutElement>();
        element.minHeight = 36f;
        if (flexible)
        {
            element.minWidth = 80f;
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
