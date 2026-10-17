using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class GroupSelectionChecks
{
    [MenuItem("Tools/Automation/Check Group Selection")]
    public static void Run()
    {
        // Flat groups use scene-wide discovery, which excludes Unity preview scenes.
        var previous = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (Application.isBatchMode && string.IsNullOrEmpty(previous.path) && !previous.isDirty)
            previous = UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/EditorMain.unity");
        var scene = UnityEditor.SceneManagement.EditorSceneManager.NewScene(
            UnityEditor.SceneManagement.NewSceneSetup.EmptyScene, UnityEditor.SceneManagement.NewSceneMode.Additive);
        var host = new GameObject("Group selection checks");
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(host, scene);
        host.SetActive(false);
        var previousCommands = CommandService.I;
        try
        {
            var selection = host.AddComponent<SelectionService>();
            selection.outline = host.AddComponent<SelectionOutline>();
            var commands = host.AddComponent<CommandService>();
            CommandService.I = commands;
            var root = Create("Jack", scene);
            root.typeId = "Imported/GroupCheck";
            var first = Create("Handle", scene, root);
            var second = Create("Base", scene, root);
            first.modelRoot = second.modelRoot = root;
            first.transform.localPosition = Vector3.right;
            second.transform.localPosition = Vector3.forward;
            var otherRoot = Create("Other model", scene);
            var otherPart = Create("Other part", scene, otherRoot);

            selection.SelectPicked(first);
            Require(selection.Current == root && selection.Selected.Count == 1,
                "First part tap selects the whole model root without selecting descendants twice");
            CheckTransform(selection, commands, new[] { root, first, second }, false);
            selection.SelectPicked(first);
            Require(selection.Current == first && selection.Selected.Count == 1,
                "Second tap selects the hit part");
            CheckTransform(selection, commands, new[] { first }, true, new[] { root, second });
            selection.SelectPicked(second);
            Require(selection.Current == second, "Further taps inside the model select parts directly");
            selection.SelectPicked(second);
            Require(selection.Current == second, "Repeated part tap keeps that part selected");
            selection.SelectPicked(otherPart);
            Require(selection.Current == otherRoot, "Another model starts with whole-model selection");
            selection.SelectPicked(first);
            Require(selection.Current == root, "Returning to a model starts with its whole group again");
            selection.SelectPicked(first);
            selection.Select(null);
            selection.SelectPicked(second);
            Require(selection.Current == root, "Clearing selection exits part selection");
            selection.Select(first);
            Require(selection.Current == first, "Explicit hierarchy row selection still selects its part");
            selection.SelectMany(new[] { root, first, second });
            Require(selection.Selected.Count == 1 && selection.Current == root,
                "A transform selection never includes a parent and its descendants");

            string group = Guid.NewGuid().ToString("N");
            var flatFirst = Create("Flat first", scene);
            var flatSecond = Create("Flat second", scene);
            flatFirst.editorGroupId = flatSecond.editorGroupId = group;
            flatSecond.transform.position = new Vector3(3f, 0f, 0f);
            selection.SelectPicked(flatFirst);
            Require(selection.Selected.Count == 2 && selection.Contains(flatFirst) && selection.Contains(flatSecond),
                "First flat-group tap selects every member");
            CheckTransform(selection, commands, new[] { flatFirst, flatSecond }, true);
            selection.SelectPicked(flatFirst);
            Require(selection.Selected.Count == 1 && selection.Current == flatFirst,
                "Second flat-group tap selects just the hit member");
            selection.SelectPicked(flatSecond);
            Require(selection.Current == flatSecond && selection.Selected.Count == 1,
                "Further flat-group taps keep selecting individual members");
            selection.Select(null);
            selection.SelectPicked(flatFirst, true);
            Require(selection.Selected.Count == 2, "Additive tap selects a whole flat group");
            selection.SelectPicked(flatSecond, true);
            Require(selection.Selected.Count == 0, "Additive tap toggles the whole selected group off");
            selection.SelectPicked(first, true);
            Require(selection.Selected.Count == 1 && selection.Current == root,
                "Additive part tap selects its whole hierarchy");
            selection.SelectPicked(second, true);
            Require(selection.Selected.Count == 0, "Additive hierarchy tap toggles its root off");

            var plainFirst = Create("Plain first", scene);
            var plainSecond = Create("Plain second", scene);
            plainSecond.transform.position = Vector3.right;
            selection.SelectMany(new[] { plainFirst, plainSecond });
            selection.SelectPicked(plainFirst);
            Require(selection.Selected.Count == 2 && selection.Current == plainFirst && selection.Contains(plainSecond),
                "Tapping a selected ordinary object changes the primary while retaining the selection");
            CheckTransform(selection, commands, new[] { plainFirst, plainSecond }, true);
            selection.SelectPicked(plainFirst);
            Require(selection.Selected.Count == 2 && selection.Current == plainFirst,
                "Tapping the current ordinary object preserves the selection");
            selection.SelectPicked(plainSecond, true);
            Require(selection.Selected.Count == 1 && selection.Current == plainFirst,
                "Additive tap still removes one ordinary object from the selection");
            Debug.Log("[Group selection] All checks passed: model/flat groups, part taps, transforms and undo/redo.");
        }
        finally
        {
            CommandService.I = previousCommands;
            UnityEditor.SceneManagement.EditorSceneManager.CloseScene(scene, true);
            if (previous.IsValid() && previous.isLoaded) UnityEngine.SceneManagement.SceneManager.SetActiveScene(previous);
        }
    }

    static PlacedObject Create(string name, UnityEngine.SceneManagement.Scene scene, PlacedObject parent = null)
    {
        var item = new GameObject(name);
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(item, scene);
        if (parent != null) item.transform.SetParent(parent.transform, false);
        var placed = item.AddComponent<PlacedObject>();
        placed.id = Guid.NewGuid().ToString("N");
        return placed;
    }

    static void CheckTransform(SelectionService selection, CommandService commands, PlacedObject[] items, bool flat,
        PlacedObject[] unaffected = null)
    {
        commands.Stack.Clear();
        var positions = items.Select(item => item.transform.position).ToArray();
        var rotations = items.Select(item => item.transform.rotation).ToArray();
        var scales = items.Select(item => item.transform.localScale).ToArray();
        var unchangedPositions = unaffected?.Select(item => item.transform.position).ToArray();
        var unchangedRotations = unaffected?.Select(item => item.transform.rotation).ToArray();
        var unchangedScales = unaffected?.Select(item => item.transform.localScale).ToArray();
        var primary = selection.Current.transform;
        Vector3 origin = primary.position;
        var gesture = new SelectionTransformSession(selection);
        Vector3 delta = new Vector3(2f, 3f, 4f);
        Quaternion rotation = Quaternion.Euler(0f, 90f, 0f);
        primary.position += delta;
        primary.rotation = rotation * primary.rotation;
        primary.localScale *= 2f;
        gesture.Commit("Group check transform");
        for (int i = 0; unaffected != null && i < unaffected.Length; i++)
            Require(Vector3.Distance(unaffected[i].transform.position, unchangedPositions[i]) < 0.0001f &&
                Quaternion.Angle(unaffected[i].transform.rotation, unchangedRotations[i]) < 0.001f &&
                Vector3.Distance(unaffected[i].transform.localScale, unchangedScales[i]) < 0.0001f,
                "Part transform leaves its parent and siblings unchanged");
        for (int i = 0; i < items.Length; i++)
        {
            Vector3 expected = origin + delta + rotation * ((positions[i] - origin) * 2f);
            Require(Vector3.Distance(items[i].transform.position, expected) < 0.0001f,
                "Whole group position follows movement, rotation and scale");
            Require(Quaternion.Angle(items[i].transform.rotation, rotation * rotations[i]) < 0.001f,
                "Whole group rotation is applied");
            Require(Vector3.Distance(items[i].transform.localScale, scales[i] * (flat || i == 0 ? 2f : 1f)) < 0.0001f,
                "Hierarchy descendants are scaled exactly once");
        }
        Require(commands.Stack.UndoCount == 1 && commands.Stack.Undo(), "Whole group transform creates one undo entry");
        for (int i = 0; i < items.Length; i++)
            Require(Vector3.Distance(items[i].transform.position, positions[i]) < 0.0001f &&
                Quaternion.Angle(items[i].transform.rotation, rotations[i]) < 0.001f &&
                Vector3.Distance(items[i].transform.localScale, scales[i]) < 0.0001f,
                "One undo restores every group member");
        Require(commands.Stack.Redo(), "Whole group transform can be redone");
        Require(commands.Stack.Undo(), "Group check restores original transforms");
    }

    static void Require(bool result, string label)
    {
        if (!result) throw new InvalidOperationException("[Group selection] " + label);
    }
}
