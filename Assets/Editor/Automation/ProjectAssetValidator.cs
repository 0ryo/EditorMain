using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class ProjectAssetValidator
{
    const string SampleScenePath = "Assets/Scenes/SampleScene.unity";
    const string MainScenePath = "Assets/EditorMain.unity";

    [MenuItem("Tools/Automation/Validate Scene and Prefab Assets")]
    public static void ValidateSceneAndPrefabAssets()
    {
        var errors = new List<string>();
        var openedScenes = new List<Scene>();
        var scenePaths = new HashSet<string>(EditorBuildSettings.scenes
            .Where(scene => scene.enabled && !string.IsNullOrWhiteSpace(scene.path))
            .Select(scene => scene.path), StringComparer.Ordinal);
        scenePaths.Add(MainScenePath);
        scenePaths.Add(SampleScenePath);

        try
        {
            foreach (string scenePath in scenePaths.OrderBy(path => path, StringComparer.Ordinal))
            {
                string projectRoot = System.IO.Directory.GetParent(Application.dataPath).FullName;
                if (!System.IO.File.Exists(System.IO.Path.Combine(projectRoot, scenePath)))
                {
                    errors.Add($"Scene file is missing: {scenePath}");
                    continue;
                }

                var scene = SceneManager.GetSceneByPath(scenePath);
                if (!scene.IsValid() || !scene.isLoaded)
                {
                    scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
                    if (scene.IsValid() && scene.isLoaded) openedScenes.Add(scene);
                }

                if (!scene.IsValid() || !scene.isLoaded)
                {
                    errors.Add($"Scene could not be loaded: {scenePath}");
                    continue;
                }

                ValidateRoots(scene.GetRootGameObjects(), scenePath, errors);
            }

            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" }))
            {
                string prefabPath = AssetDatabase.GUIDToAssetPath(guid);
                var prefabRoot = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                if (prefabRoot == null)
                {
                    errors.Add($"Prefab could not be loaded: {prefabPath}");
                    continue;
                }

                ValidateRoots(new[] { prefabRoot }, prefabPath, errors);
            }
        }
        finally
        {
            foreach (var scene in openedScenes)
                if (scene.IsValid() && scene.isLoaded)
                    EditorSceneManager.CloseScene(scene, true);
        }

        if (errors.Count > 0)
        {
            foreach (string error in errors) Debug.LogError("[ProjectAssetValidator] " + error);
            throw new InvalidOperationException($"Scene/Prefab validation found {errors.Count} issue(s).");
        }

        Debug.Log($"[ProjectAssetValidator] Validated {scenePaths.Count} scene(s) and all project Prefabs; no missing scripts or serialized object references.");
    }

    static void ValidateRoots(IEnumerable<GameObject> roots, string assetPath, List<string> errors)
    {
        foreach (var root in roots)
        {
            if (root == null) continue;
            foreach (var component in root.GetComponentsInChildren<Component>(true))
            {
                if (component == null)
                {
                    errors.Add($"Missing script component: {assetPath} ({root.name})");
                    continue;
                }

                var serialized = new SerializedObject(component);
                var property = serialized.GetIterator();
                while (property.NextVisible(true))
                {
                    if (property.propertyType != SerializedPropertyType.ObjectReference ||
                        property.objectReferenceValue != null || property.objectReferenceInstanceIDValue == 0)
                        continue;

                    errors.Add($"Missing serialized reference: {assetPath} ({component.name}.{property.propertyPath})");
                }
            }
        }
    }
}
