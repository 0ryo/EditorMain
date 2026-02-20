using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ScenarioGraphUI : MonoBehaviour
{
    [Header("Services")]
    public CurriculumGraphService graph;

    [Header("Layout")]
    public float panelHeight = 280f;

    Font defaultFont;
    RectTransform panelRoot;
    InputField projectNameInput;
    Button addStepButton;
    Text statusText;
    RectTransform nodeArea;
    RectTransform lineLayer;
    StepNodeUI nodeTemplate;
    ConnectionLineGraphic lineTemplate;

    string linkingFromStepId;
    readonly Dictionary<string, StepNodeUI> nodeUIs = new Dictionary<string, StepNodeUI>();
    readonly List<ConnectionLineGraphic> lines = new List<ConnectionLineGraphic>();

    void Start()
    {
        EnsureGraphService();
        BuildRuntimePanelIfNeeded();
        BindTopControls();

        if (graph.curriculum.steps.Count == 0)
        {
            graph.AddStep();
        }

        projectNameInput.SetTextWithoutNotify(graph.curriculum.projectName);
        RebuildAll();
    }

    void EnsureGraphService()
    {
        if (graph != null) return;

        graph = FindObjectOfType<CurriculumGraphService>();
        if (graph != null) return;

        var go = new GameObject("CurriculumGraphService");
        graph = go.AddComponent<CurriculumGraphService>();
    }

    void BuildRuntimePanelIfNeeded()
    {
        defaultFont = Resources.GetBuiltinResource<Font>("Arial.ttf");

        var canvas = FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            var canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
        }

        panelRoot = FindOrCreateRect("Panel_ScenarioGraph", canvas.transform);
        SetAnchors(panelRoot, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 0f), new Vector2(0f, panelHeight));
        EnsureImage(panelRoot.gameObject, new Color(0f, 0f, 0f, 0.55f));

        var topBar = FindOrCreateRect("TopBar", panelRoot);
        SetAnchors(topBar, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(12f, -44f), new Vector2(-12f, -8f));
        EnsureHorizontalLayout(topBar.gameObject, 10f);

        var projectFieldRoot = FindOrCreateRect("Input_ProjectName", topBar);
        projectNameInput = EnsureInputField(projectFieldRoot.gameObject, "ProjectName");
        SetMinWidth(projectFieldRoot.gameObject, 360f);

        var addButtonRoot = FindOrCreateRect("Button_AddStep", topBar);
        addStepButton = EnsureButton(addButtonRoot.gameObject, "+ Step");
        SetMinWidth(addButtonRoot.gameObject, 120f);

        var statusRoot = FindOrCreateRect("Text_Status", topBar);
        statusText = EnsureText(statusRoot.gameObject, "");
        statusText.alignment = TextAnchor.MiddleLeft;
        SetFlexibleWidth(statusRoot.gameObject);

        nodeArea = FindOrCreateRect("NodeArea", panelRoot);
        SetAnchors(nodeArea, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(12f, 8f), new Vector2(-12f, -52f));
        EnsureImage(nodeArea.gameObject, new Color(1f, 1f, 1f, 0.04f));

        lineLayer = FindOrCreateRect("LineLayer", nodeArea);
        SetAnchors(lineLayer, new Vector2(0f, 0f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
        EnsureImage(lineLayer.gameObject, new Color(0f, 0f, 0f, 0f)).raycastTarget = false;

        nodeTemplate = BuildNodeTemplate(nodeArea);
        lineTemplate = BuildLineTemplate(lineLayer);
    }

    void BindTopControls()
    {
        addStepButton.onClick.RemoveAllListeners();
        addStepButton.onClick.AddListener(() =>
        {
            graph.AddStep();
            RebuildAll();
        });
    }

    void RebuildAll()
    {
        graph.RepairBrokenReferences();

        foreach (Transform child in nodeArea)
        {
            if (child == nodeTemplate.transform || child == lineLayer) continue;
            Destroy(child.gameObject);
        }

        nodeUIs.Clear();

        float x = 30f;
        float y = -22f;
        foreach (var step in graph.curriculum.steps)
        {
            var ui = Instantiate(nodeTemplate, nodeArea);
            ui.gameObject.name = $"StepNode_{step.id}";
            ui.gameObject.SetActive(true);

            var rt = ui.GetComponent<RectTransform>();
            rt.anchoredPosition = new Vector2(x, y);

            y -= 170f;
            if (y < -320f)
            {
                y = -22f;
                x += 420f;
            }

            ui.Bind(graph, step);
            ui.onClickOutputConnector = OnClickOutputConnector;
            ui.onClickInputConnector = OnClickInputConnector;
            ui.onChanged = RefreshLines;

            nodeUIs[step.id] = ui;
        }

        RefreshLines();
        statusText.text = string.IsNullOrEmpty(linkingFromStepId) ? "" : "接続先の入力コネクタをクリック";
    }

    void OnClickOutputConnector(string fromStepId)
    {
        linkingFromStepId = fromStepId;
        statusText.text = "接続先の入力コネクタをクリック";
    }

    void OnClickInputConnector(string toStepId)
    {
        if (string.IsNullOrEmpty(linkingFromStepId)) return;

        graph.AddEdge(linkingFromStepId, toStepId);
        linkingFromStepId = null;
        statusText.text = "";
        RefreshLines();
    }

    void RefreshLines()
    {
        foreach (var line in lines)
        {
            if (line != null) Destroy(line.gameObject);
        }

        lines.Clear();

        foreach (var step in graph.curriculum.steps)
        {
            if (!nodeUIs.TryGetValue(step.id, out var fromUi)) continue;

            foreach (var nextId in step.nextStepIds)
            {
                if (!nodeUIs.TryGetValue(nextId, out var toUi)) continue;

                var line = Instantiate(lineTemplate, lineLayer);
                line.gameObject.SetActive(true);
                line.from = fromUi.outputConnector.GetComponent<RectTransform>();
                line.to = toUi.inputConnector.GetComponent<RectTransform>();
                line.color = new Color(1f, 1f, 1f, 0.9f);
                lines.Add(line);
            }
        }
    }

    StepNodeUI BuildNodeTemplate(Transform parent)
    {
        var existing = parent.Find("StepNodeTemplate");
        if (existing != null)
        {
            var existingNode = existing.GetComponent<StepNodeUI>();
            if (existingNode != null) return existingNode;
        }

        var root = new GameObject("StepNodeTemplate", typeof(RectTransform), typeof(Image), typeof(StepNodeUI));
        root.transform.SetParent(parent, false);
        var rt = root.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(390f, 160f);
        EnsureImage(root, new Color(0.11f, 0.11f, 0.11f, 0.95f));

        var node = root.GetComponent<StepNodeUI>();

        var idText = CreateText("Text_StepId", root.transform, "step-0000", 14, TextAnchor.MiddleLeft);
        SetAnchors(idText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(12f, -24f), new Vector2(-44f, -4f));

        var warning = CreateText("Warning", root.transform, "!", 18, TextAnchor.MiddleCenter);
        warning.color = new Color(1f, 0.82f, 0f, 1f);
        SetAnchors(warning.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-30f, -30f), new Vector2(-10f, -10f));

        var titleInputGo = new GameObject("Input_Title", typeof(RectTransform), typeof(Image), typeof(InputField));
        titleInputGo.transform.SetParent(root.transform, false);
        SetAnchors(titleInputGo.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(12f, -60f), new Vector2(-12f, -30f));
        EnsureImage(titleInputGo, new Color(1f, 1f, 1f, 0.1f));
        var titleInput = titleInputGo.GetComponent<InputField>();
        titleInput.textComponent = CreateText("Text", titleInputGo.transform, "", 14, TextAnchor.MiddleLeft);
        SetAnchors(titleInput.textComponent.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(8f, 0f), new Vector2(-8f, 0f));
        titleInput.placeholder = CreateText("Placeholder", titleInputGo.transform, "タイトル", 14, TextAnchor.MiddleLeft);
        titleInput.placeholder.color = new Color(1f, 1f, 1f, 0.45f);
        SetAnchors(((Text)titleInput.placeholder).rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(8f, 0f), new Vector2(-8f, 0f));

        var inputButton = CreateCircleButton("InputConnector", root.transform, "<");
        SetAnchors(inputButton.GetComponent<RectTransform>(), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(6f, -12f), new Vector2(30f, 12f));
        var outputButton = CreateCircleButton("OutputConnector", root.transform, ">");
        SetAnchors(outputButton.GetComponent<RectTransform>(), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-30f, -12f), new Vector2(-6f, 12f));

        var listRoot = FindOrCreateRect("ConditionList", root.transform);
        SetAnchors(listRoot, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(12f, 36f), new Vector2(-12f, 96f));
        EnsureVerticalLayout(listRoot.gameObject, 4f);

        var addCondition = EnsureButton(FindOrCreateRect("Button_AddCondition", root.transform).gameObject, "+ 条件");
        SetAnchors(addCondition.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(12f, 8f), new Vector2(-12f, 30f));

        var rowTemplate = BuildConditionRowTemplate(listRoot);

        node.stepIdText = idText;
        node.titleInput = titleInput;
        node.warningIcon = warning.gameObject;
        node.inputConnector = inputButton;
        node.outputConnector = outputButton;
        node.conditionListRoot = listRoot;
        node.conditionRowTemplate = rowTemplate;
        node.addConditionButton = addCondition;

        root.SetActive(false);
        rowTemplate.gameObject.SetActive(false);
        return node;
    }

    ConditionRowUI BuildConditionRowTemplate(Transform parent)
    {
        var existing = parent.Find("ConditionRowTemplate");
        if (existing != null)
        {
            var existingRow = existing.GetComponent<ConditionRowUI>();
            if (existingRow != null) return existingRow;
        }

        var rowGo = new GameObject("ConditionRowTemplate", typeof(RectTransform), typeof(ConditionRowUI), typeof(Image));
        rowGo.transform.SetParent(parent, false);
        EnsureImage(rowGo, new Color(1f, 1f, 1f, 0.05f));
        var rowRect = rowGo.GetComponent<RectTransform>();
        rowRect.sizeDelta = new Vector2(0f, 30f);
        SetMinHeight(rowGo, 30f);
        EnsureHorizontalLayout(rowGo, 4f);

        var row = rowGo.GetComponent<ConditionRowUI>();
        row.dropdownA = CreateDropdown("DropdownA", rowGo.transform);
        row.dropdownB = CreateDropdown("DropdownB", rowGo.transform);
        row.removeButton = EnsureButton(FindOrCreateRect("ButtonRemove", rowGo.transform).gameObject, "x");

        SetFlexibleWidth(row.dropdownA.gameObject);
        SetFlexibleWidth(row.dropdownB.gameObject);
        SetMinWidth(row.removeButton.gameObject, 30f);

        return row;
    }

    ConnectionLineGraphic BuildLineTemplate(Transform parent)
    {
        var existing = parent.Find("LineTemplate");
        if (existing != null)
        {
            var existingLine = existing.GetComponent<ConnectionLineGraphic>();
            if (existingLine != null) return existingLine;
        }

        var go = new GameObject("LineTemplate", typeof(RectTransform), typeof(ConnectionLineGraphic));
        go.transform.SetParent(parent, false);
        SetAnchors(go.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
        var line = go.GetComponent<ConnectionLineGraphic>();
        line.raycastTarget = false;
        go.SetActive(false);
        return line;
    }

    RectTransform FindOrCreateRect(string name, Transform parent)
    {
        var child = parent.Find(name);
        if (child != null) return child as RectTransform;

        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go.GetComponent<RectTransform>();
    }

    void SetAnchors(RectTransform rt, Vector2 min, Vector2 max, Vector2 offsetMin, Vector2 offsetMax)
    {
        rt.anchorMin = min;
        rt.anchorMax = max;
        rt.offsetMin = offsetMin;
        rt.offsetMax = offsetMax;
    }

    Image EnsureImage(GameObject go, Color color)
    {
        var image = go.GetComponent<Image>();
        if (image == null) image = go.AddComponent<Image>();
        image.color = color;
        return image;
    }

    Text EnsureText(GameObject go, string value)
    {
        var text = go.GetComponent<Text>();
        if (text == null) text = go.AddComponent<Text>();
        text.font = defaultFont;
        text.color = Color.white;
        text.fontSize = 14;
        text.text = value;
        return text;
    }

    Text CreateText(string name, Transform parent, string value, int fontSize, TextAnchor anchor)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);
        var text = go.GetComponent<Text>();
        text.font = defaultFont;
        text.text = value;
        text.fontSize = fontSize;
        text.color = Color.white;
        text.alignment = anchor;
        return text;
    }

    InputField EnsureInputField(GameObject go, string placeholder)
    {
        EnsureImage(go, new Color(1f, 1f, 1f, 0.1f));
        var input = go.GetComponent<InputField>();
        if (input == null) input = go.AddComponent<InputField>();

        if (input.textComponent == null)
        {
            input.textComponent = CreateText("Text", go.transform, "", 14, TextAnchor.MiddleLeft);
            SetAnchors(input.textComponent.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(8f, 0f), new Vector2(-8f, 0f));
        }

        if (input.placeholder == null)
        {
            var ph = CreateText("Placeholder", go.transform, placeholder, 14, TextAnchor.MiddleLeft);
            ph.color = new Color(1f, 1f, 1f, 0.45f);
            SetAnchors(ph.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(8f, 0f), new Vector2(-8f, 0f));
            input.placeholder = ph;
        }

        return input;
    }

    Button EnsureButton(GameObject go, string label)
    {
        EnsureImage(go, new Color(1f, 1f, 1f, 0.14f));
        var button = go.GetComponent<Button>();
        if (button == null) button = go.AddComponent<Button>();

        var text = go.GetComponentInChildren<Text>();
        if (text == null)
        {
            text = CreateText("Label", go.transform, label, 14, TextAnchor.MiddleCenter);
            SetAnchors(text.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
        }
        else
        {
            text.text = label;
        }

        return button;
    }

    Button CreateCircleButton(string name, Transform parent, string label)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var button = EnsureButton(go, label);
        var image = go.GetComponent<Image>();
        image.color = new Color(1f, 1f, 1f, 0.22f);
        return button;
    }

    Dropdown CreateDropdown(string name, Transform parent)
    {
        var root = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Dropdown));
        root.transform.SetParent(parent, false);
        EnsureImage(root, new Color(1f, 1f, 1f, 0.12f));

        var caption = CreateText("Caption", root.transform, "未設定", 13, TextAnchor.MiddleLeft);
        SetAnchors(caption.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(8f, 0f), new Vector2(-22f, 0f));

        var template = new GameObject("Template", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
        template.transform.SetParent(root.transform, false);
        var templateRect = template.GetComponent<RectTransform>();
        SetAnchors(templateRect, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, -120f), new Vector2(0f, 0f));
        template.SetActive(false);

        var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
        viewport.transform.SetParent(template.transform, false);
        var viewportRect = viewport.GetComponent<RectTransform>();
        SetAnchors(viewportRect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        viewport.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.2f);
        viewport.GetComponent<Mask>().showMaskGraphic = false;

        var content = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        content.transform.SetParent(viewport.transform, false);
        var contentRect = content.GetComponent<RectTransform>();
        SetAnchors(contentRect, new Vector2(0f, 1f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
        content.GetComponent<VerticalLayoutGroup>().childForceExpandHeight = false;
        content.GetComponent<VerticalLayoutGroup>().childControlHeight = true;
        content.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var item = new GameObject("Item", typeof(RectTransform), typeof(Toggle));
        item.transform.SetParent(content.transform, false);
        item.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, 24f);
        EnsureImage(item, new Color(1f, 1f, 1f, 0.08f));

        var itemLabel = CreateText("Item Label", item.transform, "Option", 13, TextAnchor.MiddleLeft);
        SetAnchors(itemLabel.rectTransform, Vector2.zero, Vector2.one, new Vector2(8f, 0f), new Vector2(-8f, 0f));

        var scroll = template.GetComponent<ScrollRect>();
        scroll.viewport = viewportRect;
        scroll.content = contentRect;
        scroll.horizontal = false;
        scroll.vertical = true;

        var dropdown = root.GetComponent<Dropdown>();
        dropdown.targetGraphic = root.GetComponent<Image>();
        dropdown.captionText = caption;
        dropdown.template = templateRect;
        dropdown.itemText = itemLabel;
        dropdown.options = new List<Dropdown.OptionData> { new Dropdown.OptionData("未設定") };
        return dropdown;
    }

    void EnsureHorizontalLayout(GameObject go, float spacing)
    {
        var layout = go.GetComponent<HorizontalLayoutGroup>();
        if (layout == null) layout = go.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = spacing;
        layout.childForceExpandHeight = true;
        layout.childForceExpandWidth = false;
        layout.childControlHeight = true;
        layout.childControlWidth = true;
        layout.padding = new RectOffset(0, 0, 0, 0);
    }

    void EnsureVerticalLayout(GameObject go, float spacing)
    {
        var layout = go.GetComponent<VerticalLayoutGroup>();
        if (layout == null) layout = go.AddComponent<VerticalLayoutGroup>();
        layout.spacing = spacing;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = true;
        layout.childControlHeight = true;
        layout.childControlWidth = true;
    }

    void SetMinWidth(GameObject go, float minWidth)
    {
        var e = go.GetComponent<LayoutElement>();
        if (e == null) e = go.AddComponent<LayoutElement>();
        e.minWidth = minWidth;
    }

    void SetMinHeight(GameObject go, float minHeight)
    {
        var e = go.GetComponent<LayoutElement>();
        if (e == null) e = go.AddComponent<LayoutElement>();
        e.minHeight = minHeight;
    }

    void SetFlexibleWidth(GameObject go)
    {
        var e = go.GetComponent<LayoutElement>();
        if (e == null) e = go.AddComponent<LayoutElement>();
        e.flexibleWidth = 1f;
    }
}
