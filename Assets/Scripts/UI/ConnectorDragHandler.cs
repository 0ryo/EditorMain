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
    [SerializeField] string stepId;

    Action<string, Vector2> beginDragOutput;
    Action<string, Vector2> dragOutput;
    Action<string, string> completeDrag;
    Action cancelDrag;

    public void ConfigureInput(string ownerStepId)
    {
        role = ConnectorRole.Input;
        stepId = ownerStepId;
    }

    public void ConfigureOutput(
        string ownerStepId,
        Action<string, Vector2> onBeginDragOutput,
        Action<string, Vector2> onDragOutput,
        Action<string, string> onCompleteDrag,
        Action onCancelDrag
    )
    {
        role = ConnectorRole.Output;
        stepId = ownerStepId;
        beginDragOutput = onBeginDragOutput;
        dragOutput = onDragOutput;
        completeDrag = onCompleteDrag;
        cancelDrag = onCancelDrag;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (role != ConnectorRole.Output) return;
        Debug.Log($"[ConnectorDrag] Begin from={stepId} pos={eventData.position}");
        beginDragOutput?.Invoke(stepId, eventData.position);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (role != ConnectorRole.Output) return;
        dragOutput?.Invoke(stepId, eventData.position);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (role != ConnectorRole.Output) return;

        string targetStepId = ResolveInputTargetStepId(eventData);
        if (!string.IsNullOrEmpty(targetStepId))
        {
            Debug.Log($"[ConnectorDrag] Complete from={stepId} to={targetStepId} pos={eventData.position}");
            completeDrag?.Invoke(stepId, targetStepId);
        }
        else
        {
            Debug.LogWarning($"[ConnectorDrag] Cancel from={stepId} target not found pos={eventData.position}");
            cancelDrag?.Invoke();
        }
    }

    string ResolveInputTargetStepId(PointerEventData eventData)
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
        if (string.IsNullOrEmpty(handler.stepId)) return null;
        return handler.stepId;
    }
}
