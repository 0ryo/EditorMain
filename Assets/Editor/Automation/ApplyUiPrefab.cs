using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

public static class ApplyUiPrefab
{
    const string UiRootPrefabPath = "Assets/UI/Prefabs/UIRoot.prefab";

    [MenuItem("Tools/Automation/Apply UI Prefab")]
    public static void Apply()
    {
        EnsureUiRootPrefab();

        var uiRootPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(UiRootPrefabPath);
        if (uiRootPrefab == null)
        {
            throw new FileNotFoundException("[ApplyUiPrefab] UIRoot prefab not found: " + UiRootPrefabPath);
        }

        var scenePaths = CollectTargetScenes();
        if (scenePaths.Count == 0)
        {
            Debug.LogWarning("[ApplyUiPrefab] No target scenes found.");
            return;
        }

        foreach (var scenePath in scenePaths)
        {
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            bool changed = false;

            var uiRootInScene = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None)
                .FirstOrDefault(t => t.name == "UIRoot");

            if (uiRootInScene == null)
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(uiRootPrefab, scene);
                instance.name = "UIRoot";
                uiRootInScene = instance.transform;
                changed = true;
                Debug.Log($"[ApplyUiPrefab] UIRoot added to scene: {scenePath}");
            }

            changed |= ConfigureCatalogUi(scene, uiRootInScene);

            if (changed)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
            else
            {
                Debug.Log($"[ApplyUiPrefab] UIRoot already exists: {scenePath}");
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    static bool ConfigureCatalogUi(UnityEngine.SceneManagement.Scene scene, Transform uiRootInScene)
    {
        if (uiRootInScene == null) return false;

        bool changed = false;
        var preferredCatalog = uiRootInScene.GetComponentInChildren<CatalogUI>(true);
        var allCatalogs = Object.FindObjectsByType<CatalogUI>(FindObjectsSortMode.None);
        var placement = Object.FindFirstObjectByType<PlacementController>(FindObjectsInactive.Exclude);

        foreach (var catalog in allCatalogs)
        {
            if (catalog == null) continue;
            if (catalog == preferredCatalog)
            {
                var so = new SerializedObject(catalog);
                var registryProp = so.FindProperty("registry");
                if (placement != null && placement.registry != null && registryProp != null &&
                    registryProp.objectReferenceValue != placement.registry)
                {
                    registryProp.objectReferenceValue = placement.registry;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    changed = true;
                }
                continue;
            }

            if (catalog.gameObject.activeSelf)
            {
                catalog.gameObject.SetActive(false);
                changed = true;
                Debug.Log($"[ApplyUiPrefab] Legacy Catalog disabled: {catalog.gameObject.name} ({scene.path})");
            }
        }

        return changed;
    }

    static void EnsureUiRootPrefab()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(UiRootPrefabPath) != null) return;

        BuildUiPrefabs.Build();
    }

    static List<string> CollectTargetScenes()
    {
        const string editorMainScene = "Assets/EditorMain.unity";
        var buildScenes = EditorBuildSettings.scenes
            .Where(s => s.enabled && !string.IsNullOrWhiteSpace(s.path))
            .Select(s => s.path)
            .Distinct()
            .ToList();
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(editorMainScene) != null && !buildScenes.Contains(editorMainScene))
        {
            buildScenes.Add(editorMainScene);
        }
        if (buildScenes.Count > 0) return buildScenes;

        var scenesRoot = "Assets/Scenes";
        if (!AssetDatabase.IsValidFolder(scenesRoot)) return new List<string>();

        var fallback = AssetDatabase.FindAssets("t:Scene", new[] { scenesRoot })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Distinct()
            .ToList();
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(editorMainScene) != null && !fallback.Contains(editorMainScene))
        {
            fallback.Add(editorMainScene);
        }
        return fallback;
    }
}
