using System;

// Keep persistence, validation and distribution ordered without depending on Unity UI.
public static class TeachingMaterialExportWorkflow
{
    public delegate bool SaveProject(out string message);

    public enum Outcome { SaveFailed, InvalidGraph, ExportFailed, Exported }

    public sealed class Result
    {
        public readonly Outcome outcome;
        public readonly string message;
        public readonly string exportPath;
        public readonly Exception exception;

        internal Result(Outcome outcome, string message, string exportPath = null, Exception exception = null)
        {
            this.outcome = outcome;
            this.message = message;
            this.exportPath = exportPath;
            this.exception = exception;
        }
    }

    public static Result Run(SaveProject save, Func<bool> canExport, Func<string> export)
    {
        // A distribution archive cannot stand in for the editable project backup.
        // Stop before validation/export so no later success can hide this failure.
        if (!save(out string saveMessage))
            return new Result(Outcome.SaveFailed,
                "教材の保存に失敗したため、書き出しを中止しました。" +
                "保存先の空き容量と権限を確認し、「保存」または「教材を書き出す」を再試行してください。理由: " + saveMessage);

        if (!canExport())
            return new Result(Outcome.InvalidGraph,
                "教材は保存済みです。手順・完了条件の問題を直してから、「教材を書き出す」を再試行してください。");

        try
        {
            string path = export();
            return new Result(Outcome.Exported,
                "教材を保存しました。書き出しました: " + path +
                " / XR配布用: " + TeachingMaterialArchive.GetPath(path), path);
        }
        catch (Exception ex)
        {
            return new Result(Outcome.ExportFailed,
                "教材は保存済みです。書き出しに失敗しました: " + ex.Message +
                " 書き出し先と空き容量を確認し、「教材を書き出す」を再試行してください。", exception: ex);
        }
    }
}
