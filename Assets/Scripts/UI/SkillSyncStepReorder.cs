using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Drag-to-reorder for the step rows: the card follows the pointer, an empty slot shows where it lands, the list
// scrolls near its edges, Esc cancels, and one drop is one move (one Undo). Rows are rebuilt by the owner, which re-attaches the handles.
public sealed class SkillSyncStepReorder : MonoBehaviour
{
    sealed class Handle : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public SkillSyncStepReorder owner;
        public int index;
        public void OnBeginDrag(PointerEventData e) => owner.Begin(index, e);
        public void OnDrag(PointerEventData e) => owner.Drag(e);
        public void OnEndDrag(PointerEventData e) => owner.End();
    }

    const float EdgeZone = 40, ScrollSpeed = 520;
    readonly List<RectTransform> rows = new List<RectTransform>();
    RectTransform content, gap, dragged;
    ScrollRect scroll;
    Func<bool> canDrag;
    Action<int, int> moved;
    Action finished;
    int dragIndex = -1, insert = -1, siblingIndex;
    float grab;
    Vector2 pointer;
    public bool Dragging => dragIndex >= 0;
    // The frame Esc cancelled a drag, so the same Esc does not also clear the selection.
    public static int CancelFrame { get; private set; } = -1;

    public void Setup(RectTransform stepsContent, Func<bool> canDrag, Action<int, int> onMoved, Action onFinished)
    {
        content = stepsContent; this.canDrag = canDrag; moved = onMoved; finished = onFinished;
        scroll = content.GetComponentInParent<ScrollRect>();
    }

    public void Reset() => rows.Clear();

    public void Attach(RectTransform row, int index)
    {
        rows.Add(row);
        var handle = row.gameObject.AddComponent<Handle>();
        handle.owner = this; handle.index = index;
    }

    // The card lifts out of the list and follows the pointer; an empty slot opens where it would land,
    // and the other cards move to make room. Esc puts everything back.
    void Begin(int index, PointerEventData e)
    {
        if (canDrag != null && !canDrag()) return;
        if (index >= rows.Count || rows[index] == null) return;
        dragIndex = index; pointer = e.position; insert = index;
        dragged = rows[index];
        siblingIndex = dragged.GetSiblingIndex();
        EnsureGap(dragged.rect.height);
        gap.SetSiblingIndex(siblingIndex);
        Layout(dragged).ignoreLayout = true;
        dragged.SetAsLastSibling();
        Group(dragged).alpha = .92f;
        Group(dragged).blocksRaycasts = false;
        grab = RectTransformUtility.ScreenPointToLocalPointInRectangle(content, pointer, null, out var local) ? dragged.localPosition.y - local.y : 0;
        Follow();
    }

    void Drag(PointerEventData e)
    {
        if (!Dragging) return;
        pointer = e.position;
        Follow();
    }

    void End()
    {
        if (!Dragging) return;
        int from = dragIndex, to = insert;
        Restore();
        if (to >= 0 && to != from) moved?.Invoke(from, to);
        finished?.Invoke();
    }

    void Cancel()
    {
        if (!Dragging) return;
        CancelFrame = Time.frameCount;
        Restore();
        finished?.Invoke();
    }

    void Restore()
    {
        if (dragged != null)
        {
            Layout(dragged).ignoreLayout = false;
            dragged.SetSiblingIndex(siblingIndex);
            Group(dragged).alpha = 1f;
            Group(dragged).blocksRaycasts = true;
        }
        if (gap != null) gap.gameObject.SetActive(false);
        dragIndex = -1; insert = -1; dragged = null;
        LayoutRebuilder.ForceRebuildLayoutImmediate(content);
    }

    void Update()
    {
        if (!Dragging) return;
        if (EditInput.CancelPressedThisFrame()) { Cancel(); return; }
        if (scroll == null || scroll.viewport == null) return;
        // Keep scrolling while the pointer rests in the top or bottom edge zone.
        var viewport = scroll.viewport;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(viewport, pointer, null, out var local))
        {
            float top = viewport.rect.yMax - local.y, bottom = local.y - viewport.rect.yMin;
            float direction = top < EdgeZone ? 1 : bottom < EdgeZone ? -1 : 0;
            float scrollable = content.rect.height - viewport.rect.height;
            if (direction != 0 && scrollable > 0)
            {
                float step = direction * ScrollSpeed * Time.unscaledDeltaTime / scrollable;
                scroll.verticalNormalizedPosition = Mathf.Clamp01(scroll.verticalNormalizedPosition + step);
                Follow();
            }
        }
    }

    void Follow()
    {
        if (dragged == null || !RectTransformUtility.ScreenPointToLocalPointInRectangle(content, pointer, null, out var local)) return;
        dragged.localPosition = new Vector3(dragged.localPosition.x, local.y + grab, dragged.localPosition.z);
        // The landing slot is counted among the other cards only, so it never flickers as they move.
        var others = new List<RectTransform>();
        for (int i = 0; i < rows.Count; i++) if (i != dragIndex && rows[i] != null) others.Add(rows[i]);
        float center = dragged.localPosition.y + (0.5f - dragged.pivot.y) * dragged.rect.height;
        int slot = 0;
        foreach (var other in others)
            if (other.localPosition.y + (0.5f - other.pivot.y) * other.rect.height > center) slot++;
        if (slot == insert) return;
        insert = slot;
        gap.SetAsLastSibling();
        if (slot < others.Count) gap.SetSiblingIndex(others[slot].GetSiblingIndex());
        dragged.SetAsLastSibling();
        LayoutRebuilder.ForceRebuildLayoutImmediate(content);
    }

    void EnsureGap(float height)
    {
        if (gap == null)
        {
            var go = new GameObject("DropSlot", typeof(RectTransform), typeof(CanvasRenderer), typeof(SkillSyncRoundedGraphic), typeof(LayoutElement));
            go.transform.SetParent(content, false);
            var surface = go.GetComponent<SkillSyncRoundedGraphic>();
            surface.color = new Color32(234, 242, 255, 255); surface.borderColor = new Color32(8, 102, 232, 255);
            surface.borderWidth = 1.5f; surface.radius = 10; surface.raycastTarget = false;
            gap = (RectTransform)go.transform;
        }
        gap.GetComponent<LayoutElement>().preferredHeight = height;
        gap.gameObject.SetActive(true);
    }

    static LayoutElement Layout(RectTransform row) => row.GetComponent<LayoutElement>() != null ? row.GetComponent<LayoutElement>() : row.gameObject.AddComponent<LayoutElement>();

    static CanvasGroup Group(RectTransform row) => row.GetComponent<CanvasGroup>() != null ? row.GetComponent<CanvasGroup>() : row.gameObject.AddComponent<CanvasGroup>();
}
