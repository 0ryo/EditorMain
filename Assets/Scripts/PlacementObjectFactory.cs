using System.Collections.Generic;
using UnityEngine;

public sealed class PlacementPrefabCatalog
{
    readonly Dictionary<string, GameObject> prefabs = new();

    public int Count => prefabs.Count;

    public void Rebuild(IEnumerable<PrefabEntry> entries)
    {
        prefabs.Clear();
        if (entries == null) return;

        foreach (var entry in entries)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.typeId) || entry.prefab == null) continue;
            if (!prefabs.ContainsKey(entry.typeId))
            {
                prefabs.Add(entry.typeId, entry.prefab);
            }
        }
    }

    public void Register(string typeId, GameObject prefab)
    {
        prefabs[typeId] = prefab;
    }

    public bool TryGet(string typeId, out GameObject prefab)
    {
        return prefabs.TryGetValue(typeId, out prefab) && prefab != null;
    }
}

public static class PlacementObjectFactory
{
    public static GameObject Create(GameObject sourcePrefab, string typeId, out PlacedObject placed)
    {
        placed = null;
        if (sourcePrefab == null) return null;

        var instance = UnityEngine.Object.Instantiate(sourcePrefab);
        try
        {
            if (!instance.activeSelf) instance.SetActive(true);

            placed = instance.GetComponent<PlacedObject>();
            if (placed == null) placed = instance.AddComponent<PlacedObject>();

            placed.InitType(typeId);
            placed.ForceNewId();
            ImportedModelParts.Register(placed);
            PlacedObjectPickability.EnsurePickable(placed, true);
            return instance;
        }
        catch
        {
            // Never leave a half-registered model in the world or autosave snapshot.
            instance.SetActive(false);
            if (Application.isPlaying) UnityEngine.Object.Destroy(instance);
            else UnityEngine.Object.DestroyImmediate(instance);
            placed = null;
            throw;
        }
    }
}
