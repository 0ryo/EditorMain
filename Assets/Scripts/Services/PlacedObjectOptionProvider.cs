using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class PlacedObjectOptionProvider
{
    public struct Option
    {
        public string id;
        public string label;
    }

    public static List<Option> GetOptions()
    {
        var byId = new Dictionary<string, Option>();
        var allPlaced = Object.FindObjectsByType<PlacedObject>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

        foreach (var placed in allPlaced)
        {
            if (placed == null) continue;

            placed.EnsureHasId();
            if (string.IsNullOrWhiteSpace(placed.id)) continue;

            var option = new Option
            {
                id    = placed.id,
                label = ImportedModelParts.Label(placed)
            };

            byId[option.id] = option;
        }

        var result = byId.Values
            .OrderBy(o => o.label)
            .ToList();

        return result;
    }

    public static string BuildSignature(List<Option> options)
    {
        if (options == null || options.Count == 0) return "empty";
        return string.Join("|", options.Select(o => $"{o.id}:{o.label}"));
    }

    // Each click goes one level further in, along the path from the model (or flat group) to the clicked part:
    // a new model starts whole, the next click picks the part or group just inside it, and so on down to the
    // clicked part. Clicking elsewhere in the same model goes one level below what the two have in common.
    public static PlacedObject ScreenCandidate(PlacedObject picked, PlacedObject currentTarget)
    {
        if (!picked) return null;
        var root = SelectionGroups.PickRoot(picked);
        if (!currentTarget || SelectionGroups.PickRoot(currentTarget) != root) return root;
        var path = new List<PlacedObject>();
        for (var node = picked.transform; node != null; node = node.parent)
        {
            var placed = node.GetComponent<PlacedObject>();
            if (placed != null) path.Insert(0, placed);
            if (placed == root) break;
        }
        int shared = -1;
        for (int i = 0; i < path.Count; i++)
            if (currentTarget == path[i] || currentTarget.transform.IsChildOf(path[i].transform)) shared = i;
        return shared < 0 ? root : path[Mathf.Min(shared + 1, path.Count - 1)];
    }
}
