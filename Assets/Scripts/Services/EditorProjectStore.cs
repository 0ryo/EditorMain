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

#if UNITY_EDITOR
    // Explicit sandbox for live Editor verification; never present in a Player build.
    public static string EditorVerificationProjectsDirectory { get; set; }
#endif
    public static string ProjectsDirectory =>
#if UNITY_EDITOR
        EditorVerificationProjectsDirectory ??
#endif
        Path.Combine(Application.persistentDataPath, "Projects");
    public static string RecoveryPath =>
        Path.Combine(ProjectsDirectory, RecoveryDirectoryName, RecoveryFileName);
    public static string TemplatesDirectory => Path.Combine(ProjectsDirectory, "Templates");
    static string DeletedProjectsDirectory => Path.Combine(ProjectsDirectory, "Trash", "Projects");
    static string DeletedTemplatesDirectory => Path.Combine(ProjectsDirectory, "Trash", "Templates");

    public static string Duplicate(string sourcePath, bool asTemplate)
    {
        string source = RequireLibraryPath(sourcePath, false);
        if (!TryLoad(source, out var project, out var error)) throw new InvalidOperationException(error);
        string name = project.projectName + (asTemplate ? " テンプレート" : " コピー");
        string directory = asTemplate ? TemplatesDirectory : ProjectsDirectory;
        Directory.CreateDirectory(directory);
        string pending = Path.Combine(directory, Guid.NewGuid().ToString("N") + ".pending");
        try
        {
            // Use a temporary file so an interrupted write never appears as a saved lesson.
            return MoveToAvailableName(pending, directory, name,
                candidate => WriteProject(project, RemoveSuffix(Path.GetFileName(candidate)), pending, false));
        }
        finally
        {
            if (File.Exists(pending)) File.Delete(pending);
            if (File.Exists(pending + ".tmp")) File.Delete(pending + ".tmp");
            if (File.Exists(pending + ".bak")) File.Delete(pending + ".bak");
        }
    }

    public static string Archive(string sourcePath)
    {
        string source = RequireLibraryPath(sourcePath, false);
        bool template = SameDirectory(source, TemplatesDirectory);
        return MoveToAvailableName(source, template ? DeletedTemplatesDirectory : DeletedProjectsDirectory,
            RemoveSuffix(Path.GetFileName(source)));
    }

    public static string RestoreArchived(string sourcePath)
    {
        string source = RequireLibraryPath(sourcePath, true);
        bool template = SameDirectory(source, DeletedTemplatesDirectory);
        return MoveToAvailableName(source, template ? TemplatesDirectory : ProjectsDirectory,
            RemoveSuffix(Path.GetFileName(source)));
    }

    static string RequireLibraryPath(string path, bool deleted)
    {
        string full = Path.GetFullPath(path);
        bool allowed = deleted
            ? SameDirectory(full, DeletedProjectsDirectory) || SameDirectory(full, DeletedTemplatesDirectory)
            : SameDirectory(full, ProjectsDirectory) || SameDirectory(full, TemplatesDirectory);
        if (!allowed || !full.EndsWith(FileSuffix, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("プロジェクト一覧のファイルを指定してください。");
        if (!File.Exists(full)) throw new FileNotFoundException("プロジェクトファイルが見つかりません。", full);
        return full;
    }

    static bool SameDirectory(string path, string directory) => string.Equals(
        Path.GetDirectoryName(Path.GetFullPath(path)), Path.GetFullPath(directory), StringComparison.OrdinalIgnoreCase);

    static string MoveToAvailableName(string source, string directory, string name, Action<string> prepare = null)
    {
        Directory.CreateDirectory(directory);
        string safeName = ExportFileNameUtility.SanitizeProjectName(name, "VRCourseEditor");
        for (int number = 1; number < 10000; number++)
        {
            string candidate = Path.Combine(directory, safeName + (number == 1 ? "" : " (" + number + ")") + FileSuffix);
            if (File.Exists(candidate)) continue;
            try { prepare?.Invoke(candidate); File.Move(source, candidate); return candidate; }
            catch (IOException) when (File.Exists(candidate)) { }
        }
        throw new IOException("同名の教材が多すぎます。別の名前で保存してください。");
    }

    public static IReadOnlyList<EditorProjectFileInfo> ListTemplates() => ListDirectory(TemplatesDirectory);
    public static IReadOnlyList<EditorProjectFileInfo> ListArchived() =>
        ListDirectory(DeletedProjectsDirectory).Concat(ListDirectory(DeletedTemplatesDirectory))
            .OrderByDescending(info => info.LastWriteTimeUtc).ToList();

    public static string Save(EditorProjectFile project, string projectName)
    {
        if (project == null) throw new ArgumentNullException(nameof(project));

        string safeName = ExportFileNameUtility.SanitizeProjectName(projectName, "VRCourseEditor");
        string path = Path.Combine(ProjectsDirectory, safeName + FileSuffix);
        return WriteProject(project, projectName, path, false);
    }

    public static string SaveAutomatic(EditorProjectFile project, string existingPath)
    {
        if (project == null) throw new ArgumentNullException(nameof(project));
        if (string.IsNullOrWhiteSpace(existingPath)) throw new ArgumentException("保存先が空です。", nameof(existingPath));

        string path = Path.GetFullPath(existingPath);
        return WriteProject(project, project.projectName, path, true);
    }

    public static string SaveExisting(EditorProjectFile project, string existingPath)
    {
        return WriteProject(project, project.projectName, RequireLibraryPath(existingPath, false), false);
    }

    static string WriteProject(EditorProjectFile project, string projectName, string path, bool automatic)
    {
        project.schemaVersion = EditorProjectFile.CurrentSchemaVersion;
        project.projectName = string.IsNullOrWhiteSpace(projectName)
            ? ExportFileNameUtility.SanitizeProjectName(projectName, "VRCourseEditor")
            : projectName.Trim();
        project.savedAtUtc = DateTime.UtcNow.ToString("O");
        project.lastSaveWasAutomatic = automatic;
        EditorProjectMigration.Normalize(project);

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
        return WriteProject(project, project.projectName, RecoveryPath, true);
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

    public static bool TryPromoteRecoveryForExistingProject(out bool promoted, out string error)
    {
        promoted = false;
        error = null;

        if (!TryGetRecovery(out _)) return true;
        if (!TryLoad(RecoveryPath, out var recovery, out error)) return false;

        string safeName = ExportFileNameUtility.SanitizeProjectName(recovery.projectName, "VRCourseEditor");
        string projectPath = Path.Combine(ProjectsDirectory, safeName + FileSuffix);
        if (!File.Exists(projectPath)) return true;

        try
        {
            SaveAutomatic(recovery, projectPath);
            if (!DeleteRecovery(out error)) return false;
            promoted = true;
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    public static IReadOnlyList<EditorProjectFileInfo> ListProjects()
        => ListDirectory(ProjectsDirectory);

    static IReadOnlyList<EditorProjectFileInfo> ListDirectory(string directory)
    {
        try
        {
            if (!Directory.Exists(directory)) return Array.Empty<EditorProjectFileInfo>();

            return Directory.GetFiles(directory, "*" + FileSuffix, SearchOption.TopDirectoryOnly)
                .Select(path => new FileInfo(path))
                .OrderByDescending(info => info.LastWriteTimeUtc)
                .Select(CreateProjectFileInfo)
                .ToList();
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[EditorProjectStore] 一覧を取得できません: " + ex.Message);
            return Array.Empty<EditorProjectFileInfo>();
        }
    }

    static EditorProjectFileInfo CreateProjectFileInfo(FileInfo info)
    {
        bool lastSaveWasAutomatic = false;
        if (TryLoad(info.FullName, out var project, out _) && project != null)
        {
            lastSaveWasAutomatic = project.lastSaveWasAutomatic;
        }

        return new EditorProjectFileInfo(
            info.FullName,
            RemoveSuffix(info.Name),
            info.LastWriteTimeUtc,
            lastSaveWasAutomatic);
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
    public bool LastSaveWasAutomatic { get; }

    public EditorProjectFileInfo(
        string path,
        string displayName,
        DateTime lastWriteTimeUtc,
        bool lastSaveWasAutomatic = false)
    {
        Path = path;
        DisplayName = displayName;
        LastWriteTimeUtc = lastWriteTimeUtc;
        LastSaveWasAutomatic = lastSaveWasAutomatic;
    }
}
