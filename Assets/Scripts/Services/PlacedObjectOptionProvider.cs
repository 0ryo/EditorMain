using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class PlacedObjectOptionProvider
{
    static string lastLoggedSignature = string.Empty;

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
                id = placed.id,
                label = placed.id
            };

            byId[option.id] = option;
        }

        var result = byId.Values
            .OrderBy(o => o.label)
            .ToList();

        var signature = BuildSignature(result);
        if (signature != lastLoggedSignature)
        {
            lastLoggedSignature = signature;
            if (result.Count == 0)
            {
                Debug.Log("[PlacedObjectOptionProvider] options=0");
            }
            else
            {
                Debug.Log($"[PlacedObjectOptionProvider] options={result.Count} ids={string.Join(", ", result.Select(o => o.id))}");
            }
        }

        return result;
    }

    public static string BuildSignature(List<Option> options)
    {
        if (options == null || options.Count == 0) return "empty";
        return string.Join("|", options.Select(o => $"{o.id}:{o.label}"));
    }
}
