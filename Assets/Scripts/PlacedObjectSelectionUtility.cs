using System;
using UnityEngine;

public static class PlacedObjectPicker
{
    public static bool TryPick(
        Ray ray,
        LayerMask pickMask,
        out PlacedObject picked,
        out bool hitSomething,
        out bool usedMaskFallback)
    {
        picked = null;
        hitSomething = false;
        usedMaskFallback = false;

        RaycastHit[] hits = Physics.RaycastAll(ray, 1000f, ~0, QueryTriggerInteraction.Collide);
        if (hits == null || hits.Length == 0) return false;
        hitSomething = true;

        Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));

        PlacedObject fallback = null;
        foreach (var hit in hits)
        {
            Collider collider = hit.collider;
            if (collider == null) continue;

            PlacedObject placed = collider.GetComponentInParent<PlacedObject>();
            if (placed == null) continue;
            if (!SelectionService.CanEdit(placed)) continue;

            if (fallback == null) fallback = placed;

            if (IsLayerIncluded(collider.gameObject.layer, pickMask) ||
                IsLayerIncluded(placed.gameObject.layer, pickMask))
            {
                picked = placed;
                return true;
            }
        }

        if (fallback == null) return false;

        picked = fallback;
        usedMaskFallback = true;
        return true;
    }

    static bool IsLayerIncluded(int layer, LayerMask mask)
    {
        return (mask.value & (1 << layer)) != 0;
    }
}

public static class PlacedObjectRestoreFactory
{
    public static GameObject Create(
        string typeId,
        PrefabRegistry registry,
        PlacementController placementController)
    {
        if (registry != null)
        {
            PrefabEntry entry = registry.entries.Find(item => item.typeId == typeId);
            if (entry != null && entry.prefab != null)
            {
                return Instantiate(entry.prefab, typeId);
            }
        }

        if (placementController != null && placementController.TryGetPrefab(typeId, out var runtimePrefab))
        {
            return Instantiate(runtimePrefab, typeId);
        }

        return null;
    }

    static GameObject Instantiate(GameObject prefab, string typeId)
    {
        if (prefab == null || string.IsNullOrWhiteSpace(typeId)) return null;

        GameObject created = UnityEngine.Object.Instantiate(prefab);
        PlacedObject placed = created.GetComponent<PlacedObject>();
        if (placed == null) placed = created.AddComponent<PlacedObject>();

        placed.InitType(typeId);
        PlacedObjectPickability.EnsurePickable(placed, true);
        return created;
    }
}
