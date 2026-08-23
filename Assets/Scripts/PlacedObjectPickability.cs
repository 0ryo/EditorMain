using UnityEngine;

public static class PlacedObjectPickability
{
    const float MinColliderSize = 0.01f;

    public static bool EnsurePickable(PlacedObject placed, bool log = false)
    {
        if (placed == null) return false;

        var editState = placed.GetComponent<PlacedObjectEditState>();
        if (editState != null && (editState.Locked || editState.Hidden)) return false;

        var root = placed.transform;
        if (root == null) return false;

        if (HasUsableCollider(root)) return false;

        if (!TryBuildRendererBounds(root, out var bounds))
        {
            if (log)
            {
                Debug.LogWarning($"[Pickable] Collider auto-fix skipped. renderer not found: {placed.name}");
            }
            return false;
        }

        var box = root.GetComponent<BoxCollider>();
        if (box == null)
        {
            box = root.gameObject.AddComponent<BoxCollider>();
        }

        var localCenter = root.InverseTransformPoint(bounds.center);
        var localSize = root.InverseTransformVector(bounds.size);
        localSize = new Vector3(
            Mathf.Max(Mathf.Abs(localSize.x), MinColliderSize),
            Mathf.Max(Mathf.Abs(localSize.y), MinColliderSize),
            Mathf.Max(Mathf.Abs(localSize.z), MinColliderSize));

        box.center = localCenter;
        box.size = localSize;
        box.isTrigger = false;
        box.enabled = true;

        if (log)
        {
            Debug.Log($"[Pickable] Added BoxCollider: id={placed.id}, type={placed.typeId}, name={placed.name}");
        }

        return true;
    }

    public static bool HasUsableCollider(Transform root)
    {
        if (root == null) return false;

        var colliders = root.GetComponentsInChildren<Collider>(true);
        foreach (var collider in colliders)
        {
            if (collider == null) continue;
            if (!collider.enabled) continue;
            if (collider.isTrigger) continue;
            return true;
        }

        return false;
    }

    static bool TryBuildRendererBounds(Transform root, out Bounds bounds)
    {
        bounds = default;

        var renderers = root.GetComponentsInChildren<Renderer>(true);
        bool initialized = false;
        foreach (var renderer in renderers)
        {
            if (renderer == null) continue;

            if (!initialized)
            {
                bounds = renderer.bounds;
                initialized = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        return initialized;
    }
}
