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
        var options = new List<Option>();

        foreach (var placed in Object.FindObjectsOfType<PlacedObject>())
        {
            options.Add(new Option
            {
                id = placed.id,
                label = placed.typeId
            });
        }

        return options.OrderBy(o => o.label).ToList();
    }
}
