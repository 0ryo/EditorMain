using UnityEngine;

public class EditorCameraController : MonoBehaviour
{
    static readonly Vector3 DefaultCameraOffset = new Vector3(0f, 6f, -10f);

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

    Camera cachedCamera;
    float yaw;
    float pitch;
    Vector2 previousMousePosition;
    bool hasPreviousMousePosition;

    void Start()
    {
        cachedCamera = GetComponent<Camera>();
        EnsurePivot();
        AttachCameraToPivot();
        ResetToDefaultView();
        WorkspaceFloorGrid.EnsureExists();

        SyncPivotAngles();
        ApplySensitivityFloor();
    }

    void Update()
    {
        Vector2 mousePosition = Input.mousePosition;
        bool pointerBlocked = PlacementController.IsScreenPositionOverCameraBlockingUi(mousePosition);

        if (!pointerBlocked)
        {
            HandleZoom(Input.mouseScrollDelta.y);
        }

        bool middlePressed = Input.GetMouseButton(2);
        if (pointerBlocked || !middlePressed)
        {
            RememberMousePosition(mousePosition);
            return;
        }

        if (Input.GetMouseButtonDown(2) || !hasPreviousMousePosition)
        {
            RememberMousePosition(mousePosition);
            return;
        }

        Vector2 delta = mousePosition - previousMousePosition;
        RememberMousePosition(mousePosition);
        if (delta.sqrMagnitude <= 0.0001f) return;

        if (IsShiftPressed())
        {
            HandleHorizontalPan(delta);
            return;
        }

        HandleOrbit(delta);
    }

    void RememberMousePosition(Vector2 mousePosition)
    {
        previousMousePosition = mousePosition;
        hasPreviousMousePosition = true;
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
        cachedCamera = GetComponent<Camera>();
        EnsurePivot();
        AttachCameraToPivot();
        pivot.position = Vector3.zero;
        pivot.rotation = Quaternion.identity;
        transform.localPosition = DefaultCameraOffset;
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
            return;
        }

        float currentDistance = Mathf.Max(0.0001f, transform.localPosition.magnitude);
        float targetDistance = Mathf.Clamp(
            currentDistance - (scroll * zoomSpeed),
            minDistance,
            maxDistance);

        transform.localPosition = transform.localPosition.normalized * targetDistance;
    }

    static bool IsShiftPressed()
    {
        return Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
    }

    static float NormalizeAngle(float angle)
    {
        angle %= 360f;
        if (angle > 180f) angle -= 360f;
        return angle;
    }
}
