using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Drag-to-reorder for the step rows: a blue insertion line shows where the row lands, the list scrolls near its
// edges, and one drop is one move (one Undo). Rows are rebuilt by the owner, which re-attaches the handles.
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
    RectTransform content, line;
    ScrollRect scroll;
    Func<bool> canDrag;
    Action<int, int> moved;
    Action finished;
    int dragIndex = -1, insert = -1;
    Vector2 pointer;
    public bool Dragging => dragIndex >= 0;

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

    void Begin(int index, PointerEventData e)
    {
        if (canDrag != null && !canDrag()) return;
        dragIndex = index; pointer = e.position;
        if (index < rows.Count && rows[index] != null) Group(rows[index]).alpha = .45f;
        EnsureLine();
        UpdateLine();
    }

    void Drag(PointerEventData e)
    {
        if (!Dragging) return;
        pointer = e.position;
        UpdateLine();
    }

    void End()
    {
        if (!Dragging) return;
        int from = dragIndex, to = insert > from ? insert - 1 : insert;
        if (from < rows.Count && rows[from] != null) Group(rows[from]).alpha = 1f;
        dragIndex = -1; insert = -1;
        if (line != null) line.gameObject.SetActive(false);
        if (to >= 0 && to != from) moved?.Invoke(from, to);
        finished?.Invoke();
    }

    void Update()
    {
        if (!Dragging || scroll == null || scroll.viewport == null) return;
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
                UpdateLine();
            }
        }
    }

    void UpdateLine()
    {
        if (rows.Count == 0 || !RectTransformUtility.ScreenPointToLocalPointInRectangle(content, pointer, null, out var local)) return;
        insert = 0;
        for (int i = 0; i < rows.Count; i++)
            if (rows[i] != null && content.InverseTransformPoint(rows[i].TransformPoint(rows[i].rect.center)).y > local.y) insert = i + 1;
        float y;
        if (insert < rows.Count && rows[insert] != null)
            y = content.InverseTransformPoint(rows[insert].TransformPoint(new Vector3(0, rows[insert].rect.yMax, 0))).y + 2;
        else
            y = content.InverseTransformPoint(rows[rows.Count - 1].TransformPoint(new Vector3(0, rows[rows.Count - 1].rect.yMin, 0))).y - 2;
        line.gameObject.SetActive(true);
        line.SetAsLastSibling();
        line.sizeDelta = new Vector2(content.rect.width, 4);
        line.localPosition = new Vector3(content.rect.xMin, y, 0);
    }

    void EnsureLine()
    {
        if (line != null) return;
        var go = new GameObject("InsertionLine", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        go.transform.SetParent(content, false);
        go.GetComponent<LayoutElement>().ignoreLayout = true;
        var image = go.GetComponent<Image>();
        image.color = new Color32(8, 102, 232, 255); image.raycastTarget = false;
        line = (RectTransform)go.transform;
        line.anchorMin = line.anchorMax = new Vector2(0, 1); line.pivot = new Vector2(0, .5f);
    }

    static CanvasGroup Group(RectTransform row) => row.GetComponent<CanvasGroup>() != null ? row.GetComponent<CanvasGroup>() : row.gameObject.AddComponent<CanvasGroup>();
}
