using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public class ScenarioGraphUI : MonoBehaviour
{
    static readonly Color ConnectionLineColor = DesignTokens.Accent;
    static readonly Color DragPreviewLineColor = new Color(DesignTokens.Accent.r, DesignTokens.Accent.g, DesignTokens.Accent.b, 0.9f);

    static readonly System.Collections.Generic.Dictionary<string, string> ErrorMessages = new System.Collections.Generic.Dictionary<string, string>
    {
        { "E-01", "スタートノードがありません" },
        { "E-02", "エンドノードがありません" },
        { "E-03", "スタートノードが次のノードに接続されていません" },
        { "E-04", "ステップが正しく繋がっていません" },
        { "E-05", "エンドノードに前のノードが接続されていません" },
        { "E-06", "手順が設定されていないステップがあります" },
        { "E-07", "どのステップにも紐付いていない手順があります" },
        { "E-08", "オブジェクトが選択されていない手順があります" },
        { "E-09", "AとBに同じオブジェクトが設定されている手順があります" },
        { "E-10", "手順で参照しているオブジェクトが削除されています" },
        { "E-11", "データが不整合な状態です。編集をやり直してください" },
    };

    static readonly System.Collections.Generic.Dictionary<string, string> WarningMessages = new System.Collections.Generic.Dictionary<string, string>
    {
        { "W-01", "手順が上限（3件）に達しているステップがあります" },
        { "W-02", "複数のステップで同じオブジェクトAが使われています" },
    };

    static readonly System.Collections.Generic.Dictionary<string, string> ConnectReasonMessages = new System.Collections.Generic.Dictionary<string, string>
    {
        { "CONNECT_EMPTY_ID", "接続情報が不正です" },
        { "CONNECT_SELF", "自分自身には接続できません" },
        { "CONNECT_NODE_NOT_FOUND", "接続先のノードが見つかりません" },
        { "CONNECT_INVALID_ROUTE", "この組み合わせは接続できません" },
        { "CONNECT_DUPLICATE", "すでに接続済みです" },
        { "STEPFLOW_OUT_LIMIT", "このノードはすでに次のノードに繋がっています" },
        { "STEPFLOW_IN_LIMIT", "このノードはすでに前のノードに繋がっています" },
        { "END_IN_LIMIT", "エンドノードはすでに接続済みです" },
        { "STEPFLOW_CYCLE", "接続すると経路が循環してしまいます" },
        { "CONDITION_BIND_LIMIT", "この手順はすでにステップに紐付いています" },
        { "STEP_CONDITION_MAX", "このステップの手順は上限（3件）です" },
    };

    [Header("Services")]
    [SerializeField] CurriculumGraphService graph;

    [Header("Controls")]
    [SerializeField] RectTransform panelRoot;
    [SerializeField] InputField projectNameInput;
    [SerializeField] Button addStepButton;
    [SerializeField] Button addConditionButton;
    [SerializeField] Button saveButton;
    [SerializeField] Text statusText;
    [SerializeField] RectTransform nodeArea;
    [SerializeField] RectTransform graphContent;
    [SerializeField] RectTransform lineLayer;
    [SerializeField] ConnectionLineGraphic lineTemplate;

    [Header("Node Templates")]
    [SerializeField] StepNodeUI stepNodeTemplate;
    [SerializeField] StepNodeUI nodeTemplate; // legacy field
    [SerializeField] ConditionNodeUI conditionNodeTemplate;
    [SerializeField] TerminalNodeUI startNodeTemplate;
    [SerializeField] TerminalNodeUI endNodeTemplate;

    [Header("Layout")]
    [SerializeField] PanelVerticalResizeHandle resizeHandle;
    [SerializeField] float cornerRadius = DesignTokens.CornerRadius;

    [Header("Condition Embed")]
    [SerializeField] float conditionEmbedSnapDistance = 80f;

    string linkingFromNodeId;
    string draggingFromNodeId;
    ConnectionLineGraphic dragPreviewLine;
    RectTransform dragPreviewTarget;
    float nextValidationPollTime;

    class NodeUiBinding
    {
        public ScenarioNodeType nodeType;
        public RectTransform root;
        public RectTransform inputConnector;
        public RectTransform outputConnector;
    }

    readonly Dictionary<string, NodeUiBinding> nodeUIs = new Dictionary<string, NodeUiBinding>();
    readonly Dictionary<string, Vector2> nodePositions = new Dictionary<string, Vector2>();
    readonly List<ConnectionLineGraphic> lines = new List<ConnectionLineGraphic>();

    void Awake()
    {
        EnsureGraphService();
        ValidateAndBindReferences();
    }

    void Start()
    {
        cornerRadius = DesignTokens.CornerRadius;
        graph.EnsureGraphInitialized();
        if (graph.GetNodes(ScenarioNodeType.Step).Count == 0)
        {
            graph.AddStep();
        }

        if (projectNameInput != null)
        {
            projectNameInput.SetTextWithoutNotify(graph.curriculum.projectName);
        }

        RebuildAll();
        DesignTokenApplier.ApplyScenarioPanel(panelRoot != null ? panelRoot : transform as RectTransform);
    }

    void Update()
    {
        if (graph == null || !isActiveAndEnabled) return;
        if (Time.unscaledTime < nextValidationPollTime) return;

        nextValidationPollTime = Time.unscaledTime + 0.5f;
        if (graph.RepairBrokenReferences())
        {
            RebuildAll();
            return;
        }

        RefreshValidationStatus();
    }

    void OnRectTransformDimensionsChange()
    {
        ClampNodesToNodeArea();
    }

    void EnsureGraphService()
    {
        if (graph != null) return;

        graph = FindObjectOfType<CurriculumGraphService>();
        if (graph != null) return;

        var go = new GameObject("CurriculumGraphService");
        graph = go.AddComponent<CurriculumGraphService>();
    }

    void ValidateAndBindReferences()
    {
        if (stepNodeTemplate == null)
        {
            stepNodeTemplate = nodeTemplate;
        }

        if (stepNodeTemplate == null)
        {
            Debug.LogError("[ScenarioGraphUI] Step node template is not assigned.");
            enabled = false;
            return;
        }

        if (projectNameInput == null || addStepButton == null || saveButton == null ||
            statusText == null || nodeArea == null || lineLayer == null || lineTemplate == null)
        {
            Debug.LogError("[ScenarioGraphUI] UI references are not assigned on prefab.");
            enabled = false;
            return;
        }

        EnsureNodeAreaMask();
        EnsureGraphContent();
        EnsurePanZoomController();

        if (addConditionButton == null)
        {
            addConditionButton = CreateRuntimeConditionButton();
        }

        EnsureRuntimeTemplates();
        ApplyRoundedTheme();

        if (resizeHandle != null && panelRoot != null)
        {
            resizeHandle.targetPanel = panelRoot;
        }

        addStepButton.onClick.RemoveAllListeners();
        addStepButton.onClick.AddListener(() =>
        {
            graph.AddStep();
            RebuildAll();
        });

        if (addConditionButton != null)
        {
            addConditionButton.onClick.RemoveAllListeners();
            addConditionButton.onClick.AddListener(() =>
            {
                graph.AddCondition();
                RebuildAll();
            });
        }

        saveButton.onClick.RemoveAllListeners();
        saveButton.onClick.AddListener(SaveScenarioExport);

        projectNameInput.onEndEdit.RemoveAllListeners();
        projectNameInput.onEndEdit.AddListener(_ =>
        {
            graph.curriculum.projectName = string.IsNullOrWhiteSpace(projectNameInput.text)
                ? "VRCourseEditor"
                : projectNameInput.text.Trim();
            RefreshValidationStatus();
        });
    }

    Button CreateRuntimeConditionButton()
    {
        if (addStepButton == null) return null;

        var cloned = Instantiate(addStepButton, addStepButton.transform.parent);
        cloned.gameObject.name = "Button_AddCondition_Runtime";
        cloned.transform.SetSiblingIndex(addStepButton.transform.GetSiblingIndex() + 1);

        var label = cloned.GetComponentInChildren<Text>(true);
        if (label != null)
        {
            label.text = "+ Condition";
        }

        return cloned;
    }

    void EnsureRuntimeTemplates()
    {
        if (startNodeTemplate == null)
        {
            startNodeTemplate = CreateTerminalTemplateFromStepTemplate(
                "StartNodeTemplate_Runtime",
                "START",
                hasInput: false,
                hasOutput: true,
                color: DesignTokens.BgSecondary);
        }

        if (endNodeTemplate == null)
        {
            endNodeTemplate = CreateTerminalTemplateFromStepTemplate(
                "EndNodeTemplate_Runtime",
                "END",
                hasInput: true,
                hasOutput: false,
                color: DesignTokens.BgSecondary);
        }

        if (conditionNodeTemplate == null)
        {
            conditionNodeTemplate = CreateConditionTemplateFromStepTemplate();
        }
    }

    void EnsureNodeAreaMask()
    {
        if (nodeArea == null) return;
        if (nodeArea.GetComponent<RectMask2D>() != null) return;

        nodeArea.gameObject.AddComponent<RectMask2D>();
    }

    void EnsureGraphContent()
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
            graphContent.sizeDelta = new Vector2(4200f, 2400f);
            graphContent.anchoredPosition = Vector2.zero;
        }

        if (graphContent.rect.width < 1f || graphContent.rect.height < 1f)
        {
            graphContent.anchorMin = new Vector2(0.5f, 0.5f);
            graphContent.anchorMax = new Vector2(0.5f, 0.5f);
            graphContent.pivot = new Vector2(0.5f, 0.5f);
            graphContent.sizeDelta = new Vector2(4200f, 2400f);
            graphContent.anchoredPosition = Vector2.zero;
        }

        ReparentToGraphContent(lineLayer);
        if (stepNodeTemplate != null) ReparentToGraphContent(stepNodeTemplate.transform as RectTransform);
        if (conditionNodeTemplate != null) ReparentToGraphContent(conditionNodeTemplate.transform as RectTransform);
        if (startNodeTemplate != null) ReparentToGraphContent(startNodeTemplate.transform as RectTransform);
        if (endNodeTemplate != null) ReparentToGraphContent(endNodeTemplate.transform as RectTransform);
    }

    void ReparentToGraphContent(RectTransform child)
    {
        if (graphContent == null || child == null) return;
        if (child == graphContent) return;
        if (child.parent == graphContent) return;
        child.SetParent(graphContent, false);
    }

    void EnsurePanZoomController()
    {
        if (nodeArea == null || graphContent == null) return;

        var panZoom = nodeArea.GetComponent<NodeAreaPanZoomController>();
        if (panZoom == null)
        {
            panZoom = nodeArea.gameObject.AddComponent<NodeAreaPanZoomController>();
        }

        panZoom.Configure(nodeArea, graphContent);
    }

    Transform GetNodeParent()
    {
        return graphContent != null ? graphContent : nodeArea;
    }

    RectTransform GetNodeBoundsRoot()
    {
        return graphContent != null ? graphContent : nodeArea;
    }

    ConditionNodeUI CreateConditionTemplateFromStepTemplate()
    {
        var clone = Instantiate(stepNodeTemplate.gameObject, GetNodeParent());
        clone.name = "ConditionNodeTemplate_Runtime";
        clone.SetActive(false);

        var rootRt = clone.GetComponent<RectTransform>();
        if (rootRt != null)
        {
            rootRt.sizeDelta = new Vector2(390f, 180f);
        }

        var image = clone.GetComponent<Image>();
        if (image != null)
        {
            image.color = DesignTokens.Surface;
        }

        var sourceStepUi = clone.GetComponent<StepNodeUI>();
        if (sourceStepUi == null)
        {
            Debug.LogError("[ScenarioGraphUI] Failed to create condition template from step template.");
            return null;
        }

        sourceStepUi.enabled = false;
        if (sourceStepUi.titleInput != null) sourceStepUi.titleInput.gameObject.SetActive(true);
        if (sourceStepUi.conditionSummaryText != null) sourceStepUi.conditionSummaryText.gameObject.SetActive(false);
        if (sourceStepUi.inputConnector != null) sourceStepUi.inputConnector.gameObject.SetActive(false);

        ConditionRowUI row = null;
        if (sourceStepUi.conditionListRoot != null && sourceStepUi.conditionRowTemplate != null)
        {
            row = Instantiate(sourceStepUi.conditionRowTemplate, sourceStepUi.conditionListRoot);
            row.gameObject.SetActive(true);
            sourceStepUi.conditionRowTemplate.gameObject.SetActive(false);
            sourceStepUi.conditionListRoot.gameObject.SetActive(true);
            sourceStepUi.conditionListRoot.anchorMin = new Vector2(0f, 0f);
            sourceStepUi.conditionListRoot.anchorMax = new Vector2(1f, 1f);
            sourceStepUi.conditionListRoot.offsetMin = new Vector2(12f, 16f);
            sourceStepUi.conditionListRoot.offsetMax = new Vector2(-12f, -34f);
        }

        var conditionUi = clone.GetComponent<ConditionNodeUI>();
        if (conditionUi == null) conditionUi = clone.AddComponent<ConditionNodeUI>();
        conditionUi.nodeIdText = sourceStepUi.stepIdText;
        conditionUi.titleInput = sourceStepUi.titleInput;
        conditionUi.warningIcon = sourceStepUi.warningIcon;
        conditionUi.conditionRow = row;
        conditionUi.outputConnector = sourceStepUi.outputConnector;
        conditionUi.deleteButton = sourceStepUi.deleteButton;
        return conditionUi;
    }

    TerminalNodeUI CreateTerminalTemplateFromStepTemplate(
        string name,
        string label,
        bool hasInput,
        bool hasOutput,
        Color color)
    {
        var clone = Instantiate(stepNodeTemplate.gameObject, GetNodeParent());
        clone.name = name;
        clone.SetActive(false);

        var rootRt = clone.GetComponent<RectTransform>();
        if (rootRt != null)
        {
            rootRt.sizeDelta = new Vector2(230f, 96f);
        }

        var image = clone.GetComponent<Image>();
        if (image != null)
        {
            image.color = color;
        }

        var sourceStepUi = clone.GetComponent<StepNodeUI>();
        if (sourceStepUi == null)
        {
            Debug.LogError("[ScenarioGraphUI] Failed to create terminal template from step template.");
            return null;
        }

        sourceStepUi.enabled = false;
        if (sourceStepUi.warningIcon != null) sourceStepUi.warningIcon.SetActive(false);
        if (sourceStepUi.titleInput != null) sourceStepUi.titleInput.gameObject.SetActive(false);
        if (sourceStepUi.conditionListRoot != null) sourceStepUi.conditionListRoot.gameObject.SetActive(false);
        if (sourceStepUi.conditionSummaryText != null) sourceStepUi.conditionSummaryText.gameObject.SetActive(false);
        if (sourceStepUi.deleteButton != null) sourceStepUi.deleteButton.gameObject.SetActive(false);
        var legacyDeleteButton = clone.transform.Find("Button_Delete");
        if (legacyDeleteButton != null) legacyDeleteButton.gameObject.SetActive(false);
        if (sourceStepUi.stepIdText != null) sourceStepUi.stepIdText.text = label;
        if (sourceStepUi.inputConnector != null) sourceStepUi.inputConnector.gameObject.SetActive(hasInput);
        if (sourceStepUi.outputConnector != null) sourceStepUi.outputConnector.gameObject.SetActive(hasOutput);

        var terminalUi = clone.GetComponent<TerminalNodeUI>();
        if (terminalUi == null) terminalUi = clone.AddComponent<TerminalNodeUI>();
        terminalUi.labelText = sourceStepUi.stepIdText;
        terminalUi.inputConnector = sourceStepUi.inputConnector;
        terminalUi.outputConnector = sourceStepUi.outputConnector;
        return terminalUi;
    }

    void RebuildAll()
    {
        graph.RepairBrokenReferences();
        CancelConnectorDrag(clearStatus: false);

        foreach (var pair in nodeUIs)
        {
            if (pair.Value == null || pair.Value.root == null) continue;
            nodePositions[pair.Key] = pair.Value.root.anchoredPosition;
        }

        var nodeParent = GetNodeParent();
        if (nodeParent == null)
        {
            Debug.LogError("[ScenarioGraphUI] Node parent is missing.");
            return;
        }

        foreach (Transform child in nodeParent)
        {
            if (child == lineLayer) continue;
            if (child == stepNodeTemplate.transform) continue;
            if (conditionNodeTemplate != null && child == conditionNodeTemplate.transform) continue;
            if (startNodeTemplate != null && child == startNodeTemplate.transform) continue;
            if (endNodeTemplate != null && child == endNodeTemplate.transform) continue;
            Destroy(child.gameObject);
        }

        nodeUIs.Clear();
        var defaultPositions = BuildDefaultNodePositions();
        var stepIndexMap = graph.BuildStepIndexMap();

        foreach (var node in graph.curriculum.nodes.Where(n => n != null).OrderBy(GetNodeSortOrder).ThenBy(n => n.nodeId))
        {
            if (string.IsNullOrWhiteSpace(node.nodeId)) continue;

            switch (node.nodeType)
            {
                case ScenarioNodeType.Start:
                    InstantiateStartNode(node, defaultPositions);
                    break;
                case ScenarioNodeType.End:
                    InstantiateEndNode(node, defaultPositions);
                    break;
                case ScenarioNodeType.Step:
                    InstantiateStepNode(node, stepIndexMap, defaultPositions);
                    break;
                case ScenarioNodeType.Condition:
                    if (!graph.IsConditionBoundToStep(node.nodeId))
                    {
                        InstantiateConditionNode(node, defaultPositions);
                    }
                    break;
            }
        }

        if (lineLayer != null)
        {
            lineLayer.SetAsLastSibling();
        }

        ApplyRoundedTheme();
        DesignTokenApplier.ApplyNodeColors(GetNodeParent() as Transform);
        RefreshLines();
        RefreshValidationStatus();
        if (!string.IsNullOrEmpty(linkingFromNodeId))
        {
            statusText.text = "入力コネクタをクリックして接続";
        }
        else if (!string.IsNullOrEmpty(draggingFromNodeId))
        {
            statusText.text = "入力コネクタへドラッグしてドロップ";
        }
    }

    static int GetNodeSortOrder(ScenarioNode node)
    {
        return node.nodeType switch
        {
            ScenarioNodeType.Start => 0,
            ScenarioNodeType.Step => 1,
            ScenarioNodeType.Condition => 2,
            ScenarioNodeType.End => 3,
            _ => 9
        };
    }

    Dictionary<string, Vector2> BuildDefaultNodePositions()
    {
        var defaults = new Dictionary<string, Vector2>();

        var start = graph.GetStartNode();
        if (start != null) defaults[start.nodeId] = new Vector2(-620f, 120f);

        var orderedSteps = graph.GetDisplayOrderedSteps();
        for (int i = 0; i < orderedSteps.Count; i++)
        {
            if (orderedSteps[i] == null || string.IsNullOrWhiteSpace(orderedSteps[i].nodeId)) continue;
            defaults[orderedSteps[i].nodeId] = new Vector2(-320f + (i * 280f), 120f);
        }

        var end = graph.GetEndNode();
        if (end != null)
        {
            defaults[end.nodeId] = new Vector2(-320f + (orderedSteps.Count * 280f) + 280f, 120f);
        }

        var slotByStep = new Dictionary<string, int>();
        int unbound = 0;
        foreach (var condition in graph.GetNodes(ScenarioNodeType.Condition))
        {
            if (condition == null || string.IsNullOrWhiteSpace(condition.nodeId)) continue;
            if (graph.IsConditionBoundToStep(condition.nodeId)) continue;

            var bindEdge = graph.curriculum.edges.FirstOrDefault(e =>
                e.edgeType == ScenarioEdgeType.ConditionBind &&
                e.fromNodeId == condition.nodeId);

            if (bindEdge != null && defaults.TryGetValue(bindEdge.toNodeId, out var stepPos))
            {
                if (!slotByStep.TryGetValue(bindEdge.toNodeId, out int slot)) slot = 0;
                defaults[condition.nodeId] = new Vector2(stepPos.x, -40f - (slot * 120f));
                slotByStep[bindEdge.toNodeId] = slot + 1;
            }
            else
            {
                defaults[condition.nodeId] = new Vector2(-620f + (unbound % 3) * 240f, -60f - (unbound / 3) * 120f);
                unbound++;
            }
        }

        return defaults;
    }

    void InstantiateStartNode(ScenarioNode node, Dictionary<string, Vector2> defaults)
    {
        if (startNodeTemplate == null) return;

        var ui = Instantiate(startNodeTemplate, GetNodeParent());
        ui.gameObject.name = $"Node_{node.nodeId}";
        ui.gameObject.SetActive(true);
        ui.onClickOutputConnector = OnClickOutputConnector;
        ui.onBeginOutputConnectorDrag = BeginConnectorDrag;
        ui.onOutputConnectorDrag = UpdateConnectorDrag;
        ui.onCompleteConnectorDrag = CompleteConnectorDrag;
        ui.onCancelConnectorDrag = () => CancelConnectorDrag(clearStatus: true);
        ui.Bind(node, "START", allowInput: false, allowOutput: true);

        RegisterNode(
            node,
            ui.transform as RectTransform,
            null,
            ui.outputConnector != null ? ui.outputConnector.GetComponent<RectTransform>() : null,
            defaults);
    }

    void InstantiateEndNode(ScenarioNode node, Dictionary<string, Vector2> defaults)
    {
        if (endNodeTemplate == null) return;

        var ui = Instantiate(endNodeTemplate, GetNodeParent());
        ui.gameObject.name = $"Node_{node.nodeId}";
        ui.gameObject.SetActive(true);
        ui.onClickInputConnector = OnClickInputConnector;
        ui.Bind(node, "END", allowInput: true, allowOutput: false);

        RegisterNode(
            node,
            ui.transform as RectTransform,
            ui.inputConnector != null ? ui.inputConnector.GetComponent<RectTransform>() : null,
            null,
            defaults);
    }

    void InstantiateStepNode(ScenarioNode node, Dictionary<string, int> stepIndexMap, Dictionary<string, Vector2> defaults)
    {
        if (stepNodeTemplate == null) return;

        var ui = Instantiate(stepNodeTemplate, GetNodeParent());
        ui.gameObject.name = $"Node_{node.nodeId}";
        ui.gameObject.SetActive(true);
        ui.onClickInputConnector = OnClickInputConnector;
        ui.onClickOutputConnector = OnClickOutputConnector;
        ui.onBeginOutputConnectorDrag = BeginConnectorDrag;
        ui.onOutputConnectorDrag = UpdateConnectorDrag;
        ui.onCompleteConnectorDrag = CompleteConnectorDrag;
        ui.onCancelConnectorDrag = () => CancelConnectorDrag(clearStatus: true);
        ui.onClickDelete = OnClickDeleteNode;
        ui.onClickEmbeddedConditionDelete = OnClickDeleteNode;
        ui.onChanged = RefreshValidationStatus;
        ui.embeddedConditionTemplate = conditionNodeTemplate;

        int stepIndex = stepIndexMap.TryGetValue(node.nodeId, out var mapped) ? mapped : 0;
        ui.Bind(graph, node, stepIndex);
        ui.RefreshConditionSummary();
        ui.RefreshWarning();

        RegisterNode(
            node,
            ui.transform as RectTransform,
            ui.inputConnector != null ? ui.inputConnector.GetComponent<RectTransform>() : null,
            ui.outputConnector != null ? ui.outputConnector.GetComponent<RectTransform>() : null,
            defaults);
    }

    void InstantiateConditionNode(ScenarioNode node, Dictionary<string, Vector2> defaults)
    {
        if (conditionNodeTemplate == null) return;

        var ui = Instantiate(conditionNodeTemplate, GetNodeParent());
        ui.gameObject.name = $"Node_{node.nodeId}";
        ui.gameObject.SetActive(true);
        ui.onClickOutputConnector = OnClickOutputConnector;
        ui.onBeginOutputConnectorDrag = BeginConnectorDrag;
        ui.onOutputConnectorDrag = UpdateConnectorDrag;
        ui.onCompleteConnectorDrag = CompleteConnectorDrag;
        ui.onCancelConnectorDrag = () => CancelConnectorDrag(clearStatus: true);
        ui.onClickDelete = OnClickDeleteNode;
        ui.onChanged = RefreshValidationStatus;
        ui.Bind(graph, node);

        RegisterNode(
            node,
            ui.transform as RectTransform,
            null,
            ui.outputConnector != null ? ui.outputConnector.GetComponent<RectTransform>() : null,
            defaults);
    }

    void RegisterNode(
        ScenarioNode node,
        RectTransform root,
        RectTransform inputConnector,
        RectTransform outputConnector,
        Dictionary<string, Vector2> defaults)
    {
        if (node == null || root == null) return;

        if (nodePositions.TryGetValue(node.nodeId, out var saved))
        {
            root.anchoredPosition = saved;
        }
        else if (defaults.TryGetValue(node.nodeId, out var fallback))
        {
            root.anchoredPosition = fallback;
        }
        else
        {
            root.anchoredPosition = Vector2.zero;
        }

        root.anchoredPosition = ClampNodePosition(root, root.anchoredPosition);

        nodeUIs[node.nodeId] = new NodeUiBinding
        {
            nodeType = node.nodeType,
            root = root,
            inputConnector = inputConnector,
            outputConnector = outputConnector
        };

        ConfigureNodeDragCallbacks(node.nodeId, node.nodeType, root);
    }

    void ConfigureNodeDragCallbacks(string nodeId, ScenarioNodeType nodeType, RectTransform root)
    {
        if (string.IsNullOrWhiteSpace(nodeId) || root == null) return;

        void ConfigureDragHandler(NodeDragHandler drag, bool blockSelectableAtStart)
        {
            if (drag == null) return;
            drag.target = root;
            drag.blockWhenPointerStartsOnSelectable = blockSelectableAtStart;
            drag.onDrag = () =>
            {
                nodePositions[nodeId] = root.anchoredPosition;
            };
            drag.onEndDrag = () =>
            {
                nodePositions[nodeId] = root.anchoredPosition;
                if (nodeType != ScenarioNodeType.Condition) return;
                TryStoreConditionIntoNearbyStep(nodeId);
            };
        }

        if (nodeType == ScenarioNodeType.Step || nodeType == ScenarioNodeType.Condition)
        {
            var rootDrag = root.GetComponent<NodeDragHandler>();
            if (rootDrag == null) rootDrag = root.gameObject.AddComponent<NodeDragHandler>();
            ConfigureDragHandler(rootDrag, blockSelectableAtStart: true);

            var dragHandleRt = root.Find("DragHandle");
            var handleDrag = dragHandleRt != null ? dragHandleRt.GetComponent<NodeDragHandler>() : null;
            ConfigureDragHandler(handleDrag, blockSelectableAtStart: false);
            return;
        }

        // Start / End ノードはルート全体をドラッグ対象にする
        if (nodeType == ScenarioNodeType.Start || nodeType == ScenarioNodeType.End)
        {
            var rootDrag = root.GetComponent<NodeDragHandler>();
            if (rootDrag == null) rootDrag = root.gameObject.AddComponent<NodeDragHandler>();
            ConfigureDragHandler(rootDrag, blockSelectableAtStart: true);
            return;
        }

        var dragHandle = root.Find("DragHandle");
        if (dragHandle == null) return;
        var drag = dragHandle.GetComponent<NodeDragHandler>();
        ConfigureDragHandler(drag, blockSelectableAtStart: false);
    }

    void TryStoreConditionIntoNearbyStep(string conditionNodeId)
    {
        if (string.IsNullOrWhiteSpace(conditionNodeId)) return;
        if (!nodeUIs.TryGetValue(conditionNodeId, out var conditionUi) || conditionUi?.root == null) return;

        var stepNodeId = FindNearestStepNodeForCondition(conditionUi.root);
        if (string.IsNullOrWhiteSpace(stepNodeId)) return;

        if (!graph.TryBindConditionToStep(conditionNodeId, stepNodeId, out var reason))
        {
            if (statusText != null)
            {
                string friendly = ConnectReasonMessages.TryGetValue(reason, out var msg) ? msg : reason;
                statusText.text = $"手順を格納できません: {friendly}";
            }
            return;
        }

        if (statusText != null)
        {
            statusText.text = string.Empty;
        }
        RebuildAll();
    }

    string FindNearestStepNodeForCondition(RectTransform conditionRoot)
    {
        if (conditionRoot == null) return null;

        string nearestStepNodeId = null;
        float nearestDistance = float.MaxValue;
        Vector2 conditionCenter = conditionRoot.anchoredPosition;
        float conditionHalfWidth = conditionRoot.rect.width * 0.5f;
        float conditionHalfHeight = conditionRoot.rect.height * 0.5f;

        foreach (var pair in nodeUIs)
        {
            var nodeUi = pair.Value;
            if (nodeUi == null || nodeUi.nodeType != ScenarioNodeType.Step || nodeUi.root == null) continue;

            var stepRoot = nodeUi.root;
            Vector2 stepCenter = stepRoot.anchoredPosition;
            Vector2 delta = conditionCenter - stepCenter;

            float rangeX = (stepRoot.rect.width * 0.5f) + conditionHalfWidth + conditionEmbedSnapDistance;
            float rangeY = (stepRoot.rect.height * 0.5f) + conditionHalfHeight + conditionEmbedSnapDistance;
            if (Mathf.Abs(delta.x) > rangeX || Mathf.Abs(delta.y) > rangeY) continue;

            float distance = delta.sqrMagnitude;
            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearestStepNodeId = pair.Key;
            }
        }

        return nearestStepNodeId;
    }

    void ClampNodesToNodeArea()
    {
        var boundsRoot = GetNodeBoundsRoot();
        if (!isActiveAndEnabled || boundsRoot == null || nodeUIs.Count == 0) return;
        if (boundsRoot.rect.width <= 1f || boundsRoot.rect.height <= 1f) return;

        bool moved = false;
        foreach (var pair in nodeUIs)
        {
            if (pair.Value == null || pair.Value.root == null) continue;
            var root = pair.Value.root;
            var clamped = ClampNodePosition(root, root.anchoredPosition);
            if ((clamped - root.anchoredPosition).sqrMagnitude > 0.01f)
            {
                root.anchoredPosition = clamped;
                moved = true;
            }

            nodePositions[pair.Key] = root.anchoredPosition;
        }

        if (moved)
        {
            RefreshLines();
        }
    }

    Vector2 ClampNodePosition(RectTransform nodeRoot, Vector2 anchoredPosition)
    {
        var boundsRoot = GetNodeBoundsRoot();
        if (boundsRoot == null || nodeRoot == null) return anchoredPosition;

        var areaRect = boundsRoot.rect;
        var nodeRect = nodeRoot.rect;

        float minX = areaRect.xMin + (nodeRect.width * nodeRoot.pivot.x);
        float maxX = areaRect.xMax - (nodeRect.width * (1f - nodeRoot.pivot.x));
        float minY = areaRect.yMin + (nodeRect.height * nodeRoot.pivot.y);
        float maxY = areaRect.yMax - (nodeRect.height * (1f - nodeRoot.pivot.y));

        if (minX > maxX)
        {
            anchoredPosition.x = areaRect.center.x;
        }
        else
        {
            anchoredPosition.x = Mathf.Clamp(anchoredPosition.x, minX, maxX);
        }

        if (minY > maxY)
        {
            anchoredPosition.y = areaRect.center.y;
        }
        else
        {
            anchoredPosition.y = Mathf.Clamp(anchoredPosition.y, minY, maxY);
        }

        return anchoredPosition;
    }

    void OnClickOutputConnector(string fromNodeId)
    {
        linkingFromNodeId = fromNodeId;
        statusText.text = "入力コネクタをクリックして接続";
        Debug.Log($"[ScenarioGraphUI] Click connect start from={fromNodeId}");
    }

    void OnClickInputConnector(string toNodeId)
    {
        if (string.IsNullOrEmpty(linkingFromNodeId)) return;

        TryConnectNodes(linkingFromNodeId, toNodeId, "click");
        linkingFromNodeId = null;
    }

    void OnClickDeleteNode(string nodeId)
    {
        if (string.IsNullOrWhiteSpace(nodeId)) return;

        if (draggingFromNodeId == nodeId)
        {
            CancelConnectorDrag(clearStatus: true);
        }

        if (linkingFromNodeId == nodeId)
        {
            linkingFromNodeId = null;
        }

        graph.RemoveNode(nodeId);
        statusText.text = string.Empty;
        RebuildAll();
    }

    void OnClickConnectionPath(ConnectionLineGraphic line)
    {
        if (line == null) return;
        if (string.IsNullOrWhiteSpace(line.fromNodeId) || string.IsNullOrWhiteSpace(line.toNodeId)) return;

        graph.RemoveEdge(line.fromNodeId, line.toNodeId, line.edgeType);
        statusText.text = string.Empty;
        RebuildAll();
    }

    void TryConnectNodes(string fromNodeId, string toNodeId, string mode)
    {
        if (string.IsNullOrWhiteSpace(fromNodeId) || string.IsNullOrWhiteSpace(toNodeId)) return;

        if (!graph.TryAddEdge(fromNodeId, toNodeId, out var reason))
        {
            string friendly = ConnectReasonMessages.TryGetValue(reason, out var msg) ? msg : reason;
            statusText.text = $"接続できません: {friendly}";
            Debug.LogWarning($"[ScenarioGraphUI] Connect rejected mode={mode} from={fromNodeId} to={toNodeId} reason={reason}");
            return;
        }

        statusText.text = string.Empty;
        Debug.Log($"[ScenarioGraphUI] Connect success mode={mode} from={fromNodeId} to={toNodeId}");
        RebuildAll();
    }

    void BeginConnectorDrag(string fromNodeId, Vector2 screenPosition)
    {
        if (string.IsNullOrWhiteSpace(fromNodeId)) return;
        if (!nodeUIs.TryGetValue(fromNodeId, out var fromUi)) return;
        if (fromUi.outputConnector == null) return;

        draggingFromNodeId = fromNodeId;
        linkingFromNodeId = null;
        EnsureDragPreview(fromUi.outputConnector);
        UpdateDragPreviewPosition(screenPosition);
        statusText.text = "入力コネクタへドラッグしてドロップ";
    }

    void UpdateConnectorDrag(string fromNodeId, Vector2 screenPosition)
    {
        if (string.IsNullOrWhiteSpace(draggingFromNodeId)) return;
        if (draggingFromNodeId != fromNodeId) return;
        UpdateDragPreviewPosition(screenPosition);
    }

    void CompleteConnectorDrag(string fromNodeId, string toNodeId)
    {
        if (string.IsNullOrWhiteSpace(fromNodeId) || string.IsNullOrWhiteSpace(toNodeId))
        {
            CancelConnectorDrag(clearStatus: true);
            return;
        }

        TryConnectNodes(fromNodeId, toNodeId, "drag");
        CancelConnectorDrag(clearStatus: true);
    }

    void CancelConnectorDrag(bool clearStatus)
    {
        draggingFromNodeId = null;

        if (dragPreviewLine != null) Destroy(dragPreviewLine.gameObject);
        if (dragPreviewTarget != null) Destroy(dragPreviewTarget.gameObject);
        dragPreviewLine = null;
        dragPreviewTarget = null;

        if (clearStatus && statusText != null)
        {
            statusText.text = string.Empty;
        }
    }

    void EnsureDragPreview(RectTransform fromConnector)
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
            dragPreviewLine = Instantiate(lineTemplate, lineLayer);
            dragPreviewLine.gameObject.name = "DragPreviewLine";
            dragPreviewLine.gameObject.SetActive(true);
            ConfigureLineGraphic(dragPreviewLine, DragPreviewLineColor, 8f, raycastTarget: false);
        }

        dragPreviewLine.from = fromConnector;
        dragPreviewLine.to = dragPreviewTarget;
        dragPreviewLine.fromNodeId = null;
        dragPreviewLine.toNodeId = null;
        dragPreviewLine.raycastBlockers = null;
        dragPreviewLine.onClickLine = null;
    }

    void UpdateDragPreviewPosition(Vector2 screenPosition)
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

    void RefreshLines()
    {
        int removed = 0;
        foreach (var line in lines)
        {
            if (line == null) continue;
            Destroy(line.gameObject);
            removed++;
        }
        lines.Clear();

        int expectedEdges = graph.curriculum.edges.Count;
        int created = 0;
        var raycastBlockers = nodeUIs.Values
            .Where(v => v != null && v.root != null)
            .Select(v => v.root)
            .ToArray();
        foreach (var edge in graph.curriculum.edges)
        {
            if (!nodeUIs.TryGetValue(edge.fromNodeId, out var fromUi) ||
                !nodeUIs.TryGetValue(edge.toNodeId, out var toUi))
            {
                continue;
            }

            if (fromUi.outputConnector == null || toUi.inputConnector == null) continue;

            var line = Instantiate(lineTemplate, lineLayer);
            line.gameObject.SetActive(true);
            line.from = fromUi.outputConnector;
            line.to = toUi.inputConnector;
            line.fromNodeId = edge.fromNodeId;
            line.toNodeId = edge.toNodeId;
            line.edgeType = edge.edgeType;
            line.raycastBlockers = raycastBlockers;
            line.onClickLine = OnClickConnectionPath;
            ConfigureLineGraphic(line, ConnectionLineColor, 8f, raycastTarget: true);
            lines.Add(line);
            created++;
        }

        var layerSize = lineLayer != null ? lineLayer.rect.size : Vector2.zero;
        Debug.Log($"[ScenarioGraphUI] RefreshLines expectedEdges={expectedEdges} created={created} removed={removed} lineLayerSize={layerSize}");
    }

    void ConfigureLineGraphic(ConnectionLineGraphic line, Color color, float thickness, bool raycastTarget)
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

    void SaveScenarioExport()
    {
        graph.curriculum.projectName = string.IsNullOrWhiteSpace(projectNameInput.text)
            ? "VRCourseEditor"
            : projectNameInput.text.Trim();

        var validation = graph.ValidateGraph();
        if (!validation.CanExport)
        {
            statusText.text = BuildValidationMessage(validation);
            Debug.LogWarning("[ScenarioGraph] Export blocked: validation errors.");
            return;
        }

        ScenarioExport export;
        try
        {
            export = graph.BuildScenarioExport();
        }
        catch (System.Exception ex)
        {
            statusText.text = $"保存失敗: {ex.Message}";
            Debug.LogException(ex);
            return;
        }

        string exportDir = Path.Combine(Application.dataPath, "Exports");
        Directory.CreateDirectory(exportDir);

        string fileName = $"{export.projectName}-curriculum.json";
        string finalPath = Path.Combine(exportDir, fileName);
        string tempPath = finalPath + ".tmp";

        File.WriteAllText(tempPath, JsonUtility.ToJson(export, true));
        if (File.Exists(finalPath)) File.Replace(tempPath, finalPath, null);
        else File.Move(tempPath, finalPath);

        statusText.text = validation.warnings.Count > 0
            ? $"保存しました（警告 {validation.warnings.Count} 件）: Assets/Exports/{fileName}"
            : $"保存しました: Assets/Exports/{fileName}";
        Debug.Log("[ScenarioGraph] " + statusText.text);
        saveButton.interactable = true;
    }

    void RefreshValidationStatus()
    {
        if (graph == null || saveButton == null || statusText == null) return;

        var validation = graph.ValidateGraph();
        saveButton.interactable = validation.CanExport;

        if (!validation.CanExport)
        {
            statusText.text = BuildValidationMessage(validation);
            return;
        }

        if (validation.warnings.Count > 0)
        {
            var firstWarn = validation.warnings[0];
            string friendly = WarningMessages.TryGetValue(firstWarn.code, out var warnMsg) ? warnMsg : firstWarn.message;
            statusText.text = validation.warnings.Count == 1
                ? $"警告: {friendly}"
                : $"警告: {friendly}（他 {validation.warnings.Count - 1} 件）";
            return;
        }

        if (string.IsNullOrWhiteSpace(statusText.text))
        {
            statusText.text = "保存できます";
        }
    }

    void ApplyRoundedTheme()
    {
        Transform root = panelRoot != null ? panelRoot : transform;
        UiRoundedTheme.ApplyToHierarchy(root, cornerRadius);
    }

    /// <summary>
    /// 外部UI（例: オブジェクト詳細）で Condition ノード見た目を再利用するための参照。
    /// </summary>
    public ConditionNodeUI GetConditionNodeTemplateForExternalUse()
    {
        EnsureRuntimeTemplates();
        return conditionNodeTemplate;
    }

    /// <summary>
    /// 外部から Condition 編集が入ったときにグラフ表示を再同期する。
    /// </summary>
    public void RebuildFromExternalChange()
    {
        if (!isActiveAndEnabled) return;
        RebuildAll();
    }

    static string BuildValidationMessage(GraphValidationResult validation)
    {
        if (validation == null || validation.errors.Count == 0) return string.Empty;

        var firstError = validation.errors[0];
        string friendly = ErrorMessages.TryGetValue(firstError.code, out var msg) ? msg : firstError.message;

        return validation.errors.Count == 1
            ? $"保存できません: {friendly}"
            : $"保存できません: {friendly}（他 {validation.errors.Count - 1} 件のエラー）";
    }
}

