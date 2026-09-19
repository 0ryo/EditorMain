using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// Session-local data only: copying never creates hidden PlacedObjects in the scene.
internal sealed class SelectionClipboard
{
    readonly List<string> snapshots = new();
    int pasteCount;
    public bool HasContent => snapshots.Count > 0;

    public int Copy(IEnumerable<PlacedObject> selection)
    {
        var next = selection.Where(SelectionService.CanEdit).Select(Capture).Select(JsonUtility.ToJson).ToList();
        if (next.Count == 0) return 0;
        snapshots.Clear();
        snapshots.AddRange(next);
        pasteCount = 0;
        return next.Count;
    }

    public PasteSelectionCommand CreateCommand(PlacementController placement, PrefabRegistry registry, bool inPlace)
    {
        return new PasteSelectionCommand(snapshots, placement, registry,
            inPlace ? Vector3.zero : new Vector3(0.2f, 0f, 0.2f) * (pasteCount + 1));
    }

    public void DidPaste(bool inPlace) { if (!inPlace) pasteCount++; }

    static EditorProjectObject Capture(PlacedObject source)
    {
        string path = source.sourceNodePath;
        if (source.modelRoot != null)
        {
            string parentPath = source.modelRoot.sourceNodePath;
            path = string.IsNullOrEmpty(parentPath) ? source.partNodePath : parentPath + "/" + source.partNodePath;
        }
        var data = new EditorProjectObject
        {
            editorGroupId = source.editorGroupId,
            typeId = source.typeId, sourceNodePath = path, sourceSignature = source.sourceSignature,
            displayName = source.displayName, description = source.description,
            hasDescriptionOverride = source.hasDescriptionOverride,
            position = source.transform.position, rotation = source.transform.rotation, scale = source.transform.lossyScale
        };
        foreach (var part in source.GetComponentsInChildren<PlacedObject>(true))
        {
            if (part == source) continue;
            var state = part.GetComponent<PlacedObjectEditState>();
            data.parts.Add(new ModelPartState
            {
                editorGroupId = part.editorGroupId,
                nodePath = ImportedModelParts.PathFrom(source.transform, part.transform),
                sourceSignature = part.sourceSignature, displayName = part.displayName,
                description = part.description, hasDescriptionOverride = part.hasDescriptionOverride,
                localPosition = part.transform.localPosition, localRotation = part.transform.localRotation,
                localScale = part.transform.localScale, active = part.gameObject.activeSelf,
                hidden = state != null && state.Hidden, locked = state != null && state.Locked
            });
        }
        return data;
    }
}

internal sealed class PasteSelectionCommand : IEditorCommand, IDiscardableEditorCommand
{
    readonly List<string> snapshots;
    readonly PlacementController placement;
    readonly PrefabRegistry registry;
    readonly Vector3 offset;
    readonly List<PlacedObject> results = new();
    public string Label => "Paste selection";
    public IReadOnlyList<PlacedObject> Results => results;

    public PasteSelectionCommand(IEnumerable<string> snapshots, PlacementController placement, PrefabRegistry registry, Vector3 offset)
    {
        this.snapshots = snapshots.ToList();
        this.placement = placement;
        this.registry = registry;
        this.offset = offset;
    }

    public bool Do()
    {
        if (snapshots.Count == 0) return false;
        if (results.Count == 0) CreateAll();
        if (results.Any(item => item == null)) return false;
        foreach (var item in results) item.gameObject.SetActive(true);
        return true;
    }

    void CreateAll()
    {
        var staging = new GameObject("PasteStaging_Runtime");
        staging.SetActive(false);
        try
        {
            foreach (string snapshot in snapshots)
            {
                var data = JsonUtility.FromJson<EditorProjectObject>(snapshot);
                GameObject prefab = null;
                if (placement != null) placement.TryGetPrefab(data.typeId, out prefab);
                if (prefab == null && registry != null)
                    prefab = registry.entries.FirstOrDefault(entry => entry != null && entry.typeId == data.typeId)?.prefab;
                if (prefab == null) throw new InvalidOperationException("コピー元のモデルを利用できません: " + data.typeId);
                var source = ImportedModelParts.Resolve(prefab.transform, data.sourceNodePath);
                ImportedModelParts.ValidateSource(source, data.sourceSignature);
                var instance = UnityEngine.Object.Instantiate(source.gameObject, staging.transform, false);
                instance.SetActive(false);
                var placed = instance.GetComponent<PlacedObject>();
                if (placed == null) placed = instance.AddComponent<PlacedObject>();
                results.Add(placed);
                placed.editorGroupId = data.editorGroupId;
                placed.typeId = data.typeId;
                placed.modelRoot = null;
                placed.partNodePath = null;
                placed.sourceNodePath = data.sourceNodePath;
                placed.sourceSignature = data.sourceSignature;
                placed.displayName = data.displayName;
                placed.description = data.description;
                placed.hasDescriptionOverride = data.hasDescriptionOverride;
                placed.ForceNewId();
                foreach (var part in data.parts) part.id = placed.id + "/part-" + Guid.NewGuid().ToString("N");
                ImportedModelParts.Restore(placed, data.parts);
                instance.transform.SetPositionAndRotation(data.position + offset, data.rotation);
                instance.transform.localScale = data.scale;
                PlacedObjectPickability.EnsurePickable(placed, true);
                var edit = instance.GetComponent<PlacedObjectEditState>();
                if (edit == null) edit = instance.AddComponent<PlacedObjectEditState>();
                edit.SetLocked(false);
                edit.SetVisible(true);
                edit.RefreshSubtree();
            }
            SelectionGroups.Reidentify(results);
            // Publish only after the whole selection is restored successfully.
            foreach (var item in results) item.transform.SetParent(null, true);
        }
        catch
        {
            foreach (var item in results)
                if (item != null) { item.gameObject.SetActive(false); UnityEngine.Object.Destroy(item.gameObject); }
            results.Clear();
            throw;
        }
        finally { UnityEngine.Object.Destroy(staging); }
    }

    public bool Undo()
    {
        if (results.Count == 0 || results.Any(item => item == null)) return false;
        foreach (var item in results) item.gameObject.SetActive(false);
        return true;
    }

    public void Discard()
    {
        foreach (var item in results)
            if (item != null && !item.gameObject.activeSelf) UnityEngine.Object.Destroy(item.gameObject);
    }
}
