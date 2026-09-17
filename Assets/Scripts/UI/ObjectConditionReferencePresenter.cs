using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

internal sealed class ObjectConditionReference
{
    public ScenarioNode node;
    public string objectAId;
    public string objectBId;
}

internal static class ObjectConditionReferenceQuery
{
    public static List<ObjectConditionReference> Collect(CurriculumGraphService graphService, string objectId)
    {
        var results = new List<ObjectConditionReference>();
        if (string.IsNullOrWhiteSpace(objectId)) return results;
        if (graphService == null || graphService.curriculum == null || graphService.curriculum.nodes == null) return results;

        foreach (var node in graphService.curriculum.nodes)
        {
            if (node == null || node.nodeType != ScenarioNodeType.Condition || node.condition == null) continue;

            bool useA = string.Equals(node.condition.objectAId, objectId, StringComparison.Ordinal);
            bool useB = ConditionTypeCatalog.RequiresObjectB(node.condition.type) &&
                string.Equals(node.condition.objectBId, objectId, StringComparison.Ordinal);
            if (!useA && !useB) continue;

            results.Add(new ObjectConditionReference
            {
                node = node,
                objectAId = node.condition.objectAId,
                objectBId = node.condition.objectBId
            });
        }

        results.Sort((a, b) => string.CompareOrdinal(
            a.node != null ? a.node.nodeId : string.Empty,
            b.node != null ? b.node.nodeId : string.Empty));
        return results;
    }

    public static string BuildSignature(string selectedObjectId, List<ObjectConditionReference> references)
    {
        var builder = new StringBuilder();
        builder.Append(selectedObjectId ?? string.Empty);
        if (references == null) return builder.ToString();

        foreach (var reference in references)
        {
            if (reference == null) continue;
            builder.Append('|');
            builder.Append(reference.node != null ? reference.node.nodeId : string.Empty);
            builder.Append(':');
            builder.Append(reference.objectAId ?? string.Empty);
            builder.Append(':');
            builder.Append(reference.objectBId ?? string.Empty);
            builder.Append(reference.node?.condition?.type ?? string.Empty);
            builder.Append(ConditionTypeCatalog.BuildParameterSignature(reference.node?.condition));
        }

        return builder.ToString();
    }
}

internal sealed class ObjectConditionReferencePresenter
{
    const float RefreshInterval = 0.3f;
    const float UsageNodeBlockMinHeight = 64f;
    const float UsageNodeBlockSpacing = 8f;
    const float UsageConditionFallbackHeight = 180f;
    const string UsageRowName = "Row_ConditionUsage";
    const string UsageLabelName = "Label";
    const string UsageEmptyTextName = "Text_ConditionUsage";
    const string UsageListName = "UsageNodeList";
    const string UsageTemplateName = "UsageNodeBlock_Template";
    const string UsageBlockBodyName = "Text_UsageNodeBody";
    const string UsageRowLabel = "\u4F7F\u7528\u4E2D\u30CE\u30FC\u30C9";
    const string UnusedLabel = "\u3053\u306E\u30AA\u30D6\u30B8\u30A7\u30AF\u30C8\u306F\u307E\u3060\u624B\u9806\u3067\u4F7F\u308F\u308C\u3066\u3044\u307E\u305B\u3093";
    const string UnsetLabel = "\u672A\u8A2D\u5B9A";
    const string DescriptionPhraseMiddle = "\u3092";
    const string DescriptionPhraseSuffix = "\u306B\u8FD1\u3065\u3051\u308B";

    readonly Transform panelRoot;
    TMP_Text usageNodeLabelText;
    TMP_Text emptyText;
    RectTransform listRoot;
    GameObject fallbackBlockTemplate;
    ObjectDetailConditionNodeStyler styler;
    CurriculumGraphService graphService;
    ScenarioGraphUI scenarioGraphUI;
    ConditionNodeUI conditionTemplateCache;
    PlacedObject selectedObject;
    float nextRefreshTime;
    string currentSignature = string.Empty;

    public ObjectConditionReferencePresenter(
        Transform panelRoot,
        TMP_Text usageNodeLabelText,
        TMP_Text emptyText,
        RectTransform listRoot,
        GameObject fallbackBlockTemplate,
        ObjectDetailConditionNodeStyler styler)
    {
        this.panelRoot = panelRoot;
        this.usageNodeLabelText = usageNodeLabelText;
        this.emptyText = emptyText;
        this.listRoot = listRoot;
        this.fallbackBlockTemplate = fallbackBlockTemplate;
        this.styler = styler;
    }

    public void SetServices(CurriculumGraphService graphService, ScenarioGraphUI scenarioGraphUI)
    {
        this.graphService = graphService;
        if (this.scenarioGraphUI == scenarioGraphUI) return;

        this.scenarioGraphUI = scenarioGraphUI;
        conditionTemplateCache = null;
    }

    public void SetStyler(ObjectDetailConditionNodeStyler styler)
    {
        this.styler = styler;
    }

    public void EnsureHierarchy()
    {
        var content = panelRoot.Find("Scroll_Detail/Viewport/Content");
        if (content == null) return;

        var row = content.Find(UsageRowName);
        if (row == null)
        {
            row = CreateUsageRow(content);
        }

        EnsureUsageLabel(row);
        EnsureEmptyText(row);
        EnsureListRoot(row);
        EnsureFallbackBlockTemplate();
    }

    public void Select(PlacedObject placedObject)
    {
        selectedObject = placedObject;
        currentSignature = string.Empty;
        nextRefreshTime = 0f;
        Refresh(force: true);
    }

    public void ClearSelection()
    {
        selectedObject = null;
        currentSignature = string.Empty;
        nextRefreshTime = 0f;
    }

    public void Tick(bool isPanelShown, float unscaledTime)
    {
        if (selectedObject == null || !isPanelShown) return;
        if (unscaledTime < nextRefreshTime) return;

        nextRefreshTime = unscaledTime + RefreshInterval;
        Refresh(force: false);
    }

    static Transform CreateUsageRow(Transform content)
    {
        var rowGo = new GameObject(UsageRowName, typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        var rowRt = rowGo.GetComponent<RectTransform>();
        rowRt.SetParent(content, false);

        var rowImage = rowGo.GetComponent<Image>();
        rowImage.color = DesignTokens.Surface;

        var rowLayout = rowGo.GetComponent<VerticalLayoutGroup>();
        rowLayout.childControlWidth = true;
        rowLayout.childControlHeight = true;
        rowLayout.childForceExpandWidth = true;
        rowLayout.childForceExpandHeight = false;
        rowLayout.spacing = DesignTokens.SpaceXs;
        rowLayout.padding = new RectOffset(
            (int)DesignTokens.SpaceMd,
            (int)DesignTokens.SpaceMd,
            (int)DesignTokens.SpaceSm,
            (int)DesignTokens.SpaceSm);

        var rowFitter = rowGo.GetComponent<ContentSizeFitter>();
        rowFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        return rowRt;
    }

    void EnsureUsageLabel(Transform row)
    {
        if (usageNodeLabelText == null)
        {
            var found = row.Find(UsageLabelName);
            if (found != null) usageNodeLabelText = found.GetComponent<TMP_Text>();
        }

        if (usageNodeLabelText == null)
        {
            var labelGo = new GameObject(UsageLabelName, typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
            labelGo.transform.SetParent(row, false);
            usageNodeLabelText = labelGo.GetComponent<TMP_Text>();
        }

        usageNodeLabelText.fontSize = DesignTokens.FontSizeCaption;
        usageNodeLabelText.color = DesignTokens.TextSecondary;
        usageNodeLabelText.alignment = TextAlignmentOptions.MidlineLeft;
        usageNodeLabelText.text = UsageRowLabel;

        var layout = usageNodeLabelText.GetComponent<LayoutElement>();
        if (layout == null) layout = usageNodeLabelText.gameObject.AddComponent<LayoutElement>();
        layout.minHeight = DesignTokens.FontSizeCaption + 4f;
        layout.preferredHeight = DesignTokens.FontSizeCaption + 4f;
    }

    void EnsureEmptyText(Transform row)
    {
        if (emptyText == null)
        {
            var found = row.Find(UsageEmptyTextName);
            if (found != null) emptyText = found.GetComponent<TMP_Text>();
        }

        if (emptyText == null)
        {
            var textGo = new GameObject(UsageEmptyTextName, typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
            textGo.transform.SetParent(row, false);
            emptyText = textGo.GetComponent<TMP_Text>();
        }

        emptyText.fontSize = DesignTokens.FontSizeBody;
        emptyText.color = DesignTokens.TextPrimary;
        emptyText.alignment = TextAlignmentOptions.TopLeft;
        emptyText.text = UnusedLabel;

        var layout = emptyText.GetComponent<LayoutElement>();
        if (layout == null) layout = emptyText.gameObject.AddComponent<LayoutElement>();
        layout.minHeight = DesignTokens.FontSizeBody + 4f;
    }

    void EnsureListRoot(Transform row)
    {
        if (listRoot == null)
        {
            listRoot = row.Find(UsageListName) as RectTransform;
        }

        if (listRoot == null)
        {
            var listGo = new GameObject(UsageListName, typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            listRoot = listGo.GetComponent<RectTransform>();
            listRoot.SetParent(row, false);
        }

        var listLayout = listRoot.GetComponent<VerticalLayoutGroup>();
        if (listLayout == null) listLayout = listRoot.gameObject.AddComponent<VerticalLayoutGroup>();
        listLayout.childControlWidth = true;
        listLayout.childControlHeight = true;
        listLayout.childForceExpandWidth = true;
        listLayout.childForceExpandHeight = false;
        listLayout.spacing = UsageNodeBlockSpacing;
        listLayout.padding = new RectOffset(0, 0, 0, 0);

        var listFitter = listRoot.GetComponent<ContentSizeFitter>();
        if (listFitter == null) listFitter = listRoot.gameObject.AddComponent<ContentSizeFitter>();
        listFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
    }

    void EnsureFallbackBlockTemplate()
    {
        if (listRoot == null) return;

        if (fallbackBlockTemplate == null)
        {
            var found = listRoot.Find(UsageTemplateName);
            if (found != null) fallbackBlockTemplate = found.gameObject;
        }

        if (fallbackBlockTemplate == null)
        {
            fallbackBlockTemplate = CreateFallbackBlockTemplate(listRoot);
        }

        fallbackBlockTemplate.SetActive(false);
    }

    static GameObject CreateFallbackBlockTemplate(Transform parent)
    {
        var blockGo = new GameObject(UsageTemplateName, typeof(RectTransform), typeof(Image), typeof(Outline), typeof(LayoutElement));
        var blockRt = blockGo.GetComponent<RectTransform>();
        blockRt.SetParent(parent, false);

        var blockImage = blockGo.GetComponent<Image>();
        blockImage.color = DesignTokens.Surface;

        var outline = blockGo.GetComponent<Outline>();
        outline.effectColor = DesignTokens.Divider;
        outline.effectDistance = new Vector2(1f, -1f);
        outline.useGraphicAlpha = false;

        var layout = blockGo.GetComponent<LayoutElement>();
        layout.minHeight = UsageNodeBlockMinHeight;
        layout.preferredHeight = UsageNodeBlockMinHeight;

        var bodyGo = new GameObject(UsageBlockBodyName, typeof(RectTransform), typeof(TextMeshProUGUI));
        var bodyRt = bodyGo.GetComponent<RectTransform>();
        bodyRt.SetParent(blockRt, false);
        bodyRt.anchorMin = Vector2.zero;
        bodyRt.anchorMax = Vector2.one;
        bodyRt.pivot = new Vector2(0.5f, 0.5f);
        bodyRt.offsetMin = new Vector2(10f, 8f);
        bodyRt.offsetMax = new Vector2(-10f, -8f);

        var bodyText = bodyGo.GetComponent<TMP_Text>();
        bodyText.fontSize = DesignTokens.FontSizeBody;
        bodyText.color = DesignTokens.TextPrimary;
        bodyText.alignment = TextAlignmentOptions.TopLeft;
        bodyText.text = UnusedLabel;
        return blockGo;
    }

    void Refresh(bool force)
    {
        if (selectedObject == null) return;
        EnsureHierarchy();
        if (listRoot == null || fallbackBlockTemplate == null) return;

        selectedObject.EnsureHasId();
        var references = ObjectConditionReferenceQuery.Collect(graphService, selectedObject.id);
        var signature = ObjectConditionReferenceQuery.BuildSignature(selectedObject.id, references);
        if (!force && string.Equals(signature, currentSignature, StringComparison.Ordinal)) return;

        currentSignature = signature;
        Render(references);
    }

    void Render(List<ObjectConditionReference> references)
    {
        ClearBlocks();

        if (references == null || references.Count == 0)
        {
            if (emptyText != null)
            {
                emptyText.gameObject.SetActive(true);
                emptyText.text = UnusedLabel;
            }
            return;
        }

        if (emptyText != null)
        {
            emptyText.gameObject.SetActive(false);
        }

        var template = ResolveConditionNodeTemplate();
        if (template == null)
        {
            var labelMap = BuildObjectLabelMap();
            foreach (var reference in references)
            {
                RenderFallbackBlock(reference, labelMap);
            }
            return;
        }

        int displayIndex = 1;
        foreach (var reference in references)
        {
            if (reference == null || reference.node == null) continue;

            var block = UnityEngine.Object.Instantiate(template, listRoot);
            block.name = $"UsageNodeBlock_{reference.node.nodeId}";
            block.gameObject.SetActive(true);
            ConfigureConditionNode(block, reference.node, displayIndex);
            displayIndex++;
        }
    }

    ConditionNodeUI ResolveConditionNodeTemplate()
    {
        if (conditionTemplateCache != null) return conditionTemplateCache;

        if (scenarioGraphUI == null)
        {
            scenarioGraphUI = UnityEngine.Object.FindFirstObjectByType<ScenarioGraphUI>();
        }

        if (scenarioGraphUI != null)
        {
            conditionTemplateCache = scenarioGraphUI.GetConditionNodeTemplateForExternalUse();
            if (conditionTemplateCache != null) return conditionTemplateCache;
        }

        var all = UnityEngine.Object.FindObjectsByType<ConditionNodeUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var ui in all)
        {
            if (ui == null) continue;
            if (listRoot != null && ui.transform.IsChildOf(listRoot)) continue;
            conditionTemplateCache = ui;
            return conditionTemplateCache;
        }

        return null;
    }

    void ConfigureConditionNode(ConditionNodeUI usageNodeUi, ScenarioNode node, int displayIndex)
    {
        if (usageNodeUi == null || node == null) return;

        DisableNodeDragHandlers(usageNodeUi);
        ApplyNodeLayout(usageNodeUi.transform as RectTransform);

        usageNodeUi.onClickOutputConnector = null;
        usageNodeUi.onBeginOutputConnectorDrag = null;
        usageNodeUi.onOutputConnectorDrag = null;
        usageNodeUi.onCompleteConnectorDrag = null;
        usageNodeUi.onCancelConnectorDrag = null;
        usageNodeUi.onClickDelete = DeleteNode;
        usageNodeUi.onChanged = HandleNodeChanged;
        usageNodeUi.Bind(graphService, node);
        usageNodeUi.EnterEmbeddedMode(displayIndex);

        if (styler != null)
        {
            styler.Apply(usageNodeUi);
        }
    }

    void HandleNodeChanged()
    {
        if (scenarioGraphUI != null)
        {
            scenarioGraphUI.RebuildFromExternalChange();
        }

        currentSignature = string.Empty;
        nextRefreshTime = 0f;
    }

    void DeleteNode(string nodeId)
    {
        if (string.IsNullOrWhiteSpace(nodeId) || graphService == null) return;

        graphService.ExecuteCommand("Delete condition", () =>
        {
            if (graphService.FindNode(nodeId) == null) return false;
            graphService.RemoveNode(nodeId);
            return graphService.FindNode(nodeId) == null;
        });
        if (scenarioGraphUI != null)
        {
            scenarioGraphUI.RebuildFromExternalChange();
        }

        currentSignature = string.Empty;
        nextRefreshTime = 0f;
        Refresh(force: true);
    }

    static void ApplyNodeLayout(RectTransform nodeRect)
    {
        if (nodeRect == null) return;

        nodeRect.anchorMin = new Vector2(0.5f, 1f);
        nodeRect.anchorMax = new Vector2(0.5f, 1f);
        nodeRect.pivot = new Vector2(0.5f, 1f);
        nodeRect.anchoredPosition = Vector2.zero;

        float height = nodeRect.rect.height > 1f
            ? nodeRect.rect.height
            : (nodeRect.sizeDelta.y > 1f ? nodeRect.sizeDelta.y : UsageConditionFallbackHeight);

        var layout = nodeRect.GetComponent<LayoutElement>();
        if (layout == null) layout = nodeRect.gameObject.AddComponent<LayoutElement>();
        layout.minHeight = height;
        layout.preferredHeight = height;
        layout.flexibleHeight = 0f;
        layout.flexibleWidth = 1f;
    }

    static void DisableNodeDragHandlers(ConditionNodeUI conditionNodeUi)
    {
        if (conditionNodeUi == null) return;

        var dragHandlers = conditionNodeUi.GetComponentsInChildren<NodeDragHandler>(true);
        foreach (var dragHandler in dragHandlers)
        {
            if (dragHandler != null) dragHandler.enabled = false;
        }
    }

    void RenderFallbackBlock(ObjectConditionReference reference, Dictionary<string, string> objectLabelMap)
    {
        if (reference == null || fallbackBlockTemplate == null || listRoot == null) return;

        var block = UnityEngine.Object.Instantiate(fallbackBlockTemplate, listRoot);
        string nodeId = reference.node != null ? reference.node.nodeId : "unknown";
        block.name = $"UsageNodeBlock_{nodeId}";
        block.SetActive(true);

        var body = block.transform.Find(UsageBlockBodyName)?.GetComponent<TMP_Text>();
        if (body == null) body = block.GetComponentInChildren<TMP_Text>(true);
        if (body == null) return;

        string aName = ResolveObjectLabel(reference.objectAId, objectLabelMap);
        string bName = ResolveObjectLabel(reference.objectBId, objectLabelMap);
        string type = reference.node?.condition?.type;
        string action = ConditionTypeCatalog.Find(type)?.label ?? type;
        body.text = ConditionTypeCatalog.RequiresObjectB(type) ? $"{aName} / {bName}\n{action}" : $"{aName}\n{action}";
    }

    void ClearBlocks()
    {
        if (listRoot == null) return;

        for (int i = listRoot.childCount - 1; i >= 0; i--)
        {
            var child = listRoot.GetChild(i);
            if (child == null) continue;
            if (fallbackBlockTemplate != null && child == fallbackBlockTemplate.transform) continue;
            UnityEngine.Object.Destroy(child.gameObject);
        }
    }

    static Dictionary<string, string> BuildObjectLabelMap()
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        var placedObjects = UnityEngine.Object.FindObjectsByType<PlacedObject>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (var placed in placedObjects)
        {
            if (placed == null) continue;
            placed.EnsureHasId();
            if (string.IsNullOrWhiteSpace(placed.id)) continue;
            map[placed.id] = placed.GetDisplayName();
        }

        return map;
    }

    static string ResolveObjectLabel(string objectId, Dictionary<string, string> objectLabelMap)
    {
        if (string.IsNullOrWhiteSpace(objectId)) return UnsetLabel;
        if (objectLabelMap != null && objectLabelMap.TryGetValue(objectId, out var label) && !string.IsNullOrWhiteSpace(label))
        {
            return label;
        }
        return objectId;
    }
}
