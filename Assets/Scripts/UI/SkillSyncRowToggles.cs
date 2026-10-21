using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Eye (show / hide) and padlock (lock) buttons at the right end of a row of the placed-parts list, drawn as shapes
// so they do not depend on glyphs the font may lack.
public sealed class SkillSyncRowToggles : MonoBehaviour
{
    public const float Slot = 36f, Margin = 10f, Reserved = Margin * 2 + Slot * 2;
    public Action toggleHidden, toggleLocked;
    SkillSyncIconGraphic eye, padlock;
    SkillSyncHoverTip eyeTip, lockTip;

    public static SkillSyncRowToggles Add(SkillSyncDesignRow row, TMP_FontAsset font)
    {
        var toggles = row.gameObject.AddComponent<SkillSyncRowToggles>();
        toggles.padlock = toggles.NewButton("LockToggle", -Margin, font, out toggles.lockTip, () => toggles.toggleLocked?.Invoke());
        toggles.eye = toggles.NewButton("VisibilityToggle", -Margin - Slot, font, out toggles.eyeTip, () => toggles.toggleHidden?.Invoke());
        return toggles;
    }

    SkillSyncIconGraphic NewButton(string name, float x, TMP_FontAsset font, out SkillSyncHoverTip tip, UnityEngine.Events.UnityAction click)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(SkillSyncIconGraphic), typeof(Button), typeof(EditorUiInputBlocker), typeof(SkillSyncHoverTip));
        var rect = (RectTransform)go.transform;
        rect.SetParent(transform, false);
        rect.anchorMin = rect.anchorMax = new Vector2(1, .5f); rect.pivot = new Vector2(1, .5f);
        rect.sizeDelta = new Vector2(Slot, Slot); rect.anchoredPosition = new Vector2(x, 0);
        var icon = go.GetComponent<SkillSyncIconGraphic>();
        icon.raycastTarget = true;
        var button = go.GetComponent<Button>();
        button.targetGraphic = icon; button.transition = Selectable.Transition.None;
        button.onClick.AddListener(click);
        tip = go.GetComponent<SkillSyncHoverTip>();
        tip.font = font;
        return icon;
    }

    public void Apply(bool hidden, bool locked)
    {
        eye.Set(hidden ? SkillSyncIconGraphic.Kind.EyeOff : SkillSyncIconGraphic.Kind.Eye, hidden);
        padlock.Set(locked ? SkillSyncIconGraphic.Kind.Lock : SkillSyncIconGraphic.Kind.Unlock, locked);
        eyeTip.text = hidden ? "隠しています。クリックで3D画面に表示します" : "表示中。クリックで3D画面から隠します";
        lockTip.text = locked ? "ロック中（編集できません）。クリックで解除します" : "クリックでロックします（選べず、動かなくなります）";
    }
}

public sealed class SkillSyncIconGraphic : MaskableGraphic
{
    public enum Kind { Eye, EyeOff, Lock, Unlock }
    static readonly Color Off = new Color32(154, 166, 183, 255), On = new Color32(8, 102, 232, 255), Hidden = new Color32(168, 44, 38, 255);
    Kind kind; bool strong, initialized;

    public void Set(Kind next, bool emphasized)
    {
        if (initialized && kind == next && strong == emphasized) return;
        initialized = true;
        kind = next; strong = emphasized;
        color = !emphasized ? Off : next == Kind.EyeOff ? Hidden : On;
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        var rect = GetPixelAdjustedRect();
        float u = Mathf.Min(rect.width, rect.height) * 0.62f;
        var c = rect.center;
        // An invisible square keeps the whole slot clickable.
        var hit = new Color(color.r, color.g, color.b, 0.001f);
        Quad(vh, rect.min, new Vector2(rect.xMax, rect.yMin), rect.max, new Vector2(rect.xMin, rect.yMax), hit);
        float w = u * 0.09f;
        if (kind == Kind.Eye || kind == Kind.EyeOff)
        {
            const int n = 14;
            for (int side = -1; side <= 1; side += 2)
                for (int i = 0; i < n; i++)
                {
                    float t0 = -1f + 2f * i / n, t1 = -1f + 2f * (i + 1) / n;
                    Line(vh, c + new Vector2(t0 * u * .5f, side * u * .3f * (1 - t0 * t0)), c + new Vector2(t1 * u * .5f, side * u * .3f * (1 - t1 * t1)), w, color);
                }
            Disc(vh, c, u * .13f, color);
            if (kind == Kind.EyeOff) Line(vh, c + new Vector2(-u * .5f, -u * .4f), c + new Vector2(u * .5f, u * .4f), w * 1.2f, color);
        }
        else
        {
            var body = c + new Vector2(0, -u * .14f);
            float bw = u * .34f, bh = u * .26f;
            Quad(vh, body + new Vector2(-bw, -bh), body + new Vector2(bw, -bh), body + new Vector2(bw, bh), body + new Vector2(-bw, bh), color);
            var centre = body + new Vector2(0, bh);
            float r = u * .2f;
            int steps = 10;
            int to = kind == Kind.Lock ? steps : steps - 3;
            for (int i = 0; i < to; i++)
            {
                float a0 = Mathf.PI * i / steps, a1 = Mathf.PI * (i + 1) / steps;
                var lift = kind == Kind.Unlock ? new Vector2(u * .1f, u * .05f) : Vector2.zero;
                Line(vh, centre + lift + new Vector2(Mathf.Cos(a0), Mathf.Sin(a0)) * r, centre + lift + new Vector2(Mathf.Cos(a1), Mathf.Sin(a1)) * r, w, color);
            }
            Line(vh, centre + new Vector2(-r, 0), centre + new Vector2(-r, u * (kind == Kind.Lock ? .0f : .06f)), w, color);
            if (kind == Kind.Lock) Line(vh, centre + new Vector2(r, 0), centre + new Vector2(r, 0.001f), w, color);
        }
    }

    static void Quad(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color color)
    {
        int i = vh.currentVertCount;
        vh.AddVert(a, color, Vector2.zero); vh.AddVert(b, color, Vector2.zero); vh.AddVert(c, color, Vector2.zero); vh.AddVert(d, color, Vector2.zero);
        vh.AddTriangle(i, i + 1, i + 2); vh.AddTriangle(i, i + 2, i + 3);
    }

    static void Line(VertexHelper vh, Vector2 a, Vector2 b, float width, Color color)
    {
        var delta = b - a;
        if (delta.sqrMagnitude < 0.0001f) return;
        var side = new Vector2(-delta.y, delta.x).normalized * width * .5f;
        Quad(vh, a - side, a + side, b + side, b - side, color);
    }

    static void Disc(VertexHelper vh, Vector2 centre, float radius, Color color)
    {
        int start = vh.currentVertCount;
        vh.AddVert(centre, color, Vector2.zero);
        for (int i = 0; i < 16; i++)
        {
            float a = Mathf.PI * 2 * i / 16;
            vh.AddVert(centre + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius, color, Vector2.zero);
        }
        for (int i = 0; i < 16; i++) vh.AddTriangle(start, start + 1 + i, start + 1 + (i + 1) % 16);
    }
}

// A small caption near the pointer after it has rested on the element for a moment.
public sealed class SkillSyncHoverTip : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    const float Delay = 0.45f, Scale = SkillSyncDesignLayout.Scale;
    static GameObject shown;
    public string text;
    public TMP_FontAsset font;
    float enteredAt = -1f;

    public void OnPointerEnter(PointerEventData eventData) { enteredAt = Time.unscaledTime; }
    public void OnPointerExit(PointerEventData eventData) { enteredAt = -1f; Hide(); }
    public void OnPointerClick(PointerEventData eventData) { enteredAt = Time.unscaledTime + 0.4f; Hide(); }
    void OnDisable() { enteredAt = -1f; Hide(); }

    void Update()
    {
        if (enteredAt < 0f || shown != null || Time.unscaledTime - enteredAt < Delay || string.IsNullOrEmpty(text)) return;
        if (SkillSyncContextMenu.OpenRoot != null) return;
        Show();
    }

    void Show()
    {
        var canvas = GetComponentInParent<Canvas>();
        if (canvas == null) return;
        var canvasRect = (RectTransform)canvas.rootCanvas.transform;
        var go = new GameObject("HoverTip", typeof(RectTransform), typeof(Canvas), typeof(SkillSyncRoundedGraphic));
        var rect = (RectTransform)go.transform;
        rect.SetParent(canvasRect, false);
        var overlay = go.GetComponent<Canvas>(); overlay.overrideSorting = true; overlay.sortingOrder = 29500;
        var surface = go.GetComponent<SkillSyncRoundedGraphic>();
        surface.color = new Color32(23, 34, 54, 240); surface.borderColor = surface.color; surface.borderWidth = 0; surface.radius = 8 * Scale; surface.raycastTarget = false;
        var labelObject = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        var labelRect = (RectTransform)labelObject.transform;
        labelRect.SetParent(rect, false);
        labelRect.anchorMin = Vector2.zero; labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(10 * Scale, 0); labelRect.offsetMax = new Vector2(-10 * Scale, 0);
        var label = labelObject.GetComponent<TextMeshProUGUI>();
        if (font != null) label.font = font;
        label.text = text; label.fontSize = 13 * Scale; label.color = Color.white; label.alignment = TextAlignmentOptions.MidlineLeft;
        label.textWrappingMode = TextWrappingModes.NoWrap; label.raycastTarget = false; label.richText = false;
        float width = label.GetPreferredValues(text).x + 20 * Scale, height = 30 * Scale;
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f); rect.pivot = new Vector2(1, 1);
        rect.sizeDelta = new Vector2(width, height);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, EditInput.MousePosition, null, out var local);
        var bounds = canvasRect.rect;
        float x = Mathf.Clamp(local.x + 6f, bounds.xMin + width + 4f, bounds.xMax - 4f);
        float y = Mathf.Clamp(local.y - 22f * Scale, bounds.yMin + height + 4f, bounds.yMax - 4f);
        rect.anchoredPosition = new Vector2(x, y) - bounds.center;
        shown = go;
    }

    static void Hide()
    {
        if (shown == null) return;
        shown.SetActive(false);
        Destroy(shown);
        shown = null;
    }
}
