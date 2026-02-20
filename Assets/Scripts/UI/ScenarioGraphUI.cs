using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

public class ScenarioGraphUI : MonoBehaviour
{
    static readonly Color ConnectionLineColor = new Color(1f, 0.92f, 0.2f, 1f);
    static readonly Color DragPreviewLineColor = new Color(1f, 0.92f, 0.2f, 0.9f);

    [Header("Services")]
    [SerializeField] CurriculumGraphService graph;

    [Header("Controls")]
    [SerializeField] RectTransform panelRoot;
    [SerializeField] InputField projectNameInput;
    [SerializeField] Button addStepButton;
    [SerializeField] Button saveButton;
    [SerializeField] Text statusText;
    [SerializeField] RectTransform nodeArea;
    [SerializeField] RectTransform lineLayer;
    [SerializeField] StepNodeUI nodeTemplate;
    [SerializeField] ConnectionLineGraphic lineTemplate;
    [SerializeField] PanelVerticalResizeHandle resizeHandle;

    string linkingFromStepId;
    string draggingFromStepId;
    ConnectionLineGraphic dragPreviewLine;
    RectTransform dragPreviewTarget;

    readonly Dictionary<string, StepNodeUI> nodeUIs = new Dictionary<string, StepNodeUI>();
    readonly Dictionary<string, Vector2> nodePositions = new Dictionary<string, Vector2>();
    readonly List<ConnectionLineGraphic> lines = new List<ConnectionLineGraphic>();

    void Awake()
    {
        EnsureGraphService();
        ValidateAndBindReferences();
    }

    void Start()
    {
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

    void ValidateAndBindReferences()
    {
        if (projectNameInput == null || addStepButton == null || saveButton == null || statusText == null ||
            nodeArea == null || lineLayer == null || nodeTemplate == null || lineTemplate == null)
        {
            Debug.LogError("[ScenarioGraphUI] UI references are not assigned on prefab.");
            enabled = false;
            return;
        }

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

        saveButton.onClick.RemoveAllListeners();
        saveButton.onClick.AddListener(SaveCurriculum);
    }

    void RebuildAll()
    {
        graph.RepairBrokenReferences();
        CancelConnectorDrag(clearStatus: false);

        foreach (var pair in nodeUIs)
        {
            if (pair.Value == null) continue;
            nodePositions[pair.Key] = pair.Value.GetComponent<RectTransform>().anchoredPosition;
        }

        foreach (Transform child in nodeArea)
        {
            if (child == nodeTemplate.transform || child == lineLayer) continue;
            Destroy(child.gameObject);
        }

        nodeUIs.Clear();

        foreach (var step in graph.curriculum.steps)
        {
            var ui = Instantiate(nodeTemplate, nodeArea);
            ui.gameObject.name = $"StepNode_{step.id}";
            ui.gameObject.SetActive(true);

            var rt = ui.GetComponent<RectTransform>();
            rt.anchoredPosition = nodePositions.TryGetValue(step.id, out var saved) ? saved : Vector2.zero;

            ui.onClickOutputConnector = OnClickOutputConnector;
            ui.onClickInputConnector = OnClickInputConnector;
            ui.onBeginOutputConnectorDrag = BeginConnectorDrag;
            ui.onOutputConnectorDrag = UpdateConnectorDrag;
            ui.onCompleteConnectorDrag = CompleteConnectorDrag;
            ui.onCancelConnectorDrag = () => CancelConnectorDrag(clearStatus: true);
            ui.onChanged = RefreshLines;
            ui.Bind(graph, step);
            nodeUIs[step.id] = ui;
        }

        // Keep lines visible above node backgrounds.
        if (lineLayer != null)
        {
            lineLayer.SetAsLastSibling();
        }

        RefreshLines();
        if (!string.IsNullOrEmpty(linkingFromStepId))
        {
            statusText.text = "入力コネクタをクリックして接続";
        }
        else if (!string.IsNullOrEmpty(draggingFromStepId))
        {
            statusText.text = "入力コネクタへドラッグしてドロップ";
        }
        else
        {
            statusText.text = "";
        }
    }

    void OnClickOutputConnector(string fromStepId)
    {
        linkingFromStepId = fromStepId;
        Debug.Log($"[ScenarioGraphUI] Click connect start from={fromStepId}");
        statusText.text = "入力コネクタをクリックして接続";
    }

    void OnClickInputConnector(string toStepId)
    {
        if (string.IsNullOrEmpty(linkingFromStepId)) return;

        Debug.Log($"[ScenarioGraphUI] Click connect complete from={linkingFromStepId} to={toStepId}");
        graph.AddEdge(linkingFromStepId, toStepId);
        linkingFromStepId = null;
        statusText.text = "";
        RefreshLines();
    }

    void BeginConnectorDrag(string fromStepId, Vector2 screenPosition)
    {
        if (string.IsNullOrEmpty(fromStepId)) return;
        if (!nodeUIs.TryGetValue(fromStepId, out var fromUi)) return;

        draggingFromStepId = fromStepId;
        linkingFromStepId = null;
        Debug.Log($"[ScenarioGraphUI] Drag connect start from={fromStepId} pos={screenPosition}");
        EnsureDragPreview(fromUi);
        UpdateDragPreviewPosition(screenPosition);
        statusText.text = "入力コネクタへドラッグしてドロップ";
    }

    void UpdateConnectorDrag(string fromStepId, Vector2 screenPosition)
    {
        if (string.IsNullOrEmpty(draggingFromStepId)) return;
        if (draggingFromStepId != fromStepId) return;
        UpdateDragPreviewPosition(screenPosition);
    }

    void CompleteConnectorDrag(string fromStepId, string toStepId)
    {
        if (string.IsNullOrEmpty(fromStepId) || string.IsNullOrEmpty(toStepId))
        {
            Debug.LogWarning($"[ScenarioGraphUI] Drag connect invalid from={fromStepId} to={toStepId}");
            CancelConnectorDrag(clearStatus: true);
            return;
        }

        Debug.Log($"[ScenarioGraphUI] Drag connect complete from={fromStepId} to={toStepId}");
        graph.AddEdge(fromStepId, toStepId);
        CancelConnectorDrag(clearStatus: true);
        RefreshLines();
    }

    void CancelConnectorDrag(bool clearStatus)
    {
        if (!string.IsNullOrEmpty(draggingFromStepId))
        {
            Debug.Log($"[ScenarioGraphUI] Drag connect cancel from={draggingFromStepId}");
        }
        draggingFromStepId = null;

        if (dragPreviewLine != null) Destroy(dragPreviewLine.gameObject);
        if (dragPreviewTarget != null) Destroy(dragPreviewTarget.gameObject);
        dragPreviewLine = null;
        dragPreviewTarget = null;

        if (clearStatus && statusText != null)
        {
            statusText.text = "";
        }
    }

    void EnsureDragPreview(StepNodeUI fromUi)
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
            ConfigureLineGraphic(dragPreviewLine, DragPreviewLineColor, 8f);
        }

        dragPreviewLine.from = fromUi.outputConnector.GetComponent<RectTransform>();
        dragPreviewLine.to = dragPreviewTarget;
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
            if (line != null)
            {
                Destroy(line.gameObject);
                removed++;
            }
        }
        lines.Clear();

        int expectedEdges = 0;
        int created = 0;
        foreach (var step in graph.curriculum.steps)
        {
            if (!nodeUIs.TryGetValue(step.id, out var fromUi))
            {
                continue;
            }

            foreach (var toId in step.nextStepIds)
            {
                expectedEdges++;
                if (!nodeUIs.TryGetValue(toId, out var toUi))
                {
                    Debug.LogWarning($"[ScenarioGraphUI] Missing node UI for edge {step.id} -> {toId}");
                    continue;
                }

                var line = Instantiate(lineTemplate, lineLayer);
                line.gameObject.SetActive(true);
                line.from = fromUi.outputConnector.GetComponent<RectTransform>();
                line.to = toUi.inputConnector.GetComponent<RectTransform>();
                ConfigureLineGraphic(line, ConnectionLineColor, 8f);
                lines.Add(line);
                created++;

                var fromRt = line.from;
                var toRt = line.to;
                var fromWorld = fromRt != null ? fromRt.TransformPoint(fromRt.rect.center) : Vector3.zero;
                var toWorld = toRt != null ? toRt.TransformPoint(toRt.rect.center) : Vector3.zero;
                float distance = Vector3.Distance(fromWorld, toWorld);
                Debug.Log($"[ScenarioGraphUI] line edge {step.id}->{toId} distance={distance:F2} from={fromWorld} to={toWorld}");
            }
        }

        var layerSize = lineLayer != null ? lineLayer.rect.size : Vector2.zero;
        Debug.Log($"[ScenarioGraphUI] RefreshLines expectedEdges={expectedEdges} created={created} removed={removed} lineLayerSize={layerSize}");
    }

    void ConfigureLineGraphic(ConnectionLineGraphic line, Color color, float thickness)
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
        line.raycastTarget = false;
        line.SetAllDirty();
    }

    void SaveCurriculum()
    {
        if (!string.IsNullOrWhiteSpace(projectNameInput.text))
        {
            graph.curriculum.projectName = projectNameInput.text.Trim();
        }

        int warningCount = 0;
        foreach (var step in graph.curriculum.steps)
        {
            if (graph.HasUnconfiguredConditions(step)) warningCount++;
        }

        string exportDir = Path.Combine(Application.dataPath, "Exports");
        Directory.CreateDirectory(exportDir);

        string fileName = $"{graph.curriculum.projectName}-curriculum.json";
        string finalPath = Path.Combine(exportDir, fileName);
        string tempPath = finalPath + ".tmp";

        File.WriteAllText(tempPath, JsonUtility.ToJson(graph.curriculum, true));
        if (File.Exists(finalPath)) File.Replace(tempPath, finalPath, null);
        else File.Move(tempPath, finalPath);

        statusText.text = warningCount > 0
            ? $"保存しました（未設定手順: {warningCount}）: Assets/Exports/{fileName}"
            : $"保存しました: Assets/Exports/{fileName}";
        Debug.Log("[ScenarioGraph] " + statusText.text);
    }
}
