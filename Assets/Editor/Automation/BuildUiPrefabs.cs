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
        scaler.referenceResolution = new Vector2(1920f, 1080f);
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
        var panelImage = panel.gameObject.AddComponent<Image>();
        panelImage.color = new Color(0.96f, 0.96f, 0.96f, 1f);

        var resizeHandle = CreateUiRect("ResizeHandleX", panel);
        SetRect(resizeHandle, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(-6f, 0f), new Vector2(6f, 0f));
        resizeHandle.gameObject.AddComponent<Image>().color = new Color(0.96f, 0.96f, 0.96f, 1f);
        var resize = resizeHandle.gameObject.AddComponent<PanelHorizontalResizeHandle>();
        resize.targetPanel = panel;

        var scroll = CreateUiRect("Scroll_Catalog", panel);
        SetRect(scroll, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(8f, 8f), new Vector2(-8f, -8f));
        var scrollImage = scroll.gameObject.AddComponent<Image>();
        scrollImage.color = Color.white;
        var scrollRect = scroll.gameObject.AddComponent<ScrollRect>();

        var viewport = CreateUiRect("Viewport", scroll);
        SetRect(viewport, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        var viewportImage = viewport.gameObject.AddComponent<Image>();
        viewportImage.color = Color.white;
        viewport.gameObject.AddComponent<Mask>().showMaskGraphic = false;

        var content = CreateUiRect("Content", viewport);
        SetRect(content, new Vector2(0f, 1f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
        var vLayout = content.gameObject.AddComponent<VerticalLayoutGroup>();
        vLayout.spacing = 6f;
        vLayout.childControlWidth = true;
        vLayout.childControlHeight = true;
        vLayout.childForceExpandHeight = false;
        content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scrollRect.viewport = viewport;
        scrollRect.content = content;
        scrollRect.horizontal = false;

        var btnTemplate = CreateUiRect("Btn_Template", panel);
        SetRect(btnTemplate, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(8f, -48f), new Vector2(-8f, -8f));
        var btnImage = btnTemplate.gameObject.AddComponent<Image>();
        btnImage.color = new Color(1f, 0.98f, 0.86f, 1f);
        var btn = btnTemplate.gameObject.AddComponent<Button>();
        btnTemplate.gameObject.AddComponent<LayoutElement>().minHeight = 36f;
        var btnLabel = CreateText("Label", btnTemplate, "Item");
        SetRect(btnLabel.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        btnTemplate.gameObject.SetActive(false);

        var catalogUi = panel.gameObject.AddComponent<CatalogUI>();
        var so = new SerializedObject(catalogUi);
        so.FindProperty("content").objectReferenceValue = content;
        so.FindProperty("buttonTemplate").objectReferenceValue = btn;
        so.ApplyModifiedPropertiesWithoutUndo();

        return panel;
    }

    static RectTransform BuildScenarioPanel(Transform parent)
    {
        var panel = CreateUiRect("Panel_ScenarioGraph", parent);
        SetRect(panel, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(288f, 0f), new Vector2(0f, 300f));
        panel.gameObject.AddComponent<Image>().color = new Color(0.96f, 0.96f, 0.96f, 1f);

        var topBar = CreateUiRect("TopBar", panel);
        SetRect(topBar, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(12f, -44f), new Vector2(-12f, -8f));
        topBar.gameObject.AddComponent<Image>().color = new Color(0.96f, 0.96f, 0.96f, 1f);
        var topLayout = topBar.gameObject.AddComponent<HorizontalLayoutGroup>();
        topLayout.spacing = 10f;
        topLayout.childControlWidth = true;
        topLayout.childControlHeight = true;

        var projectInput = CreateInputField("Input_ProjectName", topBar, "ProjectName");
        projectInput.gameObject.AddComponent<LayoutElement>().minWidth = 360f;
        var addBtn = CreateButton("Button_AddStep", topBar, "+ Step");
        addBtn.gameObject.AddComponent<LayoutElement>().minWidth = 120f;
        var saveBtn = CreateButton("Button_SaveCurriculum", topBar, "Save");
        saveBtn.gameObject.AddComponent<LayoutElement>().minWidth = 110f;
        var status = CreateText("Text_Status", topBar, "");
        status.alignment = TextAnchor.MiddleLeft;
        status.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;

        var nodeArea = CreateUiRect("NodeArea", panel);
        SetRect(nodeArea, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(12f, 8f), new Vector2(-12f, -52f));
        nodeArea.gameObject.AddComponent<Image>().color = Color.white;

        var lineLayer = CreateUiRect("LineLayer", nodeArea);
        SetRect(lineLayer, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        var lineLayerImage = lineLayer.gameObject.AddComponent<Image>();
        lineLayerImage.color = new Color(0f, 0f, 0f, 0f);
        lineLayerImage.raycastTarget = false;
        var lineTemplate = CreateUiRect("LineTemplate", lineLayer);
        SetRect(lineTemplate, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        var lineGraphic = lineTemplate.gameObject.AddComponent<ConnectionLineGraphic>();
        lineGraphic.raycastTarget = false;
        lineTemplate.gameObject.SetActive(false);

        var nodeTemplate = BuildStepNodeTemplate(nodeArea);
        var resizeHandle = CreateUiRect("ResizeHandle", panel);
        SetRect(resizeHandle, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -6f), new Vector2(0f, 6f));
        resizeHandle.gameObject.AddComponent<Image>().color = new Color(0.96f, 0.96f, 0.96f, 1f);
        var resizeComp = resizeHandle.gameObject.AddComponent<PanelVerticalResizeHandle>();
        resizeComp.targetPanel = panel;

        var scenario = panel.gameObject.AddComponent<ScenarioGraphUI>();
        var so = new SerializedObject(scenario);
        so.FindProperty("panelRoot").objectReferenceValue = panel;
        so.FindProperty("projectNameInput").objectReferenceValue = projectInput;
        so.FindProperty("addStepButton").objectReferenceValue = addBtn;
        so.FindProperty("saveButton").objectReferenceValue = saveBtn;
        so.FindProperty("statusText").objectReferenceValue = status;
        so.FindProperty("nodeArea").objectReferenceValue = nodeArea;
        so.FindProperty("lineLayer").objectReferenceValue = lineLayer;
        so.FindProperty("nodeTemplate").objectReferenceValue = nodeTemplate;
        so.FindProperty("lineTemplate").objectReferenceValue = lineGraphic;
        so.FindProperty("resizeHandle").objectReferenceValue = resizeComp;
        so.ApplyModifiedPropertiesWithoutUndo();

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
        root.gameObject.AddComponent<Image>().color = new Color(1f, 0.98f, 0.86f, 1f);
        var stepNode = root.gameObject.AddComponent<StepNodeUI>();

        var stepId = CreateText("Text_StepId", root, "step-0000");
        SetRect(stepId.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(12f, -24f), new Vector2(-44f, -4f));

        var warning = CreateText("Warning", root, "!");
        warning.alignment = TextAnchor.MiddleCenter;
        warning.color = new Color(1f, 0.82f, 0f, 1f);
        warning.fontSize = 18;
        SetRect(warning.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-30f, -30f), new Vector2(-10f, -10f));

        var dragHandle = CreateUiRect("DragHandle", root);
        SetRect(dragHandle, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -28f), new Vector2(0f, 0f));
        dragHandle.gameObject.AddComponent<Image>().color = new Color(0.98f, 0.93f, 0.70f, 1f);
        var drag = dragHandle.gameObject.AddComponent<NodeDragHandler>();
        drag.target = root;

        var title = CreateInputField("Input_Title", root, "タイトル");
        SetRect(title.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(12f, -60f), new Vector2(-12f, -30f));

        var inputConnector = CreateButton("InputConnector", root, "");
        SetRect(inputConnector.GetComponent<RectTransform>(), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(-14f, -12f), new Vector2(10f, 12f));
        inputConnector.GetComponent<Image>().color = new Color(0.99f, 0.94f, 0.70f, 1f);
        var outputConnector = CreateButton("OutputConnector", root, "");
        SetRect(outputConnector.GetComponent<RectTransform>(), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-10f, -12f), new Vector2(14f, 12f));
        outputConnector.GetComponent<Image>().color = new Color(0.99f, 0.94f, 0.70f, 1f);

        var conditionList = CreateUiRect("ConditionList", root);
        SetRect(conditionList, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(16f, 12f), new Vector2(-16f, 88f));
        var v = conditionList.gameObject.AddComponent<VerticalLayoutGroup>();
        v.spacing = 8f;
        v.childControlHeight = true;
        v.childControlWidth = true;

        var rowTemplate = BuildConditionRowTemplate(conditionList);

        stepNode.stepIdText = stepId;
        stepNode.titleInput = title;
        stepNode.warningIcon = warning.gameObject;
        stepNode.inputConnector = inputConnector;
        stepNode.outputConnector = outputConnector;
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
        row.gameObject.AddComponent<Image>().color = new Color(1f, 0.99f, 0.92f, 1f);
        row.gameObject.AddComponent<LayoutElement>().minHeight = 76f;
        var v = row.gameObject.AddComponent<VerticalLayoutGroup>();
        v.spacing = 8f;
        v.childControlHeight = true;
        v.childControlWidth = true;

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
        dropdownA.GetComponent<Image>().color = new Color(1f, 0.98f, 0.86f, 1f);
        dropdownA.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
        var textA = CreateText("Text_AfterA", lineA, "を");
        textA.gameObject.AddComponent<LayoutElement>().minWidth = 24f;

        var dropdownB = CreateDropdown("DropdownB", lineB);
        dropdownB.GetComponent<Image>().color = new Color(1f, 0.98f, 0.86f, 1f);
        dropdownB.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
        var textB = CreateText("Text_AfterB", lineB, "に近づけたら");
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
        root.gameObject.AddComponent<Image>().color = Color.white;
        var dropdown = root.gameObject.AddComponent<Dropdown>();

        var caption = CreateText("Caption", root, "未設定");
        SetRect(caption.rectTransform, Vector2.zero, Vector2.one, new Vector2(8f, 0f), new Vector2(-22f, 0f));

        var template = CreateUiRect("Template", root);
        SetRect(template, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, -120f), new Vector2(0f, 0f));
        template.gameObject.AddComponent<Image>().color = Color.white;
        var scroll = template.gameObject.AddComponent<ScrollRect>();
        template.gameObject.SetActive(false);

        var viewport = CreateUiRect("Viewport", template);
        SetRect(viewport, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        viewport.gameObject.AddComponent<Image>().color = Color.white;
        viewport.gameObject.AddComponent<Mask>().showMaskGraphic = false;

        var content = CreateUiRect("Content", viewport);
        SetRect(content, new Vector2(0f, 1f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
        var contentLayout = content.gameObject.AddComponent<VerticalLayoutGroup>();
        contentLayout.childForceExpandHeight = false;
        contentLayout.childControlHeight = true;
        content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var item = CreateUiRect("Item", content);
        item.sizeDelta = new Vector2(0f, 24f);
        item.gameObject.AddComponent<Image>().color = new Color(0.96f, 0.96f, 0.96f, 1f);
        item.gameObject.AddComponent<Toggle>();
        var itemLabel = CreateText("Item Label", item, "Option");
        SetRect(itemLabel.rectTransform, Vector2.zero, Vector2.one, new Vector2(8f, 0f), new Vector2(-8f, 0f));

        scroll.viewport = viewport;
        scroll.content = content;
        scroll.horizontal = false;

        dropdown.targetGraphic = root.GetComponent<Image>();
        dropdown.captionText = caption;
        dropdown.template = template;
        dropdown.itemText = itemLabel;
        dropdown.options = new System.Collections.Generic.List<Dropdown.OptionData> { new Dropdown.OptionData("未設定") };
        return dropdown;
    }

    static InputField CreateInputField(string name, Transform parent, string placeholderText)
    {
        var root = CreateUiRect(name, parent);
        root.gameObject.AddComponent<Image>().color = Color.white;
        var input = root.gameObject.AddComponent<InputField>();
        var text = CreateText("Text", root, "");
        SetRect(text.rectTransform, Vector2.zero, Vector2.one, new Vector2(8f, 0f), new Vector2(-8f, 0f));
        var placeholder = CreateText("Placeholder", root, placeholderText);
        placeholder.color = new Color(0.55f, 0.55f, 0.55f, 1f);
        SetRect(placeholder.rectTransform, Vector2.zero, Vector2.one, new Vector2(8f, 0f), new Vector2(-8f, 0f));
        input.textComponent = text;
        input.placeholder = placeholder;
        return input;
    }

    static Button CreateButton(string name, Transform parent, string label)
    {
        var root = CreateUiRect(name, parent);
        root.gameObject.AddComponent<Image>().color = new Color(1f, 0.98f, 0.86f, 1f);
        var btn = root.gameObject.AddComponent<Button>();
        var txt = CreateText("Label", root, label);
        txt.alignment = TextAnchor.MiddleCenter;
        SetRect(txt.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        return btn;
    }

    static Text CreateText(string name, Transform parent, string value)
    {
        var textGo = new GameObject(name, typeof(RectTransform), typeof(Text));
        textGo.transform.SetParent(parent, false);
        var text = textGo.GetComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.color = Color.black;
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
