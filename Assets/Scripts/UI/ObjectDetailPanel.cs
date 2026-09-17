using System.Collections;
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
    ObjectConditionReferencePresenter conditionReferencePresenter;
    RectTransform rt;
    CanvasGroup panelCanvasGroup;
    UiPanelDockSync panelDockSync;
    PlacedObject currentPo;

    Vector2 restOffsetMin;
    Vector2 restOffsetMax;
    Coroutine slideCoroutine;
    bool isShown;

    const float SlideDuration = 0.2f;
    const float DescriptionInputMinHeight = 96f;
    const string UsageRowName = "Row_ConditionUsage";
    const string DescriptionPlaceholder = "\u8AAC\u660E\u3092\u5165\u529B...";

    void Start()
    {
        rt = (RectTransform)transform;
        EnsurePolishedHierarchy();
        restOffsetMin = rt.offsetMin;
        restOffsetMax = rt.offsetMax;
        panelCanvasGroup = GetComponent<CanvasGroup>();
        if (panelCanvasGroup == null) panelCanvasGroup = gameObject.AddComponent<CanvasGroup>();
        panelCanvasGroup.alpha = 1f;

        ResolveRuntimeReferences();

        if (inputObjectName != null)
        {
            inputObjectName.onEndEdit.RemoveListener(OnNameInputEndEdit);
            inputObjectName.onEndEdit.AddListener(OnNameInputEndEdit);
        }

        EnsureDescriptionInputField();
        conditionReferencePresenter = new ObjectConditionReferencePresenter(
            transform,
            usageNodeLabelText,
            textConditionUsage,
            usageNodeListRoot,
            usageNodeBlockTemplate,
            usageNodeStyler);
        conditionReferencePresenter.SetServices(graphService, scenarioGraphUI);
        conditionReferencePresenter.EnsureHierarchy();
        EnsurePolishedHierarchy();
        if (usageNodeStyler == null)
        {
            usageNodeStyler = GetComponent<ObjectDetailConditionNodeStyler>();
        }
        conditionReferencePresenter.SetStyler(usageNodeStyler);

        if (inputDescription != null)
        {
            inputDescription.onEndEdit.RemoveListener(OnDescriptionInputEndEdit);
            inputDescription.onEndEdit.AddListener(OnDescriptionInputEndEdit);
        }

        DesignTokenApplier.ApplyDetailPanel(transform);
        // Keep this component alive while hidden so delayed/replaced edit services can be rebound.
        SetHiddenImmediately();
        SyncSelection();
    }

    void Update()
    {
        ResolveRuntimeReferences();
        SyncSelection();
        conditionReferencePresenter?.SetServices(graphService, scenarioGraphUI);
        conditionReferencePresenter?.Tick(isShown, Time.unscaledTime);
    }

    void OnDestroy()
    {
        if (panelDockSync != null)
        {
            panelDockSync.SetDetailPanelVisibleWidth(0f);
        }

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

    void ResolveRuntimeReferences()
    {
        var selection = FindFirstObjectByType<SelectionService>();
        if (selection != selectionService)
        {
            if (selectionService != null)
            {
                selectionService.OnSelectionChanged -= OnSelectionChanged;
            }

            selectionService = selection;
            if (selectionService != null)
            {
                selectionService.OnSelectionChanged -= OnSelectionChanged;
                selectionService.OnSelectionChanged += OnSelectionChanged;
            }
        }

        if (catalogUI == null) catalogUI = FindFirstObjectByType<CatalogUI>();
        if (graphService == null) graphService = FindFirstObjectByType<CurriculumGraphService>();
        if (scenarioGraphUI == null) scenarioGraphUI = FindFirstObjectByType<ScenarioGraphUI>();
        if (panelDockSync == null) panelDockSync = transform.root.GetComponent<UiPanelDockSync>();
    }

    void SyncSelection()
    {
        if (selectionService == null) return;
        if (currentPo == selectionService.Current) return;

        OnSelectionChanged(selectionService.Current);
    }

    void OnSelectionChanged(PlacedObject po)
    {
        if (po == null)
        {
            currentPo = null;
            conditionReferencePresenter?.ClearSelection();
            if (isShown)
            {
                isShown = false;
                if (slideCoroutine != null) StopCoroutine(slideCoroutine);
                slideCoroutine = StartCoroutine(SlideOut());
            }
            return;
        }

        currentPo = po;
        Populate(po);

        if (!isShown)
        {
            isShown = true;
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

        conditionReferencePresenter?.Select(po);
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
        rt.offsetMax = new Vector2(rt.offsetMax.x, 0f);

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

    IEnumerator SlideIn()
    {
        float panelWidth = restOffsetMax.x - restOffsetMin.x;
        float elapsed = 0f;
        float startShift = rt.offsetMin.x - restOffsetMin.x;
        float startAlpha = panelCanvasGroup != null ? panelCanvasGroup.alpha : 0f;

        if (panelCanvasGroup != null)
        {
            panelCanvasGroup.interactable = true;
            panelCanvasGroup.blocksRaycasts = true;
        }

        while (elapsed < SlideDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / SlideDuration);
            float eased = 1f - (1f - t) * (1f - t);

            float shift = Mathf.Lerp(startShift, 0f, eased);
            rt.offsetMin = new Vector2(restOffsetMin.x + shift, restOffsetMin.y);
            rt.offsetMax = new Vector2(restOffsetMax.x + shift, restOffsetMax.y);
            UpdateGlobalButtonDock(panelWidth, shift);
            if (panelCanvasGroup != null) panelCanvasGroup.alpha = Mathf.Lerp(startAlpha, 1f, eased);
            yield return null;
        }

        rt.offsetMin = restOffsetMin;
        rt.offsetMax = restOffsetMax;
        UpdateGlobalButtonDock(panelWidth, 0f);
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
            UpdateGlobalButtonDock(panelWidth, shift);
            if (panelCanvasGroup != null) panelCanvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, eased);
            yield return null;
        }

        SetHiddenImmediately();
        slideCoroutine = null;
    }

    void SetHiddenImmediately()
    {
        float panelWidth = restOffsetMax.x - restOffsetMin.x;
        rt.offsetMin = new Vector2(restOffsetMin.x + panelWidth, restOffsetMin.y);
        rt.offsetMax = new Vector2(restOffsetMax.x + panelWidth, restOffsetMax.y);
        UpdateGlobalButtonDock(panelWidth, panelWidth);

        if (panelCanvasGroup != null)
        {
            panelCanvasGroup.alpha = 0f;
            panelCanvasGroup.interactable = false;
            panelCanvasGroup.blocksRaycasts = false;
        }

        isShown = false;
    }

    void UpdateGlobalButtonDock(float panelWidth, float panelShift)
    {
        if (panelDockSync == null)
        {
            panelDockSync = transform.root.GetComponent<UiPanelDockSync>();
        }

        if (panelDockSync == null) return;
        panelDockSync.SetDetailPanelVisibleWidth(Mathf.Clamp(panelWidth - panelShift, 0f, panelWidth));
    }
}
