using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class NodeDragHandler : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public RectTransform target;
    public bool clampToParentBounds = true;
    public bool blockWhenPointerStartsOnSelectable;
    public System.Action onBeginDrag;
    public System.Action onDrag;
    public System.Action onEndDrag;

    RectTransform dragSurface;
    bool draggingActive;

    public void OnBeginDrag(PointerEventData eventData)
    {
        draggingActive = false;
        if (target == null) return;
        if (blockWhenPointerStartsOnSelectable && PointerStartsOnSelectable(eventData)) return;

        dragSurface = target.parent as RectTransform;
        if (dragSurface == null) return;
        draggingActive = true;
        onBeginDrag?.Invoke();
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!draggingActive || target == null || dragSurface == null) return;

        var eventCamera = eventData.pressEventCamera != null ? eventData.pressEventCamera : eventData.enterEventCamera;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                dragSurface, eventData.position, eventCamera, out var currentLocal) ||
            !RectTransformUtility.ScreenPointToLocalPointInRectangle(
                dragSurface, eventData.position - eventData.delta, eventCamera, out var prevLocal))
        {
            return;
        }

        target.anchoredPosition += currentLocal - prevLocal;
        if (!clampToParentBounds)
        {
            onDrag?.Invoke();
            return;
        }

        target.anchoredPosition = ClampToSurface(target.anchoredPosition);
        onDrag?.Invoke();
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!draggingActive) return;
        draggingActive = false;
        onEndDrag?.Invoke();
    }

    static bool PointerStartsOnSelectable(PointerEventData eventData)
    {
        if (eventData == null) return false;

        var go = eventData.pointerPressRaycast.gameObject;
        if (go == null) go = eventData.pointerCurrentRaycast.gameObject;
        if (go == null) return false;

        return go.GetComponentInParent<Selectable>() != null;
    }

    Vector2 ClampToSurface(Vector2 anchoredPosition)
    {
        if (dragSurface == null || target == null) return anchoredPosition;

        var surfaceRect = dragSurface.rect;
        var targetRect = target.rect;

        float minX = surfaceRect.xMin + (targetRect.width * target.pivot.x);
        float maxX = surfaceRect.xMax - (targetRect.width * (1f - target.pivot.x));
        float minY = surfaceRect.yMin + (targetRect.height * target.pivot.y);
        float maxY = surfaceRect.yMax - (targetRect.height * (1f - target.pivot.y));

        if (minX > maxX)
        {
            anchoredPosition.x = surfaceRect.center.x;
        }
        else
        {
            anchoredPosition.x = Mathf.Clamp(anchoredPosition.x, minX, maxX);
        }

        if (minY > maxY)
        {
            anchoredPosition.y = surfaceRect.center.y;
        }
        else
        {
            anchoredPosition.y = Mathf.Clamp(anchoredPosition.y, minY, maxY);
        }

        return anchoredPosition;
    }
}
