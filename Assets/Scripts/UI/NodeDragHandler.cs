using UnityEngine;
using UnityEngine.EventSystems;

public class NodeDragHandler : MonoBehaviour, IBeginDragHandler, IDragHandler
{
    public RectTransform target;

    RectTransform dragSurface;
    Canvas rootCanvas;

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (target == null) return;

        dragSurface = target.parent as RectTransform;
        rootCanvas = target.GetComponentInParent<Canvas>();
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (target == null || dragSurface == null) return;

        float scale = 1f;
        if (rootCanvas != null) scale = rootCanvas.scaleFactor;
        if (scale <= 0f) scale = 1f;

        target.anchoredPosition += eventData.delta / scale;
    }
}
