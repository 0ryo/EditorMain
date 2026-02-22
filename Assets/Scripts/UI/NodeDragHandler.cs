using UnityEngine;
using UnityEngine.EventSystems;

public class NodeDragHandler : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public RectTransform target;
    public bool clampToParentBounds = true;
    public System.Action onBeginDrag;
    public System.Action onDrag;
    public System.Action onEndDrag;

    RectTransform dragSurface;

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (target == null) return;

        dragSurface = target.parent as RectTransform;
        onBeginDrag?.Invoke();
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (target == null || dragSurface == null) return;

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
        onEndDrag?.Invoke();
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
