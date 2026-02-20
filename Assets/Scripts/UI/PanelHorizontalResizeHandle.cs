using UnityEngine;
using UnityEngine.EventSystems;

public class PanelHorizontalResizeHandle : MonoBehaviour, IBeginDragHandler, IDragHandler
{
    public RectTransform targetPanel;
    public float minWidth = 220f;
    public float maxWidth = 720f;

    Canvas rootCanvas;

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (targetPanel == null) return;
        rootCanvas = targetPanel.GetComponentInParent<Canvas>();
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (targetPanel == null) return;

        float scale = rootCanvas != null && rootCanvas.scaleFactor > 0f ? rootCanvas.scaleFactor : 1f;
        float deltaX = eventData.delta.x / scale;
        float newWidth = Mathf.Clamp(targetPanel.offsetMax.x + deltaX, minWidth, maxWidth);
        targetPanel.offsetMax = new Vector2(newWidth, targetPanel.offsetMax.y);
    }
}
