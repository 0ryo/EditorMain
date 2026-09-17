using System;
using System.IO;
using System.Linq;

public static class ScenarioModelBundle
{
    // Publish immutable asset folders before replacing JSON, so a backup JSON retains valid assets.
    public static void Prepare(ScenarioExport export, string jsonPath)
    {
        var records = ImportedModelStore.ReadAll(message => UnityEngine.Debug.LogWarning("[Model bundle] " + message))
            .GroupBy(record => record.typeId).ToDictionary(group => group.Key, group => group.First());
        string outputDirectory = Path.GetDirectoryName(Path.GetFullPath(jsonPath));
        string bundleName = Path.GetFileNameWithoutExtension(jsonPath) + "_assets_" + Guid.NewGuid().ToString("N");
        export.models.Clear();
        foreach (string typeId in export.objects.Select(item => item.typeId).Distinct())
        {
            var model = new ScenarioModelExport { typeId = typeId, requiresPreinstalledPrefab = true };
            if (records.TryGetValue(typeId, out var record) && !record.editorAsset)
            {
                string sourceModel = ImportedModelStore.ResolveModelPath(record);
                if (!File.Exists(sourceModel)) throw new IOException("保存済みモデルがありません: " + record.displayName);
                string sourceDirectory = Path.Combine(Path.GetDirectoryName(record.recordPath), "payload");
                string modelFolder = Guid.NewGuid().ToString("N");
                string targetDirectory = Path.Combine(outputDirectory, bundleName, modelFolder);
                foreach (string source in Directory.GetFiles(sourceDirectory, "*", SearchOption.AllDirectories))
                {
                    string relative = source.Substring(sourceDirectory.Length + 1);
                    string target = Path.Combine(targetDirectory, relative);
                    Directory.CreateDirectory(Path.GetDirectoryName(target));
                    File.Copy(source, target);
                }
                model.uri = bundleName + "/" + modelFolder + "/" + Path.GetFileName(sourceModel);
                model.requiresPreinstalledPrefab = false;
            }
            export.models.Add(model);
        }
    }
}
