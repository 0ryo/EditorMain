using System.Collections.Generic;
using TMPro;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// TMP の既定フォントに、同梱した日本語フォールバックを補完する初期化ユーティリティ。
/// </summary>
public static class TmpFontInitializer
{
    const string TmpSettingsAssetPath = "Assets/TextMesh Pro/Resources/TMP Settings.asset";

    static bool initialized;
    static TMP_FontAsset japaneseFallbackFontAsset;
    static bool warnedTmpCacheClearFailure;
    static bool warnedSubMeshRefreshFailure;
    static bool warnedTextRefreshFailure;

#if UNITY_EDITOR
    [InitializeOnLoadMethod]
    static void InitializeInEditor()
    {
        EditorApplication.delayCall -= EnsureJapaneseFallback;
        EditorApplication.delayCall += EnsureJapaneseFallback;
    }
#endif

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void InitializeAtRuntime()
    {
        EnsureJapaneseFallback();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void RefreshAfterSceneLoad()
    {
        RefreshLoadedTextComponents();
    }

    static void EnsureJapaneseFallback()
    {
        var defaultFont = TMP_Settings.defaultFontAsset;
        if (defaultFont == null)
        {
            Debug.LogWarning("[TmpFontInitializer] TMP default font asset not found.");
            return;
        }

        japaneseFallbackFontAsset = LoadPreferredFallbackAsset();
        if (japaneseFallbackFontAsset == null)
        {
            Debug.LogWarning("[TmpFontInitializer] Bundled Japanese fallback font is missing or invalid.");
            return;
        }

        bool changed = false;

        var defaultFallbacks = defaultFont.fallbackFontAssetTable;
        if (defaultFallbacks == null)
        {
            defaultFallbacks = new List<TMP_FontAsset>();
            defaultFont.fallbackFontAssetTable = defaultFallbacks;
            changed = true;
        }
        changed |= SanitizeFallbackList(defaultFallbacks);
        changed |= EnsureFallbackRegistered(defaultFallbacks, japaneseFallbackFontAsset);

        var globalFallbacks = TMP_Settings.fallbackFontAssets;
        if (globalFallbacks == null)
        {
            globalFallbacks = new List<TMP_FontAsset>();
            TMP_Settings.fallbackFontAssets = globalFallbacks;
            changed = true;
        }
        changed |= SanitizeFallbackList(globalFallbacks);
        changed |= EnsureFallbackRegistered(globalFallbacks, japaneseFallbackFontAsset);

#if UNITY_EDITOR
        if (changed && AssetDatabase.Contains(japaneseFallbackFontAsset))
        {
            PersistTmpSettings(defaultFont);
        }
#endif

        if (changed || !initialized)
        {
            ClearTmpMaterialCaches();
            TMPro_EventManager.ON_FONT_PROPERTY_CHANGED(true, defaultFont);
            TMPro_EventManager.ON_FONT_PROPERTY_CHANGED(true, japaneseFallbackFontAsset);
            TMPro_EventManager.ON_TMP_SETTINGS_CHANGED();
            RefreshLoadedTextComponents();
            Debug.Log($"[TmpFontInitializer] Registered Japanese fallback font: {japaneseFallbackFontAsset.name}");
        }

        initialized = true;
    }

    static TMP_FontAsset LoadPreferredFallbackAsset()
    {
        var configuredFallbacks = TMP_Settings.fallbackFontAssets;
        if (configuredFallbacks == null) return null;

        foreach (var fallback in configuredFallbacks)
        {
            if (fallback == null || fallback.material == null || fallback.sourceFontFile == null) continue;
            return fallback;
        }

        return null;
    }

#if UNITY_EDITOR
    static void PersistTmpSettings(TMP_FontAsset defaultFont)
    {
        EditorUtility.SetDirty(defaultFont);
        var settingsAsset = AssetDatabase.LoadAssetAtPath<TMP_Settings>(TmpSettingsAssetPath);
        if (settingsAsset != null) EditorUtility.SetDirty(settingsAsset);
        AssetDatabase.SaveAssets();
    }
#endif

    static bool SanitizeFallbackList(List<TMP_FontAsset> fontAssets)
    {
        if (fontAssets == null) return false;

        int before = fontAssets.Count;
        fontAssets.RemoveAll(IsBrokenFallback);
        return before != fontAssets.Count;
    }

    static bool EnsureFallbackRegistered(List<TMP_FontAsset> fontAssets, TMP_FontAsset fallback)
    {
        if (fontAssets == null || fallback == null) return false;
        if (fontAssets.Contains(fallback)) return false;

        fontAssets.Insert(0, fallback);
        return true;
    }

    static bool IsBrokenFallback(TMP_FontAsset fontAsset)
    {
        if (fontAsset == null) return true;
        if (fontAsset.material == null) return true;
        return string.Equals(fontAsset.name, "Japanese TMP Fallback", System.StringComparison.Ordinal) &&
               fontAsset.sourceFontFile == null;
    }

    static void ClearTmpMaterialCaches()
    {
        try
        {
            TMP_MaterialManager.ClearMaterials();
        }
        catch (System.Exception ex)
        {
            WarnOnce(
                ref warnedTmpCacheClearFailure,
                $"[TmpFontInitializer] TMP material cache clear skipped because TextMeshPro is mid-refresh. {ex.GetType().Name}: {ex.Message}");
        }

    }

    static void RefreshLoadedTextComponents()
    {
        var defaultFont = TMP_Settings.defaultFontAsset;
        if (defaultFont == null || defaultFont.material == null) return;

        RefreshLoadedSubMeshes(defaultFont);

        foreach (var text in UnityEngine.Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (text == null) continue;

            RefreshTextState(text, defaultFont);
        }

        RefreshLoadedSubMeshes(defaultFont);
    }

    static void RefreshTextState(TMP_Text text, TMP_FontAsset defaultFont)
    {
        try
        {
            var font = text.font;
            if (font == null || IsBrokenFallback(font))
            {
                text.font = defaultFont;
                font = defaultFont;
            }

            var targetMaterial = ResolveFontMaterial(font, defaultFont.material);
            var sharedMaterial = text.fontSharedMaterial;
            if (sharedMaterial == null || font == null || font.material == null)
            {
                text.fontSharedMaterial = targetMaterial;
            }
            else if (sharedMaterial != targetMaterial)
            {
                text.fontSharedMaterial = targetMaterial;
            }

            if (text is TextMeshProUGUI ugui)
            {
                ugui.UpdateFontAsset();
                ugui.havePropertiesChanged = true;
                ugui.SetVerticesDirty();
                ugui.SetLayoutDirty();
                ugui.SetMaterialDirty();
            }
            else if (text is TextMeshPro tmp)
            {
                tmp.UpdateFontAsset();
                tmp.havePropertiesChanged = true;
                tmp.SetVerticesDirty();
                tmp.SetLayoutDirty();
                tmp.SetMaterialDirty();
            }
        }
        catch (System.Exception ex)
        {
            WarnOnce(
                ref warnedTextRefreshFailure,
                $"[TmpFontInitializer] TMP text refresh skipped for {GetObjectName(text)}. {ex.GetType().Name}: {ex.Message}");
        }
    }

    static void RefreshLoadedSubMeshes(TMP_FontAsset defaultFont)
    {
        foreach (var subMesh in UnityEngine.Object.FindObjectsByType<TMP_SubMeshUI>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (subMesh == null) continue;
            RefreshSubMeshState(subMesh, defaultFont);
        }

        foreach (var subMesh in UnityEngine.Object.FindObjectsByType<TMP_SubMesh>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (subMesh == null) continue;
            RefreshSubMeshState(subMesh, defaultFont);
        }
    }

    static void RefreshSubMeshState(TMP_SubMeshUI subMesh, TMP_FontAsset defaultFont)
    {
        try
        {
            var font = subMesh.fontAsset;
            if (font == null || IsBrokenFallback(font))
            {
                subMesh.fontAsset = defaultFont;
                font = defaultFont;
            }

            subMesh.fallbackMaterial = null;
            subMesh.fallbackSourceMaterial = null;
            subMesh.sharedMaterial = ResolveFontMaterial(font, defaultFont.material);
            subMesh.SetMaterialDirty();
            subMesh.SetVerticesDirty();
        }
        catch (System.Exception ex)
        {
            WarnOnce(
                ref warnedSubMeshRefreshFailure,
                $"[TmpFontInitializer] TMP UI sub-mesh refresh skipped for {GetObjectName(subMesh)}. {ex.GetType().Name}: {ex.Message}");
        }
    }

    static void RefreshSubMeshState(TMP_SubMesh subMesh, TMP_FontAsset defaultFont)
    {
        try
        {
            var font = subMesh.fontAsset;
            if (font == null || IsBrokenFallback(font))
            {
                subMesh.fontAsset = defaultFont;
                font = defaultFont;
            }

            subMesh.fallbackMaterial = null;
            subMesh.fallbackSourceMaterial = null;
            subMesh.sharedMaterial = ResolveFontMaterial(font, defaultFont.material);
        }
        catch (System.Exception ex)
        {
            WarnOnce(
                ref warnedSubMeshRefreshFailure,
                $"[TmpFontInitializer] TMP sub-mesh refresh skipped for {GetObjectName(subMesh)}. {ex.GetType().Name}: {ex.Message}");
        }
    }

    static Material ResolveFontMaterial(TMP_FontAsset font, Material defaultMaterial)
    {
        return font != null && font.material != null ? font.material : defaultMaterial;
    }

    static void WarnOnce(ref bool warned, string message)
    {
        if (warned) return;
        warned = true;
        Debug.LogWarning(message);
    }

    static string GetObjectName(UnityEngine.Object obj)
    {
        return obj != null ? obj.name : "(destroyed)";
    }
}
