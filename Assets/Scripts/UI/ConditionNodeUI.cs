using System;
using UnityEngine;
using UnityEngine.UI;

public class ConditionNodeUI : MonoBehaviour
{
    const string LabelUnset = "\u672A\u8A2D\u5B9A";

    [Header("Basic")]
    public Text nodeIdText;
    public GameObject warningIcon;
    public ConditionRowUI conditionRow;

    [Header("Connectors")]
    public Button outputConnector;
    public Button deleteButton;

    ScenarioNode conditionNode;
    CurriculumGraphService graphService;
    string currentOptionSignature = string.Empty;
    float nextOptionPollTime;

    public Action<string> onClickOutputConnector;
    public Action<string, Vector2> onBeginOutputConnectorDrag;
    public Action<string, Vector2> onOutputConnectorDrag;
    public Action<string, string> onCompleteConnectorDrag;
    public Action onCancelConnectorDrag;
    public Action<string> onClickDelete;
    public Action onChanged;

    public void Bind(CurriculumGraphService graph, ScenarioNode targetCondition)
    {
        graphService = graph;
        conditionNode = targetCondition;
        currentOptionSignature = string.Empty;
        nextOptionPollTime = 0f;

        if (conditionNode == null || conditionNode.nodeType != ScenarioNodeType.Condition)
        {
            Debug.LogError("[ConditionNodeUI] Invalid bind target.");
            return;
        }

        if (conditionNode.condition == null)
        {
            conditionNode.condition = new ConditionNodeData();
        }

        ConfigureConnectorDragHandlers();
        ConfigureDeleteButton();
        RefreshConditionOptionsIfNeeded(force: true);
        RefreshWarning();
    }

    void Update()
    {
        if (conditionNode == null || !isActiveAndEnabled || graphService == null) return;
        if (Time.unscaledTime < nextOptionPollTime) return;

        nextOptionPollTime = Time.unscaledTime + 0.2f;
        RefreshConditionOptionsIfNeeded();
        RefreshWarning();
    }

    void RefreshConditionOptionsIfNeeded(bool force = false)
    {
        if (conditionRow == null) return;

        var options = PlacedObjectOptionProvider.GetOptions();
        var signature = PlacedObjectOptionProvider.BuildSignature(options);
        if (!force && signature == currentOptionSignature) return;
        currentOptionSignature = signature;

        conditionRow.Bind(
            options,
            conditionNode.condition.objectAId,
            conditionNode.condition.objectBId,
            onAChanged: newId =>
            {
                conditionNode.condition.objectAId = newId;
                UpdateNodeLabel();
                RefreshWarning();
                onChanged?.Invoke();
            },
            onBChanged: newId =>
            {
                conditionNode.condition.objectBId = newId;
                UpdateNodeLabel();
                RefreshWarning();
                onChanged?.Invoke();
            }
        );

        UpdateNodeLabel();
    }

    public void RefreshWarning()
    {
        if (warningIcon == null || graphService == null || conditionNode == null) return;
        warningIcon.SetActive(!graphService.IsConditionConfigured(conditionNode));
    }

    void ConfigureConnectorDragHandlers()
    {
        if (outputConnector == null || conditionNode == null) return;

        var outputDrag = outputConnector.GetComponent<ConnectorDragHandler>();
        if (outputDrag == null) outputDrag = outputConnector.gameObject.AddComponent<ConnectorDragHandler>();
        outputDrag.ConfigureOutput(
            conditionNode.nodeId,
            onBeginOutputConnectorDrag,
            onOutputConnectorDrag,
            onCompleteConnectorDrag,
            onCancelConnectorDrag
        );

        outputConnector.onClick.RemoveAllListeners();
        outputConnector.onClick.AddListener(() => onClickOutputConnector?.Invoke(conditionNode.nodeId));
    }

    void ConfigureDeleteButton()
    {
        EnsureDeleteButton();
        if (deleteButton == null || conditionNode == null) return;

        deleteButton.gameObject.SetActive(true);
        deleteButton.onClick.RemoveAllListeners();
        deleteButton.onClick.AddListener(() => onClickDelete?.Invoke(conditionNode.nodeId));
        deleteButton.transform.SetAsLastSibling();
    }

    void EnsureDeleteButton()
    {
        var dragHandle = transform.Find("DragHandle") as RectTransform;

        if (deleteButton == null)
        {
            var existing = transform.Find("Button_Delete");
            if (existing != null)
            {
                deleteButton = existing.GetComponent<Button>();
                if (deleteButton == null) deleteButton = existing.gameObject.AddComponent<Button>();
            }
        }

        if (deleteButton == null)
        {
            var buttonGo = new GameObject("Button_Delete", typeof(RectTransform), typeof(Image), typeof(Button));
            var rt = buttonGo.GetComponent<RectTransform>();
            rt.SetParent(dragHandle != null ? dragHandle : transform, false);

            deleteButton = buttonGo.GetComponent<Button>();

            var labelGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
            var labelRt = labelGo.GetComponent<RectTransform>();
            labelRt.SetParent(rt, false);
            labelRt.anchorMin = Vector2.zero;
            labelRt.anchorMax = Vector2.one;
            labelRt.offsetMin = Vector2.zero;
            labelRt.offsetMax = Vector2.zero;

            var labelText = labelGo.GetComponent<Text>();
            labelText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            labelText.text = "X";
            labelText.fontSize = 14;
            labelText.alignment = TextAnchor.MiddleCenter;
            labelText.color = new Color(0.45f, 0.08f, 0.08f, 1f);
            labelText.raycastTarget = false;
        }

        var image = deleteButton.GetComponent<Image>();
        if (image != null)
        {
            image.color = new Color(0.82f, 0.82f, 0.82f, 1f);
        }

        var labelTextCurrent = deleteButton.GetComponentInChildren<Text>(true);
        if (labelTextCurrent != null)
        {
            labelTextCurrent.color = new Color(0.2f, 0.2f, 0.2f, 1f);
        }

        var deleteRt = deleteButton.GetComponent<RectTransform>();
        if (deleteRt != null)
        {
            if (dragHandle != null && deleteRt.parent != dragHandle)
            {
                deleteRt.SetParent(dragHandle, false);
            }

            deleteRt.anchorMin = new Vector2(1f, 0.5f);
            deleteRt.anchorMax = new Vector2(1f, 0.5f);
            deleteRt.pivot = new Vector2(1f, 0.5f);
            deleteRt.sizeDelta = new Vector2(22f, 22f);
            deleteRt.anchoredPosition = new Vector2(-8f, 0f);
        }
    }

    void UpdateNodeLabel()
    {
        if (nodeIdText == null || conditionNode == null) return;

        string a = string.IsNullOrWhiteSpace(conditionNode.condition.objectAId) ? LabelUnset : conditionNode.condition.objectAId;
        string b = string.IsNullOrWhiteSpace(conditionNode.condition.objectBId) ? LabelUnset : conditionNode.condition.objectBId;
        nodeIdText.text = $"\"{a}\" \u3092 \"{b}\" \u306B\u8FD1\u3065\u3051\u308B";
    }
}
