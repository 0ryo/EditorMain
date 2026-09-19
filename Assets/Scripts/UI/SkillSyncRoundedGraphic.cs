using UnityEngine;

// Geometry-backed background: radius and border remain editable in the Inspector.
[RequireComponent(typeof(CanvasRenderer))]
public sealed class SkillSyncRoundedGraphic : UnityEngine.UI.MaskableGraphic
{
    public float radius = 10;
    public float borderWidth = 1;
    public Color borderColor = new Color32(205, 213, 224, 255);
    protected override void OnPopulateMesh(UnityEngine.UI.VertexHelper vh)
    {
        vh.Clear();
        var rect = GetPixelAdjustedRect();
        Fan(vh, rect, Mathf.Min(radius, Mathf.Min(rect.width, rect.height) / 2), borderColor);
        rect.xMin += borderWidth; rect.xMax -= borderWidth;
        rect.yMin += borderWidth; rect.yMax -= borderWidth;
        Fan(vh, rect, Mathf.Max(0, radius - borderWidth), color);
    }
    static void Fan(UnityEngine.UI.VertexHelper vh, Rect rect, float r, Color tint)
    {
        int start = vh.currentVertCount;
        vh.AddVert(rect.center, tint, Vector2.zero);
        for (int corner = 0; corner < 4; corner++)
        {
            Vector2 center = new Vector2(corner < 2 ? rect.xMax - r : rect.xMin + r,
                corner == 0 || corner == 3 ? rect.yMax - r : rect.yMin + r);
            for (int segment = 0; segment <= 12; segment++)
            {
                float a = (90 - corner * 90 - segment * 90f / 12) * Mathf.Deg2Rad;
                vh.AddVert(center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r, tint, Vector2.zero);
            }
        }
        int count = 52;
        for (int i = 0; i < count; i++) vh.AddTriangle(start, start + 1 + i, start + 1 + (i + 1) % count);
    }
}
