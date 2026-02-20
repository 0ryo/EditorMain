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

    public Action<string> onClickInputConnector;
    public Action<string> onClickOutputConnector;
    public Action onChanged;

    public void Bind(CurriculumGraphService graph, StepNode targetStep)
    {
        graphService = graph;
        step = targetStep;

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

        if (step.conditions.Count == 0)
        {
            step.conditions.Add(new ProximityPair { aObjectId = null, bObjectId = null });
        }

        RebuildConditions();
        RefreshWarning();
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
        var options = PlacedObjectOptionProvider.GetOptions();

        var row = Instantiate(conditionRowTemplate, conditionListRoot);
        row.gameObject.SetActive(true);

        row.Bind(
            options,
            step.conditions[0].aObjectId,
            step.conditions[0].bObjectId,
            onAChanged: newId =>
            {
                step.conditions[0].aObjectId = newId;
                RefreshWarning();
                onChanged?.Invoke();
            },
            onBChanged: newId =>
            {
                step.conditions[0].bObjectId = newId;
                RefreshWarning();
                onChanged?.Invoke();
            }
        );

        conditionRows.Add(row);
    }
}
