using System.Collections.Generic;
using UnityEngine;

// Finds what the pointer rests on among placed parts: the top face of a part (so a ghost sits on a table, not the
// floor) and parts that are locked. A locked part has its colliders switched off, so it is found by its bounds.
public static class SkillSyncSurfaceProbe
{
    const float TopNormalY = 0.7f, TopTolerance = 0.01f, CacheSeconds = 0.5f;
    static readonly List<(PlacedObject part, Bounds bounds)> locked = new();
    static float cachedAt = -10f;

    public static void Invalidate() => cachedAt = -10f;

    static void Refresh()
    {
        if (Time.unscaledTime - cachedAt < CacheSeconds) return;
        cachedAt = Time.unscaledTime;
        locked.Clear();
        foreach (var state in Object.FindObjectsByType<PlacedObjectEditState>(FindObjectsSortMode.None))
        {
            var part = state != null && state.Locked ? state.GetComponent<PlacedObject>() : null;
            if (part == null || !IsLockedOnly(part)) continue;
            if (PlacedObjectGrounding.TryGetRendererBounds(part.transform, out var bounds)) locked.Add((part, bounds));
        }
    }

    // Locked and not hidden: a hidden part is not on screen, so it can neither be clicked nor stood on.
    public static bool IsLockedOnly(PlacedObject part)
    {
        if (part == null || !part.gameObject.activeInHierarchy) return false;
        bool isLocked = false;
        for (var node = part.transform; node != null; node = node.parent)
        {
            var placed = node.GetComponent<PlacedObject>();
            if (placed != null && !PlacedObjectEditState.IsVisibleByCategory(placed)) return false;
            var state = node.GetComponent<PlacedObjectEditState>();
            if (state == null) continue;
            if (state.Hidden) return false;
            isLocked |= state.Locked;
        }
        return isLocked;
    }

    // Nearest locked part the ray passes through, by distance to its bounds.
    public static bool TryFindLocked(Ray ray, out PlacedObject part, out float distance)
    {
        Refresh();
        part = null; distance = float.PositiveInfinity;
        foreach (var (candidate, bounds) in locked)
        {
            if (candidate == null || !candidate.gameObject.activeInHierarchy) continue;
            if (bounds.IntersectRay(ray, out float d) && d < distance) { distance = d; part = candidate; }
        }
        return part != null;
    }

    // The upward face under the pointer, if a part (not the floor) is the first thing the ray meets.
    public static bool TryFindTop(Ray ray, out Vector3 point)
    {
        point = default;
        float best = float.PositiveInfinity;
        var hits = Physics.RaycastAll(ray, 1000f, ~0, QueryTriggerInteraction.Ignore);
        foreach (var hit in hits)
        {
            if (hit.collider == null || hit.distance >= best) continue;
            var placed = hit.collider.GetComponentInParent<PlacedObject>();
            if (placed == null || !SelectionService.CanEdit(placed)) continue;
            best = hit.distance;
            if (hit.normal.y >= TopNormalY) point = hit.point; else point = default;
        }
        bool found = best < float.PositiveInfinity && point != default;
        Refresh();
        foreach (var (candidate, bounds) in locked)
        {
            if (candidate == null || !candidate.gameObject.activeInHierarchy) continue;
            if (!bounds.IntersectRay(ray, out float d) || d >= best) continue;
            var p = ray.GetPoint(d);
            if (Mathf.Abs(p.y - bounds.max.y) > TopTolerance) { best = d; found = false; continue; }
            best = d; point = p; found = true;
        }
        return found;
    }

    // Is there still a top face at this height under (x, z)? Used to keep a grid-snapped ghost on the part it points at.
    public static bool HasTopAt(float x, float z, float y)
    {
        var origin = new Vector3(x, y + 0.3f, z);
        if (Physics.Raycast(origin, Vector3.down, out var hit, 0.6f, ~0, QueryTriggerInteraction.Ignore))
        {
            var placed = hit.collider != null ? hit.collider.GetComponentInParent<PlacedObject>() : null;
            if (placed != null && SelectionService.CanEdit(placed) && hit.normal.y >= TopNormalY && Mathf.Abs(hit.point.y - y) <= TopTolerance * 2f) return true;
        }
        Refresh();
        foreach (var (candidate, bounds) in locked)
            if (candidate != null && Mathf.Abs(bounds.max.y - y) <= TopTolerance * 2f &&
                x >= bounds.min.x && x <= bounds.max.x && z >= bounds.min.z && z <= bounds.max.z) return true;
        return false;
    }
}
