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
                label = placed.GetDisplayName()
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
}
