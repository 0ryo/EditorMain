using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class ScenarioNodeSelectionHandle : MonoBehaviour, IPointerClickHandler
{
    public System.Action<bool> onSelect;
    Outline outline;
    public void OnPointerClick(PointerEventData data)
    {
        if (data.button != PointerEventData.InputButton.Left || data.dragging) return;
        var hit = data.pointerCurrentRaycast.gameObject;
        if (hit != null && hit.GetComponentInParent<Selectable>() != null) return;
        onSelect?.Invoke(EditInput.ShiftPressed());
    }
    public void SetSelected(bool selected)
    {
        if (outline == null)
        {
            // Separate effect component; validation owns its own outline.
            outline = gameObject.AddComponent<Outline>();
            outline.effectColor = DesignTokens.Accent;
            outline.effectDistance = new Vector2(3, -3);
        }
        outline.enabled = selected;
    }
}
