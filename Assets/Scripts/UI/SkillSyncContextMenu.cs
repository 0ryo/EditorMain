using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// A right-click menu built in code. While it is open it counts as a modal (EditWorkspace.GetOpenModalRoot), so the
// camera, selection and shortcuts stay still until a choice is made or the menu is dismissed.
public sealed class SkillSyncContextMenu : MonoBehaviour
{
    public struct Item
    {
        public string label;
        public Action action;
        public bool danger;
    }

    const float Scale = SkillSyncDesignLayout.Scale, Width = 232f, ItemHeight = 38f, Pad = 6f;
    static SkillSyncContextMenu open;
    RectTransform panel;
    bool closeRequested;

    public static Transform OpenRoot => open != null && open.gameObject.activeSelf ? open.transform : null;

    public static void Close()
    {
        if (open == null) return;
        open.gameObject.SetActive(false);
        Destroy(open.gameObject);
        open = null;
    }

    public static void Show(Transform context, TMP_FontAsset font, Vector2 screenPoint, IList<Item> items)
    {
        Close();
        var canvas = context != null ? context.GetComponentInParent<Canvas>(true) : null;
        if (canvas == null || items == null || items.Count == 0) return;
        canvas = canvas.rootCanvas;
        var canvasRect = (RectTransform)canvas.transform;

        var go = new GameObject("ContextMenu", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster), typeof(Image), typeof(EditorUiInputBlocker), typeof(SkillSyncContextMenu));
        var root = (RectTransform)go.transform;
        root.SetParent(canvasRect, false);
        root.anchorMin = Vector2.zero; root.anchorMax = Vector2.one; root.offsetMin = root.offsetMax = Vector2.zero;
        var overlay = go.GetComponent<Canvas>(); overlay.overrideSorting = true; overlay.sortingOrder = 29000;
        var catcher = go.GetComponent<Image>(); catcher.color = Color.clear; catcher.raycastTarget = true;

        var menu = go.GetComponent<SkillSyncContextMenu>();
        float width = Width * Scale, itemHeight = ItemHeight * Scale, pad = Pad * Scale;
        float height = items.Count * itemHeight + pad * 2f;
        var panelObject = new GameObject("Panel", typeof(RectTransform), typeof(SkillSyncRoundedGraphic), typeof(EditorUiInputBlocker));
        menu.panel = (RectTransform)panelObject.transform;
        menu.panel.SetParent(root, false);
        menu.panel.anchorMin = menu.panel.anchorMax = new Vector2(0.5f, 0.5f);
        menu.panel.pivot = new Vector2(0, 1);
        menu.panel.sizeDelta = new Vector2(width, height);
        var surface = panelObject.GetComponent<SkillSyncRoundedGraphic>();
        surface.color = Color.white; surface.borderColor = new Color32(205, 213, 224, 255); surface.radius = 10 * Scale; surface.borderWidth = 1;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPoint, null, out var local);
        var rect = canvasRect.rect;
        float x = Mathf.Clamp(local.x, rect.xMin + 4f, rect.xMax - width - 4f);
        float y = Mathf.Clamp(local.y, rect.yMin + height + 4f, rect.yMax - 4f);
        menu.panel.anchoredPosition = new Vector2(x, y) - rect.center;

        for (int i = 0; i < items.Count; i++) menu.AddItem(items[i], font, i, itemHeight, pad);
        open = menu;
    }

    void AddItem(Item item, TMP_FontAsset font, int index, float itemHeight, float pad)
    {
        var go = new GameObject("Item", typeof(RectTransform), typeof(Image), typeof(Button), typeof(EditorUiInputBlocker));
        var rect = (RectTransform)go.transform;
        rect.SetParent(panel, false);
        rect.anchorMin = new Vector2(0, 1); rect.anchorMax = new Vector2(1, 1); rect.pivot = new Vector2(0.5f, 1);
        rect.offsetMin = new Vector2(pad, 0); rect.offsetMax = new Vector2(-pad, 0);
        rect.sizeDelta = new Vector2(rect.sizeDelta.x, itemHeight);
        rect.anchoredPosition = new Vector2(0, -(pad + index * itemHeight));
        var image = go.GetComponent<Image>(); image.color = Color.white;
        var button = go.GetComponent<Button>(); button.targetGraphic = image;
        var colors = ColorBlock.defaultColorBlock;
        colors.normalColor = Color.white; colors.highlightedColor = new Color(.9f, .94f, 1f, 1f);
        colors.pressedColor = new Color(.82f, .88f, 1f, 1f); colors.selectedColor = Color.white;
        button.colors = colors;
        var action = item.action;
        button.onClick.AddListener(() => { Close(); action?.Invoke(); });

        var labelObject = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        var labelRect = (RectTransform)labelObject.transform;
        labelRect.SetParent(rect, false);
        labelRect.anchorMin = Vector2.zero; labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(12 * Scale, 0); labelRect.offsetMax = new Vector2(-12 * Scale, 0);
        var label = labelObject.GetComponent<TextMeshProUGUI>();
        if (font != null) label.font = font;
        label.text = item.label; label.fontSize = 15 * Scale; label.alignment = TextAlignmentOptions.MidlineLeft;
        label.color = item.danger ? new Color32(168, 44, 38, 255) : new Color32(23, 34, 54, 255);
        label.textWrappingMode = TextWrappingModes.NoWrap; label.raycastTarget = false; label.richText = false;
    }

    void Update()
    {
        if (open != this) return;
        // Escape or a click elsewhere dismisses; Escape closes after this frame so no other handler also reacts to it.
        if (EditInput.CancelPressedThisFrame()) closeRequested = true;
        if (EditInput.RightPressedThisFrame() || EditInput.MiddlePressedThisFrame())
        {
            var eventSystem = EventSystem.current;
            bool inside = eventSystem != null && panel != null &&
                RectTransformUtility.RectangleContainsScreenPoint(panel, EditInput.MousePosition, null);
            if (!inside) closeRequested = true;
        }
    }

    void LateUpdate()
    {
        if (closeRequested && open == this) Close();
    }

    void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus && open == this) Close();
    }

    void OnDestroy()
    {
        if (open == this) open = null;
    }
}

// Detects a right click on the 3D screen: the right button also orbits the camera, so only a press and release
// without moving counts as a click.
public sealed class SkillSyncRightClick
{
    const float MaxMovePixels = 6f, MaxSeconds = 0.5f;
    bool tracking;
    Vector2 start;
    float startTime, farthest;

    public bool Tick(bool allowed, out Vector2 point)
    {
        point = start;
        var now = EditInput.MousePosition;
        if (!tracking)
        {
            if (!allowed || !EditInput.RightPressedThisFrame()) return false;
            tracking = true; start = now; startTime = Time.unscaledTime; farthest = 0f;
            return false;
        }
        farthest = Mathf.Max(farthest, (now - start).magnitude);
        if (EditInput.RightPressed()) return false;
        tracking = false;
        return allowed && farthest <= MaxMovePixels && Time.unscaledTime - startTime <= MaxSeconds;
    }
}

// Receives the clicks a list row's Button ignores: a double click (rename) and a right click (menu).
public sealed class SkillSyncRowEvents : MonoBehaviour, IPointerClickHandler
{
    public Action doubleClicked;
    public Action<Vector2> rightClicked;

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Right) rightClicked?.Invoke(eventData.position);
        else if (eventData.button == PointerEventData.InputButton.Left && eventData.clickCount == 2) doubleClicked?.Invoke();
    }
}

// Renames a list row in place: Enter confirms, Escape (or an unchanged name) leaves it as it was.
public sealed class SkillSyncInlineRename : MonoBehaviour
{
    public static int EscapeFrame { get; private set; } = -1;
    TMP_InputField field;
    string original;
    Action<string> commit;
    bool finished;

    public static SkillSyncInlineRename Begin(SkillSyncDesignRow row, string current, Action<string> commit)
    {
        if (row == null || row.title == null) return null;
        var existing = row.transform.Find("InlineRename");
        if (existing != null) Destroy(existing.gameObject);
        var go = new GameObject("InlineRename", typeof(RectTransform), typeof(SkillSyncRoundedGraphic), typeof(TMP_InputField), typeof(EditorUiInputBlocker), typeof(SkillSyncInlineRename));
        var rect = (RectTransform)go.transform;
        rect.SetParent(row.transform, false);
        var titleRect = row.title.rectTransform;
        rect.anchorMin = titleRect.anchorMin; rect.anchorMax = titleRect.anchorMax; rect.pivot = titleRect.pivot;
        rect.anchoredPosition = new Vector2(titleRect.anchoredPosition.x - 8f, titleRect.anchoredPosition.y);
        rect.sizeDelta = new Vector2(Mathf.Max(120f, titleRect.sizeDelta.x + 8f), 38.4f);
        var background = go.GetComponent<SkillSyncRoundedGraphic>();
        background.color = Color.white; background.borderColor = new Color32(8, 102, 232, 255); background.radius = 8; background.borderWidth = 2;

        var area = new GameObject("Text Area", typeof(RectTransform), typeof(RectMask2D));
        var areaRect = (RectTransform)area.transform;
        areaRect.SetParent(rect, false);
        areaRect.anchorMin = Vector2.zero; areaRect.anchorMax = Vector2.one;
        areaRect.offsetMin = new Vector2(8, 2); areaRect.offsetMax = new Vector2(-8, -2);
        var text = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        var textRect = (RectTransform)text.transform;
        textRect.SetParent(areaRect, false);
        textRect.anchorMin = Vector2.zero; textRect.anchorMax = Vector2.one; textRect.offsetMin = textRect.offsetMax = Vector2.zero;
        var label = text.GetComponent<TextMeshProUGUI>();
        label.font = row.title.font; label.fontSize = Mathf.Max(14f, row.title.fontSize);
        label.color = new Color32(23, 34, 54, 255); label.alignment = TextAlignmentOptions.MidlineLeft;
        label.textWrappingMode = TextWrappingModes.NoWrap; label.raycastTarget = false; label.richText = false;

        var rename = go.GetComponent<SkillSyncInlineRename>();
        rename.original = current ?? ""; rename.commit = commit;
        var input = go.GetComponent<TMP_InputField>();
        rename.field = input;
        input.textViewport = areaRect; input.textComponent = label; input.targetGraphic = background;
        input.lineType = TMP_InputField.LineType.SingleLine; input.transition = Selectable.Transition.None;
        input.text = rename.original;
        input.onEndEdit.AddListener(rename.Finish);
        EventSystem.current?.SetSelectedGameObject(go);
        input.ActivateInputField();
        input.selectionAnchorPosition = 0;
        input.selectionFocusPosition = input.text.Length;
        return rename;
    }

    void Update()
    {
        if (field != null && field.isFocused && EditInput.CancelPressedThisFrame()) EscapeFrame = Time.frameCount;
    }

    void Finish(string value)
    {
        if (finished) return;
        finished = true;
        var apply = commit;
        string name = (value ?? "").Trim();
        gameObject.SetActive(false);
        Destroy(gameObject);
        if (name.Length > 0 && name != original) apply?.Invoke(name);
    }
}
