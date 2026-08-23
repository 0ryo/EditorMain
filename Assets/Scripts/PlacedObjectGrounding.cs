using UnityEngine;

public static class PlacedObjectGrounding
{
    public static bool AlignRendererBoundsToGround(GameObject instance, float groundY, out Vector3 alignedPosition)
    {
        alignedPosition = instance != null ? instance.transform.position : Vector3.zero;
        if (instance == null) return false;

        if (!TryGetRendererBounds(instance.transform, out var bounds))
        {
            alignedPosition = new Vector3(alignedPosition.x, groundY, alignedPosition.z);
            instance.transform.position = alignedPosition;
            return false;
        }

        float verticalOffset = groundY - bounds.min.y;
        if (Mathf.Abs(verticalOffset) > 0.00001f)
        {
            instance.transform.position += Vector3.up * verticalOffset;
        }

        alignedPosition = instance.transform.position;
        return true;
    }

    public static bool TryGetRendererBounds(Transform root, out Bounds bounds)
    {
        bounds = default;
        if (root == null) return false;

        bool initialized = false;
        var renderers = root.GetComponentsInChildren<Renderer>(true);
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
