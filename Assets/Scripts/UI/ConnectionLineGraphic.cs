using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasRenderer))]
public class ConnectionLineGraphic : Graphic
{
    public RectTransform from;
    public RectTransform to;
    public float thickness = 3f;

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        if (from == null || to == null) return;

        Vector2 fromPoint = WorldToLocalCenter(from);
        Vector2 toPoint = WorldToLocalCenter(to);

        Vector2 direction = (toPoint - fromPoint).normalized;
        Vector2 normal = new Vector2(-direction.y, direction.x) * (thickness * 0.5f);

        AddQuad(vh, fromPoint - normal, fromPoint + normal, toPoint + normal, toPoint - normal);
    }

    Vector2 WorldToLocalCenter(RectTransform target)
    {
        Vector3 worldCenter = target.TransformPoint(target.rect.center);
        return rectTransform.InverseTransformPoint(worldCenter);
    }

    void AddQuad(VertexHelper vh, Vector2 v0, Vector2 v1, Vector2 v2, Vector2 v3)
    {
        int start = vh.currentVertCount;
        UIVertex vertex = UIVertex.simpleVert;
        vertex.color = color;

        vertex.position = v0;
        vh.AddVert(vertex);

        vertex.position = v1;
        vh.AddVert(vertex);

        vertex.position = v2;
        vh.AddVert(vertex);

        vertex.position = v3;
        vh.AddVert(vertex);

        vh.AddTriangle(start, start + 1, start + 2);
        vh.AddTriangle(start, start + 2, start + 3);
    }

    void Update()
    {
        SetVerticesDirty();
    }
}
