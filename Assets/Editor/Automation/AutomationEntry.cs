using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class AutomationEntry
{
    const string UiRootPrefabPath = "Assets/UI/Prefabs/UIRoot.prefab";
    const string EditorMainScenePath = "Assets/EditorMain.unity";

    public static void ApplyUiEdits()
    {
        Debug.Log("[AutomationEntry] ApplyUiEdits started.");
        BuildUiPrefabs.Build();
        ApplyUiPrefab.Apply();
        Debug.Log("[AutomationEntry] ApplyUiEdits completed.");
    }

    public static void MigrateScenarioData()
    {
        var services = UnityEngine.Object.FindObjectsByType<CurriculumGraphService>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        foreach (var service in services)
        {
            if (service == null) continue;
            service.EnsureGraphInitialized();
        }

        Debug.Log($"[AutomationEntry] MigrateScenarioData completed. migrated={services.Length}");
    }

    public static void ValidateProject()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(UiRootPrefabPath);
        if (prefab == null)
        {
            throw new InvalidOperationException($"[AutomationEntry] Missing prefab: {UiRootPrefabPath}");
        }

        RequirePrefabComponent<Canvas>(prefab);
        RequirePrefabComponent<GraphicRaycaster>(prefab);
        RequirePrefabComponent<ViewportStatusStrip>(prefab);

        var scenario = RequirePrefabComponent<ScenarioGraphUI>(prefab);
        RequireSerializedReferences(
            scenario,
            UiRootPrefabPath,
            "panelRoot",
            "projectNameInput",
            "addStepButton",
            "addConditionButton",
            "saveButton",
            "statusText",
            "nodeArea",
            "graphContent",
            "lineLayer",
            "lineTemplate",
            "stepNodeTemplate",
            "resizeHandle");

        var catalog = RequirePrefabComponent<CatalogUI>(prefab);
        RequireSerializedReferences(
            catalog,
            UiRootPrefabPath,
            "content",
            "buttonTemplate",
            "searchInput",
            "addButton",
            "statusText",
            "editModeRow",
            "browseModeButton",
            "transformModeButton",
            "scaleModeButton",
            "settingsButton");

        ValidateEditorMainScene();
        Debug.Log("[AutomationEntry] ValidateProject completed. Scene services, EventSystem, UI components, and serialized references are valid.");
    }

    static T RequirePrefabComponent<T>(GameObject prefab) where T : Component
    {
        var component = prefab.GetComponentInChildren<T>(true);
        if (component == null)
        {
            throw new InvalidOperationException(
                $"[AutomationEntry] {typeof(T).Name} is missing from {UiRootPrefabPath}.");
        }

        return component;
    }

    static void RequireSerializedReferences(UnityEngine.Object target, string sourcePath, params string[] propertyNames)
    {
        var serializedObject = new SerializedObject(target);
        foreach (var propertyName in propertyNames)
        {
            var property = serializedObject.FindProperty(propertyName);
            if (property == null)
            {
                throw new InvalidOperationException(
                    $"[AutomationEntry] {target.GetType().Name}.{propertyName} is not a serialized property.");
            }

            if (property.propertyType != SerializedPropertyType.ObjectReference || property.objectReferenceValue == null)
            {
                throw new InvalidOperationException(
                    $"[AutomationEntry] {target.GetType().Name}.{propertyName} is not assigned in {sourcePath}.");
            }
        }
    }

    static void ValidateEditorMainScene()
    {
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(EditorMainScenePath) == null)
        {
            throw new InvalidOperationException($"[AutomationEntry] Missing scene: {EditorMainScenePath}");
        }

        bool enabledInBuild = false;
        foreach (var buildScene in EditorBuildSettings.scenes)
        {
            if (!buildScene.enabled || buildScene.path != EditorMainScenePath) continue;
            enabledInBuild = true;
            break;
        }

        if (!enabledInBuild)
        {
            throw new InvalidOperationException(
                $"[AutomationEntry] {EditorMainScenePath} is not enabled in Editor Build Settings.");
        }

        var scene = SceneManager.GetSceneByPath(EditorMainScenePath);
        bool openedForValidation = !scene.IsValid() || !scene.isLoaded;
        if (openedForValidation)
        {
            scene = EditorSceneManager.OpenScene(EditorMainScenePath, OpenSceneMode.Additive);
        }

        try
        {
            var placement = RequireSingleSceneComponent<PlacementController>(scene);
            RequireSerializedReferences(placement, EditorMainScenePath, "registry", "cam", "selection");
            RequireSingleSceneComponent<SelectionService>(scene);
            RequireSingleSceneComponent<CommandService>(scene);
            RequireSingleSceneComponent<EditModeService>(scene);
            RequireSingleSceneComponent<IdGenerator>(scene);
            RequireSingleSceneComponent<EventSystem>(scene);
        }
        finally
        {
            if (openedForValidation && scene.IsValid() && scene.isLoaded)
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }
    }

    static T RequireSingleSceneComponent<T>(Scene scene) where T : Component
    {
        var components = new List<T>();
        foreach (var root in scene.GetRootGameObjects())
        {
            components.AddRange(root.GetComponentsInChildren<T>(true));
        }

        if (components.Count != 1)
        {
            throw new InvalidOperationException(
                $"[AutomationEntry] {EditorMainScenePath} must contain exactly one {typeof(T).Name}; found {components.Count}.");
        }

        return components[0];
    }
}
