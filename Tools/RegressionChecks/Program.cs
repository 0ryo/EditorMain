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
        if (args.Length == 1 && args[0] == "--export-workflow")
        {
            RunExportWorkflowChecks();
            return;
        }
        bool keepGoing = args.Length == 1 && args[0] == "--keep-going";
        bool groupsPassed = RunGroup("FBX", FbxReferenceChecks.Run, keepGoing);
        groupsPassed &= RunGroup("Authoring", AuthoringFeatureChecks.Run, keepGoing);
        var root = Path.Combine(Path.GetTempPath(), "SkillSyncChecks-" + Guid.NewGuid().ToString("N"));
        Application.persistentDataPath = root;
        Directory.CreateDirectory(root);
        try
        {
            groupsPassed &= RunGroup("Material export", () => MaterialExportChecks.Run(root), keepGoing);
            groupsPassed &= RunGroup("Export workflow", () => ExportWorkflowChecks.Run(root), keepGoing);
            CheckRecoveryOwnership();
            CheckProjectReadValidation();
            CheckReplacementRollback();
            var project = new EditorProjectFile { projectName = "Regression" };
            project.objects.Add(new EditorProjectObject { id = "root", typeId = "box", editorGroupId = "group", parts = new() { new ModelPartState { id = "part", editorGroupId = "group", localPosition = new Vector3(1,2,3) } } });
            string path = EditorProjectStore.Save(project, project.projectName);
            string originalSaved = File.ReadAllText(path);
            var recovery = new EditorProjectFile { projectName = project.projectName };
            recovery.objects.Add(new EditorProjectObject { id = "unsaved", typeId = "box", position = new Vector3(3, 2, 1) });
            EditorProjectStore.SaveRecovery(recovery);
            string recoveryBytes = File.ReadAllText(EditorProjectStore.RecoveryPath);
            Check(EditorProjectStore.TryPreserveRecoveryAtStartup(out var preserved, out _) && preserved, "Startup recovery succeeds");
            Check(File.ReadAllText(path) == originalSaved, "Startup recovery must not overwrite a same-name saved lesson");
            string preservedPath = Path.Combine(EditorProjectStore.ProjectsDirectory, "Regression 復旧" + EditorProjectStore.FileSuffix);
            Check(File.ReadAllText(preservedPath) == recoveryBytes && !File.Exists(EditorProjectStore.RecoveryPath), "Recovery is moved without rewriting data");
            Check(EditorProjectStore.TryLoad(preservedPath, out var recovered, out _) &&
                recovered.objects.Count == 1 && recovered.objects[0].id == "unsaved" &&
                Math.Abs(recovered.objects[0].position.x - 3f) < 0.00001f,
                "Preserved recovery restores object identity and position");
            recovered.objects[0].position.x = 4f;
            EditorProjectStore.SaveExisting(recovered, preservedPath);
            Check(File.ReadAllText(path) == originalSaved &&
                EditorProjectStore.TryLoad(preservedPath, out recovered, out _) &&
                Math.Abs(recovered.objects[0].position.x - 4f) < 0.00001f,
                "Editing a preserved lesson saves to its own file");
            recoveryBytes = File.ReadAllText(preservedPath);
            Check(EditorProjectStore.TryPreserveRecoveryAtStartup(out preserved, out _) && !preserved,
                "Repeated startup without recovery is a no-op");
            EditorProjectStore.SaveRecovery(recovery);
            Check(EditorProjectStore.TryPreserveRecoveryAtStartup(out preserved, out _) && preserved &&
                File.Exists(Path.Combine(EditorProjectStore.ProjectsDirectory, "Regression 復旧 (2)" + EditorProjectStore.FileSuffix)) &&
                File.ReadAllText(preservedPath) == recoveryBytes, "Repeated recovery uses a unique name");
            recovery.projectName = "Never saved";
            EditorProjectStore.SaveRecovery(recovery);
            string blockedPath = Path.Combine(EditorProjectStore.ProjectsDirectory, "Never saved 復旧" + EditorProjectStore.FileSuffix);
            Directory.CreateDirectory(blockedPath);
            recoveryBytes = File.ReadAllText(EditorProjectStore.RecoveryPath);
            Check(!EditorProjectStore.TryPreserveRecoveryAtStartup(out preserved, out var preserveError) &&
                !preserved && !string.IsNullOrEmpty(preserveError) && File.ReadAllText(EditorProjectStore.RecoveryPath) == recoveryBytes,
                "Failed preservation retains recovery for retry");
            Directory.Delete(blockedPath);
            Check(EditorProjectStore.TryPreserveRecoveryAtStartup(out preserved, out _) && preserved &&
                File.ReadAllText(blockedPath) == recoveryBytes, "Retry preserves a never-saved lesson");
            File.WriteAllText(EditorProjectStore.RecoveryPath, "{broken");
            Check(!EditorProjectStore.TryPreserveRecoveryAtStartup(out preserved, out _) && !preserved &&
                File.ReadAllText(EditorProjectStore.RecoveryPath) == "{broken", "Corrupt recovery is retained and reported");
            EditorProjectStore.DeleteRecovery(out _);
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
        if (!groupsPassed) Environment.ExitCode = 1;
    }

    static bool RunGroup(string name, Action run, bool keepGoing)
    {
        try { run(); return true; }
        catch (Exception ex) when (keepGoing)
        {
            Console.Error.WriteLine($"[Regression] {name} group FAILED: {ex}");
            return false;
        }
    }

    static void RunExportWorkflowChecks()
    {
        var root = Path.Combine(Path.GetTempPath(), "SkillSyncExportChecks-" + Guid.NewGuid().ToString("N"));
        Application.persistentDataPath = root;
        Directory.CreateDirectory(root);
        try { ExportWorkflowChecks.Run(root); }
        finally { Directory.Delete(root, true); }
    }
    static void CheckReplacementRollback()
    {
        bool oldActive = true, newActive = false, disposed = false;
        var original = new Curriculum { projectName = "Original" };
        var current = original;
        var stack = new CommandStack();
        var counter = new Counter();
        stack.Execute(new Change(counter, 5));
        try
        {
            EditorProjectReplacement.Run(
                () => { oldActive = false; newActive = true; current = new Curriculum(); throw new InvalidOperationException("Injected graph/UI failure"); },
                () => { disposed = true; stack.Clear(); },
                () => newActive = false,
                () => { oldActive = true; current = original; });
        }
        catch (InvalidOperationException) { }
        Check(oldActive && !newActive && ReferenceEquals(current, original) && !disposed,
            "Failed replacement restores original data and retains old instances");
        Check(stack.Undo() && counter.value == 0, "Failed replacement retains working Undo history");
        bool recoveredSelection = false;
        try
        {
            EditorProjectReplacement.Run(() => throw new InvalidOperationException("apply"),
                () => disposed = true,
                () => throw new InvalidOperationException("one rollback listener failed"),
                () => recoveredSelection = true);
            Check(false, "Rollback failure must be reported");
        }
        catch (AggregateException ex)
        {
            Check(ex.InnerExceptions.Count == 2 && recoveredSelection && !disposed,
                "Rollback continues after a failed restoration and reports both errors");
        }
        int commits = 0, restores = 0;
        EditorProjectReplacement.Run(() => { oldActive = false; newActive = true; },
            () => { disposed = true; commits++; stack.Clear(); }, () => restores++);
        Check(commits == 1 && restores == 0 && disposed && newActive && !oldActive,
            "Successful replacement commits once without rollback");
        Check(!stack.CanUndo && !stack.CanRedo, "Successful replacement clears old history");
    }

    static void CheckProjectReadValidation()
    {
        Check(!EditorProjectMigration.TryRead("{\"schemaVersion\":6}", out var invalid, out var error) &&
            invalid == null && !string.IsNullOrEmpty(error), "Incomplete current project must not become an empty lesson");
        string[] rejected =
        {
            "null",
            "{\"schemaVersion\":5}",
            "{\"schemaVersion\":6,\"objects\":[],\"curriculum\":null}",
            "{\"schemaVersion\":6,\"objects\":null,\"curriculum\":{\"nodes\":[],\"edges\":[]}}",
            "{\"schemaVersion\":6,\"objects\":[],\"curriculum\":{\"edges\":[]}}",
            "{\"schemaVersion\":6,\"objects\":[],\"curriculum\":{\"nodes\":[],\"edges\":null}}",
            CurrentJson("[null]", "[]", "[]"),
            CurrentJson("[]", "[null]", "[]"),
            CurrentJson("[]", "[]", "[null]"),
            CurrentJson("[]", "[{\"nodeType\":999}]", "[]"),
            CurrentJson("[]", "[]", "[{\"edgeType\":999}]"),
            CurrentJson("[{\"position\":{\"x\":1e39}}]", "[]", "[]"),
            CurrentJson("[{\"rotation\":{\"w\":1e39}}]", "[]", "[]"),
            CurrentJson("[{\"scale\":{\"y\":-1e39}}]", "[]", "[]"),
            CurrentJson("[{\"parts\":[null]}]", "[]", "[]"),
            CurrentJson("[{\"parts\":[{\"localPosition\":{\"z\":1e39}}]}]", "[]", "[]"),
            CurrentJson("[]", "[{\"nodeType\":3,\"condition\":{\"parameters\":[{\"numberValue\":1e39}]}}]", "[]"),
            CurrentJson("[]", "[{\"nodeType\":3,\"condition\":{\"parameters\":[null]}}]", "[]"),
            CurrentJson("[]", "[{\"nodeId\":\"duplicate\",\"nodeType\":2},{\"nodeId\":\"duplicate\",\"nodeType\":2}]", "[]"),
            CurrentJson("[]", "[{\"nodeType\":2}]", "[]"),
            CurrentJson("[{\"id\":\"shared\"},{\"id\":\"shared\"}]", "[{\"nodeId\":\"condition\",\"nodeType\":3,\"condition\":{\"objectAId\":\"shared\"}}]", "[]"),
            CurrentJson("[{\"id\":\"shared\"},{\"parts\":[{\"id\":\"shared\"}]}]", "[{\"nodeId\":\"condition\",\"nodeType\":3,\"condition\":{\"objectBId\":\"shared\"}}]", "[]")
        };
        foreach (string json in rejected)
            Check(!EditorProjectMigration.TryRead(json, out invalid, out error) && invalid == null && !string.IsNullOrEmpty(error),
                "Invalid project is rejected before normalization: " + json);
        for (int version = 1; version <= EditorProjectFile.CurrentSchemaVersion; version++)
        {
            var fixture = new EditorProjectFile { schemaVersion = version, projectName = "移行確認" };
            fixture.objects.Add(new EditorProjectObject { id = "keep", typeId = "box", position = new Vector3(1.25f, -2, 3) });
            fixture.curriculum.nodes.Add(new ScenarioNode { nodeId = "unfinished", nodeType = ScenarioNodeType.Step, step = new StepNodeData { title = "未接続の手順" } });
            Check(EditorProjectMigration.TryRead(JsonUtility.ToJson(fixture), out var read, out error) &&
                read.schemaVersion == 6 && read.objects[0].id == "keep" && Math.Abs(read.objects[0].position.x - 1.25f) < 1e-5f &&
                read.curriculum.nodes[0].step.title == "未接続の手順", "Version migration preserves draft meaning: " + version);
        }
        Check(EditorProjectMigration.TryRead(CurrentJson("[]", "[]", "[]"), out _, out _), "Intentionally empty current lesson remains valid");
        Check(EditorProjectMigration.TryRead("{\"projectName\":\"Legacy\"}", out _, out _), "Unversioned legacy migration remains supported");
        Check(EditorProjectMigration.TryRead(CurrentJson("[]", "[{\"nodeId\":\"draft\",\"nodeType\":3,\"condition\":{\"objectAId\":\"missing\"}}]", "[]"), out _, out _),
            "Unresolved draft references remain available for editing");
        Check(EditorProjectMigration.TryRead(CurrentJson("[]", "[{\"nodeId\":\"draft\",\"nodeType\":2}]", "[{\"fromNodeId\":\"missing\",\"toNodeId\":\"draft\",\"edgeType\":0}]"), out _, out _),
            "Unresolved draft edge references remain available for editing");
        Check(EditorProjectMigration.TryRead(CurrentJson("[{\"id\":\"unused\"},{\"id\":\"unused\"}]", "[]", "[]"), out _, out _),
            "Unreferenced duplicate placement IDs remain eligible for deterministic repair");
        string path = Path.Combine(EditorProjectStore.ProjectsDirectory, "invalid-input" + EditorProjectStore.FileSuffix);
        File.WriteAllText(path, rejected[3]);
        Check(!EditorProjectStore.TryLoad(path, out invalid, out error) && invalid == null && File.ReadAllText(path) == rejected[3],
            "Store rejects invalid input without rewriting the source file");
    }

    static string CurrentJson(string objects, string nodes, string edges) =>
        "{\"schemaVersion\":6,\"objects\":" + objects + ",\"curriculum\":{\"nodes\":" + nodes + ",\"edges\":" + edges + "}}";

    static void CheckRecoveryOwnership()
    {
        var session = new EditorProjectRecoverySession();
        var previous = new EditorProjectFile { projectName = "Protected" };
        previous.objects.Add(new EditorProjectObject { id = "previous", typeId = "box" });
        EditorProjectStore.SaveRecovery(previous);
        string previousBytes = File.ReadAllText(EditorProjectStore.RecoveryPath);
        string blocked = Path.Combine(EditorProjectStore.ProjectsDirectory, "Protected 復旧" + EditorProjectStore.FileSuffix);
        Directory.CreateDirectory(blocked);
        Check(!EditorProjectStore.TryPreserveRecoveryAtStartup(out _, out _), "Reproduce failed startup preservation");
        Check(session.DeleteOwned(out _) && File.Exists(EditorProjectStore.RecoveryPath) &&
            File.ReadAllText(EditorProjectStore.RecoveryPath) == previousBytes,
            "Save/load/new cleanup must retain recovery from a previous session");
        var current = new EditorProjectFile { projectName = "Current" };
        current.objects.Add(new EditorProjectObject { id = "current", typeId = "box" });
        Reject(() => session.Save(current), "Autosave refuses to overwrite unpreserved recovery");
        Check(File.ReadAllText(EditorProjectStore.RecoveryPath) == previousBytes,
            "Failed autosave retains previous recovery bytes");
        string regular = EditorProjectStore.Save(current, current.projectName);
        Check(session.DeleteOwned(out _) && File.Exists(regular) &&
            File.ReadAllText(EditorProjectStore.RecoveryPath) == previousBytes,
            "Normal save remains possible without deleting protected recovery");
        Directory.Delete(blocked);
        session.Save(current);
        Check(File.ReadAllText(blocked) == previousBytes &&
            EditorProjectStore.TryLoad(EditorProjectStore.RecoveryPath, out var loaded, out _) && loaded.objects[0].id == "current",
            "Retry preserves previous lesson before saving current lesson");
        int count = EditorProjectStore.ListProjects().Count;
        current.objects[0].position.x = 7f;
        session.Save(current);
        Check(EditorProjectStore.ListProjects().Count == count &&
            EditorProjectStore.TryLoad(EditorProjectStore.RecoveryPath, out loaded, out _) && loaded.objects[0].position.x == 7f,
            "Repeated owned autosave updates one slot without creating extra lessons");
        Check(session.DeleteOwned(out _) && !File.Exists(EditorProjectStore.RecoveryPath) &&
            File.ReadAllText(blocked) == previousBytes, "Owned cleanup removes only current recovery");
        File.WriteAllText(EditorProjectStore.RecoveryPath, "{broken");
        Reject(() => session.Save(current), "Corrupt unowned recovery blocks autosave");
        Check(session.DeleteOwned(out _) && File.ReadAllText(EditorProjectStore.RecoveryPath) == "{broken",
            "Cleanup retains corrupt unowned recovery");
        // Explicit user discard releases the slot; ordinary cleanup never does.
        Check(EditorProjectStore.DeleteRecovery(out _), "Explicit discard accepts corrupt recovery");
        session.Save(current);
        EditorProjectStore.SaveRecovery(previous);
        previousBytes = File.ReadAllText(EditorProjectStore.RecoveryPath);
        Check(session.DeleteOwned(out _) && File.ReadAllText(EditorProjectStore.RecoveryPath) == previousBytes,
            "Externally replaced recovery is no longer owned");
        var restarted = new EditorProjectRecoverySession();
        Check(restarted.DeleteOwned(out _) && File.ReadAllText(EditorProjectStore.RecoveryPath) == previousBytes,
            "New session does not inherit ownership");
        EditorProjectStore.DeleteRecovery(out _);
        session.Save(current);
        string lockedBytes = File.ReadAllText(EditorProjectStore.RecoveryPath);
        using (var locked = new FileStream(EditorProjectStore.RecoveryPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            Check(!session.DeleteOwned(out var cleanupError) && !string.IsNullOrEmpty(cleanupError),
                "Locked owned recovery reports cleanup failure");
        }
        session.Save(previous);
        string lockedPreserved = Path.Combine(EditorProjectStore.ProjectsDirectory, "Current 復旧" + EditorProjectStore.FileSuffix);
        Check(File.ReadAllText(lockedPreserved) == lockedBytes,
            "Failed cleanup relinquishes ownership before a different lesson autosaves");
        EditorProjectStore.DeleteRecovery(out _);
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
