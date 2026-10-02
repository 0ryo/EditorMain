using System;
using System.Collections.Generic;
using UnityEngine;

internal sealed class ScenarioConnectionLines
{
    const float IdleLineThickness = 2.5f;
    const float HoverLineThickness = 4f;

    static Color ConnectionLineColor => ScenarioGraphVisualStyle.IdleConnectionColor;
    static readonly Color DragPreviewLineColor = new Color(DesignTokens.Accent.r, DesignTokens.Accent.g, DesignTokens.Accent.b, 0.9f);

    readonly List<ConnectionLineGraphic> lines = new List<ConnectionLineGraphic>();
    ConnectionLineGraphic dragPreviewLine;
    RectTransform dragPreviewTarget;

    public void Clear()
    {
        foreach (var line in lines)
        {
            if (line == null) continue;
            UnityEngine.Object.Destroy(line.gameObject);
        }
        lines.Clear();
    }

    public void Add(
        ConnectionLineGraphic lineTemplate,
        RectTransform lineLayer,
        ScenarioEdge edge,
        RectTransform fromConnector,
        RectTransform toConnector,
        RectTransform[] raycastBlockers,
        Action<ConnectionLineGraphic> onClickLine)
    {
        var line = UnityEngine.Object.Instantiate(lineTemplate, lineLayer);
        line.gameObject.SetActive(true);
        line.from = fromConnector;
        line.to = toConnector;
        line.fromNodeId = edge.fromNodeId;
        line.toNodeId = edge.toNodeId;
        line.edgeType = edge.edgeType;
        line.raycastBlockers = raycastBlockers;
        line.onClickLine = onClickLine;
        ConfigureLineGraphic(line, lineLayer, ConnectionLineColor, IdleLineThickness, raycastTarget: true);
        line.SetHoverStyle(ConnectionLineColor, IdleLineThickness, DesignTokens.Accent, HoverLineThickness);
        lines.Add(line);
    }

    public void ClearDragPreview()
    {
        if (dragPreviewLine != null) UnityEngine.Object.Destroy(dragPreviewLine.gameObject);
        if (dragPreviewTarget != null) UnityEngine.Object.Destroy(dragPreviewTarget.gameObject);
        dragPreviewLine = null;
        dragPreviewTarget = null;
    }

    public void EnsureDragPreview(ConnectionLineGraphic lineTemplate, RectTransform lineLayer, RectTransform fromConnector)
    {
        if (dragPreviewTarget == null)
        {
            var targetGo = new GameObject("DragPreviewTarget", typeof(RectTransform));
            dragPreviewTarget = targetGo.GetComponent<RectTransform>();
            dragPreviewTarget.SetParent(lineLayer, false);
            dragPreviewTarget.anchorMin = new Vector2(0.5f, 0.5f);
            dragPreviewTarget.anchorMax = new Vector2(0.5f, 0.5f);
            dragPreviewTarget.sizeDelta = new Vector2(1f, 1f);
            dragPreviewTarget.anchoredPosition = Vector2.zero;
        }

        if (dragPreviewLine == null)
        {
            dragPreviewLine = UnityEngine.Object.Instantiate(lineTemplate, lineLayer);
            dragPreviewLine.gameObject.name = "DragPreviewLine";
            dragPreviewLine.gameObject.SetActive(true);
            ConfigureLineGraphic(dragPreviewLine, lineLayer, DragPreviewLineColor, HoverLineThickness, raycastTarget: false);
        }

        dragPreviewLine.from = fromConnector;
        dragPreviewLine.to = dragPreviewTarget;
        dragPreviewLine.fromNodeId = null;
        dragPreviewLine.toNodeId = null;
        dragPreviewLine.raycastBlockers = null;
        dragPreviewLine.onClickLine = null;
    }

    public void UpdateDragPreviewPosition(RectTransform lineLayer, Vector2 screenPosition)
    {
        if (dragPreviewTarget == null || lineLayer == null) return;

        Camera eventCamera = null;
        var canvas = lineLayer.GetComponentInParent<Canvas>();
        if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
        {
            eventCamera = canvas.worldCamera;
        }

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(lineLayer, screenPosition, eventCamera, out var local))
        {
            dragPreviewTarget.anchoredPosition = local;
        }
    }

    static void ConfigureLineGraphic(ConnectionLineGraphic line, RectTransform lineLayer, Color color, float thickness, bool raycastTarget)
    {
        if (line == null || lineLayer == null) return;

        if (line.GetComponent<CanvasRenderer>() == null)
        {
            line.gameObject.AddComponent<CanvasRenderer>();
            Debug.LogWarning($"[ScenarioGraphUI] Added missing CanvasRenderer on {line.gameObject.name}");
        }

        var rt = line.rectTransform;
        rt.SetParent(lineLayer, false);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.localScale = Vector3.one;
        rt.localRotation = Quaternion.identity;

        line.color = color;
        line.thickness = thickness;
        line.raycastTarget = raycastTarget;
        line.SetAllDirty();
    }
}

public static class ScenarioGraphVisualStyle
{
    const float NodeHeaderHeight = 40f;

    public static Color IdleConnectorColor => DesignTokens.Divider;
    public static Color IdleConnectionColor => DesignTokens.Divider;

    public static void ApplyConnector(UnityEngine.UI.Button button)
    {
        if (button == null) return;

        var graphic = button.targetGraphic != null ? button.targetGraphic : button.GetComponent<UnityEngine.UI.Graphic>();
        if (graphic != null)
        {
            button.targetGraphic = graphic;
            graphic.color = IdleConnectorColor;
        }

        var colors = button.colors;
        colors.normalColor = IdleConnectorColor;
        colors.highlightedColor = DesignTokens.Accent;
        colors.pressedColor = DesignTokens.AccentPress;
        colors.selectedColor = DesignTokens.AccentHover;
        colors.disabledColor = IdleConnectorColor;
        colors.colorMultiplier = 1f;
        button.colors = colors;
        button.transition = UnityEngine.UI.Selectable.Transition.ColorTint;
    }

    public static void EnsureNodeHeader(Transform nodeRoot)
    {
        if (!(nodeRoot is RectTransform root)) return;

        var header = root.Find("NodeHeaderSurface") as RectTransform;
        if (header == null)
        {
            var headerObject = new GameObject("NodeHeaderSurface", typeof(RectTransform), typeof(UnityEngine.UI.Image));
            header = headerObject.GetComponent<RectTransform>();
            header.SetParent(root, false);
        }

        header.anchorMin = new Vector2(0f, 1f);
        header.anchorMax = new Vector2(1f, 1f);
        header.pivot = new Vector2(0.5f, 1f);
        header.offsetMin = new Vector2(1f, -NodeHeaderHeight);
        header.offsetMax = new Vector2(-1f, -1f);

        var image = header.GetComponent<UnityEngine.UI.Image>();
        if (image == null) image = header.gameObject.AddComponent<UnityEngine.UI.Image>();
        image.color = DesignTokens.BgSecondary;
        image.raycastTarget = false;
        header.SetAsFirstSibling();
    }
}
