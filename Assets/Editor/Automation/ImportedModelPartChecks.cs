using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class ImportedModelPartChecks
{
    [MenuItem("Tools/Automation/Tire Change/Check Part Import")]
    public static void Run()
    {
        var scene = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
        var source = new GameObject("Car");
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(source, scene);
        var instances = new List<GameObject> { source };
        try
        {
            var wheel = new GameObject("Wheel");
            wheel.transform.SetParent(source.transform, false);
            for (int i = 0; i < 2; i++)
            {
                var nut = GameObject.CreatePrimitive(PrimitiveType.Cube);
                UnityEngine.Object.DestroyImmediate(nut.GetComponent<Collider>());
                nut.name = "Nut / same name";
                nut.transform.SetParent(wheel.transform, false);
                nut.transform.localPosition = new Vector3(i, 0f, 0f);
            }
            CheckPlacement(source, instances);
            source.SetActive(false); // Restored model library templates are inactive.
            CheckPlacement(source, instances);
            source.SetActive(true);
            var instance = UnityEngine.Object.Instantiate(source);
            instances.Add(instance);
            var root = instance.AddComponent<PlacedObject>();
            root.id = "check-root";
            root.typeId = "Imported/Check";
            ImportedModelParts.Register(root);
            ImportedModelParts.ValidateSource(source.transform, root.sourceSignature);
            var parts = instance.GetComponentsInChildren<PlacedObject>(true);
            Require(parts.Length == 4, "Root, group and two meshes registered without extra geometry");
            Require(source.GetComponentsInChildren<PlacedObject>(true).Length == 0, "Source remains untouched");
            Require(parts.Select(p => p.id).Distinct().Count() == 4, "Same-name parts have distinct IDs");
            string id = parts[2].id;
            ImportedModelParts.Register(root);
            Require(parts[2].id == id, "Repeated registration retains IDs");
            parts[2].transform.localPosition = new Vector3(3f, 4f, 5f);
            parts[2].SetDisplayName("Edited nut");
            var firstState = parts[2].GetComponent<PlacedObjectEditState>();
            var rootState = root.GetComponent<PlacedObjectEditState>();
            firstState.SetVisible(false);
            rootState.SetVisible(false);
            rootState.SetVisible(true);
            Require(!parts[2].GetComponent<Renderer>().enabled && parts[3].GetComponent<Renderer>().enabled,
                "Parent show preserves child's hidden state");
            rootState.SetLocked(true);
            firstState.SetVisible(true);
            Require(!SelectionService.CanEdit(parts[2]) && !parts[2].GetComponent<Collider>().enabled,
                "Parent lock is inherited after child visibility change");
            rootState.SetLocked(false);
            Require(SelectionService.CanEdit(parts[2]), "Unlock restores child editability");
            var delete = new DeleteObjectCommand(parts[3].gameObject, root.typeId, null);
            Require(delete.Do() && !parts[3].gameObject.activeSelf && delete.Undo() && parts[3].gameObject.activeSelf,
                "Part deletion undo restores same object");
            delete.Do();
            delete.Discard();
            Require(parts[3] != null, "Deleted source node retained for save tombstone");

            var project = new EditorProjectFile();
            project.objects.Add(new EditorProjectObject { id = root.id, typeId = root.typeId,
                parts = ImportedModelParts.Capture(root) });
            Require(EditorProjectMigration.TryRead(JsonUtility.ToJson(project), out var loaded, out _), "v6 project round trip");
            var restored = UnityEngine.Object.Instantiate(source);
            instances.Add(restored);
            restored.SetActive(false);
            var restoredRoot = restored.AddComponent<PlacedObject>();
            restoredRoot.id = root.id;
            restoredRoot.typeId = root.typeId;
            ImportedModelParts.Restore(restoredRoot, loaded.objects[0].parts);
            var restoredParts = restored.GetComponentsInChildren<PlacedObject>(true);
            Require(restoredParts.Length == 4 && restored.GetComponentsInChildren<MeshFilter>(true).Length == 2,
                "Reload binds existing geometry exactly once");
            Require(restoredParts[2].id == id && restoredParts[2].GetDisplayName() == "Edited nut" &&
                restoredParts[2].transform.localPosition == new Vector3(3f, 4f, 5f), "Part identity and edit restored");
            Require(!restoredParts[3].gameObject.activeSelf, "Deleted part does not reappear after reload");

            var duplicate = UnityEngine.Object.Instantiate(instance);
            instances.Add(duplicate);
            var clone = duplicate.GetComponent<PlacedObject>();
            ImportedModelParts.ReidentifyDuplicate(clone, root);
            Require(!clone.modelRoot && duplicate.GetComponentsInChildren<PlacedObject>(true).All(p =>
                !parts.Any(original => original.id == p.id)), "Whole model duplicate reassigns all IDs");
            var leafCopy = UnityEngine.Object.Instantiate(parts[2].gameObject);
            instances.Add(leafCopy);
            var leaf = leafCopy.GetComponent<PlacedObject>();
            ImportedModelParts.ReidentifyDuplicate(leaf, parts[2]);
            Require(!leaf.modelRoot && ImportedModelParts.Resolve(source.transform, leaf.sourceNodePath).name == parts[2].name,
                "Single part duplicate resolves only its original subtree");

            source.transform.GetChild(0).GetChild(0).localPosition += Vector3.up;
            bool changedRejected = false;
            try { ImportedModelParts.ValidateSource(source.transform, root.sourceSignature); }
            catch (InvalidOperationException) { changedRejected = true; }
            Require(changedRejected, "Same-name source pose changes are detected");
            source.transform.GetChild(0).GetChild(0).name = "Replacement";
            bool missingRejected = false;
            try { ImportedModelParts.Resolve(source.transform, parts[2].partNodePath); }
            catch (InvalidOperationException) { missingRejected = true; }
            Require(missingRejected, "Changed source is rejected rather than silently rebinding");
            Require(EditorProjectMigration.TryRead("{\"schemaVersion\":5,\"objects\":[]}", out var legacy, out _) &&
                legacy.schemaVersion == 6, "Legacy project migration accepted");
            Debug.Log("[Part import] All checks passed: identity, hierarchy, visibility/lock, deletion, duplication and persistence.");
        }
        finally
        {
            foreach (var instance in instances) if (instance) UnityEngine.Object.DestroyImmediate(instance);
            UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);
        }
    }

    static void Require(bool result, string label)
    {
        if (!result) throw new InvalidOperationException("[Part import] " + label);
    }

    static void CheckPlacement(GameObject source, List<GameObject> instances)
    {
        PlacedObject placed = null;
        GameObject created = null;
        var command = new PlaceObjectCommand("Imported/PlacementCheck", new Vector3(12f, 0f, 13f),
            Quaternion.identity, type =>
            {
                created = PlacementObjectFactory.Create(source, type, out placed);
                instances.Add(created);
                return created;
            });
        Require(command.Do() && created && placed, "Actual placement pipeline succeeds for meshless root/group");
        Require(PlacedObjectGrounding.TryGetRendererBounds(created.transform, out var bounds) &&
            Mathf.Abs(bounds.min.y - EditWorkspace.GroundY) < 0.0001f, "Model bottom rests on floor");
        Require(Mathf.Abs(created.transform.position.x - 12f) < 0.0001f &&
            Mathf.Abs(created.transform.position.z - 13f) < 0.0001f, "Requested horizontal position retained");
        Require(SelectionService.CanEdit(placed), "Placed root can be selected and edited");
        foreach (var filter in created.GetComponentsInChildren<MeshFilter>())
        {
            var part = filter.GetComponent<PlacedObject>();
            var collider = filter.GetComponent<Collider>();
            Require(part && SelectionService.CanEdit(part) && collider && collider.enabled,
                "Geometry has selectable part and enabled collider");
        }
        Vector3 groundedPosition = created.transform.position;
        var move = new MoveObjectCommand(created, groundedPosition, groundedPosition + Vector3.right);
        Require(move.Do() && created.transform.position == groundedPosition + Vector3.right && move.Undo() &&
            created.transform.position == groundedPosition, "Placed model move and undo");
        string id = placed.id;
        Require(command.Undo() && !created.activeSelf && command.Do() && placed.id == id &&
            created.transform.position == groundedPosition, "Placement redo retains identity and grounding");
    }
}
