using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

public static class EditorProjectStore
{
    public const string FileSuffix = ".skillsync.json";

    public static string ProjectsDirectory =>
        Path.Combine(Application.persistentDataPath, "Projects");

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
