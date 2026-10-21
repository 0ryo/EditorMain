using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;

// Lesson samples that ship with the app (StreamingAssets/Samples/<name>/sample.json). A sample is opened as a copy,
// so the shipped files are never changed. Its 3D models are added to the library on first use.
public static class SkillSyncSamples
{
    [Serializable] sealed class Manifest { public string title, description, project; public ModelEntry[] models; }
    [Serializable] sealed class ModelEntry { public string typeId, displayName, description, file; }

    public sealed class Sample
    {
        public string title, description, projectPath;
        public readonly List<(string typeId, string displayName, string description, string path)> models = new List<(string, string, string, string)>();
    }

    const string TireChangeFolder = "TireChange";

    public static string Root => Path.Combine(Application.streamingAssetsPath, "Samples");

    // Null when the sample is not part of this build or any of its files is missing.
    public static Sample TireChange()
    {
        try
        {
            string folder = Path.Combine(Root, TireChangeFolder);
            string manifestPath = Path.Combine(folder, "sample.json");
            if (!File.Exists(manifestPath)) return null;
            var manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(manifestPath));
            if (manifest == null || string.IsNullOrEmpty(manifest.project) || string.IsNullOrWhiteSpace(manifest.title)) return null;
            var sample = new Sample { title = manifest.title, description = manifest.description, projectPath = Path.Combine(folder, manifest.project) };
            if (!File.Exists(sample.projectPath)) return null;
            foreach (var model in manifest.models ?? Array.Empty<ModelEntry>())
            {
                string path = Path.Combine(folder, model.file);
                if (!File.Exists(path)) return null;
                sample.models.Add((model.typeId, model.displayName, model.description, path));
            }
            return sample;
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[SkillSyncSamples] " + ex.Message);
            return null;
        }
    }

    // True when every 3D model of the sample is available in the library.
    public static async Task<bool> PrepareModelsAsync(Sample sample, CatalogUI catalog)
    {
        if (sample == null || catalog == null) return false;
        foreach (var model in sample.models)
            if (!await catalog.EnsureBundledModelAsync(model.typeId, model.displayName, model.description, model.path)) return false;
        return true;
    }
}
