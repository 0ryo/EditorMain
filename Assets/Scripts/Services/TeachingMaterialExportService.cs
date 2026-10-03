using System;
using System.IO;
using System.Linq;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public static class TeachingMaterialExportService
{
    public static string Export(ScenarioExport export, PlacementController placement)
    {
        if (export == null) throw new ArgumentNullException(nameof(export));
        string directory = RuntimeExportPathUtility.CreateTeachingMaterialDirectory(
            RuntimeExportPathUtility.ExportsDirectory, DateTime.Now);
        string name = Path.GetFileName(directory);
        string jsonPath = Path.Combine(directory, name + ".json");
        ScenarioModelBundle.Prepare(export, jsonPath);
        export.prefabPackage = null;
#if UNITY_EDITOR
        PreparePrefabPackage(export, placement, jsonPath, name);
#endif
        // The archive is self-contained, including the exact JSON and all its asset references.
        TeachingMaterialArchive.Write(export, jsonPath);
        ExportFileWriter.WriteAllTextWithBackup(jsonPath, JsonUtility.ToJson(export, true));
        return jsonPath;
    }

#if UNITY_EDITOR
    static void PreparePrefabPackage(ScenarioExport export, PlacementController placement, string jsonPath, string name)
    {
        var prefabs = export.models.Where(model => model.requiresPreinstalledPrefab).ToList();
        if (prefabs.Count == 0) return;
        if (placement == null) throw new InvalidOperationException("書き出すオブジェクトの一覧が見つかりません。");

        foreach (var model in prefabs)
        {
            if (!placement.TryGetPrefab(model.typeId, out var prefab) || prefab == null)
                throw new InvalidOperationException("書き出すオブジェクトが見つかりません: " + model.typeId);
            model.prefabAssetPath = AssetDatabase.GetAssetPath(prefab);
            if (string.IsNullOrWhiteSpace(model.prefabAssetPath) ||
                !(model.prefabAssetPath.StartsWith("Assets/", StringComparison.Ordinal) ||
                  model.prefabAssetPath.StartsWith("Packages/", StringComparison.Ordinal)))
                throw new InvalidOperationException("保存済みPrefabが必要です: " + model.typeId);
        }

        // Resolve dependencies explicitly: IncludeDependencies may export unrelated project scripts.
        // Packages are supplied by the XR project's package manager, not copied into Assets.
        string[] assets = AssetDatabase.GetDependencies(prefabs.Select(model => model.prefabAssetPath).Distinct().ToArray(), true)
            .Where(path => path.StartsWith("Assets/", StringComparison.Ordinal))
            .Distinct().OrderBy(path => path, StringComparer.Ordinal).ToArray();
        if (assets.Length == 0) return;

        string directory = Path.GetDirectoryName(jsonPath);
        Directory.CreateDirectory(directory);
        // Each export owns a new folder, so earlier packages are never replaced.
        string packageName = name + ".unitypackage";
        string packagePath = Path.Combine(directory, packageName);
        string tempPath = packagePath + ".tmp.unitypackage";
        try
        {
            AssetDatabase.ExportPackage(assets, tempPath, ExportPackageOptions.Default);
            if (!File.Exists(tempPath) || new FileInfo(tempPath).Length == 0)
                throw new IOException("オブジェクトのパッケージを作成できませんでした。");
            File.Move(tempPath, packagePath);
            export.prefabPackage = packageName;
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }
#endif
}
