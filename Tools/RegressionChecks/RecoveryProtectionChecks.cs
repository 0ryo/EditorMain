using System;
using System.IO;

static class RecoveryProtectionChecks
{
    static int checks;

    public static void Run()
    {
        var previous = new EditorProjectFile { projectName = "Startup warning" };
        string original = EditorProjectStore.Save(previous, previous.projectName);
        string originalBytes = File.ReadAllText(original);
        previous.objects.Add(new EditorProjectObject { id = "unsaved", typeId = "box" });
        EditorProjectStore.SaveRecovery(previous);
        string recoveryBytes = File.ReadAllText(EditorProjectStore.RecoveryPath);
        string blocked = Path.Combine(EditorProjectStore.ProjectsDirectory,
            previous.projectName + " 復旧" + EditorProjectStore.FileSuffix);
        Directory.CreateDirectory(blocked);

        var protection = new EditorProjectRecoveryProtection();
        Check(!protection.TryPreserve(out var startupMessage) && protection.HasFailure,
            "Startup preservation failure is retained before any UI subscribes");
        int notifications = 0;
        protection.Changed += () => notifications++;
        Check(protection.WarningMessage == startupMessage && !string.IsNullOrEmpty(protection.Error),
            "Late UI can read the warning with recovery actions and failure details");
        Check(startupMessage.Contains("通常保存") && startupMessage.Contains("復元") && startupMessage.Contains("破棄"),
            "Warning explains available next actions");
        Check(!protection.TryPreserve(out _) && notifications == 0,
            "Unchanged retry failure does not generate duplicate notifications");
        Check(File.ReadAllText(original) == originalBytes && File.ReadAllText(EditorProjectStore.RecoveryPath) == recoveryBytes,
            "Failed startup and retry leave original lesson and recovery unchanged");

        var current = new EditorProjectFile { projectName = "Normally saved during warning" };
        string savedCurrent = EditorProjectStore.Save(current, current.projectName);
        var session = new EditorProjectRecoverySession();
        Check(session.DeleteOwned(out _), "Unowned cleanup succeeds without deleting startup recovery");
        protection.RefreshResolution();
        Check(protection.HasFailure && notifications == 0 && File.Exists(savedCurrent) &&
            File.ReadAllText(EditorProjectStore.RecoveryPath) == recoveryBytes,
            "Normal save and cleanup retain warning while the startup recovery remains");

        Directory.Delete(blocked);
        Check(protection.TryPreserve(out _) && !protection.HasFailure && protection.WarningMessage == null && notifications == 1,
            "Successful retry clears warning and notifies the existing UI once");
        Check(File.ReadAllText(blocked) == recoveryBytes && File.ReadAllText(original) == originalBytes,
            "Successful retry preserves exact bytes under a separate lesson name");
        Check(protection.TryPreserve(out _) && notifications == 1,
            "Repeated resolution without recovery produces no extra notice");

        File.WriteAllText(EditorProjectStore.RecoveryPath, "{broken");
        Check(!protection.TryPreserve(out _) && protection.HasFailure && notifications == 2,
            "Malformed startup recovery is retained as a visible failure");
        Check(!protection.TryPreserve(out _) && notifications == 2 && File.ReadAllText(EditorProjectStore.RecoveryPath) == "{broken",
            "Retry never deletes malformed recovery or duplicates its warning");
        Check(EditorProjectStore.DeleteRecovery(out _), "Explicit discard removes malformed recovery");
        protection.RefreshResolution();
        protection.RefreshResolution();
        Check(!protection.HasFailure && notifications == 3,
            "Explicit discard resolves warning once without requiring restart");

        EditorProjectStore.SaveRecovery(previous);
        var cleanStartup = new EditorProjectRecoveryProtection();
        int cleanNotifications = 0;
        cleanStartup.Changed += () => cleanNotifications++;
        Check(cleanStartup.TryPreserve(out _) && !cleanStartup.HasFailure && cleanNotifications == 0 &&
            File.ReadAllText(original) == originalBytes,
            "Successful startup does not display a warning or overwrite the original lesson");
        Check(cleanStartup.TryPreserve(out _) && cleanNotifications == 0,
            "Startup without recovery remains silent");
        Console.WriteLine($"[Regression] {checks} recovery protection notification checks passed.");
    }

    static void Check(bool value, string label)
    {
        if (!value) throw new Exception(label);
        checks++;
    }
}
