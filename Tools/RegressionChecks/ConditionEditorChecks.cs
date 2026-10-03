using System;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;

static class ConditionEditorChecks
{
    static int checks;
    static void Check(bool result, string message)
    {
        if (!result) throw new Exception("Condition editor: " + message);
        checks++;
    }

    public static void Run()
    {
        checks = 0;
        string root = Path.Combine(Path.GetTempPath(), "SkillSyncConditionChecks-" + Guid.NewGuid().ToString("N"));
        string previousPath = Application.persistentDataPath;
        var previousCommand = CommandService.I;
        var previousObjects = UnityEngine.Object.placed;
        var previousCulture = CultureInfo.CurrentCulture;
        Directory.CreateDirectory(root);
        try
        {
            Application.persistentDataPath = root;
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            UnityEngine.Object.placed = new[] { new PlacedObject { id = "a" }, new PlacedObject { id = "b" } };
            var graph = new CurriculumGraphService();
            CommandService.I = new CommandService();
            graph.EnsureGraphInitialized();
            Check(graph.TryAddStepAtEnd(out var step, out _), "Add a connected step");
            Check(graph.TryAddConditionToStep(step.nodeId, out var node, out _), "Add a bound condition");
            string id = node.nodeId;
            ConditionNodeData Current() => graph.FindNode(id).condition;
            graph.UpdateConditionData(id, "Set targets", data => { data.objectAId = "a"; data.objectBId = "b"; });
            graph.UpdateConditionData(id, "Set distance", data => SkillSyncConditionEditing.TrySetValue(data, 0, "25"));
            graph.UpdateConditionData(id, "Set hold", data => SkillSyncConditionEditing.TrySetValue(data, 1, "3"));

            Check(ConditionTypeCatalog.Definitions.Select(d => d.id).SequenceEqual(new[] { "Proximity", "SnapHold", "Push", "Pull", "Grabbed", "Turn" }), "Exactly six new choices in the requested order");
            foreach (var definition in ConditionTypeCatalog.Definitions)
            {
                graph.UpdateConditionData(id, "Set type", data => SkillSyncConditionEditing.ChangeType(data, definition.id, graph.curriculum.rules));
                Check(Current().type == definition.id, "Type selection: " + definition.id);
                Check(SkillSyncConditionEditing.TargetsValid(Current(), value => value == "a" || value == "b"), "Configured targets: " + definition.id);
                Check(SkillSyncConditionEditing.Summary(Current()).StartsWith(definition.label + "："), "Type-specific summary: " + definition.id);
                Check(SkillSyncConditionEditing.SupportsPcTrial(Current()) == (definition.id == "Proximity" || definition.id == "SnapHold"), "PC trial capability: " + definition.id);
                Check(SkillSyncConditionEditing.Parameter(Current(), definition.parameters.Count) == null, "Unused parameter slot: " + definition.id);
                if (!definition.requiresObjectB)
                {
                    graph.UpdateConditionData(id, "Retain missing inactive B", data => data.objectBId = "missing");
                    Check(SkillSyncConditionEditing.TargetsValid(Current(), value => value == "a"), "Single target needs no B: " + definition.id);
                }
                Check(graph.ValidateGraph().CanExport, "Production validation: " + definition.id);
                var export = graph.BuildScenarioExport().requiredActions.Single().conditions.Single();
                Check(export.type == definition.id && export.aObjectId == "a" &&
                    export.bObjectId == (definition.requiresObjectB ? "b" : null), "Export targets: " + definition.id);
                Check(export.parameters.Select(p => p.key).SequenceEqual(definition.parameters.Select(p => p.key)), "Export includes only active parameters: " + definition.id);

                var project = new EditorProjectFile { projectName = "Conditions", curriculum = graph.curriculum };
                project.objects.Add(new EditorProjectObject { id = "a", typeId = "box" });
                project.objects.Add(new EditorProjectObject { id = "b", typeId = "box" });
                string path = EditorProjectStore.Save(project, project.projectName);
                Check(EditorProjectStore.TryLoad(path, out var restored, out _), "Project reload: " + definition.id);
                var savedCondition = restored.curriculum.nodes.Single(n => n.nodeId == id).condition;
                Check(JsonUtility.ToJson(savedCondition) == JsonUtility.ToJson(Current()), "Reload preserves type, targets and all retained values: " + definition.id);
                // Rebuild the graph from reloaded data and verify the real export path again.
                var loadedGraph = new CurriculumGraphService { curriculum = restored.curriculum };
                Check(JsonUtility.ToJson(loadedGraph.BuildScenarioExport()) == JsonUtility.ToJson(graph.BuildScenarioExport()), "Reload preserves export: " + definition.id);
            }

            Check(SkillSyncConditionEditing.Value(Current(), 0) == "90", "Turn uses angle default, not retained distance");
            graph.UpdateConditionData(id, "Edit turn angle", data => SkillSyncConditionEditing.TrySetValue(data, 0, "270.5"));
            Check(ConditionTypeCatalog.GetNumber(Current(), ConditionTypeCatalog.AngleKey) == 270.5f, "Angle is stored in degrees");
            Check(CommandService.I.Stack.Undo() && SkillSyncConditionEditing.Value(Current(), 0) == "90", "Parameter Undo");
            Check(CommandService.I.Stack.Redo() && SkillSyncConditionEditing.Value(Current(), 0) == "270.5", "Parameter Redo");
            string snapshot = JsonUtility.ToJson(Current());
            foreach (string invalid in new[] { "NaN", "Infinity", "-1", "0", "36001", "text" })
                Check(!SkillSyncConditionEditing.TrySetValue(Current(), 0, invalid) && JsonUtility.ToJson(Current()) == snapshot, "Reject invalid angle without mutation: " + invalid);
            graph.UpdateConditionData(id, "Back to hold", data => SkillSyncConditionEditing.ChangeType(data, "SnapHold", graph.curriculum.rules));
            Check(Current().objectBId == "missing" && SkillSyncConditionEditing.Value(Current(), 0) == "25" && SkillSyncConditionEditing.Value(Current(), 1) == "3", "Switching restores retained B, distance and hold");
            Check(!SkillSyncConditionEditing.TargetsValid(Current(), value => value == "a" || value == "b") && !graph.ValidateGraph().CanExport, "Two-target condition still rejects missing B");
            Check(CommandService.I.Stack.Undo() && Current().type == "Turn" && SkillSyncConditionEditing.Value(Current(), 0) == "270.5", "Type Undo restores previous type and parameter");
            Check(CommandService.I.Stack.Redo() && Current().type == "SnapHold", "Type Redo");
            graph.UpdateConditionData(id, "Restore B", data => data.objectBId = "b");
            graph.UpdateConditionData(id, "Edit centimetres", data => SkillSyncConditionEditing.TrySetValue(data, 0, "12.5"));
            Check(Math.Abs(ConditionTypeCatalog.GetNumber(Current(), ConditionTypeCatalog.DistanceKey) - .125f) < .00001f, "Centimetres are stored in metres");
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            Check(SkillSyncConditionEditing.TrySetValue(Current(), 0, "25,5") && SkillSyncConditionEditing.Value(Current(), 0) == "25,5", "Locale-aware decimal input and display");
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            foreach (string legacy in new[] { "Separation", "RotationMatch", "Released", "CustomType" })
            {
                var condition = new ConditionNodeData { type = legacy, objectAId = "a", objectBId = "b" };
                ConditionTypeCatalog.Normalize(condition);
                string before = JsonUtility.ToJson(condition);
                SkillSyncConditionEditing.Parameter(condition, 0);
                SkillSyncConditionEditing.Summary(condition);
                Check(JsonUtility.ToJson(condition) == before, "Displaying legacy/unknown type does not migrate it: " + legacy);
                Check(!SkillSyncConditionEditing.SupportsPcTrial(condition), "Legacy/unknown PC trial is not claimed: " + legacy);
                Check(!SkillSyncConditionEditing.ChangeType(condition, legacy, null), "Legacy type is not a new choice: " + legacy);
            }
            Check(!SkillSyncConditionEditing.TargetsValid(new ConditionNodeData { objectAId = "a", objectBId = "a" }, _ => true), "Pair rejects the same target");
            Check(!SkillSyncConditionEditing.TargetsValid(new ConditionNodeData { type = "Push", objectAId = "missing" }, _ => false), "Single target rejects missing A");
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
            Application.persistentDataPath = previousPath;
            CommandService.I = previousCommand;
            UnityEngine.Object.placed = previousObjects;
            Directory.Delete(root, true);
        }
        Console.WriteLine($"[Condition editor] {checks} type / persistence / Undo / export checks passed.");
    }
}
