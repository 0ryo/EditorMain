using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

public static class EditorProjectStore
{
    public const string FileSuffix = ".skillsync.json";
    const string RecoveryDirectoryName = "Recovery";
    const string RecoveryFileName = "autosave" + FileSuffix;

    public static string ProjectsDirectory =>
        Path.Combine(Application.persistentDataPath, "Projects");
    public static string RecoveryPath =>
        Path.Combine(ProjectsDirectory, RecoveryDirectoryName, RecoveryFileName);

    public static string Save(EditorProjectFile project, string projectName)
    {
        if (project == null) throw new ArgumentNullException(nameof(project));

        string safeName = ExportFileNameUtility.SanitizeProjectName(projectName, "VRCourseEditor");
        project.schemaVersion = EditorProjectFile.CurrentSchemaVersion;
        project.projectName = string.IsNullOrWhiteSpace(projectName) ? safeName : projectName.Trim();
        project.savedAtUtc = DateTime.UtcNow.ToString("O");
        EditorProjectMigration.Normalize(project);

        string path = Path.Combine(ProjectsDirectory, safeName + FileSuffix);
        ExportFileWriter.WriteAllTextWithBackup(path, JsonUtility.ToJson(project, true));
        return path;
    }

    public static bool TryLoad(string path, out EditorProjectFile project, out string error)
    {
        project = null;
        error = null;

        try
        {
            string resolvedPath = Path.GetFullPath(path);
            if (!File.Exists(resolvedPath))
            {
                error = "プロジェクトファイルが見つかりません。";
                return false;
            }

            return EditorProjectMigration.TryRead(File.ReadAllText(resolvedPath), out project, out error);
        }
        catch (Exception ex)
        {
            error = "プロジェクトを読み込めません: " + ex.Message;
            return false;
        }
    }

    public static string SaveRecovery(EditorProjectFile project)
    {
        if (project == null) throw new ArgumentNullException(nameof(project));

        project.schemaVersion = EditorProjectFile.CurrentSchemaVersion;
        project.savedAtUtc = DateTime.UtcNow.ToString("O");
        EditorProjectMigration.Normalize(project);
        ExportFileWriter.WriteAllTextWithBackup(RecoveryPath, JsonUtility.ToJson(project, true));
        return RecoveryPath;
    }

    public static bool TryGetRecovery(out EditorProjectFileInfo info)
    {
        info = null;
        try
        {
            if (!File.Exists(RecoveryPath)) return false;

            var file = new FileInfo(RecoveryPath);
            string displayName = "自動保存データ";
            if (TryLoad(file.FullName, out var project, out _) && project != null &&
                !string.IsNullOrWhiteSpace(project.projectName))
            {
                displayName = project.projectName;
            }

            info = new EditorProjectFileInfo(file.FullName, displayName, file.LastWriteTimeUtc);
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[EditorProjectStore] 自動保存データを確認できません: " + ex.Message);
            return false;
        }
    }

    public static bool DeleteRecovery(out string error)
    {
        error = null;
        try
        {
            if (File.Exists(RecoveryPath)) File.Delete(RecoveryPath);
            string backupPath = RecoveryPath + ".bak";
            if (File.Exists(backupPath)) File.Delete(backupPath);
            string tempPath = RecoveryPath + ".tmp";
            if (File.Exists(tempPath)) File.Delete(tempPath);
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    public static IReadOnlyList<EditorProjectFileInfo> ListProjects()
    {
        try
        {
            if (!Directory.Exists(ProjectsDirectory)) return Array.Empty<EditorProjectFileInfo>();

            return Directory.GetFiles(ProjectsDirectory, "*" + FileSuffix, SearchOption.TopDirectoryOnly)
                .Select(path => new FileInfo(path))
                .OrderByDescending(info => info.LastWriteTimeUtc)
                .Select(info => new EditorProjectFileInfo(
                    info.FullName,
                    RemoveSuffix(info.Name),
                    info.LastWriteTimeUtc))
                .ToList();
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[EditorProjectStore] 一覧を取得できません: " + ex.Message);
            return Array.Empty<EditorProjectFileInfo>();
        }
    }

    static string RemoveSuffix(string fileName)
    {
        return fileName.EndsWith(FileSuffix, StringComparison.OrdinalIgnoreCase)
            ? fileName.Substring(0, fileName.Length - FileSuffix.Length)
            : Path.GetFileNameWithoutExtension(fileName);
    }
}

public sealed class EditorProjectFileInfo
{
    public string Path { get; }
    public string DisplayName { get; }
    public DateTime LastWriteTimeUtc { get; }

    public EditorProjectFileInfo(string path, string displayName, DateTime lastWriteTimeUtc)
    {
        Path = path;
        DisplayName = displayName;
        LastWriteTimeUtc = lastWriteTimeUtc;
    }
}
