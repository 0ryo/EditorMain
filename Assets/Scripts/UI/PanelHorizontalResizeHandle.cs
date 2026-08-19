using UnityEngine;
using UnityEngine.EventSystems;

public class PanelHorizontalResizeHandle : MonoBehaviour, IBeginDragHandler, IDragHandler
{
    public RectTransform targetPanel;
    public float minWidth = DesignTokens.CatalogMinWidth;
    public float maxWidth = DesignTokens.CatalogMaxWidth;

    Canvas rootCanvas;

    void Awake()
    {
        ApplyDefaultLimits();
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        ApplyDefaultLimits();
    }
#endif

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (targetPanel == null) return;
        ApplyDefaultLimits();
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

    void ApplyDefaultLimits()
    {
        minWidth = DesignTokens.CatalogMinWidth;
        maxWidth = DesignTokens.CatalogMaxWidth;
    }
}
