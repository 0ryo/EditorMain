using UnityEngine;

internal static class ScenarioGraphMinimapLayout
{
    public const float Width = 180f;
    public const float Height = 110f;
    const float Padding = 8f;

    public struct BoundsBuilder
    {
        bool hasBounds;
        Vector2 min;
        Vector2 max;

        public void Encapsulate(Vector2 position, Rect rect)
        {
            Vector2 nodeMin = position + rect.min;
            Vector2 nodeMax = position + rect.max;
            if (!hasBounds)
            {
                min = nodeMin;
                max = nodeMax;
                hasBounds = true;
            }
            else
            {
                min = Vector2.Min(min, nodeMin);
                max = Vector2.Max(max, nodeMax);
            }
        }

        public bool TryGetBounds(out Rect bounds)
        {
            bounds = default;
            if (!hasBounds) return false;

            Vector2 paddedMin = min - Vector2.one * 120f;
            Vector2 paddedMax = max + Vector2.one * 120f;
            Vector2 center = (paddedMin + paddedMax) * 0.5f;
            Vector2 size = Vector2.Max(paddedMax - paddedMin, new Vector2(800f, 480f));
            bounds = new Rect(center - (size * 0.5f), size);
            return true;
        }
    }

    public static void GetNodeIndicatorLayout(
        Rect contentBounds,
        Vector2 nodePosition,
        Rect nodeRect,
        out Vector2 position,
        out Vector2 size)
    {
        Vector2 nodeMin = MapContentPoint(contentBounds, nodePosition + nodeRect.min);
        Vector2 nodeMax = MapContentPoint(contentBounds, nodePosition + nodeRect.max);
        position = (nodeMin + nodeMax) * 0.5f;
        size = new Vector2(
            Mathf.Max(4f, nodeMax.x - nodeMin.x),
            Mathf.Max(4f, nodeMax.y - nodeMin.y));
    }

    public static void GetViewportIndicatorLayout(
        Rect contentBounds,
        Vector2 contentPosition,
        float contentZoom,
        Vector2 viewportSize,
        out Vector2 position,
        out Vector2 size)
    {
        float zoom = Mathf.Max(0.001f, contentZoom);
        Vector2 center = -contentPosition / zoom;
        Vector2 halfSize = viewportSize / (zoom * 2f);
        Vector2 visibleMin = MapContentPoint(contentBounds, center - halfSize);
        Vector2 visibleMax = MapContentPoint(contentBounds, center + halfSize);
        float width = Mathf.Clamp(visibleMax.x - visibleMin.x, 4f, Width - (Padding * 2f));
        float height = Mathf.Clamp(visibleMax.y - visibleMin.y, 4f, Height - (Padding * 2f));
        float centerX = Mathf.Clamp((visibleMin.x + visibleMax.x) * 0.5f, Padding + (width * 0.5f), Width - Padding - (width * 0.5f));
        float centerY = Mathf.Clamp((visibleMin.y + visibleMax.y) * 0.5f, Padding + (height * 0.5f), Height - Padding - (height * 0.5f));
        position = new Vector2(centerX, centerY);
        size = new Vector2(width, height);
    }

    static Vector2 MapContentPoint(Rect contentBounds, Vector2 point)
    {
        float normalizedX = Mathf.InverseLerp(contentBounds.xMin, contentBounds.xMax, point.x);
        float normalizedY = Mathf.InverseLerp(contentBounds.yMin, contentBounds.yMax, point.y);
        return new Vector2(
            Mathf.Lerp(Padding, Width - Padding, normalizedX),
            Mathf.Lerp(Padding, Height - Padding, normalizedY));
    }
}
