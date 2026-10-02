using UnityEngine;

/// <summary>移動中の配置物を近接した配置物のAABB面へ吸着させる。</summary>
public static class PlacementObjectSnapper
{
    static readonly Collider[] NearbyColliders = new Collider[64];

    public static bool TrySnap(PlacedObject moving, float maximumDistance, Vector3 movementAxis, out Vector3 position)
    {
        position = moving != null ? moving.transform.position : Vector3.zero;
        if (moving == null || !PlacedObjectGrounding.TryGetRendererBounds(moving.transform, out var movingBounds))
            return false;

        float range = Mathf.Max(0f, maximumDistance);
        if (range <= 0f || movementAxis.sqrMagnitude < 0.0001f) return false;
        movementAxis.Normalize();

        int nearbyCount = Physics.OverlapBoxNonAlloc(movingBounds.center, movingBounds.extents + Vector3.one * range,
            NearbyColliders, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
        Collider[] overflowColliders = nearbyCount >= NearbyColliders.Length
            ? Physics.OverlapBox(movingBounds.center, movingBounds.extents + Vector3.one * range,
                Quaternion.identity, ~0, QueryTriggerInteraction.Ignore)
            : null;
        int colliderCount = overflowColliders != null ? overflowColliders.Length : nearbyCount;
        float bestDistance = range + 0.0001f;
        Vector3 bestDelta = Vector3.zero;
        bool found = false;
        var movingRoot = ResolveRoot(moving);

        for (int i = 0; i < colliderCount; i++)
        {
            var collider = overflowColliders != null ? overflowColliders[i] : NearbyColliders[i];
            if (collider == null) continue;
            var candidate = ResolveRoot(collider.GetComponentInParent<PlacedObject>());
            if (candidate == null || candidate == movingRoot || !SelectionService.CanEdit(candidate) ||
                !PlacedObjectGrounding.TryGetRendererBounds(candidate.transform, out var candidateBounds))
                continue;

            ConsiderAxisSnap(movingBounds, candidateBounds, movementAxis, range,
                ref bestDistance, ref bestDelta, ref found);
        }

        if (!found) return false;
        position += bestDelta;
        return true;
    }

    static void ConsiderAxisSnap(Bounds moving, Bounds candidate, Vector3 axis, float range,
        ref float bestDistance, ref Vector3 bestDelta, ref bool found)
    {
        Vector3 centerDelta = candidate.center - moving.center;
        float centerProjection = Vector3.Dot(centerDelta, axis);
        Vector3 perpendicularDelta = centerDelta - axis * centerProjection;
        Vector3 combinedExtents = moving.extents + candidate.extents + Vector3.one * range;
        if (Mathf.Abs(perpendicularDelta.x) > combinedExtents.x ||
            Mathf.Abs(perpendicularDelta.y) > combinedExtents.y ||
            Mathf.Abs(perpendicularDelta.z) > combinedExtents.z)
            return;

        Vector3 absoluteAxis = new Vector3(Mathf.Abs(axis.x), Mathf.Abs(axis.y), Mathf.Abs(axis.z));
        float combinedRadius = Vector3.Dot(moving.extents + candidate.extents, absoluteAxis);
        float towardPositive = centerProjection - combinedRadius;
        float towardNegative = centerProjection + combinedRadius;
        float delta = Mathf.Abs(towardPositive) <= Mathf.Abs(towardNegative) ? towardPositive : towardNegative;
        float distance = Mathf.Abs(delta);
        if (distance > range || distance >= bestDistance) return;

        bestDelta = axis * delta;
        bestDistance = distance;
        found = true;
    }

    static PlacedObject ResolveRoot(PlacedObject placed)
    {
        if (placed == null) return null;
        var root = placed;
        for (var parent = placed.transform.parent; parent != null; parent = parent.parent)
        {
            var ancestor = parent.GetComponent<PlacedObject>();
            if (ancestor != null) root = ancestor;
        }
        return root;
    }
}
