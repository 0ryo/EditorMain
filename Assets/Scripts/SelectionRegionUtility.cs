using UnityEngine;
using UnityEngine.UI;

internal static class SelectionRegionUtility
{
    public static Rect FromPoints(Vector2 a, Vector2 b) => Rect.MinMaxRect(
        Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));

    public static bool TryGetBounds(PlacedObject item, out Bounds bounds)
    {
        bounds = new Bounds(item.transform.position, Vector3.zero);
        bool found = false;
        foreach (var renderer in item.GetComponentsInChildren<Renderer>(false))
        {
            if (!renderer.enabled) continue;
            if (!found) bounds = renderer.bounds;
            else bounds.Encapsulate(renderer.bounds);
            found = true;
        }
        return found;
    }

    public static bool Overlaps(Camera camera, Rect region, PlacedObject item)
    {
        if (camera == null || item == null) return false;
        bool hasRenderer = false;
        foreach (var renderer in item.GetComponentsInChildren<Renderer>(false))
        {
            if (!renderer.enabled) continue;
            hasRenderer = true;
            if (OverlapsBounds(camera, region, renderer.bounds)) return true;
        }
        return !hasRenderer && OverlapsBounds(camera, region, new Bounds(item.transform.position, Vector3.zero));
    }

    static bool OverlapsBounds(Camera camera, Rect region, Bounds bounds)
    {
        var corners = new Vector3[8];
        var depths = new float[8];
        Vector2 min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
        Vector2 max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
        bool projected = false;
        void Include(Vector3 world)
        {
            Vector3 screen = camera.WorldToScreenPoint(world);
            min = Vector2.Min(min, screen);
            max = Vector2.Max(max, screen);
            projected = true;
        }
        for (int i = 0; i < 8; i++)
        {
            corners[i] = bounds.center + Vector3.Scale(bounds.extents, new Vector3(
                (i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
            depths[i] = Vector3.Dot(corners[i] - camera.transform.position, camera.transform.forward);
            if (depths[i] >= camera.nearClipPlane && depths[i] <= camera.farClipPlane) Include(corners[i]);
        }
        // Clip box edges at both depth planes so partially visible objects still participate.
        for (int i = 0; i < 8; i++)
        for (int axis = 1; axis <= 4; axis <<= 1)
        {
            int j = i ^ axis;
            if (j <= i || Mathf.Approximately(depths[i], depths[j])) continue;
            Clip(camera.nearClipPlane);
            Clip(camera.farClipPlane);
            void Clip(float depth)
            {
                float t = (depth - depths[i]) / (depths[j] - depths[i]);
                if (t >= 0f && t <= 1f) Include(Vector3.Lerp(corners[i], corners[j], t));
            }
        }
        return projected && OverlapsScreenRect(region, Rect.MinMaxRect(min.x, min.y, max.x, max.y));
    }

    internal static bool OverlapsScreenRect(Rect a, Rect b) =>
        a.xMin <= b.xMax && a.xMax >= b.xMin && a.yMin <= b.yMax && a.yMax >= b.yMin;
}
// Transient gesture feedback in screen pixels, never a persisted editor panel.
internal sealed class SelectionRegionOverlay
{
    GameObject root;
    RectTransform fill;
    Texture2D horizontalPattern;
    Texture2D verticalPattern;
    readonly RawImage[] edges = new RawImage[4];

    public void Show(Rect region)
    {
        if (root == null)
        {
            root = new GameObject("SelectionRegion_Runtime", typeof(Canvas));
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var go = new GameObject("Region", typeof(RectTransform));
            fill = go.GetComponent<RectTransform>();
            fill.SetParent(root.transform, false);
            fill.anchorMin = fill.anchorMax = fill.pivot = Vector2.zero;
            horizontalPattern = CreatePattern(true);
            verticalPattern = CreatePattern(false);
            edges[0] = AddEdge("Top", new Vector2(0f, 1f), Vector2.one, new Vector2(0f, -2f), Vector2.zero, horizontalPattern);
            edges[1] = AddEdge("Bottom", Vector2.zero, new Vector2(1f, 0f), Vector2.zero, new Vector2(0f, 2f), horizontalPattern);
            edges[2] = AddEdge("Left", Vector2.zero, new Vector2(0f, 1f), Vector2.zero, new Vector2(2f, 0f), verticalPattern);
            edges[3] = AddEdge("Right", new Vector2(1f, 0f), Vector2.one, new Vector2(-2f, 0f), Vector2.zero, verticalPattern);
        }
        root.SetActive(true);
        fill.anchoredPosition = region.min;
        fill.sizeDelta = region.size;
        // Keep 2px dots and 2px gaps constant as the selection rectangle grows.
        edges[0].uvRect = edges[1].uvRect = new Rect(0f, 0f, region.width / 4f, 1f);
        edges[2].uvRect = edges[3].uvRect = new Rect(0f, 0f, 1f, region.height / 4f);
    }

    static Texture2D CreatePattern(bool horizontal)
    {
        var texture = new Texture2D(horizontal ? 4 : 1, horizontal ? 1 : 4, TextureFormat.RGBA32, false);
        texture.hideFlags = HideFlags.HideAndDontSave;
        texture.wrapMode = TextureWrapMode.Repeat;
        texture.filterMode = FilterMode.Point;
        texture.SetPixels(new[] { Color.white, Color.white, Color.clear, Color.clear });
        texture.Apply(false, true);
        return texture;
    }

    RawImage AddEdge(string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax, Texture2D pattern)
    {
        var edge = new GameObject(name, typeof(RectTransform), typeof(RawImage));
        var rect = edge.GetComponent<RectTransform>();
        rect.SetParent(fill, false);
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
        var image = edge.GetComponent<RawImage>();
        image.texture = pattern;
        image.color = DesignTokens.Accent;
        image.raycastTarget = false;
        return image;
    }
    public void Hide() { if (root != null) root.SetActive(false); }
    public void Dispose()
    {
        if (root != null) Object.Destroy(root);
        if (horizontalPattern != null) Object.Destroy(horizontalPattern);
        if (verticalPattern != null) Object.Destroy(verticalPattern);
    }
}
