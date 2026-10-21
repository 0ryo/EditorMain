using System;
using System.Collections.Generic;
using System.Linq;

// One problem as the fixed UI shows it: where it is, what is wrong and what to do next.
internal sealed class ValidationEntry
{
    public string code, stepId, conditionId;
    // Which part of the condition editor to open: "A", "B", "type", "conditions"; null when the step or condition itself is the place.
    public string field;
    // 1-based position in the step list; 0 when the problem belongs to no single step.
    public int stepNumber;
    // "完了条件2" (null when the problem is about the step or the whole lesson) and what is wrong with it.
    public string subject, what;
    public string action;
    // Position in the list (step, then condition), for ordering and for finding "the next problem".
    public int order;

    public string Reason => string.IsNullOrEmpty(subject) ? what : subject + "の" + what;
    public string Line => stepNumber > 0 ? "手順" + stepNumber + "：" + Reason : Reason;
    public string Message => string.IsNullOrEmpty(action) ? Line : Line + "。" + action;
}

internal static class ScenarioValidationText
{
    // Wording for code that only sees an issue (the dormant node graph UI). The fixed UI uses BuildEntries.
    static readonly Dictionary<string, string> ErrorMessages = new Dictionary<string, string>
    {
        { "E-01", "手順の順番の情報に問題があります。手順を一度入れ替えてください" },
        { "E-02", "手順の順番の情報に問題があります。手順を一度入れ替えてください" },
        { "E-03", "手順の順番の情報に問題があります。手順を一度入れ替えてください" },
        { "E-04", "手順がまだないか、手順の順番の情報に問題があります。手順を追加するか、一度入れ替えてください" },
        { "E-05", "手順の順番の情報に問題があります。手順を一度入れ替えてください" },
        { "E-06", "完了条件がない、または多すぎる、重複している手順があります。手順ごとに完了条件を確認してください" },
        { "E-07", "どの手順にも属していない完了条件があります。その完了条件を削除してください" },
        { "E-08", "部品が選ばれていない完了条件があります。部品を選んでください" },
        { "E-09", "「動かすもの」と「近づける先」に同じ部品が選ばれている完了条件があります。どちらかを変えてください" },
        { "E-10", "選んだ部品が3D画面にない完了条件があります。部品を選び直すか、削除を元に戻してください" },
        { "E-11", "教材のデータに問題があります。教材を保存して開き直してください" },
        { "E-12", "対応していない種類の完了条件があります。種類を選び直してください" },
    };

    static readonly Dictionary<string, string> WarningMessages = new Dictionary<string, string>
    {
        { "W-01", "完了条件の数が上限に達している手順があります" },
        { "W-02", "複数の手順で同じ部品が「動かすもの」に使われています" },
    };

    public static string GetFriendlyMessage(GraphValidationIssue issue)
    {
        if (issue == null) return string.Empty;
        if (ErrorMessages.TryGetValue(issue.code, out var errorMessage)) return errorMessage;
        if (WarningMessages.TryGetValue(issue.code, out var warningMessage)) return warningMessage;
        return issue.message;
    }

    // The errors as a list ordered like the step list. partExists tells whether a part is still on the 3D screen (null: unknown).
    public static List<ValidationEntry> BuildEntries(CurriculumGraphService graph, GraphValidationResult validation, Func<string, bool> partExists = null)
    {
        var entries = new List<ValidationEntry>();
        if (graph == null || validation == null || validation.errors.Count == 0) return entries;
        // No steps at all: Start/End links and the like are internal, so there is exactly one thing to say.
        if (graph.GetNodes(ScenarioNodeType.Step).Count == 0)
        {
            entries.Add(new ValidationEntry
            {
                code = "E-04", what = "手順がまだありません",
                action = "「＋ 手順を追加」から最初の手順を作ってください",
            });
            return entries;
        }

        var steps = graph.GetDisplayOrderedSteps();
        var seen = new HashSet<string>();
        foreach (var issue in validation.errors)
        {
            var node = graph.FindNode(issue.nodeId);
            string stepId = node?.nodeType == ScenarioNodeType.Step ? node.nodeId :
                node?.nodeType == ScenarioNodeType.Condition ? graph.GetConditionBoundStepNodeId(node.nodeId) : null;
            var conditions = string.IsNullOrEmpty(stepId) ? new List<ScenarioNode>() : graph.GetConditionNodesForStep(stepId);
            var entry = new ValidationEntry
            {
                code = issue.code, stepId = stepId, stepNumber = steps.FindIndex(s => s.nodeId == stepId) + 1,
            };
            int conditionIndex = node?.nodeType == ScenarioNodeType.Condition ? conditions.FindIndex(c => c.nodeId == node.nodeId) : -1;
            if (node?.nodeType == ScenarioNodeType.Condition && conditionIndex >= 0)
            {
                entry.conditionId = node.nodeId;
                entry.subject = conditions.Count > 1 ? "完了条件" + (conditionIndex + 1) : "完了条件";
            }
            if (!Describe(entry, issue, node, conditions, graph, partExists)) continue;
            if (!seen.Add(entry.stepId + "|" + entry.conditionId + "|" + entry.Reason)) continue;
            int at = conditions.FindIndex(c => c.nodeId == entry.conditionId);
            entry.order = (entry.stepNumber == 0 ? 100000 : entry.stepNumber) * 1000 + Math.Max(0, at);
            entries.Add(entry);
        }

        if (entries.Count == 0)
            entries.Add(new ValidationEntry { code = "E-11", what = "教材のデータに問題があります", action = "教材を保存して、開き直してください" });
        return entries.OrderBy(e => e.order).ToList();
    }

    static bool Describe(ValidationEntry entry, GraphValidationIssue issue, ScenarioNode node, List<ScenarioNode> conditions,
        CurriculumGraphService graph, Func<string, bool> partExists)
    {
        var data = node?.nodeType == ScenarioNodeType.Condition ? node.condition : null;
        string typeA = SkillSyncConditionEditing.TargetLabelA(data?.type), typeB = SkillSyncConditionEditing.TargetLabelB(data?.type);
        bool twoTargets = data != null && ConditionTypeCatalog.RequiresObjectB(data.type);
        switch (issue.code)
        {
            case "E-01": case "E-02": case "E-03": case "E-04": case "E-05":
                entry.stepId = null; entry.stepNumber = 0;
                entry.what = "手順の順番の情報に問題があります";
                entry.action = "「↑ 上へ」「↓ 下へ」で手順を一度入れ替えてください";
                return true;
            case "E-06":
                if (conditions.Count == 0)
                {
                    entry.what = "完了条件がありません"; entry.field = "conditions";
                    entry.action = "「＋ 完了条件を追加」で、この手順が終わる条件を追加してください";
                }
                else if (conditions.Count > graph.GetMaxConditionsPerStep())
                {
                    entry.what = "完了条件が多すぎます（最大" + graph.GetMaxConditionsPerStep() + "件）";
                    entry.action = "いらない完了条件を削除してください";
                }
                else
                {
                    // The later one of two identical conditions is the one to change or remove.
                    var configured = conditions.Where(c => graph.IsConditionConfigured(c)).ToList();
                    var seenKeys = new HashSet<string>();
                    ScenarioNode duplicate = null;
                    foreach (var c in configured) if (!seenKeys.Add(DuplicateKey(c)) && duplicate == null) duplicate = c;
                    if (duplicate == null) return false;
                    entry.conditionId = duplicate.nodeId;
                    entry.what = "同じ内容の完了条件が重複しています";
                    entry.action = "どちらかを削除するか、部品や種類を変えてください";
                }
                return true;
            case "E-07":
                entry.stepId = null; entry.stepNumber = 0; entry.conditionId = null; entry.subject = null;
                entry.what = "どの手順にも属していない完了条件があります";
                entry.action = "手順を選んで、完了条件を追加し直してください";
                return true;
            case "E-08":
            {
                if (data == null) return false;
                bool missingA = string.IsNullOrWhiteSpace(data.objectAId), missingB = twoTargets && string.IsNullOrWhiteSpace(data.objectBId);
                entry.field = missingA ? "A" : "B";
                entry.what = missingA && missingB ? Quote(typeA) + "と" + Quote(typeB) + "が未設定です" : (missingA ? Quote(typeA) : Quote(typeB)) + "が未設定です";
                entry.action = missingA && missingB ? "右の欄で、それぞれ部品を選んでください" : "右の「" + (missingA ? typeA : typeB) + "」の欄で、部品を選んでください";
                return true;
            }
            case "E-09":
                entry.field = "B";
                entry.what = Quote(typeA) + "と" + Quote(typeB) + "に同じ部品が選ばれています";
                entry.action = "どちらかを別の部品に変えてください";
                return true;
            case "E-10":
            {
                if (data == null) return false;
                bool missingA = partExists != null && !partExists(data.objectAId);
                bool missingB = partExists != null && twoTargets && !partExists(data.objectBId);
                entry.field = missingA || !missingB ? "A" : "B";
                string names = missingA && missingB ? Quote(typeA) + "と" + Quote(typeB) : Quote(missingB && !missingA ? typeB : typeA);
                entry.what = names + "の部品が3D画面にありません";
                entry.action = "「↶ 元に戻す」で部品の削除を取り消すか、部品を選び直してください";
                return true;
            }
            case "E-12":
                entry.field = "type";
                entry.what = "種類に対応していません";
                entry.action = "完了条件の種類を選び直してください";
                return true;
            default:
                entry.stepId = null; entry.stepNumber = 0; entry.conditionId = null; entry.subject = null;
                entry.what = "教材のデータに問題があります";
                entry.action = "教材を保存して、開き直してください。直らないときは手順や完了条件を追加し直してください";
                return true;
        }
    }

    // A target field named in a message: the generic single target reads as a plain word.
    static string Quote(string label) => label == "対象" ? label : "「" + label + "」";

    static string DuplicateKey(ScenarioNode c) =>
        c.condition.type + "|" + c.condition.objectAId + "|" +
        (ConditionTypeCatalog.RequiresObjectB(c.condition.type) ? c.condition.objectBId : string.Empty) + "|" +
        ConditionTypeCatalog.BuildParameterSignature(c.condition);

    public static string BuildUiSignature(GraphValidationResult validation)
    {
        if (validation == null) return string.Empty;
        return string.Join(";", validation.errors.Select(issue =>
                   $"E|{issue.code}|{issue.nodeId}|{issue.message}")) +
               "#" +
               string.Join(";", validation.warnings.Select(issue =>
                   $"W|{issue.code}|{issue.nodeId}|{issue.message}"));
    }

    public static string BuildExportBlockedMessage(GraphValidationResult validation)
    {
        if (validation == null || validation.errors.Count == 0) return string.Empty;

        var firstError = validation.errors[0];
        string friendly = ErrorMessages.TryGetValue(firstError.code, out var msg) ? msg : firstError.message;

        return validation.errors.Count == 1
            ? $"書き出せません: {friendly}"
            : $"書き出せません: {friendly}（ほか {validation.errors.Count - 1} 件）";
    }

    public static string BuildStatusMessage(GraphValidationResult validation)
    {
        if (!validation.CanExport)
        {
            return $"要修正: {validation.errors.Count}件";
        }

        if (validation.warnings.Count > 0)
        {
            var firstWarn = validation.warnings[0];
            string friendly = WarningMessages.TryGetValue(firstWarn.code, out var warnMsg) ? warnMsg : firstWarn.message;
            return validation.warnings.Count == 1
                ? $"注意: {friendly}"
                : $"注意: {friendly}（ほか {validation.warnings.Count - 1} 件）";
        }

        return "書き出せます";
    }
}
