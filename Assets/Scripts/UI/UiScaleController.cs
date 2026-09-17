using UnityEngine;
using UnityEngine.UI;

public sealed class UiScaleController : MonoBehaviour
{
    [SerializeField, Range(0.8f, 1.4f)] float scale = 1f;

    CanvasScaler canvasScaler;

    public float Scale => scale;

    public static UiScaleController Ensure(Transform uiRoot)
    {
        if (uiRoot == null) return null;
        var controller = uiRoot.GetComponent<UiScaleController>();
        if (controller == null) controller = uiRoot.gameObject.AddComponent<UiScaleController>();
        controller.ResolveScaler();
        return controller;
    }

    public void Apply(float value)
    {
        scale = Mathf.Clamp(value, 0.8f, 1.4f);
        ResolveScaler();
        if (canvasScaler == null) return;

        canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasScaler.referenceResolution = DesignTokens.ReferenceResolution / scale;
        canvasScaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        canvasScaler.matchWidthOrHeight = 0.5f;
    }

    void ResolveScaler()
    {
        if (canvasScaler == null) canvasScaler = GetComponent<CanvasScaler>();
    }
}
