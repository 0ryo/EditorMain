using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public static class EditWorkspace
{
    public const float GroundY = 0f;
    public static readonly Vector3 DefaultCameraPosition = new Vector3(0f, 6f, -10f);

    static readonly Plane GroundPlane = new Plane(Vector3.up, new Vector3(0f, GroundY, 0f));

    public static Camera ResolveCamera(Camera preferred = null)
    {
        if (preferred != null) return preferred;
        if (Camera.main != null) return Camera.main;
        return Object.FindFirstObjectByType<Camera>();
    }

    public static void EnsureWorkspaceVisuals()
    {
        WorkspaceFloorGrid.EnsureExists();
    }

    public static bool TryScreenToGround(Camera camera, Vector2 screenPosition, out Vector3 point, out string reason)
    {
        point = default;
        reason = string.Empty;

        camera = ResolveCamera(camera);
        if (camera == null)
        {
            reason = "camera missing";
            return false;
        }

        var ray = camera.ScreenPointToRay(screenPosition);
        if (GroundPlane.Raycast(ray, out var enter) && enter >= 0f)
        {
            point = ray.GetPoint(enter);
            reason = "workspace plane";
            return true;
        }

        point = new Vector3(camera.transform.position.x, GroundY, camera.transform.position.z);
        reason = "camera projection fallback";
        return true;
    }

    public static Vector3 SnapPlacementPoint(Vector3 groundPoint, float gridSize, float yOffset)
    {
        float snap = Mathf.Max(0.0001f, gridSize);
        return new Vector3(
            Mathf.Round(groundPoint.x / snap) * snap,
            groundPoint.y + yOffset,
            Mathf.Round(groundPoint.z / snap) * snap);
    }

    public static bool TryGetBlockingUiName(Vector2 screenPosition, string[] blockingNames, out string blockingUiName)
    {
        blockingUiName = null;
        if (blockingNames == null || blockingNames.Length == 0) return false;

        foreach (var rect in Object.FindObjectsByType<RectTransform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (rect == null || !IsNamedBlockingUiRect(rect.name, blockingNames)) continue;
            if (!RectTransformUtility.RectangleContainsScreenPoint(rect, screenPosition, null)) continue;

            blockingUiName = rect.name;
            return true;
        }

        return false;
    }

    public static bool IsTypingIntoInputField()
    {
        if (EventSystem.current == null) return false;

        var selected = EventSystem.current.currentSelectedGameObject;
        if (selected == null) return false;

        var legacyInput = selected.GetComponent<InputField>() ?? selected.GetComponentInParent<InputField>();
        if (legacyInput != null && legacyInput.isFocused) return true;

        var tmpInput = selected.GetComponent<TMP_InputField>() ?? selected.GetComponentInParent<TMP_InputField>();
        return tmpInput != null && tmpInput.isFocused;
    }

    static bool IsNamedBlockingUiRect(string objectName, string[] blockingNames)
    {
        if (string.IsNullOrWhiteSpace(objectName)) return false;

        foreach (var blockingName in blockingNames)
        {
            if (string.Equals(objectName, blockingName, System.StringComparison.Ordinal)) return true;
        }

        return false;
    }
}
