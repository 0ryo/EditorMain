using UnityEngine;
using UnityEngine.EventSystems;

public class PanelVerticalResizeHandle : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerEnterHandler, IPointerExitHandler
{
    public RectTransform targetPanel;
    public float minHeight = DesignTokens.ScenarioMinHeight;
    public float maxHeight = DesignTokens.ScenarioMaxHeight;

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
        UiResizeCursor.SetVertical();
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

    public void OnEndDrag(PointerEventData eventData)
    {
        isDragging = false;
        if (!pointerInside) UiResizeCursor.Reset();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        pointerInside = true;
        UiResizeCursor.SetVertical();
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
        minHeight = DesignTokens.ScenarioMinHeight;
        maxHeight = DesignTokens.ScenarioMaxHeight;
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
        grip.sizeDelta = new Vector2(64f, 2f);

        var gripImage = grip.GetComponent<UnityEngine.UI.Image>();
        gripImage.color = DesignTokens.Divider;
        gripImage.raycastTarget = false;
    }
}

static class UiResizeCursor
{
    const int CursorSize = 24;
    static Texture2D horizontal;
    static Texture2D vertical;

    public static void SetHorizontal()
    {
        Cursor.SetCursor(GetCursor(false), new Vector2(CursorSize / 2f, CursorSize / 2f), CursorMode.Auto);
    }

    public static void SetVertical()
    {
        Cursor.SetCursor(GetCursor(true), new Vector2(CursorSize / 2f, CursorSize / 2f), CursorMode.Auto);
    }

    public static void Reset()
    {
        Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
    }

    static Texture2D GetCursor(bool isVertical)
    {
        if (isVertical && vertical != null) return vertical;
        if (!isVertical && horizontal != null) return horizontal;

        var texture = new Texture2D(CursorSize, CursorSize, TextureFormat.RGBA32, false)
        {
            name = isVertical ? "ResizeVerticalCursor_Runtime" : "ResizeHorizontalCursor_Runtime",
            hideFlags = HideFlags.HideAndDontSave,
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
        };
        texture.SetPixels(new Color[CursorSize * CursorSize]);

        Vector2Int centerA = isVertical ? new Vector2Int(12, 4) : new Vector2Int(4, 12);
        Vector2Int centerB = isVertical ? new Vector2Int(12, 19) : new Vector2Int(19, 12);
        Vector2Int sideA1 = isVertical ? new Vector2Int(7, 9) : new Vector2Int(9, 7);
        Vector2Int sideA2 = isVertical ? new Vector2Int(17, 9) : new Vector2Int(9, 17);
        Vector2Int sideB1 = isVertical ? new Vector2Int(7, 14) : new Vector2Int(14, 7);
        Vector2Int sideB2 = isVertical ? new Vector2Int(17, 14) : new Vector2Int(14, 17);

        var segments = new[]
        {
            new[] { centerA, centerB },
            new[] { centerA, sideA1 },
            new[] { centerA, sideA2 },
            new[] { centerB, sideB1 },
            new[] { centerB, sideB2 },
        };

        for (int i = 0; i < segments.Length; i++) DrawLine(texture, segments[i][0], segments[i][1], DesignTokens.TextPrimary, 1);
        for (int i = 0; i < segments.Length; i++) DrawLine(texture, segments[i][0], segments[i][1], DesignTokens.Surface, 0);
        texture.Apply(false, false);

        if (isVertical) vertical = texture;
        else horizontal = texture;
        return texture;
    }

    static void DrawLine(Texture2D texture, Vector2Int from, Vector2Int to, Color color, int radius)
    {
        int steps = Mathf.Max(Mathf.Abs(to.x - from.x), Mathf.Abs(to.y - from.y));
        for (int step = 0; step <= steps; step++)
        {
            float t = steps == 0 ? 0f : (float)step / steps;
            int x = Mathf.RoundToInt(Mathf.Lerp(from.x, to.x, t));
            int y = Mathf.RoundToInt(Mathf.Lerp(from.y, to.y, t));
            for (int offsetY = -radius; offsetY <= radius; offsetY++)
            {
                for (int offsetX = -radius; offsetX <= radius; offsetX++)
                {
                    int pixelX = x + offsetX;
                    int pixelY = y + offsetY;
                    if (pixelX < 0 || pixelX >= texture.width || pixelY < 0 || pixelY >= texture.height) continue;
                    texture.SetPixel(pixelX, pixelY, color);
                }
            }
        }
    }
}
