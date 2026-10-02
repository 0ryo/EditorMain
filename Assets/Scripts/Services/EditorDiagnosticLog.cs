using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

public sealed class EditorDiagnosticLog : MonoBehaviour
{
    const int MaxEntryCount = 500;
    static readonly Queue<string> Entries = new();
    static EditorDiagnosticLog instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void InitializeBeforeSceneLoad()
    {
        if (instance != null) return;
        Entries.Clear();
        var gameObject = new GameObject("EditorDiagnosticLog");
        gameObject.hideFlags = HideFlags.HideInHierarchy;
        DontDestroyOnLoad(gameObject);
        instance = gameObject.AddComponent<EditorDiagnosticLog>();
    }

    void OnEnable() => Application.logMessageReceived += Capture;
    void OnDisable() => Application.logMessageReceived -= Capture;

    static void Capture(string condition, string stackTrace, LogType type)
    {
        if (type == LogType.Log) return;

        var entry = $"{DateTime.UtcNow:O} [{type}] {condition}";
        if (!string.IsNullOrWhiteSpace(stackTrace)) entry += "\n" + stackTrace;
        Entries.Enqueue(entry);
        while (Entries.Count > MaxEntryCount) Entries.Dequeue();
    }

    public static bool TryExportLatest(out string path, out string error)
    {
        path = null;
        error = null;
        try
        {
            string directory = Path.Combine(Application.persistentDataPath, "Diagnostics");
            Directory.CreateDirectory(directory);
            path = Path.Combine(directory, $"SkillSync-diagnostics-{DateTime.UtcNow:yyyyMMdd-HHmmss-fff}.txt");

            var report = new StringBuilder();
            report.AppendLine("SkillSync Editor diagnostic report");
            report.AppendLine($"Exported UTC: {DateTime.UtcNow:O}");
            report.AppendLine($"Application: {Application.version}");
            report.AppendLine($"Unity: {Application.unityVersion}");
            report.AppendLine($"Platform: {Application.platform}");
            report.AppendLine($"Device: {SystemInfo.deviceModel}");
            report.AppendLine($"OS: {SystemInfo.operatingSystem}");
            report.AppendLine("Included entries: Unity warnings, errors, assertions, and exceptions; informational messages and user status text are excluded.");
            report.AppendLine($"Recent warning/error entries (maximum {MaxEntryCount}): {Entries.Count}");
            report.AppendLine();
            foreach (var entry in Entries) report.AppendLine(entry);

            File.WriteAllText(path, report.ToString(), new UTF8Encoding(false));
            return true;
        }
        catch (Exception ex)
        {
            path = null;
            error = ex.Message;
            return false;
        }
    }
}
