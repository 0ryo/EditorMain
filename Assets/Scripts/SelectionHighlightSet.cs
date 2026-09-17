using System.Collections.Generic;
using UnityEngine;

// All selected objects share two render textures, including the primary selection.
public sealed class SelectionHighlightSet
{
    ObjectCandidatePreview silhouettes;
    readonly List<PlacedObject> visible = new();

    public void Refresh(SelectionService owner, IReadOnlyList<PlacedObject> selected)
    {
        visible.Clear();
        foreach (var item in selected) if (SelectionService.CanEdit(item)) visible.Add(item);
        if (visible.Count == 0)
        {
            if (silhouettes) silhouettes.Clear();
            return;
        }
        if (!silhouettes) silhouettes = owner.gameObject.AddComponent<ObjectCandidatePreview>();
        silhouettes.ShowSelection(visible);
    }

    public void Clear()
    {
        if (silhouettes)
        {
            silhouettes.Clear();
            Object.Destroy(silhouettes);
            silhouettes = null;
        }
        visible.Clear();
    }
}
