using System.Collections.Generic;
using UnityEngine;

public static class PlacementOverlapDetector
{
    const float MinimumPenetration = 0.005f;
    static readonly Collider[] NearbyColliders = new Collider[128];

    public static bool TryFindOverlap(PlacedObject target, out PlacedObject other)
    {
        return TryFindOverlap(target != null ? new[] { target } : null, out _, out other);
    }

    public static bool TryFindOverlap(IEnumerable<PlacedObject> targets, out PlacedObject target, out PlacedObject other)
    {
        target = null;
        other = null;
        if (targets == null) return false;

        var roots = new HashSet<PlacedObject>();
        foreach (var item in targets)
        {
            var root = ResolveRoot(item);
            if (root != null) roots.Add(root);
        }
        if (roots.Count == 0) return false;

        foreach (var moving in roots)
        {
            if (!IsVisible(moving) || !PlacedObjectGrounding.TryGetRendererBounds(moving.transform, out var movingBounds))
                continue;

            int nearbyCount = Physics.OverlapBoxNonAlloc(movingBounds.center, movingBounds.extents,
                NearbyColliders, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
            Collider[] overflowColliders = nearbyCount >= NearbyColliders.Length
                ? Physics.OverlapBox(movingBounds.center, movingBounds.extents,
                    Quaternion.identity, ~0, QueryTriggerInteraction.Ignore)
                : null;
            int colliderCount = overflowColliders != null ? overflowColliders.Length : nearbyCount;
            for (int i = 0; i < colliderCount; i++)
            {
                var collider = overflowColliders != null ? overflowColliders[i] : NearbyColliders[i];
                if (collider == null) continue;
                var candidateRoot = ResolveRoot(collider.GetComponentInParent<PlacedObject>());
                if (candidateRoot == null || candidateRoot == moving || roots.Contains(candidateRoot) || !IsVisible(candidateRoot) ||
                    !PlacedObjectGrounding.TryGetRendererBounds(candidateRoot.transform, out var candidateBounds)) continue;

                if (!HasPositiveIntersection(movingBounds, candidateBounds)) continue;
                target = moving;
                other = candidateRoot;
                return true;
            }
        }

        return false;
    }

    static PlacedObject ResolveRoot(PlacedObject placed)
    {
        if (placed == null) return null;
        for (var parent = placed.transform.parent; parent != null; parent = parent.parent)
        {
            var ancestor = parent.GetComponent<PlacedObject>();
            if (ancestor != null) return ancestor;
        }
        return placed;
    }

    static bool IsVisible(PlacedObject placed)
    {
        if (placed == null || !placed.gameObject.activeInHierarchy) return false;
        for (var node = placed.transform; node != null; node = node.parent)
        {
            var parentObject = node.GetComponent<PlacedObject>();
            if (parentObject != null && !PlacedObjectEditState.IsVisibleByCategory(parentObject)) return false;
            var editState = node.GetComponent<PlacedObjectEditState>();
            if (editState != null && editState.Hidden) return false;
        }
        return true;
    }

    static bool HasPositiveIntersection(Bounds a, Bounds b)
    {
        if (!a.Intersects(b)) return false;
        var overlap = Vector3.Min(a.max, b.max) - Vector3.Max(a.min, b.min);
        return overlap.x > MinimumPenetration && overlap.y > MinimumPenetration && overlap.z > MinimumPenetration;
    }
}
