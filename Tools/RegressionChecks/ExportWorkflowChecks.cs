using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using UnityEngine;

static class ExportWorkflowChecks
{
    public static void Run(string root)
    {
        int checks = 0, saves = 0, validations = 0, exports = 0;
        void Require(bool value, string message)
        {
            if (!value) throw new Exception(message);
            checks++;
        }

        var draft = new EditorProjectFile { projectName = "Export workflow" };
        draft.objects.Add(new EditorProjectObject { id = "keep", typeId = "box", position = new Vector3(1, 2, 3) });
        string projectPath = EditorProjectStore.Save(draft, draft.projectName);
        EditorProjectStore.SaveExisting(draft, projectPath);
        string original = File.ReadAllText(projectPath);
        string backup = File.ReadAllText(projectPath + ".bak");
        EditorProjectStore.SaveRecovery(draft);
        string recovery = File.ReadAllText(EditorProjectStore.RecoveryPath);
        string exportRoot = Path.Combine(root, "WorkflowExports");
        Directory.CreateDirectory(exportRoot);
        string oldJson = Path.Combine(exportRoot, "previous.json");
        var distribution = new ScenarioExport { projectName = "Previous distribution" };
        TeachingMaterialArchive.Write(distribution, oldJson);
        ExportFileWriter.WriteAllTextWithBackup(oldJson, JsonUtility.ToJson(distribution, true));
        byte[] oldJsonBytes = File.ReadAllBytes(oldJson);
        byte[] oldZipBytes = File.ReadAllBytes(TeachingMaterialArchive.GetPath(oldJson));
        draft.objects[0].position.x = 9;
        draft.curriculum.nodes.Add(new ScenarioNode { nodeId = "draft", nodeType = ScenarioNodeType.Step,
            step = new StepNodeData { title = "編集中の手順" } });
        string curriculum = JsonUtility.ToJson(draft.curriculum);

        bool Save(out string message)
        {
            saves++;
            try
            {
                EditorProjectStore.SaveExisting(draft, projectPath);
                message = "保存しました: " + draft.projectName;
                return true;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                message = "保存できません: " + ex.Message;
                return false;
            }
        }
        bool Validate() { validations++; return true; }
        string Export()
        {
            exports++;
            string directory = RuntimeExportPathUtility.CreateTeachingMaterialDirectory(exportRoot, new DateTime(2026, 10, 3));
            string path = Path.Combine(directory, Path.GetFileName(directory) + ".json");
            TeachingMaterialArchive.Write(distribution, path);
            ExportFileWriter.WriteAllTextWithBackup(path, JsonUtility.ToJson(distribution, true));
            return path;
        }
        void RequireOldDistribution()
        {
            Require(File.ReadAllBytes(oldJson).SequenceEqual(oldJsonBytes) &&
                File.ReadAllBytes(TeachingMaterialArchive.GetPath(oldJson)).SequenceEqual(oldZipBytes),
                "Failed/retried workflow must preserve the previous distribution bytes");
        }

        // Real I/O failure, including under root: the writer's tmp filename is a directory.
        string blocked = projectPath + ".tmp";
        Directory.CreateDirectory(blocked);
        for (int attempt = 1; attempt <= 2; attempt++)
        {
            var failed = TeachingMaterialExportWorkflow.Run(Save, Validate, Export);
            Require(failed.outcome == TeachingMaterialExportWorkflow.Outcome.SaveFailed && failed.exportPath == null,
                "Editable save failure must stop a valid graph from exporting");
            Require(saves == attempt && validations == 0 && exports == 0 && Directory.GetDirectories(exportRoot).Length == 0,
                "No graph validation, archive write or export directory creation may follow failed saving");
            Require(failed.message.Contains("保存に失敗") && failed.message.Contains("書き出しを中止") &&
                failed.message.Contains("保存できません:") && failed.message.Contains("再試行") &&
                !failed.message.Contains("出力しました") && !failed.message.Contains("保存済み"),
                "Save failure must retain its reason and retry guidance without a success claim");
            Require(File.ReadAllText(projectPath) == original && File.ReadAllText(projectPath + ".bak") == backup &&
                File.ReadAllText(EditorProjectStore.RecoveryPath) == recovery && Directory.Exists(blocked),
                "Failed save must retain the original project, backup and recovery data");
            Require(draft.objects[0].position.x == 9 && JsonUtility.ToJson(draft.curriculum) == curriculum,
                "Failed export must retain authored placement and graph data for retry");
            RequireOldDistribution();
        }
        Directory.Delete(blocked);

        var succeeded = TeachingMaterialExportWorkflow.Run(Save, Validate, Export);
        Require(succeeded.outcome == TeachingMaterialExportWorkflow.Outcome.Exported && saves == 3 && validations == 1 && exports == 1,
            "Retry after removing the save failure must save, validate and export once");
        Require(succeeded.message.Contains("再編集用データを保存しました") && succeeded.message.Contains("教材を出力しました") &&
            succeeded.message.Contains(succeeded.exportPath) && succeeded.message.Contains(TeachingMaterialArchive.GetPath(succeeded.exportPath)),
            "Success must identify both editable save and distribution paths");
        Require(EditorProjectStore.TryLoad(projectPath, out var saved, out _) && saved.objects[0].position.x == 9 &&
            saved.curriculum.nodes[0].step.title == "編集中の手順" && File.ReadAllText(projectPath + ".bak") == original,
            "Successful retry must persist authored data and retain the previous project backup");
        using (var zip = ZipFile.OpenRead(TeachingMaterialArchive.GetPath(succeeded.exportPath)))
        {
            using var reader = new StreamReader(zip.GetEntry(Path.GetFileName(succeeded.exportPath)).Open());
            Require(reader.ReadToEnd() == File.ReadAllText(succeeded.exportPath),
                "Successful workflow must emit the exact standalone JSON inside its XR archive");
        }
        RequireOldDistribution();
        byte[] successfulJson = File.ReadAllBytes(succeeded.exportPath);
        byte[] successfulZip = File.ReadAllBytes(TeachingMaterialArchive.GetPath(succeeded.exportPath));

        draft.objects[0].position.x = 11;
        var invalid = TeachingMaterialExportWorkflow.Run(Save, () => { validations++; return false; }, Export);
        Require(invalid.outcome == TeachingMaterialExportWorkflow.Outcome.InvalidGraph && exports == 1 && invalid.exportPath == null &&
            invalid.message.Contains("保存済み") && invalid.message.Contains("エラーを修正"),
            "Invalid graph must keep the saved draft and give repair guidance without exporting");
        Require(EditorProjectStore.TryLoad(projectPath, out saved, out _) && saved.objects[0].position.x == 11,
            "Validation rejection must still preserve the latest draft");

        draft.objects[0].position.x = 13;
        distribution.prefabPackage = "missing.unitypackage";
        var archiveFailed = TeachingMaterialExportWorkflow.Run(Save, Validate, Export);
        Require(archiveFailed.outcome == TeachingMaterialExportWorkflow.Outcome.ExportFailed && archiveFailed.exception is IOException &&
            archiveFailed.exportPath == null && archiveFailed.message.Contains("保存済み") &&
            archiveFailed.message.Contains("書き出しに失敗") && archiveFailed.message.Contains(archiveFailed.exception.Message) &&
            archiveFailed.message.Contains("再試行") && !archiveFailed.message.Contains("出力しました"),
            "Archive failure must retain the exception and distinguish saved draft from failed distribution");
        Require(EditorProjectStore.TryLoad(projectPath, out saved, out _) && saved.objects[0].position.x == 13,
            "Archive failure must not undo the successful editable save");
        RequireOldDistribution();
        distribution.prefabPackage = null;
        var archiveRetried = TeachingMaterialExportWorkflow.Run(Save, Validate, Export);
        Require(archiveRetried.outcome == TeachingMaterialExportWorkflow.Outcome.Exported &&
            archiveRetried.exportPath != succeeded.exportPath && File.Exists(TeachingMaterialArchive.GetPath(archiveRetried.exportPath)),
            "Retry after repairing an archive dependency must produce a separate distribution");
        Require(File.ReadAllBytes(succeeded.exportPath).SequenceEqual(successfulJson) &&
            File.ReadAllBytes(TeachingMaterialArchive.GetPath(succeeded.exportPath)).SequenceEqual(successfulZip),
            "Archive failure and retry must preserve the earlier successful workflow output");
        Require(Directory.GetFiles(exportRoot, "*.tmp", SearchOption.AllDirectories).Length == 0,
            "Failed archive write must not leave temporary files");
        EditorProjectStore.DeleteRecovery(out _);
        Console.WriteLine($"[Regression] {checks} export workflow checks passed.");
    }
}
