using System;
using System.IO;
using UnityEngine;

// Untitled-name generation and in-place rename (first-use UX: new, save, rename).
static class ProjectNamingChecks
{
    static int checks;

    public static void Run()
    {
        string previousRoot = Application.persistentDataPath;
        string root = Path.Combine(Path.GetTempPath(), "ProjectNamingChecks-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        Application.persistentDataPath = root;
        try { RunChecks(); }
        finally
        {
            Application.persistentDataPath = previousRoot;
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
        Console.WriteLine($"[Regression] {checks} project naming / rename checks passed.");
    }

    static void RunChecks()
    {
        Check(EditorProjectStore.UniqueProjectName(null) == EditorProjectStore.UntitledProjectName &&
            EditorProjectStore.UntitledProjectName == "無題の教材", "A new lesson starts as 無題の教材");
        var untitled = new EditorProjectFile { projectName = EditorProjectStore.UntitledProjectName };
        string firstPath = EditorProjectStore.Save(untitled, untitled.projectName);
        Check(EditorProjectStore.UniqueProjectName(null) == "無題の教材 2", "Second untitled lesson is numbered");
        EditorProjectStore.Save(new EditorProjectFile { projectName = "無題の教材 2" }, "無題の教材 2");
        Check(EditorProjectStore.UniqueProjectName("無題の教材") == "無題の教材 3", "Numbering skips taken names");
        Check(EditorProjectStore.FindProjectPathByName("無題の教材") == firstPath &&
            EditorProjectStore.FindProjectPathByName("  無題の教材 ") == firstPath &&
            EditorProjectStore.FindProjectPathByName("無題の教材 9") == null, "Name lookup finds the file by name only");

        var project = new EditorProjectFile { projectName = "Before" };
        project.objects.Add(new EditorProjectObject { id = "a", typeId = "box", position = new Vector3(1, 2, 3) });
        string before = EditorProjectStore.Save(project, "Before");
        EditorProjectStore.Save(new EditorProjectFile { projectName = "Taken" }, "Taken");
        string takenBytes = File.ReadAllText(EditorProjectStore.FindProjectPathByName("Taken"));

        string after = EditorProjectStore.Rename(before, "After");
        Check(!File.Exists(before) && File.Exists(after) && Path.GetFileName(after) == "After" + EditorProjectStore.FileSuffix,
            "Rename moves the lesson file, leaving no old name behind");
        Check(EditorProjectStore.TryLoad(after, out var renamed, out _) && renamed.projectName == "After" &&
            renamed.objects.Count == 1 && renamed.objects[0].id == "a" && Math.Abs(renamed.objects[0].position.z - 3f) < 0.00001f,
            "Rename keeps the content and updates the stored name");
        bool refused = false;
        try { EditorProjectStore.Rename(after, "Taken"); } catch (IOException) { refused = true; }
        Check(refused && File.Exists(after) && File.ReadAllText(EditorProjectStore.FindProjectPathByName("Taken")) == takenBytes,
            "Rename never overwrites another lesson");
        string sameName = EditorProjectStore.Rename(after, "After");
        Check(sameName == after && File.Exists(after), "Renaming to the same name is harmless");
        refused = false;
        try { EditorProjectStore.Rename(after, "  "); } catch (ArgumentException) { refused = true; }
        Check(refused, "Blank names are rejected");

        Check(EditorProjectStore.TryGetFileInfo(after, out var info) && info.DisplayName == "After" && !EditorProjectStore.TryGetFileInfo(before, out _),
            "File info is read from the file name without opening it");
    }

    static void Check(bool value, string label)
    {
        if (!value) throw new Exception(label);
        checks++;
    }
}
