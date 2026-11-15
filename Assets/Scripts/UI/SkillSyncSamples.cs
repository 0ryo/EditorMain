using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;

// Lesson samples that ship with the app (StreamingAssets/Samples/<name>/sample.json), such as the finished example and the evaluation task lessons. A sample is opened as a copy,
// so the shipped files are never changed. Its 3D models are added to the library on first use.
public static class SkillSyncSamples
{
    [Serializable] sealed class Manifest { public string title, description, project; public int order; public ModelEntry[] models; }
    [Serializable] sealed class ModelEntry { public string typeId, displayName, description, file; }

    public sealed class Sample
    {
        public string title, description, projectPath;
        public int order;
        public readonly List<(string typeId, string displayName, string description, string path)> models = new List<(string, string, string, string)>();
    }

    public static string Root => Path.Combine(Application.streamingAssetsPath, "Samples");

    // Every sample folder in this build, by the manifest's order and then by folder name. Incomplete samples are left out.
    public static List<Sample> All()
    {
        var samples = new List<(Sample sample, string folder)>();
        try
        {
            if (!Directory.Exists(Root)) return new List<Sample>();
            foreach (string folder in Directory.GetDirectories(Root))
            {
                var sample = Load(folder);
                if (sample != null) samples.Add((sample, Path.GetFileName(folder)));
            }
        }
        catch (Exception ex) { Debug.LogWarning("[SkillSyncSamples] " + ex.Message); }
        samples.Sort((a, b) => a.sample.order != b.sample.order ? a.sample.order.CompareTo(b.sample.order) : string.CompareOrdinal(a.folder, b.folder));
        return samples.ConvertAll(entry => entry.sample);
    }

    // Null when the folder has no sample or any of its files is missing. Model files may be shared with another sample folder.
    static Sample Load(string folder)
    {
        try
        {
            string manifestPath = Path.Combine(folder, "sample.json");
            if (!File.Exists(manifestPath)) return null;
            var manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(manifestPath));
            if (manifest == null || string.IsNullOrEmpty(manifest.project) || string.IsNullOrWhiteSpace(manifest.title)) return null;
            var sample = new Sample { title = manifest.title, description = manifest.description, order = manifest.order, projectPath = Path.Combine(folder, manifest.project) };
            if (!File.Exists(sample.projectPath)) return null;
            foreach (var model in manifest.models ?? Array.Empty<ModelEntry>())
            {
                string path = Path.GetFullPath(Path.Combine(folder, model.file));
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
