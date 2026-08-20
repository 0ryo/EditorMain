using UnityEngine;

public class EditorCameraController : MonoBehaviour
{
    static readonly string[] NodeAreaUiNames = { "NodeArea" };

    [Header("Sensitivity")]
    public float orbitSpeed = 12f;
    public float panSpeed = 0.01f;
    public float zoomSpeed = 18f;
    public float orthographicZoomSpeed = 0.12f;

    [Header("Limits")]
    public float minDistance = 1.2f;
    public float maxDistance = 240f;
    public float minOrthographicSize = 0.4f;
    public float maxOrthographicSize = 80f;
    public float minPitch = -80f;
    public float maxPitch = 80f;
    public float orthographicDistancePerSize = 2f;

    [Header("References")]
    public Transform pivot;

    [Header("Runtime Policy")]
    public bool enforceHighSensitivity = true;

    [Header("Diagnostics")]
    public bool enableDiagnostics = false;
    public float diagnosticInterval = 1f;

    Camera cachedCamera;
    float yaw;
    float pitch;
    Vector2 previousMousePosition;
    bool hasPreviousMousePosition;
    float nextDiagnosticLogTime;

    void Awake()
    {
        NormalizeZoomSensitivity();
    }

    void Start()
    {
        EnsureCameraRig();
        ResetToDefaultView();
        EditWorkspace.EnsureWorkspaceVisuals();

        SyncPivotAngles();
        ApplySensitivityFloor();
        LogDiagnostics("Start", true, Vector2.zero, 0f);
    }

    void Update()
    {
        EnsureCameraRig();
        bool typingBlocked = EditWorkspace.IsTypingIntoInputField();
        Vector2 mousePosition = EditInput.MousePosition;
        float scrollY = EditInput.ScrollY;
        bool middlePressed = EditInput.MiddlePressed();
        bool shiftPressed = EditInput.ShiftPressed();
        bool overNodeArea = EditWorkspace.TryGetBlockingUiName(mousePosition, NodeAreaUiNames, out _);

        if (typingBlocked)
        {
            if (middlePressed || Mathf.Abs(scrollY) > 0.0001f)
            {
                LogDiagnostics("Input ignored because a text field is focused", true, Vector2.zero, scrollY);
            }
            return;
        }

        if (overNodeArea)
        {
            RememberMousePosition(mousePosition);
            return;
        }

        HandleZoom(scrollY);

        if (!middlePressed)
        {
            RememberMousePosition(mousePosition);
            return;
        }

        if (EditInput.MiddlePressedThisFrame() || !hasPreviousMousePosition)
        {
            RememberMousePosition(mousePosition);
            LogDiagnostics("Middle drag started", true, Vector2.zero, scrollY);
            return;
        }

        Vector2 delta = mousePosition - previousMousePosition;
        RememberMousePosition(mousePosition);
        if (delta.sqrMagnitude <= 0.0001f) return;

        if (shiftPressed)
        {
            HandleHorizontalPan(delta);
            LogDiagnostics("Pan", true, delta, scrollY);
            return;
        }

        HandleOrbit(delta);
        LogDiagnostics("Orbit", true, delta, scrollY);
    }

    void RememberMousePosition(Vector2 mousePosition)
    {
        previousMousePosition = mousePosition;
        hasPreviousMousePosition = true;
    }

    void EnsureCameraRig()
    {
        if (cachedCamera == null)
        {
            cachedCamera = GetComponent<Camera>();
        }

        EnsurePivot();
        AttachCameraToPivot();
    }

    void EnsurePivot()
    {
        if (pivot != null) return;

        var go = new GameObject("CameraPivot");
        go.transform.position = Vector3.zero;
        pivot = go.transform;
    }

    void AttachCameraToPivot()
    {
        if (transform.parent == pivot) return;
        transform.SetParent(pivot, true);
    }

    public void ResetToDefaultView()
    {
        EnsureCameraRig();
        pivot.position = Vector3.zero;
        pivot.rotation = Quaternion.identity;
        transform.localPosition = EditWorkspace.DefaultCameraPosition;
        transform.LookAt(pivot.position, Vector3.up);

        if (cachedCamera != null)
        {
            cachedCamera.orthographic = true;
            cachedCamera.orthographicSize = 7f;
            cachedCamera.nearClipPlane = 0.01f;
            cachedCamera.farClipPlane = 1000f;
            SyncOrthographicCameraDistance(cachedCamera.orthographicSize);
        }
    }

    void ApplySensitivityFloor()
    {
        if (!enforceHighSensitivity) return;

        orbitSpeed = Mathf.Max(orbitSpeed, 12f);
        zoomSpeed = Mathf.Max(zoomSpeed, 18f);
        orthographicZoomSpeed = Mathf.Clamp(orthographicZoomSpeed, 0.04f, 0.3f);
    }

    void NormalizeZoomSensitivity()
    {
        if (!float.IsFinite(orthographicZoomSpeed) || orthographicZoomSpeed <= 0f || orthographicZoomSpeed > 0.5f)
        {
            orthographicZoomSpeed = 0.12f;
        }
    }

    void SyncPivotAngles()
    {
        Vector3 euler = pivot.rotation.eulerAngles;
        pitch = NormalizeAngle(euler.x);
        yaw = NormalizeAngle(euler.y);
    }

    void HandleOrbit(Vector2 delta)
    {
        yaw += delta.x * (orbitSpeed * 0.02f);
        pitch = Mathf.Clamp(pitch - (delta.y * orbitSpeed * 0.02f), minPitch, maxPitch);
        ApplyPivotRotation();
    }

    void ApplyPivotRotation()
    {
        pivot.rotation = Quaternion.Euler(pitch, yaw, 0f);
    }

    void HandleHorizontalPan(Vector2 delta)
    {
        Vector3 right = Vector3.ProjectOnPlane(transform.right, Vector3.up).normalized;
        Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
        if (right.sqrMagnitude <= 0.0001f || forward.sqrMagnitude <= 0.0001f) return;

        float distance = Mathf.Max(minDistance, transform.localPosition.magnitude);
        float scaledPanSpeed = panSpeed * distance * 0.1f;
        Vector3 move = ((-delta.x * right) + (-delta.y * forward)) * scaledPanSpeed;
        pivot.position += move;
    }

    void HandleZoom(float rawScrollY)
    {
        if (Mathf.Abs(rawScrollY) <= 0.0001f) return;

        float scroll = Mathf.Abs(rawScrollY) > 1f ? rawScrollY / 120f : rawScrollY;

        if (cachedCamera != null && cachedCamera.orthographic)
        {
            float minSize = Mathf.Max(0.01f, minOrthographicSize);
            float maxSize = Mathf.Max(minSize, maxOrthographicSize);
            float zoomFactor = Mathf.Exp(-scroll * orthographicZoomSpeed);
            float targetSize = Mathf.Clamp(cachedCamera.orthographicSize * zoomFactor, minSize, maxSize);

            cachedCamera.orthographicSize = targetSize;
            SyncOrthographicCameraDistance(targetSize);
            LogDiagnostics("Zoom", true, Vector2.zero, rawScrollY);
            return;
        }

        float currentDistance = Mathf.Max(0.0001f, transform.localPosition.magnitude);
        float targetDistance = Mathf.Clamp(
            currentDistance - (scroll * zoomSpeed),
            minDistance,
            maxDistance);

        transform.localPosition = transform.localPosition.normalized * targetDistance;
        LogDiagnostics("Zoom", true, Vector2.zero, rawScrollY);
    }

    void SyncOrthographicCameraDistance(float orthographicSize)
    {
        float distance = Mathf.Clamp(
            orthographicSize * Mathf.Max(1f, orthographicDistancePerSize),
            minDistance,
            maxDistance);

        Vector3 direction = transform.localPosition.sqrMagnitude > 0.0001f
            ? transform.localPosition.normalized
            : EditWorkspace.DefaultCameraPosition.normalized;
        transform.localPosition = direction * distance;

        if (cachedCamera != null)
        {
            cachedCamera.farClipPlane = Mathf.Max(1000f, distance + orthographicSize * 4f);
        }
    }

    static float NormalizeAngle(float angle)
    {
        angle %= 360f;
        if (angle > 180f) angle -= 360f;
        return angle;
    }

    void LogDiagnostics(string phase, bool force, Vector2 delta, float scrollY)
    {
        if (!enableDiagnostics) return;

        float now = Time.unscaledTime;
        if (!force && now < nextDiagnosticLogTime) return;
        nextDiagnosticLogTime = now + Mathf.Max(0.1f, diagnosticInterval);

        string cameraName = cachedCamera != null ? cachedCamera.name : "(null)";
        string pivotPosition = pivot != null ? pivot.position.ToString("F2") : "(null)";
        float orthoSize = cachedCamera != null && cachedCamera.orthographic ? cachedCamera.orthographicSize : -1f;

        Debug.Log(
            $"[CameraDiag] {phase}: enabled={enabled}, active={gameObject.activeInHierarchy}, cam={cameraName}, mouse={EditInput.MousePosition}, middle={EditInput.MiddlePressed()}, shift={EditInput.ShiftPressed()}, scrollY={scrollY:F3}, delta={delta}, pivot={pivotPosition}, camPos={transform.position.ToString("F2")}, yaw={yaw:F1}, pitch={pitch:F1}, ortho={orthoSize:F2}");
    }
}
