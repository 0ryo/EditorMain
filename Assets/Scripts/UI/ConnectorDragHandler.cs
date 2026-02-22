using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class ConnectorDragHandler : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public enum ConnectorRole
    {
        Input,
        Output
    }

    [SerializeField] ConnectorRole role;
    [SerializeField] string nodeId;

    Action<string, Vector2> beginDragOutput;
    Action<string, Vector2> dragOutput;
    Action<string, string> completeDrag;
    Action cancelDrag;

    public void ConfigureInput(string ownerNodeId)
    {
        role = ConnectorRole.Input;
        nodeId = ownerNodeId;
    }

    public void ConfigureOutput(
        string ownerNodeId,
        Action<string, Vector2> onBeginDragOutput,
        Action<string, Vector2> onDragOutput,
        Action<string, string> onCompleteDrag,
        Action onCancelDrag
    )
    {
        role = ConnectorRole.Output;
        nodeId = ownerNodeId;
        beginDragOutput = onBeginDragOutput;
        dragOutput = onDragOutput;
        completeDrag = onCompleteDrag;
        cancelDrag = onCancelDrag;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (role != ConnectorRole.Output) return;
        Debug.Log($"[ConnectorDrag] Begin from={nodeId} pos={eventData.position}");
        beginDragOutput?.Invoke(nodeId, eventData.position);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (role != ConnectorRole.Output) return;
        dragOutput?.Invoke(nodeId, eventData.position);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (role != ConnectorRole.Output) return;

        string targetNodeId = ResolveInputTargetNodeId(eventData);
        if (!string.IsNullOrEmpty(targetNodeId))
        {
            Debug.Log($"[ConnectorDrag] Complete from={nodeId} to={targetNodeId} pos={eventData.position}");
            completeDrag?.Invoke(nodeId, targetNodeId);
        }
        else
        {
            Debug.LogWarning($"[ConnectorDrag] Cancel from={nodeId} target not found pos={eventData.position}");
            cancelDrag?.Invoke();
        }
    }

    string ResolveInputTargetNodeId(PointerEventData eventData)
    {
        var direct = ResolveFromGameObject(eventData.pointerCurrentRaycast.gameObject);
        if (!string.IsNullOrEmpty(direct)) return direct;

        direct = ResolveFromGameObject(eventData.pointerEnter);
        if (!string.IsNullOrEmpty(direct)) return direct;

        if (eventData.hovered != null)
        {
            for (int i = 0; i < eventData.hovered.Count; i++)
            {
                direct = ResolveFromGameObject(eventData.hovered[i]);
                if (!string.IsNullOrEmpty(direct)) return direct;
            }
        }

        // Fallback: explicit UI raycast at pointer position.
        if (EventSystem.current != null)
        {
            var raycastEvent = new PointerEventData(EventSystem.current)
            {
                position = eventData.position
            };
            var results = new List<RaycastResult>();
            EventSystem.current.RaycastAll(raycastEvent, results);
            for (int i = 0; i < results.Count; i++)
            {
                direct = ResolveFromGameObject(results[i].gameObject);
                if (!string.IsNullOrEmpty(direct)) return direct;
            }
        }

        return null;
    }

    string ResolveFromGameObject(GameObject go)
    {
        if (go == null) return null;

        var handler = go.GetComponentInParent<ConnectorDragHandler>();
        if (handler == null) return null;
        if (handler.role != ConnectorRole.Input) return null;
        if (string.IsNullOrEmpty(handler.nodeId)) return null;
        return handler.nodeId;
    }
}
