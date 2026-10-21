using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using Unity.Profiling;

public static class EditorProjectStore
{
    static readonly ProfilerMarker SerializeProjectMarker = new("EditorProjectStore.SerializeProject");
    static readonly ProfilerMarker WriteProjectFileMarker = new("EditorProjectStore.WriteProjectFile");
    static readonly ProfilerMarker TryLoadReadFileMarker = new("EditorProjectStore.TryLoad.ReadFile");
    static readonly ProfilerMarker TryLoadParseMarker = new("EditorProjectStore.TryLoad.ParseAndMigrate");
    static readonly ProfilerMarker ListDirectoryMarker = new("EditorProjectStore.ListDirectory");

    public const string FileSuffix = ".skillsync.json";
    // Name of a lesson that has never been saved or named by the user.
    public const string UntitledProjectName = "無題の教材";
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

    public static string DisplayNameOf(string path) => RemoveSuffix(Path.GetFileName(path));

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
            throw new InvalidOperationException("教材の一覧にあるファイルを指定してください。");
        if (!File.Exists(full)) throw new FileNotFoundException("教材のファイルが見つかりません。", full);
        return full;
    }

    static bool SameDirectory(string path, string directory) => string.Equals(
        Path.GetDirectoryName(Path.GetFullPath(path)), Path.GetFullPath(directory), StringComparison.OrdinalIgnoreCase);

    static string MoveToAvailableName(string source, string directory, string name, Action<string> prepare = null)
    {
        Directory.CreateDirectory(directory);
        string safeName = ExportFileNameUtility.SanitizeProjectName(name, UntitledProjectName);
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

    // Path of the saved lesson whose file name matches (case-insensitive), or null. Reads names only, never file contents.
    public static string FindProjectPathByName(string projectName)
    {
        try
        {
            if (!Directory.Exists(ProjectsDirectory)) return null;
            string safe = ExportFileNameUtility.SanitizeProjectName(projectName, UntitledProjectName);
            foreach (string path in Directory.GetFiles(ProjectsDirectory, "*" + FileSuffix, SearchOption.TopDirectoryOnly))
                if (string.Equals(RemoveSuffix(Path.GetFileName(path)), safe, StringComparison.OrdinalIgnoreCase)) return path;
        }
        catch (Exception ex) { Debug.LogWarning("[EditorProjectStore] 教材名を確認できません: " + ex.Message); }
        return null;
    }

    // "name", then "name 2", "name 3" ... until no saved lesson uses it.
    public static string UniqueProjectName(string baseName)
    {
        string name = string.IsNullOrWhiteSpace(baseName) ? UntitledProjectName : baseName.Trim();
        string candidate = name;
        for (int number = 2; FindProjectPathByName(candidate) != null && number < 10000; number++) candidate = name + " " + number;
        return candidate;
    }

    // Renames the saved lesson file in place (same lesson, new name). Never overwrites another lesson.
    public static string Rename(string existingPath, string newName)
    {
        string source = RequireLibraryPath(existingPath, false);
        string name = newName?.Trim();
        if (string.IsNullOrEmpty(name)) throw new ArgumentException("教材名を入力してください。", nameof(newName));
        if (!TryLoad(source, out var project, out var error)) throw new InvalidOperationException(error);
        string target = Path.Combine(Path.GetDirectoryName(source),
            ExportFileNameUtility.SanitizeProjectName(name, UntitledProjectName) + FileSuffix);
        bool sameFile = string.Equals(source, target, StringComparison.OrdinalIgnoreCase);
        if (!sameFile && File.Exists(target)) throw new IOException("同じ名前の教材があります。");
        if (!string.Equals(source, target, StringComparison.Ordinal)) File.Move(source, target);
        try { WriteProject(project, name, target, false); }
        catch
        {
            if (!string.Equals(source, target, StringComparison.Ordinal) && File.Exists(target) && !File.Exists(source)) File.Move(target, source);
            throw;
        }
        return target;
    }

    // Newest saved lessons by file time, without opening any file.
    public static IReadOnlyList<EditorProjectFileInfo> ListRecentlyWritten(int count)
    {
        var result = new List<EditorProjectFileInfo>();
        try
        {
            if (!Directory.Exists(ProjectsDirectory)) return result;
            foreach (var file in Directory.GetFiles(ProjectsDirectory, "*" + FileSuffix, SearchOption.TopDirectoryOnly)
                         .Select(path => new FileInfo(path)).OrderByDescending(file => file.LastWriteTimeUtc).Take(count))
                result.Add(new EditorProjectFileInfo(file.FullName, RemoveSuffix(file.Name), file.LastWriteTimeUtc));
        }
        catch (Exception ex) { Debug.LogWarning("[EditorProjectStore] 最近の教材を取得できません: " + ex.Message); }
        return result;
    }

    public static bool TryGetFileInfo(string path, out EditorProjectFileInfo info)
    {
        info = null;
        try
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return false;
            var file = new FileInfo(path);
            info = new EditorProjectFileInfo(file.FullName, RemoveSuffix(file.Name), file.LastWriteTimeUtc);
            return true;
        }
        catch { return false; }
    }

    public static string Save(EditorProjectFile project, string projectName)
    {
        if (project == null) throw new ArgumentNullException(nameof(project));

        string safeName = ExportFileNameUtility.SanitizeProjectName(projectName, UntitledProjectName);
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
            ? ExportFileNameUtility.SanitizeProjectName(projectName, UntitledProjectName)
            : projectName.Trim();
        project.savedAtUtc = DateTime.UtcNow.ToString("O");
        project.lastSaveWasAutomatic = automatic;
        EditorProjectMigration.Normalize(project);

        string serialized;
        using (SerializeProjectMarker.Auto()) serialized = JsonUtility.ToJson(project, true);
        using (WriteProjectFileMarker.Auto()) ExportFileWriter.WriteAllTextWithBackup(path, serialized);
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
                error = "教材のファイルが見つかりません。";
                return false;
            }

            string contents;
            using (TryLoadReadFileMarker.Auto()) contents = File.ReadAllText(resolvedPath);
            using (TryLoadParseMarker.Auto())
            {
                return EditorProjectMigration.TryRead(contents, out project, out error);
            }
        }
        catch (Exception ex)
        {
            error = "教材を開けません: " + ex.Message;
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
            string displayName = "復旧データ";
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

    public static bool TryPreserveRecoveryAtStartup(out bool preserved, out string error)
        => TryPreserveRecoveryAtStartup(out preserved, out error, out _, out _);

    // preservedPath / originalName identify the protected copy ("name 復旧") and the lesson name it was recorded under.
    public static bool TryPreserveRecoveryAtStartup(out bool preserved, out string error, out string preservedPath, out string originalName)
    {
        preserved = false;
        error = null;
        preservedPath = null;
        originalName = null;

        try
        {
            if (!File.Exists(RecoveryPath)) return true;
            if (!TryLoad(RecoveryPath, out var recovery, out error)) return false;

            // A name (or timestamp) cannot identify the original project. Preserve
            // the exact recovery bytes as a separate lesson before any new session
            // can replace/delete the shared recovery slot. Move never overwrites.
            // A recovery identical to a lesson already in the list (its saved file or an earlier copy) adds nothing; drop it instead of piling up copies.
            if (FindSameContent(recovery) != null)
            {
                DeleteRecovery(out _);
                return true;
            }
            preservedPath = MoveToAvailableName(RecoveryPath, ProjectsDirectory, recovery.projectName + " 復旧");
            originalName = recovery.projectName;
            preserved = true;
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    // True for a protected copy made from a recovery slot ("name 復旧", "name 復旧 (2)").
    public static bool IsPreservedRecoveryName(string displayName) =>
        !string.IsNullOrEmpty(displayName) && System.Text.RegularExpressions.Regex.IsMatch(displayName, @" 復旧( \(\d+\))?$");

    static string FindSameContent(EditorProjectFile recovery)
    {
        string wanted = ContentFingerprint(recovery);
        if (!Directory.Exists(ProjectsDirectory)) return null;
        foreach (string path in Directory.GetFiles(ProjectsDirectory, "*" + FileSuffix, SearchOption.TopDirectoryOnly))
            if (TryLoad(path, out var project, out _) && project != null && string.Equals(ContentFingerprint(project), wanted, StringComparison.Ordinal))
                return path;
        return null;
    }

    // Compares what the lesson contains, not when or how it was written.
    static string ContentFingerprint(EditorProjectFile project)
    {
        string savedAt = project.savedAtUtc;
        bool automatic = project.lastSaveWasAutomatic;
        try
        {
            project.savedAtUtc = string.Empty;
            project.lastSaveWasAutomatic = false;
            return JsonUtility.ToJson(project);
        }
        finally { project.savedAtUtc = savedAt; project.lastSaveWasAutomatic = automatic; }
    }

    public static IReadOnlyList<EditorProjectFileInfo> ListProjects()
        => ListDirectory(ProjectsDirectory);

    static IReadOnlyList<EditorProjectFileInfo> ListDirectory(string directory)
    {
        using (ListDirectoryMarker.Auto())
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

public sealed class EditorProjectRecoverySession
{
    string ownedPath;
    string ownedContents;

    public void Save(EditorProjectFile project)
    {
        if (project == null) throw new ArgumentNullException(nameof(project));
        string path = EditorProjectStore.RecoveryPath;
        if (File.Exists(path) && !Owns(path))
        {
            if (!EditorProjectStore.TryPreserveRecoveryAtStartup(out _, out var error))
                throw new IOException("以前の復旧データを残せないため、復旧データの記録を中止しました。教材を保存してください。 " + error);
        }

        EditorProjectStore.SaveRecovery(project);
        // Ownership is granted only after the write succeeds. Compare contents as
        // well as path so a replaced recovery is never silently deleted later.
        ownedContents = File.ReadAllText(path);
        ownedPath = path;
    }

    public bool DeleteOwned(out string error)
    {
        error = null;
        try
        {
            string path = EditorProjectStore.RecoveryPath;
            if (File.Exists(path) && Owns(path)) File.Delete(path);
            // Backups may belong to an earlier session; only explicit discard
            // may remove them. Unowned recovery also remains available in the UI.
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
        finally
        {
            // Cleanup ends this editing context even if the file was locked.
            // A later project's autosave must preserve that remaining file first.
            ownedPath = null;
            ownedContents = null;
        }
    }

    bool Owns(string path) => ownedContents != null &&
        string.Equals(ownedPath, path, StringComparison.Ordinal) &&
        string.Equals(ownedContents, File.ReadAllText(path), StringComparison.Ordinal);
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
