using UnityEngine;
using UnityEngine.UI;

/// <summary>共通の操作領域寸法を適用する。</summary>
public static class UiAccessibilityMetrics
{
    public static void EnsureButtonTarget(Button button)
    {
        if (button == null) return;

        var rect = button.transform as RectTransform;
        if (rect == null) return;
        EditWorkspace.EnsureInputBlockerForControl(button.transform);

        // Measured design controls share fixed visual positions. Growing them
        // independently creates overlapping hit areas and survives later resizes.
        // Their dimensions are authored in the prefab/design updater instead.
        if (button.GetComponentInParent<SkillSyncDesignView>(true) != null) return;

        var canvas = button.GetComponentInParent<Canvas>()?.rootCanvas;
        float canvasScale = canvas != null ? canvas.scaleFactor : 1f;
        float rootScaleX = canvas != null ? Mathf.Abs(canvas.transform.lossyScale.x) : 1f;
        float rootScaleY = canvas != null ? Mathf.Abs(canvas.transform.lossyScale.y) : 1f;
        float effectiveScaleX = canvasScale * Mathf.Abs(rect.lossyScale.x) / Mathf.Max(0.0001f, rootScaleX);
        float effectiveScaleY = canvasScale * Mathf.Abs(rect.lossyScale.y) / Mathf.Max(0.0001f, rootScaleY);
        float minimumWidth = DesignTokens.MinTouchTarget / Mathf.Max(0.01f, effectiveScaleX);
        float minimumHeight = DesignTokens.MinTouchTarget / Mathf.Max(0.01f, effectiveScaleY);

        var layout = button.GetComponent<LayoutElement>();
        if (layout != null && !layout.ignoreLayout)
        {
            layout.minWidth = Mathf.Max(layout.minWidth, minimumWidth);
            layout.minHeight = Mathf.Max(layout.minHeight, minimumHeight);
            if (layout.preferredWidth > 0f)
                layout.preferredWidth = Mathf.Max(layout.preferredWidth, minimumWidth);
            if (layout.preferredHeight > 0f)
                layout.preferredHeight = Mathf.Max(layout.preferredHeight, minimumHeight);
            return;
        }

        // Fixed-position buttons have no LayoutGroup to honor LayoutElement.
        // Grow the existing RectTransform around its current anchor and pivot.
        rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,
            Mathf.Max(rect.rect.width, minimumWidth));
        rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,
            Mathf.Max(rect.rect.height, minimumHeight));
    }

    public static void EnsureButtonTargets(Transform root)
    {
        if (root == null) return;
        foreach (var button in root.GetComponentsInChildren<Button>(true))
            EnsureButtonTarget(button);
    }
}
