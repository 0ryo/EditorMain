using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// The step editor's right panel scrolls as one page when its content is taller than the panel (a step with many
// completion conditions): the step's title and text, the condition editor, the list of conditions and the buttons.
// The parts shown only in the step editor states are moved into a content rect with the same size and coordinates
// as the inspector (InspectorSlide), so every existing layout keeps placing them as before. The clipping rect itself
// covers just the panel (RectMask2D padding is measured in canvas units, not in the panel's, so it is not used);
// the content sits inside it offset back to the inspector's origin and moves by the scroll amount.
// Placement and trial parts stay where they are; in those states nothing is clipped.
public sealed class SkillSyncInspectorScroll : MonoBehaviour
{
    public const string ContentName = "StepInspectorContent";
    const int StepStates = (1 << 1) | (1 << 5);
    const float Top = 88, Padding = 16, Sensitivity = 48, ThumbWidth = 4;

    public RectTransform content;
    SkillSyncDesignView view;
    RectMask2D mask;
    RectTransform thumb, inspector;
    float offset, contentBottom;
    int lastState = -1;
    GameObject lastSelected;

    public static SkillSyncInspectorScroll Find(SkillSyncDesignView view) =>
        view != null ? view.GetComponentInChildren<SkillSyncInspectorScroll>(true) : null;

    // Moves the step editor's parts of the inspector into the scrolling content (once; later calls return it).
    public static SkillSyncInspectorScroll Ensure(SkillSyncDesignView view, RectTransform inspector, IEnumerable<Transform> extra)
    {
        if (view == null || inspector == null) return null;
        var existing = inspector.GetComponentInChildren<SkillSyncInspectorScroll>(true);
        if (existing != null) return existing;
        var go = new GameObject("StepInspectorScroll", typeof(RectTransform), typeof(RectMask2D), typeof(SkillSyncInspectorScroll));
        var rect = (RectTransform)go.transform;
        rect.SetParent(inspector, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
        var scroll = go.GetComponent<SkillSyncInspectorScroll>();
        scroll.view = view;
        scroll.inspector = inspector;
        scroll.mask = go.GetComponent<RectMask2D>();
        scroll.content = new GameObject(ContentName, typeof(RectTransform)).GetComponent<RectTransform>();
        scroll.content.SetParent(rect, false);
        scroll.content.anchorMin = scroll.content.anchorMax = scroll.content.pivot = new Vector2(0, 1);
        scroll.Fit();
        var moving = new List<Transform>();
        void Take(Transform t, int stateMask)
        {
            if (t == null || t.parent != inspector || stateMask == 0 || (stateMask & ~StepStates) != 0 || moving.Contains(t)) return;
            moving.Add(t);
        }
        foreach (var v in view.visuals) if (v?.target != null) Take(v.target.transform, v.mask);
        foreach (var c in view.controls) if (c?.button != null) Take(c.button.transform, c.mask);
        foreach (var f in view.fields) if (f?.input != null) Take(f.input.transform, f.mask);
        if (extra != null) foreach (var t in extra) Take(t, StepStates);
        // Keep their drawing order: the content takes the place of the first of them.
        moving.Sort((a, b) => a.GetSiblingIndex().CompareTo(b.GetSiblingIndex()));
        if (moving.Count > 0) rect.SetSiblingIndex(moving[0].GetSiblingIndex());
        foreach (var t in moving) t.SetParent(scroll.content, false);
        scroll.thumb = new GameObject("ScrollThumb", typeof(RectTransform), typeof(SkillSyncRoundedGraphic)).GetComponent<RectTransform>();
        scroll.thumb.SetParent(rect, false);
        var graphic = scroll.thumb.GetComponent<SkillSyncRoundedGraphic>();
        graphic.color = new Color32(168, 178, 192, 200); graphic.radius = ThumbWidth / 2; graphic.raycastTarget = false;
        scroll.thumb.anchorMin = scroll.thumb.anchorMax = scroll.thumb.pivot = new Vector2(1, 1);
        scroll.thumb.gameObject.SetActive(false);
        scroll.Apply();
        return scroll;
    }

    // The clip rect is the panel (from its left edge and below the header); the content is the inspector's size,
    // placed so its coordinates are the inspector's, then raised by the scroll amount.
    void Fit()
    {
        var clip = (RectTransform)transform;
        var size = inspector.rect.size;
        clip.anchoredPosition = new Vector2(PanelLeft, -Top);
        clip.sizeDelta = new Vector2(Mathf.Max(0, size.x - PanelLeft), Mathf.Max(0, size.y - Top));
        if (content.sizeDelta != size) content.sizeDelta = size;
    }

    bool StepState => view != null && (view.State == 1 || view.State == 5);
    float PanelLeft => view != null && view.wideLayout ? SkillSyncDesignLayout.MapX(1160) : 1160;
    float Height => inspector != null ? inspector.rect.height : 0;
    float MaxOffset => Mathf.Max(0, contentBottom + Padding - Height);

    public void ScrollToTop() { offset = 0; Apply(); }

    void Update()
    {
        if (!StepState || MaxOffset <= 0) return;
        if (!PointerInPanel() || Blocked()) return;
        float wheel = EditInput.ScrollDelta.y;
        if (Mathf.Abs(wheel) < .0001f) return;
        offset = Mathf.Clamp(offset - wheel * Sensitivity, 0, MaxOffset);
        Apply();
    }

    void LateUpdate()
    {
        bool step = StepState;
        if (step && lastState != view.State && lastState != 1 && lastState != 5) offset = 0;
        lastState = view != null ? view.State : -1;
        contentBottom = step ? MeasureBottom() : 0;
        if (step) FollowSelection();
        offset = Mathf.Clamp(offset, 0, MaxOffset);
        Apply();
    }

    void Apply()
    {
        bool step = StepState;
        float shown = step ? offset : 0;
        if (inspector == null || content == null) return;
        Fit();
        var position = new Vector2(-PanelLeft, Top + shown);
        if ((content.anchoredPosition - position).sqrMagnitude > .0001f) content.anchoredPosition = position;
        if (mask != null && mask.enabled != step) mask.enabled = step;
        // The design view brings each state's parts to the front when it switches state; the step parts now live in
        // this container, so the container itself goes to the front of the inspector (other parts there are
        // hidden in the step states, but some stay drawn over it otherwise).
        if (step && transform.GetSiblingIndex() != transform.parent.childCount - 1) transform.SetAsLastSibling();
        if (thumb == null) return;
        float max = MaxOffset, visible = Height - Top;
        bool overflow = step && max > 0 && visible > 0;
        if (thumb.gameObject.activeSelf != overflow) thumb.gameObject.SetActive(overflow);
        if (!overflow) return;
        float track = visible - 8, length = Mathf.Max(24, track * visible / (visible + max));
        thumb.sizeDelta = new Vector2(ThumbWidth, length);
        thumb.anchoredPosition = new Vector2(-4, -(4 + (track - length) * offset / max));
        thumb.SetAsLastSibling();
    }

    // The lowest active part, in inspector coordinates measured from the top.
    float MeasureBottom()
    {
        float bottom = 0;
        var corners = new Vector3[4];
        foreach (Transform child in content)
        {
            if (!child.gameObject.activeSelf || !(child is RectTransform rect)) continue;
            rect.GetWorldCorners(corners);
            bottom = Mathf.Max(bottom, -content.InverseTransformPoint(corners[0]).y);
        }
        return bottom;
    }

    bool PointerInPanel()
    {
        var canvas = GetComponentInParent<Canvas>();
        var camera = canvas != null && canvas.rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.rootCanvas.worldCamera : null;
        return RectTransformUtility.RectangleContainsScreenPoint((RectTransform)transform, EditInput.MousePosition, camera);
    }

    // An open list (the condition type, the target picker) or a dialog keeps the wheel for itself.
    bool Blocked()
    {
        if (EditWorkspace.HasOpenModal) return true;
        if (view.conditionTypeDropdown != null && view.conditionTypeDropdown.IsExpanded) return true;
        var picker = view.GetComponentInParent<SkillSyncObjectPicker>();
        if (picker == null) picker = view.transform.root.GetComponentInChildren<SkillSyncObjectPicker>(true);
        return picker != null && picker.IsOpen;
    }

    // Moving to a field below the visible part (Tab) brings it into view.
    void FollowSelection()
    {
        var selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        if (selected == lastSelected) return;
        lastSelected = selected;
        if (selected == null || !selected.transform.IsChildOf(content) || !(selected.transform is RectTransform rect)) return;
        var corners = new Vector3[4];
        rect.GetWorldCorners(corners);
        float top = -content.InverseTransformPoint(corners[1]).y, bottom = -content.InverseTransformPoint(corners[0]).y;
        if (top - offset < Top + 8) offset = top - Top - 8;
        else if (bottom - offset > Height - Padding) offset = bottom - Height + Padding;
    }
}
