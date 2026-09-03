using System.Collections.Generic;
using System.Linq;

internal static class ScenarioValidationText
{
    static readonly Dictionary<string, string> ErrorMessages = new Dictionary<string, string>
    {
        { "E-01", "スタートノードがありません" },
        { "E-02", "エンドノードがありません" },
        { "E-03", "スタートノードが次のノードに接続されていません" },
        { "E-04", "ステップが正しく繋がっていません" },
        { "E-05", "エンドノードに前のノードが接続されていません" },
        { "E-06", "手順が設定されていないステップがあります" },
        { "E-07", "どのステップにも紐付いていない手順があります" },
        { "E-08", "オブジェクトが選択されていない手順があります" },
        { "E-09", "AとBに同じオブジェクトが設定されている手順があります" },
        { "E-10", "参照オブジェクトがありません。別のオブジェクトへ差し替えるか、この条件を削除するか、オブジェクト削除をUndoしてください" },
        { "E-11", "データが不整合な状態です。編集をやり直してください" },
        { "E-12", "対応していない条件種別があります" },
    };

    static readonly Dictionary<string, string> WarningMessages = new Dictionary<string, string>
    {
        { "W-01", "条件数が設定上限に達しているステップがあります" },
        { "W-02", "複数のステップで同じオブジェクトAが使われています" },
    };

    public static string GetFriendlyMessage(GraphValidationIssue issue)
    {
        if (issue == null) return string.Empty;
        if (ErrorMessages.TryGetValue(issue.code, out var errorMessage)) return errorMessage;
        if (WarningMessages.TryGetValue(issue.code, out var warningMessage)) return warningMessage;
        return issue.message;
    }

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
            ? $"保存できません: {friendly}"
            : $"保存できません: {friendly}（他 {validation.errors.Count - 1} 件のエラー）";
    }

    public static string BuildStatusMessage(GraphValidationResult validation)
    {
        if (!validation.CanExport)
        {
            return $"\u8981\u78BA\u8A8D: {validation.errors.Count}\u4EF6";
        }

        if (validation.warnings.Count > 0)
        {
            var firstWarn = validation.warnings[0];
            string friendly = WarningMessages.TryGetValue(firstWarn.code, out var warnMsg) ? warnMsg : firstWarn.message;
            return validation.warnings.Count == 1
                ? $"警告: {friendly}"
                : $"警告: {friendly}（他 {validation.warnings.Count - 1} 件）";
        }

        return "JSON出力できます";
    }
}
