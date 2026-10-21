#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

// First-use flows: untitled lessons, never-saved state, in-place rename, save-as with replace confirmation.
public sealed class FirstUseFlowTests
{
    readonly List<GameObject> roots = new List<GameObject>();
    PropertyInfo sandboxProperty;
    string previousSandbox, sandbox;

    [SetUp]
    public void SetUp()
    {
        Assert.That(UnityEngine.Object.FindObjectsByType(RuntimeType("EditorProjectService"),
            FindObjectsInactive.Include, FindObjectsSortMode.None), Is.Empty, "Run in an isolated test scene.");
        sandboxProperty = RuntimeType("EditorProjectStore").GetProperty("EditorVerificationProjectsDirectory");
        previousSandbox = (string)sandboxProperty.GetValue(null);
        sandbox = Path.Combine(Path.GetTempPath(), "FirstUseFlow-" + Guid.NewGuid().ToString("N"));
        sandboxProperty.SetValue(null, sandbox);
        Directory.CreateDirectory(sandbox);
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        foreach (var root in roots) if (root != null) UnityEngine.Object.Destroy(root);
        roots.Clear();
        yield return null;
        sandboxProperty.SetValue(null, previousSandbox);
        if (Directory.Exists(sandbox)) Directory.Delete(sandbox, true);
    }

    [UnityTest]
    public IEnumerator LessonStartsUntitledAndRenamesInPlace()
    {
        var service = CreateService(withPlacement: true);
        yield return null;
        Assert.That(Prop(service, "CurrentProjectName"), Is.EqualTo("無題の教材"));
        Assert.That(Prop(service, "HasSavedFile"), Is.False);
        Assert.That(Prop(service, "IsDirty"), Is.False);
        Assert.That(Prop(service, "SaveState").ToString(), Is.EqualTo("NeverSaved"));

        Assert.That(Call(service, "Save", "First lesson", null), Is.True);
        string path = (string)Prop(service, "CurrentProjectPath");
        Assert.That(File.Exists(path), Is.True);
        Assert.That(Prop(service, "SaveState").ToString(), Is.EqualTo("Saved"));

        var args = new object[] { "Renamed lesson", null };
        Assert.That(Call(service, "Rename", args), Is.True, (string)args[1]);
        string renamedPath = (string)Prop(service, "CurrentProjectPath");
        Assert.That(File.Exists(path), Is.False, "The old file name must not stay behind.");
        Assert.That(Path.GetFileName(renamedPath), Is.EqualTo("Renamed lesson.skillsync.json"));
        Assert.That(Prop(service, "SaveState").ToString(), Is.EqualTo("Saved"), "A rename alone does not make the lesson unsaved.");
        Assert.That(Directory.GetFiles(sandbox, "*.skillsync.json").Length, Is.EqualTo(1));

        Store("Save").Invoke(null, new[] { Activator.CreateInstance(RuntimeType("EditorProjectFile")), "Taken" });
        string taken = Path.Combine(sandbox, "Taken.skillsync.json");
        string takenBytes = File.ReadAllText(taken);
        Assert.That(Call(service, "NameInUse", "Taken"), Is.True);
        args = new object[] { "Taken", null };
        Assert.That(Call(service, "Rename", args), Is.False);
        Assert.That(File.ReadAllText(taken), Is.EqualTo(takenBytes), "Renaming never overwrites another lesson.");
        Assert.That(Prop(service, "CurrentProjectName"), Is.EqualTo("Renamed lesson"));

        args = new object[] { null, null };
        Assert.That(Call(service, "NewProject", args), Is.True);
        Assert.That(Prop(service, "CurrentProjectName"), Is.EqualTo("無題の教材"));
        Assert.That(Prop(service, "HasSavedFile"), Is.False);
        Assert.That(Prop(service, "IsDirty"), Is.False, "An untouched new lesson has nothing to lose.");
        Assert.That(Prop(service, "SaveState").ToString(), Is.EqualTo("NeverSaved"));
    }

    [UnityTest]
    public IEnumerator SavingANeverSavedLessonAsksForANameAndConfirmsReplacement()
    {
        var service = CreateService(withPlacement: false);
        var canvas = CreateCanvas();
        yield return null;
        Store("Save").Invoke(null, new[] { Activator.CreateInstance(RuntimeType("EditorProjectFile")), "Taken" });
        string taken = Path.Combine(sandbox, "Taken.skillsync.json");
        string takenBytes = File.ReadAllText(taken);

        bool? finished = null;
        RuntimeType("EditorProjectSaveFlow").GetMethod("Run").Invoke(null, new object[] { service, (Action<bool>)(saved => finished = saved) });
        yield return null;
        Assert.That((bool)RuntimeType("SkillSyncSaveAsDialog").GetProperty("IsOpen").GetValue(null), Is.True, "A never-saved lesson asks for a name.");
        Assert.That(HasOpenModal(), Is.True);
        var dialog = canvas.transform.Find("SkillSyncSaveAsDialog");
        var input = dialog.GetComponentInChildren(RuntimeType("TMPro.TMP_InputField"), true);
        Assert.That(Prop(input, "text"), Is.EqualTo("無題の教材"), "The name field starts with the current lesson name.");
        SetText(input, "Taken");
        yield return null;

        dialog.Find("Panel/Buttons/Button_Save").GetComponent<Button>().onClick.Invoke();
        yield return null;
        var confirm = canvas.transform.Find("SkillSyncConfirmDialog");
        Assert.That(confirm != null && confirm.gameObject.activeSelf, Is.True, "Replacing another lesson needs an explicit choice.");
        Assert.That(File.ReadAllText(taken), Is.EqualTo(takenBytes), "Nothing is written before the choice.");
        Assert.That(finished, Is.Null);

        confirm.Find("Panel/Buttons/Button_Cancel").GetComponent<Button>().onClick.Invoke(); // 名前を変える
        yield return null;
        Assert.That((bool)RuntimeType("SkillSyncSaveAsDialog").GetProperty("IsOpen").GetValue(null), Is.True, "Choosing a new name returns to the name field.");
        Assert.That(File.ReadAllText(taken), Is.EqualTo(takenBytes));
        SetText(input, "Free name");
        dialog.Find("Panel/Buttons/Button_Save").GetComponent<Button>().onClick.Invoke();
        yield return null;
        Assert.That(finished, Is.True);
        Assert.That(File.Exists(Path.Combine(sandbox, "Free name.skillsync.json")), Is.True);
        Assert.That(File.ReadAllText(taken), Is.EqualTo(takenBytes));
        Assert.That(Prop(service, "SaveState").ToString(), Is.EqualTo("Saved"));

        // Saving again goes to the lesson's own file without asking.
        finished = null;
        RuntimeType("EditorProjectSaveFlow").GetMethod("Run").Invoke(null, new object[] { service, (Action<bool>)(saved => finished = saved) });
        Assert.That(finished, Is.True);
        Assert.That(HasOpenModal(), Is.False);
    }

    // ---- helpers -------------------------------------------------------------------------------------------

    Component CreateService(bool withPlacement)
    {
        CreateRoot("Graph").AddComponent(RuntimeType("CurriculumGraphService"));
        if (withPlacement) CreateRoot("Placement").AddComponent(RuntimeType("PlacementController"));
        return CreateRoot("Service").AddComponent(RuntimeType("EditorProjectService"));
    }

    GameObject CreateCanvas()
    {
        var go = CreateRoot("TestCanvas", typeof(RectTransform), typeof(Canvas));
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        var rect = (RectTransform)go.transform;
        rect.sizeDelta = new Vector2(2560f, 1440f);
        return go;
    }

    GameObject CreateRoot(string name, params Type[] components)
    {
        var root = new GameObject(name, components);
        roots.Add(root);
        return root;
    }

    static bool HasOpenModal() => (bool)RuntimeType("EditWorkspace").GetProperty("HasOpenModal").GetValue(null);
    static MethodInfo Store(string name) => RuntimeType("EditorProjectStore").GetMethod(name);
    static void SetText(object input, string value) => input.GetType().GetProperty("text").SetValue(input, value);
    static object Prop(object target, string name) => target.GetType().GetProperty(name).GetValue(target);

    static object Call(object target, string name, params object[] args)
    {
        var method = target.GetType().GetMethod(name);
        return method.Invoke(target, args);
    }

    static Type RuntimeType(string name) => AppDomain.CurrentDomain.GetAssemblies()
        .Select(assembly => assembly.GetType(name)).First(type => type != null);
}
#endif
