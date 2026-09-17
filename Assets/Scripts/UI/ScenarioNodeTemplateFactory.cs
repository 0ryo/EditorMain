using UnityEngine;
using UnityEngine.UI;

internal static class ScenarioNodeTemplateFactory
{
    public const string StartNodeLabel = "開始";
    public const string EndNodeLabel = "終了";

    public static void Ensure(
        StepNodeUI stepNodeTemplate,
        Transform nodeParent,
        ref TerminalNodeUI startNodeTemplate,
        ref TerminalNodeUI endNodeTemplate,
        ref ConditionNodeUI conditionNodeTemplate)
    {
        if (startNodeTemplate == null)
        {
            startNodeTemplate = CreateTerminalTemplateFromStepTemplate(
                stepNodeTemplate, nodeParent,
                "StartNodeTemplate_Runtime",
                StartNodeLabel,
                hasInput: false,
                hasOutput: true,
                color: DesignTokens.BgSecondary);
        }

        if (endNodeTemplate == null)
        {
            endNodeTemplate = CreateTerminalTemplateFromStepTemplate(
                stepNodeTemplate, nodeParent,
                "EndNodeTemplate_Runtime",
                EndNodeLabel,
                hasInput: true,
                hasOutput: false,
                color: DesignTokens.BgSecondary);
        }

        if (conditionNodeTemplate == null)
        {
            conditionNodeTemplate = CreateConditionTemplateFromStepTemplate(stepNodeTemplate, nodeParent);
        }
    }

    static ConditionNodeUI CreateConditionTemplateFromStepTemplate(StepNodeUI stepNodeTemplate, Transform nodeParent)
    {
        var clone = UnityEngine.Object.Instantiate(stepNodeTemplate.gameObject, nodeParent);
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
            row = UnityEngine.Object.Instantiate(sourceStepUi.conditionRowTemplate, sourceStepUi.conditionListRoot);
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

    static TerminalNodeUI CreateTerminalTemplateFromStepTemplate(
        StepNodeUI stepNodeTemplate,
        Transform nodeParent,
        string name,
        string label,
        bool hasInput,
        bool hasOutput,
        Color color)
    {
        var clone = UnityEngine.Object.Instantiate(stepNodeTemplate.gameObject, nodeParent);
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
}
