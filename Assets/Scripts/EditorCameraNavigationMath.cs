using UnityEngine;

internal static class EditorCameraNavigationMath
{
    public static void GetFocusTarget(PlacedObject placedObject, out Vector3 center, out float radius)
    {
        var renderers = placedObject.GetComponentsInChildren<Renderer>(true);
        Bounds bounds = new Bounds(placedObject.transform.position, Vector3.one);
        bool hasBounds = false;
        foreach (var renderer in renderers)
        {
            if (renderer == null) continue;
            if (!hasBounds)
            {
                bounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        center = hasBounds ? bounds.center : placedObject.transform.position;
        radius = Mathf.Max(0.5f, hasBounds ? bounds.extents.magnitude : 0.5f);
    }

    public static void CalculateOrbit(
        float currentYaw,
        float currentPitch,
        Vector2 delta,
        float orbitSpeed,
        float minPitch,
        float maxPitch,
        out float yaw,
        out float pitch)
    {
        yaw = currentYaw + delta.x * (orbitSpeed * 0.02f);
        pitch = Mathf.Clamp(currentPitch - (delta.y * orbitSpeed * 0.02f), minPitch, maxPitch);
    }

    public static bool TryCalculateHorizontalPan(
        Vector3 cameraRight,
        Vector3 cameraForward,
        float cameraDistance,
        float minDistance,
        float panSpeed,
        Vector2 delta,
        out Vector3 move)
    {
        Vector3 right = Vector3.ProjectOnPlane(cameraRight, Vector3.up).normalized;
        Vector3 forward = Vector3.ProjectOnPlane(cameraForward, Vector3.up).normalized;
        if (right.sqrMagnitude <= 0.0001f || forward.sqrMagnitude <= 0.0001f)
        {
            move = default;
            return false;
        }

        float distance = Mathf.Max(minDistance, cameraDistance);
        float scaledPanSpeed = panSpeed * distance * 0.1f;
        move = ((-delta.x * right) + (-delta.y * forward)) * scaledPanSpeed;
        return true;
    }

    public static float NormalizeScroll(float rawScrollY)
    {
        return Mathf.Abs(rawScrollY) > 1f ? rawScrollY / 120f : rawScrollY;
    }

    public static float CalculateOrthographicSize(
        float currentSize,
        float scroll,
        float zoomSpeed,
        float minOrthographicSize,
        float maxOrthographicSize)
    {
        float minSize = Mathf.Max(0.01f, minOrthographicSize);
        float maxSize = Mathf.Max(minSize, maxOrthographicSize);
        float zoomFactor = Mathf.Exp(-scroll * zoomSpeed);
        return Mathf.Clamp(currentSize * zoomFactor, minSize, maxSize);
    }

    public static float CalculatePerspectiveDistance(
        float currentDistance,
        float scroll,
        float zoomSpeed,
        float minDistance,
        float maxDistance)
    {
        currentDistance = Mathf.Max(0.0001f, currentDistance);
        return Mathf.Clamp(currentDistance - (scroll * zoomSpeed), minDistance, maxDistance);
    }

    public static float CalculatePerspectiveDistanceForSize(
        float orthographicSize,
        float fieldOfView,
        float minDistance,
        float maxDistance)
    {
        return Mathf.Clamp(
            orthographicSize / Mathf.Tan(fieldOfView * 0.5f * Mathf.Deg2Rad),
            minDistance,
            maxDistance);
    }

    public static float CalculateOrthographicSizeForDistance(
        float distance,
        float fieldOfView,
        float minOrthographicSize,
        float maxOrthographicSize)
    {
        return Mathf.Clamp(
            distance * Mathf.Tan(fieldOfView * 0.5f * Mathf.Deg2Rad),
            minOrthographicSize,
            maxOrthographicSize);
    }

    public static float CalculateOrthographicCameraDistance(
        float orthographicSize,
        float orthographicDistancePerSize,
        float minDistance,
        float maxDistance)
    {
        return Mathf.Clamp(
            orthographicSize * Mathf.Max(1f, orthographicDistancePerSize),
            minDistance,
            maxDistance);
    }

    public static float NormalizeAngle(float angle)
    {
        angle %= 360f;
        if (angle > 180f) angle -= 360f;
        return angle;
    }
}
