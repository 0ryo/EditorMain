using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEngine;
#if UNITY_EDITOR
using System.IO;
using UnityEditor;
#endif

/// <summary>
/// TMP の既定フォントに日本語フォールバックを補完する初期化ユーティリティ。
/// Editor では永続アセットを作成して TMP Settings に登録し、Play 中はそれを再利用する。
/// </summary>
public static class TmpFontInitializer
{
    const string FallbackAssetPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/Japanese TMP Fallback.asset";
    const string FallbackResourcePath = "Fonts & Materials/Japanese TMP Fallback";
    const string FallbackNameSuffix = "Dynamic TMP Fallback";

    static bool initialized;
    static TMP_FontAsset japaneseFallbackFontAsset;

    static readonly string[] JapaneseFontFamilies =
    {
        "Yu Gothic UI",
        "Yu Gothic",
        "Meiryo UI",
        "Meiryo",
        "BIZ UDPGothic",
        "MS Gothic",
    };

    static readonly string[] StyleCandidates =
    {
        "Regular",
        "Normal",
        "Book",
    };

    static readonly char[] ProbeCharacters = { 'あ', 'ア', '漢', '条', '（', '）' };

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
            Debug.LogWarning("[TmpFontInitializer] No compatible Japanese system font found.");
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
        if (changed)
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
#if UNITY_EDITOR
        var persistent = LoadOrCreatePersistentFallbackAsset();
        if (persistent != null) return persistent;
#endif

        var resourceFont = Resources.Load<TMP_FontAsset>(FallbackResourcePath);
        if (resourceFont != null) return resourceFont;

        return CreateTransientFallbackAsset();
    }

#if UNITY_EDITOR
    static TMP_FontAsset LoadOrCreatePersistentFallbackAsset()
    {
        var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FallbackAssetPath);
        if (existing != null)
        {
            RepairPersistentFontAsset(existing);
            return existing;
        }

        var directory = Path.GetDirectoryName(FallbackAssetPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
            AssetDatabase.Refresh();
        }

        var created = CreateTransientFallbackAsset();
        if (created == null) return null;

        created.name = Path.GetFileNameWithoutExtension(FallbackAssetPath);
        AssetDatabase.CreateAsset(created, FallbackAssetPath);
        AddSubAssets(created);
        AssetDatabase.SaveAssets();
        AssetDatabase.ImportAsset(FallbackAssetPath, ImportAssetOptions.ForceUpdate);

        var loaded = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FallbackAssetPath);
        if (loaded != null)
        {
            RepairPersistentFontAsset(loaded);
            AssetDatabase.SaveAssets();
        }
        return loaded;
    }

    static void RepairPersistentFontAsset(TMP_FontAsset fontAsset)
    {
        if (fontAsset == null) return;

        AddSubAssets(fontAsset);
        EditorUtility.SetDirty(fontAsset);
    }

    static void AddSubAssets(TMP_FontAsset fontAsset)
    {
        if (fontAsset == null) return;

        if (fontAsset.atlasTextures != null)
        {
            foreach (var tex in fontAsset.atlasTextures)
            {
                if (tex == null) continue;
                if (AssetDatabase.Contains(tex)) continue;
                tex.name = fontAsset.name + " Atlas";
                AssetDatabase.AddObjectToAsset(tex, fontAsset);
            }
        }

        var mat = fontAsset.material;
        if (mat != null && !AssetDatabase.Contains(mat))
        {
            mat.name = fontAsset.name + " Material";
            AssetDatabase.AddObjectToAsset(mat, fontAsset);
        }
    }

    static void PersistTmpSettings(TMP_FontAsset defaultFont)
    {
        EditorUtility.SetDirty(defaultFont);

        var settingsAsset = AssetDatabase.LoadAssetAtPath<TMP_Settings>("Assets/TextMesh Pro/Resources/TMP Settings.asset");
        if (settingsAsset != null)
        {
            EditorUtility.SetDirty(settingsAsset);
        }

        AssetDatabase.SaveAssets();
    }
#endif

    static TMP_FontAsset CreateTransientFallbackAsset()
    {
        foreach (var family in JapaneseFontFamilies)
        {
            foreach (var style in StyleCandidates)
            {
                TMP_FontAsset tmpFont = null;
                try
                {
                    tmpFont = TMP_FontAsset.CreateFontAsset(family, style, 90);
                }
                catch
                {
                    tmpFont = null;
                }

                if (tmpFont == null) continue;
                if (!ContainsProbeCharacters(tmpFont, true)) continue;

                tmpFont.name = $"{family} {style} ({FallbackNameSuffix})";
                tmpFont.isMultiAtlasTexturesEnabled = true;
                return tmpFont;
            }
        }

        return null;
    }

    static bool SanitizeFallbackList(List<TMP_FontAsset> fontAssets)
    {
        if (fontAssets == null) return false;

        int before = fontAssets.Count;
        fontAssets.RemoveAll(IsBrokenOrTransientFallback);
        return before != fontAssets.Count;
    }

    static bool EnsureFallbackRegistered(List<TMP_FontAsset> fontAssets, TMP_FontAsset fallback)
    {
        if (fontAssets == null || fallback == null) return false;
        if (fontAssets.Contains(fallback)) return false;

        fontAssets.Insert(0, fallback);
        return true;
    }

    static bool IsBrokenOrTransientFallback(TMP_FontAsset fontAsset)
    {
        if (fontAsset == null) return true;
        if (fontAsset.material == null) return true;

#if UNITY_EDITOR
        if (fontAsset.name.Contains(FallbackNameSuffix) && !AssetDatabase.Contains(fontAsset))
            return true;
#endif

        return false;
    }

    static bool ContainsProbeCharacters(TMP_FontAsset fontAsset, bool tryAddCharacters)
    {
        if (fontAsset == null) return false;

        foreach (var c in ProbeCharacters)
        {
            if (!fontAsset.HasCharacter(c, true, tryAddCharacters))
                return false;
        }

        return true;
    }

    static void ClearTmpMaterialCaches()
    {
        TMP_MaterialManager.ClearMaterials();

        var type = typeof(TMP_MaterialManager);
        ClearPrivateCollection(type, "m_fallbackMaterials");
        ClearPrivateCollection(type, "m_fallbackMaterialLookup");
        ClearPrivateCollection(type, "m_fallbackCleanupList");
    }

    static void ClearPrivateCollection(System.Type type, string fieldName)
    {
        var field = type.GetField(fieldName, BindingFlags.Static | BindingFlags.NonPublic);
        if (field == null) return;

        var value = field.GetValue(null);
        switch (value)
        {
            case IDictionary dict:
                dict.Clear();
                break;
            case IList list:
                list.Clear();
                break;
        }
    }

    static void RefreshLoadedTextComponents()
    {
        var defaultFont = TMP_Settings.defaultFontAsset;
        if (defaultFont == null || defaultFont.material == null) return;

        RefreshLoadedSubMeshes(defaultFont);

        foreach (var text in Resources.FindObjectsOfTypeAll<TMP_Text>())
        {
            if (text == null) continue;
#if UNITY_EDITOR
            if (EditorUtility.IsPersistent(text)) continue;
#endif

            var font = text.font;
            if (font == null || IsBrokenOrTransientFallback(font))
            {
                text.font = defaultFont;
                font = defaultFont;
            }

            var targetMaterial = ResolveFontMaterial(font, defaultFont.material);
            ClearTextMaterialState(text);

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

        RefreshLoadedSubMeshes(defaultFont);
    }

    static void RefreshLoadedSubMeshes(TMP_FontAsset defaultFont)
    {
        foreach (var subMesh in Resources.FindObjectsOfTypeAll<TMP_SubMeshUI>())
        {
            if (subMesh == null) continue;
#if UNITY_EDITOR
            if (EditorUtility.IsPersistent(subMesh)) continue;
#endif
            RefreshSubMeshState(subMesh, defaultFont);
        }

        foreach (var subMesh in Resources.FindObjectsOfTypeAll<TMP_SubMesh>())
        {
            if (subMesh == null) continue;
#if UNITY_EDITOR
            if (EditorUtility.IsPersistent(subMesh)) continue;
#endif
            RefreshSubMeshState(subMesh, defaultFont);
        }
    }

    static void RefreshSubMeshState(TMP_SubMeshUI subMesh, TMP_FontAsset defaultFont)
    {
        var font = subMesh.fontAsset;
        if (font == null || IsBrokenOrTransientFallback(font))
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

    static void RefreshSubMeshState(TMP_SubMesh subMesh, TMP_FontAsset defaultFont)
    {
        var font = subMesh.fontAsset;
        if (font == null || IsBrokenOrTransientFallback(font))
        {
            subMesh.fontAsset = defaultFont;
            font = defaultFont;
        }

        subMesh.fallbackMaterial = null;
        subMesh.fallbackSourceMaterial = null;
        subMesh.sharedMaterial = ResolveFontMaterial(font, defaultFont.material);
    }

    static Material ResolveFontMaterial(TMP_FontAsset font, Material defaultMaterial)
    {
        return font != null && font.material != null ? font.material : defaultMaterial;
    }

    static void ClearTextMaterialState(TMP_Text text)
    {
        ClearPrivateInstanceField(text, "m_fontMaterial");
        ClearPrivateInstanceField(text, "m_fontMaterials");
        ClearPrivateInstanceField(text, "m_fontSharedMaterials");
        ClearPrivateInstanceField(text, "m_currentMaterial");
    }

    static void ClearPrivateInstanceField(object target, string fieldName)
    {
        var field = typeof(TMP_Text).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        if (field == null) return;

        field.SetValue(target, null);
    }
}
