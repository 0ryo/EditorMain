using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;

public class MoveTool : MonoBehaviour
{
    const float GizmoWidthScale = 0.9f;
    const float GizmoAlphaScale = 0.9f;

    public Camera cam;
    public SelectionService sel;
    public float gridSize = 0.1f;

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
    readonly Transform[] axisConeTransforms = new Transform[3];
    readonly Material[] axisConeMaterials = new Material[3];
    readonly LineRenderer[] rotateArcRenderers = new LineRenderer[3];
    readonly Collider[][] rotateArcColliders = new Collider[3][];
    Material gizmoLineMaterial;
    Mesh gizmoConeMesh;
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
    Vector3 gizmoDragAxisWorldDir;
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

        Vector3 axisDir = AxisFromEnum(axis, gizmoDragStartRotation);
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

            sel.Current.transform.position = gizmoDragStartPosition + gizmoDragAxisWorldDir * deltaWorld;
            return;
        }

        if (activeGizmoDragMode == GizmoDragMode.Rotate)
        {
            if (!TryRaycastPlane(Input.mousePosition, gizmoRotationPlane, out var point)) return;

            Vector3 axisDir = AxisFromEnum(activeGizmoAxis, gizmoDragStartRotation);
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
            Vector3 axisDir = AxisFromEnum((GizmoAxis)i, sel.Current.transform.rotation);
            if (!TryWorldToScreen(center + axisDir * axisLength, out var tipScreen)) continue;

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

            for (int i = 0; i < rotateArcColliders.Length; i++)
            {
                var colliders = rotateArcColliders[i];
                if (colliders == null) continue;

                for (int j = 0; j < colliders.Length; j++)
                {
                    if (colliders[j] == null) continue;
                    if (collider != colliders[j]) continue;

                    axis = GetRotateArcRotationAxis(i);
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
            Vector3 axis = AxisFromEnum((GizmoAxis)i, objectRotation);
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
                SetMaterialColor(coneMaterial, axisColor);
            }
        }

        for (int i = 0; i < rotateArcRenderers.Length; i++)
        {
            UpdateRotateArcVisual(i, center, objectRotation, arcRadius, arcLineWidth, arcColliderThickness);
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
        gizmoConeMesh = CreateConeMesh(10);

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
            ConfigureAlwaysOnTopMaterial(coneMaterial);
            SetMaterialColor(coneMaterial, GizmoColors[i]);
            meshRenderer.sharedMaterial = coneMaterial;

            axisConeTransforms[i] = coneGo.transform;
            axisConeMaterials[i] = coneMaterial;
        }

        int colliderSegments = Mathf.Max(2, rotateArcColliderSegments);
        for (int i = 0; i < rotateArcRenderers.Length; i++)
        {
            var arcGo = new GameObject($"RotateArc_{GetRotateArcLabel(i)}");
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
            arcRenderer.startColor = GizmoColors[(int)GetRotateArcRotationAxis(i)];
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

    static Vector3 AxisFromEnum(GizmoAxis axis, Quaternion rotation)
    {
        Vector3 localAxis = axis switch
        {
            GizmoAxis.X => Vector3.right,
            GizmoAxis.Y => Vector3.up,
            GizmoAxis.Z => Vector3.forward,
            _ => Vector3.right
        };

        return (rotation * localAxis).normalized;
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

        GizmoAxis axisA = GetRotateArcStartAxis(arcIndex);
        GizmoAxis axisB = GetRotateArcEndAxis(arcIndex);
        GizmoAxis rotateAxis = GetRotateArcRotationAxis(arcIndex);
        Vector3 dirA = AxisFromEnum(axisA, objectRotation);
        Vector3 dirB = AxisFromEnum(axisB, objectRotation);
        Vector3 normal = AxisFromEnum(rotateAxis, objectRotation);

        int segmentCount = Mathf.Max(6, rotateArcLineSegments);
        lr.widthMultiplier = arcLineWidth;
        lr.positionCount = segmentCount + 1;
        for (int i = 0; i <= segmentCount; i++)
        {
            float t = i / (float)segmentCount;
            lr.SetPosition(i, EvaluateArcPoint(center, dirA, dirB, arcRadius, t));
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
            Vector3 p0 = EvaluateArcPoint(center, dirA, dirB, arcRadius, t0);
            Vector3 p1 = EvaluateArcPoint(center, dirA, dirB, arcRadius, t1);
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

    static Vector3 EvaluateArcPoint(Vector3 center, Vector3 axisA, Vector3 axisB, float radius, float t)
    {
        float radians = Mathf.Clamp01(t) * Mathf.PI * 0.5f;
        Vector3 radial = (axisA * Mathf.Cos(radians)) + (axisB * Mathf.Sin(radians));
        return center + radial.normalized * radius;
    }

    static GizmoAxis GetRotateArcStartAxis(int arcIndex)
    {
        return arcIndex switch
        {
            0 => GizmoAxis.X, // XY arc
            1 => GizmoAxis.Y, // YZ arc
            2 => GizmoAxis.Z, // ZX arc
            _ => GizmoAxis.X
        };
    }

    static GizmoAxis GetRotateArcEndAxis(int arcIndex)
    {
        return arcIndex switch
        {
            0 => GizmoAxis.Y, // XY arc
            1 => GizmoAxis.Z, // YZ arc
            2 => GizmoAxis.X, // ZX arc
            _ => GizmoAxis.Y
        };
    }

    static GizmoAxis GetRotateArcRotationAxis(int arcIndex)
    {
        return arcIndex switch
        {
            0 => GizmoAxis.Z, // XY arc -> rotate around Z
            1 => GizmoAxis.X, // YZ arc -> rotate around X
            2 => GizmoAxis.Y, // ZX arc -> rotate around Y
            _ => GizmoAxis.None
        };
    }

    static string GetRotateArcLabel(int arcIndex)
    {
        return arcIndex switch
        {
            0 => "XY",
            1 => "YZ",
            2 => "ZX",
            _ => "Unknown"
        };
    }

    static Mesh CreateConeMesh(int segmentCount)
    {
        int segments = Mathf.Max(8, segmentCount);
        int vertexCount = segments + 2;
        var vertices = new Vector3[vertexCount];
        var normals = new Vector3[vertexCount];
        var uvs = new Vector2[vertexCount];
        int triangleCount = segments * 2;
        var triangles = new int[triangleCount * 3];

        vertices[0] = new Vector3(0f, 0f, 1f); // tip (+Z)
        normals[0] = Vector3.forward;
        uvs[0] = new Vector2(0.5f, 1f);

        vertices[1] = Vector3.zero; // base center
        normals[1] = Vector3.back;
        uvs[1] = new Vector2(0.5f, 0.5f);

        for (int i = 0; i < segments; i++)
        {
            float t = i / (float)segments;
            float angle = t * Mathf.PI * 2f;
            float x = Mathf.Cos(angle);
            float y = Mathf.Sin(angle);
            int v = i + 2;
            vertices[v] = new Vector3(x, y, 0f);
            normals[v] = new Vector3(x, y, 0.35f).normalized;
            uvs[v] = new Vector2((x + 1f) * 0.5f, (y + 1f) * 0.5f);
        }

        int tri = 0;
        for (int i = 0; i < segments; i++)
        {
            int current = i + 2;
            int next = ((i + 1) % segments) + 2;

            // Side triangle
            triangles[tri++] = 0;
            triangles[tri++] = current;
            triangles[tri++] = next;

            // Base triangle (facing -Z)
            triangles[tri++] = 1;
            triangles[tri++] = next;
            triangles[tri++] = current;
        }

        var mesh = new Mesh
        {
            name = "RuntimeGizmoCone"
        };
        mesh.vertices = vertices;
        mesh.normals = normals;
        mesh.uv = uvs;
        mesh.triangles = triangles;
        mesh.RecalculateBounds();
        return mesh;
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
        cam = Camera.main;
        if (cam == null)
        {
            cam = FindFirstObjectByType<Camera>();
        }
    }
}
