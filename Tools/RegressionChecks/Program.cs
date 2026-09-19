using System;
using System.IO;
using UnityEngine;

static class Program
{
    static int checks;
    static void Main(string[] args)
    {
        if (args.Length == 2 && args[0] == "--fbx")
        {
            var document = FbxTextureReferences.ReadDocument(args[1]);
            var refs = document.bindings;
            Console.WriteLine("Blender: " + document.isBlender);
            foreach (var pair in document.materials)
                Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(new { name = pair.Key, metallic = pair.Value.Metallic, smoothness = pair.Value.Smoothness, color = pair.Value.properties.TryGetValue("DiffuseColor", out var color) ? color : null }));
            int existing = 0;
            foreach (var reference in refs)
                if (File.Exists(reference.file) || File.Exists(reference.file.Normalize(System.Text.NormalizationForm.FormC))) existing++;
            Console.WriteLine($"FBX: {refs.Count} bindings; {existing} original files exist");
            return;
        }
        FbxReferenceChecks.Run();
        AuthoringFeatureChecks.Run();
        var root = Path.Combine(Path.GetTempPath(), "SkillSyncChecks-" + Guid.NewGuid().ToString("N"));
        Application.persistentDataPath = root;
        Directory.CreateDirectory(root);
        try
        {
            var project = new EditorProjectFile { projectName = "Regression" };
            project.objects.Add(new EditorProjectObject { id = "root", typeId = "box", editorGroupId = "group", parts = new() { new ModelPartState { id = "part", editorGroupId = "group", localPosition = new Vector3(1,2,3) } } });
            string path = EditorProjectStore.Save(project, project.projectName);
            Check(EditorProjectStore.TryLoad(path, out var loaded, out _) && loaded.objects[0].editorGroupId == "group" && loaded.objects[0].parts[0].localPosition.y == 2, "Save/load preserves groups and part transforms");
            string copy = EditorProjectStore.Duplicate(path, false);
            Check(copy != path && File.Exists(copy), "Duplicate preserves original");
            string template = EditorProjectStore.Duplicate(path, true);
            Check(File.Exists(template) && Path.GetDirectoryName(template) == EditorProjectStore.TemplatesDirectory, "Template is separate");
            string archived = EditorProjectStore.Archive(copy);
            Check(!File.Exists(copy) && File.Exists(archived), "Archive removes active copy");
            string restored = EditorProjectStore.RestoreArchived(archived);
            Check(File.Exists(restored) && File.Exists(path), "Restore preserves original");
            Check(!EditorProjectMigration.TryRead("{broken", out _, out _), "Corrupt JSON is rejected");
            Check(!EditorProjectMigration.TryRead("{\"schemaVersion\":999}", out _, out _), "Future version is rejected");
            Check(EditorProjectMigration.TryRead("{\"schemaVersion\":1,\"projectName\":\"Old\"}", out loaded, out _) && loaded.schemaVersion == 6, "Legacy migration");
            string model = Path.Combine(root, "model.gltf");
            File.WriteAllText(model, "{\"buffers\":[{\"uri\":\"buffer.bin\"}]}");
            Reject(() => ImportedModelStore.ValidateInput(model, 4096), "Missing dependency is rejected");
            File.WriteAllBytes(Path.Combine(root, "buffer.bin"), new byte[512]);
            ImportedModelStore.ValidateInput(model, 4096);
            Check(true, "Valid local dependency is accepted");
            Reject(() => ImportedModelStore.ValidateInput(model, 256), "Dependency size counts toward limit");
            File.WriteAllText(model, "{\"images\":[{\"uri\":\"../outside.png\"}]}");
            Reject(() => ImportedModelStore.ValidateInput(model, 4096), "Outside dependency is rejected");
            File.WriteAllText(model, "{\"images\":[{\"uri\":\"https://example.invalid/image.png\"}]}");
            Reject(() => ImportedModelStore.ValidateInput(model, 4096), "Remote dependency is rejected before load");
            string glb = Path.Combine(root, "invalid.glb");
            File.WriteAllBytes(glb, new byte[40]);
            Reject(() => ImportedModelStore.ValidateInput(glb, 4096), "Corrupt GLB header is rejected");
            string materialsFolder = Path.Combine(root, "Materials");
            Directory.CreateDirectory(materialsFolder);
            Directory.CreateDirectory(Path.Combine(materialsFolder, "Textures"));
            File.WriteAllText(Path.Combine(materialsFolder, "Paint.001.mat"), "candidate");
            File.WriteAllText(Path.Combine(materialsFolder, "Textures", "Body.png"), "candidate");
            File.WriteAllText(Path.Combine(materialsFolder, "Other.mat"), "candidate");
            File.WriteAllText(Path.Combine(materialsFolder, "ignore.txt"), "ignored");
            var search = MaterialFileSearch.Find(materialsFolder, new[] { "Paint.001 (Instance)", "Body.png" }, default);
            Check(search.matches.Count == 2 && search.nearby.Count == 1, "Recursive material search ranks named candidates and excludes unrelated extensions");
            Check(MaterialFileSearch.Normalize("Paint.001") == "Paint.001", "Material name suffix is preserved");
            bool cancelled = false;
            try { MaterialFileSearch.Find(materialsFolder, Array.Empty<string>(), new System.Threading.CancellationToken(true)); }
            catch (OperationCanceledException) { cancelled = true; }
            Check(cancelled, "Material search is cancellable");
            Reject(() => MaterialFileSearch.Find(Path.Combine(root, "missing-folder"), Array.Empty<string>(), default), "Missing search folder is reported");
            var stack = new CommandStack();
            var value = new Counter();
            Check(stack.Execute(new Change(value, 3)) && value.value == 3, "Execute");
            Check(stack.Undo() && value.value == 0, "Undo");
            Check(stack.Redo() && value.value == 3, "Redo");
            Check(!stack.ExecuteTransaction("rollback", new Change(value, 5), new Change(value, 9, true)) && value.value == 3, "Transaction failure rolls back");
            Check(stack.Undo() && value.value == 0, "Failed transaction preserves history");
        }
        finally
        {
            // Only the isolated, freshly-created test directory is removed.
            Directory.Delete(root, true);
        }
        Console.WriteLine($"[Regression] {checks} persistence / history checks passed.");
    }
    static void Reject(Action action, string label)
    {
        bool rejected = false;
        try { action(); } catch (IOException) { rejected = true; }
        Check(rejected, label);
    }
    static void Check(bool value,string label) { if(!value) throw new Exception(label); checks++; }
    sealed class Counter { public int value; }
    sealed class Change : IEditorCommand
    {
        readonly Counter target; readonly int before, after; readonly bool fail;
        public string Label => "Change";
        public Change(Counter target,int after,bool fail=false) { this.target=target;before=target.value;this.after=after;this.fail=fail; }
        public bool Do() { if(fail)return false;target.value=after;return true; }
        public bool Undo() { target.value=before;return true; }
    }
}
