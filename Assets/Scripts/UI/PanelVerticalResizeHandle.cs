using UnityEngine;
using UnityEngine.EventSystems;

public class PanelVerticalResizeHandle : MonoBehaviour, IBeginDragHandler, IDragHandler
{
    public RectTransform targetPanel;
    public float minHeight = 180f;
    public float maxHeight = 720f;

    Canvas rootCanvas;

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (targetPanel == null) return;
        rootCanvas = targetPanel.GetComponentInParent<Canvas>();
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (targetPanel == null) return;

        float scale = 1f;
        if (rootCanvas != null) scale = rootCanvas.scaleFactor;
        if (scale <= 0f) scale = 1f;

        float deltaY = eventData.delta.y / scale;
        float newHeight = Mathf.Clamp(targetPanel.offsetMax.y + deltaY, minHeight, maxHeight);
        targetPanel.offsetMax = new Vector2(targetPanel.offsetMax.x, newHeight);
    }
}
