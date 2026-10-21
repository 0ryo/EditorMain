using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Central confirmation dialog for destructive or irreversible actions (delete, overwrite, discard, quit).
// Runtime-built, so no Prefab or Scene change is needed. Esc = cancel, Enter = primary button.
// While open it counts as an editor modal (EditWorkspace.HasOpenModal), which blocks 3D editing and shortcuts.
//
//   SkillSyncConfirmDialog.Show("部品を削除しますか？", "手順1で使われています。", "削除", () => DoDelete(), destructive: true);
//   // Three choices: primary / secondary / cancel.
//   SkillSyncConfirmDialog.Show("未保存の変更があります", "…", "保存して終了", Save, secondaryLabel: "保存せず終了", onSecondary: Quit);
[DefaultExecutionOrder(1000)] // After the editor controllers, so they still see the dialog as open on the frame it closes.
public sealed class SkillSyncConfirmDialog : MonoBehaviour
{
    const string ObjectName = "SkillSyncConfirmDialog";
    const float PanelWidth = 640f, ButtonHeight = 48f, ButtonMinWidth = 112f, ButtonPadding = 40f;
    const int TitleSize = 26, MessageSize = 20, ButtonSize = 20;
    static SkillSyncConfirmDialog instance;

    RectTransform panel;
    TMP_Text titleText, messageText;
    DialogButton cancelButton, secondaryButton, primaryButton;
    Action onConfirm, onSecondary, onCancel;
    int openedFrame;

    sealed class DialogButton
    {
        public Button button;
        public SkillSyncRoundedGraphic surface;
        public TMP_Text label;
        public LayoutElement layout;
    }

    public static bool IsOpen => instance != null && instance.gameObject.activeSelf;

    // Used by EditWorkspace.GetOpenModalRoot so the dialog blocks 3D editing and shortcuts like other modals.
    public static Transform GetOpenModalRoot(Transform within = null)
    {
        if (!IsOpen) return null;
        var root = instance.transform;
        return within == null || root == within || root.IsChildOf(within) ? root : null;
    }

    public static void Close()
    {
        if (instance != null) instance.Hide();
    }

    // title: bold heading (the action as a question). message: 1-2 sentences saying what will happen.
    // confirmLabel / secondaryLabel / cancelLabel: use verbs ("削除", "保存せず終了"), never "OK" or "実行".
    // destructive: paints the primary button red. secondaryLabel != null adds a middle button (three choices).
    // cancelLabel null or empty hides the cancel button (Esc still closes and calls onCancel).
    public static void Show(string title, string message, string confirmLabel, Action onConfirm,
        string cancelLabel = "キャンセル", bool destructive = false, string secondaryLabel = null, Action onSecondary = null, Action onCancel = null)
    {
        var dialog = Resolve();
        if (dialog == null)
        {
            Debug.LogWarning("[SkillSyncConfirmDialog] No canvas found: " + title);
            return;
        }
        dialog.Open(title, message, confirmLabel, onConfirm, cancelLabel, destructive, secondaryLabel, onSecondary, onCancel);
    }

    static SkillSyncConfirmDialog Resolve()
    {
        Canvas canvas = null;
        var controller = SkillSyncEditorController.Active;
        if (controller != null) canvas = controller.GetComponentInParent<Canvas>(true);
        if (canvas == null) canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null) return null;
        canvas = canvas.rootCanvas;
        if (instance != null && instance.transform.parent != canvas.transform) { Destroy(instance.gameObject); instance = null; }
        if (instance != null) return instance;

        var existing = canvas.transform.Find(ObjectName);
        if (existing != null && existing.GetComponent<SkillSyncConfirmDialog>() != null)
        {
            instance = existing.GetComponent<SkillSyncConfirmDialog>();
            return instance;
        }
        var go = new GameObject(ObjectName, typeof(RectTransform), typeof(Image), typeof(EditorUiInputBlocker), typeof(SkillSyncConfirmDialog));
        go.transform.SetParent(canvas.transform, false);
        var rect = (RectTransform)go.transform;
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
        var dimmer = go.GetComponent<Image>();
        dimmer.color = new Color(0f, 0f, 0f, 0.45f); dimmer.raycastTarget = true;
        var dialog = go.GetComponent<SkillSyncConfirmDialog>();
        dialog.Build(controller != null ? controller.GetComponent<SkillSyncDesignView>() : null);
        go.SetActive(false);
        instance = dialog;
        return dialog;
    }

    void Build(SkillSyncDesignView view)
    {
        TMP_FontAsset font = view != null && view.inspectorTitle != null ? view.inspectorTitle.font : null;
        panel = new GameObject("Panel", typeof(RectTransform), typeof(SkillSyncRoundedGraphic), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter)).GetComponent<RectTransform>();
        panel.SetParent(transform, false);
        panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(0.5f, 0.5f);
        panel.sizeDelta = new Vector2(PanelWidth, 200f);
        panel.localScale = Vector3.one * SkillSyncDesignLayout.Scale;
        var background = panel.GetComponent<SkillSyncRoundedGraphic>();
        background.color = DesignTokens.Surface; background.radius = 14f; background.borderWidth = 1f;
        background.borderColor = DesignTokens.Divider; background.raycastTarget = true;
        var layout = panel.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(32, 32, 28, 24); layout.spacing = 14f;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;
        var fitter = panel.GetComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained; fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        titleText = NewText("Title", panel, font, TitleSize, DesignTokens.TextPrimary, FontStyles.Bold);
        messageText = NewText("Message", panel, font, MessageSize, DesignTokens.TextPrimary, FontStyles.Normal);
        messageText.lineSpacing = 8f;

        var row = new GameObject("Buttons", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement)).GetComponent<RectTransform>();
        row.SetParent(panel, false);
        var rowLayout = row.GetComponent<HorizontalLayoutGroup>();
        rowLayout.spacing = 12f; rowLayout.childAlignment = TextAnchor.MiddleRight;
        rowLayout.childControlWidth = rowLayout.childControlHeight = true;
        rowLayout.childForceExpandWidth = rowLayout.childForceExpandHeight = false;
        rowLayout.padding = new RectOffset(0, 0, 10, 0);
        row.GetComponent<LayoutElement>().preferredHeight = ButtonHeight + 10f;
        cancelButton = NewButton("Button_Cancel", row, font);
        secondaryButton = NewButton("Button_Secondary", row, font);
        primaryButton = NewButton("Button_Primary", row, font);
        cancelButton.button.onClick.AddListener(() => Finish(onCancel));
        secondaryButton.button.onClick.AddListener(() => Finish(onSecondary));
        primaryButton.button.onClick.AddListener(() => Finish(onConfirm));
    }

    static TMP_Text NewText(string name, Transform parent, TMP_FontAsset font, int size, Color color, FontStyles style)
    {
        var text = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
        text.transform.SetParent(parent, false);
        if (font != null) text.font = font;
        text.fontSize = size; text.fontStyle = style; text.color = color; text.richText = false; text.raycastTarget = false;
        text.alignment = TextAlignmentOptions.TopLeft; text.textWrappingMode = TextWrappingModes.Normal;
        text.overflowMode = TextOverflowModes.Overflow;
        return text;
    }

    static DialogButton NewButton(string name, Transform parent, TMP_FontAsset font)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(SkillSyncRoundedGraphic), typeof(Button), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        var result = new DialogButton
        {
            surface = go.GetComponent<SkillSyncRoundedGraphic>(), button = go.GetComponent<Button>(), layout = go.GetComponent<LayoutElement>()
        };
        result.surface.radius = 8f; result.surface.borderWidth = 1f; result.surface.raycastTarget = true;
        result.button.targetGraphic = result.surface;
        result.button.navigation = new Navigation { mode = Navigation.Mode.None };
        var colors = result.button.colors;
        colors.normalColor = Color.white; colors.highlightedColor = new Color(0.9f, 0.9f, 0.9f, 1f);
        colors.pressedColor = new Color(0.78f, 0.78f, 0.78f, 1f); colors.selectedColor = Color.white;
        colors.colorMultiplier = 1f; colors.fadeDuration = 0.05f;
        result.button.colors = colors;
        result.layout.preferredHeight = ButtonHeight; result.layout.minHeight = ButtonHeight;
        var label = NewText("Label", go.transform, font, ButtonSize, DesignTokens.TextPrimary, FontStyles.Normal);
        label.alignment = TextAlignmentOptions.Center; label.textWrappingMode = TextWrappingModes.NoWrap;
        var labelRect = label.rectTransform;
        labelRect.anchorMin = Vector2.zero; labelRect.anchorMax = Vector2.one; labelRect.offsetMin = labelRect.offsetMax = Vector2.zero;
        result.label = label;
        return result;
    }

    static void Style(DialogButton button, string label, Color fill, Color border, Color text)
    {
        button.label.text = label; button.label.color = text;
        button.surface.color = fill; button.surface.borderColor = border; button.surface.SetVerticesDirty();
        float width = Mathf.Max(ButtonMinWidth, button.label.GetPreferredValues(label, Mathf.Infinity, Mathf.Infinity).x + ButtonPadding);
        button.layout.minWidth = button.layout.preferredWidth = width;
    }

    void Open(string title, string message, string confirmLabel, Action confirm, string cancelLabel, bool destructive, string secondaryLabel, Action secondary, Action cancel)
    {
        onConfirm = confirm; onSecondary = secondary; onCancel = cancel;
        gameObject.SetActive(true); // TMP needs an active hierarchy to measure the button captions.
        titleText.text = title ?? ""; titleText.gameObject.SetActive(!string.IsNullOrEmpty(title));
        messageText.text = message ?? ""; messageText.gameObject.SetActive(!string.IsNullOrEmpty(message));
        var border = DesignTokens.Divider;
        Style(primaryButton, string.IsNullOrEmpty(confirmLabel) ? "確定" : confirmLabel,
            destructive ? DesignTokens.Error : DesignTokens.Accent, destructive ? DesignTokens.Error : DesignTokens.Accent, DesignTokens.ButtonTextLight);
        bool hasSecondary = !string.IsNullOrEmpty(secondaryLabel);
        secondaryButton.button.gameObject.SetActive(hasSecondary);
        if (hasSecondary) Style(secondaryButton, secondaryLabel, DesignTokens.Surface, border, destructive ? DesignTokens.TextPrimary : DesignTokens.Error);
        bool hasCancel = !string.IsNullOrEmpty(cancelLabel);
        cancelButton.button.gameObject.SetActive(hasCancel);
        if (hasCancel) Style(cancelButton, cancelLabel, DesignTokens.Surface, border, DesignTokens.TextPrimary);

        // Release focus so a typed Enter or a previously clicked button cannot "submit" behind the dialog.
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        openedFrame = Time.frameCount;
        transform.SetAsLastSibling();
        LayoutRebuilder.ForceRebuildLayoutImmediate(panel);
    }

    void Update()
    {
        if (!Application.isFocused) return;
        // Ignore keys on the frame that opened the dialog (the Enter/Delete that triggered it must not also answer it).
        if (Time.frameCount <= openedFrame) return;
        if (EditInput.CancelPressedThisFrame()) Finish(onCancel);
        else if (EditInput.ConfirmPressedThisFrame()) Finish(onConfirm);
    }

    void Hide()
    {
        onConfirm = onSecondary = onCancel = null;
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        gameObject.SetActive(false);
    }

    // Closes first so the callback may open another dialog (for example save, then quit).
    void Finish(Action action)
    {
        Hide();
        action?.Invoke();
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
    }
}
