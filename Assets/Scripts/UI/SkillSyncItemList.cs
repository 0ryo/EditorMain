using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

// Runtime-built, scrollable card rows for the inspector. The editor lists every condition of a step
// (selected one highlighted); the trial lists them with an optional manual-success action.
public sealed class SkillSyncItemList : MonoBehaviour
{
    public struct Item
    {
        public string id, title, detail, tag, actionLabel, badge;
        public int number;
        public bool selected, warn, done;
    }

    public const float RowHeight = 48, RowGap = 4;
    static readonly Color32 Ink = new Color32(23, 34, 54, 255), Muted = new Color32(88, 103, 124, 255),
        Line = new Color32(205, 213, 224, 255), Blue = new Color32(8, 102, 232, 255), BlueSoft = new Color32(234, 242, 255, 255),
        Warn = new Color32(180, 83, 9, 255), Ok = new Color32(21, 115, 74, 255), Badge = new Color32(238, 242, 247, 255);

    RectTransform viewport, content;
    TMP_Text fontSource;
    readonly List<GameObject> rows = new List<GameObject>();
    string signature = "", selectedId;
    List<Item> current;
    float width;
    public Action<string> Selected, ActionClicked;
    public int Count { get; private set; }
    public float ContentHeight => Count == 0 ? 0 : Count * (RowHeight + RowGap) - RowGap;

    public static SkillSyncItemList Create(Transform parent, string name, TMP_Text font)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.ScrollRect), typeof(EditorUiInputBlocker));
        go.transform.SetParent(parent, false);
        var list = go.AddComponent<SkillSyncItemList>();
        list.fontSource = font;
        var root = (RectTransform)go.transform;
        root.anchorMin = root.anchorMax = root.pivot = new Vector2(0, 1);
        go.GetComponent<UnityEngine.UI.Image>().color = Color.clear;
        var view = new GameObject("Viewport", typeof(RectTransform), typeof(UnityEngine.UI.RectMask2D));
        view.transform.SetParent(root, false);
        list.viewport = (RectTransform)view.transform;
        list.viewport.anchorMin = Vector2.zero; list.viewport.anchorMax = Vector2.one;
        list.viewport.offsetMin = list.viewport.offsetMax = Vector2.zero;
        var body = new GameObject("Content", typeof(RectTransform));
        body.transform.SetParent(list.viewport, false);
        list.content = (RectTransform)body.transform;
        list.content.anchorMin = new Vector2(0, 1); list.content.anchorMax = new Vector2(1, 1); list.content.pivot = new Vector2(.5f, 1);
        list.content.anchoredPosition = Vector2.zero;
        var scroll = go.GetComponent<UnityEngine.UI.ScrollRect>();
        scroll.viewport = list.viewport; scroll.content = list.content;
        scroll.horizontal = false; scroll.scrollSensitivity = 30;
        scroll.movementType = UnityEngine.UI.ScrollRect.MovementType.Clamped;
        return list;
    }

    // Top-left anchored in the parent, same convention as the condition editor layout.
    public void SetBounds(float x, float y, float w, float h)
    {
        var root = (RectTransform)transform;
        root.anchoredPosition = new Vector2(x, -y);
        root.sizeDelta = new Vector2(w, h);
        if (Mathf.Abs(width - w) <= .5f) return;
        width = w; signature = "";
        // Row geometry depends on the width, so shown rows are rebuilt at once.
        if (current != null) SetItems(current);
    }

    public void SetItems(IReadOnlyList<Item> items)
    {
        string next = width.ToString("0") + "|" + string.Join("¦", Array.ConvertAll(ToArray(items),
            i => string.Join("¤", i.id, i.title, i.detail, i.tag, i.actionLabel, i.badge, i.number, i.selected, i.warn, i.done)));
        Count = items.Count;
        current = new List<Item>(items);
        content.sizeDelta = new Vector2(0, ContentHeight);
        if (next == signature) return;
        signature = next;
        float offset = content.anchoredPosition.y;
        foreach (var row in rows) if (row != null) { row.name = "Retired"; row.SetActive(false); Destroy(row); }
        rows.Clear();
        string newSelected = null; int selectedIndex = -1;
        for (int i = 0; i < items.Count; i++)
        {
            Build(items[i], i);
            if (items[i].selected) { newSelected = items[i].id; selectedIndex = i; }
        }
        float max = Mathf.Max(0, ContentHeight - viewport.rect.height);
        content.anchoredPosition = new Vector2(0, Mathf.Clamp(offset, 0, max));
        // Follow the selection only when it changed, so manual scrolling is never fought.
        if (newSelected != selectedId && selectedIndex >= 0) EnsureVisible(selectedIndex);
        selectedId = newSelected;
    }

    static Item[] ToArray(IReadOnlyList<Item> items)
    {
        var array = new Item[items.Count];
        for (int i = 0; i < array.Length; i++) array[i] = items[i];
        return array;
    }

    void EnsureVisible(int index)
    {
        float top = index * (RowHeight + RowGap), bottom = top + RowHeight, height = viewport.rect.height;
        float current = content.anchoredPosition.y;
        if (top < current) current = top;
        else if (bottom > current + height) current = bottom - height;
        content.anchoredPosition = new Vector2(0, Mathf.Clamp(current, 0, Mathf.Max(0, ContentHeight - height)));
    }

    void Build(Item item, int index)
    {
        var row = new GameObject("Row_" + index, typeof(RectTransform), typeof(SkillSyncRoundedGraphic), typeof(UnityEngine.UI.Button));
        row.transform.SetParent(content, false);
        rows.Add(row);
        var rect = (RectTransform)row.transform;
        rect.anchorMin = new Vector2(0, 1); rect.anchorMax = new Vector2(1, 1); rect.pivot = new Vector2(.5f, 1);
        rect.anchoredPosition = new Vector2(0, -index * (RowHeight + RowGap));
        rect.sizeDelta = new Vector2(0, RowHeight);
        var background = row.GetComponent<SkillSyncRoundedGraphic>();
        background.radius = 8;
        background.color = item.done ? new Color32(234, 247, 240, 255) : item.selected ? BlueSoft : Color.white;
        background.borderColor = item.selected ? Blue : item.done ? Ok : Line;
        background.borderWidth = item.selected ? 2 : 1;
        var button = row.GetComponent<UnityEngine.UI.Button>();
        button.targetGraphic = background;
        var colors = UnityEngine.UI.ColorBlock.defaultColorBlock;
        colors.highlightedColor = new Color(.96f, .98f, 1f, 1f); colors.pressedColor = new Color(.88f, .92f, .98f, 1f);
        colors.selectedColor = Color.white;
        button.colors = colors;
        string id = item.id;
        button.onClick.AddListener(() => Selected?.Invoke(id));

        var badge = new GameObject("Number", typeof(RectTransform), typeof(SkillSyncRoundedGraphic));
        badge.transform.SetParent(rect, false);
        Place((RectTransform)badge.transform, 10, 12, 24, 24);
        var badgeGraphic = badge.GetComponent<SkillSyncRoundedGraphic>();
        badgeGraphic.radius = 12; badgeGraphic.borderWidth = 0; badgeGraphic.raycastTarget = false;
        badgeGraphic.color = item.done ? Ok : item.selected ? Blue : Badge;
        var number = Label(badge.transform, "Text", 0, 0, 24, 24, 13, (item.done || item.selected) ? Color.white : Ink, TextAlignmentOptions.Midline, FontWeight.Bold);
        number.text = item.done ? "✓" : item.badge ?? item.number.ToString();

        bool action = !string.IsNullOrEmpty(item.actionLabel);
        float right = action ? 134 : string.IsNullOrEmpty(item.tag) ? 10 : 72;
        var title = Label(rect, "Title", 44, 5, Mathf.Max(40, width - 44 - right), 20, 14, Ink, TextAlignmentOptions.MidlineLeft, FontWeight.Medium);
        title.text = item.title;
        var detail = Label(rect, "Detail", 44, 26, Mathf.Max(40, width - 44 - (action ? 134 : 10)), 16, 12, item.warn ? Warn : item.done ? Ok : Muted, TextAlignmentOptions.MidlineLeft, FontWeight.Regular);
        detail.text = item.detail;
        if (!string.IsNullOrEmpty(item.tag) && !action)
        {
            var tag = Label(rect, "Tag", width - 66, 6, 58, 18, 11, Blue, TextAlignmentOptions.MidlineRight, FontWeight.Bold);
            tag.text = item.tag;
        }
        if (action)
        {
            var go = new GameObject("Action", typeof(RectTransform), typeof(SkillSyncRoundedGraphic), typeof(UnityEngine.UI.Button));
            go.transform.SetParent(rect, false);
            Place((RectTransform)go.transform, width - 126, 8, 118, 32);
            var surface = go.GetComponent<SkillSyncRoundedGraphic>();
            surface.radius = 8; surface.color = Color.white; surface.borderColor = Blue; surface.borderWidth = 1;
            var actionButton = go.GetComponent<UnityEngine.UI.Button>();
            actionButton.targetGraphic = surface;
            actionButton.onClick.AddListener(() => ActionClicked?.Invoke(id));
            var caption = Label(go.transform, "Text", 0, 0, 118, 32, 12, Blue, TextAlignmentOptions.Midline, FontWeight.Bold);
            caption.text = item.actionLabel;
        }
    }

    static void Place(RectTransform rect, float x, float y, float w, float h)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = new Vector2(x, -y);
        rect.sizeDelta = new Vector2(w, h);
    }

    TMP_Text Label(Transform parent, string name, float x, float y, float w, float h, float size, Color color, TextAlignmentOptions alignment, FontWeight weight)
    {
        var text = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
        text.transform.SetParent(parent, false);
        Place(text.rectTransform, x, y, w, h);
        text.font = fontSource.font;
        text.fontSharedMaterial = fontSource.fontSharedMaterial;
        text.fontSize = size;
        text.fontWeight = weight;
        text.color = color;
        text.alignment = alignment;
        text.richText = false;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Ellipsis;
        // Shrink a little before cutting a long part name off.
        text.enableAutoSizing = size >= 12; text.fontSizeMax = size; text.fontSizeMin = Mathf.Min(size, 11);
        text.raycastTarget = false;
        return text;
    }
}
