using UnityEngine;
using UnityEngine.UI;

public sealed class UiScaleController : MonoBehaviour
{
    [SerializeField, Range(0.8f, 1.4f)] float scale = 1f;

    CanvasScaler canvasScaler;
    int lastWidth, lastHeight;

    public float Scale => scale;

    public static UiScaleController Ensure(Transform uiRoot)
    {
        if (uiRoot == null) return null;
        var controller = uiRoot.GetComponent<UiScaleController>();
        if (controller == null) controller = uiRoot.gameObject.AddComponent<UiScaleController>();
        controller.ResolveScaler();
        UiWorkspacePanels.Ensure(uiRoot);
        return controller;
    }

    public void Apply(float value)
    {
        scale = Mathf.Clamp(value, 0.8f, 1.4f);
        ResolveScaler();
        if (canvasScaler == null) return;

        // Keep body text at its authored pixel size on small displays; panels can collapse.
        float fit = Mathf.Sqrt(Mathf.Max(1, Screen.width) / DesignTokens.ReferenceResolution.x *
            Mathf.Max(1, Screen.height) / DesignTokens.ReferenceResolution.y) * scale;
        canvasScaler.uiScaleMode = fit < 1f ? CanvasScaler.ScaleMode.ConstantPixelSize : CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasScaler.scaleFactor = 1f;
        canvasScaler.referenceResolution = DesignTokens.ReferenceResolution / scale;
        canvasScaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        canvasScaler.matchWidthOrHeight = 0.5f;
    }

    void Update()
    {
        if (lastWidth == Screen.width && lastHeight == Screen.height) return;
        lastWidth = Screen.width; lastHeight = Screen.height;
        Apply(scale);
    }

    void ResolveScaler()
    {
        if (canvasScaler == null) canvasScaler = GetComponent<CanvasScaler>();
    }
}
