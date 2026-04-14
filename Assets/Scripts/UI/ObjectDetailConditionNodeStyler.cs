using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class ObjectDetailConditionNodeStyler : MonoBehaviour
{
    [Header("Detail Only Overrides")]
    [SerializeField] bool overrideNodeBackground;
    [SerializeField] Color nodeBackground = new Color(0.969f, 0.969f, 0.973f, 1f);
    [SerializeField] bool overrideOutlineColor;
    [SerializeField] Color outlineColor = new Color(0.82f, 0.82f, 0.839f, 1f);

    [Header("Detail Visibility")]
    [SerializeField] bool hideDeleteButton;
    [SerializeField] bool hideWarningIcon;
    [SerializeField] bool forceHideOutputConnector;

    [Header("Detail Layout")]
    [SerializeField] float minNodeHeight;
    [SerializeField] float preferredNodeHeight;

    public void Apply(ConditionNodeUI nodeUi)
    {
        if (nodeUi == null) return;

        if (overrideNodeBackground)
        {
            var image = nodeUi.GetComponent<Image>();
            if (image != null) image.color = nodeBackground;

            var dragHandle = nodeUi.transform.Find("DragHandle");
            var dragImage = dragHandle != null ? dragHandle.GetComponent<Image>() : null;
            if (dragImage != null) dragImage.color = nodeBackground;
        }

        if (overrideOutlineColor)
        {
            ApplyOutline(nodeUi.transform, outlineColor);
            var dragHandle = nodeUi.transform.Find("DragHandle");
            if (dragHandle != null) ApplyOutline(dragHandle, outlineColor);
        }

        if (hideDeleteButton && nodeUi.deleteButton != null)
        {
            nodeUi.deleteButton.gameObject.SetActive(false);
        }

        if (hideWarningIcon && nodeUi.warningIcon != null)
        {
            nodeUi.warningIcon.SetActive(false);
        }

        if (forceHideOutputConnector && nodeUi.outputConnector != null)
        {
            nodeUi.outputConnector.gameObject.SetActive(false);
        }

        if (minNodeHeight > 0f || preferredNodeHeight > 0f)
        {
            var layout = nodeUi.GetComponent<LayoutElement>();
            if (layout == null) layout = nodeUi.gameObject.AddComponent<LayoutElement>();

            if (minNodeHeight > 0f) layout.minHeight = minNodeHeight;
            if (preferredNodeHeight > 0f) layout.preferredHeight = preferredNodeHeight;
            layout.flexibleHeight = 0f;
        }
    }

    static void ApplyOutline(Transform target, Color color)
    {
        if (target == null) return;
        var graphic = target.GetComponent<Graphic>();
        if (graphic == null) return;

        var outline = target.GetComponent<Outline>();
        if (outline == null) outline = target.gameObject.AddComponent<Outline>();
        outline.effectColor = color;
        outline.effectDistance = new Vector2(0.5f, -0.5f);
        outline.useGraphicAlpha = false;
        outline.enabled = true;
    }
}
