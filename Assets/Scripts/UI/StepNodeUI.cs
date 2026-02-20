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
    public Button addConditionButton;

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

        addConditionButton.onClick.RemoveAllListeners();
        addConditionButton.onClick.AddListener(() =>
        {
            step.conditions.Add(new ProximityPair { aObjectId = null, bObjectId = null });
            RebuildConditions();
            onChanged?.Invoke();
        });

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
            Destroy(child.gameObject);
        }

        conditionRows.Clear();
        var options = PlacedObjectOptionProvider.GetOptions();

        for (int i = 0; i < step.conditions.Count; i++)
        {
            int index = i;
            var row = Instantiate(conditionRowTemplate, conditionListRoot);
            row.gameObject.SetActive(true);

            row.Bind(
                options,
                step.conditions[index].aObjectId,
                step.conditions[index].bObjectId,
                onAChanged: newId =>
                {
                    step.conditions[index].aObjectId = newId;
                    RefreshWarning();
                    onChanged?.Invoke();
                },
                onBChanged: newId =>
                {
                    step.conditions[index].bObjectId = newId;
                    RefreshWarning();
                    onChanged?.Invoke();
                },
                onRemove: () =>
                {
                    if (step.conditions.Count <= 1)
                    {
                        step.conditions[index].aObjectId = null;
                        step.conditions[index].bObjectId = null;
                    }
                    else
                    {
                        step.conditions.RemoveAt(index);
                    }

                    RebuildConditions();
                    RefreshWarning();
                    onChanged?.Invoke();
                }
            );

            conditionRows.Add(row);
        }
    }
}
