using UnityEngine;
using UnityEngine.UI;

public static class HighContrastTheme
{
    const string PreferenceKey = "SkillSync.HighContrastTheme";
    static Transform uiRoot;
    static bool initialized;

    public static bool Enabled => DesignTokens.HighContrastEnabled;

    public static void Ensure(Transform root)
    {
        if (root == null) return;
        var canvas = root.GetComponentInParent<Canvas>(true);
        uiRoot = canvas != null ? canvas.rootCanvas.transform : root;
        if (!initialized)
        {
            bool enabled = PlayerPrefs.GetInt(PreferenceKey, 0) != 0;
            DesignTokens.SetHighContrastEnabled(enabled);
            ApplyPalette(uiRoot, enabled);
            initialized = true;
        }
        else if (Enabled)
        {
            ApplyPalette(uiRoot, true);
        }
    }

    public static void SetEnabled(bool enabled, Transform root = null)
    {
        if (root != null) Ensure(root);
        if (DesignTokens.HighContrastEnabled == enabled) return;

        DesignTokens.SetHighContrastEnabled(enabled);
        ApplyPalette(uiRoot, enabled);
        PlayerPrefs.SetInt(PreferenceKey, enabled ? 1 : 0);
        PlayerPrefs.Save();
    }

    static void ApplyPalette(Transform root, bool highContrast)
    {
        if (root != null)
        {
            foreach (var graphic in root.GetComponentsInChildren<Graphic>(true))
                if (graphic != null) graphic.color = DesignTokens.MapPaletteColor(graphic.color, highContrast);

            foreach (var selectable in root.GetComponentsInChildren<Selectable>(true))
            {
                if (selectable == null) continue;
                var colors = selectable.colors;
                colors.normalColor = DesignTokens.MapPaletteColor(colors.normalColor, highContrast);
                colors.highlightedColor = DesignTokens.MapPaletteColor(colors.highlightedColor, highContrast);
                colors.pressedColor = DesignTokens.MapPaletteColor(colors.pressedColor, highContrast);
                colors.selectedColor = DesignTokens.MapPaletteColor(colors.selectedColor, highContrast);
                colors.disabledColor = DesignTokens.MapPaletteColor(colors.disabledColor, highContrast);
                selectable.colors = colors;
            }

            foreach (var outline in root.GetComponentsInChildren<Outline>(true))
                if (outline != null) outline.effectColor = DesignTokens.MapPaletteColor(outline.effectColor, highContrast);
        }

        foreach (var outline in Object.FindObjectsByType<SelectionOutline>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (outline != null) outline.ApplyAccessibilityTheme();
    }
}
