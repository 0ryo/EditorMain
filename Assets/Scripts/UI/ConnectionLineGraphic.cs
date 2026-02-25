using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasRenderer))]
public class ConnectionLineGraphic : MaskableGraphic, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    const float ClickPadding = 6f;
    const float HoverLabelPadding = 12f;

    public RectTransform from;
    public RectTransform to;
    public float thickness = 3f;
    public string fromNodeId;
    public string toNodeId;
    public ScenarioEdgeType edgeType;
    public RectTransform[] raycastBlockers;

    public Action<ConnectionLineGraphic> onClickLine;

    Text hoverDeleteLabel;
    bool isPointerOver;

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

    public override bool Raycast(Vector2 sp, Camera eventCamera)
    {
        if (!raycastTarget || from == null || to == null) return false;

        if (raycastBlockers != null)
        {
            for (int i = 0; i < raycastBlockers.Length; i++)
            {
                var blocker = raycastBlockers[i];
                if (blocker == null || !blocker.gameObject.activeInHierarchy) continue;
                if (RectTransformUtility.RectangleContainsScreenPoint(blocker, sp, eventCamera)) return false;
            }
        }

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, sp, eventCamera, out var localPoint))
        {
            return false;
        }

        Vector2 fromPoint = WorldToLocalCenter(from);
        Vector2 toPoint = WorldToLocalCenter(to);
        float distance = DistancePointToSegment(localPoint, fromPoint, toPoint);
        float hitRange = (thickness * 0.5f) + ClickPadding;
        return distance <= hitRange;
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
        UpdateHoverDeleteLabelPosition();
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        isPointerOver = false;
        if (hoverDeleteLabel != null)
        {
            hoverDeleteLabel.gameObject.SetActive(false);
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!raycastTarget) return;

        isPointerOver = true;
        EnsureHoverDeleteLabel();
        if (hoverDeleteLabel != null)
        {
            hoverDeleteLabel.gameObject.SetActive(true);
            hoverDeleteLabel.transform.SetAsLastSibling();
            UpdateHoverDeleteLabelPosition();
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        isPointerOver = false;
        if (hoverDeleteLabel != null)
        {
            hoverDeleteLabel.gameObject.SetActive(false);
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (!raycastTarget) return;
        if (eventData.button != PointerEventData.InputButton.Left) return;

        onClickLine?.Invoke(this);
        eventData.Use();
    }

    void EnsureHoverDeleteLabel()
    {
        if (hoverDeleteLabel != null) return;

        var labelGo = new GameObject("Text_DeleteHint", typeof(RectTransform), typeof(Text));
        var labelRt = labelGo.GetComponent<RectTransform>();
        labelRt.SetParent(rectTransform, false);
        labelRt.anchorMin = new Vector2(0.5f, 0.5f);
        labelRt.anchorMax = new Vector2(0.5f, 0.5f);
        labelRt.pivot = new Vector2(0.5f, 0.5f);
        labelRt.sizeDelta = new Vector2(52f, 22f);
        labelRt.anchoredPosition = Vector2.zero;

        hoverDeleteLabel = labelGo.GetComponent<Text>();
        hoverDeleteLabel.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        hoverDeleteLabel.text = "\u524A\u9664";
        hoverDeleteLabel.fontSize = 12;
        hoverDeleteLabel.alignment = TextAnchor.MiddleCenter;
        hoverDeleteLabel.color = new Color(0.80f, 0.12f, 0.12f, 1f);
        hoverDeleteLabel.raycastTarget = false;
        hoverDeleteLabel.gameObject.SetActive(false);
    }

    void UpdateHoverDeleteLabelPosition()
    {
        if (!isPointerOver || hoverDeleteLabel == null || from == null || to == null) return;

        Vector2 fromPoint = WorldToLocalCenter(from);
        Vector2 toPoint = WorldToLocalCenter(to);
        Vector2 direction = toPoint - fromPoint;
        Vector2 normal = direction.sqrMagnitude > 0.0001f
            ? new Vector2(-direction.y, direction.x).normalized
            : Vector2.up;
        if (normal.y < 0f) normal = -normal;

        var labelRt = hoverDeleteLabel.rectTransform;
        labelRt.anchoredPosition = ((fromPoint + toPoint) * 0.5f) + (normal * (thickness + HoverLabelPadding));
    }

    static float DistancePointToSegment(Vector2 point, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float sqrLen = ab.sqrMagnitude;
        if (sqrLen <= 0.0001f) return Vector2.Distance(point, a);

        float t = Mathf.Clamp01(Vector2.Dot(point - a, ab) / sqrLen);
        Vector2 closest = a + (ab * t);
        return Vector2.Distance(point, closest);
    }
}
