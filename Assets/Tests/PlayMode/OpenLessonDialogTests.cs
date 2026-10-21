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

// "教材を開く" dialog: search, row selection, trash wording, and no save/new/export controls.
public sealed class OpenLessonDialogTests
{
    readonly List<GameObject> roots = new List<GameObject>();
    PropertyInfo sandboxProperty;
    string previousSandbox, sandbox;

    [SetUp]
    public void SetUp()
    {
        sandboxProperty = RuntimeType("EditorProjectStore").GetProperty("EditorVerificationProjectsDirectory");
        previousSandbox = (string)sandboxProperty.GetValue(null);
        sandbox = Path.Combine(Path.GetTempPath(), "OpenLessonDialog-" + Guid.NewGuid().ToString("N"));
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
    public IEnumerator DialogListsSearchesSelectsAndMovesToTrash()
    {
        var store = RuntimeType("EditorProjectStore");
        var fileType = RuntimeType("EditorProjectFile");
        foreach (var name in new[] { "Brake lesson", "Tire lesson" })
            store.GetMethod("Save").Invoke(null, new[] { Activator.CreateInstance(fileType), name });

        var ui = new GameObject("DialogUi", typeof(RectTransform), typeof(Canvas));
        roots.Add(ui);
        roots.Add(new GameObject("Graph", typeof(Transform)).AddComponent(RuntimeType("CurriculumGraphService")).gameObject);
        var panelType = RuntimeType("EditorProjectPanel");
        var panel = panelType.GetMethod("Ensure").Invoke(null, new object[] { ui.transform });
        panelType.GetMethod("OpenForDesignUi").Invoke(panel, null);
        yield return null;

        var modal = ui.transform.Find("Panel_ProjectFiles");
        var dialog = modal.Find("Dialog");
        Assert.That(TextOf(dialog.Find("Title")), Is.EqualTo("教材を開く"));
        foreach (var gone in new[] { "Input_ProjectName", "Button_SaveProject", "Button_NewProject", "Button_ExportPlacement", "Text_SaveLocation" })
            Assert.That(dialog.Find(gone), Is.Null, gone + " belongs to the header/start screen or is internal.");
        Assert.That(new[] { "教材", "テンプレート", "ゴミ箱" },
            Is.EqualTo(Enumerable.Range(0, 3).Select(i => TextOf(dialog.Find("Button_Library" + i).Find("Label"))).ToArray()));

        var content = dialog.Find("Scroll_Projects/Viewport/Content");
        Assert.That(Rows(content), Is.EquivalentTo(new[] { "Brake lesson", "Tire lesson" }));

        var search = dialog.Find("Input_Search").GetComponent(RuntimeType("TMPro.TMP_InputField"));
        search.GetType().GetProperty("text").SetValue(search, "tire");
        yield return null;
        Assert.That(Rows(content), Is.EqualTo(new[] { "Tire lesson" }));

        var row = content.Cast<Transform>().First(t => t.name.StartsWith("Project_"));
        Assert.That(row.Find("Button_Action開く"), Is.Null, "Actions appear once a row is selected.");
        row.GetComponent<Button>().onClick.Invoke();
        yield return null;
        row = content.Cast<Transform>().First(t => t.name.StartsWith("Project_"));
        Assert.That(row.Find("Button_Action開く"), Is.Not.Null);
        Assert.That(row.Find("Button_Actionゴミ箱へ移す"), Is.Not.Null);

        // Moving to the trash goes through the central confirmation.
        row.Find("Button_Actionゴミ箱へ移す").GetComponent<Button>().onClick.Invoke();
        yield return null;
        var confirm = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
            .FirstOrDefault(t => t.name == "SkillSyncConfirmDialog");
        Assert.That(confirm, Is.Not.Null);
        confirm.Find("Panel/Buttons/Button_Primary").GetComponent<Button>().onClick.Invoke();
        yield return null;

        search.GetType().GetProperty("text").SetValue(search, string.Empty);
        yield return null;
        Assert.That(Rows(content), Is.EqualTo(new[] { "Brake lesson" }));
        dialog.Find("Button_Library2").GetComponent<Button>().onClick.Invoke();
        yield return null;
        Assert.That(Rows(content), Is.EqualTo(new[] { "Tire lesson" }));
        var trashRow = content.Cast<Transform>().First(t => t.name.StartsWith("Project_"));
        trashRow.GetComponent<Button>().onClick.Invoke();
        yield return null;
        trashRow = content.Cast<Transform>().First(t => t.name.StartsWith("Project_"));
        Assert.That(trashRow.Find("Button_Action元に戻す"), Is.Not.Null);
    }

    static string[] Rows(Transform content) => content.Cast<Transform>().Where(t => t.gameObject.activeInHierarchy && t.name.StartsWith("Project_"))
        .Select(t => TextOf(t.Find("Label"))).ToArray();

    static string TextOf(Transform t) => (string)t.GetComponent(RuntimeType("TMPro.TMP_Text")).GetType().GetProperty("text").GetValue(t.GetComponent(RuntimeType("TMPro.TMP_Text")));

    static Type RuntimeType(string name) => AppDomain.CurrentDomain.GetAssemblies()
        .Select(assembly => assembly.GetType(name)).First(type => type != null);
}
#endif
