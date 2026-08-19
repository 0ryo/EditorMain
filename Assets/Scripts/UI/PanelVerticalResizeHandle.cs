using UnityEngine;
using UnityEngine.EventSystems;

public class PanelVerticalResizeHandle : MonoBehaviour, IBeginDragHandler, IDragHandler
{
    public RectTransform targetPanel;
    public float minHeight = DesignTokens.ScenarioMinHeight;
    public float maxHeight = DesignTokens.ScenarioMaxHeight;

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

        float scale = 1f;
        if (rootCanvas != null) scale = rootCanvas.scaleFactor;
        if (scale <= 0f) scale = 1f;

        float deltaY = eventData.delta.y / scale;
        float newHeight = Mathf.Clamp(targetPanel.offsetMax.y + deltaY, minHeight, maxHeight);
        targetPanel.offsetMax = new Vector2(targetPanel.offsetMax.x, newHeight);
    }

    void ApplyDefaultLimits()
    {
        minHeight = DesignTokens.ScenarioMinHeight;
        maxHeight = DesignTokens.ScenarioMaxHeight;
    }
}
