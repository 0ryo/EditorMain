using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

public class ScenarioGraphUI : MonoBehaviour
{
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

            foreach (var toId in step.nextStepIds)
            {
                if (!nodeUIs.TryGetValue(toId, out var toUi)) continue;

                var line = Instantiate(lineTemplate, lineLayer);
                line.gameObject.SetActive(true);
                line.from = fromUi.outputConnector.GetComponent<RectTransform>();
                line.to = toUi.inputConnector.GetComponent<RectTransform>();
                line.color = new Color(0.35f, 0.35f, 0.35f, 1f);
                lines.Add(line);
            }
        }
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
            ? $"保存しました（⚠未設定手順: {warningCount}）: Assets/Exports/{fileName}"
            : $"保存しました: Assets/Exports/{fileName}";
        Debug.Log("[ScenarioGraph] " + statusText.text);
    }
}
