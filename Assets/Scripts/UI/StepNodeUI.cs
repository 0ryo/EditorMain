using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class StepNodeUI : MonoBehaviour
{
    [Header("Basic")]
    public Text stepIdText;
    public InputField titleInput;
    public GameObject warningIcon;

    [Header("Connectors")]
    public Button inputConnector;
    public Button outputConnector;

    [Header("Conditions")]
    public RectTransform conditionListRoot;
    public ConditionRowUI conditionRowTemplate;

    StepNode step;
    CurriculumGraphService graphService;
    readonly List<ConditionRowUI> conditionRows = new List<ConditionRowUI>();
    string currentOptionSignature = string.Empty;
    float nextOptionPollTime;

    public Action<string> onClickInputConnector;
    public Action<string> onClickOutputConnector;
    public Action<string, Vector2> onBeginOutputConnectorDrag;
    public Action<string, Vector2> onOutputConnectorDrag;
    public Action<string, string> onCompleteConnectorDrag;
    public Action onCancelConnectorDrag;
    public Action onChanged;

    public void Bind(CurriculumGraphService graph, StepNode targetStep)
    {
        graphService = graph;
        step = targetStep;
        currentOptionSignature = string.Empty;
        nextOptionPollTime = 0f;

        ApplyConditionLayoutStyle();

        stepIdText.text = step.id;

        titleInput.SetTextWithoutNotify(step.title);
        titleInput.onEndEdit.RemoveAllListeners();
        titleInput.onEndEdit.AddListener(v =>
        {
            step.title = v;
            onChanged?.Invoke();
        });

        inputConnector.onClick.RemoveAllListeners();
        outputConnector.onClick.RemoveAllListeners();
        inputConnector.onClick.AddListener(() => onClickInputConnector?.Invoke(step.id));
        outputConnector.onClick.AddListener(() => onClickOutputConnector?.Invoke(step.id));
        ConfigureConnectorDragHandlers();

        if (step.conditions.Count == 0)
        {
            step.conditions.Add(new ProximityPair { aObjectId = null, bObjectId = null });
        }

        RebuildConditions();
        RefreshWarning();
    }

    void Update()
    {
        if (step == null || !isActiveAndEnabled) return;
        if (Time.unscaledTime < nextOptionPollTime) return;

        nextOptionPollTime = Time.unscaledTime + 0.2f;
        RefreshConditionOptionsIfNeeded();
    }

    public void RefreshWarning()
    {
        bool hasWarning = graphService != null && graphService.HasUnconfiguredConditions(step);
        if (warningIcon != null)
        {
            warningIcon.SetActive(hasWarning);
        }
    }

    void RebuildConditions()
    {
        foreach (Transform child in conditionListRoot)
        {
            if (conditionRowTemplate != null && child == conditionRowTemplate.transform) continue;
            Destroy(child.gameObject);
        }

        conditionRows.Clear();

        var row = Instantiate(conditionRowTemplate, conditionListRoot);
        row.gameObject.SetActive(true);
        conditionRows.Add(row);

        RefreshConditionOptionsIfNeeded(force: true);
    }

    void RefreshConditionOptionsIfNeeded(bool force = false)
    {
        if (conditionRows.Count == 0 || conditionRows[0] == null) return;
        if (step.conditions.Count == 0)
        {
            step.conditions.Add(new ProximityPair { aObjectId = null, bObjectId = null });
        }

        var options = PlacedObjectOptionProvider.GetOptions();
        var newSignature = PlacedObjectOptionProvider.BuildSignature(options);

        if (!force && newSignature == currentOptionSignature) return;
        currentOptionSignature = newSignature;

        Debug.Log($"[StepNodeUI:{step.id}] refresh options={options.Count} force={force}");

        var condition = step.conditions[0];
        conditionRows[0].Bind(
            options,
            condition.aObjectId,
            condition.bObjectId,
            onAChanged: newId =>
            {
                condition.aObjectId = newId;
                RefreshWarning();
                onChanged?.Invoke();
            },
            onBChanged: newId =>
            {
                condition.bObjectId = newId;
                RefreshWarning();
                onChanged?.Invoke();
            }
        );
    }

    void ApplyConditionLayoutStyle()
    {
        if (conditionListRoot == null) return;

        conditionListRoot.offsetMin = new Vector2(conditionListRoot.offsetMin.x, 30f);
        conditionListRoot.offsetMax = new Vector2(conditionListRoot.offsetMax.x, 106f);
    }

    void ConfigureConnectorDragHandlers()
    {
        if (inputConnector != null)
        {
            var inputDrag = inputConnector.GetComponent<ConnectorDragHandler>();
            if (inputDrag == null) inputDrag = inputConnector.gameObject.AddComponent<ConnectorDragHandler>();
            inputDrag.ConfigureInput(step.id);
        }

        if (outputConnector != null)
        {
            var outputDrag = outputConnector.GetComponent<ConnectorDragHandler>();
            if (outputDrag == null) outputDrag = outputConnector.gameObject.AddComponent<ConnectorDragHandler>();
            outputDrag.ConfigureOutput(
                step.id,
                onBeginOutputConnectorDrag,
                onOutputConnectorDrag,
                onCompleteConnectorDrag,
                onCancelConnectorDrag
            );
        }
    }
}
