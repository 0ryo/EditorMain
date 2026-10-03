using System;
using System.IO;

// Startup protection is durable state, independent of later operation statuses.
public sealed class EditorProjectRecoveryProtection
{
    public event Action Changed;
    public string Error { get; private set; }
    public bool HasFailure => !string.IsNullOrEmpty(Error);
    public string WarningMessage => HasFailure
        ? "起動時の自動保存データを別教材として保護できません。\n" +
          "以前のデータを保持しています。現在の編集内容は通常保存できます。\n" +
          "保護の再試行、または保存済みタブの「復元」「破棄」を選べます。\n詳細: " + Error
        : null;

    public bool TryPreserve(out string message)
    {
        bool succeeded = EditorProjectStore.TryPreserveRecoveryAtStartup(out bool preserved, out string error);
        SetError(succeeded ? null : string.IsNullOrWhiteSpace(error) ? "復旧データを保護できません。" : error);
        message = succeeded
            ? preserved ? "以前の自動保存データを別教材として保護しました。" : "保護が必要な自動保存データはありません。"
            : WarningMessage;
        return succeeded;
    }

    public void RefreshResolution()
    {
        // A normal lesson save/load must not acknowledge an unpreserved slot.
        if (HasFailure && !File.Exists(EditorProjectStore.RecoveryPath)) SetError(null);
    }

    void SetError(string error)
    {
        if (string.Equals(Error, error, StringComparison.Ordinal)) return;
        Error = error;
        Changed?.Invoke();
    }
}
