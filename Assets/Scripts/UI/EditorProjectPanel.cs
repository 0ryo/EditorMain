using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class EditorProjectPanel : MonoBehaviour
{
    const string ControllerName = "EditorProjectPanelController";
    const string ModalName = "Panel_ProjectFiles";

    Transform uiRoot;
    EditorProjectService projectService;
    Button openButton;
    Button saveProjectButton;
    RectTransform modal;
    TMP_Text dialogTitleText;
    TMP_InputField projectNameInput;
    TMP_Text statusText;
    RectTransform listContent;
    RectTransform confirmation;
    TMP_Text confirmationText;
    Action confirmedAction;
#if !UNITY_EDITOR
    bool allowQuit;
#endif

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
        if (uiRoot == null) uiRoot = transform.parent;
    }

    void OnDestroy()
    {
        if (projectService != null)
        {
            projectService.StatusChanged -= OnServiceStatusChanged;
            projectService.DirtyChanged -= OnDirtyChanged;
            projectService.RecoveryChanged -= OnRecoveryChanged;
        }
#if !UNITY_EDITOR
        Application.wantsToQuit -= HandleWantsToQuit;
#endif
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

        BuildOpenButton();
        BuildModal();
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
            openButton = CreateButton("Button_ProjectFiles", topBar, "プロジェクト", 112f);
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
            var dimmer = modal.gameObject.AddComponent<Image>();
            dimmer.color = new Color(0f, 0f, 0f, 0.35f);
            dimmer.raycastTarget = true;
            modal.gameObject.AddComponent<EditorUiInputBlocker>();
        }

        var dialog = CreateRect("Dialog", modal);
        dialog.anchorMin = dialog.anchorMax = new Vector2(0.5f, 0.5f);
        dialog.pivot = new Vector2(0.5f, 0.5f);
        dialog.sizeDelta = new Vector2(720f, 620f);
        var dialogImage = dialog.gameObject.AddComponent<Image>();
        dialogImage.color = DesignTokens.Surface;

        dialogTitleText = CreateText("Title", dialog, "プロジェクト", DesignTokens.FontSizeHeading, DesignTokens.TextPrimary);
        SetRect(dialogTitleText.rectTransform, new Vector2(24f, -20f), new Vector2(560f, 36f));

        var close = CreateButton("Button_Close", dialog, "閉じる", 80f);
        SetTopRight(close.transform as RectTransform, new Vector2(-20f, -18f), new Vector2(80f, 36f));
        close.onClick.AddListener(Close);

        var nameLabel = CreateText("Label_ProjectName", dialog, "プロジェクト名", DesignTokens.FontSizeCaption, DesignTokens.TextSecondary);
        SetRect(nameLabel.rectTransform, new Vector2(24f, -72f), new Vector2(160f, 24f));
        projectNameInput = CreateInput("Input_ProjectName", dialog, "VRCourseEditor");
        SetRect(projectNameInput.transform as RectTransform, new Vector2(24f, -98f), new Vector2(672f, 40f));
        projectNameInput.onValueChanged.AddListener(_ => RefreshSaveButtonLabel());

        saveProjectButton = CreateButton("Button_SaveProject", dialog, "名前を付けて保存", 168f, true);
        SetRect(saveProjectButton.transform as RectTransform, new Vector2(24f, -154f), new Vector2(168f, 40f));
        saveProjectButton.onClick.AddListener(SaveProject);

        var newButton = CreateButton("Button_NewProject", dialog, "新規", 96f);
        SetRect(newButton.transform as RectTransform, new Vector2(204f, -154f), new Vector2(96f, 40f));
        newButton.onClick.AddListener(RequestNewProject);

        var placementExportButton = CreateButton("Button_ExportPlacement", dialog, "配置JSONエクスポート", 180f);
        SetTopRight(placementExportButton.transform as RectTransform, new Vector2(-24f, -154f), new Vector2(180f, 40f));
        placementExportButton.onClick.AddListener(ExportPlacement);

        var folderText = CreateText(
            "Text_SaveLocation",
            dialog,
            "保存先: アプリの永続データ / Projects",
            DesignTokens.FontSizeCaption,
            DesignTokens.TextSecondary);
        SetRect(folderText.rectTransform, new Vector2(24f, -202f), new Vector2(672f, 24f));

        statusText = CreateText("Text_Status", dialog, string.Empty, DesignTokens.FontSizeBody, DesignTokens.TextSecondary);
        SetRect(statusText.rectTransform, new Vector2(24f, -228f), new Vector2(672f, 32f));

        var recentTitle = CreateText("Title_Recent", dialog, "保存済みプロジェクト", DesignTokens.FontSizeSubheading, DesignTokens.TextPrimary);
        SetRect(recentTitle.rectTransform, new Vector2(24f, -268f), new Vector2(300f, 28f));

        BuildProjectList(dialog);
        BuildConfirmation(dialog);
        UiRoundedTheme.ApplyToHierarchy(dialog, DesignTokens.CornerRadius);
        RefreshDirtyVisual();
        modal.gameObject.SetActive(false);
    }

    void BuildProjectList(RectTransform dialog)
    {
        var scrollRoot = CreateRect("Scroll_Projects", dialog);
        SetRect(scrollRoot, new Vector2(24f, -304f), new Vector2(672f, 282f));
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
    }

    void BuildConfirmation(RectTransform dialog)
    {
        confirmation = CreateRect("Panel_Confirmation", dialog);
        confirmation.anchorMin = new Vector2(0f, 0f);
        confirmation.anchorMax = new Vector2(1f, 0f);
        confirmation.pivot = new Vector2(0.5f, 0f);
        confirmation.offsetMin = new Vector2(24f, 18f);
        confirmation.offsetMax = new Vector2(-24f, 72f);
        var image = confirmation.gameObject.AddComponent<Image>();
        image.color = DesignTokens.BgSecondary;

        confirmationText = CreateText("Text_Message", confirmation, string.Empty, DesignTokens.FontSizeCaption, DesignTokens.TextPrimary);
        confirmationText.rectTransform.anchorMin = new Vector2(0f, 0f);
        confirmationText.rectTransform.anchorMax = new Vector2(1f, 1f);
        confirmationText.rectTransform.offsetMin = new Vector2(12f, 0f);
        confirmationText.rectTransform.offsetMax = new Vector2(-184f, 0f);
        confirmationText.alignment = TextAlignmentOptions.MidlineLeft;

        var cancel = CreateButton("Button_Cancel", confirmation, "キャンセル", 80f);
        SetTopRight(cancel.transform as RectTransform, new Vector2(-96f, -7f), new Vector2(88f, 40f));
        cancel.onClick.AddListener(HideConfirmation);
        var confirm = CreateButton("Button_Confirm", confirmation, "実行", 72f, true);
        SetTopRight(confirm.transform as RectTransform, new Vector2(-12f, -7f), new Vector2(72f, 40f));
        confirm.onClick.AddListener(ConfirmPendingAction);
        confirmation.gameObject.SetActive(false);
    }

    void Open()
    {
        if (modal == null) BuildModal();
        if (modal == null) return;

        var graph = FindFirstObjectByType<CurriculumGraphService>();
        string name = graph != null && graph.curriculum != null
            ? graph.curriculum.projectName
            : projectService.CurrentProjectName;
        projectNameInput.SetTextWithoutNotify(string.IsNullOrWhiteSpace(name) ? "VRCourseEditor" : name);
        RefreshSaveButtonLabel();
        RefreshDirtyVisual();
        statusText.text = projectService.IsDirty ? "未保存の変更があります" : string.Empty;
        statusText.color = projectService.IsDirty ? DesignTokens.Warning : DesignTokens.TextSecondary;
        HideConfirmation();
        RefreshProjectList();
        modal.gameObject.SetActive(true);
        modal.SetAsLastSibling();
    }

    void Close()
    {
        HideConfirmation();
        if (modal != null) modal.gameObject.SetActive(false);
    }

    void SaveProject()
    {
        string name = GetProjectName();
        string safeName = ExportFileNameUtility.SanitizeProjectName(name, "VRCourseEditor");
        EditorProjectFileInfo existing = null;
        foreach (var info in EditorProjectStore.ListProjects())
        {
            if (!string.Equals(info.DisplayName, safeName, StringComparison.OrdinalIgnoreCase)) continue;
            existing = info;
            break;
        }

        bool isCurrentFile = existing != null &&
            !string.IsNullOrWhiteSpace(projectService.CurrentProjectPath) &&
            string.Equals(existing.Path, projectService.CurrentProjectPath, StringComparison.OrdinalIgnoreCase);
        if (existing != null && !isCurrentFile)
        {
            ShowConfirmation($"「{existing.DisplayName}」を上書きします。", () => SaveProjectNow(name));
            return;
        }

        SaveProjectNow(name);
    }

    void SaveProjectNow(string name)
    {
        projectService.Save(name, out var message);
        statusText.text = message;
        RefreshSaveButtonLabel();
        HideConfirmation();
        RefreshProjectList();
    }

    void ExportPlacement()
    {
        var exportService = FindFirstObjectByType<PlacementExportService>();
        if (exportService == null)
        {
            exportService = projectService.gameObject.AddComponent<PlacementExportService>();
        }

        exportService.projectName = GetProjectName();
        bool succeeded = exportService.TryExportPlacementJson(out var path, out var error);
        statusText.text = succeeded ? "配置JSONを出力しました: " + path : "配置JSONを出力できません: " + error;
        statusText.color = succeeded ? DesignTokens.Success : DesignTokens.Error;
    }

    void RequestNewProject()
    {
        ShowConfirmation("現在の編集内容を閉じて、新規プロジェクトを作成します。", () =>
        {
            projectService.NewProject(GetProjectName(), out var message);
            statusText.text = message;
            RefreshSaveButtonLabel();
            HideConfirmation();
        });
    }

    void RequestLoad(EditorProjectFileInfo info)
    {
        ShowConfirmation($"現在の編集内容を閉じて「{info.DisplayName}」を読み込みます。", () =>
        {
            bool loaded = projectService.Load(info.Path, out var message);
            statusText.text = message;
            if (loaded)
            {
                projectNameInput.SetTextWithoutNotify(projectService.CurrentProjectName);
                RefreshSaveButtonLabel();
                RefreshProjectList();
            }
            HideConfirmation();
        });
    }

    void RequestRecoveryLoad(EditorProjectFileInfo info)
    {
        ShowConfirmation($"現在の編集内容を閉じて「{info.DisplayName}」の自動保存を復元します。", () =>
        {
            bool loaded = projectService.LoadRecovery(out var message);
            statusText.text = message;
            if (loaded)
            {
                projectNameInput.SetTextWithoutNotify(projectService.CurrentProjectName);
                RefreshSaveButtonLabel();
                RefreshDirtyVisual();
            }
            HideConfirmation();
        });
    }

    void RequestRecoveryDelete(EditorProjectFileInfo info)
    {
        ShowConfirmation($"「{info.DisplayName}」の自動保存データを破棄します。", () =>
        {
            projectService.DeleteRecovery(out var message);
            statusText.text = message;
            HideConfirmation();
        });
    }

    void RefreshProjectList()
    {
        if (listContent == null) return;
        for (int i = listContent.childCount - 1; i >= 0; i--)
        {
            Destroy(listContent.GetChild(i).gameObject);
        }

        var projects = EditorProjectStore.ListProjects();
        int rowIndex = 0;
        if (EditorProjectStore.TryGetRecovery(out var recovery))
        {
            CreateRecoveryRow(recovery, rowIndex++);
        }

        if (projects.Count == 0 && rowIndex == 0)
        {
            var empty = CreateText("Text_Empty", listContent, "保存済みプロジェクトはありません", DesignTokens.FontSizeBody, DesignTokens.TextSecondary);
            empty.alignment = TextAlignmentOptions.Center;
            SetListItemRect(empty.rectTransform, 0, 52f);
            SetListContentHeight(68f);
            return;
        }

        for (int index = 0; index < projects.Count; index++)
        {
            var info = projects[index];
            var row = CreateRect("Project_" + info.DisplayName, listContent);
            SetListItemRect(row, rowIndex++, 52f);
            var rowImage = row.gameObject.AddComponent<Image>();
            rowImage.color = DesignTokens.Surface;

            var label = CreateText("Label", row, info.DisplayName, DesignTokens.FontSizeBody, DesignTokens.TextPrimary);
            label.rectTransform.anchorMin = new Vector2(0f, 0f);
            label.rectTransform.anchorMax = new Vector2(1f, 1f);
            label.rectTransform.offsetMin = new Vector2(12f, 0f);
            label.rectTransform.offsetMax = new Vector2(-180f, 0f);
            label.alignment = TextAlignmentOptions.MidlineLeft;

            var date = CreateText("Date", row, info.LastWriteTimeUtc.ToLocalTime().ToString("yyyy/MM/dd HH:mm"), DesignTokens.FontSizeCaption, DesignTokens.TextSecondary);
            date.rectTransform.anchorMin = new Vector2(1f, 0f);
            date.rectTransform.anchorMax = new Vector2(1f, 1f);
            date.rectTransform.pivot = new Vector2(1f, 0.5f);
            date.rectTransform.anchoredPosition = new Vector2(-96f, 0f);
            date.rectTransform.sizeDelta = new Vector2(150f, 52f);
            date.alignment = TextAlignmentOptions.MidlineRight;

            var load = CreateButton("Button_Load", row, "読込", 72f);
            load.transform.SetAsLastSibling();
            var loadRect = load.transform as RectTransform;
            loadRect.anchorMin = loadRect.anchorMax = new Vector2(1f, 0.5f);
            loadRect.pivot = new Vector2(1f, 0.5f);
            loadRect.anchoredPosition = new Vector2(-8f, 0f);
            loadRect.sizeDelta = new Vector2(72f, 36f);
            load.onClick.AddListener(() => RequestLoad(info));
            UiRoundedTheme.ApplyToHierarchy(row, DesignTokens.CornerRadius);
        }

        SetListContentHeight(16f + rowIndex * 52f + Mathf.Max(0, rowIndex - 1) * DesignTokens.SpaceSm);
    }

    void CreateRecoveryRow(EditorProjectFileInfo info, int index)
    {
        var row = CreateRect("Project_Recovery", listContent);
        SetListItemRect(row, index, 52f);
        var rowImage = row.gameObject.AddComponent<Image>();
        rowImage.color = DesignTokens.BadgeBg(DesignTokens.Warning);

        var label = CreateText("Label", row, "自動保存: " + info.DisplayName, DesignTokens.FontSizeBody, DesignTokens.TextPrimary);
        label.rectTransform.anchorMin = new Vector2(0f, 0f);
        label.rectTransform.anchorMax = new Vector2(1f, 1f);
        label.rectTransform.offsetMin = new Vector2(12f, 0f);
        label.rectTransform.offsetMax = new Vector2(-312f, 0f);
        label.alignment = TextAlignmentOptions.MidlineLeft;

        var date = CreateText("Date", row, info.LastWriteTimeUtc.ToLocalTime().ToString("yyyy/MM/dd HH:mm"), DesignTokens.FontSizeCaption, DesignTokens.TextSecondary);
        date.rectTransform.anchorMin = new Vector2(1f, 0f);
        date.rectTransform.anchorMax = new Vector2(1f, 1f);
        date.rectTransform.pivot = new Vector2(1f, 0.5f);
        date.rectTransform.anchoredPosition = new Vector2(-168f, 0f);
        date.rectTransform.sizeDelta = new Vector2(136f, 52f);
        date.alignment = TextAlignmentOptions.MidlineRight;

        var discard = CreateButton("Button_DiscardRecovery", row, "破棄", 64f);
        var discardRect = discard.transform as RectTransform;
        discardRect.anchorMin = discardRect.anchorMax = new Vector2(1f, 0.5f);
        discardRect.pivot = new Vector2(1f, 0.5f);
        discardRect.anchoredPosition = new Vector2(-88f, 0f);
        discardRect.sizeDelta = new Vector2(64f, 36f);
        discard.onClick.AddListener(() => RequestRecoveryDelete(info));

        var restore = CreateButton("Button_RestoreRecovery", row, "復元", 72f, true);
        var restoreRect = restore.transform as RectTransform;
        restoreRect.anchorMin = restoreRect.anchorMax = new Vector2(1f, 0.5f);
        restoreRect.pivot = new Vector2(1f, 0.5f);
        restoreRect.anchoredPosition = new Vector2(-8f, 0f);
        restoreRect.sizeDelta = new Vector2(72f, 36f);
        restore.onClick.AddListener(() => RequestRecoveryLoad(info));
        UiRoundedTheme.ApplyToHierarchy(row, DesignTokens.CornerRadius);
    }

    void RefreshSaveButtonLabel()
    {
        if (saveProjectButton == null || projectService == null) return;

        bool hasCurrentFile = !string.IsNullOrWhiteSpace(projectService.CurrentProjectPath);
        bool sameProjectName = string.Equals(
            ExportFileNameUtility.SanitizeProjectName(GetProjectName(), "VRCourseEditor"),
            ExportFileNameUtility.SanitizeProjectName(projectService.CurrentProjectName, "VRCourseEditor"),
            StringComparison.OrdinalIgnoreCase);
        SetButtonLabel(saveProjectButton, hasCurrentFile && sameProjectName ? "上書き保存" : "名前を付けて保存");
    }

    static void SetListItemRect(RectTransform rect, int index, float height)
    {
        float top = 8f + index * (height + DesignTokens.SpaceSm);
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

    void ShowConfirmation(string message, Action action)
    {
        confirmedAction = action;
        confirmationText.text = message;
        confirmation.gameObject.SetActive(true);
        confirmation.SetAsLastSibling();
    }

    void ConfirmPendingAction()
    {
        var action = confirmedAction;
        confirmedAction = null;
        action?.Invoke();
    }

    void HideConfirmation()
    {
        confirmedAction = null;
        if (confirmation != null) confirmation.gameObject.SetActive(false);
    }

    void OnServiceStatusChanged(string message, bool succeeded)
    {
        if (statusText == null) return;
        statusText.text = message;
        statusText.color = succeeded ? DesignTokens.Success : DesignTokens.Error;
    }

    void OnDirtyChanged(bool dirty)
    {
        RefreshDirtyVisual();
        if (statusText == null || modal == null || !modal.gameObject.activeInHierarchy) return;

        if (dirty)
        {
            statusText.text = "未保存の変更があります";
            statusText.color = DesignTokens.Warning;
        }
        else if (string.Equals(statusText.text, "未保存の変更があります", StringComparison.Ordinal))
        {
            statusText.text = string.Empty;
            statusText.color = DesignTokens.TextSecondary;
        }
    }

    void OnRecoveryChanged()
    {
        if (listContent != null) RefreshProjectList();
    }

    void RefreshDirtyVisual()
    {
        bool dirty = projectService != null && projectService.IsDirty;
        if (dialogTitleText != null)
        {
            dialogTitleText.text = dirty ? "プロジェクト  •  未保存" : "プロジェクト";
            dialogTitleText.color = dirty ? DesignTokens.Warning : DesignTokens.TextPrimary;
        }

        if (openButton != null)
        {
            var label = openButton.GetComponentInChildren<TMP_Text>(true);
            if (label != null)
            {
                label.text = dirty ? "プロジェクト  •" : "プロジェクト";
                label.color = dirty ? DesignTokens.Warning : DesignTokens.TextPrimary;
            }
        }
    }

#if !UNITY_EDITOR
    bool HandleWantsToQuit()
    {
        if (allowQuit || projectService == null || !projectService.IsDirty) return true;

        projectService.SaveRecoveryNow(out _);
        Open();
        ShowConfirmation("未保存の変更があります。終了するときは復旧用の自動保存を残します。", () =>
        {
            allowQuit = true;
            Application.Quit();
        });
        return false;
    }
#endif

    string GetProjectName()
    {
        return string.IsNullOrWhiteSpace(projectNameInput.text)
            ? "VRCourseEditor"
            : projectNameInput.text.Trim();
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
        text.enableWordWrapping = false;
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
