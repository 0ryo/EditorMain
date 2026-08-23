using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ObjectDetailPanel : MonoBehaviour
{
    [SerializeField] TMP_Text headerDisplayNameText;
    [SerializeField] TMP_Text headerTechnicalIdText;
    [SerializeField] TMP_Text textPrefabLabel;
    [SerializeField] TMP_InputField inputObjectName;
    [SerializeField] TMP_InputField inputDescription;
    [SerializeField] TMP_Text textDescription; // Legacy fallback.
    [SerializeField] GameObject rowDescription;

    [SerializeField] TMP_Text usageNodeLabelText;
    [SerializeField] TMP_Text textConditionUsage; // Empty-state fallback text.
    [SerializeField] RectTransform usageNodeListRoot;
    [SerializeField] GameObject usageNodeBlockTemplate;
    [SerializeField] ObjectDetailConditionNodeStyler usageNodeStyler;

    SelectionService selectionService;
    CatalogUI catalogUI;
    CurriculumGraphService graphService;
    ScenarioGraphUI scenarioGraphUI;
    ConditionNodeUI usageConditionTemplateCache;
    RectTransform rt;
    CanvasGroup panelCanvasGroup;
    PlacedObject currentPo;

    Vector2 restOffsetMin;
    Vector2 restOffsetMax;
    Coroutine slideCoroutine;

    const float SlideDuration = 0.2f;
    const float DescriptionInputMinHeight = 96f;
    const float ConditionUsageRefreshInterval = 0.3f;
    const float UsageNodeBlockMinHeight = 64f;
    const float UsageNodeBlockSpacing = 8f;
    const string UsageRowName = "Row_ConditionUsage";
    const string UsageLabelName = "Label";
    const string UsageEmptyTextName = "Text_ConditionUsage";
    const string UsageListName = "UsageNodeList";
    const string UsageTemplateName = "UsageNodeBlock_Template";
    const string UsageBlockBodyName = "Text_UsageNodeBody";
    const string UsageRowLabel = "\u4F7F\u7528\u4E2D\u30CE\u30FC\u30C9";
    const string UnusedLabel = "\u3053\u306E\u30AA\u30D6\u30B8\u30A7\u30AF\u30C8\u306F\u307E\u3060\u624B\u9806\u3067\u4F7F\u308F\u308C\u3066\u3044\u307E\u305B\u3093";
    const string UnsetLabel = "\u672A\u8A2D\u5B9A";
    const string DescriptionPlaceholder = "\u8AAC\u660E\u3092\u5165\u529B...";
    const string DescriptionPhraseMiddle = "\u3092";
    const string DescriptionPhraseSuffix = "\u306B\u8FD1\u3065\u3051\u308B";

    const float UsageConditionFallbackHeight = 180f;

    float nextConditionUsageRefreshTime;
    string currentUsageSignature = string.Empty;

    class ConditionUsageEntry
    {
        public ScenarioNode node;
        public string objectAId;
        public string objectBId;
    }

    void Start()
    {
        rt = (RectTransform)transform;
        EnsurePolishedHierarchy();
        restOffsetMin = rt.offsetMin;
        restOffsetMax = rt.offsetMax;
        panelCanvasGroup = GetComponent<CanvasGroup>();
        if (panelCanvasGroup == null) panelCanvasGroup = gameObject.AddComponent<CanvasGroup>();
        panelCanvasGroup.alpha = 1f;

        selectionService = FindFirstObjectByType<SelectionService>();
        catalogUI = FindFirstObjectByType<CatalogUI>();
        graphService = FindFirstObjectByType<CurriculumGraphService>();
        scenarioGraphUI = FindFirstObjectByType<ScenarioGraphUI>();

        if (selectionService != null)
        {
            selectionService.OnSelectionChanged += OnSelectionChanged;
        }

        if (inputObjectName != null)
        {
            inputObjectName.onEndEdit.RemoveListener(OnNameInputEndEdit);
            inputObjectName.onEndEdit.AddListener(OnNameInputEndEdit);
        }

        EnsureDescriptionInputField();
        EnsureUsageNodeSection();
        EnsurePolishedHierarchy();
        if (usageNodeStyler == null)
        {
            usageNodeStyler = GetComponent<ObjectDetailConditionNodeStyler>();
        }

        if (inputDescription != null)
        {
            inputDescription.onEndEdit.RemoveListener(OnDescriptionInputEndEdit);
            inputDescription.onEndEdit.AddListener(OnDescriptionInputEndEdit);
        }

        DesignTokenApplier.ApplyDetailPanel(transform);
        gameObject.SetActive(false);
    }

    void Update()
    {
        if (currentPo == null || !isActiveAndEnabled || !gameObject.activeInHierarchy) return;
        if (Time.unscaledTime < nextConditionUsageRefreshTime) return;

        nextConditionUsageRefreshTime = Time.unscaledTime + ConditionUsageRefreshInterval;
        UpdateConditionUsage(currentPo, force: false);
    }

    void OnDestroy()
    {
        if (selectionService != null)
        {
            selectionService.OnSelectionChanged -= OnSelectionChanged;
        }

        if (inputObjectName != null)
        {
            inputObjectName.onEndEdit.RemoveListener(OnNameInputEndEdit);
        }

        if (inputDescription != null)
        {
            inputDescription.onEndEdit.RemoveListener(OnDescriptionInputEndEdit);
        }
    }

    void OnSelectionChanged(PlacedObject po)
    {
        if (po == null)
        {
            currentPo = null;
            currentUsageSignature = string.Empty;
            if (gameObject.activeSelf)
            {
                if (slideCoroutine != null) StopCoroutine(slideCoroutine);
                slideCoroutine = StartCoroutine(SlideOut());
            }
            return;
        }

        currentPo = po;
        Populate(po);
        currentUsageSignature = string.Empty;
        nextConditionUsageRefreshTime = 0f;

        if (!gameObject.activeSelf)
        {
            gameObject.SetActive(true);
            if (slideCoroutine != null) StopCoroutine(slideCoroutine);
            slideCoroutine = StartCoroutine(SlideIn());
        }
    }

    void Populate(PlacedObject po)
    {
        string label = po.typeId ?? string.Empty;
        string defaultDescription = string.Empty;

        if (catalogUI != null)
        {
            catalogUI.TryGetTypeInfo(po.typeId, out label, out defaultDescription);
        }

        if (textPrefabLabel != null)
        {
            textPrefabLabel.text = po.typeId ?? string.Empty;
        }

        if (headerDisplayNameText != null)
        {
            headerDisplayNameText.text = po.GetDisplayName();
        }

        if (headerTechnicalIdText != null)
        {
            headerTechnicalIdText.text = po.typeId ?? string.Empty;
        }

        if (inputObjectName != null)
        {
            inputObjectName.SetTextWithoutNotify(po.GetDisplayName());
        }

        if (rowDescription != null)
        {
            rowDescription.SetActive(true);
        }

        var description = po.GetDisplayDescription(defaultDescription);
        if (inputDescription != null)
        {
            inputDescription.SetTextWithoutNotify(description);
        }
        else if (textDescription != null)
        {
            textDescription.text = description;
        }

        UpdateConditionUsage(po, force: true);
    }

    void OnNameInputEndEdit(string value)
    {
        if (currentPo == null) return;

        currentPo.SetDisplayName(value);
        if (inputObjectName != null)
        {
            inputObjectName.SetTextWithoutNotify(currentPo.GetDisplayName());
        }
        if (headerDisplayNameText != null)
        {
            headerDisplayNameText.text = currentPo.GetDisplayName();
        }
    }

    void EnsurePolishedHierarchy()
    {
        if (rt == null) return;

        rt.offsetMin = new Vector2(-320f, rt.offsetMin.y);
        rt.offsetMax = new Vector2(rt.offsetMax.x, -64f);

        var header = transform.Find("Header") as RectTransform;
        if (header != null)
        {
            header.anchorMin = new Vector2(0f, 1f);
            header.anchorMax = new Vector2(1f, 1f);
            header.offsetMin = new Vector2(16f, -72f);
            header.offsetMax = new Vector2(-16f, -8f);

            if (headerDisplayNameText == null)
            {
                headerDisplayNameText = header.Find("Text_DisplayName")?.GetComponent<TMP_Text>();
                if (headerDisplayNameText == null) headerDisplayNameText = header.Find("Title")?.GetComponent<TMP_Text>();
            }
            if (headerDisplayNameText != null)
            {
                headerDisplayNameText.gameObject.name = "Text_DisplayName";
                headerDisplayNameText.fontSize = DesignTokens.FontSizeSubheading;
                headerDisplayNameText.color = DesignTokens.TextPrimary;
                headerDisplayNameText.alignment = TextAlignmentOptions.MidlineLeft;
                SetRect(headerDisplayNameText.rectTransform, Vector2.zero, Vector2.one, new Vector2(12f, 32f), new Vector2(-12f, -4f));
            }

            if (headerTechnicalIdText == null)
            {
                headerTechnicalIdText = header.Find("Text_TechnicalId")?.GetComponent<TMP_Text>();
            }
            if (headerTechnicalIdText == null)
            {
                var idGo = new GameObject("Text_TechnicalId", typeof(RectTransform), typeof(TextMeshProUGUI));
                var idRt = idGo.GetComponent<RectTransform>();
                idRt.SetParent(header, false);
                headerTechnicalIdText = idGo.GetComponent<TMP_Text>();
            }
            headerTechnicalIdText.fontSize = DesignTokens.FontSizeCaption;
            headerTechnicalIdText.color = DesignTokens.TextSecondary;
            headerTechnicalIdText.alignment = TextAlignmentOptions.MidlineLeft;
            headerTechnicalIdText.raycastTarget = false;
            SetRect(headerTechnicalIdText.rectTransform, Vector2.zero, Vector2.one, new Vector2(12f, 4f), new Vector2(-12f, -36f));
        }

        var scroll = transform.Find("Scroll_Detail") as RectTransform;
        if (scroll != null)
        {
            scroll.offsetMax = new Vector2(scroll.offsetMax.x, -88f);
        }

        var content = transform.Find("Scroll_Detail/Viewport/Content");
        if (content == null) return;

        var basicSection = EnsureSectionLabel(content, "Section_Basic", "\u57FA\u672C\u60C5\u5831");
        var prefabRow = content.Find("Row_PrefabLabel");
        if (prefabRow != null)
        {
            PlaceBefore(basicSection, prefabRow);
            var label = prefabRow.Find("Label")?.GetComponent<TMP_Text>();
            if (label != null) label.text = "\u6280\u8853ID";
        }

        var descriptionRow = content.Find("Row_Description");
        if (descriptionRow != null)
        {
            var descriptionSection = EnsureSectionLabel(content, "Section_Description", "\u8AAC\u660E");
            PlaceBefore(descriptionSection, descriptionRow);
        }

        var usageRow = content.Find(UsageRowName);
        if (usageRow != null)
        {
            var usageSection = EnsureSectionLabel(content, "Section_Usage", "\u4F7F\u7528\u4E2D\u306E\u6761\u4EF6");
            PlaceBefore(usageSection, usageRow);
        }
    }

    static void PlaceBefore(Transform item, Transform target)
    {
        if (item == null || target == null || item.parent != target.parent) return;
        int targetIndex = target.GetSiblingIndex();
        if (item.GetSiblingIndex() < targetIndex) targetIndex--;
        item.SetSiblingIndex(Mathf.Max(0, targetIndex));
    }

    static RectTransform EnsureSectionLabel(Transform content, string objectName, string value)
    {
        var section = content.Find(objectName) as RectTransform;
        if (section == null)
        {
            var go = new GameObject(objectName, typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
            section = go.GetComponent<RectTransform>();
            section.SetParent(content, false);
        }

        var text = section.GetComponent<TMP_Text>();
        text.text = value;
        text.fontSize = DesignTokens.FontSizeSubheading;
        text.color = DesignTokens.TextPrimary;
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.margin = new Vector4(16f, 8f, 16f, 0f);
        text.raycastTarget = false;

        var layout = section.GetComponent<LayoutElement>();
        layout.minHeight = 48f;
        layout.preferredHeight = 48f;
        return section;
    }

    static void SetRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        if (rect == null) return;
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
    }

    void OnDescriptionInputEndEdit(string value)
    {
        if (currentPo == null) return;

        currentPo.SetDescription(value);
        if (inputDescription != null)
        {
            inputDescription.SetTextWithoutNotify(currentPo.GetDescription());
        }
        if (textDescription != null)
        {
            textDescription.text = currentPo.GetDescription();
        }
    }

    void EnsureDescriptionInputField()
    {
        if (rowDescription == null)
        {
            var row = transform.Find("Scroll_Detail/Viewport/Content/Row_Description");
            if (row != null) rowDescription = row.gameObject;
        }

        if (inputDescription == null && rowDescription != null)
        {
            foreach (var input in rowDescription.GetComponentsInChildren<TMP_InputField>(true))
            {
                if (input != null && input.gameObject.name == "Input_Description")
                {
                    inputDescription = input;
                    break;
                }
            }
        }

        if (inputDescription == null && rowDescription != null)
        {
            inputDescription = CreateDescriptionInput(rowDescription.transform);
        }

        if (inputDescription != null)
        {
            ConfigureDescriptionInput(inputDescription);
            if (textDescription != null) textDescription.gameObject.SetActive(false);
        }
        else if (textDescription == null && rowDescription != null)
        {
            textDescription = rowDescription.GetComponentInChildren<TMP_Text>(true);
        }
    }

    static TMP_InputField CreateDescriptionInput(Transform row)
    {
        var inputGo = new GameObject("Input_Description", typeof(RectTransform), typeof(Image), typeof(TMP_InputField), typeof(LayoutElement));
        var inputRt = inputGo.GetComponent<RectTransform>();
        inputRt.SetParent(row, false);

        var input = inputGo.GetComponent<TMP_InputField>();
        input.lineType = TMP_InputField.LineType.MultiLineNewline;

        var image = inputGo.GetComponent<Image>();
        image.color = DesignTokens.BgPrimary;

        var layout = inputGo.GetComponent<LayoutElement>();
        layout.minHeight = DescriptionInputMinHeight;
        layout.preferredHeight = DescriptionInputMinHeight;

        var text = CreateInputText(inputRt, "Text", string.Empty, DesignTokens.TextPrimary);
        text.alignment = TextAlignmentOptions.TopLeft;

        var placeholder = CreateInputText(inputRt, "Placeholder", DescriptionPlaceholder, DesignTokens.TextTertiary);
        placeholder.alignment = TextAlignmentOptions.TopLeft;

        input.textComponent = text;
        input.placeholder = placeholder;
        return input;
    }

    static TMP_Text CreateInputText(RectTransform parent, string name, string value, Color color)
    {
        var textGo = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        var textRt = textGo.GetComponent<RectTransform>();
        textRt.SetParent(parent, false);
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.pivot = new Vector2(0.5f, 0.5f);
        textRt.offsetMin = new Vector2(8f, 8f);
        textRt.offsetMax = new Vector2(-8f, -8f);

        var text = textGo.GetComponent<TMP_Text>();
        text.fontSize = DesignTokens.FontSizeBody;
        text.color = color;
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.text = value;
        return text;
    }

    static void ConfigureDescriptionInput(TMP_InputField input)
    {
        if (input == null) return;

        input.lineType = TMP_InputField.LineType.MultiLineNewline;

        var image = input.GetComponent<Image>();
        if (image != null)
        {
            image.color = DesignTokens.BgPrimary;
        }

        var layout = input.GetComponent<LayoutElement>();
        if (layout == null) layout = input.gameObject.AddComponent<LayoutElement>();
        if (layout.minHeight < DescriptionInputMinHeight) layout.minHeight = DescriptionInputMinHeight;
        if (layout.preferredHeight < DescriptionInputMinHeight) layout.preferredHeight = DescriptionInputMinHeight;

        if (input.textComponent != null)
        {
            input.textComponent.alignment = TextAlignmentOptions.TopLeft;
            var textRt = input.textComponent.rectTransform;
            textRt.offsetMin = new Vector2(8f, 8f);
            textRt.offsetMax = new Vector2(-8f, -8f);
        }

        if (input.placeholder is TMP_Text placeholderText)
        {
            placeholderText.alignment = TextAlignmentOptions.TopLeft;
            var placeholderRt = placeholderText.rectTransform;
            placeholderRt.offsetMin = new Vector2(8f, 8f);
            placeholderRt.offsetMax = new Vector2(-8f, -8f);
        }
    }

    void EnsureUsageNodeSection()
    {
        var content = transform.Find("Scroll_Detail/Viewport/Content");
        if (content == null) return;

        var row = content.Find(UsageRowName);
        if (row == null)
        {
            row = CreateUsageRow(content);
        }

        EnsureUsageLabel(row);
        EnsureUsageEmptyText(row);
        EnsureUsageListRoot(row);
        EnsureUsageBlockTemplate();
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
            var tf = row.Find(UsageLabelName);
            if (tf != null) usageNodeLabelText = tf.GetComponent<TMP_Text>();
        }

        if (usageNodeLabelText == null)
        {
            var labelGo = new GameObject(UsageLabelName, typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
            var labelRt = labelGo.GetComponent<RectTransform>();
            labelRt.SetParent(row, false);
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

    void EnsureUsageEmptyText(Transform row)
    {
        if (textConditionUsage == null)
        {
            var tf = row.Find(UsageEmptyTextName);
            if (tf != null) textConditionUsage = tf.GetComponent<TMP_Text>();
        }

        if (textConditionUsage == null)
        {
            var textGo = new GameObject(UsageEmptyTextName, typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
            var textRt = textGo.GetComponent<RectTransform>();
            textRt.SetParent(row, false);
            textConditionUsage = textGo.GetComponent<TMP_Text>();
        }

        textConditionUsage.fontSize = DesignTokens.FontSizeBody;
        textConditionUsage.color = DesignTokens.TextPrimary;
        textConditionUsage.alignment = TextAlignmentOptions.TopLeft;
        textConditionUsage.text = UnusedLabel;

        var layout = textConditionUsage.GetComponent<LayoutElement>();
        if (layout == null) layout = textConditionUsage.gameObject.AddComponent<LayoutElement>();
        layout.minHeight = DesignTokens.FontSizeBody + 4f;
    }

    void EnsureUsageListRoot(Transform row)
    {
        if (usageNodeListRoot == null)
        {
            var tf = row.Find(UsageListName) as RectTransform;
            if (tf != null) usageNodeListRoot = tf;
        }

        if (usageNodeListRoot == null)
        {
            var listGo = new GameObject(UsageListName, typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            usageNodeListRoot = listGo.GetComponent<RectTransform>();
            usageNodeListRoot.SetParent(row, false);
        }

        var listLayout = usageNodeListRoot.GetComponent<VerticalLayoutGroup>();
        if (listLayout == null) listLayout = usageNodeListRoot.gameObject.AddComponent<VerticalLayoutGroup>();
        listLayout.childControlWidth = true;
        listLayout.childControlHeight = true;
        listLayout.childForceExpandWidth = true;
        listLayout.childForceExpandHeight = false;
        listLayout.spacing = UsageNodeBlockSpacing;
        listLayout.padding = new RectOffset(0, 0, 0, 0);

        var listFitter = usageNodeListRoot.GetComponent<ContentSizeFitter>();
        if (listFitter == null) listFitter = usageNodeListRoot.gameObject.AddComponent<ContentSizeFitter>();
        listFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
    }

    void EnsureUsageBlockTemplate()
    {
        if (usageNodeListRoot == null) return;

        if (usageNodeBlockTemplate == null)
        {
            var tf = usageNodeListRoot.Find(UsageTemplateName);
            if (tf != null) usageNodeBlockTemplate = tf.gameObject;
        }

        if (usageNodeBlockTemplate == null)
        {
            usageNodeBlockTemplate = CreateUsageBlockTemplate(usageNodeListRoot);
        }

        usageNodeBlockTemplate.SetActive(false);
    }

    static GameObject CreateUsageBlockTemplate(Transform parent)
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

    void UpdateConditionUsage(PlacedObject po, bool force)
    {
        if (po == null) return;
        EnsureUsageNodeSection();
        if (usageNodeListRoot == null || usageNodeBlockTemplate == null) return;

        po.EnsureHasId();
        var entries = string.IsNullOrWhiteSpace(po.id)
            ? new List<ConditionUsageEntry>()
            : CollectConditionUsageEntries(po.id);

        var signature = BuildUsageSignature(po.id, entries);
        if (!force && string.Equals(signature, currentUsageSignature, StringComparison.Ordinal)) return;
        currentUsageSignature = signature;

        RenderUsageBlocks(entries);
    }

    List<ConditionUsageEntry> CollectConditionUsageEntries(string objectId)
    {
        var results = new List<ConditionUsageEntry>();
        if (string.IsNullOrWhiteSpace(objectId)) return results;
        if (graphService == null || graphService.curriculum == null || graphService.curriculum.nodes == null) return results;

        foreach (var node in graphService.curriculum.nodes)
        {
            if (node == null || node.nodeType != ScenarioNodeType.Condition || node.condition == null) continue;

            bool useA = string.Equals(node.condition.objectAId, objectId, StringComparison.Ordinal);
            bool useB = string.Equals(node.condition.objectBId, objectId, StringComparison.Ordinal);
            if (!useA && !useB) continue;

            results.Add(new ConditionUsageEntry
            {
                node = node,
                objectAId = node.condition.objectAId,
                objectBId = node.condition.objectBId
            });
        }

        results.Sort((a, b) => string.CompareOrdinal(a.node != null ? a.node.nodeId : string.Empty, b.node != null ? b.node.nodeId : string.Empty));
        return results;
    }

    static string BuildUsageSignature(string selectedObjectId, List<ConditionUsageEntry> entries)
    {
        var sb = new StringBuilder();
        sb.Append(selectedObjectId ?? string.Empty);
        if (entries == null) return sb.ToString();

        foreach (var entry in entries)
        {
            if (entry == null) continue;
            sb.Append('|');
            sb.Append(entry.node != null ? entry.node.nodeId : string.Empty);
            sb.Append(':');
            sb.Append(entry.objectAId ?? string.Empty);
            sb.Append(':');
            sb.Append(entry.objectBId ?? string.Empty);
        }

        return sb.ToString();
    }

    static Dictionary<string, string> BuildObjectLabelMap()
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        var placedObjects = FindObjectsByType<PlacedObject>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (var placed in placedObjects)
        {
            if (placed == null) continue;
            placed.EnsureHasId();
            if (string.IsNullOrWhiteSpace(placed.id)) continue;
            map[placed.id] = placed.GetDisplayName();
        }

        return map;
    }

    void RenderUsageBlocks(List<ConditionUsageEntry> entries)
    {
        ClearUsageBlocks();

        if (entries == null || entries.Count == 0)
        {
            if (textConditionUsage != null)
            {
                textConditionUsage.gameObject.SetActive(true);
                textConditionUsage.text = UnusedLabel;
            }
            return;
        }

        if (textConditionUsage != null)
        {
            textConditionUsage.gameObject.SetActive(false);
        }

        var template = TryResolveConditionNodeTemplate();
        if (template == null)
        {
            var labelMap = BuildObjectLabelMap();
            foreach (var entry in entries)
            {
                RenderUsageFallbackTextBlock(entry, labelMap);
            }
            return;
        }

        int displayIndex = 1;
        foreach (var entry in entries)
        {
            if (entry == null || entry.node == null) continue;

            var block = Instantiate(template, usageNodeListRoot);
            block.name = $"UsageNodeBlock_{entry.node.nodeId}";
            block.gameObject.SetActive(true);

            ConfigureUsageConditionNode(block, entry.node, displayIndex);
            displayIndex++;
        }
    }

    ConditionNodeUI TryResolveConditionNodeTemplate()
    {
        if (usageConditionTemplateCache != null) return usageConditionTemplateCache;

        if (scenarioGraphUI == null)
        {
            scenarioGraphUI = FindFirstObjectByType<ScenarioGraphUI>();
        }

        if (scenarioGraphUI != null)
        {
            usageConditionTemplateCache = scenarioGraphUI.GetConditionNodeTemplateForExternalUse();
            if (usageConditionTemplateCache != null) return usageConditionTemplateCache;
        }

        var all = FindObjectsByType<ConditionNodeUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var ui in all)
        {
            if (ui == null) continue;
            if (usageNodeListRoot != null && ui.transform.IsChildOf(usageNodeListRoot)) continue;
            usageConditionTemplateCache = ui;
            return usageConditionTemplateCache;
        }

        return null;
    }

    void ConfigureUsageConditionNode(ConditionNodeUI usageNodeUi, ScenarioNode node, int displayIndex)
    {
        if (usageNodeUi == null || node == null) return;

        DisableNodeDragHandlers(usageNodeUi);
        ApplyUsageNodeLayout(usageNodeUi.transform as RectTransform);

        usageNodeUi.onClickOutputConnector = null;
        usageNodeUi.onBeginOutputConnectorDrag = null;
        usageNodeUi.onOutputConnectorDrag = null;
        usageNodeUi.onCompleteConnectorDrag = null;
        usageNodeUi.onCancelConnectorDrag = null;
        usageNodeUi.onClickDelete = OnUsageNodeDelete;
        usageNodeUi.onChanged = OnUsageNodeChanged;

        usageNodeUi.Bind(graphService, node);
        usageNodeUi.EnterEmbeddedMode(displayIndex);

        if (usageNodeStyler != null)
        {
            usageNodeStyler.Apply(usageNodeUi);
        }
    }

    void OnUsageNodeChanged()
    {
        if (scenarioGraphUI != null)
        {
            scenarioGraphUI.RebuildFromExternalChange();
        }

        currentUsageSignature = string.Empty;
        nextConditionUsageRefreshTime = 0f;
    }

    void OnUsageNodeDelete(string nodeId)
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

        currentUsageSignature = string.Empty;
        nextConditionUsageRefreshTime = 0f;
        if (currentPo != null)
        {
            UpdateConditionUsage(currentPo, force: true);
        }
    }

    static void ApplyUsageNodeLayout(RectTransform usageNodeRt)
    {
        if (usageNodeRt == null) return;

        usageNodeRt.anchorMin = new Vector2(0.5f, 1f);
        usageNodeRt.anchorMax = new Vector2(0.5f, 1f);
        usageNodeRt.pivot = new Vector2(0.5f, 1f);
        usageNodeRt.anchoredPosition = Vector2.zero;

        float height = usageNodeRt.rect.height > 1f
            ? usageNodeRt.rect.height
            : (usageNodeRt.sizeDelta.y > 1f ? usageNodeRt.sizeDelta.y : UsageConditionFallbackHeight);

        var layout = usageNodeRt.GetComponent<LayoutElement>();
        if (layout == null) layout = usageNodeRt.gameObject.AddComponent<LayoutElement>();
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
            if (dragHandler == null) continue;
            dragHandler.enabled = false;
        }
    }

    void RenderUsageFallbackTextBlock(ConditionUsageEntry entry, Dictionary<string, string> objectLabelMap)
    {
        if (entry == null || usageNodeBlockTemplate == null || usageNodeListRoot == null) return;

        var block = Instantiate(usageNodeBlockTemplate, usageNodeListRoot);
        string nodeId = entry.node != null ? entry.node.nodeId : "unknown";
        block.name = $"UsageNodeBlock_{nodeId}";
        block.SetActive(true);

        var body = block.transform.Find(UsageBlockBodyName)?.GetComponent<TMP_Text>();
        if (body == null)
        {
            body = block.GetComponentInChildren<TMP_Text>(true);
        }
        if (body == null) return;

        string aName = ResolveObjectLabel(entry.objectAId, objectLabelMap);
        string bName = ResolveObjectLabel(entry.objectBId, objectLabelMap);
        body.text = $"{aName}{DescriptionPhraseMiddle}\n{bName}{DescriptionPhraseSuffix}";
    }

    void ClearUsageBlocks()
    {
        if (usageNodeListRoot == null) return;

        for (int i = usageNodeListRoot.childCount - 1; i >= 0; i--)
        {
            var child = usageNodeListRoot.GetChild(i);
            if (child == null) continue;
            if (usageNodeBlockTemplate != null && child == usageNodeBlockTemplate.transform) continue;
            Destroy(child.gameObject);
        }
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

    IEnumerator SlideIn()
    {
        float panelWidth = restOffsetMax.x - restOffsetMin.x;
        float elapsed = 0f;

        rt.offsetMin = new Vector2(restOffsetMin.x + panelWidth, restOffsetMin.y);
        rt.offsetMax = new Vector2(restOffsetMax.x + panelWidth, restOffsetMax.y);
        if (panelCanvasGroup != null)
        {
            panelCanvasGroup.alpha = 0f;
            panelCanvasGroup.interactable = true;
            panelCanvasGroup.blocksRaycasts = true;
        }

        while (elapsed < SlideDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / SlideDuration);
            float eased = 1f - (1f - t) * (1f - t);

            float shift = Mathf.Lerp(panelWidth, 0f, eased);
            rt.offsetMin = new Vector2(restOffsetMin.x + shift, restOffsetMin.y);
            rt.offsetMax = new Vector2(restOffsetMax.x + shift, restOffsetMax.y);
            if (panelCanvasGroup != null) panelCanvasGroup.alpha = eased;
            yield return null;
        }

        rt.offsetMin = restOffsetMin;
        rt.offsetMax = restOffsetMax;
        if (panelCanvasGroup != null) panelCanvasGroup.alpha = 1f;
        slideCoroutine = null;
    }

    IEnumerator SlideOut()
    {
        float panelWidth = restOffsetMax.x - restOffsetMin.x;
        float elapsed = 0f;
        float startShift = rt.offsetMin.x - restOffsetMin.x;
        float startAlpha = panelCanvasGroup != null ? panelCanvasGroup.alpha : 1f;
        if (panelCanvasGroup != null)
        {
            panelCanvasGroup.interactable = false;
            panelCanvasGroup.blocksRaycasts = false;
        }

        while (elapsed < SlideDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / SlideDuration);
            float eased = t * t;

            float shift = Mathf.Lerp(startShift, panelWidth, eased);
            rt.offsetMin = new Vector2(restOffsetMin.x + shift, restOffsetMin.y);
            rt.offsetMax = new Vector2(restOffsetMax.x + shift, restOffsetMax.y);
            if (panelCanvasGroup != null) panelCanvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, eased);
            yield return null;
        }

        rt.offsetMin = restOffsetMin;
        rt.offsetMax = restOffsetMax;
        if (panelCanvasGroup != null) panelCanvasGroup.alpha = 1f;
        slideCoroutine = null;
        gameObject.SetActive(false);
    }
}
