using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using Unity.Profiling;
using UnityEngine.UI;

public sealed class EditorProjectPanel : MonoBehaviour
{
    const string ControllerName = "EditorProjectPanelController";
    const string ModalName = "Panel_ProjectFiles";
    const float RecoveryWarningHeight = 148f;
    const string DialogTitle = "教材を開く";
    const string DefaultHint = "教材をクリックして選び、ダブルクリックか「開く」で開きます。";
    static readonly ProfilerMarker RefreshProjectListMarker = new("EditorProjectPanel.RefreshProjectList");
    static readonly HashSet<EditorProjectPanel> Panels = new();

    public static bool HasOpenModal
    {
        get
        {
            return GetOpenModalRoot() != null;
        }
    }

    public static Transform GetOpenModalRoot(Transform within = null)
    {
        foreach (var panel in Panels)
        {
            if (panel == null || panel.modal == null || !panel.modal.gameObject.activeInHierarchy) continue;
            if (within == null || panel.modal == within || panel.modal.IsChildOf(within)) return panel.modal;
        }
        return null;
    }

    Transform uiRoot;
    EditorProjectService projectService;
    Button openButton;
    RectTransform modal;
    TMP_Text dialogTitleText;
    TMP_InputField searchInput;
    TMP_Text statusText;
    RectTransform listContent;
    int libraryMode;
    readonly Button[] libraryTabs = new Button[3];
    string selectedPath;
    string lastClickedPath;
    float lastClickTime;
    bool recoveryWarningShown;
    bool allowQuit;

    public static EditorProjectPanel Ensure(Transform parent)
    {
        if (parent == null) return null;

        var found = parent.Find(ControllerName);
        var panel = found != null ? found.GetComponent<EditorProjectPanel>() : null;
        if (panel != null) return panel;

        var go = new GameObject(ControllerName, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        panel = go.AddComponent<EditorProjectPanel>();
        panel.uiRoot = parent;
        panel.Build();
        return panel;
    }

    void Awake()
    {
        Panels.Add(this);
        if (uiRoot == null) uiRoot = transform.parent;
    }

    void Start() => ShowRecoveryProtectionWarning();

    void OnDestroy()
    {
        Panels.Remove(this);
        if (projectService != null)
        {
            projectService.StatusChanged -= OnServiceStatusChanged;
            projectService.DirtyChanged -= OnDirtyChanged;
            projectService.RecoveryChanged -= OnRecoveryChanged;
            projectService.RecoveryProtectionChanged -= OnRecoveryProtectionChanged;
        }
#if !UNITY_EDITOR
        Application.wantsToQuit -= HandleWantsToQuit;
#endif
    }

    void Update()
    {
        if (modal == null || !modal.gameObject.activeInHierarchy ||
            !Application.isFocused || SkillSyncConfirmDialog.IsOpen || !EditInput.CancelPressedThisFrame()) return;

        var selected = UnityEngine.EventSystems.EventSystem.current?.currentSelectedGameObject;
        var input = selected != null ? selected.GetComponent<TMP_InputField>() : null;
        if (input != null && input.isFocused) return;

        Close();
    }

    void Build()
    {
        if (uiRoot == null) return;
        projectService = EditorProjectService.Ensure(uiRoot);
        projectService.StatusChanged -= OnServiceStatusChanged;
        projectService.StatusChanged += OnServiceStatusChanged;
        projectService.DirtyChanged -= OnDirtyChanged;
        projectService.DirtyChanged += OnDirtyChanged;
        projectService.RecoveryChanged -= OnRecoveryChanged;
        projectService.RecoveryChanged += OnRecoveryChanged;
        projectService.RecoveryProtectionChanged -= OnRecoveryProtectionChanged;
        projectService.RecoveryProtectionChanged += OnRecoveryProtectionChanged;

        BuildOpenButton();
        BuildModal();
        UiAccessibilityMetrics.EnsureButtonTargets(uiRoot);
#if !UNITY_EDITOR
        Application.wantsToQuit -= HandleWantsToQuit;
        Application.wantsToQuit += HandleWantsToQuit;
#endif
    }

    void BuildOpenButton()
    {
        var topBar = uiRoot.Find("Panel_ScenarioGraph/TopBar") as RectTransform;
        if (topBar == null)
        {
            Debug.LogWarning("[EditorProjectPanel] Scenario top bar was not found.");
            return;
        }

        var existing = topBar.Find("Button_ProjectFiles");
        openButton = existing != null ? existing.GetComponent<Button>() : null;
        if (openButton == null)
        {
            openButton = CreateButton("Button_ProjectFiles", topBar, DialogTitle, 112f);
            var saveButton = topBar.Find("Button_SaveCurriculum");
            if (saveButton != null) openButton.transform.SetSiblingIndex(saveButton.GetSiblingIndex());
        }

        openButton.onClick.RemoveListener(Open);
        openButton.onClick.AddListener(Open);
        UiRoundedTheme.ApplyToHierarchy(openButton.transform, DesignTokens.CornerRadius);
    }

    void BuildModal()
    {
        var existing = uiRoot.Find(ModalName) as RectTransform;
        modal = existing;
        if (modal == null)
        {
            modal = CreateRect(ModalName, uiRoot);
            Stretch(modal);
        }

        var dimmer = modal.GetComponent<Image>();
        if (dimmer == null) dimmer = modal.gameObject.AddComponent<Image>();
        dimmer.raycastTarget = true;
        if (modal.GetComponent<EditorUiInputBlocker>() == null)
            modal.gameObject.AddComponent<EditorUiInputBlocker>();
        if (existing == null) dimmer.color = new Color(0f, 0f, 0f, 0.35f);

        var dialog = CreateRect("Dialog", modal);
        dialog.anchorMin = dialog.anchorMax = new Vector2(0.5f, 0.5f);
        dialog.pivot = new Vector2(0.5f, 0.5f);
        dialog.sizeDelta = new Vector2(720f, 620f);
        dialog.localScale = Vector3.one * SkillSyncDesignLayout.Scale;
        var dialogImage = dialog.gameObject.AddComponent<Image>();
        dialogImage.color = DesignTokens.Surface;

        // "教材を開く": search, three tabs, the list and a quiet footer. Saving and creating live in the header and the start screen.
        dialogTitleText = CreateText("Title", dialog, DialogTitle, DesignTokens.FontSizeHeading, DesignTokens.TextPrimary);
        SetRect(dialogTitleText.rectTransform, new Vector2(24f, -20f), new Vector2(360f, 36f));

        var close = CreateButton("Button_Close", dialog, "閉じる", 80f);
        SetTopRight(close.transform as RectTransform, new Vector2(-20f, -18f), new Vector2(80f, 36f));
        close.onClick.AddListener(Close);

        searchInput = CreateInput("Input_Search", dialog, "教材の名前で探す");
        SetRect(searchInput.transform as RectTransform, new Vector2(24f, -68f), new Vector2(672f, 40f));
        searchInput.onValueChanged.AddListener(_ => RefreshProjectList());

        string[] tabNames = { "教材", "テンプレート", "ゴミ箱" };
        for (int i = 0; i < tabNames.Length; i++)
        {
            int mode = i;
            libraryTabs[i] = CreateButton("Button_Library" + i, dialog, tabNames[i], 216f);
            SetRect(libraryTabs[i].transform as RectTransform, new Vector2(24f + i * 228f, -120f), new Vector2(216f, 36f));
            libraryTabs[i].onClick.AddListener(() => { libraryMode = mode; selectedPath = null; RefreshProjectList(); });
        }

        BuildProjectList(dialog);

        var reveal = CreateButton("Button_RevealFolder", dialog, RevealFolderLabel, 244f);
        SetRect(reveal.transform as RectTransform, new Vector2(24f, -566f), new Vector2(244f, 40f));
        reveal.onClick.AddListener(RevealStorageFolder);
        statusText = CreateText("Text_Status", dialog, string.Empty, DesignTokens.FontSizeCaption, DesignTokens.TextSecondary);
        SetRect(statusText.rectTransform, new Vector2(284f, -566f), new Vector2(412f, 40f));
        statusText.textWrappingMode = TextWrappingModes.Normal;

        UiRoundedTheme.ApplyToHierarchy(dialog, DesignTokens.CornerRadius);
        UiAccessibilityMetrics.EnsureButtonTargets(dialog);
        RefreshDirtyVisual();
        modal.gameObject.SetActive(false);
    }

    void BuildProjectList(RectTransform dialog)
    {
        var scrollRoot = CreateRect("Scroll_Projects", dialog);
        SetRect(scrollRoot, new Vector2(24f, -164f), new Vector2(672f, 390f));
        var scrollImage = scrollRoot.gameObject.AddComponent<Image>();
        scrollImage.color = DesignTokens.BgPrimary;
        var scroll = scrollRoot.gameObject.AddComponent<ScrollRect>();
        scroll.horizontal = false;

        var viewport = CreateRect("Viewport", scrollRoot);
        Stretch(viewport);
        var viewportImage = viewport.gameObject.AddComponent<Image>();
        viewportImage.color = Color.clear;
        viewportImage.raycastTarget = true;
        viewport.gameObject.AddComponent<RectMask2D>();

        listContent = CreateRect("Content", viewport);
        listContent.anchorMin = new Vector2(0f, 1f);
        listContent.anchorMax = new Vector2(1f, 1f);
        listContent.pivot = new Vector2(0.5f, 1f);
        listContent.offsetMin = new Vector2(8f, 0f);
        listContent.offsetMax = new Vector2(-8f, 0f);
        scroll.viewport = viewport;
        scroll.content = listContent;
        scroll.vertical = true;
        scroll.scrollSensitivity = 24f;

        var barRect = CreateRect("Scrollbar_Vertical", scrollRoot);
        barRect.anchorMin = new Vector2(1f, 0f);
        barRect.anchorMax = new Vector2(1f, 1f);
        barRect.pivot = new Vector2(1f, 0.5f);
        barRect.offsetMin = new Vector2(-10f, 2f);
        barRect.offsetMax = new Vector2(-2f, -2f);
        var barImage = barRect.gameObject.AddComponent<Image>();
        barImage.color = DesignTokens.BgSecondary;
        var slidingArea = CreateRect("Sliding Area", barRect);
        Stretch(slidingArea);
        var handle = CreateRect("Handle", slidingArea);
        Stretch(handle);
        var handleImage = handle.gameObject.AddComponent<Image>();
        handleImage.color = DesignTokens.TextTertiary;
        var scrollbar = barRect.gameObject.AddComponent<Scrollbar>();
        scrollbar.direction = Scrollbar.Direction.BottomToTop;
        scrollbar.handleRect = handle;
        scrollbar.targetGraphic = handleImage;
        scroll.verticalScrollbar = scrollbar;
        scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;
        scroll.verticalScrollbarSpacing = 2f;
    }

    static string RevealFolderLabel => ProjectFileLocation.RevealLabel == "フォルダを開く"
        ? "保存場所のフォルダを開く" : "保存場所を" + ProjectFileLocation.RevealLabel;

    void Open()
    {
        if (modal == null) BuildModal();
        if (modal == null) return;
        projectService.RefreshRecoveryProtection();

        selectedPath = null;
        lastClickedPath = null;
        searchInput.SetTextWithoutNotify(string.Empty);
        SetStatus(string.Empty, true);
        RefreshDirtyVisual();
        RefreshProjectList();
        modal.gameObject.SetActive(true);
        modal.SetAsLastSibling();
    }

    public void OpenForDesignUi() => OpenLibraryTab(0);

    // tab: 0 lessons, 1 templates, 2 trash. Always starts on the requested tab, not on a previous one.
    public void OpenLibraryTab(int tab)
    {
        gameObject.SetActive(true);
        if (projectService == null) Build();
        libraryMode = Mathf.Clamp(tab, 0, 2);
        Open();
    }

    public void ShowRecoveryProtectionWarning()
    {
        if (projectService == null || modal == null || recoveryWarningShown ||
            string.IsNullOrEmpty(projectService.RecoveryProtectionWarning)) return;
        recoveryWarningShown = true;
        OpenForDesignUi();
    }

    void Close()
    {
        if (modal != null) modal.gameObject.SetActive(false);
    }

    void SetStatus(string message, bool succeeded)
    {
        if (statusText == null) return;
        bool empty = string.IsNullOrEmpty(message);
        statusText.text = empty ? DefaultHint : message;
        statusText.color = empty ? DesignTokens.TextTertiary : succeeded ? DesignTokens.Success : DesignTokens.Error;
    }

    // Shows the selected lesson in the file manager, or else the newest lesson, so the folder stays findable without internal paths in the UI.
    void RevealStorageFolder()
    {
        string path = selectedPath;
        if (string.IsNullOrEmpty(path))
        {
            var recent = EditorProjectStore.ListRecentlyWritten(1);
            path = recent.Count > 0 ? recent[0].Path : EditorProjectStore.ProjectsDirectory;
        }
        ProjectFileLocation.Reveal(path);
    }

    // Developer-only placement data export. No longer offered in the lesson dialog; kept callable for a future developer entry point.
    public bool ExportPlacementData(out string path, out string error)
    {
        var exportService = FindFirstObjectByType<PlacementExportService>();
        if (exportService == null) exportService = projectService.gameObject.AddComponent<PlacementExportService>();
        exportService.projectName = projectService.CurrentProjectName;
        return exportService.TryExportPlacementJson(out path, out error);
    }

    // Ctrl/Cmd+N and the header 新規 button. Asks only when there are unsaved changes.
    public void RequestNewProject()
    {
        if (!projectService.IsDirty)
        {
            NewProjectNow();
            return;
        }

        Ask("現在の教材を保存しますか？",
            $"「{projectService.CurrentProjectName}」に未保存の変更があります。新しい教材を作る前に保存しますか？",
            "保存して新規作成",
            () => EditorProjectSaveFlow.Run(projectService, saved => { if (saved) NewProjectNow(); }),
            secondaryLabel: "保存せず新規作成", onSecondary: NewProjectNow);
    }

    void NewProjectNow()
    {
        projectService.NewProject(null, out var message);
        SetStatus(message, true);
        Close();
        TopCenterNotification.Ensure(uiRoot, null)?.Show(message, false, 3f);
    }

    // Opens a saved lesson, asking about unsaved changes first. done(true) once the lesson is open.
    public void RequestOpen(EditorProjectFileInfo info, Action<bool> done = null)
    {
        if (projectService.IsDirty)
        {
            Ask("現在の教材を保存しますか？",
                $"「{projectService.CurrentProjectName}」に未保存の変更があります。「{info.DisplayName}」を開く前に保存しますか？",
                "保存して開く",
                () => EditorProjectSaveFlow.Run(projectService, saved => { if (saved) OpenNow(info, done); else done?.Invoke(false); }),
                secondaryLabel: "保存せず開く", onSecondary: () => OpenNow(info, done));
            return;
        }

        OpenNow(info, done);
    }

    // Opens a template (or bundled sample) as a new, unsaved lesson; the source stays untouched. done(true) once it is open.
    public void RequestCreateFrom(string sourcePath, string baseName, Action<bool> done = null)
    {
        if (projectService.IsDirty)
        {
            Ask("現在の教材を保存しますか？",
                $"「{projectService.CurrentProjectName}」に未保存の変更があります。「{baseName}」から新しい教材を作る前に保存しますか？",
                "保存して作成",
                () => EditorProjectSaveFlow.Run(projectService, saved => { if (saved) CreateNow(sourcePath, baseName, done); else done?.Invoke(false); }),
                secondaryLabel: "保存せず作成", onSecondary: () => CreateNow(sourcePath, baseName, done));
            return;
        }

        CreateNow(sourcePath, baseName, done);
    }

    void CreateNow(string sourcePath, string baseName, Action<bool> done)
    {
        bool created = projectService.OpenCopy(sourcePath, baseName, out var message);
        if (created)
        {
            Close();
            TopCenterNotification.Ensure(uiRoot, null)?.Show(message, false, 5f);
        }
        done?.Invoke(created);
    }

    public static string TemplateBaseName(string displayName)
    {
        const string suffix = " テンプレート";
        return displayName.EndsWith(suffix, StringComparison.Ordinal) && displayName.Length > suffix.Length
            ? displayName.Substring(0, displayName.Length - suffix.Length) : displayName;
    }

    void OpenNow(EditorProjectFileInfo info, Action<bool> done)
    {
        bool loaded = projectService.Load(info.Path, out var message);
        if (loaded)
        {
            RefreshProjectList();
            RefreshDirtyVisual();
            TopCenterNotification.Ensure(uiRoot, null)?.Show(message, false, 3f);
        }
        done?.Invoke(loaded);
    }

    // The previous session ended with unsaved changes; its protected copy is offered here and on the start screen.
    public void RequestOpenStartupRecovery(Action<bool> done = null)
    {
        var notice = projectService.StartupRecovery;
        if (notice == null || !EditorProjectStore.TryGetFileInfo(notice.path, out var info))
        {
            projectService.DismissStartupRecovery();
            RefreshProjectList();
            done?.Invoke(false);
            return;
        }
        RequestOpen(info, opened =>
        {
            if (opened) projectService.DismissStartupRecovery();
            RefreshProjectList();
            done?.Invoke(opened);
        });
    }

    public void RequestDiscardStartupRecovery(Action done = null)
    {
        var notice = projectService.StartupRecovery;
        if (notice == null) return;
        string name = string.IsNullOrWhiteSpace(notice.originalName) ? EditorProjectStore.UntitledProjectName : notice.originalName;
        Ask("復旧データを破棄しますか？",
            $"「{name}」の、保存されなかった変更を破棄します。復旧データはゴミ箱へ移すので、必要なら「ゴミ箱」タブから戻せます。",
            "破棄", () =>
        {
            projectService.DiscardPreservedRecovery(notice.path, out var message);
            TopCenterNotification.Ensure(uiRoot, null)?.Show(message, false, 4f);
            RefreshProjectList();
            done?.Invoke();
        }, destructive: true);
    }

    // Recovery data that could not be set aside at startup (see RecoveryProtectionWarning): open or discard the shared slot itself.
    void RequestRecoveryLoad(EditorProjectFileInfo info)
    {
        Ask("復旧データを開きますか？",
            $"今の編集内容を閉じて、「{info.DisplayName}」の復旧データを開きます。保存していない変更は失われます。",
            "復旧して開く", () =>
        {
            bool loaded = projectService.LoadRecovery(out var message);
            SetStatus(message, loaded);
            if (loaded)
            {
                RefreshDirtyVisual();
                Close();
                TopCenterNotification.Ensure(uiRoot, null)?.Show(message, false, 4f);
            }
        }, destructive: true);
    }

    void RequestRecoveryDelete(EditorProjectFileInfo info)
    {
        Ask("復旧データを破棄しますか？",
            $"「{info.DisplayName}」の復旧データを破棄します。この操作は元に戻せません。",
            "破棄", () =>
        {
            projectService.DeleteRecovery(out var message);
            SetStatus(message, true);
        }, destructive: true);
    }

    void RefreshProjectList()
    {
        if (listContent == null) return;
        using (RefreshProjectListMarker.Auto())
        {
            RefreshProjectListContents();
        }
    }

    void RefreshProjectListContents()
    {
        for (int i = listContent.childCount - 1; i >= 0; i--)
        {
            Destroy(listContent.GetChild(i).gameObject);
        }

        for (int i = 0; i < libraryTabs.Length; i++)
            if (libraryTabs[i] != null) libraryTabs[i].interactable = i != libraryMode;
        var all = libraryMode == 1 ? EditorProjectStore.ListTemplates() :
            libraryMode == 2 ? EditorProjectStore.ListArchived() : EditorProjectStore.ListProjects();
        string query = searchInput != null ? searchInput.text.Trim() : string.Empty;
        var projects = new List<EditorProjectFileInfo>();
        foreach (var info in all)
            if (query.Length == 0 || info.DisplayName.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0) projects.Add(info);

        float cursor = 0f;
        if (!string.IsNullOrEmpty(projectService.RecoveryProtectionWarning))
        {
            CreateRecoveryProtectionWarning(ref cursor);
            if (libraryMode == 0 && EditorProjectStore.TryGetRecovery(out var slot))
                CreateRecoveryBand(ref cursor, "前回保存されなかった教材「" + slot.DisplayName + "」の復旧データがあります",
                    slot.LastWriteTimeUtc.ToLocalTime().ToString("M月d日 HH:mm") + "までの内容です。",
                    () => RequestRecoveryLoad(slot), () => RequestRecoveryDelete(slot));
        }
        var notice = projectService.StartupRecovery;
        if (libraryMode == 0 && notice != null && EditorProjectStore.TryGetFileInfo(notice.path, out _))
        {
            string name = string.IsNullOrWhiteSpace(notice.originalName) ? EditorProjectStore.UntitledProjectName : notice.originalName;
            CreateRecoveryBand(ref cursor, "前回保存されずに終了した教材「" + name + "」の復旧データがあります",
                notice.savedAtUtc.ToLocalTime().ToString("M月d日 HH:mm") + "までの内容です。",
                () => RequestOpenStartupRecovery(opened => { if (opened) Close(); }), () => RequestDiscardStartupRecovery());
        }

        if (projects.Count == 0)
        {
            string emptyText = query.Length > 0 ? "「" + query + "」に合う教材はありません。"
                : libraryMode == 1 ? "テンプレートはまだありません。「教材」タブで教材を選び、「テンプレートにする」を押すと追加できます。"
                : libraryMode == 2 ? "ゴミ箱は空です。"
                : "まだ保存した教材はありません。";
            var empty = CreateText("Text_Empty", listContent, emptyText, DesignTokens.FontSizeBody, DesignTokens.TextSecondary);
            empty.alignment = TextAlignmentOptions.Center;
            empty.textWrappingMode = TextWrappingModes.Normal;
            PlaceItem(empty.rectTransform, cursor, 64f);
            SetListContentHeight(cursor + 72f);
            return;
        }

        foreach (var info in projects) CreateLibraryRow(info, ref cursor);
        SetListContentHeight(cursor + 8f);
    }

    void CreateLibraryRow(EditorProjectFileInfo info, ref float cursor)
    {
        bool selected = string.Equals(info.Path, selectedPath, StringComparison.OrdinalIgnoreCase);
        float height = selected ? 112f : 60f;
        var row = CreateRect("Project_" + info.DisplayName, listContent);
        PlaceItem(row, cursor, height);
        cursor += height + DesignTokens.SpaceSm;
        var image = row.gameObject.AddComponent<Image>();
        image.color = selected ? DesignTokens.BadgeBg(DesignTokens.Accent) : DesignTokens.Surface;
        var rowButton = row.gameObject.AddComponent<Button>();
        rowButton.targetGraphic = image;
        rowButton.onClick.AddListener(() => OnRowClicked(info));

        var label = CreateText("Label", row, info.DisplayName, DesignTokens.FontSizeBody, DesignTokens.TextPrimary);
        SetRect(label.rectTransform, new Vector2(12f, -16f), new Vector2(430f, 28f));
        string dateLabel = (info.LastSaveWasAutomatic ? "復旧データ  " : "") + info.LastWriteTimeUtc.ToLocalTime().ToString("yyyy/MM/dd HH:mm");
        var date = CreateText("Date", row, dateLabel, DesignTokens.FontSizeCaption, DesignTokens.TextSecondary);
        SetTopRight(date.rectTransform, new Vector2(-12f, -16f), new Vector2(220f, 28f));
        date.alignment = TextAlignmentOptions.MidlineRight;
        if (selected) AddSelectedActions(row, info);
        UiRoundedTheme.ApplyToHierarchy(row, DesignTokens.CornerRadius);
        UiAccessibilityMetrics.EnsureButtonTargets(row);
    }

    // First click selects; a second click on the same row within 0.4s opens it (templates: creates a lesson from it).
    void OnRowClicked(EditorProjectFileInfo info)
    {
        bool doubleClick = string.Equals(lastClickedPath, info.Path, StringComparison.OrdinalIgnoreCase) &&
            Time.unscaledTime - lastClickTime < 0.4f;
        lastClickedPath = info.Path;
        lastClickTime = Time.unscaledTime;
        if (doubleClick && libraryMode != 2) { RunPrimaryAction(info); return; }
        if (string.Equals(selectedPath, info.Path, StringComparison.OrdinalIgnoreCase)) return;
        selectedPath = info.Path;
        RefreshProjectList();
    }

    void RunPrimaryAction(EditorProjectFileInfo info)
    {
        if (libraryMode == 0) RequestOpen(info, opened => { if (opened) Close(); });
        else if (libraryMode == 1) RequestCreateFrom(info.Path, TemplateBaseName(info.DisplayName));
        else RestoreFromTrash(info);
    }

    void AddSelectedActions(RectTransform row, EditorProjectFileInfo info)
    {
        float x = 12f;
        Button Add(string label, float width, UnityEngine.Events.UnityAction action, bool primary = false)
        {
            var button = CreateButton("Button_Action" + label, row, label, width, primary);
            SetRect(button.transform as RectTransform, new Vector2(x, -60f), new Vector2(width, 40f));
            button.onClick.AddListener(action);
            x += width + 10f;
            return button;
        }

        if (libraryMode == 2)
        {
            Add("元に戻す", 140f, () => RestoreFromTrash(info), true);
            return;
        }
        if (libraryMode == 0)
        {
            Add("開く", 100f, () => RunPrimaryAction(info), true);
            Add("複製", 100f, () => DuplicateSaved(info, false));
            Add("テンプレートにする", 180f, () => DuplicateSaved(info, true));
        }
        else Add("この教材から作成", 180f, () => RunPrimaryAction(info), true);

        var trash = Add("ゴミ箱へ移す", 140f, () => Ask(
            libraryMode == 1 ? "テンプレートをゴミ箱へ移しますか？" : "教材をゴミ箱へ移しますか？",
            $"「{info.DisplayName}」をゴミ箱へ移します。「ゴミ箱」タブから元に戻せます。",
            "ゴミ箱へ移す", () =>
            {
                projectService.ArchiveSaved(info.Path, out _);
                selectedPath = null;
                RefreshProjectList();
            }));
        // The open lesson cannot be moved away from under the editor.
        if (string.Equals(info.Path, projectService.CurrentProjectPath, StringComparison.OrdinalIgnoreCase))
            trash.interactable = false;
    }

    void RestoreFromTrash(EditorProjectFileInfo info)
    {
        projectService.RestoreSaved(info.Path, out _);
        selectedPath = null;
        RefreshProjectList();
    }

    void DuplicateSaved(EditorProjectFileInfo info, bool asTemplate)
    {
        if (!projectService.DuplicateSaved(info.Path, asTemplate, out _)) return;
        libraryMode = asTemplate ? 1 : 0;
        selectedPath = null;
        RefreshProjectList();
    }

    void CreateRecoveryProtectionWarning(ref float cursor)
    {
        var row = CreateRect("Project_RecoveryProtectionWarning", listContent);
        PlaceItem(row, cursor, RecoveryWarningHeight);
        cursor += RecoveryWarningHeight + DesignTokens.SpaceSm;
        row.gameObject.AddComponent<UnityEngine.UI.Image>().color = DesignTokens.BadgeBg(DesignTokens.Warning);
        var warning = CreateText("Text_Warning", row, projectService.RecoveryProtectionWarning,
            DesignTokens.FontSizeCaption, DesignTokens.TextPrimary);
        SetRect(warning.rectTransform, new Vector2(12f, -8f), new Vector2(636f, 88f));
        var retry = CreateButton("Button_RetryRecoveryProtection", row, "再試行", 168f);
        SetRect(retry.transform as RectTransform, new Vector2(12f, -100f), new Vector2(168f, 40f));
        retry.onClick.AddListener(() =>
        {
            if (!projectService.RetryRecoveryProtection(out _))
                OnServiceStatusChanged("復旧データを別の教材として残せませんでした。詳細を確認して、もう一度試してください。", false);
        });
        UiRoundedTheme.ApplyToHierarchy(row, DesignTokens.CornerRadius);
        UiAccessibilityMetrics.EnsureButtonTargets(row);
    }

    // A notice band above the list: unsaved work from the previous session, with 復旧して開く / 破棄.
    void CreateRecoveryBand(ref float cursor, string message, string detail, Action open, Action discard)
    {
        const float height = 132f;
        var row = CreateRect("Project_Recovery", listContent);
        PlaceItem(row, cursor, height);
        cursor += height + DesignTokens.SpaceSm;
        row.gameObject.AddComponent<Image>().color = DesignTokens.BadgeBg(DesignTokens.Warning);

        var text = CreateText("Label", row, message, DesignTokens.FontSizeBody, DesignTokens.TextPrimary);
        text.textWrappingMode = TextWrappingModes.Normal;
        SetRect(text.rectTransform, new Vector2(12f, -8f), new Vector2(636f, 52f));
        var sub = CreateText("Date", row, detail, DesignTokens.FontSizeCaption, DesignTokens.TextSecondary);
        SetRect(sub.rectTransform, new Vector2(12f, -62f), new Vector2(636f, 22f));

        var openBtn = CreateButton("Button_RestoreRecovery", row, "復旧して開く", 160f, true);
        SetRect(openBtn.transform as RectTransform, new Vector2(12f, -88f), new Vector2(160f, 36f));
        openBtn.onClick.AddListener(() => open());
        var discardButton = CreateButton("Button_DiscardRecovery", row, "破棄", 96f);
        SetRect(discardButton.transform as RectTransform, new Vector2(182f, -88f), new Vector2(96f, 36f));
        discardButton.onClick.AddListener(() => discard());
        UiAccessibilityMetrics.EnsureButtonTargets(row);
        UiRoundedTheme.ApplyToHierarchy(row, DesignTokens.CornerRadius);
    }

    static void PlaceItem(RectTransform rect, float top, float height)
    {
        top += 8f;
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.offsetMin = new Vector2(4f, -top - height);
        rect.offsetMax = new Vector2(-4f, -top);
    }

    void SetListContentHeight(float height)
    {
        if (listContent == null) return;
        listContent.sizeDelta = new Vector2(listContent.sizeDelta.x, Mathf.Max(0f, height));
        listContent.anchoredPosition = new Vector2(listContent.anchoredPosition.x, 0f);
    }

    // All confirmations go through the central dialog (Esc = cancel, Enter = primary).
    static void Ask(string title, string message, string confirmLabel, Action onConfirm, bool destructive = false, string secondaryLabel = null, Action onSecondary = null)
        => SkillSyncConfirmDialog.Show(title, message, confirmLabel, onConfirm, destructive: destructive, secondaryLabel: secondaryLabel, onSecondary: onSecondary);

    void OnServiceStatusChanged(string message, bool succeeded)
    {
        SetStatus(message, succeeded);
    }

    void OnDirtyChanged(bool dirty) => RefreshDirtyVisual();

    void OnRecoveryChanged()
    {
        if (listContent != null) RefreshProjectList();
    }

    void OnRecoveryProtectionChanged()
    {
        if (string.IsNullOrEmpty(projectService.RecoveryProtectionWarning)) recoveryWarningShown = false;
        if (listContent != null) RefreshProjectList();
        ShowRecoveryProtectionWarning();
    }

    void RefreshDirtyVisual()
    {
        if (dialogTitleText != null)
        {
            dialogTitleText.text = DialogTitle;
            dialogTitleText.color = DesignTokens.TextPrimary;
        }

        if (openButton != null)
        {
            var label = openButton.GetComponentInChildren<TMP_Text>(true);
            if (label != null)
            {
                label.text = DialogTitle;
                label.color = DesignTokens.TextPrimary;
            }
        }
    }

    // Hooked to Application.wantsToQuit in players only (stopping Play Mode in the Editor never asks).
    bool HandleWantsToQuit()
    {
        if (allowQuit || projectService == null || !projectService.IsDirty) return true;
        AskBeforeQuit();
        return false;
    }

    void AskBeforeQuit()
    {
        Ask("終了する前に保存しますか？",
            $"「{projectService.CurrentProjectName}」に未保存の変更があります。保存せずに終了すると、変更は失われます。",
            "保存して終了",
            () => EditorProjectSaveFlow.Run(projectService, saved =>
            {
                if (!saved) return;
                allowQuit = true;
                Application.Quit();
            }),
            secondaryLabel: "保存せず終了",
            onSecondary: () =>
            {
                projectService.DiscardOwnedRecovery();
                allowQuit = true;
                Application.Quit();
            });
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
        text.textWrappingMode = TMPro.TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Ellipsis;
        return text;
    }

    static Button CreateButton(string objectName, Transform parent, string labelValue, float width, bool primary = false)
    {
        var rect = CreateRect(objectName, parent);
        rect.sizeDelta = new Vector2(width, DesignTokens.ButtonHeight);
        var image = rect.gameObject.AddComponent<Image>();
        image.color = primary ? DesignTokens.Accent : DesignTokens.BgSecondary;
        var button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        var layout = rect.gameObject.AddComponent<LayoutElement>();
        layout.minWidth = width;
        layout.preferredWidth = width;
        layout.preferredHeight = DesignTokens.ButtonHeight;

        var label = CreateText("Label", rect, labelValue, DesignTokens.FontSizeBody, primary ? DesignTokens.ButtonTextLight : DesignTokens.TextPrimary);
        Stretch(label.rectTransform);
        label.alignment = TextAlignmentOptions.Center;
        return button;
    }

    static void SetButtonLabel(Button button, string labelValue)
    {
        if (button == null) return;
        var label = button.GetComponentInChildren<TMP_Text>(true);
        if (label != null) label.text = labelValue;
    }

    static TMP_InputField CreateInput(string objectName, Transform parent, string placeholderValue)
    {
        var root = CreateRect(objectName, parent);
        var image = root.gameObject.AddComponent<Image>();
        image.color = DesignTokens.BgPrimary;

        var viewport = CreateRect("Text Area", root);
        viewport.anchorMin = Vector2.zero;
        viewport.anchorMax = Vector2.one;
        viewport.offsetMin = new Vector2(DesignTokens.InputPaddingH, 2f);
        viewport.offsetMax = new Vector2(-DesignTokens.InputPaddingH, -2f);
        viewport.gameObject.AddComponent<RectMask2D>();

        var placeholder = CreateText("Placeholder", viewport, placeholderValue, DesignTokens.FontSizeBody, DesignTokens.TextTertiary);
        Stretch(placeholder.rectTransform);
        var value = CreateText("Text", viewport, string.Empty, DesignTokens.FontSizeBody, DesignTokens.TextPrimary);
        Stretch(value.rectTransform);

        var input = root.gameObject.AddComponent<TMP_InputField>();
        input.targetGraphic = image;
        input.textViewport = viewport;
        input.textComponent = value;
        input.placeholder = placeholder;
        input.lineType = TMP_InputField.LineType.SingleLine;
        return input;
    }

    static void SetRect(RectTransform rect, Vector2 topLeft, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = topLeft;
        rect.sizeDelta = size;
    }

    static void SetTopRight(RectTransform rect, Vector2 topRight, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(1f, 1f);
        rect.anchoredPosition = topRight;
        rect.sizeDelta = size;
    }

    static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
