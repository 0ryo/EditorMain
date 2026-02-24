using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public static class BuildUiPrefabs
{
    const string UiPrefabDir = "Assets/UI/Prefabs";
    const string UiRootPrefabPath = UiPrefabDir + "/UIRoot.prefab";

    [MenuItem("Tools/Automation/Build UI Prefabs")]
    public static void Build()
    {
        Directory.CreateDirectory(UiPrefabDir);

        var root = new GameObject("UIRoot");
        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(2560f, 1440f);
        root.AddComponent<GraphicRaycaster>();

        var catalogPanel = BuildCatalogPanel(root.transform);
        var scenarioPanel = BuildScenarioPanel(root.transform);
        BuildDockSync(root, catalogPanel, scenarioPanel);
        BuildEventSystem(root.transform);

        PrefabUtility.SaveAsPrefabAsset(root, UiRootPrefabPath);
        Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[BuildUiPrefabs] Saved: " + UiRootPrefabPath);
    }

    static void BuildEventSystem(Transform parent)
    {
        var eventGo = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        eventGo.transform.SetParent(parent, false);
    }

    static RectTransform BuildCatalogPanel(Transform parent)
    {
        var panel = CreateUiRect("Panel_Catalog", parent);
        SetRect(panel, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0f), new Vector2(288f, 0f));
        panel.gameObject.AddComponent<Image>().color = DesignTokens.BgPrimary;

        var header = CreateUiRect("Header", panel);
        SetRect(header, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(10f, -48f), new Vector2(-10f, -10f));
        header.gameObject.AddComponent<Image>().color = DesignTokens.Surface;

        var title = CreateText("Title", header, "\u30AA\u30D6\u30B8\u30A7\u30AF\u30C8\u4E00\u89A7");
        title.fontSize = 16;
        title.alignment = TextAnchor.MiddleLeft;
        SetRect(title.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(10f, 0f), new Vector2(-52f, 0f));

        var addButton = CreateButton("Button_AddObject", header, "\uFF0B");
        SetRect(addButton.GetComponent<RectTransform>(), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-38f, -14f), new Vector2(-8f, 14f));
        addButton.GetComponent<Image>().color = DesignTokens.BgSecondary;

        var searchRow = CreateUiRect("SearchRow", panel);
        SetRect(searchRow, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(10f, -44f), new Vector2(-10f, -8f));
        searchRow.gameObject.AddComponent<Image>().color = DesignTokens.BgPrimary;
        var searchInput = CreateInputField("Input_Search", searchRow, "\u30AA\u30D6\u30B8\u30A7\u30AF\u30C8\u3092\u691C\u7D22...");
        SetRect(searchInput.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, new Vector2(8f, 4f), new Vector2(-8f, -4f));

        var statusText = CreateText("Text_Status", panel, "");
        statusText.fontSize = 12;
        statusText.color = DesignTokens.TextSecondary;
        SetRect(statusText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(14f, -66f), new Vector2(-14f, -46f));

        var resizeHandle = CreateUiRect("ResizeHandleX", panel);
        SetRect(resizeHandle, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(-6f, 0f), new Vector2(6f, 0f));
        resizeHandle.gameObject.AddComponent<Image>().color = DesignTokens.BgPrimary;
        var resize = resizeHandle.gameObject.AddComponent<PanelHorizontalResizeHandle>();
        resize.targetPanel = panel;

        var scroll = CreateUiRect("Scroll_Catalog", panel);
        SetRect(scroll, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(8f, 8f), new Vector2(-8f, -72f));
        scroll.gameObject.AddComponent<Image>().color = DesignTokens.Surface;
        var scrollRect = scroll.gameObject.AddComponent<ScrollRect>();

        var viewport = CreateUiRect("Viewport", scroll);
        SetRect(viewport, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        viewport.gameObject.AddComponent<Image>().color = DesignTokens.Surface;
        viewport.gameObject.AddComponent<Mask>().showMaskGraphic = false;

        var content = CreateUiRect("Content", viewport);
        SetRect(content, new Vector2(0f, 1f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
        var vLayout = content.gameObject.AddComponent<VerticalLayoutGroup>();
        vLayout.spacing = DesignTokens.SpaceSm;
        vLayout.padding = new RectOffset((int)DesignTokens.SpaceSm, (int)DesignTokens.SpaceSm, (int)DesignTokens.SpaceSm, (int)DesignTokens.SpaceSm);
        vLayout.childAlignment = TextAnchor.UpperLeft;
        vLayout.childControlWidth = true;
        vLayout.childControlHeight = true;
        vLayout.childForceExpandHeight = false;
        content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scrollRect.viewport = viewport;
        scrollRect.content = content;
        scrollRect.horizontal = false;

        var cardTemplate = CreateUiRect("Card_Template", content);
        SetRect(cardTemplate, new Vector2(0f, 1f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
        cardTemplate.gameObject.AddComponent<Image>().color = DesignTokens.BgSecondary;
        var cardButton = cardTemplate.gameObject.AddComponent<Button>();
        var cardLayout = cardTemplate.gameObject.AddComponent<LayoutElement>();
        cardLayout.minHeight = 84f;
        cardLayout.preferredHeight = 84f;

        var thumb = CreateUiRect("Thumbnail", cardTemplate);
        SetRect(thumb, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(10f, -22f), new Vector2(54f, 22f));
        thumb.gameObject.AddComponent<Image>().color = DesignTokens.BgSecondary;

        var labelMain = CreateText("LabelMain", cardTemplate, "Item");
        labelMain.fontSize = 14;
        labelMain.alignment = TextAnchor.MiddleLeft;
        SetRect(labelMain.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(64f, 0f), new Vector2(-10f, 0f));

        cardTemplate.gameObject.SetActive(false);

        var catalogUi = panel.gameObject.AddComponent<CatalogUI>();
        var catalogSo = new SerializedObject(catalogUi);
        catalogSo.FindProperty("content").objectReferenceValue = content;
        catalogSo.FindProperty("buttonTemplate").objectReferenceValue = cardButton;
        catalogSo.FindProperty("searchInput").objectReferenceValue = searchInput;
        catalogSo.FindProperty("addButton").objectReferenceValue = addButton;
        catalogSo.FindProperty("statusText").objectReferenceValue = statusText;
        var catalogCornerProp = catalogSo.FindProperty("cornerRadius");
        if (catalogCornerProp != null) catalogCornerProp.floatValue = DesignTokens.CornerRadius;
        catalogSo.ApplyModifiedPropertiesWithoutUndo();

        return panel;
    }

    static RectTransform BuildScenarioPanel(Transform parent)
    {
        var panel = CreateUiRect("Panel_ScenarioGraph", parent);
        SetRect(panel, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(288f, 0f), new Vector2(0f, 300f));
        panel.gameObject.AddComponent<Image>().color = DesignTokens.BgPrimary;

        var topBar = CreateUiRect("TopBar", panel);
        SetRect(topBar, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(12f, -44f), new Vector2(-12f, -8f));
        topBar.gameObject.AddComponent<Image>().color = DesignTokens.BgPrimary;
        var topLayout = topBar.gameObject.AddComponent<HorizontalLayoutGroup>();
        topLayout.spacing = 10f;
        topLayout.childControlWidth = true;
        topLayout.childControlHeight = true;

        var projectInput = CreateInputField("Input_ProjectName", topBar, "ProjectName");
        projectInput.gameObject.AddComponent<LayoutElement>().minWidth = 360f;

        var addStepButton = CreateButton("Button_AddStep", topBar, "+ Step");
        addStepButton.gameObject.AddComponent<LayoutElement>().minWidth = 120f;

        var addConditionButton = CreateButton("Button_AddCondition", topBar, "+ Condition");
        addConditionButton.gameObject.AddComponent<LayoutElement>().minWidth = 150f;

        var saveButton = CreateButton("Button_SaveCurriculum", topBar, "Save");
        saveButton.gameObject.AddComponent<LayoutElement>().minWidth = 110f;

        var status = CreateText("Text_Status", topBar, "");
        status.alignment = TextAnchor.MiddleLeft;
        status.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;

        var nodeArea = CreateUiRect("NodeArea", panel);
        SetRect(nodeArea, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(12f, 8f), new Vector2(-12f, -52f));
        nodeArea.gameObject.AddComponent<Image>().color = DesignTokens.Surface;
        nodeArea.gameObject.AddComponent<RectMask2D>();

        var graphContent = CreateUiRect("GraphContent", nodeArea);
        graphContent.anchorMin = new Vector2(0.5f, 0.5f);
        graphContent.anchorMax = new Vector2(0.5f, 0.5f);
        graphContent.pivot = new Vector2(0.5f, 0.5f);
        graphContent.sizeDelta = new Vector2(4200f, 2400f);
        graphContent.anchoredPosition = Vector2.zero;

        var panZoom = nodeArea.gameObject.AddComponent<NodeAreaPanZoomController>();
        panZoom.Configure(nodeArea, graphContent);

        var lineLayer = CreateUiRect("LineLayer", graphContent);
        SetRect(lineLayer, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        var lineLayerImage = lineLayer.gameObject.AddComponent<Image>();
        lineLayerImage.color = new Color(0f, 0f, 0f, 0f);
        lineLayerImage.raycastTarget = false;

        var lineTemplateRect = CreateUiRect("LineTemplate", lineLayer);
        SetRect(lineTemplateRect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        lineTemplateRect.gameObject.AddComponent<CanvasRenderer>();
        var lineTemplate = lineTemplateRect.gameObject.AddComponent<ConnectionLineGraphic>();
        lineTemplate.raycastTarget = false;
        lineTemplateRect.gameObject.SetActive(false);

        var nodeTemplate = BuildStepNodeTemplate(graphContent);

        var resizeHandle = CreateUiRect("ResizeHandle", panel);
        SetRect(resizeHandle, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -6f), new Vector2(0f, 6f));
        resizeHandle.gameObject.AddComponent<Image>().color = DesignTokens.BgPrimary;
        var resizeComp = resizeHandle.gameObject.AddComponent<PanelVerticalResizeHandle>();
        resizeComp.targetPanel = panel;

        var scenarioUi = panel.gameObject.AddComponent<ScenarioGraphUI>();
        var scenarioSo = new SerializedObject(scenarioUi);
        scenarioSo.FindProperty("panelRoot").objectReferenceValue = panel;
        scenarioSo.FindProperty("projectNameInput").objectReferenceValue = projectInput;
        scenarioSo.FindProperty("addStepButton").objectReferenceValue = addStepButton;
        var addConditionProp = scenarioSo.FindProperty("addConditionButton");
        if (addConditionProp != null) addConditionProp.objectReferenceValue = addConditionButton;
        scenarioSo.FindProperty("saveButton").objectReferenceValue = saveButton;
        scenarioSo.FindProperty("statusText").objectReferenceValue = status;
        scenarioSo.FindProperty("nodeArea").objectReferenceValue = nodeArea;
        var graphContentProp = scenarioSo.FindProperty("graphContent");
        if (graphContentProp != null) graphContentProp.objectReferenceValue = graphContent;
        scenarioSo.FindProperty("lineLayer").objectReferenceValue = lineLayer;
        var stepTemplateProp = scenarioSo.FindProperty("stepNodeTemplate");
        if (stepTemplateProp != null) stepTemplateProp.objectReferenceValue = nodeTemplate;
        var legacyNodeTemplateProp = scenarioSo.FindProperty("nodeTemplate");
        if (legacyNodeTemplateProp != null) legacyNodeTemplateProp.objectReferenceValue = nodeTemplate;
        scenarioSo.FindProperty("lineTemplate").objectReferenceValue = lineTemplate;
        scenarioSo.FindProperty("resizeHandle").objectReferenceValue = resizeComp;
        var scenarioCornerProp = scenarioSo.FindProperty("cornerRadius");
        if (scenarioCornerProp != null) scenarioCornerProp.floatValue = DesignTokens.CornerRadius;
        scenarioSo.ApplyModifiedPropertiesWithoutUndo();

        return panel;
    }

    static void BuildDockSync(GameObject root, RectTransform catalogPanel, RectTransform scenarioPanel)
    {
        var sync = root.AddComponent<UiPanelDockSync>();
        sync.catalogPanel = catalogPanel;
        sync.scenarioPanel = scenarioPanel;
        sync.gap = 0f;
    }

    static StepNodeUI BuildStepNodeTemplate(Transform parent)
    {
        var root = CreateUiRect("StepNodeTemplate", parent);
        root.sizeDelta = new Vector2(390f, 180f);
        root.gameObject.AddComponent<Image>().color = DesignTokens.Surface;
        var stepNode = root.gameObject.AddComponent<StepNodeUI>();

        var stepId = CreateText("Text_StepId", root, "step-0000");
        SetRect(stepId.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(12f, -24f), new Vector2(-44f, -4f));

        var conditionSummary = CreateText("Text_ConditionSummary", root, "\u6761\u4EF6: 0");
        conditionSummary.fontSize = 12;
        conditionSummary.alignment = TextAnchor.MiddleLeft;
        conditionSummary.color = DesignTokens.TextSecondary;
        SetRect(conditionSummary.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(12f, -42f), new Vector2(-44f, -22f));

        var dragHandle = CreateUiRect("DragHandle", root);
        SetRect(dragHandle, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -28f), new Vector2(0f, 0f));
        dragHandle.gameObject.AddComponent<Image>().color = DesignTokens.BgSecondary;
        var drag = dragHandle.gameObject.AddComponent<NodeDragHandler>();
        drag.target = root;

        var warning = CreateText("Warning", root, "!");
        warning.fontSize = 18;
        warning.color = DesignTokens.Warning;
        warning.alignment = TextAnchor.MiddleCenter;
        SetRect(warning.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-56f, -30f), new Vector2(-36f, -10f));

        var deleteButton = CreateButton("Button_Delete", dragHandle, "X");
        SetRect(deleteButton.GetComponent<RectTransform>(), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-30f, -11f), new Vector2(-8f, 11f));
        deleteButton.GetComponent<Image>().color = DesignTokens.BgTertiary;
        var deleteLabel = deleteButton.GetComponentInChildren<Text>(true);
        if (deleteLabel != null)
        {
            deleteLabel.color = DesignTokens.TextPrimary;
            deleteLabel.fontSize = 14;
        }

        var title = CreateInputField("Input_Title", root, "\u30BF\u30A4\u30C8\u30EB");
        SetRect(title.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(12f, -60f), new Vector2(-12f, -30f));

        var inputConnector = CreateButton("InputConnector", root, "");
        SetRect(inputConnector.GetComponent<RectTransform>(), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(-14f, -12f), new Vector2(10f, 12f));
        inputConnector.GetComponent<Image>().color = DesignTokens.Accent;

        var outputConnector = CreateButton("OutputConnector", root, "");
        SetRect(outputConnector.GetComponent<RectTransform>(), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-10f, -12f), new Vector2(14f, 12f));
        outputConnector.GetComponent<Image>().color = DesignTokens.Accent;

        var conditionList = CreateUiRect("ConditionList", root);
        SetRect(conditionList, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(16f, 30f), new Vector2(-16f, 106f));
        var conditionLayout = conditionList.gameObject.AddComponent<VerticalLayoutGroup>();
        conditionLayout.spacing = 8f;
        conditionLayout.childControlHeight = true;
        conditionLayout.childControlWidth = true;

        var rowTemplate = BuildConditionRowTemplate(conditionList);

        stepNode.stepIdText = stepId;
        stepNode.conditionSummaryText = conditionSummary;
        stepNode.titleInput = title;
        stepNode.warningIcon = warning.gameObject;
        stepNode.inputConnector = inputConnector;
        stepNode.outputConnector = outputConnector;
        stepNode.deleteButton = deleteButton;
        stepNode.conditionListRoot = conditionList;
        stepNode.conditionRowTemplate = rowTemplate;

        root.gameObject.SetActive(false);
        rowTemplate.gameObject.SetActive(false);
        return stepNode;
    }

    static ConditionRowUI BuildConditionRowTemplate(Transform parent)
    {
        var row = CreateUiRect("ConditionRowTemplate", parent);
        row.sizeDelta = new Vector2(0f, 76f);
        row.gameObject.AddComponent<Image>().color = DesignTokens.BgPrimary;
        row.gameObject.AddComponent<LayoutElement>().minHeight = 76f;

        var rowLayout = row.gameObject.AddComponent<VerticalLayoutGroup>();
        rowLayout.spacing = 8f;
        rowLayout.childControlHeight = true;
        rowLayout.childControlWidth = true;

        var lineA = CreateUiRect("LineA", row);
        var lineALayout = lineA.gameObject.AddComponent<HorizontalLayoutGroup>();
        lineALayout.spacing = 10f;
        lineALayout.childControlHeight = true;
        lineALayout.childControlWidth = true;

        var lineB = CreateUiRect("LineB", row);
        var lineBLayout = lineB.gameObject.AddComponent<HorizontalLayoutGroup>();
        lineBLayout.spacing = 10f;
        lineBLayout.childControlHeight = true;
        lineBLayout.childControlWidth = true;

        var dropdownA = CreateDropdown("DropdownA", lineA);
        dropdownA.GetComponent<Image>().color = DesignTokens.BgSecondary;
        StyleConditionDropdown(dropdownA);
        dropdownA.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
        var textA = CreateText("Text_AfterA", lineA, "\u3092");
        textA.gameObject.AddComponent<LayoutElement>().minWidth = 24f;

        var dropdownB = CreateDropdown("DropdownB", lineB);
        dropdownB.GetComponent<Image>().color = DesignTokens.BgSecondary;
        StyleConditionDropdown(dropdownB);
        dropdownB.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
        var textB = CreateText("Text_AfterB", lineB, "\u306B\u8FD1\u3065\u3051\u305F\u3089");
        textB.gameObject.AddComponent<LayoutElement>().minWidth = 96f;

        var ui = row.gameObject.AddComponent<ConditionRowUI>();
        ui.dropdownA = dropdownA;
        ui.dropdownB = dropdownB;
        ui.textAfterA = textA;
        ui.textAfterB = textB;
        return ui;
    }

    static Dropdown CreateDropdown(string name, Transform parent)
    {
        var root = CreateUiRect(name, parent);
        root.gameObject.AddComponent<Image>().color = DesignTokens.BgSecondary;
        var dropdown = root.gameObject.AddComponent<Dropdown>();

        var caption = CreateText("Caption", root, "\u672A\u8A2D\u5B9A");
        SetRect(caption.rectTransform, Vector2.zero, Vector2.one, new Vector2(8f, 0f), new Vector2(-22f, 0f));

        var template = CreateUiRect("Template", root);
        SetRect(template, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, -120f), new Vector2(0f, 0f));
        template.pivot = new Vector2(0.5f, 1f);
        template.anchoredPosition = Vector2.zero;
        template.gameObject.AddComponent<Image>().color = DesignTokens.Surface;
        var scroll = template.gameObject.AddComponent<ScrollRect>();
        template.gameObject.SetActive(false);

        var viewport = CreateUiRect("Viewport", template);
        SetRect(viewport, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        viewport.gameObject.AddComponent<Image>().color = DesignTokens.Surface;
        viewport.gameObject.AddComponent<Mask>().showMaskGraphic = false;

        var content = CreateUiRect("Content", viewport);
        SetRect(content, new Vector2(0f, 1f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
        var contentLayout = content.gameObject.AddComponent<VerticalLayoutGroup>();
        contentLayout.childForceExpandHeight = false;
        contentLayout.childControlHeight = true;
        content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var item = CreateUiRect("Item", content);
        item.sizeDelta = new Vector2(0f, 24f);
        item.gameObject.AddComponent<Image>().color = DesignTokens.BgSecondary;
        item.gameObject.AddComponent<Toggle>();
        var itemLayout = item.gameObject.AddComponent<LayoutElement>();
        itemLayout.minHeight = 24f;
        itemLayout.preferredHeight = 24f;
        var itemLabel = CreateText("Item Label", item, "Option");
        SetRect(itemLabel.rectTransform, Vector2.zero, Vector2.one, new Vector2(8f, 0f), new Vector2(-8f, 0f));

        scroll.viewport = viewport;
        scroll.content = content;
        scroll.horizontal = false;

        dropdown.targetGraphic = root.GetComponent<Image>();
        dropdown.captionText = caption;
        dropdown.template = template;
        dropdown.itemText = itemLabel;
        dropdown.options = new System.Collections.Generic.List<Dropdown.OptionData>
        {
            new Dropdown.OptionData("\u672A\u8A2D\u5B9A")
        };
        return dropdown;
    }

    static void StyleConditionDropdown(Dropdown dropdown)
    {
        if (dropdown == null) return;

        if (dropdown.captionText != null)
        {
            dropdown.captionText.color = DesignTokens.TextPrimary;
        }
    }

    static InputField CreateInputField(string name, Transform parent, string placeholderText)
    {
        var root = CreateUiRect(name, parent);
        root.gameObject.AddComponent<Image>().color = DesignTokens.BgPrimary;
        var input = root.gameObject.AddComponent<InputField>();

        var text = CreateText("Text", root, "");
        SetRect(text.rectTransform, Vector2.zero, Vector2.one, new Vector2(8f, 0f), new Vector2(-8f, 0f));

        var placeholder = CreateText("Placeholder", root, placeholderText);
        placeholder.color = DesignTokens.TextTertiary;
        SetRect(placeholder.rectTransform, Vector2.zero, Vector2.one, new Vector2(8f, 0f), new Vector2(-8f, 0f));

        input.textComponent = text;
        input.placeholder = placeholder;
        return input;
    }

    static Button CreateButton(string name, Transform parent, string label)
    {
        var root = CreateUiRect(name, parent);
        root.gameObject.AddComponent<Image>().color = DesignTokens.BgSecondary;
        var button = root.gameObject.AddComponent<Button>();

        var text = CreateText("Label", root, label);
        text.alignment = TextAnchor.MiddleCenter;
        SetRect(text.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        return button;
    }

    static Text CreateText(string name, Transform parent, string value)
    {
        var textGo = new GameObject(name, typeof(RectTransform), typeof(Text));
        textGo.transform.SetParent(parent, false);
        var text = textGo.GetComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.color = DesignTokens.TextPrimary;
        text.fontSize = 14;
        text.alignment = TextAnchor.MiddleLeft;
        text.text = value;
        return text;
    }

    static RectTransform CreateUiRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go.GetComponent<RectTransform>();
    }

    static void SetRect(RectTransform rt, Vector2 min, Vector2 max, Vector2 offsetMin, Vector2 offsetMax)
    {
        rt.anchorMin = min;
        rt.anchorMax = max;
        rt.offsetMin = offsetMin;
        rt.offsetMax = offsetMax;
    }
}
