using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class ViewportCameraToolbar : MonoBehaviour
{
    const string RootName = "Panel_CameraToolbar";

    [SerializeField] Button focusButton;
    [SerializeField] Button frontButton;
    [SerializeField] Button rightButton;
    [SerializeField] Button topButton;
    [SerializeField] Button projectionButton;
    [SerializeField] Button resetButton;
    [SerializeField] TMP_Text projectionLabel;
    [SerializeField] GameObject hintRoot;
    [SerializeField] TMP_Text hintText;

    EditorCameraController cameraController;
    SelectionService selectionService;

    public static ViewportCameraToolbar Ensure(Transform parent)
    {
        if (parent == null) return null;

        var found = parent.Find(RootName);
        var toolbar = found != null ? found.GetComponent<ViewportCameraToolbar>() : null;
        if (toolbar == null)
        {
            toolbar = Build(parent);
        }

        toolbar.ResolveReferences();
        toolbar.WireButtons();
        toolbar.RefreshState();
        return toolbar;
    }

    void Awake()
    {
        ResolveReferences();
        WireButtons();
        RefreshState();
    }

    void LateUpdate()
    {
        ResolveReferences();
        RefreshState();
    }

    void ResolveReferences()
    {
        if (cameraController == null) cameraController = FindFirstObjectByType<EditorCameraController>();
        if (selectionService == null) selectionService = FindFirstObjectByType<SelectionService>();
    }

    void WireButtons()
    {
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

    internal void ShowHint(string message)
    {
        bool visible = !string.IsNullOrWhiteSpace(message);
        if (hintText != null) hintText.text = visible ? message : string.Empty;
        if (hintRoot != null) hintRoot.SetActive(visible);
    }

    static ViewportCameraToolbar Build(Transform parent)
    {
        var root = new GameObject(RootName, typeof(RectTransform), typeof(Image), typeof(HorizontalLayoutGroup), typeof(EditorUiInputBlocker));
        var rect = root.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.sizeDelta = new Vector2(548f, 44f);
        rect.anchoredPosition = new Vector2(0f, -60f);

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
        toolbar.focusButton = CreateButton(rect, "Button_FocusSelected", "選択へ  F", 88f, "選択中のオブジェクトを画面中央に表示します（F）");
        toolbar.frontButton = CreateButton(rect, "Button_ViewFront", "Z  前", 64f, "正面ビューへ切り替えます（1）");
        toolbar.rightButton = CreateButton(rect, "Button_ViewRight", "X  右", 64f, "右ビューへ切り替えます（3）");
        toolbar.topButton = CreateButton(rect, "Button_ViewTop", "Y  上", 64f, "上面ビューへ切り替えます（7）");
        toolbar.projectionButton = CreateButton(rect, "Button_Projection", "平行  O", 82f, "平行投影と透視投影を切り替えます（O）");
        toolbar.projectionLabel = toolbar.projectionButton.GetComponentInChildren<TMP_Text>(true);
        toolbar.resetButton = CreateButton(rect, "Button_ResetCamera", "初期  Home", 100f, "カメラを初期位置へ戻します（Home）");
        CreateHint(parent, toolbar);

        UiRoundedTheme.ApplyToHierarchy(root.transform, DesignTokens.CornerRadius);
        return toolbar;
    }

    static void CreateHint(Transform parent, ViewportCameraToolbar toolbar)
    {
        var root = new GameObject("Tooltip_CameraToolbar", typeof(RectTransform), typeof(Image));
        var rect = root.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.sizeDelta = new Vector2(420f, 32f);
        rect.anchoredPosition = new Vector2(0f, -108f);

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
        toolbar.hintRoot = root;

        UiRoundedTheme.ApplyToHierarchy(root.transform, DesignTokens.CornerRadius);
        root.SetActive(false);
    }

    static Button CreateButton(RectTransform parent, string objectName, string labelValue, float width, string hint)
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
        hintTrigger.Message = hint;
        return button;
    }
}

public sealed class ViewportToolbarHintTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public string Message { get; set; }

    public void OnPointerEnter(PointerEventData eventData)
    {
        GetComponentInParent<ViewportCameraToolbar>()?.ShowHint(Message);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        GetComponentInParent<ViewportCameraToolbar>()?.ShowHint(string.Empty);
    }
}
