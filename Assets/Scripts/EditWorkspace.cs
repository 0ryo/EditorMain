using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public static class EditWorkspace
{
    public const float GroundY = 0f;
    public static readonly Vector3 DefaultCameraPosition = new Vector3(0f, 6f, -10f);
    public static readonly Color BackgroundColor = new Color32(0x42, 0x42, 0x42, 0xFF);

    static readonly Plane GroundPlane = new Plane(Vector3.up, new Vector3(0f, GroundY, 0f));
    static readonly List<RaycastResult> UiRaycastResults = new List<RaycastResult>();
    static Camera cachedResolvedCamera;
    public static bool HasOpenModal => GetOpenModalRoot() != null;

    public static Transform GetOpenModalRoot(Transform within = null)
    {
        var modal = EditorProjectPanel.GetOpenModalRoot(within);
        if (modal != null) return modal;
        modal = CatalogUI.GetOpenModalRoot();
        return modal != null && (within == null || modal == within || modal.IsChildOf(within)) ? modal : null;
    }
    public static void EnsureInputBlockers(Transform uiRoot)
    {
        if (uiRoot == null) return;

        // Raycastable uGUI graphics already define the interactive screen boundary.
        // Marking those objects avoids using hierarchy names as an input API.
        foreach (var graphic in uiRoot.GetComponentsInChildren<Graphic>(true))
        {
            if (graphic == null || !graphic.raycastTarget) continue;
            var blockerRoot = graphic.transform;
            for (var current = graphic.transform.parent; current != null; current = current.parent)
            {
                var ancestorGraphic = current.GetComponent<Graphic>();
                if (ancestorGraphic != null && ancestorGraphic.raycastTarget) blockerRoot = current;
                if (current == uiRoot) break;
            }
            EnsureInputBlocker(blockerRoot);
        }

        foreach (var selectable in uiRoot.GetComponentsInChildren<Selectable>(true))
        {
            if (selectable == null) continue;
            if (selectable is Button button) UiAccessibilityMetrics.EnsureButtonTarget(button);
            else EnsureInputBlockerForControl(selectable.transform);
        }
    }

    public static void EnsureInputBlockerForControl(Transform target)
    {
        if (target == null || target.GetComponentInParent<EditorUiInputBlocker>() != null) return;
        EnsureInputBlocker(target);
    }

    public static void EnsureInputBlocker(Transform target)
    {
        if (target != null && target.GetComponent<EditorUiInputBlocker>() == null)
            target.gameObject.AddComponent<EditorUiInputBlocker>();
    }

    public static Camera ResolveCamera(Camera preferred = null)
    {
        if (preferred != null) return preferred;
        if (cachedResolvedCamera != null && cachedResolvedCamera.isActiveAndEnabled && cachedResolvedCamera.gameObject.activeInHierarchy)
            return cachedResolvedCamera;

        cachedResolvedCamera = Camera.main;
        if (cachedResolvedCamera == null)
            cachedResolvedCamera = Object.FindFirstObjectByType<Camera>();
        return cachedResolvedCamera;
    }

    public static void EnsureWorkspaceVisuals()
    {
        var camera = ResolveCamera();
        if (camera != null)
        {
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = BackgroundColor;
            ViewportLightingController.Ensure(camera);
        }

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

    public static bool TryScreenToPlacedSurface(Camera camera, Vector2 screenPosition, out Vector3 point)
    {
        point = default;
        camera = ResolveCamera(camera);
        if (camera == null) return false;

        var hits = Physics.RaycastAll(camera.ScreenPointToRay(screenPosition), 1000f, ~0, QueryTriggerInteraction.Ignore);
        if (hits == null || hits.Length == 0) return false;
        System.Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));

        foreach (var hit in hits)
        {
            if (hit.collider == null || hit.normal.y < 0.5f) continue;
            var placed = hit.collider.GetComponentInParent<PlacedObject>();
            if (placed == null || !SelectionService.CanEdit(placed)) continue;
            point = hit.point;
            return true;
        }

        return false;
    }

    public static Vector3 SnapPlacementPoint(Vector3 groundPoint, float gridSize, float yOffset)
    {
        _ = yOffset; // 旧Prefabとのserialized互換用。Y位置は配置後にrenderer boundsから決定する。
        if (!EditSnapSettings.ShouldSnap)
        {
            return new Vector3(groundPoint.x, GroundY, groundPoint.z);
        }

        float snap = Mathf.Max(0.0001f, gridSize);
        return new Vector3(
            Mathf.Round(groundPoint.x / snap) * snap,
            GroundY,
            Mathf.Round(groundPoint.z / snap) * snap);
    }

    public static bool TryGetBlockingUiName(Vector2 screenPosition, out string blockingUiName)
    {
        blockingUiName = null;
        if (HasOpenModal)
        {
            blockingUiName = "Editor modal";
            return true;
        }
        if (SkillSyncEditorController.Active != null && SkillSyncEditorController.Active.BlocksWorkspace(screenPosition))
        {
            blockingUiName = "SkillSyncDesign";
            return true;
        }
        var eventSystem = EventSystem.current;
        if (eventSystem == null) return false;

        UiRaycastResults.Clear();
        eventSystem.RaycastAll(new PointerEventData(eventSystem) { position = screenPosition }, UiRaycastResults);

        foreach (var result in UiRaycastResults)
        {
            for (var current = result.gameObject != null ? result.gameObject.transform : null;
                 current != null;
                 current = current.parent)
            {
                if (current.GetComponent<EditorUiInputBlocker>() != null)
                {
                    blockingUiName = "Editor UI";
                    UiRaycastResults.Clear();
                    return true;
                }

            }
        }

        UiRaycastResults.Clear();
        return false;
    }

    public static bool IsTypingIntoInputField()
    {
        // A visible project modal captures editing shortcuts even when its input field is unfocused.
        if (!Application.isFocused || HasOpenModal) return true;
        if (SkillSyncEditorController.Active != null && SkillSyncEditorController.Active.CapturesTextSensitiveInput) return true;
        if (ObjectScreenPicker.Capturing) return true;
        if (EventSystem.current == null) return false;

        var selected = EventSystem.current.currentSelectedGameObject;
        if (selected == null) return false;

        var legacyInput = selected.GetComponent<InputField>() ?? selected.GetComponentInParent<InputField>();
        if (legacyInput != null && legacyInput.isFocused) return true;

        var tmpInput = selected.GetComponent<TMP_InputField>() ?? selected.GetComponentInParent<TMP_InputField>();
        return tmpInput != null && tmpInput.isFocused;
    }

}
