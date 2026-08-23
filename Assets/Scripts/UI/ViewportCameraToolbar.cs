using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class ViewportCameraToolbar : MonoBehaviour
{
    const string RootName = "Panel_CameraToolbar";
    const string OpenButtonName = "Button_CameraTools";
    const float DockGap = 8f;

    [SerializeField] Button openButton;
    [SerializeField] Button focusButton;
    [SerializeField] Button frontButton;
    [SerializeField] Button rightButton;
    [SerializeField] Button topButton;
    [SerializeField] Button projectionButton;
    [SerializeField] Button resetButton;
    [SerializeField] TMP_Text projectionLabel;
    [SerializeField] RectTransform hintRoot;
    [SerializeField] TMP_Text hintText;

    EditorCameraController cameraController;
    SelectionService selectionService;
    RectTransform settingsButtonRect;
    RectTransform toolbarRect;
    CanvasGroup toolbarCanvasGroup;
    bool expanded;
    RectTransform activeHintTarget;
    readonly Vector3[] worldCorners = new Vector3[4];

    public static ViewportCameraToolbar Ensure(Transform parent)
    {
        if (parent == null) return null;

        var found = parent.Find(RootName);
        var toolbar = found != null ? found.GetComponent<ViewportCameraToolbar>() : null;
        if (toolbar == null) toolbar = Build(parent);

        toolbar.ResolveReferences();
        toolbar.WireButtons();
        toolbar.EnsureViewportButtonGuides(parent);
        toolbar.PositionDockedElements();
        toolbar.RefreshState();
        return toolbar;
    }

    void Awake()
    {
        toolbarRect = transform as RectTransform;
        toolbarCanvasGroup = GetComponent<CanvasGroup>();
        ResolveReferences();
        WireButtons();
        RefreshState();
    }

    void LateUpdate()
    {
        ResolveReferences();
        PositionDockedElements();
        PositionHintForTarget();
        RefreshState();
    }

    void ResolveReferences()
    {
        if (cameraController == null) cameraController = FindFirstObjectByType<EditorCameraController>();
        if (selectionService == null) selectionService = FindFirstObjectByType<SelectionService>();

        var parent = transform.parent;
        if (openButton == null && parent != null)
        {
            openButton = parent.Find(OpenButtonName)?.GetComponent<Button>();
        }

        if (settingsButtonRect == null && parent != null)
        {
            settingsButtonRect = parent.Find("Button_Settings") as RectTransform;
            if (settingsButtonRect == null) settingsButtonRect = parent.Find("Button_Settings_Runtime") as RectTransform;
        }

        if (toolbarRect == null) toolbarRect = transform as RectTransform;
        if (toolbarCanvasGroup == null) toolbarCanvasGroup = GetComponent<CanvasGroup>();
    }

    void EnsureViewportButtonGuides(Transform parent)
    {
        if (parent == null) return;

        var editModeRow = parent.Find("EditModeRow") ?? parent.Find("EditModeRow_Runtime");
        AttachGuide(
            editModeRow?.Find("Button_ModeBrowse")?.GetComponent<Button>(),
            "閲覧モード：オブジェクトを選択して内容を確認します");
        AttachGuide(
            editModeRow?.Find("Button_ModeTransform")?.GetComponent<Button>(),
            "移動モード：ギズモまたはW/A/S/D・矢印キーで位置を調整します");
        AttachGuide(
            editModeRow?.Find("Button_ModeScale")?.GetComponent<Button>(),
            "スケールモード：選択したオブジェクトの大きさを調整します");

        var settings = parent.Find("Button_Settings") ?? parent.Find("Button_Settings_Runtime");
        AttachGuide(
            settings?.GetComponent<Button>(),
            "設定：視点感度や位置・角度の吸着間隔を変更します");
        AttachGuide(
            parent.Find("Button_Hints")?.GetComponent<Button>(),
            "ヒント：操作方法の一覧を3Dビュー中央に表示します");
        AttachGuide(
            parent.Find("Button_Outliner")?.GetComponent<Button>(),
            "一覧：配置済みオブジェクトを検索・選択・表示・固定します");
        AttachGuide(openButton, "カメラ操作を開閉します");
    }

    void AttachGuide(Button button, string message)
    {
        if (button == null) return;
        var trigger = button.GetComponent<ViewportToolbarHintTrigger>();
        if (trigger == null) trigger = button.gameObject.AddComponent<ViewportToolbarHintTrigger>();
        trigger.Owner = this;
        trigger.Message = message;
    }

    void WireButtons()
    {
        Wire(openButton, ToggleExpanded);
        Wire(focusButton, FocusSelected);
        Wire(frontButton, () => SetPreset(EditorCameraController.ViewPreset.Front));
        Wire(rightButton, () => SetPreset(EditorCameraController.ViewPreset.Right));
        Wire(topButton, () => SetPreset(EditorCameraController.ViewPreset.Top));
        Wire(projectionButton, ToggleProjection);
        Wire(resetButton, ResetView);
    }

    static void Wire(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button == null) return;
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(action);
    }

    void ToggleExpanded()
    {
        SetExpanded(!expanded);
    }

    void SetExpanded(bool value)
    {
        expanded = value;
        if (toolbarCanvasGroup != null)
        {
            toolbarCanvasGroup.alpha = expanded ? 1f : 0f;
            toolbarCanvasGroup.interactable = expanded;
            toolbarCanvasGroup.blocksRaycasts = expanded;
        }

        if (!expanded) ShowHint(string.Empty);
        RefreshOpenButtonVisual();
    }

    void FocusSelected()
    {
        cameraController?.FocusSelected();
    }

    void SetPreset(EditorCameraController.ViewPreset preset)
    {
        cameraController?.SetViewPreset(preset);
    }

    void ToggleProjection()
    {
        cameraController?.ToggleProjection();
        RefreshState();
    }

    void ResetView()
    {
        cameraController?.ResetToDefaultView();
        RefreshState();
    }

    void RefreshState()
    {
        bool hasCamera = cameraController != null;
        if (openButton != null) openButton.interactable = hasCamera;
        if (focusButton != null) focusButton.interactable = hasCamera && selectionService != null && selectionService.Current != null;
        if (frontButton != null) frontButton.interactable = hasCamera;
        if (rightButton != null) rightButton.interactable = hasCamera;
        if (topButton != null) topButton.interactable = hasCamera;
        if (projectionButton != null) projectionButton.interactable = hasCamera;
        if (resetButton != null) resetButton.interactable = hasCamera;

        if (projectionLabel != null)
        {
            projectionLabel.text = hasCamera && cameraController.IsOrthographic ? "平行  O" : "透視  O";
        }
    }

    void RefreshOpenButtonVisual()
    {
        var image = openButton != null ? openButton.GetComponent<Image>() : null;
        if (image != null) image.color = DesignTokens.Surface;

        var outline = openButton != null ? openButton.GetComponent<Outline>() : null;
        if (outline != null)
        {
            outline.effectColor = expanded ? DesignTokens.Accent : DesignTokens.Divider;
        }
    }

    internal void ShowHint(string message, RectTransform target = null)
    {
        bool visible = !string.IsNullOrWhiteSpace(message);
        activeHintTarget = visible ? target : null;
        if (hintText != null) hintText.text = visible ? message : string.Empty;
        if (hintRoot != null) hintRoot.gameObject.SetActive(visible);
        if (visible) PositionHintForTarget();
    }

    void PositionDockedElements()
    {
        var parentRect = transform.parent as RectTransform;
        var openRect = openButton != null ? openButton.transform as RectTransform : null;
        if (parentRect == null || toolbarRect == null || openRect == null) return;

        float right = parentRect.rect.xMax - 12f;
        float settingsBottom = parentRect.rect.yMax - 52f;
        if (settingsButtonRect != null && settingsButtonRect.gameObject.activeInHierarchy)
        {
            settingsButtonRect.GetWorldCorners(worldCorners);
            right = parentRect.InverseTransformPoint(worldCorners[2]).x;
            settingsBottom = parentRect.InverseTransformPoint(worldCorners[0]).y;
        }

        float rightOffset = right - parentRect.rect.xMax;
        float cameraTopOffset = settingsBottom - parentRect.rect.yMax - DockGap;
        SetTopRightRect(openRect, new Vector2(40f, 40f), new Vector2(rightOffset, cameraTopOffset));

        float toolbarTopOffset = cameraTopOffset - 40f - DockGap;
        SetTopRightRect(toolbarRect, new Vector2(548f, 44f), new Vector2(rightOffset, toolbarTopOffset));

    }

    void PositionHintForTarget()
    {
        var parentRect = transform.parent as RectTransform;
        if (parentRect == null || hintRoot == null || activeHintTarget == null) return;

        hintRoot.sizeDelta = new Vector2(420f, 32f);
        activeHintTarget.GetWorldCorners(worldCorners);
        Vector3 bottomLeft = parentRect.InverseTransformPoint(worldCorners[0]);
        Vector3 topRight = parentRect.InverseTransformPoint(worldCorners[2]);
        float halfWidth = hintRoot.sizeDelta.x * 0.5f;
        float centerX = Mathf.Clamp(
            (bottomLeft.x + topRight.x) * 0.5f,
            parentRect.rect.xMin + halfWidth + 8f,
            parentRect.rect.xMax - halfWidth - 8f);
        float topY = bottomLeft.y - 4f;

        hintRoot.anchorMin = new Vector2(0f, 1f);
        hintRoot.anchorMax = new Vector2(0f, 1f);
        hintRoot.pivot = new Vector2(0.5f, 1f);
        hintRoot.anchoredPosition = new Vector2(centerX - parentRect.rect.xMin, topY - parentRect.rect.yMax);
    }

    static void SetTopRightRect(RectTransform rect, Vector2 size, Vector2 position)
    {
        if (rect == null) return;
        rect.anchorMin = Vector2.one;
        rect.anchorMax = Vector2.one;
        rect.pivot = Vector2.one;
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
    }

    static ViewportCameraToolbar Build(Transform parent)
    {
        var root = new GameObject(
            RootName,
            typeof(RectTransform),
            typeof(Image),
            typeof(HorizontalLayoutGroup),
            typeof(CanvasGroup),
            typeof(EditorUiInputBlocker));
        var rect = root.GetComponent<RectTransform>();
        rect.SetParent(parent, false);

        var image = root.GetComponent<Image>();
        image.color = DesignTokens.Surface;

        var outline = root.AddComponent<Outline>();
        outline.effectColor = DesignTokens.Divider;
        outline.effectDistance = new Vector2(1f, -1f);

        var layout = root.GetComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(6, 6, 4, 4);
        layout.spacing = 4f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = true;

        var toolbar = root.AddComponent<ViewportCameraToolbar>();
        toolbar.toolbarRect = rect;
        toolbar.toolbarCanvasGroup = root.GetComponent<CanvasGroup>();
        toolbar.openButton = CreateOpenButton(parent, toolbar);
        toolbar.focusButton = CreateButton(rect, "Button_FocusSelected", "選択へ  F", 88f, "選択中のオブジェクトを画面中央に表示します（F）", toolbar);
        toolbar.frontButton = CreateButton(rect, "Button_ViewFront", "Z  前", 64f, "正面ビューへ切り替えます（1）", toolbar);
        toolbar.rightButton = CreateButton(rect, "Button_ViewRight", "X  右", 64f, "右ビューへ切り替えます（3）", toolbar);
        toolbar.topButton = CreateButton(rect, "Button_ViewTop", "Y  上", 64f, "上面ビューへ切り替えます（7）", toolbar);
        toolbar.projectionButton = CreateButton(rect, "Button_Projection", "平行  O", 82f, "平行投影と透視投影を切り替えます（O）", toolbar);
        toolbar.projectionLabel = toolbar.projectionButton.GetComponentInChildren<TMP_Text>(true);
        toolbar.resetButton = CreateButton(rect, "Button_ResetCamera", "初期  Home", 100f, "カメラを初期位置へ戻します（Home）", toolbar);
        CreateHint(parent, toolbar);

        UiRoundedTheme.ApplyToHierarchy(root.transform, DesignTokens.CornerRadius);
        toolbar.SetExpanded(false);
        return toolbar;
    }

    static Button CreateOpenButton(Transform parent, ViewportCameraToolbar toolbar)
    {
        var go = new GameObject(OpenButtonName, typeof(RectTransform), typeof(Image), typeof(Button), typeof(EditorUiInputBlocker));
        var rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);

        var image = go.GetComponent<Image>();
        image.color = DesignTokens.Surface;
        var outline = go.AddComponent<Outline>();
        outline.effectColor = DesignTokens.Divider;
        outline.effectDistance = new Vector2(1f, -1f);

        var button = go.GetComponent<Button>();
        button.targetGraphic = image;

        UiRoundedTheme.ApplyToHierarchy(go.transform, DesignTokens.CornerRadius);
        var iconSprite = Resources.Load<Sprite>("UI/Icons/icon_camera");
        if (iconSprite != null)
        {
            var icon = CreateIconPart("Icon_Camera", rect, new Vector2(30f, 26f), Vector2.zero);
            icon.sprite = iconSprite;
            icon.preserveAspect = true;
            icon.color = Color.white;
        }
        else
        {
            var body = CreateIconPart("Icon_CameraBody", rect, new Vector2(23f, 15f), new Vector2(0f, -1f));
            CreateIconPart("Icon_CameraTop", rect, new Vector2(9f, 4f), new Vector2(-4f, 8f));
            var lens = CreateIconPart("Icon_CameraLens", body.rectTransform, new Vector2(7f, 7f), Vector2.zero);
            lens.color = DesignTokens.Surface;
            UiRoundedTheme.ApplyCircleToElement(lens);
            Debug.LogWarning("[ViewportCameraToolbar] Camera icon sprite was not found. Using the fallback icon.");
        }

        var hintTrigger = go.AddComponent<ViewportToolbarHintTrigger>();
        hintTrigger.Owner = toolbar;
        hintTrigger.Message = "カメラ操作を開閉します";
        return button;
    }

    static Image CreateIconPart(string name, Transform parent, Vector2 size, Vector2 position)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        var rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
        var image = go.GetComponent<Image>();
        image.color = DesignTokens.TextPrimary;
        image.raycastTarget = false;
        return image;
    }

    static void CreateHint(Transform parent, ViewportCameraToolbar toolbar)
    {
        var root = new GameObject("Tooltip_CameraToolbar", typeof(RectTransform), typeof(Image));
        var rect = root.GetComponent<RectTransform>();
        rect.SetParent(parent, false);

        var image = root.GetComponent<Image>();
        image.color = DesignTokens.Surface;
        image.raycastTarget = false;

        var outline = root.AddComponent<Outline>();
        outline.effectColor = DesignTokens.Divider;
        outline.effectDistance = new Vector2(1f, -1f);

        var textGo = new GameObject("Text_Hint", typeof(RectTransform), typeof(TextMeshProUGUI));
        var textRect = textGo.GetComponent<RectTransform>();
        textRect.SetParent(rect, false);
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(12f, 0f);
        textRect.offsetMax = new Vector2(-12f, 0f);

        toolbar.hintText = textGo.GetComponent<TMP_Text>();
        toolbar.hintText.fontSize = DesignTokens.FontSizeCaption;
        toolbar.hintText.color = DesignTokens.TextPrimary;
        toolbar.hintText.alignment = TextAlignmentOptions.Center;
        toolbar.hintText.raycastTarget = false;
        toolbar.hintRoot = rect;

        UiRoundedTheme.ApplyToHierarchy(root.transform, DesignTokens.CornerRadius);
        root.SetActive(false);
    }

    static Button CreateButton(RectTransform parent, string objectName, string labelValue, float width, string hint, ViewportCameraToolbar toolbar)
    {
        var go = new GameObject(objectName, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        var rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);

        var layout = go.GetComponent<LayoutElement>();
        layout.minWidth = width;
        layout.preferredWidth = width;
        layout.minHeight = 36f;

        var image = go.GetComponent<Image>();
        image.color = DesignTokens.BgSecondary;

        var button = go.GetComponent<Button>();
        button.targetGraphic = image;

        var labelGo = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        var labelRect = labelGo.GetComponent<RectTransform>();
        labelRect.SetParent(rect, false);
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(6f, 0f);
        labelRect.offsetMax = new Vector2(-6f, 0f);

        var label = labelGo.GetComponent<TMP_Text>();
        label.text = labelValue;
        label.fontSize = DesignTokens.FontSizeCaption;
        label.color = DesignTokens.TextPrimary;
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;

        var hintTrigger = go.AddComponent<ViewportToolbarHintTrigger>();
        hintTrigger.Owner = toolbar;
        hintTrigger.Message = hint;
        return button;
    }
}

public sealed class ViewportToolbarHintTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public ViewportCameraToolbar Owner { get; set; }
    public string Message { get; set; }

    public void OnPointerEnter(PointerEventData eventData)
    {
        (Owner ?? GetComponentInParent<ViewportCameraToolbar>())?.ShowHint(Message, transform as RectTransform);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        (Owner ?? GetComponentInParent<ViewportCameraToolbar>())?.ShowHint(string.Empty);
    }
}
