using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

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
        BuildDetailPanel(root.transform);
        var editModeRow = root.transform.Find("EditModeRow") as RectTransform;
        var settingsButton = root.transform.Find("Button_Settings") as RectTransform;
        BuildDockSync(root, catalogPanel, scenarioPanel, editModeRow, settingsButton);

        PrefabUtility.SaveAsPrefabAsset(root, UiRootPrefabPath);
        Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[BuildUiPrefabs] Saved: " + UiRootPrefabPath);
    }

    static RectTransform BuildCatalogPanel(Transform parent)
    {
        var panel = CreateUiRect("Panel_Catalog", parent);
        SetRect(panel, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0f), new Vector2(288f, 0f));
        panel.gameObject.AddComponent<Image>().color = DesignTokens.BgPrimary;

        var editModeRow = CreateUiRect("EditModeRow", parent);
        SetRect(editModeRow, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(300f, -52f), new Vector2(536f, -12f));
        editModeRow.pivot = new Vector2(0f, 1f);
        var editModeLayout = editModeRow.gameObject.AddComponent<HorizontalLayoutGroup>();
        editModeLayout.spacing = 8f;
        editModeLayout.childControlWidth = true;
        editModeLayout.childControlHeight = true;
        editModeLayout.childForceExpandWidth = true;
        editModeLayout.childForceExpandHeight = true;
        editModeLayout.padding = new RectOffset(0, 0, 0, 0);

        var browseModeButton = CreateButton("Button_ModeBrowse", editModeRow, "\u95b2\u89a7");
        var browseModeLayout = browseModeButton.gameObject.AddComponent<LayoutElement>();
        browseModeLayout.minHeight = 40f;
        browseModeLayout.preferredHeight = 40f;
        browseModeLayout.minWidth = 70f;
        browseModeLayout.preferredWidth = 70f;
        browseModeLayout.flexibleWidth = 1f;

        var transformModeButton = CreateButton("Button_ModeTransform", editModeRow, "\u79fb\u52d5");
        var transformModeLayout = transformModeButton.gameObject.AddComponent<LayoutElement>();
        transformModeLayout.minHeight = 40f;
        transformModeLayout.preferredHeight = 40f;
        transformModeLayout.minWidth = 70f;
        transformModeLayout.preferredWidth = 70f;
        transformModeLayout.flexibleWidth = 1f;

        var scaleModeButton = CreateButton("Button_ModeScale", editModeRow, "\u30b9\u30b1\u30fc\u30eb");
        var scaleModeLayout = scaleModeButton.gameObject.AddComponent<LayoutElement>();
        scaleModeLayout.minHeight = 40f;
        scaleModeLayout.preferredHeight = 40f;
        scaleModeLayout.minWidth = 70f;
        scaleModeLayout.preferredWidth = 70f;
        scaleModeLayout.flexibleWidth = 1f;

        var settingsButton = CreateButton("Button_Settings", parent, "\u2699");
        SetRect(settingsButton.GetComponent<RectTransform>(), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-82f, -52f), new Vector2(-12f, -12f));
        var settingsLabel = settingsButton.GetComponentInChildren<TMP_Text>(true);
        if (settingsLabel != null)
        {
            settingsLabel.fontSize = 22;
            settingsLabel.alignment = TextAlignmentOptions.Center;
        }

        var header = CreateUiRect("Header", panel);
        SetRect(header, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(10f, -48f), new Vector2(-10f, -10f));
        header.gameObject.AddComponent<Image>().color = DesignTokens.Surface;

        var title = CreateText("Title", header, "\u30AA\u30D6\u30B8\u30A7\u30AF\u30C8\u4E00\u89A7");
        title.fontSize = 16;
        title.alignment = TextAlignmentOptions.MidlineLeft;
        SetRect(title.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(10f, 0f), new Vector2(-10f, 0f));

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

        var addButton = CreateButton("Button_AddObjectBottom", panel, "FBX\u3092\u8FFD\u52A0");
        SetRect(addButton.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(10f, 10f), new Vector2(-10f, 48f));
        addButton.GetComponent<Image>().color = DesignTokens.BgSecondary;

        var scroll = CreateUiRect("Scroll_Catalog", panel);
        SetRect(scroll, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(8f, 56f), new Vector2(-8f, -72f));
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

        var labelMain = CreateText("LabelMain", cardTemplate, "Item");
        labelMain.fontSize = 14;
        labelMain.alignment = TextAlignmentOptions.Center;
        SetRect(labelMain.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(10f, 0f), new Vector2(-10f, 0f));

        cardTemplate.gameObject.SetActive(false);
        var settingsPanel = BuildSettingsDialog(
            parent,
            out var settingsTabGeneralButton,
            out var settingsTabIntegrationButton,
            out var settingsTabAccountButton,
            out var settingsGeneralContent,
            out var settingsIntegrationContent,
            out var settingsAccountContent,
            out var settingsSensitivitySlider,
            out var settingsSensitivityValueText,
            out var settingsIntegrationLinkButton,
            out var settingsRevertButton,
            out var settingsApplyButton,
            out var settingsAccountUserNameText,
            out var settingsAccountEmailText);
        var newObjectSettingsPanel = BuildNewObjectSettingsDialog(parent, out var newObjectNameInput, out var newObjectDescriptionInput, out var newObjectApplyButton, out var newObjectCancelButton, out var newObjectPathText);

        var catalogUi = panel.gameObject.AddComponent<CatalogUI>();
        var catalogSo = new SerializedObject(catalogUi);
        catalogSo.FindProperty("content").objectReferenceValue = content;
        catalogSo.FindProperty("buttonTemplate").objectReferenceValue = cardButton;
        catalogSo.FindProperty("searchInput").objectReferenceValue = searchInput;
        catalogSo.FindProperty("addButton").objectReferenceValue = addButton;
        catalogSo.FindProperty("statusText").objectReferenceValue = statusText;
        var modeRowProp = catalogSo.FindProperty("editModeRow");
        if (modeRowProp != null) modeRowProp.objectReferenceValue = editModeRow;
        var browseModeProp = catalogSo.FindProperty("browseModeButton");
        if (browseModeProp != null) browseModeProp.objectReferenceValue = browseModeButton;
        var transformModeProp = catalogSo.FindProperty("transformModeButton");
        if (transformModeProp != null) transformModeProp.objectReferenceValue = transformModeButton;
        var scaleModeProp = catalogSo.FindProperty("scaleModeButton");
        if (scaleModeProp != null) scaleModeProp.objectReferenceValue = scaleModeButton;
        var settingsButtonProp = catalogSo.FindProperty("settingsButton");
        if (settingsButtonProp != null) settingsButtonProp.objectReferenceValue = settingsButton;
        var settingsPanelProp = catalogSo.FindProperty("settingsPanel");
        if (settingsPanelProp != null) settingsPanelProp.objectReferenceValue = settingsPanel;
        var settingsTabGeneralProp = catalogSo.FindProperty("settingsTabGeneralButton");
        if (settingsTabGeneralProp != null) settingsTabGeneralProp.objectReferenceValue = settingsTabGeneralButton;
        var settingsTabIntegrationProp = catalogSo.FindProperty("settingsTabIntegrationButton");
        if (settingsTabIntegrationProp != null) settingsTabIntegrationProp.objectReferenceValue = settingsTabIntegrationButton;
        var settingsTabAccountProp = catalogSo.FindProperty("settingsTabAccountButton");
        if (settingsTabAccountProp != null) settingsTabAccountProp.objectReferenceValue = settingsTabAccountButton;
        var settingsGeneralContentProp = catalogSo.FindProperty("settingsGeneralContent");
        if (settingsGeneralContentProp != null) settingsGeneralContentProp.objectReferenceValue = settingsGeneralContent;
        var settingsIntegrationContentProp = catalogSo.FindProperty("settingsIntegrationContent");
        if (settingsIntegrationContentProp != null) settingsIntegrationContentProp.objectReferenceValue = settingsIntegrationContent;
        var settingsAccountContentProp = catalogSo.FindProperty("settingsAccountContent");
        if (settingsAccountContentProp != null) settingsAccountContentProp.objectReferenceValue = settingsAccountContent;
        var settingsSensitivitySliderProp = catalogSo.FindProperty("settingsSensitivitySlider");
        if (settingsSensitivitySliderProp != null) settingsSensitivitySliderProp.objectReferenceValue = settingsSensitivitySlider;
        var settingsSensitivityValueTextProp = catalogSo.FindProperty("settingsSensitivityValueText");
        if (settingsSensitivityValueTextProp != null) settingsSensitivityValueTextProp.objectReferenceValue = settingsSensitivityValueText;
        var settingsIntegrationLinkButtonProp = catalogSo.FindProperty("settingsIntegrationLinkButton");
        if (settingsIntegrationLinkButtonProp != null) settingsIntegrationLinkButtonProp.objectReferenceValue = settingsIntegrationLinkButton;
        var settingsRevertButtonProp = catalogSo.FindProperty("settingsRevertButton");
        if (settingsRevertButtonProp != null) settingsRevertButtonProp.objectReferenceValue = settingsRevertButton;
        var settingsApplyButtonProp = catalogSo.FindProperty("settingsApplyButton");
        if (settingsApplyButtonProp != null) settingsApplyButtonProp.objectReferenceValue = settingsApplyButton;
        var settingsAccountUserNameTextProp = catalogSo.FindProperty("settingsAccountUserNameText");
        if (settingsAccountUserNameTextProp != null) settingsAccountUserNameTextProp.objectReferenceValue = settingsAccountUserNameText;
        var settingsAccountEmailTextProp = catalogSo.FindProperty("settingsAccountEmailText");
        if (settingsAccountEmailTextProp != null) settingsAccountEmailTextProp.objectReferenceValue = settingsAccountEmailText;
        var newObjectSettingsPanelProp = catalogSo.FindProperty("newObjectSettingsPanel");
        if (newObjectSettingsPanelProp != null) newObjectSettingsPanelProp.objectReferenceValue = newObjectSettingsPanel;
        var settingsNameProp = catalogSo.FindProperty("newObjectNameInput");
        if (settingsNameProp != null) settingsNameProp.objectReferenceValue = newObjectNameInput;
        var settingsDescProp = catalogSo.FindProperty("newObjectDescriptionInput");
        if (settingsDescProp != null) settingsDescProp.objectReferenceValue = newObjectDescriptionInput;
        var settingsApplyProp = catalogSo.FindProperty("newObjectApplyButton");
        if (settingsApplyProp != null) settingsApplyProp.objectReferenceValue = newObjectApplyButton;
        var settingsCancelProp = catalogSo.FindProperty("newObjectCancelButton");
        if (settingsCancelProp != null) settingsCancelProp.objectReferenceValue = newObjectCancelButton;
        var settingsPathProp = catalogSo.FindProperty("newObjectPathText");
        if (settingsPathProp != null) settingsPathProp.objectReferenceValue = newObjectPathText;
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
        status.alignment = TextAlignmentOptions.MidlineLeft;
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

    static void BuildDockSync(GameObject root, RectTransform catalogPanel, RectTransform scenarioPanel, RectTransform editModeRow, RectTransform settingsButton)
    {
        var sync = root.AddComponent<UiPanelDockSync>();
        sync.catalogPanel = catalogPanel;
        sync.scenarioPanel = scenarioPanel;
        sync.editModePanel = editModeRow;
        sync.settingsButtonPanel = settingsButton;
        sync.gap = 0f;
    }

    // 笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏
    // 繧ｪ繝悶ず繧ｧ繧ｯ繝郁ｩｳ邏ｰ繝代ロ繝ｫ
    // 笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏

    static RectTransform BuildDetailPanel(Transform parent)
    {
        var panel = CreateUiRect("Panel_Detail", parent);
        SetRect(panel, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(-288f, 0f), new Vector2(0f, 0f));
        panel.gameObject.AddComponent<Image>().color = DesignTokens.BgPrimary;

        var header = CreateUiRect("Header", panel);
        SetRect(header, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(10f, -48f), new Vector2(-10f, -10f));
        header.gameObject.AddComponent<Image>().color = DesignTokens.Surface;

        var title = CreateText("Title", header, "\u30aa\u30d6\u30b8\u30a7\u30af\u30c8\u8a73\u7d30");
        title.fontSize = DesignTokens.FontSizeSubheading;
        title.alignment = TextAlignmentOptions.MidlineLeft;
        SetRect(title.rectTransform, Vector2.zero, Vector2.one, new Vector2(10f, 0f), new Vector2(-10f, 0f));

        var scroll = CreateUiRect("Scroll_Detail", panel);
        SetRect(scroll, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0f, 0f), new Vector2(0f, -68f));
        var scrollRect = scroll.gameObject.AddComponent<ScrollRect>();

        var viewport = CreateUiRect("Viewport", scroll);
        SetRect(viewport, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        viewport.gameObject.AddComponent<Image>().color = DesignTokens.Surface;
        viewport.gameObject.AddComponent<Mask>().showMaskGraphic = false;

        var content = CreateUiRect("Content", viewport);
        SetRect(content, new Vector2(0f, 1f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
        content.pivot = new Vector2(0f, 1f);
        var vLayout = content.gameObject.AddComponent<VerticalLayoutGroup>();
        vLayout.childControlWidth = true;
        vLayout.childControlHeight = true;
        vLayout.childForceExpandWidth = true;
        vLayout.childForceExpandHeight = false;
        vLayout.spacing = 0f;
        content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scrollRect.viewport = viewport;
        scrollRect.content = content;
        scrollRect.horizontal = false;

        var textPrefabLabel = BuildDetailRow(content, "Row_PrefabLabel", "\u30d7\u30ec\u30d5\u30a1\u30d6\u540d", "Text_PrefabLabel", out _);
        BuildDetailDivider(content);

        var inputObjectName = BuildDetailNameRow(content, "Row_ObjectName", "\u30aa\u30d6\u30b8\u30a7\u30af\u30c8\u540d");
        BuildDetailDivider(content);

        var inputDescription = BuildDetailDescriptionRow(content, "Row_Description", "\u8aac\u660e", out var rowDescriptionGo);
        BuildDetailDivider(content);

        var textConditionUsage = BuildDetailConditionUsageRow(
            content,
            out var usageNodeLabelText,
            out var usageNodeListRoot,
            out var usageNodeBlockTemplate);

        var usageStyler = panel.gameObject.AddComponent<ObjectDetailConditionNodeStyler>();
        var detailPanel = panel.gameObject.AddComponent<ObjectDetailPanel>();
        var detailSo = new SerializedObject(detailPanel);
        detailSo.FindProperty("textPrefabLabel").objectReferenceValue = textPrefabLabel;
        detailSo.FindProperty("inputObjectName").objectReferenceValue = inputObjectName;
        detailSo.FindProperty("inputDescription").objectReferenceValue = inputDescription;
        detailSo.FindProperty("usageNodeLabelText").objectReferenceValue = usageNodeLabelText;
        detailSo.FindProperty("textConditionUsage").objectReferenceValue = textConditionUsage;
        detailSo.FindProperty("usageNodeListRoot").objectReferenceValue = usageNodeListRoot;
        detailSo.FindProperty("usageNodeBlockTemplate").objectReferenceValue = usageNodeBlockTemplate;
        detailSo.FindProperty("usageNodeStyler").objectReferenceValue = usageStyler;
        detailSo.FindProperty("rowDescription").objectReferenceValue = rowDescriptionGo;
        detailSo.ApplyModifiedPropertiesWithoutUndo();

        return panel;
    }

    static TextMeshProUGUI BuildDetailConditionUsageRow(
        Transform parent,
        out TextMeshProUGUI labelText,
        out RectTransform usageNodeListRoot,
        out GameObject usageNodeBlockTemplate)
    {
        var row = CreateUiRect("Row_ConditionUsage", parent);
        row.gameObject.AddComponent<Image>().color = DesignTokens.Surface;
        var rowLayout = row.gameObject.AddComponent<VerticalLayoutGroup>();
        rowLayout.childControlWidth = true;
        rowLayout.childControlHeight = true;
        rowLayout.childForceExpandWidth = true;
        rowLayout.childForceExpandHeight = false;
        rowLayout.padding = new RectOffset(
            (int)DesignTokens.SpaceMd,
            (int)DesignTokens.SpaceMd,
            (int)DesignTokens.SpaceSm,
            (int)DesignTokens.SpaceSm);
        rowLayout.spacing = DesignTokens.SpaceXs;
        row.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        labelText = CreateText("Label", row, "\u4F7F\u7528\u4E2D\u30CE\u30FC\u30C9");
        labelText.fontSize = DesignTokens.FontSizeCaption;
        labelText.color = DesignTokens.TextSecondary;
        var labelLayout = labelText.gameObject.AddComponent<LayoutElement>();
        labelLayout.minHeight = DesignTokens.FontSizeCaption + 4f;
        labelLayout.preferredHeight = DesignTokens.FontSizeCaption + 4f;

        var emptyText = CreateText("Text_ConditionUsage", row, "\u672A\u4F7F\u7528");
        emptyText.fontSize = DesignTokens.FontSizeBody;
        emptyText.color = DesignTokens.TextPrimary;
        emptyText.alignment = TextAlignmentOptions.TopLeft;
        var emptyLayout = emptyText.gameObject.AddComponent<LayoutElement>();
        emptyLayout.minHeight = DesignTokens.FontSizeBody + 4f;

        usageNodeListRoot = CreateUiRect("UsageNodeList", row);
        var listLayout = usageNodeListRoot.gameObject.AddComponent<VerticalLayoutGroup>();
        listLayout.childControlWidth = true;
        listLayout.childControlHeight = true;
        listLayout.childForceExpandWidth = true;
        listLayout.childForceExpandHeight = false;
        listLayout.spacing = 8f;
        listLayout.padding = new RectOffset(0, 0, 0, 0);
        usageNodeListRoot.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var blockTemplate = CreateUiRect("UsageNodeBlock_Template", usageNodeListRoot);
        blockTemplate.gameObject.AddComponent<Image>().color = DesignTokens.Surface;
        EnsureThinOutline(blockTemplate.gameObject, DesignTokens.Divider);
        var blockLayout = blockTemplate.gameObject.AddComponent<LayoutElement>();
        blockLayout.minHeight = 64f;
        blockLayout.preferredHeight = 64f;

        var body = CreateText("Text_UsageNodeBody", blockTemplate, "\u672A\u4F7F\u7528");
        body.fontSize = DesignTokens.FontSizeBody;
        body.color = DesignTokens.TextPrimary;
        body.alignment = TextAlignmentOptions.TopLeft;
        SetRect(body.rectTransform, Vector2.zero, Vector2.one, new Vector2(10f, 8f), new Vector2(-10f, -8f));

        usageNodeBlockTemplate = blockTemplate.gameObject;
        usageNodeBlockTemplate.SetActive(false);
        return emptyText;
    }

    static TMP_InputField BuildDetailNameRow(Transform parent, string rowName, string labelText)
    {
        var row = CreateUiRect(rowName, parent);
        row.gameObject.AddComponent<Image>().color = DesignTokens.Surface;
        var rowLayout = row.gameObject.AddComponent<VerticalLayoutGroup>();
        rowLayout.childControlWidth = true;
        rowLayout.childControlHeight = true;
        rowLayout.childForceExpandWidth = true;
        rowLayout.childForceExpandHeight = false;
        rowLayout.padding = new RectOffset(
            (int)DesignTokens.SpaceMd,
            (int)DesignTokens.SpaceMd,
            (int)DesignTokens.SpaceSm,
            (int)DesignTokens.SpaceSm);
        rowLayout.spacing = DesignTokens.SpaceXs;
        row.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var label = CreateText("Label", row, labelText);
        label.fontSize = DesignTokens.FontSizeCaption;
        label.color = DesignTokens.TextSecondary;
        var labelLayout = label.gameObject.AddComponent<LayoutElement>();
        labelLayout.minHeight = DesignTokens.FontSizeCaption + 4f;
        labelLayout.preferredHeight = DesignTokens.FontSizeCaption + 4f;

        var input = CreateInputField("Input_ObjectName", row, "\u540D\u524D\u3092\u5165\u529B...");
        var inputLayout = input.gameObject.AddComponent<LayoutElement>();
        inputLayout.minHeight = DesignTokens.InputHeight;
        inputLayout.preferredHeight = DesignTokens.InputHeight;

        return input;
    }

    static TMP_InputField BuildDetailDescriptionRow(Transform parent, string rowName, string labelText, out GameObject rowGo)
    {
        var row = CreateUiRect(rowName, parent);
        row.gameObject.AddComponent<Image>().color = DesignTokens.Surface;
        var rowLayout = row.gameObject.AddComponent<VerticalLayoutGroup>();
        rowLayout.childControlWidth = true;
        rowLayout.childControlHeight = true;
        rowLayout.childForceExpandWidth = true;
        rowLayout.childForceExpandHeight = false;
        rowLayout.padding = new RectOffset(
            (int)DesignTokens.SpaceMd,
            (int)DesignTokens.SpaceMd,
            (int)DesignTokens.SpaceSm,
            (int)DesignTokens.SpaceSm);
        rowLayout.spacing = DesignTokens.SpaceXs;
        row.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var label = CreateText("Label", row, labelText);
        label.fontSize = DesignTokens.FontSizeCaption;
        label.color = DesignTokens.TextSecondary;
        var labelLayout = label.gameObject.AddComponent<LayoutElement>();
        labelLayout.minHeight = DesignTokens.FontSizeCaption + 4f;
        labelLayout.preferredHeight = DesignTokens.FontSizeCaption + 4f;

        var input = CreateInputField("Input_Description", row, "\u8AAC\u660E\u3092\u5165\u529B...");
        input.lineType = TMP_InputField.LineType.MultiLineNewline;
        if (input.textComponent != null)
        {
            input.textComponent.alignment = TextAlignmentOptions.TopLeft;
            var textRt = input.textComponent.rectTransform;
            textRt.offsetMin = new Vector2(8f, 8f);
            textRt.offsetMax = new Vector2(-8f, -8f);
        }

        if (input.placeholder is TMP_Text placeholder)
        {
            placeholder.alignment = TextAlignmentOptions.TopLeft;
            var placeholderRt = placeholder.rectTransform;
            placeholderRt.offsetMin = new Vector2(8f, 8f);
            placeholderRt.offsetMax = new Vector2(-8f, -8f);
        }

        var inputLayout = input.gameObject.AddComponent<LayoutElement>();
        inputLayout.minHeight = 96f;
        inputLayout.preferredHeight = 96f;

        rowGo = row.gameObject;
        return input;
    }

    static TextMeshProUGUI BuildDetailRow(Transform parent, string rowName, string labelText, string valueTextName, out GameObject rowGo)
    {
        var row = CreateUiRect(rowName, parent);
        row.gameObject.AddComponent<Image>().color = DesignTokens.Surface;
        var rowLayout = row.gameObject.AddComponent<VerticalLayoutGroup>();
        rowLayout.childControlWidth = true;
        rowLayout.childControlHeight = true;
        rowLayout.childForceExpandWidth = true;
        rowLayout.childForceExpandHeight = false;
        rowLayout.padding = new RectOffset(
            (int)DesignTokens.SpaceMd,
            (int)DesignTokens.SpaceMd,
            (int)DesignTokens.SpaceSm,
            (int)DesignTokens.SpaceSm);
        rowLayout.spacing = DesignTokens.SpaceXs;
        row.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var label = CreateText("Label", row, labelText);
        label.fontSize = DesignTokens.FontSizeCaption;
        label.color = DesignTokens.TextSecondary;
        var labelLayout = label.gameObject.AddComponent<LayoutElement>();
        labelLayout.minHeight = DesignTokens.FontSizeCaption + 4f;
        labelLayout.preferredHeight = DesignTokens.FontSizeCaption + 4f;

        var valueText = CreateText(valueTextName, row, "");
        valueText.fontSize = DesignTokens.FontSizeBody;
        valueText.color = DesignTokens.TextPrimary;
        var valueLayout = valueText.gameObject.AddComponent<LayoutElement>();
        valueLayout.minHeight = DesignTokens.FontSizeBody + 4f;

        rowGo = row.gameObject;
        return valueText;
    }

    static void BuildDetailDivider(Transform parent)
    {
        var divGo = new GameObject("Divider", typeof(RectTransform), typeof(Image));
        divGo.transform.SetParent(parent, false);
        divGo.GetComponent<Image>().color = DesignTokens.Divider;
        divGo.GetComponent<Image>().raycastTarget = false;
        var divLayout = divGo.AddComponent<LayoutElement>();
        divLayout.minHeight = DesignTokens.DividerHeight;
        divLayout.preferredHeight = DesignTokens.DividerHeight;
        divLayout.flexibleWidth = 1f;
    }

    static StepNodeUI BuildStepNodeTemplate(Transform parent)
    {
        var root = CreateUiRect("StepNodeTemplate", parent);
        root.sizeDelta = new Vector2(390f, 180f);
        root.gameObject.AddComponent<Image>().color = DesignTokens.Surface;
        EnsureThinOutline(root.gameObject, DesignTokens.Divider);
        var stepNode = root.gameObject.AddComponent<StepNodeUI>();

        var stepId = CreateText("Text_StepId", root, "STEP 1");
        stepId.fontStyle = FontStyles.Bold;
        SetRect(stepId.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(12f, -28f), new Vector2(-44f, -8f));

        var conditionSummary = CreateText("Text_ConditionSummary", root, "\u6761\u4EF6: 0");
        conditionSummary.fontSize = 12;
        conditionSummary.alignment = TextAlignmentOptions.MidlineLeft;
        conditionSummary.color = DesignTokens.TextSecondary;
        SetRect(conditionSummary.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(12f, -92f), new Vector2(-44f, -72f));

        var dragHandle = CreateUiRect("DragHandle", root);
        SetRect(dragHandle, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(12f, 16f), new Vector2(-12f, -34f));
        dragHandle.gameObject.AddComponent<Image>().color = DesignTokens.Surface;
        EnsureThinOutline(dragHandle.gameObject, DesignTokens.Divider);
        var drag = dragHandle.gameObject.AddComponent<NodeDragHandler>();
        drag.target = root;

        var warning = CreateText("Warning", root, "!");
        warning.fontSize = 18;
        warning.color = DesignTokens.Warning;
        warning.alignment = TextAlignmentOptions.Center;
        SetRect(warning.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-56f, -30f), new Vector2(-36f, -10f));

        var deleteButton = CreateButton("Button_Delete", root, "X");
        SetRect(deleteButton.GetComponent<RectTransform>(), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-34f, -30f), new Vector2(-12f, -8f));
        deleteButton.GetComponent<Image>().color = DesignTokens.Surface;
        EnsureThinOutline(deleteButton.gameObject, DesignTokens.Divider);
        var deleteLabel = deleteButton.GetComponentInChildren<TMP_Text>(true);
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
        row.gameObject.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0f);
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
        dropdownA.GetComponent<Image>().color = DesignTokens.Surface;
        StyleConditionDropdown(dropdownA);
        dropdownA.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
        var textA = CreateText("Text_AfterA", lineA, "\u3092");
        textA.fontStyle = FontStyles.Bold;
        textA.gameObject.AddComponent<LayoutElement>().minWidth = 24f;

        var dropdownB = CreateDropdown("DropdownB", lineB);
        dropdownB.GetComponent<Image>().color = DesignTokens.Surface;
        StyleConditionDropdown(dropdownB);
        dropdownB.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
        var textB = CreateText("Text_AfterB", lineB, "\u306B\u8FD1\u3065\u3051\u308B");
        textB.fontStyle = FontStyles.Bold;
        textB.gameObject.AddComponent<LayoutElement>().minWidth = 96f;

        var ui = row.gameObject.AddComponent<ConditionRowUI>();
        ui.dropdownA = dropdownA;
        ui.dropdownB = dropdownB;
        ui.textAfterA = textA;
        ui.textAfterB = textB;
        return ui;
    }

    static TMP_Dropdown CreateDropdown(string name, Transform parent)
    {
        var root = CreateUiRect(name, parent);
        root.gameObject.AddComponent<Image>().color = DesignTokens.Surface;
        EnsureThinOutline(root.gameObject, DesignTokens.Divider);
        var dropdown = root.gameObject.AddComponent<TMP_Dropdown>();

        var caption = CreateText("Caption", root, "\u672A\u8A2D\u5B9A");
        SetRect(caption.rectTransform, Vector2.zero, Vector2.one, new Vector2(8f, 0f), new Vector2(-22f, 0f));

        var template = CreateUiRect("Template", root);
        SetRect(template, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, -120f), new Vector2(0f, 0f));
        template.pivot = new Vector2(0.5f, 1f);
        template.anchoredPosition = Vector2.zero;
        template.gameObject.AddComponent<Image>().color = DesignTokens.Surface;
        EnsureThinOutline(template.gameObject, DesignTokens.Divider);
        var scroll = template.gameObject.AddComponent<ScrollRect>();
        template.gameObject.SetActive(false);

        var viewport = CreateUiRect("Viewport", template);
        SetRect(viewport, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        viewport.gameObject.AddComponent<Image>().color = DesignTokens.Surface;
        EnsureThinOutline(viewport.gameObject, DesignTokens.Divider);
        viewport.gameObject.AddComponent<Mask>().showMaskGraphic = false;

        var content = CreateUiRect("Content", viewport);
        SetRect(content, new Vector2(0f, 1f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
        var contentLayout = content.gameObject.AddComponent<VerticalLayoutGroup>();
        contentLayout.childForceExpandHeight = false;
        contentLayout.childControlHeight = true;
        content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var item = CreateUiRect("Item", content);
        item.sizeDelta = new Vector2(0f, 24f);
        item.gameObject.AddComponent<Image>().color = DesignTokens.Surface;
        EnsureThinOutline(item.gameObject, DesignTokens.Divider);
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
        dropdown.options = new System.Collections.Generic.List<TMP_Dropdown.OptionData>
        {
            new TMP_Dropdown.OptionData("\u672A\u8A2D\u5B9A")
        };
        return dropdown;
    }

    static void EnsureThinOutline(GameObject target, Color color)
    {
        if (target == null) return;
        if (target.GetComponent<Graphic>() == null) return;

        var outline = target.GetComponent<Outline>();
        if (outline == null) outline = target.AddComponent<Outline>();
        outline.effectColor = color;
        outline.effectDistance = new Vector2(1f, -1f);
        outline.useGraphicAlpha = false;
    }

    static void StyleConditionDropdown(TMP_Dropdown dropdown)
    {
        if (dropdown == null) return;

        if (dropdown.captionText != null)
        {
            dropdown.captionText.color = DesignTokens.TextPrimary;
        }
    }

    static TMP_InputField CreateInputField(string name, Transform parent, string placeholderText)
    {
        var root = CreateUiRect(name, parent);
        root.gameObject.AddComponent<Image>().color = DesignTokens.BgPrimary;
        var input = root.gameObject.AddComponent<TMP_InputField>();

        var textArea = CreateUiRect("Text Area", root);
        SetRect(textArea, Vector2.zero, Vector2.one, new Vector2(8f, 0f), new Vector2(-8f, 0f));
        textArea.gameObject.AddComponent<RectMask2D>();

        var text = CreateText("Text", textArea, "");
        SetRect(text.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        var placeholder = CreateText("Placeholder", textArea, placeholderText);
        placeholder.color = DesignTokens.TextTertiary;
        SetRect(placeholder.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        input.textViewport = textArea;
        input.textComponent = text;
        input.placeholder = placeholder;
        return input;
    }

    static RectTransform BuildNewObjectSettingsDialog(
        Transform host,
        out TMP_InputField nameInput,
        out TMP_InputField descriptionInput,
        out Button applyButton,
        out Button cancelButton,
        out TextMeshProUGUI pathText)
    {
        var overlay = CreateUiRect("Panel_NewObjectSettings", host);
        SetRect(overlay, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        var overlayImage = overlay.gameObject.AddComponent<Image>();
        var overlayColor = DesignTokens.TextPrimary;
        overlayColor.a = 0.32f;
        overlayImage.color = overlayColor;
        overlayImage.raycastTarget = true;
        var window = CreateUiRect("Window", overlay);
        window.anchorMin = new Vector2(0.5f, 0.5f);
        window.anchorMax = new Vector2(0.5f, 0.5f);
        window.pivot = new Vector2(0.5f, 0.5f);
        window.sizeDelta = new Vector2(560f, 430f);
        window.anchoredPosition = Vector2.zero;
        window.gameObject.AddComponent<Image>().color = DesignTokens.Surface;
        var title = CreateText("Text_Title", window, "\u30AA\u30D6\u30B8\u30A7\u30AF\u30C8\u8A2D\u5B9A");
        title.fontSize = 16;
        title.alignment = TextAlignmentOptions.MidlineLeft;
        SetRect(title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(16f, -44f), new Vector2(-16f, -16f));
        pathText = CreateText("Text_FilePath", window, "");
        pathText.fontSize = 12;
        pathText.color = DesignTokens.TextSecondary;
        pathText.alignment = TextAlignmentOptions.TopLeft;
        SetRect(pathText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(16f, -84f), new Vector2(-16f, -52f));
        var nameLabel = CreateText("Text_NameLabel", window, "\u30AA\u30D6\u30B8\u30A7\u30AF\u30C8\u540D");
        nameLabel.fontSize = 13;
        SetRect(nameLabel.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(16f, -116f), new Vector2(-16f, -92f));
        nameInput = CreateInputField("Input_NewObjectName", window, "New Object");
        SetRect(nameInput.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(16f, -160f), new Vector2(-16f, -120f));
        var descriptionLabel = CreateText("Text_DescriptionLabel", window, "\u8AAC\u660E");
        descriptionLabel.fontSize = 13;
        SetRect(descriptionLabel.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(16f, -196f), new Vector2(-16f, -172f));
        descriptionInput = CreateInputField("Input_NewObjectDescription", window, "\u8AAC\u660E\u3092\u5165\u529B...");
        descriptionInput.lineType = TMP_InputField.LineType.MultiLineNewline;
        if (descriptionInput.textComponent != null)
        {
            descriptionInput.textComponent.alignment = TextAlignmentOptions.TopLeft;
            var descTextRt = descriptionInput.textComponent.rectTransform;
            descTextRt.offsetMin = new Vector2(8f, 8f);
            descTextRt.offsetMax = new Vector2(-8f, -8f);
        }
        if (descriptionInput.placeholder is TMP_Text descPlaceholder)
        {
            descPlaceholder.alignment = TextAlignmentOptions.TopLeft;
            var descPlaceholderRt = descPlaceholder.rectTransform;
            descPlaceholderRt.offsetMin = new Vector2(8f, 8f);
            descPlaceholderRt.offsetMax = new Vector2(-8f, -8f);
        }
        SetRect(descriptionInput.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(16f, -324f), new Vector2(-16f, -208f));
        var buttonsRow = CreateUiRect("ButtonsRow", window);
        SetRect(buttonsRow, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(16f, 16f), new Vector2(-16f, 56f));
        var rowLayout = buttonsRow.gameObject.AddComponent<HorizontalLayoutGroup>();
        rowLayout.spacing = 8f;
        rowLayout.childControlWidth = true;
        rowLayout.childControlHeight = true;
        rowLayout.childForceExpandWidth = true;
        applyButton = CreateButton("Button_Apply", buttonsRow, "\u8FFD\u52A0");
        var applyLayout = applyButton.gameObject.AddComponent<LayoutElement>();
        applyLayout.minHeight = 40f;
        applyLayout.preferredHeight = 40f;
        applyButton.GetComponent<Image>().color = DesignTokens.Accent;
        var applyLabel = applyButton.GetComponentInChildren<TMP_Text>(true);
        if (applyLabel != null) applyLabel.color = DesignTokens.Surface;
        cancelButton = CreateButton("Button_Cancel", buttonsRow, "\u30AD\u30E3\u30F3\u30BB\u30EB");
        var cancelLayout = cancelButton.gameObject.AddComponent<LayoutElement>();
        cancelLayout.minHeight = 40f;
        cancelLayout.preferredHeight = 40f;
        cancelButton.GetComponent<Image>().color = DesignTokens.BgSecondary;
        overlay.gameObject.SetActive(false);
        return overlay;
    }

    static RectTransform BuildSettingsDialog(
        Transform host,
        out Button tabGeneralButton,
        out Button tabIntegrationButton,
        out Button tabAccountButton,
        out RectTransform generalContent,
        out RectTransform integrationContent,
        out RectTransform accountContent,
        out Slider sensitivitySlider,
        out TextMeshProUGUI sensitivityValueText,
        out Button integrationLinkButton,
        out Button revertButton,
        out Button applyButton,
        out TextMeshProUGUI accountUserNameText,
        out TextMeshProUGUI accountEmailText)
    {
        var overlay = CreateUiRect("Panel_Settings", host);
        SetRect(overlay, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        var overlayImage = overlay.gameObject.AddComponent<Image>();
        var overlayColor = DesignTokens.TextPrimary;
        overlayColor.a = 0.32f;
        overlayImage.color = overlayColor;
        overlayImage.raycastTarget = true;

        var window = CreateUiRect("Window", overlay);
        window.anchorMin = new Vector2(0.5f, 0.5f);
        window.anchorMax = new Vector2(0.5f, 0.5f);
        window.pivot = new Vector2(0.5f, 0.5f);
        window.sizeDelta = new Vector2(760f, 460f);
        window.anchoredPosition = Vector2.zero;
        window.gameObject.AddComponent<Image>().color = DesignTokens.Surface;

        var tabs = CreateUiRect("Tabs", window);
        SetRect(tabs, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(16f, 16f), new Vector2(172f, -16f));
        tabs.gameObject.AddComponent<Image>().color = DesignTokens.BgPrimary;
        var tabsLayout = tabs.gameObject.AddComponent<VerticalLayoutGroup>();
        tabsLayout.spacing = 8f;
        tabsLayout.padding = new RectOffset(8, 8, 8, 8);
        tabsLayout.childControlWidth = true;
        tabsLayout.childControlHeight = false;
        tabsLayout.childForceExpandWidth = true;
        tabsLayout.childForceExpandHeight = false;

        tabGeneralButton = CreateButton("Tab_General", tabs, "\u4e00\u822c");
        var tabGeneralLayout = tabGeneralButton.gameObject.AddComponent<LayoutElement>();
        tabGeneralLayout.minHeight = 40f;
        tabGeneralLayout.preferredHeight = 40f;

        tabIntegrationButton = CreateButton("Tab_Integration", tabs, "\u9023\u643a");
        var tabIntegrationLayout = tabIntegrationButton.gameObject.AddComponent<LayoutElement>();
        tabIntegrationLayout.minHeight = 40f;
        tabIntegrationLayout.preferredHeight = 40f;

        tabAccountButton = CreateButton("Tab_Account", tabs, "\u30a2\u30ab\u30a6\u30f3\u30c8");
        var tabAccountLayout = tabAccountButton.gameObject.AddComponent<LayoutElement>();
        tabAccountLayout.minHeight = 40f;
        tabAccountLayout.preferredHeight = 40f;

        var content = CreateUiRect("Content", window);
        SetRect(content, Vector2.zero, Vector2.one, new Vector2(188f, 16f), new Vector2(-16f, -16f));
        content.gameObject.AddComponent<Image>().color = DesignTokens.BgPrimary;

        generalContent = CreateUiRect("Content_General", content);
        SetRect(generalContent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        var sensitivityTitle = CreateText("Text_Title", generalContent, "\u8996\u70b9\u64cd\u4f5c\u611f\u5ea6");
        sensitivityTitle.fontSize = 16;
        SetRect(sensitivityTitle.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(24f, -56f), new Vector2(-24f, -24f));

        var sliderRow = CreateUiRect("SliderRow", generalContent);
        SetRect(sliderRow, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(24f, -116f), new Vector2(-24f, -76f));
        sensitivitySlider = CreateSlider("Slider_Sensitivity", sliderRow);
        SetRect(sensitivitySlider.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, new Vector2(0f, 0f), new Vector2(-90f, 0f));
        sensitivitySlider.minValue = 0.2f;
        sensitivitySlider.maxValue = 2.5f;
        sensitivitySlider.value = 1f;
        sensitivityValueText = CreateText("Text_SensitivityValue", sliderRow, "1.00x");
        sensitivityValueText.fontSize = 14;
        sensitivityValueText.color = DesignTokens.TextSecondary;
        sensitivityValueText.alignment = TextAlignmentOptions.MidlineRight;
        SetRect(sensitivityValueText.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(-84f, 0f), new Vector2(0f, 0f));

        integrationContent = CreateUiRect("Content_Integration", content);
        SetRect(integrationContent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        integrationLinkButton = CreateButton("Button_WebLink", integrationContent, "\u3053\u3053\u304b\u3089\u30a6\u30a7\u30d6\u30b5\u30a4\u30c8\u3078\u9077\u79fb");
        SetRect(integrationLinkButton.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(24f, -80f), new Vector2(-24f, -32f));
        integrationLinkButton.GetComponent<Image>().color = DesignTokens.BgSecondary;
        var linkLabel = integrationLinkButton.GetComponentInChildren<TMP_Text>(true);
        if (linkLabel != null)
        {
            linkLabel.color = DesignTokens.Accent;
            linkLabel.fontSize = 16;
            linkLabel.alignment = TextAlignmentOptions.Center;
        }

        revertButton = CreateButton("Button_RevertSettings", window, "\u5143\u306b\u623b\u3059");
        var revertButtonRt = revertButton.GetComponent<RectTransform>();
        revertButtonRt.anchorMin = new Vector2(1f, 0f);
        revertButtonRt.anchorMax = new Vector2(1f, 0f);
        revertButtonRt.pivot = new Vector2(1f, 0f);
        revertButtonRt.sizeDelta = new Vector2(136f, 40f);
        revertButtonRt.anchoredPosition = new Vector2(-160f, 16f);
        revertButton.GetComponent<Image>().color = DesignTokens.BgSecondary;
        var revertLabel = revertButton.GetComponentInChildren<TMP_Text>(true);
        if (revertLabel != null)
        {
            revertLabel.color = DesignTokens.TextPrimary;
            revertLabel.fontSize = 14;
            revertLabel.alignment = TextAlignmentOptions.Center;
        }

        applyButton = CreateButton("Button_ApplySettings", window, "\u9069\u7528");
        var applyButtonRt = applyButton.GetComponent<RectTransform>();
        applyButtonRt.anchorMin = new Vector2(1f, 0f);
        applyButtonRt.anchorMax = new Vector2(1f, 0f);
        applyButtonRt.pivot = new Vector2(1f, 0f);
        applyButtonRt.sizeDelta = new Vector2(136f, 40f);
        applyButtonRt.anchoredPosition = new Vector2(-16f, 16f);
        applyButton.GetComponent<Image>().color = DesignTokens.Accent;
        var applyLabel = applyButton.GetComponentInChildren<TMP_Text>(true);
        if (applyLabel != null)
        {
            applyLabel.color = DesignTokens.Surface;
            applyLabel.fontSize = 14;
            applyLabel.alignment = TextAlignmentOptions.Center;
        }

        accountContent = CreateUiRect("Content_Account", content);
        SetRect(accountContent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        var accountIcon = CreateText("Text_AvatarIcon", accountContent, "\u25CF");
        accountIcon.fontSize = 72;
        accountIcon.alignment = TextAlignmentOptions.Center;
        accountIcon.color = DesignTokens.Accent;
        accountIcon.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        accountIcon.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        accountIcon.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        accountIcon.rectTransform.sizeDelta = new Vector2(120f, 120f);
        accountIcon.rectTransform.anchoredPosition = new Vector2(0f, 80f);

        accountUserNameText = CreateText("Text_UserName", accountContent, "User Name");
        accountUserNameText.fontSize = 28;
        accountUserNameText.alignment = TextAlignmentOptions.Center;
        accountUserNameText.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        accountUserNameText.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        accountUserNameText.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        accountUserNameText.rectTransform.sizeDelta = new Vector2(360f, 44f);
        accountUserNameText.rectTransform.anchoredPosition = new Vector2(0f, -6f);

        accountEmailText = CreateText("Text_Email", accountContent, "user@example.com");
        accountEmailText.fontSize = 14;
        accountEmailText.color = DesignTokens.TextSecondary;
        accountEmailText.alignment = TextAlignmentOptions.Center;
        accountEmailText.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        accountEmailText.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        accountEmailText.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        accountEmailText.rectTransform.sizeDelta = new Vector2(360f, 32f);
        accountEmailText.rectTransform.anchoredPosition = new Vector2(0f, -42f);

        integrationContent.gameObject.SetActive(false);
        accountContent.gameObject.SetActive(false);
        revertButton.gameObject.SetActive(false);
        applyButton.gameObject.SetActive(false);
        overlay.gameObject.SetActive(false);
        return overlay;
    }

    static Slider CreateSlider(string name, Transform parent)
    {
        var root = CreateUiRect(name, parent);
        root.gameObject.AddComponent<Image>().color = DesignTokens.BgSecondary;
        var slider = root.gameObject.AddComponent<Slider>();

        var fillArea = CreateUiRect("Fill Area", root);
        SetRect(fillArea, Vector2.zero, Vector2.one, new Vector2(8f, 10f), new Vector2(-24f, -10f));
        var fill = CreateUiRect("Fill", fillArea);
        SetRect(fill, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        fill.gameObject.AddComponent<Image>().color = DesignTokens.Accent;

        var handleArea = CreateUiRect("Handle Slide Area", root);
        SetRect(handleArea, Vector2.zero, Vector2.one, new Vector2(8f, 0f), new Vector2(-8f, 0f));
        var handle = CreateUiRect("Handle", handleArea);
        handle.anchorMin = new Vector2(0.5f, 0.5f);
        handle.anchorMax = new Vector2(0.5f, 0.5f);
        handle.sizeDelta = new Vector2(18f, 30f);
        var handleImage = handle.gameObject.AddComponent<Image>();
        handleImage.color = DesignTokens.Surface;

        slider.fillRect = fill;
        slider.handleRect = handle;
        slider.targetGraphic = handleImage;
        slider.direction = Slider.Direction.LeftToRight;
        return slider;
    }
    static Button CreateButton(string name, Transform parent, string label)
    {
        var root = CreateUiRect(name, parent);
        root.gameObject.AddComponent<Image>().color = DesignTokens.BgSecondary;
        var button = root.gameObject.AddComponent<Button>();

        var text = CreateText("Label", root, label);
        text.alignment = TextAlignmentOptions.Center;
        SetRect(text.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        return button;
    }

    static TextMeshProUGUI CreateText(string name, Transform parent, string value)
    {
        var textGo = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        textGo.transform.SetParent(parent, false);
        var text = textGo.GetComponent<TextMeshProUGUI>();
        text.color = DesignTokens.TextPrimary;
        text.fontSize = 14;
        text.alignment = TextAlignmentOptions.MidlineLeft;
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
