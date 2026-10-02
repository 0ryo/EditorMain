using UnityEngine;
using UnityEngine.UI;

public sealed class UiScaleController : MonoBehaviour
{
    [SerializeField, Range(0.8f, 1.4f)] float scale = 1f;

    CanvasScaler canvasScaler;
    SkillSyncDesignView designView;
    int lastWidth, lastHeight;

    public float Scale => scale;

    public static UiScaleController Ensure(Transform uiRoot)
    {
        if (uiRoot == null) return null;
        var controller = uiRoot.GetComponent<UiScaleController>();
        if (controller == null) controller = uiRoot.gameObject.AddComponent<UiScaleController>();
        controller.ResolveScaler();
        if (controller.designView == null) UiWorkspacePanels.Ensure(uiRoot);
        return controller;
    }

    public void Apply(float value)
    {
        scale = Mathf.Clamp(value, 0.8f, 1.4f);
        ResolveScaler();
        if (canvasScaler == null) return;

        // The fixed design already includes its authored local scale. Its entire
        // 2560x1440 workspace must fit, including when legacy settings are applied.
        if (designView != null && designView.gameObject.activeInHierarchy)
        {
            canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasScaler.referenceResolution = new Vector2(SkillSyncDesignLayout.Width, SkillSyncDesignLayout.Height);
            canvasScaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            return;
        }

        // Keep body text at its authored pixel size on small displays; panels can collapse.
        float requestedFit = CalculateRequestedScale(Screen.width, Screen.height, scale);
        canvasScaler.uiScaleMode = requestedFit < 1f ? CanvasScaler.ScaleMode.ConstantPixelSize : CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasScaler.scaleFactor = 1f;
        canvasScaler.referenceResolution = DesignTokens.ReferenceResolution / scale;
        canvasScaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        canvasScaler.matchWidthOrHeight = 0.5f;
    }

    public static float CalculateEffectiveScale(int width, int height, float userScale)
    {
        return Mathf.Max(1f, CalculateRequestedScale(width, height, userScale));
    }

    static float CalculateRequestedScale(int width, int height, float userScale) =>
        Mathf.Sqrt(Mathf.Max(1, width) / DesignTokens.ReferenceResolution.x *
            Mathf.Max(1, height) / DesignTokens.ReferenceResolution.y) * Mathf.Clamp(userScale, 0.8f, 1.4f);

    void Update()
    {
        if (lastWidth == Screen.width && lastHeight == Screen.height) return;
        lastWidth = Screen.width; lastHeight = Screen.height;
        Apply(scale);
    }

    void ResolveScaler()
    {
        if (canvasScaler == null) canvasScaler = GetComponent<CanvasScaler>();
        if (designView == null) designView = GetComponentInChildren<SkillSyncDesignView>(true);
    }
}
