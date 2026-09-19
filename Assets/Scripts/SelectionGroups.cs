using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// A flat editor selection group. Model parentage and all scenario IDs remain intact.
public static class SelectionGroups
{
    public static IEnumerable<PlacedObject> Expand(IEnumerable<PlacedObject> source)
    {
        var items = source.Where(item => item != null).ToList();
        var ids = new HashSet<string>(items.Select(item => item.editorGroupId).Where(id => !string.IsNullOrEmpty(id)));
        if (ids.Count == 0) return items;
        return items.Concat(UnityEngine.Object.FindObjectsByType<PlacedObject>(FindObjectsSortMode.None)
            .Where(item => ids.Contains(item.editorGroupId))).Distinct();
    }

    public static void Reidentify(IEnumerable<PlacedObject> roots)
    {
        var ids = new Dictionary<string, string>();
        foreach (var item in roots.Where(root => root != null).SelectMany(root => root.GetComponentsInChildren<PlacedObject>(true)).Distinct())
        {
            if (string.IsNullOrEmpty(item.editorGroupId)) continue;
            if (!ids.TryGetValue(item.editorGroupId, out var fresh))
                ids[item.editorGroupId] = fresh = Guid.NewGuid().ToString("N");
            item.editorGroupId = fresh;
        }
    }
}

internal sealed class SelectionGroupCommand : IEditorCommand
{
    readonly List<PlacedObject> items;
    readonly List<string> before;
    readonly string after;
    public string Label => string.IsNullOrEmpty(after) ? "Ungroup objects" : "Group objects";
    public SelectionGroupCommand(IEnumerable<PlacedObject> items, string group)
    { this.items = items.ToList(); before = this.items.Select(item => item.editorGroupId).ToList(); after = group; }
    public bool Do() => Apply(false);
    public bool Undo() => Apply(true);
    bool Apply(bool undo)
    {
        if (items.Count == 0 || items.Any(item => item == null)) return false;
        for (int i = 0; i < items.Count; i++) items[i].editorGroupId = undo ? before[i] : after;
        return true;
    }
}
