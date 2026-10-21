using System;
using System.IO;

// Startup protection is durable state, independent of later operation statuses.
public sealed class EditorProjectRecoveryProtection
{
    public event Action Changed;
    public string Error { get; private set; }
    // Set when a recovery slot was moved to a protected lesson ("name 復旧") by the latest successful TryPreserve.
    public string PreservedPath { get; private set; }
    public string PreservedOriginalName { get; private set; }
    public bool HasFailure => !string.IsNullOrEmpty(Error);
    public string WarningMessage => HasFailure
        ? "前回終了時の復旧データを、別の教材として残せませんでした。\n" +
          "以前の復旧データはそのまま保持しています。今の教材は保存できます。\n" +
          "「再試行」するか、一覧上部の「復旧して開く」「破棄」を選べます。\n詳細: " + Error
        : null;

    public bool TryPreserve(out string message)
    {
        bool succeeded = EditorProjectStore.TryPreserveRecoveryAtStartup(out bool preserved, out string error, out string preservedPath, out string originalName);
        PreservedPath = preserved ? preservedPath : null;
        PreservedOriginalName = preserved ? originalName : null;
        SetError(succeeded ? null : string.IsNullOrWhiteSpace(error) ? "復旧データを保護できません。" : error);
        message = succeeded
            ? preserved ? "前回の復旧データを別の教材として残しました。" : "残す必要のある復旧データはありません。"
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
