using System;
using System.Collections.Generic;
using UnityEngine;

public static class ViewportOutlinerData
{
    public static List<PlacedObject> CollectSorted(out int sourceCount)
    {
        PlacedObject[] found = UnityEngine.Object.FindObjectsByType<PlacedObject>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);
        var placedObjects = new List<PlacedObject>(found.Length);
        sourceCount = found.Length;
        foreach (var placed in found)
        {
            if (placed == null || !placed.gameObject.scene.IsValid()) continue;
            placedObjects.Add(placed);
        }

        placedObjects.Sort(ComparePlacedObjects);
        return placedObjects;
    }

    public static int CalculateSignature(IReadOnlyList<PlacedObject> placedObjects, int sourceCount)
    {
        unchecked
        {
            int signature = sourceCount;
            for (int i = 0; i < placedObjects.Count; i++)
            {
                var placed = placedObjects[i];
                if (placed == null || !placed.gameObject.scene.IsValid()) continue;
                signature ^= BuildItemSignature(placed);
            }
            return signature;
        }
    }

    public static int CalculateCurrentSignature()
    {
        PlacedObject[] placedObjects = UnityEngine.Object.FindObjectsByType<PlacedObject>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);
        return CalculateSignature(placedObjects, placedObjects.Length);
    }

    public static bool MatchesSearch(PlacedObject placed, string query)
    {
        if (placed == null || string.IsNullOrWhiteSpace(query)) return true;
        return ContainsIgnoreCase(placed.GetDisplayName(), query)
            || ContainsIgnoreCase(placed.Id, query)
            || ContainsIgnoreCase(placed.TypeId, query);
    }

    public static string SafeName(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "Object";
        return value.Replace('/', '_').Replace('\\', '_').Replace(' ', '_');
    }

    static int BuildItemSignature(PlacedObject placed)
    {
        int itemSignature = placed.GetInstanceID();
        var state = placed.GetComponent<PlacedObjectEditState>();
        if (state != null)
        {
            itemSignature = itemSignature * 397 ^ (state.Hidden ? 1 : 0);
            itemSignature = itemSignature * 397 ^ (state.Locked ? 1 : 0);
        }
        return itemSignature;
    }

    static int ComparePlacedObjects(PlacedObject left, PlacedObject right)
    {
        string leftName = left != null ? left.GetDisplayName() : string.Empty;
        string rightName = right != null ? right.GetDisplayName() : string.Empty;
        int displayComparison = string.Compare(
            leftName,
            rightName,
            StringComparison.CurrentCultureIgnoreCase);
        if (displayComparison != 0) return displayComparison;
        return string.Compare(left?.Id, right?.Id, StringComparison.OrdinalIgnoreCase);
    }

    static bool ContainsIgnoreCase(string source, string query)
    {
        return !string.IsNullOrEmpty(source)
            && source.IndexOf(query, StringComparison.CurrentCultureIgnoreCase) >= 0;
    }
}
