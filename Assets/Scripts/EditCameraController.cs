using UnityEngine;

public class EditorCameraController : MonoBehaviour
{
    [Header("Sensitivity")]
    public float orbitSpeed = 12f;
    public float panSpeed = 0.01f;
    public float zoomSpeed = 18f;
    public float orthographicZoomSpeed = 1.6f;

    [Header("Limits")]
    public float minDistance = 1.2f;
    public float maxDistance = 120f;
    public float minOrthographicSize = 0.4f;
    public float maxOrthographicSize = 80f;
    public float minPitch = -80f;
    public float maxPitch = 80f;

    [Header("References")]
    public Transform pivot;

    [Header("Runtime Policy")]
    public bool enforceHighSensitivity = true;

    [Header("Diagnostics")]
    public bool enableDiagnostics = true;
    public float diagnosticInterval = 1f;

    Camera cachedCamera;
    float yaw;
    float pitch;
    Vector2 previousMousePosition;
    bool hasPreviousMousePosition;
    float nextDiagnosticLogTime;

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

        if (typingBlocked)
        {
            if (middlePressed || Mathf.Abs(scrollY) > 0.0001f)
            {
                Debug.Log("[CameraDiag] Input ignored because a text field is focused.");
            }
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
            Debug.Log($"[CameraDiag] Middle drag started. mouse={mousePosition}, shift={shiftPressed}");
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
            cachedCamera.nearClipPlane = 0.05f;
            cachedCamera.farClipPlane = 1000f;
        }
    }

    void ApplySensitivityFloor()
    {
        if (!enforceHighSensitivity) return;

        orbitSpeed = Mathf.Max(orbitSpeed, 12f);
        zoomSpeed = Mathf.Max(zoomSpeed, 18f);
        orthographicZoomSpeed = Mathf.Max(orthographicZoomSpeed, 1.6f);
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
            float targetSize = Mathf.Clamp(
                cachedCamera.orthographicSize - (scroll * orthographicZoomSpeed),
                minSize,
                maxSize);

            cachedCamera.orthographicSize = targetSize;
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
