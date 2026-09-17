using UnityEngine;
using UnityEngine.Rendering;

public class MoveTool : MonoBehaviour
{
    const float GizmoWidthScale = 0.9f;
    const float GizmoAlphaScale = 0.9f;

    public Camera cam;
    public SelectionService sel;
    public float gridSize = 0.1f;
    public bool enableDiagnostics = true;

    [Header("Transform Gizmo")]
    public float gizmoLineWidth = 0.04f;
    public float gizmoMinAxisLength = 0.45f;
    public float gizmoAxisLengthMultiplier = 0.84f;
    public float moveHandlePickRadiusPixels = 10f;
    [Range(0.2f, 0.9f)]
    public float rotateArcRadiusRatio = 0.62f;
    public float rotateArcMinRadius = 0.12f;
    public float rotateArcLineWidthMultiplier = 1.1f;
    public float rotateArcColliderThicknessMultiplier = 3f;
    [Min(6)]
    public int rotateArcLineSegments = 20;
    [Min(2)]
    public int rotateArcColliderSegments = 8;
    public float rotateSnapDegrees = 15f;
    public Color moveAxisSelectedColor = new Color(0.2f, 1f, 1f, 1f);
    public Color rotateArcSelectedColor = new Color(1f, 0.92f, 0.35f, 1f);

    static readonly Vector3[] GizmoAxes = { Vector3.right, Vector3.up, Vector3.forward };
    static readonly Color[] GizmoColors =
    {
        new Color(0.96f, 0.31f, 0.31f, 1f),
        new Color(0.36f, 0.86f, 0.44f, 1f),
        new Color(0.35f, 0.59f, 0.96f, 1f)
    };

    Transform gizmoRoot;
    readonly LineRenderer[] axisRenderers = new LineRenderer[3];
    readonly Transform[] axisConeTransforms = new Transform[3];
    readonly Material[] axisConeMaterials = new Material[3];
    readonly LineRenderer[] rotateArcRenderers = new LineRenderer[3];
    readonly Collider[][] rotateArcColliders = new Collider[3][];
    Material gizmoLineMaterial;
    Mesh gizmoConeMesh;
    bool gizmoInitialized;
    PlacedObject gizmoVisualTarget;
    Vector3 gizmoVisualPosition;
    Quaternion gizmoVisualRotation;
    Vector3 gizmoVisualScale;
    GizmoDragMode gizmoVisualDragMode = GizmoDragMode.None;
    GizmoAxis gizmoVisualAxis = GizmoAxis.None;
    int gizmoVisualSettingsRevision = -1;
    bool gizmoVisualDirty = true;

    GizmoDragMode activeGizmoDragMode;
    GizmoAxis activeGizmoAxis = GizmoAxis.None;
    Vector3 gizmoDragStartPosition;
    Quaternion gizmoDragStartRotation;
    Vector3 gizmoDragStartCenter;
    Vector2 gizmoDragStartCenterScreen;
    Vector2 gizmoDragAxisScreenDir;
    float gizmoDragStartPointerProjection;
    float gizmoDragWorldPerPixel;
    Vector3 gizmoDragAxisWorldDir;
    Plane gizmoRotationPlane;
    Vector3 gizmoRotationStartVector;

    public bool ShouldConsumeSelectionClick()
    {
        EnsureCamera();

        if (!IsTransformMode()) return false;
        if (sel == null || sel.Current == null) return false;
        if (activeGizmoDragMode != GizmoDragMode.None) return true;
        if (!EditInput.LeftPressedThisFrame()) return false;
        if (PlacementController.IsScreenPositionOverBlockingUi(EditInput.MousePosition)) return false;

        return TryGetHandleUnderPointer(EditInput.MousePosition, out _, out _);
    }

    void Update()
    {
        if (ObjectScreenPicker.Capturing) { CancelRuntimeDragStates(); SetGizmoVisible(false); return; }
        EnsureCamera();
        EnsureSelection();

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
            if (EditInput.LeftReleasedThisFrame())
            {
                CommitGizmoDragIfNeeded();
                return;
            }

            if (EditInput.LeftPressed())
            {
                UpdateGizmoDrag();
                return;
            }

            CancelRuntimeDragStates();
            return;
        }

        if (PlacementController.IsScreenPositionOverBlockingUi(EditInput.MousePosition))
        {
            return;
        }

        if (TryBeginGizmoDrag(EditInput.MousePosition))
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

        if (gizmoConeMesh != null)
        {
            Destroy(gizmoConeMesh);
        }

        for (int i = 0; i < axisConeMaterials.Length; i++)
        {
            if (axisConeMaterials[i] == null) continue;
            Destroy(axisConeMaterials[i]);
            axisConeMaterials[i] = null;
        }
    }

    void HandleKeyboardNudgeMove()
    {
        if (sel == null || sel.Current == null) return;
        if (EditWorkspace.IsTypingIntoInputField()) return;

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
            var target = sel.Current.gameObject;
            var from = target.transform.position;
            var to = from + nudge;

            var gesture = new SelectionTransformSession(sel);
            target.transform.position = to;
            gesture.Commit("Move selection");
        }
    }

    SelectionTransformSession selectionGesture;

    bool TryBeginGizmoDrag(Vector2 pointer)
    {
        if (!EditInput.LeftPressedThisFrame()) return false;
        if (!TryGetHandleUnderPointer(pointer, out var dragMode, out var axis)) return false;
        if (!TryGetSelectionCenterAndAxisLength(out var center, out var axisLength)) return false;
        if (!TryWorldToScreen(center, out var centerScreen)) return false;

        selectionGesture = new SelectionTransformSession(sel);
        activeGizmoDragMode = dragMode;
        activeGizmoAxis = axis;
        gizmoDragStartPosition = sel.Current.transform.position;
        gizmoDragStartRotation = sel.Current.transform.rotation;
        gizmoDragStartCenter = center;
        gizmoDragStartCenterScreen = centerScreen;

        Vector3 axisDir = TransformGizmoUtility.AxisDirection(axis, gizmoDragStartRotation);
        gizmoDragAxisWorldDir = axisDir;

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
            LogDebug($"Move drag started. axis={axis}, pointer={pointer}");
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
        LogDebug($"Rotate drag started. axis={axis}, pointer={pointer}");
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
            float projection = Vector2.Dot(EditInput.MousePosition - gizmoDragStartCenterScreen, gizmoDragAxisScreenDir);
            float deltaWorld = (projection - gizmoDragStartPointerProjection) * gizmoDragWorldPerPixel;

            if (EditSnapSettings.ShouldSnap && gridSize > 0.0001f)
            {
                deltaWorld = Mathf.Round(deltaWorld / gridSize) * gridSize;
            }

            sel.Current.transform.position = gizmoDragStartPosition + gizmoDragAxisWorldDir * deltaWorld;
            selectionGesture?.Apply();
            return;
        }

        if (activeGizmoDragMode == GizmoDragMode.Rotate)
        {
            if (!TryRaycastPlane(EditInput.MousePosition, gizmoRotationPlane, out var point)) return;

            Vector3 axisDir = TransformGizmoUtility.AxisDirection(activeGizmoAxis, gizmoDragStartRotation);
            Vector3 currentVector = Vector3.ProjectOnPlane(point - gizmoDragStartCenter, axisDir);
            if (currentVector.sqrMagnitude < 0.00001f) return;

            float angleDelta = Vector3.SignedAngle(gizmoRotationStartVector, currentVector.normalized, axisDir);
            if (EditSnapSettings.ShouldSnap && rotateSnapDegrees > 0.001f)
            {
                angleDelta = Mathf.Round(angleDelta / rotateSnapDegrees) * rotateSnapDegrees;
            }

            sel.Current.transform.rotation = Quaternion.AngleAxis(angleDelta, axisDir) * gizmoDragStartRotation;
            selectionGesture?.Apply();
        }
    }

    void CommitGizmoDragIfNeeded()
    {
        if (sel == null || sel.Current == null)
        {
            CancelRuntimeDragStates();
            return;
        }

        selectionGesture?.Commit("Transform selection");
        selectionGesture = null;

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
            Vector3 axisDir = TransformGizmoUtility.AxisDirection((GizmoAxis)i, sel.Current.transform.rotation);
            if (!TryWorldToScreen(center + axisDir * axisLength, out var tipScreen)) continue;

            float distance = TransformGizmoUtility.DistanceToSegment(pointer, centerScreen, tipScreen, out float t);
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

            for (int i = 0; i < rotateArcColliders.Length; i++)
            {
                var colliders = rotateArcColliders[i];
                if (colliders == null) continue;

                for (int j = 0; j < colliders.Length; j++)
                {
                    if (colliders[j] == null) continue;
                    if (collider != colliders[j]) continue;

                    axis = TransformGizmoUtility.GetArcRotationAxis(i);
                    return true;
                }
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

        var current = sel != null ? sel.Current : null;
        var currentTransform = current != null ? current.transform : null;
        bool transformChanged = currentTransform != null &&
                                (currentTransform.position != gizmoVisualPosition ||
                                 currentTransform.rotation != gizmoVisualRotation ||
                                 currentTransform.lossyScale != gizmoVisualScale);
        bool stateChanged = current != gizmoVisualTarget ||
                            activeGizmoDragMode != gizmoVisualDragMode ||
                            activeGizmoAxis != gizmoVisualAxis ||
                            gizmoVisualSettingsRevision != TransformToolSettings.Revision;
        if (!gizmoVisualDirty && !transformChanged && !stateChanged && gizmoRoot.gameObject.activeSelf)
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

        float lineWidth = GetScaledGizmoLineWidth(axisLength) * GizmoWidthScale;
        float arcLineWidth = lineWidth * Mathf.Max(0.1f, rotateArcLineWidthMultiplier);
        float arcRadius = GetRotateArcRadius(axisLength);
        float arcColliderThickness = arcLineWidth * Mathf.Max(1f, rotateArcColliderThicknessMultiplier);
        float headLength = axisLength * 0.22f;
        float headWidth = headLength * 0.66f * GizmoWidthScale;
        Quaternion objectRotation = sel.Current.transform.rotation;

        for (int i = 0; i < GizmoAxes.Length; i++)
        {
            Vector3 axis = TransformGizmoUtility.AxisDirection((GizmoAxis)i, objectRotation);
            Vector3 tip = center + axis * axisLength;
            Vector3 shaftEnd = tip - axis * headLength;
            bool isMoveAxisActive = activeGizmoDragMode == GizmoDragMode.Move && activeGizmoAxis == (GizmoAxis)i;
            Color axisColor = ApplyGizmoOpacity(isMoveAxisActive ? moveAxisSelectedColor : GizmoColors[i]);

            var lr = axisRenderers[i];
            if (lr != null)
            {
                lr.widthMultiplier = lineWidth;
                lr.positionCount = 2;
                lr.SetPosition(0, center);
                lr.SetPosition(1, shaftEnd);
                lr.startColor = axisColor;
                lr.endColor = axisColor;
            }

            var cone = axisConeTransforms[i];
            if (cone != null)
            {
                cone.position = shaftEnd;
                cone.rotation = Quaternion.LookRotation(axis);
                cone.localScale = new Vector3(headWidth, headWidth, headLength);
            }

            var coneMaterial = axisConeMaterials[i];
            if (coneMaterial != null)
            {
                TransformGizmoUtility.SetMaterialColor(coneMaterial, axisColor);
            }
        }

        for (int i = 0; i < rotateArcRenderers.Length; i++)
        {
            UpdateRotateArcVisual(i, center, objectRotation, arcRadius, arcLineWidth, arcColliderThickness);
        }

        gizmoVisualTarget = current;
        gizmoVisualPosition = currentTransform.position;
        gizmoVisualRotation = currentTransform.rotation;
        gizmoVisualScale = currentTransform.lossyScale;
        gizmoVisualDragMode = activeGizmoDragMode;
        gizmoVisualAxis = activeGizmoAxis;
        gizmoVisualSettingsRevision = TransformToolSettings.Revision;
        gizmoVisualDirty = false;
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
        TransformGizmoUtility.ConfigureAlwaysOnTopMaterial(gizmoLineMaterial);
        gizmoConeMesh = TransformGizmoUtility.CreateConeMesh(10);

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

            var coneGo = new GameObject($"AxisCone_{(GizmoAxis)i}");
            coneGo.hideFlags = HideFlags.DontSave;
            coneGo.transform.SetParent(gizmoRoot, false);

            var meshFilter = coneGo.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = gizmoConeMesh;

            var meshRenderer = coneGo.AddComponent<MeshRenderer>();
            var coneMaterial = new Material(gizmoLineMaterial);
            coneMaterial.hideFlags = HideFlags.DontSave;
            TransformGizmoUtility.ConfigureAlwaysOnTopMaterial(coneMaterial);
            TransformGizmoUtility.SetMaterialColor(coneMaterial, GizmoColors[i]);
            meshRenderer.sharedMaterial = coneMaterial;

            axisConeTransforms[i] = coneGo.transform;
            axisConeMaterials[i] = coneMaterial;
        }

        int colliderSegments = Mathf.Max(2, rotateArcColliderSegments);
        for (int i = 0; i < rotateArcRenderers.Length; i++)
        {
            var arcGo = new GameObject($"RotateArc_{TransformGizmoUtility.GetArcLabel(i)}");
            arcGo.hideFlags = HideFlags.DontSave;
            arcGo.transform.SetParent(gizmoRoot, false);

            var arcRenderer = arcGo.AddComponent<LineRenderer>();
            arcRenderer.material = gizmoLineMaterial;
            arcRenderer.useWorldSpace = true;
            arcRenderer.widthMultiplier = gizmoLineWidth;
            arcRenderer.alignment = LineAlignment.View;
            arcRenderer.shadowCastingMode = ShadowCastingMode.Off;
            arcRenderer.receiveShadows = false;
            arcRenderer.textureMode = LineTextureMode.Stretch;
            arcRenderer.numCapVertices = 0;
            arcRenderer.sortingOrder = short.MaxValue;
            arcRenderer.startColor = GizmoColors[(int)TransformGizmoUtility.GetArcRotationAxis(i)];
            arcRenderer.endColor = arcRenderer.startColor;
            rotateArcRenderers[i] = arcRenderer;

            var colliders = new Collider[colliderSegments];
            for (int j = 0; j < colliderSegments; j++)
            {
                var segmentGo = new GameObject($"Collider_{j}");
                segmentGo.hideFlags = HideFlags.DontSave;
                segmentGo.transform.SetParent(arcGo.transform, false);

                var boxCollider = segmentGo.AddComponent<BoxCollider>();
                boxCollider.isTrigger = false;
                colliders[j] = boxCollider;
            }

            rotateArcColliders[i] = colliders;
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
            if (!visible) gizmoVisualDirty = true;
        }
    }

    void CancelRuntimeDragStates()
    {
        selectionGesture?.Cancel();
        selectionGesture = null;
        activeGizmoDragMode = GizmoDragMode.None;
        activeGizmoAxis = GizmoAxis.None;
        gizmoDragStartPosition = Vector3.zero;
        gizmoDragStartRotation = Quaternion.identity;
        gizmoDragStartCenter = Vector3.zero;
        gizmoDragStartCenterScreen = Vector2.zero;
        gizmoDragAxisScreenDir = Vector2.zero;
        gizmoDragStartPointerProjection = 0f;
        gizmoDragWorldPerPixel = 0f;
        gizmoDragAxisWorldDir = Vector3.zero;
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

        center = TransformToolSettings.PivotMode == TransformPivotMode.Pivot
            ? target.transform.position
            : bounds.center;
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

    float GetScaledGizmoLineWidth(float axisLength)
    {
        if (gizmoMinAxisLength <= 0.0001f) return gizmoLineWidth;
        return gizmoLineWidth * (axisLength / gizmoMinAxisLength);
    }

    void UpdateRotateArcVisual(int arcIndex, Vector3 center, Quaternion objectRotation, float arcRadius, float arcLineWidth, float arcColliderThickness)
    {
        var lr = rotateArcRenderers[arcIndex];
        if (lr == null) return;

        GizmoAxis axisA = TransformGizmoUtility.GetArcStartAxis(arcIndex);
        GizmoAxis axisB = TransformGizmoUtility.GetArcEndAxis(arcIndex);
        GizmoAxis rotateAxis = TransformGizmoUtility.GetArcRotationAxis(arcIndex);
        Vector3 dirA = TransformGizmoUtility.AxisDirection(axisA, objectRotation);
        Vector3 dirB = TransformGizmoUtility.AxisDirection(axisB, objectRotation);
        Vector3 normal = TransformGizmoUtility.AxisDirection(rotateAxis, objectRotation);

        int segmentCount = Mathf.Max(6, rotateArcLineSegments);
        lr.widthMultiplier = arcLineWidth;
        lr.positionCount = segmentCount + 1;
        for (int i = 0; i <= segmentCount; i++)
        {
            float t = i / (float)segmentCount;
            lr.SetPosition(i, TransformGizmoUtility.EvaluateArcPoint(center, dirA, dirB, arcRadius, t));
        }

        bool isActive = activeGizmoDragMode == GizmoDragMode.Rotate && activeGizmoAxis == rotateAxis;
        Color arcColor = isActive ? DesignTokens.TextSecondary : DesignTokens.Divider;
        arcColor = ApplyGizmoOpacity(arcColor);
        lr.startColor = arcColor;
        lr.endColor = arcColor;

        var colliders = rotateArcColliders[arcIndex];
        if (colliders == null || colliders.Length == 0) return;

        for (int i = 0; i < colliders.Length; i++)
        {
            var box = colliders[i] as BoxCollider;
            if (box == null) continue;

            float t0 = i / (float)colliders.Length;
            float t1 = (i + 1) / (float)colliders.Length;
            Vector3 p0 = TransformGizmoUtility.EvaluateArcPoint(center, dirA, dirB, arcRadius, t0);
            Vector3 p1 = TransformGizmoUtility.EvaluateArcPoint(center, dirA, dirB, arcRadius, t1);
            Vector3 segment = p1 - p0;
            float segmentLength = segment.magnitude;
            if (segmentLength < 0.0001f)
            {
                box.enabled = false;
                continue;
            }

            box.enabled = true;
            Transform boxTransform = box.transform;
            boxTransform.position = (p0 + p1) * 0.5f;
            boxTransform.rotation = Quaternion.LookRotation(segment.normalized, normal);
            box.center = Vector3.zero;
            box.size = new Vector3(arcColliderThickness, arcColliderThickness, segmentLength + (arcColliderThickness * 0.2f));
        }
    }

    float GetRotateArcRadius(float axisLength)
    {
        float rawRadius = axisLength * Mathf.Clamp01(rotateArcRadiusRatio);
        float minRadius = Mathf.Max(rotateArcMinRadius, GetScaledGizmoLineWidth(axisLength) * 2f);
        float maxRadius = Mathf.Max(minRadius, axisLength * 0.98f);
        return Mathf.Clamp(rawRadius, minRadius, maxRadius);
    }

    static Color ApplyGizmoOpacity(Color color)
    {
        color.a *= GizmoAlphaScale;
        return color;
    }

    bool IsTransformMode()
    {
        return EditModeService.I != null && EditModeService.I.Mode == EditMode.Transform;
    }

    void EnsureCamera()
    {
        if (cam != null) return;
        cam = EditWorkspace.ResolveCamera();
    }

    void EnsureSelection()
    {
        if (sel != null) return;
        sel = FindFirstObjectByType<SelectionService>();
    }

    void LogDebug(string message)
    {
        if (!enableDiagnostics) return;
        Debug.Log("[MoveTool] " + message);
    }
}
