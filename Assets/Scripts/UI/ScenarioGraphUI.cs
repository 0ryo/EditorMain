using System.Collections.Generic;
using System.Collections;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ScenarioGraphUI : MonoBehaviour
{
    const string AddStepLabel = "+ 手順";
    const string AddConditionLabel = "+ 条件";
    const string PreviewLabel = "プレビュー";
    const string SaveLabel = "JSON出力";
    const string EmptyGraphGuide = "「+ 手順」からシナリオを作成してください";
    const float NodeLayoutGap = 64f;
    const int MaxLayoutColumns = 8;

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
        { "STEP_CONDITION_MAX", "このステップの条件数が設定上限に達しています" },
    };

    [Header("Services")]
    [SerializeField] CurriculumGraphService graph;

    [Header("Controls")]
    [SerializeField] RectTransform panelRoot;
    [SerializeField] TMP_InputField projectNameInput;
    [SerializeField] Button addStepButton;
    [SerializeField] Button addConditionButton;
    [SerializeField] Button previewButton;
    [SerializeField] Button saveButton;
    [SerializeField] TMP_Text statusText;
    [SerializeField] ScenarioValidationPanel validationPanel;
    [SerializeField] ScenarioPreviewPanel previewPanel;
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
    bool graphRebuildRequested;
    bool validationRefreshRequested;
    string lastValidationUiSignature;

    sealed class NodeIssueCounts
    {
        public int errors;
        public int warnings;
    }

    sealed class NodePositionCommand : IEditorCommand
    {
        readonly ScenarioGraphUI owner;
        readonly string nodeId;
        readonly Vector2 from;
        readonly Vector2 to;

        public string Label => "Move scenario node";

        public NodePositionCommand(ScenarioGraphUI owner, string nodeId, Vector2 from, Vector2 to)
        {
            this.owner = owner;
            this.nodeId = nodeId;
            this.from = from;
            this.to = to;
        }

        public bool Do() => owner != null && owner.SetNodePosition(nodeId, to);
        public bool Undo() => owner != null && owner.SetNodePosition(nodeId, from);
    }

    readonly Dictionary<string, ScenarioNodeViewBinding> nodeUIs = new Dictionary<string, ScenarioNodeViewBinding>();
    readonly Dictionary<string, Vector2> nodePositions = new Dictionary<string, Vector2>();
    readonly Dictionary<Graphic, Color> connectorBaseColors = new Dictionary<Graphic, Color>();
    readonly ScenarioGraphViewport graphViewport = new ScenarioGraphViewport();
    readonly HashSet<string> expandedStepDetailNodeIds = new HashSet<string>();
    readonly ScenarioConnectionLines connectionLines = new ScenarioConnectionLines();
    NodeAreaPanZoomController panZoomController;
    Outline validationFocusOutline;
    Coroutine validationFocusFlashCoroutine;
    Graphic validationFocusGraphic;
    Color validationFocusBaseColor;
    CommandStack validationCommandStack;

    public void PrepareAuthoringPrefab()
    {
        if (stepNodeTemplate == null) stepNodeTemplate = nodeTemplate;
        if (stepNodeTemplate == null) return;
        ScenarioNodeTemplateFactory.Ensure(stepNodeTemplate, stepNodeTemplate.transform.parent,
            ref startNodeTemplate, ref endNodeTemplate, ref conditionNodeTemplate);
        conditionNodeTemplate?.PrepareTemplateControls();
        previewPanel = ScenarioPreviewPanel.Ensure(nodeArea != null ? nodeArea : transform as RectTransform, previewPanel);
    }

    void Awake()
    {
        EnsureGraphService();
        ValidateAndBindReferences();
    }

    void OnEnable()
    {
        EnsureGraphService();
        graph.GraphChanged -= OnGraphChanged;
        graph.GraphChanged += OnGraphChanged;
        BindValidationChangeSources();
        BindValidationPanelEvents();
    }

    void OnDisable()
    {
        if (graph != null) graph.GraphChanged -= OnGraphChanged;
        UnbindValidationChangeSources();
        if (validationPanel != null) validationPanel.Hidden -= ClearValidationFocus;
        previewPanel?.Hide();
        ClearValidationFocus();
        ClearConnectionCandidates();
    }

    void Start()
    {
        cornerRadius = DesignTokens.CornerRadius;
        graph.EnsureGraphInitialized();

        if (projectNameInput != null)
        {
            projectNameInput.SetTextWithoutNotify(graph.curriculum.projectName);
        }

        RebuildAndResetView();
        BindValidationChangeSources();
        DesignTokenApplier.ApplyScenarioPanel(panelRoot != null ? panelRoot : transform as RectTransform);
    }

    void Update()
    {
        if (graph == null || !isActiveAndEnabled) return;
        if (graphRebuildRequested)
        {
            graphRebuildRequested = false;
            RebuildAll();
            return;
        }
        if (validationCommandStack == null)
        {
            BindValidationChangeSources();
        }
        if (!validationRefreshRequested) return;

        validationRefreshRequested = false;
        RefreshValidationStatus();
    }

    void LateUpdate()
    {
        RefreshMinimapViewport();
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

        ScenarioGraphViewport.EnsureMask(nodeArea);
        EnsureGraphContent();
        EnsurePanZoomController();
        validationPanel = ScenarioValidationPanel.Ensure(nodeArea, validationPanel);
        previewPanel = ScenarioPreviewPanel.Ensure(nodeArea, previewPanel);
        BindValidationPanelEvents();

        if (addConditionButton == null)
        {
            addConditionButton = CreateRuntimeConditionButton();
        }

        if (previewButton == null)
        {
            previewButton = CreateRuntimePreviewButton();
        }

        EnsureRuntimeTemplates();
        ApplyRoundedTheme();
        EnsureControlLabels();

        if (resizeHandle != null && panelRoot != null)
        {
            resizeHandle.targetPanel = panelRoot;
        }

        addStepButton.onClick.RemoveAllListeners();
        addStepButton.onClick.AddListener(() => AddNodeAtViewportCenter(ScenarioNodeType.Step));

        if (addConditionButton != null)
        {
            addConditionButton.onClick.RemoveAllListeners();
            addConditionButton.onClick.AddListener(() => AddNodeAtViewportCenter(ScenarioNodeType.Condition));
        }

        saveButton.onClick.RemoveAllListeners();
        saveButton.onClick.AddListener(SaveScenarioExport);

        if (previewButton != null)
        {
            previewButton.onClick.RemoveAllListeners();
            previewButton.onClick.AddListener(OpenScenarioPreview);
        }

        projectNameInput.onEndEdit.RemoveAllListeners();
        projectNameInput.onEndEdit.AddListener(_ =>
        {
            string projectName = string.IsNullOrWhiteSpace(projectNameInput.text)
                ? "VRCourseEditor"
                : projectNameInput.text.Trim();
            graph.ExecuteCommand("Rename project", () =>
            {
                graph.curriculum.projectName = projectName;
                return true;
            });
        });
    }

    void OnGraphChanged()
    {
        if (!isActiveAndEnabled) return;
        previewPanel?.Hide();
        if (projectNameInput != null)
        {
            projectNameInput.SetTextWithoutNotify(graph.curriculum.projectName);
        }
        graphRebuildRequested = true;
    }

    void AddNodeAtViewportCenter(ScenarioNodeType nodeType)
    {
        if (graph == null) return;

        ScenarioNode addedNode = null;
        string commandLabel = nodeType == ScenarioNodeType.Condition ? "Add condition" : "Add step";
        bool added = graph.ExecuteCommand(commandLabel, () =>
        {
            addedNode = nodeType == ScenarioNodeType.Condition
                ? graph.AddCondition()
                : graph.AddStep();
            return addedNode != null;
        });

        if (!added || addedNode == null || string.IsNullOrWhiteSpace(addedNode.nodeId)) return;
        nodePositions[addedNode.nodeId] = GetViewportCenterContentPosition();
    }

    Vector2 GetViewportCenterContentPosition()
    {
        if (graphContent == null) return Vector2.zero;
        float zoom = Mathf.Max(0.001f, graphContent.localScale.x);
        // This is the same content-space center represented by the blue minimap viewport indicator.
        return -graphContent.anchoredPosition / zoom;
    }

    void OpenScenarioPreview()
    {
        if (graph == null) return;

        var validation = graph.ValidateGraph();
        if (!validation.CanExport)
        {
            ShowValidationPanel(validation);
            validationPanel?.MinimizeForFocus();
            return;
        }

        previewPanel = ScenarioPreviewPanel.Ensure(nodeArea, previewPanel);
        previewPanel?.Show(graph);
    }

    void BindValidationChangeSources()
    {
        var nextStack = CommandService.I != null ? CommandService.I.Stack : null;
        if (nextStack == validationCommandStack) return;

        UnbindValidationChangeSources();
        validationCommandStack = nextStack;
        if (validationCommandStack != null)
        {
            validationCommandStack.HistoryChanged += RequestValidationRefresh;
        }
    }

    void UnbindValidationChangeSources()
    {
        if (validationCommandStack != null)
        {
            validationCommandStack.HistoryChanged -= RequestValidationRefresh;
        }
        validationCommandStack = null;
    }

    void RequestValidationRefresh()
    {
        if (isActiveAndEnabled) validationRefreshRequested = true;
    }

    void EnsureControlLabels()
    {
        SetButtonLabel(addStepButton, AddStepLabel);
        SetButtonLabel(addConditionButton, AddConditionLabel);
        SetButtonLabel(previewButton, PreviewLabel);
        SetButtonLabel(saveButton, SaveLabel);
    }

    static void SetButtonLabel(Button button, string labelText)
    {
        if (button == null) return;

        var tmpLabel = button.GetComponentInChildren<TMP_Text>(true);
        if (tmpLabel != null)
        {
            tmpLabel.text = labelText;
            tmpLabel.fontSize = DesignTokens.FontSizeBody;
            tmpLabel.alignment = TextAlignmentOptions.Center;
        }

        var legacyLabel = button.GetComponentInChildren<Text>(true);
        if (legacyLabel != null)
        {
            legacyLabel.text = labelText;
            legacyLabel.fontSize = DesignTokens.FontSizeBody;
            legacyLabel.alignment = TextAnchor.MiddleCenter;
        }
    }

    Button CreateRuntimeConditionButton()
    {
        if (addStepButton == null) return null;

        var cloned = Instantiate(addStepButton, addStepButton.transform.parent);
        cloned.gameObject.name = "Button_AddCondition_Runtime";
        cloned.transform.SetSiblingIndex(addStepButton.transform.GetSiblingIndex() + 1);

        SetButtonLabel(cloned, AddConditionLabel);

        return cloned;
    }

    Button CreateRuntimePreviewButton()
    {
        if (saveButton == null) return null;

        var cloned = Instantiate(saveButton, saveButton.transform.parent);
        cloned.gameObject.name = "Button_Preview_Runtime";
        cloned.transform.SetSiblingIndex(saveButton.transform.GetSiblingIndex());
        SetButtonLabel(cloned, PreviewLabel);
        return cloned;
    }

    void EnsureRuntimeTemplates()
    {
        ScenarioNodeTemplateFactory.Ensure(
            stepNodeTemplate, GetNodeParent(), ref startNodeTemplate, ref endNodeTemplate, ref conditionNodeTemplate);
    }

    void EnsureGraphContent()
    {
        if (nodeArea == null) return;
        ScenarioGraphViewport.EnsureContent(nodeArea, ref graphContent);

        ReparentToGraphContent(lineLayer);
        if (stepNodeTemplate != null) ReparentToGraphContent(stepNodeTemplate.transform as RectTransform);
        if (conditionNodeTemplate != null) ReparentToGraphContent(conditionNodeTemplate.transform as RectTransform);
        if (startNodeTemplate != null) ReparentToGraphContent(startNodeTemplate.transform as RectTransform);
        if (endNodeTemplate != null) ReparentToGraphContent(endNodeTemplate.transform as RectTransform);
    }

    void ReparentToGraphContent(RectTransform child)
    {
        ScenarioGraphViewport.ReparentToContent(graphContent, child);
    }

    void EnsurePanZoomController()
    {
        if (nodeArea == null || graphContent == null) return;

        ScenarioGraphViewport.ConfigurePanZoom(nodeArea, graphContent, ref panZoomController);
        EnsureViewportTools();
    }

    Transform GetNodeParent()
    {
        return graphContent != null ? graphContent : nodeArea;
    }

    RectTransform GetNodeBoundsRoot()
    {
        return graphContent != null ? graphContent : nodeArea;
    }

    void RebuildAll()
    {
        graphRebuildRequested = false;
        validationRefreshRequested = false;
        CancelConnectorDrag(clearStatus: false);

        foreach (var pair in nodeUIs)
        {
            if (pair.Value == null || pair.Value.root == null) continue;
            nodePositions[pair.Key] = pair.Value.root.anchoredPosition;
        }

        foreach (var staleNodeId in nodePositions.Keys.Where(nodeId => graph.FindNode(nodeId) == null).ToArray())
        {
            nodePositions.Remove(staleNodeId);
            expandedStepDetailNodeIds.Remove(staleNodeId);
        }

        var nodeParent = GetNodeParent();
        if (nodeParent == null)
        {
            Debug.LogError("[ScenarioGraphUI] Node parent is missing.");
            return;
        }

        var nodeViews = new ScenarioNodeViewFactory(
            graph, nodeParent, stepNodeTemplate, conditionNodeTemplate, startNodeTemplate, endNodeTemplate,
            expandedStepDetailNodeIds, new ScenarioNodeViewFactory.Callbacks
            {
                onClickInputConnector = OnClickInputConnector,
                onClickOutputConnector = OnClickOutputConnector,
                onBeginOutputConnectorDrag = BeginConnectorDrag,
                onOutputConnectorDrag = UpdateConnectorDrag,
                onCompleteConnectorDrag = CompleteConnectorDrag,
                onCancelConnectorDrag = () => CancelConnectorDrag(clearStatus: true),
                onClickDelete = OnClickDeleteNode,
                onClickEmbeddedConditionDelete = OnClickExtractEmbeddedCondition,
                onDetailsExpandedChanged = OnStepDetailsExpandedChanged,
                onChanged = RefreshValidationStatus,
                registerNode = RegisterNode
            });
        nodeViews.ClearNodes(lineLayer);

        nodeUIs.Clear();
        var defaultPositions = BuildDefaultNodePositions();
        var stepIndexMap = graph.BuildStepIndexMap();

        nodeViews.CreateNodes(defaultPositions, stepIndexMap);

        if (lineLayer != null)
        {
            lineLayer.SetAsFirstSibling();
            var lineLayerImage = lineLayer.GetComponent<Image>();
            if (lineLayerImage != null)
            {
                lineLayerImage.color = Color.clear;
                lineLayerImage.raycastTarget = false;
            }
        }

        ApplyRoundedTheme();
        DesignTokenApplier.ApplyNodeColors(GetNodeParent() as Transform);
        RefreshLines();
        RebuildMinimapIndicators();
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

    Dictionary<string, Vector2> BuildDefaultNodePositions()
    {
        var flowNodes = new List<ScenarioNode>();
        var start = graph.GetStartNode();
        if (start != null) flowNodes.Add(start);

        var orderedSteps = graph.GetDisplayOrderedSteps();
        for (int i = 0; i < orderedSteps.Count; i++)
        {
            if (orderedSteps[i] == null || string.IsNullOrWhiteSpace(orderedSteps[i].nodeId)) continue;
            flowNodes.Add(orderedSteps[i]);
        }

        var end = graph.GetEndNode();
        if (end != null) flowNodes.Add(end);

        var unboundConditions = graph.GetNodes(ScenarioNodeType.Condition)
            .Where(condition => condition != null &&
                                !string.IsNullOrWhiteSpace(condition.nodeId) &&
                                !graph.IsConditionBoundToStep(condition.nodeId))
            .OrderBy(condition => condition.nodeId)
            .ToList();

        Vector2 flowSize = ScenarioGraphLayout.GetLargestTemplateSize(
            new Component[] { startNodeTemplate, stepNodeTemplate, endNodeTemplate },
            new Vector2(390f, 220f));
        Vector2 conditionSize = ScenarioGraphLayout.GetLargestTemplateSize(
            new Component[] { conditionNodeTemplate },
            new Vector2(390f, 180f));
        return ScenarioGraphLayout.BuildDefaultNodePositions(
            flowNodes,
            unboundConditions,
            flowSize,
            conditionSize,
            NodeLayoutGap,
            MaxLayoutColumns);
    }

    void OnStepDetailsExpandedChanged(string nodeId, bool expanded)
    {
        if (string.IsNullOrWhiteSpace(nodeId)) return;
        if (expanded) expandedStepDetailNodeIds.Add(nodeId);
        else expandedStepDetailNodeIds.Remove(nodeId);

        RefreshLines();
        RefreshMinimapNodes();
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
            root.anchoredPosition = FindAvailableNodePosition(node.nodeId, root, fallback);
        }
        else
        {
            root.anchoredPosition = Vector2.zero;
        }

        root.anchoredPosition = ClampNodePosition(root, root.anchoredPosition);
        nodePositions[node.nodeId] = root.anchoredPosition;

        nodeUIs[node.nodeId] = new ScenarioNodeViewBinding
        {
            nodeType = node.nodeType,
            root = root,
            inputConnector = inputConnector,
            outputConnector = outputConnector
        };

        ConfigureNodeDragCallbacks(node.nodeId, node.nodeType, root);
    }

    Vector2 FindAvailableNodePosition(string nodeId, RectTransform root, Vector2 preferred)
    {
        float verticalStep = Mathf.Max(1f, root.rect.height, root.sizeDelta.y) + NodeLayoutGap;
        for (int rowOffset = 0; rowOffset < 24; rowOffset++)
        {
            var candidate = ClampNodePosition(root, preferred + (Vector2.down * verticalStep * rowOffset));
            if (!IsNodePositionOccupied(nodeId, root, candidate)) return candidate;
        }

        return preferred;
    }

    bool IsNodePositionOccupied(string nodeId, RectTransform root, Vector2 candidate)
    {
        float width = Mathf.Max(1f, root.rect.width, root.sizeDelta.x);
        float height = Mathf.Max(1f, root.rect.height, root.sizeDelta.y);
        foreach (var pair in nodePositions)
        {
            if (pair.Key == nodeId) continue;
            if (Mathf.Abs(pair.Value.x - candidate.x) >= width + NodeLayoutGap) continue;
            if (Mathf.Abs(pair.Value.y - candidate.y) >= height + NodeLayoutGap) continue;
            return true;
        }

        return false;
    }

    void ConfigureNodeDragCallbacks(string nodeId, ScenarioNodeType nodeType, RectTransform root)
    {
        if (string.IsNullOrWhiteSpace(nodeId) || root == null) return;

        void ConfigureDragHandler(NodeDragHandler drag, bool blockSelectableAtStart)
        {
            if (drag == null) return;
            Vector2 dragStart = root.anchoredPosition;
            drag.target = root;
            drag.blockWhenPointerStartsOnSelectable = blockSelectableAtStart;
            drag.onBeginDrag = () =>
            {
                dragStart = root.anchoredPosition;
            };
            drag.onDrag = () =>
            {
                nodePositions[nodeId] = root.anchoredPosition;
                RefreshMinimapNodes();
            };
            drag.onEndDrag = () =>
            {
                Vector2 dragEnd = root.anchoredPosition;
                nodePositions[nodeId] = dragEnd;
                if ((dragEnd - dragStart).sqrMagnitude > 0.01f &&
                    CommandService.I != null && CommandService.I.Stack != null)
                {
                    CommandService.I.Stack.RecordApplied(
                        new NodePositionCommand(this, nodeId, dragStart, dragEnd));
                }
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

        string reason = null;
        bool bound = graph.ExecuteCommand("Bind condition", () =>
            graph.TryBindConditionToStep(conditionNodeId, stepNodeId, out reason));
        if (!bound)
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
    }

    bool SetNodePosition(string nodeId, Vector2 position)
    {
        if (string.IsNullOrWhiteSpace(nodeId) || graph == null || graph.FindNode(nodeId) == null) return false;

        nodePositions[nodeId] = position;
        if (nodeUIs.TryGetValue(nodeId, out var binding) && binding?.root != null)
        {
            binding.root.anchoredPosition = ClampNodePosition(binding.root, position);
            nodePositions[nodeId] = binding.root.anchoredPosition;
            RefreshLines();
            RefreshMinimapNodes();
        }

        return true;
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
            RefreshMinimapNodes();
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
        ShowConnectionCandidates(fromNodeId);
        statusText.text = "入力コネクタをクリックして接続";
        Debug.Log($"[ScenarioGraphUI] Click connect start from={fromNodeId}");
    }

    void OnClickInputConnector(string toNodeId)
    {
        if (string.IsNullOrEmpty(linkingFromNodeId)) return;

        TryConnectNodes(linkingFromNodeId, toNodeId, "click");
        linkingFromNodeId = null;
        ClearConnectionCandidates();
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
            ClearConnectionCandidates();
        }

        graph.ExecuteCommand("Delete scenario node", () =>
        {
            if (graph.FindNode(nodeId) == null) return false;
            graph.RemoveNode(nodeId);
            return graph.FindNode(nodeId) == null;
        });
        statusText.text = string.Empty;
    }

    void OnClickExtractEmbeddedCondition(string nodeId)
    {
        if (string.IsNullOrWhiteSpace(nodeId)) return;

        bool extracted = graph.ExecuteCommand("Unbind condition", () =>
            graph.TryUnbindConditionFromStep(nodeId));
        if (!extracted) return;

        var defaults = BuildDefaultNodePositions();
        if (defaults.TryGetValue(nodeId, out var extractedPosition))
        {
            nodePositions[nodeId] = extractedPosition;
        }

        statusText.text = string.Empty;
    }

    void OnClickConnectionPath(ConnectionLineGraphic line)
    {
        if (line == null) return;
        if (string.IsNullOrWhiteSpace(line.fromNodeId) || string.IsNullOrWhiteSpace(line.toNodeId)) return;

        graph.ExecuteCommand("Delete scenario connection", () =>
        {
            bool exists = graph.curriculum.edges.Any(edge =>
                edge.fromNodeId == line.fromNodeId &&
                edge.toNodeId == line.toNodeId &&
                edge.edgeType == line.edgeType);
            if (!exists) return false;

            graph.RemoveEdge(line.fromNodeId, line.toNodeId, line.edgeType);
            return true;
        });
        statusText.text = string.Empty;
    }

    bool TryConnectNodes(string fromNodeId, string toNodeId, string mode)
    {
        if (string.IsNullOrWhiteSpace(fromNodeId) || string.IsNullOrWhiteSpace(toNodeId)) return false;

        string reason = null;
        if (!graph.CanAddEdge(fromNodeId, toNodeId, out reason))
        {
            string friendly = ConnectReasonMessages.TryGetValue(reason, out var msg) ? msg : reason;
            statusText.text = $"接続できません: {friendly}";
            Debug.LogWarning($"[ScenarioGraphUI] Connect rejected before command mode={mode} from={fromNodeId} to={toNodeId} reason={reason}");
            return false;
        }

        bool connected = graph.ExecuteCommand("Connect scenario nodes", () =>
            graph.TryAddEdge(fromNodeId, toNodeId, out reason));
        if (!connected)
        {
            string friendly = ConnectReasonMessages.TryGetValue(reason, out var msg) ? msg : reason;
            statusText.text = $"接続できません: {friendly}";
            Debug.LogWarning($"[ScenarioGraphUI] Connect rejected mode={mode} from={fromNodeId} to={toNodeId} reason={reason}");
            return false;
        }

        statusText.text = string.Empty;
        Debug.Log($"[ScenarioGraphUI] Connect success mode={mode} from={fromNodeId} to={toNodeId}");
        return true;
    }

    void BeginConnectorDrag(string fromNodeId, Vector2 screenPosition)
    {
        if (string.IsNullOrWhiteSpace(fromNodeId)) return;
        if (!nodeUIs.TryGetValue(fromNodeId, out var fromUi)) return;
        if (fromUi.outputConnector == null) return;

        draggingFromNodeId = fromNodeId;
        linkingFromNodeId = null;
        ShowConnectionCandidates(fromNodeId);
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

        bool connected = TryConnectNodes(fromNodeId, toNodeId, "drag");
        CancelConnectorDrag(clearStatus: connected);
    }

    void CancelConnectorDrag(bool clearStatus)
    {
        draggingFromNodeId = null;
        ClearConnectionCandidates();

        connectionLines.ClearDragPreview();

        if (clearStatus && statusText != null)
        {
            statusText.text = string.Empty;
        }
    }

    void EnsureDragPreview(RectTransform fromConnector)
    {
        connectionLines.EnsureDragPreview(lineTemplate, lineLayer, fromConnector);
    }

    void UpdateDragPreviewPosition(Vector2 screenPosition)
    {
        connectionLines.UpdateDragPreviewPosition(lineLayer, screenPosition);
    }

    void RefreshLines()
    {
        connectionLines.Clear();

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

            connectionLines.Add(
                lineTemplate, lineLayer, edge, fromUi.outputConnector, toUi.inputConnector,
                raycastBlockers, OnClickConnectionPath);
        }
    }

    void ShowConnectionCandidates(string fromNodeId)
    {
        ClearConnectionCandidates();
        if (graph == null || string.IsNullOrWhiteSpace(fromNodeId)) return;

        foreach (var pair in nodeUIs)
        {
            var connector = pair.Value?.inputConnector;
            if (connector == null || !connector.gameObject.activeInHierarchy) continue;

            var graphic = connector.GetComponent<Graphic>();
            if (graphic == null) continue;
            connectorBaseColors[graphic] = graphic.color;
            bool canConnect = graph.CanAddEdge(fromNodeId, pair.Key, out _);
            graphic.color = canConnect ? DesignTokens.Success : DesignTokens.Error;
        }
    }

    void ClearConnectionCandidates()
    {
        foreach (var pair in connectorBaseColors)
        {
            if (pair.Key != null) pair.Key.color = pair.Value;
        }
        connectorBaseColors.Clear();
    }

    void RebuildAndResetView()
    {
        RebuildAll();
        panZoomController?.ResetView();
    }

    void EnsureViewportTools()
    {
        graphViewport.EnsureTools(nodeArea, FitGraphToContent, ResetGraphZoom, AutoLayoutNodes);
    }

    void FitGraphToContent()
    {
        panZoomController?.FitContent(nodeUIs.Values.Where(binding => binding?.root != null).Select(binding => binding.root));
    }

    void ResetGraphZoom()
    {
        panZoomController?.ResetView();
    }

    void AutoLayoutNodes()
    {
        var defaults = BuildDefaultNodePositions();
        var commands = new List<IEditorCommand>();
        foreach (var pair in nodeUIs)
        {
            if (pair.Value?.root == null || !defaults.TryGetValue(pair.Key, out var targetPosition)) continue;
            targetPosition = ClampNodePosition(pair.Value.root, targetPosition);
            Vector2 currentPosition = pair.Value.root.anchoredPosition;
            if ((targetPosition - currentPosition).sqrMagnitude <= 0.01f) continue;
            commands.Add(new NodePositionCommand(this, pair.Key, currentPosition, targetPosition));
        }

        if (commands.Count > 0)
        {
            if (CommandService.I != null && CommandService.I.Stack != null)
            {
                CommandService.I.Stack.ExecuteTransaction("Auto layout scenario nodes", commands.ToArray());
            }
            else
            {
                foreach (var command in commands) command.Do();
            }
        }

        RefreshMinimapNodes();
        FitGraphToContent();
        if (statusText != null) statusText.text = "ノードを自動整列しました";
    }

    void RebuildMinimapIndicators()
    {
        graphViewport.RebuildMinimapIndicators(nodeUIs, nodeArea, graphContent);
    }

    void RefreshMinimapNodes()
    {
        graphViewport.RefreshMinimapNodes(nodeUIs, nodeArea, graphContent);
    }

    void RefreshMinimapViewport()
    {
        graphViewport.RefreshMinimapViewport(nodeArea, graphContent);
    }

    void SaveScenarioExport()
    {
        string projectName = string.IsNullOrWhiteSpace(projectNameInput.text)
            ? "VRCourseEditor"
            : projectNameInput.text.Trim();
        if (!string.Equals(graph.curriculum.projectName, projectName, System.StringComparison.Ordinal))
        {
            graph.ExecuteCommand("Rename project", () =>
            {
                graph.curriculum.projectName = projectName;
                return true;
            });
        }

        var validation = graph.ValidateGraph();
        if (!validation.CanExport)
        {
            statusText.text = ScenarioValidationText.BuildExportBlockedMessage(validation);
            ShowValidationPanel(validation);
            Debug.LogWarning("[ScenarioGraph] Export blocked: validation errors.");
            return;
        }

        validationPanel?.Hide();

        ScenarioExport export;
        try
        {
            export = graph.BuildScenarioExport();
        }
        catch (System.Exception ex)
        {
            statusText.text = $"JSON出力失敗: {ex.Message}";
            Debug.LogException(ex);
            return;
        }

        string safeProjectName = ExportFileNameUtility.SanitizeProjectName(export.projectName, "VRCourseEditor");
        string fileName = $"{safeProjectName}-curriculum.json";
        string finalPath = RuntimeExportPathUtility.BuildPath(fileName);
        try
        {
            ScenarioModelBundle.Prepare(export, finalPath);
            ExportFileWriter.WriteAllTextWithBackup(finalPath, JsonUtility.ToJson(export, true));
        }
        catch (System.Exception ex)
        {
            statusText.text = $"JSON出力失敗: {ex.Message}";
            Debug.LogException(ex);
            return;
        }

        statusText.text = validation.warnings.Count > 0
            ? $"JSON出力しました（警告 {validation.warnings.Count} 件）: Exports/{fileName}"
            : $"JSON出力しました: Exports/{fileName}";
        if (export.models.Any(model => model.requiresPreinstalledPrefab && model.typeId.StartsWith("Imported/")))
            statusText.text += " / FBX等のモデルはVR側で事前登録が必要です。モデル同梱にはGLB/glTFを使用してください。";
        Debug.Log("[ScenarioGraph] " + statusText.text);
        validationPanel?.Hide();
        saveButton.interactable = true;
    }

    void RefreshValidationStatus()
    {
        if (graph == null || saveButton == null || statusText == null) return;

        if (graph.GetNodes(ScenarioNodeType.Step).Count == 0)
        {
            saveButton.interactable = false;
            if (previewButton != null) previewButton.interactable = false;
            statusText.text = EmptyGraphGuide;
            ClearNodeValidationIndicators();
            lastValidationUiSignature = null;
            validationPanel?.Hide();
            return;
        }

        if (previewButton != null) previewButton.interactable = true;

        var validation = graph.ValidateGraph();
        saveButton.interactable = validation.CanExport;
        RefreshNodeValidationIndicators(validation);

        if (!validation.CanExport)
        {
            statusText.text = ScenarioValidationText.BuildStatusMessage(validation);
            bool wasVisible = validationPanel != null && validationPanel.IsVisible;
            string validationSignature = ScenarioValidationText.BuildUiSignature(validation);
            if (!wasVisible || !string.Equals(validationSignature, lastValidationUiSignature, System.StringComparison.Ordinal))
            {
                ShowValidationPanel(validation);
                lastValidationUiSignature = validationSignature;
            }
            if (!wasVisible && validationPanel != null)
            {
                validationPanel.MinimizeForFocus();
            }
            return;
        }

        if (validationPanel != null && validationPanel.IsVisible)
        {
            validationPanel.Hide();
        }
        lastValidationUiSignature = null;

        statusText.text = ScenarioValidationText.BuildStatusMessage(validation);
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

    void ShowValidationPanel(GraphValidationResult validation)
    {
        if (validationPanel == null)
        {
            validationPanel = ScenarioValidationPanel.Ensure(nodeArea);
        }

        BindValidationPanelEvents();
        validationPanel?.Show(validation, ScenarioValidationText.GetFriendlyMessage, FocusNode);
    }

    void FocusNode(string nodeId)
    {
        if (string.IsNullOrWhiteSpace(nodeId)) return;
        if (!TryResolveValidationBinding(nodeId, out var binding)) return;

        HighlightValidationFocus(binding.root);
        panZoomController?.FocusContentPoint(binding.root.anchoredPosition);
    }

    bool TryResolveValidationBinding(string nodeId, out ScenarioNodeViewBinding binding)
    {
        binding = null;
        if (string.IsNullOrWhiteSpace(nodeId)) return false;
        if (nodeUIs.TryGetValue(nodeId, out binding) && binding != null && binding.root != null)
        {
            return true;
        }

        var node = graph != null ? graph.FindNode(nodeId) : null;
        if (node == null || node.nodeType != ScenarioNodeType.Condition) return false;

        var bindEdge = graph.curriculum.edges.FirstOrDefault(edge =>
            edge.edgeType == ScenarioEdgeType.ConditionBind && edge.fromNodeId == nodeId);
        return bindEdge != null &&
            nodeUIs.TryGetValue(bindEdge.toNodeId, out binding) &&
            binding != null &&
            binding.root != null;
    }

    void RefreshNodeValidationIndicators(GraphValidationResult validation)
    {
        var countsByRoot = new Dictionary<RectTransform, NodeIssueCounts>();
        if (validation != null)
        {
            foreach (var issue in validation.errors) AddNodeIssueCount(countsByRoot, issue, true);
            foreach (var issue in validation.warnings) AddNodeIssueCount(countsByRoot, issue, false);
        }

        var visitedRoots = new HashSet<RectTransform>();
        foreach (var binding in nodeUIs.Values)
        {
            var root = binding?.root;
            if (root == null || !visitedRoots.Add(root)) continue;
            countsByRoot.TryGetValue(root, out var counts);
            var indicator = root.GetComponent<ScenarioNodeValidationIndicator>();
            if (counts == null)
            {
                indicator?.SetCounts(0, 0);
                continue;
            }

            if (indicator == null) indicator = root.gameObject.AddComponent<ScenarioNodeValidationIndicator>();
            indicator.SetCounts(counts.errors, counts.warnings);
        }
    }

    void AddNodeIssueCount(
        Dictionary<RectTransform, NodeIssueCounts> countsByRoot,
        GraphValidationIssue issue,
        bool isError)
    {
        if (issue == null || !TryResolveValidationBinding(issue.nodeId, out var binding)) return;
        if (!countsByRoot.TryGetValue(binding.root, out var counts))
        {
            counts = new NodeIssueCounts();
            countsByRoot[binding.root] = counts;
        }
        if (isError) counts.errors++;
        else counts.warnings++;
    }

    void ClearNodeValidationIndicators()
    {
        var visitedRoots = new HashSet<RectTransform>();
        foreach (var binding in nodeUIs.Values)
        {
            var root = binding?.root;
            if (root == null || !visitedRoots.Add(root)) continue;
            root.GetComponent<ScenarioNodeValidationIndicator>()?.SetCounts(0, 0);
        }
    }

    void BindValidationPanelEvents()
    {
        if (validationPanel == null) return;
        validationPanel.Hidden -= ClearValidationFocus;
        validationPanel.Hidden += ClearValidationFocus;
    }

    void HighlightValidationFocus(RectTransform nodeRoot)
    {
        ClearValidationFocus();
        if (nodeRoot == null || nodeRoot.GetComponent<Graphic>() == null) return;

        validationFocusOutline = nodeRoot.gameObject.AddComponent<Outline>();
        validationFocusOutline.effectColor = DesignTokens.Accent;
        validationFocusOutline.effectDistance = new Vector2(4f, -4f);
        validationFocusOutline.useGraphicAlpha = false;
        validationFocusGraphic = nodeRoot.GetComponent<Graphic>();
        if (validationFocusGraphic != null)
        {
            validationFocusBaseColor = validationFocusGraphic.color;
            validationFocusFlashCoroutine = StartCoroutine(FlashValidationFocus(validationFocusGraphic));
        }
    }

    IEnumerator FlashValidationFocus(Graphic graphic)
    {
        Color flashColor = Color.Lerp(validationFocusBaseColor, DesignTokens.Error, 0.22f);
        for (int i = 0; i < 3; i++)
        {
            if (graphic == null) yield break;
            graphic.color = flashColor;
            yield return new WaitForSecondsRealtime(0.14f);
            if (graphic == null) yield break;
            graphic.color = validationFocusBaseColor;
            yield return new WaitForSecondsRealtime(0.12f);
        }
        validationFocusFlashCoroutine = null;
    }

    void ClearValidationFocus()
    {
        if (validationFocusFlashCoroutine != null)
        {
            StopCoroutine(validationFocusFlashCoroutine);
            validationFocusFlashCoroutine = null;
        }
        if (validationFocusGraphic != null)
        {
            validationFocusGraphic.color = validationFocusBaseColor;
            validationFocusGraphic = null;
        }
        if (validationFocusOutline != null)
        {
            Destroy(validationFocusOutline);
            validationFocusOutline = null;
        }
    }

}

