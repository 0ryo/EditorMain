using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;

public class MoveTool : MonoBehaviour
{
    public Camera cam;
    public SelectionService sel;
    public float gridSize = 0.1f;

    [Header("Transform Gizmo")]
    public float gizmoLineWidth = 0.04f;
    public float gizmoMinAxisLength = 0.45f;
    public float gizmoAxisLengthMultiplier = 0.84f;
    public float moveHandlePickRadiusPixels = 10f;
    public float rotateHandleScale = 0.16f;
    [Range(0.2f, 0.9f)]
    public float rotateHandleDistanceRatio = 0.62f;
    public float rotateHandleMinDistance = 0.12f;
    public float rotateSnapDegrees = 15f;
    public Color moveAxisSelectedColor = new Color(0.2f, 1f, 1f, 1f);

    static readonly Vector3[] GizmoAxes = { Vector3.right, Vector3.up, Vector3.forward };
    static readonly Color[] GizmoColors =
    {
        new Color(0.96f, 0.31f, 0.31f, 1f),
        new Color(0.36f, 0.86f, 0.44f, 1f),
        new Color(0.35f, 0.59f, 0.96f, 1f)
    };

    enum GizmoAxis
    {
        None = -1,
        X = 0,
        Y = 1,
        Z = 2
    }

    enum GizmoDragMode
    {
        None = 0,
        Move = 1,
        Rotate = 2
    }

    Transform gizmoRoot;
    readonly LineRenderer[] axisRenderers = new LineRenderer[3];
    readonly Transform[] rotateMarkers = new Transform[3];
    readonly Collider[] rotateMarkerColliders = new Collider[3];
    readonly Material[] rotateMarkerMaterials = new Material[3];
    Material gizmoLineMaterial;
    bool gizmoInitialized;

    GizmoDragMode activeGizmoDragMode;
    GizmoAxis activeGizmoAxis = GizmoAxis.None;
    Vector3 gizmoDragStartPosition;
    Quaternion gizmoDragStartRotation;
    Vector3 gizmoDragStartCenter;
    Vector2 gizmoDragStartCenterScreen;
    Vector2 gizmoDragAxisScreenDir;
    float gizmoDragStartPointerProjection;
    float gizmoDragWorldPerPixel;
    Plane gizmoRotationPlane;
    Vector3 gizmoRotationStartVector;

    public bool ShouldConsumeSelectionClick()
    {
        EnsureCamera();

        if (!IsTransformMode()) return false;
        if (sel == null || sel.Current == null) return false;
        if (activeGizmoDragMode != GizmoDragMode.None) return true;
        if (!Input.GetMouseButtonDown(0)) return false;
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return false;

        return TryGetHandleUnderPointer(Input.mousePosition, out _, out _);
    }

    void Update()
    {
        EnsureCamera();

        if (!IsTransformMode())
        {
            CancelRuntimeDragStates();
            SetGizmoVisible(false);
            return;
        }

        if (sel == null || sel.Current == null)
        {
            CancelRuntimeDragStates();
            SetGizmoVisible(false);
            return;
        }

        UpdateGizmoVisual();

        if (activeGizmoDragMode != GizmoDragMode.None)
        {
            if (Input.GetMouseButtonUp(0))
            {
                CommitGizmoDragIfNeeded();
                return;
            }

            if (Input.GetMouseButton(0))
            {
                UpdateGizmoDrag();
                return;
            }

            CancelRuntimeDragStates();
            return;
        }

        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        {
            return;
        }

        if (TryBeginGizmoDrag(Input.mousePosition))
        {
            return;
        }

        HandleKeyboardNudgeMove();
    }

    void OnDestroy()
    {
        if (gizmoRoot != null)
        {
            Destroy(gizmoRoot.gameObject);
        }

        if (gizmoLineMaterial != null)
        {
            Destroy(gizmoLineMaterial);
        }

        for (int i = 0; i < rotateMarkerMaterials.Length; i++)
        {
            if (rotateMarkerMaterials[i] != null)
            {
                Destroy(rotateMarkerMaterials[i]);
                rotateMarkerMaterials[i] = null;
            }
        }
    }

    void HandleKeyboardNudgeMove()
    {
        if (sel == null || sel.Current == null) return;

        Vector3 nudge = Vector3.zero;
        bool modifierPressed =
            Input.GetKey(KeyCode.LeftControl) ||
            Input.GetKey(KeyCode.RightControl) ||
            Input.GetKey(KeyCode.LeftCommand) ||
            Input.GetKey(KeyCode.RightCommand) ||
            Input.GetKey(KeyCode.LeftAlt) ||
            Input.GetKey(KeyCode.RightAlt);

        if (!modifierPressed)
        {
            if (Input.GetKeyDown(KeyCode.W)) nudge += new Vector3(0, 0, gridSize);
            if (Input.GetKeyDown(KeyCode.S)) nudge += new Vector3(0, 0, -gridSize);
            if (Input.GetKeyDown(KeyCode.A)) nudge += new Vector3(-gridSize, 0, 0);
            if (Input.GetKeyDown(KeyCode.D)) nudge += new Vector3(gridSize, 0, 0);
        }

        if (Input.GetKeyDown(KeyCode.UpArrow)) nudge += new Vector3(0, 0, gridSize);
        if (Input.GetKeyDown(KeyCode.DownArrow)) nudge += new Vector3(0, 0, -gridSize);
        if (Input.GetKeyDown(KeyCode.LeftArrow)) nudge += new Vector3(-gridSize, 0, 0);
        if (Input.GetKeyDown(KeyCode.RightArrow)) nudge += new Vector3(gridSize, 0, 0);

        if (nudge != Vector3.zero)
        {
            sel.Current.transform.position += nudge;
        }
    }

    bool TryBeginGizmoDrag(Vector2 pointer)
    {
        if (!Input.GetMouseButtonDown(0)) return false;
        if (!TryGetHandleUnderPointer(pointer, out var dragMode, out var axis)) return false;
        if (!TryGetSelectionCenterAndAxisLength(out var center, out var axisLength)) return false;
        if (!TryWorldToScreen(center, out var centerScreen)) return false;

        activeGizmoDragMode = dragMode;
        activeGizmoAxis = axis;
        gizmoDragStartPosition = sel.Current.transform.position;
        gizmoDragStartRotation = sel.Current.transform.rotation;
        gizmoDragStartCenter = center;
        gizmoDragStartCenterScreen = centerScreen;

        Vector3 axisDir = AxisFromEnum(axis);

        if (dragMode == GizmoDragMode.Move)
        {
            if (!TryWorldToScreen(center + axisDir * axisLength, out var tipScreen))
            {
                CancelRuntimeDragStates();
                return false;
            }

            Vector2 axisScreenVector = tipScreen - centerScreen;
            float axisPixels = axisScreenVector.magnitude;
            if (axisPixels < 0.0001f)
            {
                CancelRuntimeDragStates();
                return false;
            }

            gizmoDragAxisScreenDir = axisScreenVector / axisPixels;
            gizmoDragStartPointerProjection = Vector2.Dot(pointer - centerScreen, gizmoDragAxisScreenDir);
            gizmoDragWorldPerPixel = axisLength / axisPixels;
            return true;
        }

        gizmoRotationPlane = new Plane(axisDir, center);
        if (!TryRaycastPlane(pointer, gizmoRotationPlane, out var startPoint))
        {
            CancelRuntimeDragStates();
            return false;
        }

        var startVector = Vector3.ProjectOnPlane(startPoint - center, axisDir);
        if (startVector.sqrMagnitude < 0.00001f)
        {
            CancelRuntimeDragStates();
            return false;
        }

        gizmoRotationStartVector = startVector.normalized;
        return true;
    }

    void UpdateGizmoDrag()
    {
        if (sel == null || sel.Current == null)
        {
            CancelRuntimeDragStates();
            return;
        }

        if (activeGizmoDragMode == GizmoDragMode.Move)
        {
            float projection = Vector2.Dot((Vector2)Input.mousePosition - gizmoDragStartCenterScreen, gizmoDragAxisScreenDir);
            float deltaWorld = (projection - gizmoDragStartPointerProjection) * gizmoDragWorldPerPixel;

            if (gridSize > 0.0001f)
            {
                deltaWorld = Mathf.Round(deltaWorld / gridSize) * gridSize;
            }

            Vector3 axisDir = AxisFromEnum(activeGizmoAxis);
            sel.Current.transform.position = gizmoDragStartPosition + axisDir * deltaWorld;
            return;
        }

        if (activeGizmoDragMode == GizmoDragMode.Rotate)
        {
            if (!TryRaycastPlane(Input.mousePosition, gizmoRotationPlane, out var point)) return;

            Vector3 axisDir = AxisFromEnum(activeGizmoAxis);
            Vector3 currentVector = Vector3.ProjectOnPlane(point - gizmoDragStartCenter, axisDir);
            if (currentVector.sqrMagnitude < 0.00001f) return;

            float angleDelta = Vector3.SignedAngle(gizmoRotationStartVector, currentVector.normalized, axisDir);
            if (rotateSnapDegrees > 0.001f)
            {
                angleDelta = Mathf.Round(angleDelta / rotateSnapDegrees) * rotateSnapDegrees;
            }

            sel.Current.transform.rotation = Quaternion.AngleAxis(angleDelta, axisDir) * gizmoDragStartRotation;
        }
    }

    void CommitGizmoDragIfNeeded()
    {
        if (sel == null || sel.Current == null)
        {
            CancelRuntimeDragStates();
            return;
        }

        if (CommandService.I == null)
        {
            CancelRuntimeDragStates();
            return;
        }

        if (activeGizmoDragMode == GizmoDragMode.Move)
        {
            Vector3 endPos = sel.Current.transform.position;
            if ((endPos - gizmoDragStartPosition).sqrMagnitude > 0.000001f)
            {
                var cmd = new MoveObjectCommand(sel.Current.gameObject, gizmoDragStartPosition, endPos);
                CommandService.I.Stack.Execute(cmd);
            }
        }
        else if (activeGizmoDragMode == GizmoDragMode.Rotate)
        {
            Quaternion endRot = sel.Current.transform.rotation;
            if (Quaternion.Angle(gizmoDragStartRotation, endRot) > 0.001f)
            {
                var cmd = new RotateObjectQuaternionCommand(sel.Current.gameObject, gizmoDragStartRotation, endRot);
                CommandService.I.Stack.Execute(cmd);
            }
        }

        CancelRuntimeDragStates();
    }

    bool TryGetHandleUnderPointer(Vector2 pointer, out GizmoDragMode dragMode, out GizmoAxis axis)
    {
        dragMode = GizmoDragMode.None;
        axis = GizmoAxis.None;

        if (cam == null) return false;
        if (sel == null || sel.Current == null) return false;
        if (!TryGetSelectionCenterAndAxisLength(out var center, out var axisLength)) return false;
        if (!TryWorldToScreen(center, out var centerScreen)) return false;

        if (TryPickRotateHandleFromRay(pointer, out axis))
        {
            dragMode = GizmoDragMode.Rotate;
            return true;
        }

        float bestMoveDistance = float.MaxValue;
        for (int i = 0; i < GizmoAxes.Length; i++)
        {
            if (!TryWorldToScreen(center + GizmoAxes[i] * axisLength, out var tipScreen)) continue;

            float distance = DistanceToSegment(pointer, centerScreen, tipScreen, out float t);
            bool isInsideSegment = t > 0.12f && t < 0.9f;
            if (!isInsideSegment) continue;
            if (distance > moveHandlePickRadiusPixels) continue;
            if (distance >= bestMoveDistance) continue;

            bestMoveDistance = distance;
            dragMode = GizmoDragMode.Move;
            axis = (GizmoAxis)i;
        }

        return dragMode != GizmoDragMode.None;
    }

    bool TryPickRotateHandleFromRay(Vector2 pointer, out GizmoAxis axis)
    {
        axis = GizmoAxis.None;
        if (cam == null) return false;

        Ray ray = cam.ScreenPointToRay(pointer);
        var hits = Physics.RaycastAll(ray, 1000f, ~0, QueryTriggerInteraction.Collide);
        if (hits == null || hits.Length == 0) return false;

        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        foreach (var hit in hits)
        {
            var collider = hit.collider;
            if (collider == null) continue;

            for (int i = 0; i < rotateMarkerColliders.Length; i++)
            {
                if (rotateMarkerColliders[i] == null) continue;
                if (collider != rotateMarkerColliders[i]) continue;

                axis = (GizmoAxis)i;
                return true;
            }
        }

        return false;
    }

    void UpdateGizmoVisual()
    {
        EnsureGizmo();

        if (gizmoRoot == null)
        {
            return;
        }

        if (!TryGetSelectionCenterAndAxisLength(out var center, out var axisLength))
        {
            SetGizmoVisible(false);
            return;
        }

        gizmoRoot.position = center;
        SetGizmoVisible(true);

        float headLength = axisLength * 0.22f;
        float headWidth = headLength * 0.66f;
        float markerSize = Mathf.Max(0.1f, axisLength * rotateHandleScale);

        for (int i = 0; i < GizmoAxes.Length; i++)
        {
            Vector3 axis = GizmoAxes[i];
            Vector3 tip = center + axis * axisLength;
            Vector3 viewDir = cam != null ? (cam.transform.position - tip).normalized : Vector3.up;
            Vector3 side = Vector3.Cross(axis, viewDir);
            if (side.sqrMagnitude < 0.0001f)
            {
                side = Vector3.Cross(axis, Vector3.up);
            }
            side.Normalize();

            Vector3 headA = tip - axis * headLength + side * headWidth;
            Vector3 headB = tip - axis * headLength - side * headWidth;
            bool isMoveAxisActive = activeGizmoDragMode == GizmoDragMode.Move && activeGizmoAxis == (GizmoAxis)i;
            Color axisColor = isMoveAxisActive ? moveAxisSelectedColor : GizmoColors[i];

            var lr = axisRenderers[i];
            if (lr != null)
            {
                lr.positionCount = 5;
                lr.SetPosition(0, center);
                lr.SetPosition(1, tip);
                lr.SetPosition(2, headA);
                lr.SetPosition(3, tip);
                lr.SetPosition(4, headB);
                lr.startColor = axisColor;
                lr.endColor = axisColor;
            }

            var marker = rotateMarkers[i];
            if (marker != null)
            {
                marker.position = GetRotateHandleWorld(center, axis, axisLength);
                marker.localScale = Vector3.one * markerSize;
                marker.gameObject.SetActive(true);
            }
        }
    }

    void EnsureGizmo()
    {
        if (gizmoInitialized) return;

        Shader shader = Shader.Find("Hidden/Internal-Colored");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        if (shader == null) shader = Shader.Find("Unlit/Color");
        if (shader == null) return;

        gizmoLineMaterial = new Material(shader);
        gizmoLineMaterial.hideFlags = HideFlags.DontSave;
        ConfigureAlwaysOnTopMaterial(gizmoLineMaterial);

        var root = new GameObject("RuntimeTransformGizmo");
        root.hideFlags = HideFlags.DontSave;
        gizmoRoot = root.transform;
        gizmoRoot.SetParent(transform, false);

        for (int i = 0; i < GizmoAxes.Length; i++)
        {
            var axisGo = new GameObject($"Axis_{(GizmoAxis)i}");
            axisGo.hideFlags = HideFlags.DontSave;
            axisGo.transform.SetParent(gizmoRoot, false);

            var lr = axisGo.AddComponent<LineRenderer>();
            lr.material = gizmoLineMaterial;
            lr.useWorldSpace = true;
            lr.widthMultiplier = gizmoLineWidth;
            lr.alignment = LineAlignment.View;
            lr.shadowCastingMode = ShadowCastingMode.Off;
            lr.receiveShadows = false;
            lr.textureMode = LineTextureMode.Stretch;
            lr.numCapVertices = 0;
            lr.sortingOrder = short.MaxValue;
            lr.startColor = GizmoColors[i];
            lr.endColor = GizmoColors[i];
            axisRenderers[i] = lr;

            var markerGo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            markerGo.name = $"RotateHandle_{(GizmoAxis)i}";
            markerGo.hideFlags = HideFlags.DontSave;
            markerGo.transform.SetParent(gizmoRoot, true);

            var markerCollider = markerGo.GetComponent<Collider>();
            if (markerCollider != null)
            {
                markerCollider.isTrigger = false;
                rotateMarkerColliders[i] = markerCollider;
            }

            var markerRenderer = markerGo.GetComponent<Renderer>();
            if (markerRenderer != null)
            {
                var markerMaterial = new Material(gizmoLineMaterial);
                markerMaterial.hideFlags = HideFlags.DontSave;
                ConfigureAlwaysOnTopMaterial(markerMaterial);
                SetMaterialColor(markerMaterial, GizmoColors[i]);
                markerRenderer.material = markerMaterial;
                rotateMarkerMaterials[i] = markerMaterial;
            }

            rotateMarkers[i] = markerGo.transform;
        }

        SetGizmoVisible(false);
        gizmoInitialized = true;
    }

    void SetGizmoVisible(bool visible)
    {
        if (gizmoRoot == null) return;
        if (gizmoRoot.gameObject.activeSelf != visible)
        {
            gizmoRoot.gameObject.SetActive(visible);
        }
    }

    void CancelRuntimeDragStates()
    {
        activeGizmoDragMode = GizmoDragMode.None;
        activeGizmoAxis = GizmoAxis.None;
        gizmoDragStartPosition = Vector3.zero;
        gizmoDragStartRotation = Quaternion.identity;
        gizmoDragStartCenter = Vector3.zero;
        gizmoDragStartCenterScreen = Vector2.zero;
        gizmoDragAxisScreenDir = Vector2.zero;
        gizmoDragStartPointerProjection = 0f;
        gizmoDragWorldPerPixel = 0f;
        gizmoRotationPlane = default;
        gizmoRotationStartVector = Vector3.zero;
    }

    bool TryGetSelectionCenterAndAxisLength(out Vector3 center, out float axisLength)
    {
        center = Vector3.zero;
        axisLength = gizmoMinAxisLength;

        if (sel == null || sel.Current == null) return false;

        var target = sel.Current.gameObject;
        if (target == null) return false;

        var renderers = target.GetComponentsInChildren<Renderer>();
        if (renderers == null || renderers.Length == 0)
        {
            center = target.transform.position;
            axisLength = gizmoMinAxisLength;
            return true;
        }

        var bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }

        center = bounds.center;
        float maxExtent = Mathf.Max(bounds.extents.x, bounds.extents.y, bounds.extents.z);
        axisLength = Mathf.Max(gizmoMinAxisLength, maxExtent * 2f * gizmoAxisLengthMultiplier);
        return true;
    }

    bool TryWorldToScreen(Vector3 world, out Vector2 screen)
    {
        screen = default;
        if (cam == null) return false;

        var point = cam.WorldToScreenPoint(world);
        if (point.z <= 0f) return false;

        screen = new Vector2(point.x, point.y);
        return true;
    }

    bool TryRaycastPlane(Vector2 pointer, Plane plane, out Vector3 point)
    {
        point = default;
        if (cam == null) return false;

        Ray ray = cam.ScreenPointToRay(pointer);
        if (!plane.Raycast(ray, out float enter)) return false;

        point = ray.GetPoint(enter);
        return true;
    }

    static float DistanceToSegment(Vector2 point, Vector2 start, Vector2 end, out float t)
    {
        Vector2 segment = end - start;
        float lenSq = segment.sqrMagnitude;
        if (lenSq <= 0.00001f)
        {
            t = 0f;
            return Vector2.Distance(point, start);
        }

        t = Mathf.Clamp01(Vector2.Dot(point - start, segment) / lenSq);
        Vector2 projection = start + segment * t;
        return Vector2.Distance(point, projection);
    }

    static Vector3 AxisFromEnum(GizmoAxis axis)
    {
        return axis switch
        {
            GizmoAxis.X => Vector3.right,
            GizmoAxis.Y => Vector3.up,
            GizmoAxis.Z => Vector3.forward,
            _ => Vector3.right
        };
    }

    Vector3 GetRotateHandleWorld(Vector3 center, Vector3 axis, float axisLength)
    {
        float distanceRatio = Mathf.Clamp01(rotateHandleDistanceRatio);
        float markerRadius = Mathf.Max(0.03f, axisLength * rotateHandleScale * 0.5f);
        float rawDistance = axisLength * distanceRatio;
        float minDistanceFromCenter = markerRadius * 1.05f;
        float maxDistanceFromCenter = Mathf.Max(minDistanceFromCenter, axisLength - (markerRadius * 0.2f));
        float desiredDistance = Mathf.Max(rawDistance, rotateHandleMinDistance);
        float handleDistance = Mathf.Clamp(desiredDistance, minDistanceFromCenter, maxDistanceFromCenter);
        return center + axis * handleDistance;
    }

    static void ConfigureAlwaysOnTopMaterial(Material material)
    {
        if (material == null) return;

        material.renderQueue = (int)RenderQueue.Overlay;
        SetMaterialIntIfPresent(material, "_ZWrite", 0);
        SetMaterialIntIfPresent(material, "_ZTest", (int)CompareFunction.Always);
        SetMaterialIntIfPresent(material, "_Cull", (int)CullMode.Off);
        SetMaterialIntIfPresent(material, "_SrcBlend", (int)BlendMode.SrcAlpha);
        SetMaterialIntIfPresent(material, "_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
    }

    static void SetMaterialIntIfPresent(Material material, string propertyName, int value)
    {
        if (material == null || !material.HasProperty(propertyName)) return;
        material.SetInt(propertyName, value);
    }

    static void SetMaterialColor(Material material, Color color)
    {
        if (material == null) return;

        if (material.HasProperty("_Color"))
        {
            material.SetColor("_Color", color);
        }

        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", color);
        }
    }

    bool IsTransformMode()
    {
        return EditModeService.I != null && EditModeService.I.Mode == EditMode.Transform;
    }

    void EnsureCamera()
    {
        if (cam != null) return;
        cam = Camera.main;
        if (cam == null)
        {
            cam = FindFirstObjectByType<Camera>();
        }
    }
}
