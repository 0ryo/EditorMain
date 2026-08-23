using System;
using TMPro;
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

    TextMeshProUGUI hoverDeleteLabel;
    bool isPointerOver;

    const float AaEdgeWidth = 2.5f; // アンチエイリアス用の端のぼかし幅

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        if (from == null || to == null) return;

        Vector2 fromPoint = WorldToLocalCenter(from);
        Vector2 toPoint = WorldToLocalCenter(to);

        Vector2 direction = (toPoint - fromPoint).normalized;
        Vector2 normal = new Vector2(-direction.y, direction.x);

        float halfCore = thickness * 0.5f;
        float halfTotal = halfCore + AaEdgeWidth;

        // 8頂点: 外側(透明) → 内側(不透明) → 内側(不透明) → 外側(透明)
        Vector2 outerL0 = fromPoint - normal * halfTotal;
        Vector2 innerL0 = fromPoint - normal * halfCore;
        Vector2 innerR0 = fromPoint + normal * halfCore;
        Vector2 outerR0 = fromPoint + normal * halfTotal;

        Vector2 outerL1 = toPoint - normal * halfTotal;
        Vector2 innerL1 = toPoint - normal * halfCore;
        Vector2 innerR1 = toPoint + normal * halfCore;
        Vector2 outerR1 = toPoint + normal * halfTotal;

        Color coreColor = color;
        Color edgeColor = new Color(color.r, color.g, color.b, 0f);

        int i = vh.currentVertCount;
        AddVert(vh, outerL0, edgeColor);  // 0
        AddVert(vh, innerL0, coreColor);  // 1
        AddVert(vh, innerR0, coreColor);  // 2
        AddVert(vh, outerR0, edgeColor);  // 3
        AddVert(vh, outerL1, edgeColor);  // 4
        AddVert(vh, innerL1, coreColor);  // 5
        AddVert(vh, innerR1, coreColor);  // 6
        AddVert(vh, outerR1, edgeColor);  // 7

        // 左端ぼかし (0,1,5,4)
        vh.AddTriangle(i+0, i+1, i+5);
        vh.AddTriangle(i+0, i+5, i+4);
        // 中央コア (1,2,6,5)
        vh.AddTriangle(i+1, i+2, i+6);
        vh.AddTriangle(i+1, i+6, i+5);
        // 右端ぼかし (2,3,7,6)
        vh.AddTriangle(i+2, i+3, i+7);
        vh.AddTriangle(i+2, i+7, i+6);
    }

    static void AddVert(VertexHelper vh, Vector2 position, Color vertColor)
    {
        UIVertex vertex = UIVertex.simpleVert;
        vertex.color = vertColor;
        vertex.position = position;
        vh.AddVert(vertex);
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

        var labelGo = new GameObject("Text_DeleteHint", typeof(RectTransform), typeof(TextMeshProUGUI));
        var labelRt = labelGo.GetComponent<RectTransform>();
        labelRt.SetParent(rectTransform, false);
        labelRt.anchorMin = new Vector2(0.5f, 0.5f);
        labelRt.anchorMax = new Vector2(0.5f, 0.5f);
        labelRt.pivot = new Vector2(0.5f, 0.5f);
        labelRt.sizeDelta = new Vector2(140f, 22f);
        labelRt.anchoredPosition = Vector2.zero;

        hoverDeleteLabel = labelGo.GetComponent<TextMeshProUGUI>();
        hoverDeleteLabel.text = "\u7DDA\u3092\u30AF\u30EA\u30C3\u30AF\u3067\u524A\u9664";
        hoverDeleteLabel.fontSize = 12;
        hoverDeleteLabel.alignment = TextAlignmentOptions.Center;
        hoverDeleteLabel.color = DesignTokens.Error;
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
