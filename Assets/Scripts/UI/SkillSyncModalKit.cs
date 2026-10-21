using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Runtime builders shared by the start screen and the save-as dialog, matching SkillSyncConfirmDialog's look
// (rounded white panel on a dimmer, DesignTokens colours, the inspector's Japanese font).
public static class SkillSyncModalKit
{
    public enum ButtonKind { Primary, Secondary, Danger, Quiet }

    public sealed class KitButton
    {
        public Button button;
        public SkillSyncRoundedGraphic surface;
        public TMP_Text label;
        public LayoutElement layout;
        public ButtonKind kind;
    }

    // Root canvas that hosts modals, and the font to use. Null when the UI is not up yet.
    public static Canvas ResolveCanvas(out TMP_FontAsset font)
    {
        font = null;
        Canvas canvas = null;
        var controller = SkillSyncEditorController.Active;
        if (controller != null)
        {
            canvas = controller.GetComponentInParent<Canvas>(true);
            var view = controller.GetComponent<SkillSyncDesignView>();
            if (view != null && view.inspectorTitle != null) font = view.inspectorTitle.font;
        }
        if (canvas == null) canvas = UnityEngine.Object.FindFirstObjectByType<Canvas>();
        return canvas != null ? canvas.rootCanvas : null;
    }

    // Full-screen dimmer that blocks the editor behind it. The caller adds its own MonoBehaviour to the returned object.
    public static GameObject NewModalRoot(string name, Canvas canvas, params Type[] extraComponents)
    {
        var types = new Type[3 + extraComponents.Length];
        types[0] = typeof(RectTransform); types[1] = typeof(Image); types[2] = typeof(EditorUiInputBlocker);
        extraComponents.CopyTo(types, 3);
        var go = new GameObject(name, types);
        go.transform.SetParent(canvas.transform, false);
        var rect = (RectTransform)go.transform;
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
        var dimmer = go.GetComponent<Image>();
        dimmer.color = new Color(0f, 0f, 0f, 0.45f); dimmer.raycastTarget = true;
        return go;
    }

    public static RectTransform NewPanel(Transform parent, float width, RectOffset padding, float spacing)
    {
        var panel = new GameObject("Panel", typeof(RectTransform), typeof(SkillSyncRoundedGraphic), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter)).GetComponent<RectTransform>();
        panel.SetParent(parent, false);
        panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(0.5f, 0.5f);
        panel.sizeDelta = new Vector2(width, 200f);
        panel.localScale = Vector3.one * SkillSyncDesignLayout.Scale;
        var background = panel.GetComponent<SkillSyncRoundedGraphic>();
        background.color = DesignTokens.Surface; background.radius = 14f; background.borderWidth = 1f;
        background.borderColor = DesignTokens.Divider; background.raycastTarget = true;
        var layout = panel.GetComponent<VerticalLayoutGroup>();
        layout.padding = padding; layout.spacing = spacing;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;
        var fitter = panel.GetComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained; fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        return panel;
    }

    public static TMP_Text NewText(string name, Transform parent, TMP_FontAsset font, int size, Color color, FontStyles style = FontStyles.Normal,
        TextAlignmentOptions alignment = TextAlignmentOptions.TopLeft, bool wrap = true)
    {
        var text = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
        text.transform.SetParent(parent, false);
        if (font != null) text.font = font;
        text.fontSize = size; text.fontStyle = style; text.color = color; text.richText = false; text.raycastTarget = false;
        text.alignment = alignment;
        text.textWrappingMode = wrap ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
        text.overflowMode = wrap ? TextOverflowModes.Overflow : TextOverflowModes.Ellipsis;
        SkillSyncLineBreak.Attach(text);
        return text;
    }

    public static LayoutElement WithHeight(Component component, float preferredHeight, float flexibleWidth = -1f)
    {
        var layout = component.GetComponent<LayoutElement>();
        if (layout == null) layout = component.gameObject.AddComponent<LayoutElement>();
        layout.preferredHeight = preferredHeight; layout.minHeight = preferredHeight;
        if (flexibleWidth >= 0f) layout.flexibleWidth = flexibleWidth;
        return layout;
    }

    public static KitButton NewButton(string name, Transform parent, TMP_FontAsset font, int fontSize = 20)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(SkillSyncRoundedGraphic), typeof(Button), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        var result = new KitButton
        {
            surface = go.GetComponent<SkillSyncRoundedGraphic>(), button = go.GetComponent<Button>(), layout = go.GetComponent<LayoutElement>()
        };
        result.surface.radius = 8f; result.surface.borderWidth = 1f; result.surface.raycastTarget = true;
        result.button.targetGraphic = result.surface;
        result.button.navigation = new Navigation { mode = Navigation.Mode.None };
        var colors = result.button.colors;
        colors.normalColor = Color.white; colors.highlightedColor = new Color(0.9f, 0.9f, 0.9f, 1f);
        colors.pressedColor = new Color(0.78f, 0.78f, 0.78f, 1f); colors.selectedColor = Color.white;
        colors.disabledColor = new Color(1f, 1f, 1f, 0.6f);
        colors.colorMultiplier = 1f; colors.fadeDuration = 0.05f;
        result.button.colors = colors;
        var label = NewText("Label", go.transform, font, fontSize, DesignTokens.TextPrimary, FontStyles.Normal, TextAlignmentOptions.Center, false);
        var labelRect = label.rectTransform;
        labelRect.anchorMin = Vector2.zero; labelRect.anchorMax = Vector2.one; labelRect.offsetMin = labelRect.offsetMax = Vector2.zero;
        result.label = label;
        return result;
    }

    // Caption, look and a width that fits the caption. Quiet = text-like button for low-emphasis choices.
    public static void Style(KitButton button, string caption, ButtonKind kind, float height = 48f, float minWidth = 112f, float padding = 40f)
    {
        button.kind = kind;
        button.label.text = caption;
        Color fill, border, text;
        switch (kind)
        {
            case ButtonKind.Primary: fill = border = DesignTokens.Accent; text = DesignTokens.ButtonTextLight; break;
            case ButtonKind.Danger: fill = border = DesignTokens.Error; text = DesignTokens.ButtonTextLight; break;
            case ButtonKind.Quiet: fill = Color.clear; border = Color.clear; text = DesignTokens.TextSecondary; break;
            default: fill = DesignTokens.Surface; border = DesignTokens.Divider; text = DesignTokens.TextPrimary; break;
        }
        button.label.color = text;
        button.surface.color = fill; button.surface.borderColor = border; button.surface.SetVerticesDirty();
        button.layout.preferredHeight = button.layout.minHeight = height;
        float width = Mathf.Max(minWidth, button.label.GetPreferredValues(caption, Mathf.Infinity, Mathf.Infinity).x + padding);
        button.layout.minWidth = button.layout.preferredWidth = width;
    }

    // A disabled primary button must look disabled, not just ignore clicks.
    public static void SetInteractable(KitButton button, bool interactable)
    {
        button.button.interactable = interactable;
        var alpha = interactable ? 1f : 0.45f;
        button.label.alpha = alpha;
        var color = button.surface.color; color.a = button.kind == ButtonKind.Quiet ? 0f : alpha;
        button.surface.color = color; button.surface.SetVerticesDirty();
    }

    public static TMP_InputField NewInput(string name, Transform parent, TMP_FontAsset font, int fontSize, string placeholderText)
    {
        var root = new GameObject(name, typeof(RectTransform), typeof(SkillSyncRoundedGraphic), typeof(LayoutElement));
        root.transform.SetParent(parent, false);
        var surface = root.GetComponent<SkillSyncRoundedGraphic>();
        surface.color = DesignTokens.Surface; surface.borderColor = DesignTokens.Divider; surface.radius = 8f; surface.borderWidth = 1.5f;
        surface.raycastTarget = true;
        WithHeight(root.GetComponent<LayoutElement>(), 54f);

        var viewport = new GameObject("Text Area", typeof(RectTransform), typeof(RectMask2D)).GetComponent<RectTransform>();
        viewport.SetParent(root.transform, false);
        viewport.anchorMin = Vector2.zero; viewport.anchorMax = Vector2.one;
        viewport.offsetMin = new Vector2(16f, 4f); viewport.offsetMax = new Vector2(-16f, -4f);
        var placeholder = NewText("Placeholder", viewport, font, fontSize, DesignTokens.TextTertiary, FontStyles.Normal, TextAlignmentOptions.MidlineLeft, false);
        placeholder.text = placeholderText;
        var value = NewText("Text", viewport, font, fontSize, DesignTokens.TextPrimary, FontStyles.Normal, TextAlignmentOptions.MidlineLeft, false);
        foreach (var graphic in new[] { placeholder, value })
        {
            graphic.rectTransform.anchorMin = Vector2.zero; graphic.rectTransform.anchorMax = Vector2.one;
            graphic.rectTransform.offsetMin = graphic.rectTransform.offsetMax = Vector2.zero;
            graphic.overflowMode = TextOverflowModes.Overflow;
        }

        var input = root.AddComponent<TMP_InputField>();
        input.targetGraphic = surface;
        input.textViewport = viewport;
        input.textComponent = value;
        input.placeholder = placeholder;
        input.lineType = TMP_InputField.LineType.SingleLine;
        input.characterLimit = 96;
        input.caretWidth = 2;
        input.customCaretColor = true; input.caretColor = DesignTokens.TextPrimary;
        input.selectionColor = new Color(DesignTokens.Accent.r, DesignTokens.Accent.g, DesignTokens.Accent.b, 0.3f);
        return input;
    }

    // Releases the previously clicked button so a typed Enter cannot "press" it behind the new modal.
    public static void ClearSelection()
    {
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
    }
}
