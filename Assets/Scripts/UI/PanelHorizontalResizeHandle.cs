using UnityEngine;
using UnityEngine.EventSystems;

public class PanelHorizontalResizeHandle : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerEnterHandler, IPointerExitHandler
{
    public RectTransform targetPanel;
    public float minWidth = DesignTokens.CatalogMinWidth;
    public float maxWidth = DesignTokens.CatalogMaxWidth;

    Canvas rootCanvas;
    bool pointerInside;
    bool isDragging;

    void Awake()
    {
        ApplyDefaultLimits();
        EnsureAffordance();
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
        isDragging = true;
        UiResizeCursor.SetHorizontal();
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (targetPanel == null) return;

        float scale = rootCanvas != null && rootCanvas.scaleFactor > 0f ? rootCanvas.scaleFactor : 1f;
        float deltaX = eventData.delta.x / scale;
        float newWidth = Mathf.Clamp(targetPanel.offsetMax.x + deltaX, minWidth, maxWidth);
        targetPanel.offsetMax = new Vector2(newWidth, targetPanel.offsetMax.y);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        isDragging = false;
        if (!pointerInside) UiResizeCursor.Reset();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        pointerInside = true;
        UiResizeCursor.SetHorizontal();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        pointerInside = false;
        if (!isDragging) UiResizeCursor.Reset();
    }

    void OnDisable()
    {
        pointerInside = false;
        isDragging = false;
        UiResizeCursor.Reset();
    }

    void ApplyDefaultLimits()
    {
        minWidth = DesignTokens.CatalogMinWidth;
        maxWidth = DesignTokens.CatalogMaxWidth;
    }

    void EnsureAffordance()
    {
        var root = transform as RectTransform;
        if (root == null) return;

        var image = GetComponent<UnityEngine.UI.Image>();
        if (image != null) image.color = DesignTokens.BgPrimary;

        var grip = root.Find("Grip") as RectTransform;
        if (grip == null)
        {
            var go = new GameObject("Grip", typeof(RectTransform), typeof(UnityEngine.UI.Image));
            grip = go.GetComponent<RectTransform>();
            grip.SetParent(root, false);
        }

        grip.anchorMin = new Vector2(0.5f, 0.5f);
        grip.anchorMax = new Vector2(0.5f, 0.5f);
        grip.pivot = new Vector2(0.5f, 0.5f);
        grip.anchoredPosition = Vector2.zero;
        grip.sizeDelta = new Vector2(2f, 64f);

        var gripImage = grip.GetComponent<UnityEngine.UI.Image>();
        gripImage.color = DesignTokens.Divider;
        gripImage.raycastTarget = false;
    }
}
