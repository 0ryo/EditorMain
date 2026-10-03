using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using UnityEngine;

static class MaterialExportChecks
{
    public static void Run(string root)
    {
        string directory = Path.Combine(root, "ExportChecks");
        Directory.CreateDirectory(Path.Combine(directory, "input", "textures"));
        string source = Path.Combine(directory, "input", "model.gltf");
        File.WriteAllText(source, "{\"buffers\":[{\"uri\":\"mesh.bin\"}],\"images\":[{\"uri\":\"textures/body.png\"}]}");
        File.WriteAllBytes(Path.Combine(directory, "input", "mesh.bin"), new byte[] { 1, 2, 3 });
        File.WriteAllBytes(Path.Combine(directory, "input", "textures", "body.png"), new byte[] { 4, 5, 6 });
        ImportedModelStore.Save(source, "Imported/Used", "Used", "");
        ImportedModelStore.Save(source, "Imported/Unused", "Unused", "");
        CheckSeparateExports(root);
        CheckModelNames(root, source);
        var export = new ScenarioExport { projectName = "Lesson" };
        export.objects.Add(new PlacementExportObject { id = "a", typeId = "Imported/Used" });
        export.objects.Add(new PlacementExportObject { id = "b", typeId = "Imported/Used" });
        export.objects.Add(new PlacementExportObject { id = "c", typeId = "Builtin/Box" });
        string jsonPath = Path.Combine(directory, "Lesson-curriculum.json");
        ScenarioModelBundle.Prepare(export, jsonPath);
        Require(export.models.Count == 2 && export.models.Count(model => !model.requiresPreinstalledPrefab) == 1,
            "Only used model types are exported, without duplicate payloads");
        string firstUri = export.models.Single(model => model.typeId == "Imported/Used").uri;
        Require(File.Exists(Path.Combine(directory, firstUri)), "Model URI resolves beside the JSON");

        export.prefabPackage = "Lesson-objects.unitypackage";
        export.models.Single(model => model.typeId == "Builtin/Box").prefabAssetPath = "Assets/Prefabs/Box.prefab";
        File.WriteAllBytes(Path.Combine(directory, export.prefabPackage), new byte[] { 7, 8, 9 });
        File.WriteAllText(jsonPath + ".bak", "old JSON is not distributed");
        File.WriteAllText(Path.Combine(directory, "unrelated.json"), "not distributed");
        TeachingMaterialArchive.Write(export, jsonPath);
        string zipPath = TeachingMaterialArchive.GetPath(jsonPath);
        using (var zip = ZipFile.OpenRead(zipPath))
        {
            Require(zip.Entries.Count == 5, "ZIP contains JSON, package, model, buffer and texture only");
            using var reader = new StreamReader(zip.GetEntry(Path.GetFileName(jsonPath)).Open());
            var archived = JsonUtility.FromJson<ScenarioExport>(reader.ReadToEnd());
            Require(archived.prefabPackage == export.prefabPackage &&
                archived.models.Single(model => model.typeId == "Builtin/Box").prefabAssetPath == "Assets/Prefabs/Box.prefab",
                "JSON preserves XR prefab mapping and package reference");
            Require(zip.GetEntry(firstUri) != null && zip.GetEntry(export.prefabPackage) != null &&
                zip.GetEntry(firstUri.Substring(0, firstUri.LastIndexOf('/') + 1) + "textures/body.png") != null,
                "All archive-relative references resolve with texture hierarchy preserved");
        }
        byte[] firstZip = File.ReadAllBytes(zipPath);
        export.projectName = "Changed";
        ScenarioModelBundle.Prepare(export, jsonPath);
        string newUri = export.models.Single(model => model.typeId == "Imported/Used").uri;
        TeachingMaterialArchive.Write(export, jsonPath);
        Require(File.Exists(Path.Combine(directory, firstUri)) && newUri != firstUri,
            "Re-export preserves previous JSON's immutable model payload");
        Require(File.ReadAllBytes(zipPath + ".bak").SequenceEqual(firstZip), "Re-export preserves previous archive as backup");
        using (var zip = ZipFile.OpenRead(zipPath))
            Require(zip.GetEntry(newUri) != null && zip.GetEntry(firstUri) == null, "Re-export ZIP contains current assets only");

        byte[] goodZip = File.ReadAllBytes(zipPath);
        File.Delete(Path.Combine(directory, newUri));
        Reject(() => TeachingMaterialArchive.Write(export, jsonPath), "Missing model blocks export");
        Require(File.ReadAllBytes(zipPath).SequenceEqual(goodZip) && !File.Exists(zipPath + ".tmp"),
            "Failed export keeps previous archive and cleans temporary file");
        export.models.Single(model => model.typeId == "Imported/Used").uri = "../outside.gltf";
        Reject(() => TeachingMaterialArchive.Write(export, jsonPath), "Outside files cannot enter distribution archive");

        export.models.Clear();
        export.prefabPackage = null;
        TeachingMaterialArchive.Write(export, jsonPath);
        using (var zip = ZipFile.OpenRead(zipPath))
            Require(zip.Entries.Count == 1, "Lessons without model files still produce an XR archive");
        Console.WriteLine("[Regression] Material export checks passed.");
    }

    static void CheckSeparateExports(string root)
    {
        string directory = Path.Combine(root, "SeparateExports");
        var savedAt = new DateTime(2026, 10, 3, 15, 4, 5);
        const string name = "XR教材データ-20261003150405";
        var export = new ScenarioExport { projectName = "First lesson" };
        export.objects.Add(new PlacementExportObject { id = "object", typeId = "Imported/Used" });
        string firstDirectory = RuntimeExportPathUtility.CreateTeachingMaterialDirectory(directory, savedAt);
        string firstJson = Path.Combine(firstDirectory, Path.GetFileName(firstDirectory) + ".json");
        ScenarioModelBundle.Prepare(export, firstJson);
        ExportFileWriter.WriteAllTextWithBackup(firstJson, JsonUtility.ToJson(export, true));
        TeachingMaterialArchive.Write(export, firstJson);
        byte[] firstJsonBytes = File.ReadAllBytes(firstJson);
        byte[] firstZipBytes = File.ReadAllBytes(TeachingMaterialArchive.GetPath(firstJson));

        export.projectName = "Second lesson";
        string secondDirectory = RuntimeExportPathUtility.CreateTeachingMaterialDirectory(directory, savedAt);
        string secondJson = Path.Combine(secondDirectory, Path.GetFileName(secondDirectory) + ".json");
        ScenarioModelBundle.Prepare(export, secondJson);
        ExportFileWriter.WriteAllTextWithBackup(secondJson, JsonUtility.ToJson(export, true));
        TeachingMaterialArchive.Write(export, secondJson);
        Require(Path.GetFileName(firstDirectory) == name && Path.GetFileName(secondDirectory) == name + "-2",
            "Timestamp naming gives same-second exports separate folders");
        Require(Path.GetFileName(firstJson) == name + ".json" &&
            Path.GetFileName(TeachingMaterialArchive.GetPath(firstJson)) == name + ".zip",
            "JSON and ZIP use the requested export name");
        Require(File.ReadAllBytes(firstJson).SequenceEqual(firstJsonBytes) &&
            File.ReadAllBytes(TeachingMaterialArchive.GetPath(firstJson)).SequenceEqual(firstZipBytes),
            "Re-export preserves the first JSON and complete archive unchanged");
        using (var zip = ZipFile.OpenRead(TeachingMaterialArchive.GetPath(secondJson)))
        {
            using var reader = new StreamReader(zip.GetEntry(Path.GetFileName(secondJson)).Open());
            var archived = JsonUtility.FromJson<ScenarioExport>(reader.ReadToEnd());
            Require(archived.projectName == "Second lesson" && zip.GetEntry(archived.models[0].uri) != null &&
                File.Exists(Path.Combine(secondDirectory, archived.models[0].uri)),
                "The second export has its own JSON and resolvable model assets");
        }
        Require(!File.Exists(firstJson + ".bak") && !File.Exists(secondJson + ".bak"),
            "Separate exports do not rely on overwrite backups");

        string occupiedFile = Path.Combine(directory, name + "-3");
        File.WriteAllText(occupiedFile, "keep this file");
        Require(Path.GetFileName(RuntimeExportPathUtility.CreateTeachingMaterialDirectory(directory, savedAt)) == name + "-4" &&
            File.ReadAllText(occupiedFile) == "keep this file", "Existing files are never reused as export folders");
        var parallelDirectories = new string[12];
        System.Threading.Tasks.Parallel.For(0, parallelDirectories.Length, index =>
            parallelDirectories[index] = RuntimeExportPathUtility.CreateTeachingMaterialDirectory(directory, savedAt));
        Require(parallelDirectories.Distinct().Count() == parallelDirectories.Length &&
            parallelDirectories.All(Directory.Exists) && Directory.GetDirectories(directory, ".XR-*.tmp").Length == 0,
            "Concurrent exports reserve distinct folders without leaving staging folders");
    }

    static void CheckModelNames(string root, string source)
    {
        var names = new[] { "Socket_Wrench", "Socket_Wrench", "Socket_Wrench-2", "socket_wrench", "工具/レンチ:*", "CON", "" };
        var expected = new[] { "Socket_Wrench", "Socket_Wrench-2", "Socket_Wrench-2-2", "socket_wrench-3", "工具_レンチ_", "_CON", "model" };
        var export = new ScenarioExport();
        for (int index = 0; index < names.Length; index++)
        {
            string typeId = "Imported/NameCheck" + index;
            ImportedModelStore.Save(source, typeId, names[index], "");
            export.objects.Add(new PlacementExportObject { id = "named-" + index, typeId = typeId });
        }
        string directory = Path.Combine(root, "ModelNameChecks");
        string jsonPath = Path.Combine(directory, "XR教材データ-20261003150405.json");
        ScenarioModelBundle.Prepare(export, jsonPath);
        Require(export.models.Select(model => model.uri.Split('/')[1]).SequenceEqual(expected),
            "Model folders use import names and disambiguate duplicate, sanitized and case-insensitive names");
        Require(export.models.All(model => model.uri.StartsWith("XR教材データ-20261003150405_assets/", StringComparison.Ordinal) &&
            File.Exists(Path.Combine(directory, model.uri))), "Named model URIs resolve without opaque folder IDs");
        TeachingMaterialArchive.Write(export, jsonPath);
        using (var zip = ZipFile.OpenRead(TeachingMaterialArchive.GetPath(jsonPath)))
        {
            Require(export.models.All(model => zip.GetEntry(model.uri) != null &&
                zip.GetEntry(model.uri.Substring(0, model.uri.LastIndexOf('/') + 1) + "textures/body.png") != null),
                "ZIP preserves the named model folders and referenced texture structure");
        }
    }

    static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }

    static void Reject(Action action, string message)
    {
        try { action(); }
        catch (IOException) { return; }
        throw new Exception(message);
    }
}
