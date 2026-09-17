using UnityEditor;
using UnityEngine;

public static class ApplyAuthoringFeatures
{
    const string PrefabPath = "Assets/UI/Prefabs/UIRoot.prefab";

    [MenuItem("Tools/Automation/Apply Authoring Features")]
    public static void Apply()
    {
        var root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            Prepare(root);
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        AssetDatabase.SaveAssets();
        Debug.Log("[Authoring] 複数選択・整列、成功条件、分岐プレビューのUIを更新しました。");
    }

    public static void Prepare(GameObject root)
    {
        ViewportOutliner.PreparePrefab(root.transform);
        foreach (var graph in root.GetComponentsInChildren<ScenarioGraphUI>(true)) graph.PrepareAuthoringPrefab();
        foreach (var condition in root.GetComponentsInChildren<ConditionNodeUI>(true)) condition.PrepareTemplateControls();
        foreach (var row in root.GetComponentsInChildren<ConditionRowUI>(true))
        {
            ObjectDropdownBrowser.Prepare(row.dropdownA);
            ObjectDropdownBrowser.Prepare(row.dropdownB);
        }
    }
}
