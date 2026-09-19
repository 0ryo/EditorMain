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
    [SerializeField] Button visibilityButton;
    [SerializeField] Button lockButton;
    [SerializeField] Button duplicateButton;
    [SerializeField] Button deleteButton;

    readonly Button[] selectionActionButtons = new Button[6];
    RectTransform panelRect;
    CanvasGroup panelCanvasGroup;
    SelectionService selectionService;
    PlacementController placementController;
    CommandStack commandStack;
    PlacedObject activeObject;
    PlacedObject rangeAnchor;
    readonly System.Collections.Generic.List<PlacedObject> visibleObjects = new();
    readonly System.Collections.Generic.HashSet<PlacedObject> collapsedGroups = new();
    bool displayNameBound;
    bool showingOutliner;
    bool alignmentExpanded;
    Button alignmentToggle;
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
        outliner.EnsureSelectionControls();
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
        EnsureSelectionControls();
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
        int signature = ViewportOutlinerData.CalculateCurrentSignature();
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

        Wire(visibilityButton, ToggleActiveVisibility);
        Wire(lockButton, ToggleActiveLock);
        Wire(duplicateButton, DuplicateActiveObject);
        Wire(deleteButton, DeleteActiveObject);
    }

    static void Wire(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button == null) return;
        button.onClick.RemoveListener(action);
        button.onClick.AddListener(action);
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
        RefreshActionButtons();
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
        activeObject = _;
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
        var listLayout = listRoot.GetComponent<VerticalLayoutGroup>();
        if (listLayout != null) listLayout.spacing = 0f;

        for (int i = listRoot.childCount - 1; i >= 0; i--)
        {
            var child = listRoot.GetChild(i);
            child.gameObject.SetActive(false);
            Destroy(child.gameObject);
        }

        var placedObjects = ViewportOutlinerData.CollectSorted(out int sourceObjectCount);

        if (activeObject != null && !placedObjects.Contains(activeObject)) activeObject = null;
        if (activeObject == null && selectionService != null && placedObjects.Contains(selectionService.Current))
        {
            activeObject = selectionService.Current;
        }

        string query = searchInput != null ? searchInput.text?.Trim() : string.Empty;
        bool searching = !string.IsNullOrWhiteSpace(query);
        collapsedGroups.RemoveWhere(item => item == null);
        int matchCount = 0;
        visibleObjects.Clear();
        foreach (var placed in placedObjects)
        {
            if (!ViewportOutlinerData.MatchesSearch(placed, query)) continue;
            // Searching temporarily reveals matching children without losing the fold state.
            if (!searching && HasCollapsedAncestor(placed)) continue;
            visibleObjects.Add(placed);
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

        lastObjectSignature = ViewportOutlinerData.CalculateSignature(placedObjects, sourceObjectCount);
        RefreshActionButtons();
    }

    bool HasCollapsedAncestor(PlacedObject placed)
    {
        for (var parent = placed.transform.parent; parent != null; parent = parent.parent)
        {
            var group = parent.GetComponent<PlacedObject>();
            if (group != null && collapsedGroups.Contains(group)) return true;
        }
        return false;
    }

    void ToggleGroup(PlacedObject group)
    {
        if (group == null) return;
        if (!collapsedGroups.Add(group)) collapsedGroups.Remove(group);
        RebuildList();
    }

    void CreateObjectRow(PlacedObject placed)
    {
        int depth = ImportedModelParts.Depth(placed);
        int indent = Mathf.Min(depth, 6) * 16;
        int descendantCount = placed.GetComponentsInChildren<PlacedObject>(false).Length - 1;
        bool isGroup = descendantCount > 0;
        var row = CreateRect("Row_" + ViewportOutlinerData.SafeName(placed.Id), listRoot);
        var rowImage = row.gameObject.AddComponent<Image>();
        bool selected = selectionService != null && selectionService.Contains(placed);
        rowImage.color = selected ? DesignTokens.BadgeBg(DesignTokens.Accent) :
            (visibleObjects.Count % 2 == 0 ? DesignTokens.BgSecondary : DesignTokens.Surface);

        var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(8 + indent, 8, 0, 0);
        layout.spacing = 4f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = true;

        var rowElement = row.gameObject.AddComponent<LayoutElement>();
        rowElement.minHeight = rowElement.preferredHeight = 28f;

        var editState = placed.GetComponent<PlacedObjectEditState>();
        if (editState == null) editState = placed.gameObject.AddComponent<PlacedObjectEditState>();

        string prefix = editState.Hidden ? "非表示 · " : editState.Locked ? "固定 · " : "";
        string displayName = placed.GetDisplayName();
        if (string.IsNullOrWhiteSpace(displayName)) displayName = placed.Id;
        if (string.IsNullOrWhiteSpace(displayName)) displayName = placed.name;

        if (isGroup)
        {
            bool searching = searchInput != null && !string.IsNullOrWhiteSpace(searchInput.text);
            var fold = CreateListButton(row, "Button_ToggleChildren",
                !searching && collapsedGroups.Contains(placed) ? "▸" : "▾", 24f, false);
            fold.interactable = !searching;
            fold.onClick.AddListener(() => ToggleGroup(placed));
            ConfigureTreeButton(fold, rowImage.color);
        }
        else
        {
            var spacer = CreateRect("FoldSpacer", row).gameObject.AddComponent<LayoutElement>();
            spacer.minWidth = spacer.preferredWidth = 24f;
        }
        var icon = CreateText("Text_ObjectKind", row, isGroup ? "◇" : "□",
            DesignTokens.FontSizeCaption, DesignTokens.TextSecondary);
        icon.alignment = TextAlignmentOptions.Center;
        var iconLayout = icon.gameObject.AddComponent<LayoutElement>();
        iconLayout.minWidth = iconLayout.preferredWidth = 16f;
        var selectButton = CreateListButton(row, "Button_Select", prefix + displayName, 0f, true);
        ConfigureTreeButton(selectButton, rowImage.color);
        var label = selectButton.GetComponentInChildren<TMP_Text>(true);
        label.richText = false;
        label.fontWeight = isGroup ? FontWeight.SemiBold : FontWeight.Regular;
        label.enableWordWrapping = false;
        if (depth > 0)
        {
            // Fixed-pixel tree rails remain aligned regardless of the font's space width.
            for (int level = 0; level < Mathf.Min(depth, 6); level++)
                CreateTreeLine(row, 12f + level * 16f, 0f, 1f, 1f);
            var branch = CreateRect("TreeBranch", row);
            branch.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            SetRect(branch, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(indent - 4f, 0f), new Vector2(indent + 4f, 1f));
            var branchImage = branch.gameObject.AddComponent<Image>();
            branchImage.color = DesignTokens.Divider;
            branchImage.raycastTarget = false;
        }
        selectButton.onClick.AddListener(() => ActivatePlacedObject(placed, editState));

        // Contiguous tree rows have square corners and no card border.
    }

    static void ConfigureTreeButton(Button button, Color background)
    {
        button.GetComponent<Image>().color = background;
        var element = button.GetComponent<LayoutElement>();
        element.minHeight = element.preferredHeight = 28f;
        var label = button.GetComponentInChildren<TMP_Text>(true);
        SetRect(label.rectTransform, Vector2.zero, Vector2.one, new Vector2(4f, 0f), new Vector2(-4f, 0f));
    }
    static void CreateTreeLine(RectTransform row, float x, float bottom, float top, float width)
    {
        var line = CreateRect("TreeRail", row);
        line.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
        SetRect(line, new Vector2(0f, bottom), new Vector2(0f, top),
            new Vector2(x, 0f), new Vector2(x + width, 0f));
        var image = line.gameObject.AddComponent<Image>();
        image.color = DesignTokens.Divider;
        image.raycastTarget = false;
    }

    void ActivatePlacedObject(PlacedObject placed, PlacedObjectEditState editState)
    {
        if (placed == null || editState == null) return;

        if (SelectionService.CanEdit(placed))
        {
            int anchorIndex = visibleObjects.IndexOf(rangeAnchor);
            int targetIndex = visibleObjects.IndexOf(placed);
            if (EditInput.ShiftPressed() && anchorIndex >= 0 && targetIndex >= 0 && selectionService != null)
            {
                int first = Mathf.Min(anchorIndex, targetIndex);
                int count = Mathf.Abs(anchorIndex - targetIndex) + 1;
                bool additive = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl) ||
                    Input.GetKey(KeyCode.LeftCommand) || Input.GetKey(KeyCode.RightCommand);
                selectionService.SelectMany(visibleObjects.GetRange(first, count), additive);
            }
            else
            {
                selectionService?.Select(placed, SelectionService.AdditiveSelection);
                rangeAnchor = placed;
            }
            activeObject = selectionService != null ? selectionService.Current : placed;
        }
        else
        {
            if (selectionService != null && selectionService.Current != null) selectionService.Select(null);
            activeObject = placed;
        }

        RebuildList();
    }

    void RefreshActionButtons()
    {
        int selectedCount = selectionService != null ? selectionService.Selected.Count : 0;
        for (int i = 0; i < selectionActionButtons.Length; i++)
            if (selectionActionButtons[i] != null) selectionActionButtons[i].interactable = selectedCount >= (i < 3 ? 2 : 3);
        if (duplicateButton != null) SetButtonLabel(duplicateButton, selectedCount > 1 ? $"複製({selectedCount})" : "複製");
        if (deleteButton != null) SetButtonLabel(deleteButton, selectedCount > 1 ? $"削除({selectedCount})" : "削除");
        var target = activeObject;
        var editState = target != null ? target.GetComponent<PlacedObjectEditState>() : null;
        bool hasTarget = target != null && editState != null;

        if (visibilityButton != null)
        {
            visibilityButton.interactable = hasTarget;
            SetButtonLabel(visibilityButton, hasTarget && editState.Hidden ? "表示" : "隠す");
        }
        if (lockButton != null)
        {
            lockButton.interactable = hasTarget;
            SetButtonLabel(lockButton, hasTarget && editState.Locked ? "解除" : "固定");
        }
        if (duplicateButton != null) duplicateButton.interactable = hasTarget;
        if (deleteButton != null) deleteButton.interactable = hasTarget;
    }

    void ToggleActiveVisibility()
    {
        var placed = activeObject;
        var editState = placed != null ? placed.GetComponent<PlacedObjectEditState>() : null;
        if (placed == null || editState == null) return;

        bool willHide = !editState.Hidden;
        if (willHide && selectionService != null && selectionService.Current == placed)
        {
            selectionService.Select(null);
        }
        editState.SetVisible(!willHide);
        activeObject = placed;
        RebuildList();
    }

    void ToggleActiveLock()
    {
        var placed = activeObject;
        var editState = placed != null ? placed.GetComponent<PlacedObjectEditState>() : null;
        if (placed == null || editState == null) return;

        bool willLock = !editState.Locked;
        if (willLock && selectionService != null && selectionService.Current == placed)
        {
            selectionService.Select(null);
        }
        editState.SetLocked(willLock);
        activeObject = placed;
        RebuildList();
    }

    void DuplicateActiveObject()
    {
        if (selectionService != null && selectionService.Contains(activeObject)) { selectionService.DuplicateSelected(); RebuildList(); return; }
        if (activeObject == null) return;

        var command = new DuplicateObjectCommand(activeObject.gameObject, new Vector3(0.2f, 0f, 0.2f));
        bool succeeded = CommandService.I != null && CommandService.I.Stack != null
            ? CommandService.I.Stack.Execute(command)
            : command.Do();
        if (!succeeded || command.Result == null) return;

        activeObject = command.Result;
        selectionService?.Select(activeObject);
        RebuildList();
    }

    void DeleteActiveObject()
    {
        if (selectionService != null && selectionService.Contains(activeObject)) { selectionService.DeleteSelected(); activeObject = null; RebuildList(); return; }
        if (activeObject == null) return;

        var target = activeObject;
        var command = new DeleteObjectCommand(
            target.gameObject,
            target.TypeId,
            typeId => PlacedObjectRestoreFactory.Create(typeId, null, placementController));
        bool succeeded = CommandService.I != null && CommandService.I.Stack != null
            ? CommandService.I.Stack.Execute(command)
            : command.Do();
        if (!succeeded) return;

        if (selectionService != null && selectionService.Current == target) selectionService.Select(null);
        activeObject = null;
        RebuildList();
    }

    public void EnsureSelectionControls()
    {
        var hint = transform.Find("Text_SelectionHelp");
        if (hint != null)
        {
            hint.gameObject.SetActive(false);
            if (Application.isPlaying) Destroy(hint.gameObject);
            else DestroyImmediate(hint.gameObject);
        }
        alignmentToggle = transform.Find("Button_AlignmentToggle")?.GetComponent<Button>();
        if (alignmentToggle == null)
            alignmentToggle = CreateButton("Button_AlignmentToggle", transform, "> 整列メニュー", DesignTokens.BgSecondary);
        SetRect(alignmentToggle.transform as RectTransform, new Vector2(0f, 1f), Vector2.one,
            new Vector2(0f, -178f), new Vector2(0f, -138f));
        var toggleLabel = alignmentToggle.GetComponentInChildren<TMP_Text>(true);
        toggleLabel.alignment = TextAlignmentOptions.MidlineLeft;
        SetRect(toggleLabel.rectTransform, Vector2.zero, Vector2.one, new Vector2(16f, 4f), new Vector2(-16f, -4f));
        alignmentToggle.onClick.RemoveListener(ToggleAlignment);
        alignmentToggle.onClick.AddListener(ToggleAlignment);
        for (int mode = 0; mode < 2; mode++)
        {
            string name = mode == 0 ? "Actions_Align" : "Actions_Distribute";
            var row = transform.Find(name) as RectTransform;
            if (row == null)
            {
                row = CreateRect(name, transform);
                var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
                layout.spacing = 4f;
                layout.childControlWidth = layout.childControlHeight = true;
                layout.childForceExpandWidth = layout.childForceExpandHeight = true;
            }
            SetRect(row, new Vector2(0f, 1f), Vector2.one, new Vector2(0f, -226f - mode * 48f), new Vector2(0f, -186f - mode * 48f));
            for (int axis = 0; axis < 3; axis++)
            {
                string buttonName = "Button_" + axis;
                var button = row.Find(buttonName)?.GetComponent<Button>();
                if (button == null) button = CreateActionButton(row, buttonName, "XYZ"[axis] + (mode == 0 ? "整列" : "等間隔"));
                selectionActionButtons[mode * 3 + axis] = button;
                int selectedAxis = axis;
                bool distribute = mode == 1;
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => selectionService?.Align(selectedAxis, distribute));
            }
        }
        ApplyAlignmentLayout();
        UiRoundedTheme.ApplyToHierarchy(alignmentToggle.transform, DesignTokens.CornerRadius);
    }

    void ToggleAlignment()
    {
        alignmentExpanded = !alignmentExpanded;
        ApplyAlignmentLayout();
    }

    void ApplyAlignmentLayout()
    {
        SetButtonLabel(alignmentToggle, alignmentExpanded ? "v 整列メニュー" : "> 整列メニュー");
        var align = transform.Find("Actions_Align");
        var distribute = transform.Find("Actions_Distribute");
        if (align != null) align.gameObject.SetActive(alignmentExpanded);
        if (distribute != null) distribute.gameObject.SetActive(alignmentExpanded);
        var scroll = transform.Find("Scroll_Outliner") as RectTransform;
        if (scroll != null) SetRect(scroll, Vector2.zero, Vector2.one, Vector2.zero,
            new Vector2(0f, alignmentExpanded ? -282f : -186f));
    }

    public static void PreparePrefab(Transform uiRoot)
    {
        var catalog = uiRoot.Find("Panel_Catalog") as RectTransform;
        if (catalog == null) return;
        var panel = catalog.Find(PanelName)?.GetComponent<ViewportOutliner>();
        if (panel == null) panel = Build(catalog);
        panel.EnsureSelectionControls();
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

        var actionBar = CreateRect("Actions_Outliner", panel);
        SetRect(actionBar, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -130f), new Vector2(0f, -90f));
        var actionLayout = actionBar.gameObject.AddComponent<HorizontalLayoutGroup>();
        actionLayout.spacing = 4f;
        actionLayout.childControlWidth = true;
        actionLayout.childControlHeight = true;
        actionLayout.childForceExpandWidth = true;
        actionLayout.childForceExpandHeight = true;

        var visibility = CreateActionButton(actionBar, "Button_Visibility", "隠す");
        var lockButton = CreateActionButton(actionBar, "Button_Lock", "固定");
        var duplicate = CreateActionButton(actionBar, "Button_Duplicate", "複製");
        var delete = CreateActionButton(actionBar, "Button_DeleteObject", "削除");
        var deleteImage = delete.GetComponent<Image>();
        if (deleteImage != null) deleteImage.color = DesignTokens.BadgeBg(DesignTokens.Error);

        var list = CreateScrollList(panel);
        SetRect(list, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(0f, -140f));

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
        outliner.visibilityButton = visibility;
        outliner.lockButton = lockButton;
        outliner.duplicateButton = duplicate;
        outliner.deleteButton = delete;

        outliner.EnsureSelectionControls();
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
        layout.spacing = 0f;
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

    static Button CreateActionButton(RectTransform parent, string objectName, string labelValue)
    {
        var button = CreateButton(objectName, parent, labelValue, DesignTokens.BgSecondary);
        var element = button.gameObject.AddComponent<LayoutElement>();
        element.minWidth = 40f;
        element.flexibleWidth = 1f;
        element.minHeight = 36f;
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

    static void SetButtonLabel(Button button, string value)
    {
        var label = button != null ? button.GetComponentInChildren<TMP_Text>(true) : null;
        if (label != null) label.text = value;
    }

    static void SetRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
    }
}
