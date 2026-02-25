using System;
using UnityEditor;
using UnityEngine;

public static class AutomationEntry
{
    const string UiRootPrefabPath = "Assets/UI/Prefabs/UIRoot.prefab";

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

        var scenario = prefab.GetComponentInChildren<ScenarioGraphUI>(true);
        if (scenario == null)
        {
            throw new InvalidOperationException("[AutomationEntry] ScenarioGraphUI is missing from UIRoot prefab.");
        }

        var catalog = prefab.GetComponentInChildren<CatalogUI>(true);
        if (catalog == null)
        {
            throw new InvalidOperationException("[AutomationEntry] CatalogUI is missing from UIRoot prefab.");
        }

        Debug.Log("[AutomationEntry] ValidateProject completed. Core UI components are present.");
    }
}
