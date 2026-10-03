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

public sealed class RecoveryStartupWarningTests
{
    readonly List<GameObject> roots = new List<GameObject>();
    Type storeType;
    PropertyInfo sandboxProperty;
    string previousSandbox;
    string sandbox;

    [SetUp]
    public void SetUp()
    {
        Assert.That(UnityEngine.Object.FindObjectsByType(RuntimeType("EditorProjectService"),
            FindObjectsInactive.Include, FindObjectsSortMode.None), Is.Empty,
            "Run in an isolated test scene, without an existing editor session.");
        storeType = RuntimeType("EditorProjectStore");
        sandboxProperty = storeType.GetProperty("EditorVerificationProjectsDirectory");
        previousSandbox = (string)sandboxProperty.GetValue(null);
        sandbox = Path.Combine(Path.GetTempPath(), "RecoveryWarningPlayMode-" + Guid.NewGuid().ToString("N"));
        sandboxProperty.SetValue(null, sandbox);
        Directory.CreateDirectory(Path.GetDirectoryName(RecoveryPath));
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        foreach (var root in roots) if (root != null) UnityEngine.Object.Destroy(root);
        roots.Clear();
        yield return null;
        if (sandboxProperty != null) sandboxProperty.SetValue(null, previousSandbox);
        if (sandbox != null && Directory.Exists(sandbox)) Directory.Delete(sandbox, true);
    }

    [UnityTest]
    public IEnumerator LateUiShowsWarningOnceAndKeepsItUntilExplicitDiscard()
    {
        File.WriteAllText(RecoveryPath, "{broken");
        var service = CreateRoot("RecoveryService").AddComponent(RuntimeType("EditorProjectService"));
        Assert.That(Warning(service), Is.Not.Empty, "Awake failure must survive before UI subscription.");
        int statuses = 0;
        Action<string, bool> statusListener = (_, __) => statuses++;
        service.GetType().GetEvent("StatusChanged").AddEventHandler(service, statusListener);

        var ui = CreateRoot("RecoveryUi", typeof(RectTransform), typeof(Canvas));
        var topBar = new GameObject("Panel_ScenarioGraph", typeof(RectTransform));
        topBar.transform.SetParent(ui.transform, false);
        new GameObject("TopBar", typeof(RectTransform)).transform.SetParent(topBar.transform, false);
        var panelType = RuntimeType("EditorProjectPanel");
        var panel = panelType.GetMethod("Ensure").Invoke(null, new object[] { ui.transform });
        panelType.GetMethod("ShowRecoveryProtectionWarning").Invoke(panel, null);
        var modal = ui.transform.Find("Panel_ProjectFiles");
        Assert.That(modal.gameObject.activeSelf, Is.True, "Startup warning must be visible without opening the library manually.");
        yield return null;

        var warningRow = modal.Find("Dialog/Scroll_Projects/Viewport/Content/Project_RecoveryProtectionWarning");
        Assert.That(warningRow, Is.Not.Null);
        var retry = warningRow.Find("Button_RetryRecoveryProtection").GetComponent<Button>();
        retry.onClick.Invoke();
        Assert.That(statuses, Is.Zero, "Repeated startup protection failure must not emit a later autosave failure status.");
        Assert.That(File.ReadAllText(RecoveryPath), Is.EqualTo("{broken"));

        panelType.GetMethod("Close", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(panel, null);
        panelType.GetMethod("ShowRecoveryProtectionWarning").Invoke(panel, null);
        Assert.That(modal.gameObject.activeSelf, Is.False, "An acknowledged startup warning must not repeatedly reopen the library.");
        panelType.GetMethod("OpenForDesignUi").Invoke(panel, null);
        Assert.That(Warning(service), Is.Not.Empty, "Closing and reopening must retain unresolved warning state.");

        Assert.That(InvokeOperation(service, "DeleteRecovery"), Is.True);
        yield return null;
        Assert.That(Warning(service), Is.Null);
        Assert.That(modal.Find("Dialog/Scroll_Projects/Viewport/Content/Project_RecoveryProtectionWarning"), Is.Null);
        Assert.That(File.Exists(RecoveryPath), Is.False);
    }

    [UnityTest]
    public IEnumerator AutosaveResolvesProtectionAndReportsLaterWriteFailuresSeparately()
    {
        File.WriteAllText(RecoveryPath, "{broken");
        CreateRoot("RecoveryGraph").AddComponent(RuntimeType("CurriculumGraphService"));
        var service = CreateRoot("RecoveryService").AddComponent(RuntimeType("EditorProjectService"));
        yield return null;
        service.GetType().GetProperty("IsDirty").SetValue(service, true);
        service.GetType().GetField("trackingInitialized", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(service, true);
        int failures = 0;
        Action<string, bool> listener = (_, succeeded) => { if (!succeeded) failures++; };
        service.GetType().GetEvent("StatusChanged").AddEventHandler(service, listener);
        Assert.That(InvokeOperation(service, "SaveRecoveryNow"), Is.False);
        Assert.That(failures, Is.Zero, "Startup preservation remains a separate retained warning.");
        Assert.That(Warning(service), Is.Not.Empty);
        Assert.That(File.ReadAllText(RecoveryPath), Is.EqualTo("{broken"));

        Assert.That((bool)service.GetType().GetMethod("Save").Invoke(service, new object[] { "Current lesson", null }), Is.True);
        Assert.That(Warning(service), Is.Not.Empty, "Normal save must not dismiss unpreserved startup recovery.");
        Assert.That(File.ReadAllText(RecoveryPath), Is.EqualTo("{broken"));
        Assert.That((bool)service.GetType().GetMethod("NewProject").Invoke(service, new object[] { "Next lesson", null }), Is.True);
        Assert.That(Warning(service), Is.Not.Empty, "A new lesson must retain the previous recovery warning.");

        // Simulate the user repairing the previously unreadable recovery.
        var previous = Activator.CreateInstance(RuntimeType("EditorProjectFile"));
        storeType.GetMethod("SaveRecovery").Invoke(null, new[] { previous });
        string repairedBytes = File.ReadAllText(RecoveryPath);
        Assert.That(InvokeOperation(service, "SaveRecoveryNow"), Is.True);
        Assert.That(Warning(service), Is.Null, "Automatic retry also resolves the startup warning.");
        Assert.That(File.ReadAllText(Path.Combine(sandbox, "VRCourseEditor 復旧.skillsync.json")), Is.EqualTo(repairedBytes));

        // A later current-slot write failure still uses the existing StatusChanged channel.
        Assert.That(InvokeOperation(service, "DeleteRecovery"), Is.True);
        Directory.CreateDirectory(RecoveryPath);
        Assert.That(InvokeOperation(service, "SaveRecoveryNow"), Is.False);
        Assert.That(failures, Is.EqualTo(1));
        Assert.That(Warning(service), Is.Null, "Later autosave errors must not become startup protection warnings.");
    }

    [UnityTest]
    public IEnumerator SuccessfulRetryPreservesOriginalAndLeavesNewOwnedRecoveryAlone()
    {
        var project = Activator.CreateInstance(RuntimeType("EditorProjectFile"));
        project.GetType().GetField("projectName").SetValue(project, "Previous lesson");
        string original = (string)storeType.GetMethod("Save").Invoke(null, new[] { project, "Previous lesson" });
        string originalBytes = File.ReadAllText(original);
        storeType.GetMethod("SaveRecovery").Invoke(null, new[] { project });
        string recoveryBytes = File.ReadAllText(RecoveryPath);
        string blocked = Path.Combine(sandbox, "Previous lesson 復旧.skillsync.json");
        Directory.CreateDirectory(blocked);
        var service = CreateRoot("RecoveryService").AddComponent(RuntimeType("EditorProjectService"));
        Assert.That(Warning(service), Is.Not.Empty);

        Directory.Delete(blocked);
        Assert.That(InvokeOperation(service, "RetryRecoveryProtection"), Is.True);
        Assert.That(Warning(service), Is.Null);
        Assert.That(File.ReadAllText(blocked), Is.EqualTo(recoveryBytes));
        Assert.That(File.ReadAllText(original), Is.EqualTo(originalBytes));

        var session = service.GetType().GetField("recoverySession", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(service);
        session.GetType().GetMethod("Save").Invoke(session, new[] { project });
        string ownedBytes = File.ReadAllText(RecoveryPath);
        Assert.That(InvokeOperation(service, "RetryRecoveryProtection"), Is.True);
        Assert.That(File.ReadAllText(RecoveryPath), Is.EqualTo(ownedBytes), "Resolved retry must not move the session's owned recovery.");
        Assert.That(File.Exists(Path.Combine(sandbox, "Previous lesson 復旧 (2).skillsync.json")), Is.False);
        yield return null;
    }

    string RecoveryPath => (string)storeType.GetProperty("RecoveryPath").GetValue(null);
    static string Warning(Component service) => (string)service.GetType().GetProperty("RecoveryProtectionWarning").GetValue(service);
    static bool InvokeOperation(Component service, string name) =>
        (bool)service.GetType().GetMethod(name).Invoke(service, new object[] { null });
    static Type RuntimeType(string name) => AppDomain.CurrentDomain.GetAssemblies()
        .Select(assembly => assembly.GetType(name)).First(type => type != null);

    GameObject CreateRoot(string name, params Type[] components)
    {
        var root = new GameObject(name, components);
        roots.Add(root);
        return root;
    }
}
#endif
