using System;
using System.IO;
using System.Linq;
using UnityEngine;

// Problems are worded for the fixed UI (steps, completion conditions, parts, 3D screen) and point at where to fix them.
static class ValidationTextChecks
{
    static int checks;
    static void Check(bool result, string message)
    {
        if (!result) throw new Exception("Validation text: " + message);
        checks++;
    }

    public static void Run()
    {
        checks = 0;
        var previous = CommandService.I;
        try
        {
            CheckNoSteps();
            CheckStepProblems();
            CheckConditionProblems();
            CheckNoJargon();
            CheckExportFolderName();
            Console.WriteLine($"[Regression] {checks} validation text checks passed.");
        }
        finally { CommandService.I = previous; UnityEngine.Object.placed = Array.Empty<PlacedObject>(); }
    }

    static CurriculumGraphService Fixture(int steps)
    {
        CommandService.I = new CommandService();
        var graph = new CurriculumGraphService();
        for (int i = 0; i < steps; i++) Check(graph.TryInsertStepWithCondition(null, out _, out _, out _), "Create step with condition");
        UnityEngine.Object.placed = new[] { new PlacedObject { id = "a" }, new PlacedObject { id = "b" } };
        return graph;
    }

    static System.Collections.Generic.List<ValidationEntry> Entries(CurriculumGraphService graph, Func<string, bool> exists = null) =>
        ScenarioValidationText.BuildEntries(graph, graph.ValidateGraph(), exists ?? (id => id == "a" || id == "b"));

    static void CheckNoSteps()
    {
        var graph = Fixture(0);
        var validation = graph.ValidateGraph();
        Check(validation.errors.Count == 1 && validation.errors[0].code == "E-04", "An empty lesson has one error, not Start/End link errors");
        var entries = Entries(graph);
        Check(entries.Count == 1 && entries[0].stepId == null && entries[0].stepNumber == 0, "One entry, tied to no step");
        Check(entries[0].Message == "手順がまだありません。「＋ 手順を追加」から最初の手順を作ってください", "Empty lesson says there are no steps and what to press");
    }

    static void CheckStepProblems()
    {
        var graph = Fixture(2);
        var steps = graph.GetDisplayOrderedSteps();
        var firstCondition = graph.GetConditionNodesForStep(steps[0].nodeId)[0];
        var secondCondition = graph.GetConditionNodesForStep(steps[1].nodeId)[0];
        graph.UpdateConditionData(firstCondition.nodeId, "Set", d => { d.objectAId = "a"; d.objectBId = "b"; });
        graph.UpdateConditionData(secondCondition.nodeId, "Set", d => { d.objectAId = "a"; d.objectBId = "b"; });
        Check(Entries(graph).Count == 0 && graph.ValidateGraph().CanExport, "A complete lesson has no problems");

        // A step without any condition: pointed at the step and the add button.
        graph.TryRemoveNode(secondCondition.nodeId);
        var entries = Entries(graph);
        Check(entries.Count == 1 && entries[0].stepId == steps[1].nodeId && entries[0].stepNumber == 2 && entries[0].field == "conditions",
            "A step without conditions points at the step's add button");
        Check(entries[0].Line == "手順2：完了条件がありません" && entries[0].action.Contains("＋ 完了条件を追加"), "Missing conditions say what to press");

        // Two conditions with no targets are "unassigned", never "duplicates".
        graph.TryAddConditionToStep(steps[1].nodeId, out var added1, out _);
        graph.TryAddConditionToStep(steps[1].nodeId, out var added2, out _);
        entries = Entries(graph);
        Check(entries.Count == 2 && entries.All(e => e.stepNumber == 2 && e.field == "A"), "Empty twin conditions each ask for their targets");
        Check(entries.All(e => !e.Reason.Contains("重複")), "Empty twin conditions are not called duplicates");
        Check(entries[0].conditionId == added1.nodeId && entries[1].conditionId == added2.nodeId, "Problems follow the condition order");
        Check(entries[0].Line.StartsWith("手順2：完了条件1の") && entries[1].Line.StartsWith("手順2：完了条件2の"), "Several conditions are numbered");

        // Identical complete conditions are duplicates, reported on the later one.
        graph.UpdateConditionData(added1.nodeId, "Set", d => { d.objectAId = "a"; d.objectBId = "b"; });
        graph.UpdateConditionData(added2.nodeId, "Set", d => { d.objectAId = "a"; d.objectBId = "b"; });
        entries = Entries(graph);
        Check(entries.Count == 1 && entries[0].Reason.Contains("重複") && entries[0].conditionId == added2.nodeId, "Duplicates point at the later condition");

        // Fixing the first problem leaves the next one in order.
        graph.UpdateConditionData(firstCondition.nodeId, "Set", d => d.objectBId = null);
        entries = Entries(graph);
        Check(entries.Count == 2 && entries[0].stepNumber == 1 && entries[1].stepNumber == 2, "Problems are ordered by step");
        Check(entries[0].order < entries[1].order, "Order values increase with the step number");
    }

    static void CheckConditionProblems()
    {
        var graph = Fixture(1);
        var step = graph.GetDisplayOrderedSteps()[0];
        var condition = graph.GetConditionNodesForStep(step.nodeId)[0];
        graph.UpdateConditionData(condition.nodeId, "Set", d => { d.objectAId = "a"; d.objectBId = null; });
        var entry = Entries(graph).Single();
        Check(entry.field == "B" && entry.Line == "手順1：完了条件の「近づける先」が未設定です", "A missing second target names the field as the editor does");
        Check(entry.action.Contains("近づける先"), "The action names the same field");

        graph.UpdateConditionData(condition.nodeId, "Set", d => { d.objectAId = null; d.objectBId = "b"; });
        entry = Entries(graph).Single();
        Check(entry.field == "A" && entry.what == "「動かすもの」が未設定です", "A missing first target is the part to move");

        graph.UpdateConditionData(condition.nodeId, "Set", d => { d.objectAId = "a"; d.objectBId = "a"; });
        entry = Entries(graph).Single();
        Check(entry.field == "B" && entry.what.Contains("同じ部品"), "Same part twice is explained in part words");

        graph.UpdateConditionData(condition.nodeId, "Set", d => { d.objectAId = "a"; d.objectBId = "b"; });
        UnityEngine.Object.placed = new[] { new PlacedObject { id = "a" } };
        entry = Entries(graph, id => id == "a").Single();
        UnityEngine.Object.placed = new[] { new PlacedObject { id = "a" }, new PlacedObject { id = "b" } };
        Check(entry.field == "B" && entry.what == "「近づける先」の部品が3D画面にありません" && entry.action.Contains("元に戻す"),
            "A deleted part is said to be missing from the 3D screen and Undo is offered");

        graph.UpdateConditionData(condition.nodeId, "Set", d => SkillSyncConditionEditing.ChangeType(d, ConditionTypeCatalog.Grabbed, graph.curriculum.rules));
        graph.UpdateConditionData(condition.nodeId, "Set", d => d.objectAId = null);
        entry = Entries(graph).Single();
        Check(entry.field == "A" && entry.Line == "手順1：完了条件の対象が未設定です", "A single-target type just says 対象");
    }

    static void CheckNoJargon()
    {
        string[] forbidden = { "ノード", "ステップ", "オブジェクト", "Undo", "E-0", "E-1", "JSON", "typeId", "スタート", "エンド" };
        foreach (var code in new[] { "E-01", "E-02", "E-03", "E-04", "E-05", "E-06", "E-07", "E-08", "E-09", "E-10", "E-11", "E-12", "W-01", "W-02" })
        {
            string text = ScenarioValidationText.GetFriendlyMessage(new GraphValidationIssue(code, "internal"));
            Check(!string.IsNullOrEmpty(text) && forbidden.All(word => !text.Contains(word)), "Issue text avoids jargon: " + code + " " + text);
        }
        Check(!ScenarioValidationText.BuildExportBlockedMessage(Fixture(0).ValidateGraph()).Contains("保存できません"), "Blocked export is not called a save failure");
    }

    static void CheckExportFolderName()
    {
        var at = new DateTime(2026, 10, 21, 15, 30, 59);
        Check(RuntimeExportPathUtility.BuildTeachingMaterialName("ジャッキアップ手順", at) == "ジャッキアップ手順-20261021-1530", "Folder is named after the lesson and the minute");
        Check(RuntimeExportPathUtility.BuildTeachingMaterialName("a/b:c?", at) == "a_b_c_-20261021-1530", "Forbidden characters use the usual replacement");
        Check(RuntimeExportPathUtility.BuildTeachingMaterialName("  ", at).StartsWith("教材-") || RuntimeExportPathUtility.BuildTeachingMaterialName("  ", at).StartsWith("XR教材データ-"), "A blank name still gives a usable folder");
        string root = Path.Combine(Path.GetTempPath(), "SkillSyncNameChecks-" + Guid.NewGuid().ToString("N"));
        try
        {
            string first = RuntimeExportPathUtility.CreateTeachingMaterialDirectory(root, at, "教材A");
            string second = RuntimeExportPathUtility.CreateTeachingMaterialDirectory(root, at, "教材A");
            Check(Path.GetFileName(first) == "教材A-20261021-1530" && Path.GetFileName(second) == "教材A-20261021-1530-2" && Directory.Exists(first),
                "A second export of the same lesson in the same minute gets -2 and never reuses a folder");
            Check(Directory.GetDirectories(root).Length == 2, "No staging folder is left behind");
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
}
