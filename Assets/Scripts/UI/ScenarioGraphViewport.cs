using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

internal sealed class ScenarioGraphViewport
{
    const float GraphContentMinWidth = 6000f;
    const float GraphContentMinHeight = 6000f;

    readonly Dictionary<string, RectTransform> minimapNodeIndicators = new Dictionary<string, RectTransform>();
    RectTransform minimapRoot;
    RectTransform minimapViewportIndicator;
    Rect minimapContentBounds;

    public static void EnsureMask(RectTransform nodeArea)
    {
        if (nodeArea == null) return;
        if (nodeArea.GetComponent<RectMask2D>() != null) return;

        nodeArea.gameObject.AddComponent<RectMask2D>();
    }

    public static void EnsureContent(RectTransform nodeArea, ref RectTransform graphContent)
    {
        if (nodeArea == null) return;

        if (graphContent == null)
        {
            var found = nodeArea.Find("GraphContent") as RectTransform;
            if (found != null)
            {
                graphContent = found;
            }
        }

        if (graphContent == null)
        {
            var graphGo = new GameObject("GraphContent", typeof(RectTransform));
            graphContent = graphGo.GetComponent<RectTransform>();
            graphContent.SetParent(nodeArea, false);
            graphContent.anchorMin = new Vector2(0.5f, 0.5f);
            graphContent.anchorMax = new Vector2(0.5f, 0.5f);
            graphContent.pivot = new Vector2(0.5f, 0.5f);
            graphContent.sizeDelta = new Vector2(GraphContentMinWidth, GraphContentMinHeight);
            graphContent.anchoredPosition = Vector2.zero;
        }

        if (graphContent.rect.width < GraphContentMinWidth || graphContent.rect.height < GraphContentMinHeight)
        {
            graphContent.anchorMin = new Vector2(0.5f, 0.5f);
            graphContent.anchorMax = new Vector2(0.5f, 0.5f);
            graphContent.pivot = new Vector2(0.5f, 0.5f);
            graphContent.sizeDelta = new Vector2(
                Mathf.Max(GraphContentMinWidth, graphContent.rect.width),
                Mathf.Max(GraphContentMinHeight, graphContent.rect.height));
            graphContent.anchoredPosition = Vector2.zero;
        }
    }

    public static void ReparentToContent(RectTransform graphContent, RectTransform child)
    {
        if (graphContent == null || child == null) return;
        if (child == graphContent) return;
        if (child.parent == graphContent) return;
        child.SetParent(graphContent, false);
    }

    public static void ConfigurePanZoom(RectTransform nodeArea, RectTransform graphContent, ref NodeAreaPanZoomController panZoomController)
    {
        if (nodeArea == null || graphContent == null) return;

        var nodeAreaImage = nodeArea.GetComponent<Image>();
        if (nodeAreaImage != null)
        {
            nodeAreaImage.raycastTarget = true;
        }

        panZoomController = nodeArea.GetComponent<NodeAreaPanZoomController>();
        if (panZoomController == null)
        {
            panZoomController = nodeArea.gameObject.AddComponent<NodeAreaPanZoomController>();
        }

        panZoomController.Configure(nodeArea, graphContent);
    }

    public void EnsureTools(
        RectTransform nodeArea, UnityAction fitContent, UnityAction resetZoom, UnityAction autoLayout)
    {
        if (nodeArea == null) return;

        var tools = nodeArea.Find("ViewportTools") as RectTransform;
        if (tools == null)
        {
            var toolsGo = new GameObject("ViewportTools", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            tools = toolsGo.GetComponent<RectTransform>();
            tools.SetParent(nodeArea, false);
        }

        tools.anchorMin = Vector2.one;
        tools.anchorMax = Vector2.one;
        tools.pivot = Vector2.one;
        tools.anchoredPosition = new Vector2(-12f, -12f);
        tools.sizeDelta = new Vector2(216f, 34f);
        var layout = tools.GetComponent<HorizontalLayoutGroup>();
        if (layout == null) layout = tools.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 6f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = true;

        var fitContentButton = EnsureViewportButton(tools, "Button_FitContent", "全体");
        var zoomResetButton = EnsureViewportButton(tools, "Button_ZoomReset", "100%");
        var autoLayoutButton = EnsureViewportButton(tools, "Button_AutoLayout", "整列");

        fitContentButton.onClick.RemoveAllListeners();
        fitContentButton.onClick.AddListener(fitContent);
        zoomResetButton.onClick.RemoveAllListeners();
        zoomResetButton.onClick.AddListener(resetZoom);
        autoLayoutButton.onClick.RemoveAllListeners();
        autoLayoutButton.onClick.AddListener(autoLayout);

        minimapRoot = nodeArea.Find("ScenarioMinimap") as RectTransform;
        if (minimapRoot == null)
        {
            var minimapGo = new GameObject("ScenarioMinimap", typeof(RectTransform), typeof(Image), typeof(Outline));
            minimapRoot = minimapGo.GetComponent<RectTransform>();
            minimapRoot.SetParent(nodeArea, false);
        }

        minimapRoot.anchorMin = new Vector2(1f, 0f);
        minimapRoot.anchorMax = new Vector2(1f, 0f);
        minimapRoot.pivot = new Vector2(1f, 0f);
        minimapRoot.anchoredPosition = new Vector2(-12f, 12f);
        minimapRoot.sizeDelta = new Vector2(ScenarioGraphMinimapLayout.Width, ScenarioGraphMinimapLayout.Height);
        var minimapImage = minimapRoot.GetComponent<Image>();
        if (minimapImage == null) minimapImage = minimapRoot.gameObject.AddComponent<Image>();
        minimapImage.color = new Color(DesignTokens.BgSecondary.r, DesignTokens.BgSecondary.g, DesignTokens.BgSecondary.b, 0.94f);
        minimapImage.raycastTarget = false;
        var minimapOutline = minimapRoot.GetComponent<Outline>();
        if (minimapOutline == null) minimapOutline = minimapRoot.gameObject.AddComponent<Outline>();
        minimapOutline.effectColor = DesignTokens.Divider;
        minimapOutline.effectDistance = new Vector2(1f, -1f);

        var viewportTransform = minimapRoot.Find("Viewport") as RectTransform;
        if (viewportTransform == null)
        {
            var viewportGo = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Outline));
            viewportTransform = viewportGo.GetComponent<RectTransform>();
            viewportTransform.SetParent(minimapRoot, false);
        }
        minimapViewportIndicator = viewportTransform;
        ConfigureMinimapChild(minimapViewportIndicator);
        var viewportImage = minimapViewportIndicator.GetComponent<Image>();
        if (viewportImage == null) viewportImage = minimapViewportIndicator.gameObject.AddComponent<Image>();
        viewportImage.color = new Color(DesignTokens.Accent.r, DesignTokens.Accent.g, DesignTokens.Accent.b, 0.12f);
        viewportImage.raycastTarget = false;
        var viewportOutline = minimapViewportIndicator.GetComponent<Outline>();
        if (viewportOutline == null) viewportOutline = minimapViewportIndicator.gameObject.AddComponent<Outline>();
        viewportOutline.effectColor = DesignTokens.Accent;
        viewportOutline.effectDistance = new Vector2(1f, -1f);

        UiRoundedTheme.ApplyToHierarchy(tools, DesignTokens.CornerRadius);
        UiRoundedTheme.ApplyToHierarchy(minimapRoot, DesignTokens.CornerRadius);
        minimapRoot.SetAsLastSibling();
        tools.SetAsLastSibling();
    }

    static Button EnsureViewportButton(RectTransform parent, string objectName, string labelText)
    {
        var found = parent.Find(objectName);
        Button button = found != null ? found.GetComponent<Button>() : null;
        if (button == null)
        {
            var go = new GameObject(objectName, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            button = go.GetComponent<Button>();
        }

        var image = button.GetComponent<Image>();
        if (image == null) image = button.gameObject.AddComponent<Image>();
        image.color = DesignTokens.Surface;
        button.targetGraphic = image;
        var element = button.GetComponent<LayoutElement>();
        if (element == null) element = button.gameObject.AddComponent<LayoutElement>();
        element.preferredWidth = 68f;
        element.minHeight = 34f;

        var label = button.GetComponentInChildren<TMP_Text>(true);
        if (label == null)
        {
            var labelGo = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            labelGo.transform.SetParent(button.transform, false);
            label = labelGo.GetComponent<TMP_Text>();
        }
        SetRect(label.rectTransform, Vector2.zero, Vector2.one, new Vector2(4f, 0f), new Vector2(-4f, 0f));
        label.text = labelText;
        label.fontSize = DesignTokens.FontSizeCaption;
        label.color = DesignTokens.TextPrimary;
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;
        return button;
    }

    public void RebuildMinimapIndicators(Dictionary<string, ScenarioNodeViewBinding> nodeUIs, RectTransform nodeArea, RectTransform graphContent)
    {
        foreach (var indicator in minimapNodeIndicators.Values)
        {
            if (indicator != null) UnityEngine.Object.Destroy(indicator.gameObject);
        }
        minimapNodeIndicators.Clear();
        if (minimapRoot == null) return;

        foreach (var pair in nodeUIs)
        {
            if (pair.Value?.root == null) continue;
            var go = new GameObject($"Node_{pair.Key}", typeof(RectTransform), typeof(Image));
            var indicator = go.GetComponent<RectTransform>();
            indicator.SetParent(minimapRoot, false);
            ConfigureMinimapChild(indicator);
            var image = go.GetComponent<Image>();
            image.color = pair.Value.nodeType switch
            {
                ScenarioNodeType.Start => DesignTokens.Success,
                ScenarioNodeType.End => DesignTokens.Error,
                ScenarioNodeType.Condition => DesignTokens.Warning,
                _ => DesignTokens.Accent,
            };
            image.raycastTarget = false;
            minimapNodeIndicators[pair.Key] = indicator;
        }

        if (minimapViewportIndicator != null) minimapViewportIndicator.SetAsLastSibling();
        RefreshMinimapNodes(nodeUIs, nodeArea, graphContent);
    }

    public void RefreshMinimapNodes(Dictionary<string, ScenarioNodeViewBinding> nodeUIs, RectTransform nodeArea, RectTransform graphContent)
    {
        if (minimapRoot == null || minimapNodeIndicators.Count == 0) return;

        var boundsBuilder = new ScenarioGraphMinimapLayout.BoundsBuilder();
        foreach (var pair in nodeUIs)
        {
            var root = pair.Value?.root;
            if (root == null) continue;
            boundsBuilder.Encapsulate(root.anchoredPosition, root.rect);
        }
        if (!boundsBuilder.TryGetBounds(out var contentBounds)) return;
        minimapContentBounds = contentBounds;

        foreach (var pair in minimapNodeIndicators)
        {
            if (!nodeUIs.TryGetValue(pair.Key, out var binding) || binding?.root == null || pair.Value == null) continue;
            var root = binding.root;
            ScenarioGraphMinimapLayout.GetNodeIndicatorLayout(
                minimapContentBounds, root.anchoredPosition, root.rect, out var position, out var size);
            pair.Value.anchoredPosition = position;
            pair.Value.sizeDelta = size;
        }

        RefreshMinimapViewport(nodeArea, graphContent);
    }

    public void RefreshMinimapViewport(RectTransform nodeArea, RectTransform graphContent)
    {
        if (minimapViewportIndicator == null || nodeArea == null || graphContent == null ||
            minimapContentBounds.width <= 0f || minimapContentBounds.height <= 0f) return;

        ScenarioGraphMinimapLayout.GetViewportIndicatorLayout(
            minimapContentBounds, graphContent.anchoredPosition, graphContent.localScale.x, nodeArea.rect.size,
            out var position, out var size);
        minimapViewportIndicator.anchoredPosition = position;
        minimapViewportIndicator.sizeDelta = size;
    }

    static void ConfigureMinimapChild(RectTransform child)
    {
        child.anchorMin = Vector2.zero;
        child.anchorMax = Vector2.zero;
        child.pivot = new Vector2(0.5f, 0.5f);
        child.localScale = Vector3.one;
    }

    static void SetRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        if (rect == null) return;
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
    }
}
