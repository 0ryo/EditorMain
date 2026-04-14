using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TerminalNodeUI : MonoBehaviour
{
    [Header("Basic")]
    public TMP_Text labelText;

    [Header("Connectors")]
    public Button inputConnector;
    public Button outputConnector;

    ScenarioNode node;

    public Action<string> onClickInputConnector;
    public Action<string> onClickOutputConnector;
    public Action<string, Vector2> onBeginOutputConnectorDrag;
    public Action<string, Vector2> onOutputConnectorDrag;
    public Action<string, string> onCompleteConnectorDrag;
    public Action onCancelConnectorDrag;

    public void Bind(
        ScenarioNode targetNode,
        string displayLabel,
        bool allowInput,
        bool allowOutput
    )
    {
        node = targetNode;
        if (node == null)
        {
            Debug.LogError("[TerminalNodeUI] Invalid bind target.");
            return;
        }

        if (labelText != null)
        {
            labelText.text = displayLabel;
            labelText.raycastTarget = false;
            labelText.transform.SetAsLastSibling();
        }

        ConfigureInputConnector(allowInput);
        ConfigureOutputConnector(allowOutput);
    }

    void ConfigureInputConnector(bool allowInput)
    {
        if (inputConnector == null) return;

        inputConnector.gameObject.SetActive(allowInput);
        inputConnector.onClick.RemoveAllListeners();
        if (!allowInput) return;

        var drag = inputConnector.GetComponent<ConnectorDragHandler>();
        if (drag == null) drag = inputConnector.gameObject.AddComponent<ConnectorDragHandler>();
        drag.ConfigureInput(node.nodeId);

        inputConnector.onClick.AddListener(() => onClickInputConnector?.Invoke(node.nodeId));
    }

    void ConfigureOutputConnector(bool allowOutput)
    {
        if (outputConnector == null) return;

        outputConnector.gameObject.SetActive(allowOutput);
        outputConnector.onClick.RemoveAllListeners();
        if (!allowOutput) return;

        var drag = outputConnector.GetComponent<ConnectorDragHandler>();
        if (drag == null) drag = outputConnector.gameObject.AddComponent<ConnectorDragHandler>();
        drag.ConfigureOutput(
            node.nodeId,
            onBeginOutputConnectorDrag,
            onOutputConnectorDrag,
            onCompleteConnectorDrag,
            onCancelConnectorDrag
        );

        outputConnector.onClick.AddListener(() => onClickOutputConnector?.Invoke(node.nodeId));
    }
}
