using System;
using UnityEngine;

/// <summary>2本指スクロールの扱い。マウス＝ホイールでズーム、トラックパッド＝2本指で回転（Shiftでパン、Cmd/Ctrl+でズーム）。</summary>
public enum EditorCameraScrollMode { Auto, Mouse, Trackpad }

public static class EditorCameraInputSettings
{
    const string Key = "EditorMain.CameraScrollMode";
    static EditorCameraScrollMode mode;
    static bool loaded;
    public static event Action Changed;

    const string DetectedKey = "EditorMain.TrackpadDetected";
    static bool detectedLoaded, detected;

    /// <summary>Auto の間にトラックパッドらしい横スクロールを検出したら true。次の起動でも覚えている（毎回最初の2本指でズームしない）。</summary>
    public static bool DetectedTrackpad
    {
        get
        {
            if (!detectedLoaded)
            {
                detectedLoaded = true;
                try { detected = PlayerPrefs.GetInt(DetectedKey, 0) != 0; } catch (Exception) { detected = false; }
            }
            return detected;
        }
    }

    public static EditorCameraScrollMode Mode
    {
        get
        {
            if (!loaded)
            {
                loaded = true;
                try { mode = (EditorCameraScrollMode)Mathf.Clamp(PlayerPrefs.GetInt(Key, 0), 0, 2); }
                catch (Exception) { mode = EditorCameraScrollMode.Auto; }
            }
            return mode;
        }
        set
        {
            loaded = true;
            mode = value;
            try { PlayerPrefs.SetInt(Key, (int)value); PlayerPrefs.Save(); }
            catch (Exception) { }
            Changed?.Invoke();
        }
    }

    public static bool UseTrackpad => Mode == EditorCameraScrollMode.Trackpad || (Mode == EditorCameraScrollMode.Auto && DetectedTrackpad);

    public static void MarkTrackpadDetected()
    {
        if (DetectedTrackpad) return;
        detected = true;
        try { PlayerPrefs.SetInt(DetectedKey, 1); PlayerPrefs.Save(); } catch (Exception) { }
        Changed?.Invoke();
    }

    public static void Toggle()
    {
        Mode = UseTrackpad ? EditorCameraScrollMode.Mouse : EditorCameraScrollMode.Trackpad;
    }
}

public class EditorCameraController : MonoBehaviour
{
    [Header("Sensitivity")]
    public float orbitSpeed = 12f;
    public float panSpeed = 0.01f;
    public const float DefaultPerspectiveZoomSpeed = 0.08f;
    public float zoomSpeed = DefaultPerspectiveZoomSpeed;
    public float orthographicZoomSpeed = 0.12f;
    [Header("Trackpad (two-finger scroll)")]
    public float trackpadOrbitGain = 14f;
    public float trackpadPanGain = 14f;
    public float trackpadZoomGain = 0.5f;

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
    bool leftDragNavigation;
    int trackpadScrollFrames;

    /// <summary>視点を切り替えたときの短い通知（「正面から表示」など）。ボタンでもキーでも同じ文言を出す。</summary>
    public static event Action<string> Notice;

    public enum ViewPreset
    {
        Front,
        Right,
        Top,
        Left,
        Back,
        Bottom
    }

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
        TickTransition();
        Vector2 mousePosition = EditInput.MousePosition;
        if (!IsPointerInsideGameView(mousePosition))
        {
            hasPreviousMousePosition = false;
            return;
        }

        bool typingBlocked = EditWorkspace.IsCameraInputBlocked();
        bool textFieldFocused = EditWorkspace.IsTextFieldFocused();
        Vector2 scroll = EditInput.ScrollDelta;
        float scrollY = scroll.y;
        if (!EditInput.LeftPressed()) leftDragNavigation = false;
        bool leftPressed = leftDragNavigation && EditInput.LeftPressed();
        bool middlePressed = EditInput.MiddlePressed();
        bool rightPressed = EditInput.RightPressed();
        bool shiftPressed = EditInput.ShiftPressed();
        bool overBlockingUi = EditWorkspace.TryGetBlockingUiName(mousePosition, out _, true);

        if (typingBlocked)
        {
            if (middlePressed || rightPressed || Mathf.Abs(scrollY) > 0.0001f)
            {
                LogDiagnostics("Input ignored because the editor is busy", true, Vector2.zero, scrollY);
            }
            return;
        }

        if (overBlockingUi)
        {
            RememberMousePosition(mousePosition);
            return;
        }

        // Any wheel, trackpad or drag input takes the view back from a running transition at once.
        if (transitionStart >= 0f && (scroll.sqrMagnitude > 0.00000001f || middlePressed || rightPressed || leftPressed)) transitionStart = -1f;
        HandleScroll(scroll, shiftPressed);
        // Keys stay with the text field while it has the caret; the mouse works whenever it is over the 3D screen.
        if (!textFieldFocused) HandleViewShortcuts();

        bool navigationPressed = middlePressed || rightPressed || leftPressed;
        if (!navigationPressed)
        {
            RememberMousePosition(mousePosition);
            return;
        }

        if (EditInput.MiddlePressedThisFrame() || EditInput.RightPressedThisFrame() || !hasPreviousMousePosition)
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

    static bool IsPointerInsideGameView(Vector2 mousePosition)
    {
        return Application.isFocused &&
               mousePosition.x >= 0f &&
               mousePosition.y >= 0f &&
               mousePosition.x < Screen.width &&
               mousePosition.y < Screen.height;
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
        float distance = EditWorkspace.DefaultCameraPosition.magnitude;
        pitch = Mathf.Atan2(EditWorkspace.DefaultCameraPosition.y, -EditWorkspace.DefaultCameraPosition.z) * Mathf.Rad2Deg;
        yaw = 0f;
        ApplyRigPose(distance);

        if (cachedCamera != null)
        {
            cachedCamera.orthographic = false;
            cachedCamera.orthographicSize = 7f;
            cachedCamera.nearClipPlane = 0.01f;
            cachedCamera.farClipPlane = 1000f;
        }
    }

    /// <summary>置いた部品すべてが収まる視点にする。部品が無ければ作業台の既定位置。</summary>
    public void ShowAll()
    {
        EnsureCameraRig();
        Bounds bounds = default;
        bool found = false;
        foreach (var item in FindObjectsByType<PlacedObject>(FindObjectsSortMode.None))
        {
            if (item == null || !item.gameObject.activeInHierarchy) continue;
            if (!SelectionRegionUtility.TryGetBounds(item, out var itemBounds)) continue;
            if (!found) bounds = itemBounds;
            else bounds.Encapsulate(itemBounds);
            found = true;
        }
        Transition(() =>
        {
            // Keep the angle the user chose; only a view from below or the side (easy to get lost in) returns to the default tilt.
            if (!found) { ResetToDefaultView(); return; }
            if (pitch < 10f) pitch = Mathf.Atan2(EditWorkspace.DefaultCameraPosition.y, -EditWorkspace.DefaultCameraPosition.z) * Mathf.Rad2Deg;
            ApplyPivotRotation();
            FocusBounds(bounds.center, Mathf.Max(3f, bounds.extents.magnitude));
        });
        Notice?.Invoke(found ? "全体を表示" : "全体を表示（部品はまだありません）");
    }

    public bool FocusSelected()
    {
        bool focused = false;
        Transition(() => focused = FocusSelectedCore());
        Notice?.Invoke(focused ? "選択した部品を表示" : "部品を選ぶと、その部品を画面に収めて表示します");
        return focused;
    }

    bool FocusSelectedCore()
    {
        var moveTool = FindFirstObjectByType<MoveTool>();
        var preview = moveTool != null ? moveTool.PreviewTarget : null;
        if (preview != null)
        {
            if (PlacedObjectGrounding.TryGetRendererBounds(preview, out var previewBounds))
                FocusBounds(previewBounds.center, Mathf.Max(0.5f, previewBounds.extents.magnitude));
            else FocusBounds(preview.position, 0.5f);
            return true;
        }
        var selection = FindFirstObjectByType<SelectionService>();
        if (selection == null) return false;
        Bounds bounds = default;
        bool found = false;
        foreach (var item in selection.Selected)
        {
            if (!SelectionService.CanEdit(item)) continue;
            SelectionRegionUtility.TryGetBounds(item, out var itemBounds);
            if (!found) bounds = itemBounds;
            else bounds.Encapsulate(itemBounds);
            found = true;
        }
        if (!found) return false;
        FocusBounds(bounds.center, Mathf.Max(0.5f, bounds.extents.magnitude));
        return true;
    }

    void FocusBounds(Vector3 center, float radius)
    {
        EnsureCameraRig();
        pivot.position = center;

        if (cachedCamera != null && cachedCamera.orthographic)
        {
            float aspect = Mathf.Min(1f, Mathf.Max(0.01f, cachedCamera.aspect));
            cachedCamera.orthographicSize = Mathf.Clamp(radius * 1.35f / aspect, minOrthographicSize, maxOrthographicSize);
            SyncOrthographicCameraDistance(cachedCamera.orthographicSize);
        }
        else
        {
            float halfFov = cachedCamera != null ? cachedCamera.fieldOfView * 0.5f * Mathf.Deg2Rad : 30f * Mathf.Deg2Rad;
            if (cachedCamera != null) halfFov = Mathf.Min(halfFov, Mathf.Atan(Mathf.Tan(halfFov) * Mathf.Max(0.01f, cachedCamera.aspect)));
            float distance = Mathf.Clamp((radius / Mathf.Sin(halfFov)) * 1.25f, minDistance, maxDistance);
            SetCameraDistance(distance);
        }
    }

    public void SetViewPreset(ViewPreset preset)
    {
        EnsureCameraRig();
        var from = CapturePose();
        float distance = Mathf.Clamp(transform.localPosition.magnitude, minDistance, maxDistance);
        switch (preset)
        {
            case ViewPreset.Right:
                pitch = 0f;
                yaw = -90f;
                break;
            case ViewPreset.Top:
                pitch = 89f;
                yaw = 0f;
                break;
            case ViewPreset.Left:
                pitch = 0f;
                yaw = 90f;
                break;
            case ViewPreset.Back:
                pitch = 0f;
                yaw = 180f;
                break;
            case ViewPreset.Bottom:
                pitch = -89f;
                yaw = 0f;
                break;
            default:
                pitch = 0f;
                yaw = 0f;
                break;
        }

        ApplyRigPose(distance);
        BeginTransition(from);
        Notice?.Invoke(ViewPresetName(preset) + "から表示");
    }

    public static string ViewPresetName(ViewPreset preset)
    {
        switch (preset)
        {
            case ViewPreset.Right: return "右";
            case ViewPreset.Left: return "左";
            case ViewPreset.Top: return "上";
            case ViewPreset.Bottom: return "下";
            case ViewPreset.Back: return "背面";
            default: return "正面";
        }
    }

    /// <summary>Option(Alt)＋左ドラッグ（何もない所）で始まる視点操作。実際の動きは Update が左ボタンを離すまで続ける。</summary>
    public void BeginLeftDragNavigation()
    {
        leftDragNavigation = true;
        RememberMousePosition(EditInput.MousePosition);
    }

    public bool ToggleProjection()
    {
        EnsureCameraRig();
        if (cachedCamera == null) return false;

        if (cachedCamera.orthographic)
        {
            float targetDistance = EditorCameraNavigationMath.CalculatePerspectiveDistanceForSize(
                cachedCamera.orthographicSize,
                cachedCamera.fieldOfView,
                minDistance,
                maxDistance);
            cachedCamera.orthographic = false;
            SetCameraDistance(targetDistance);
        }
        else
        {
            float distance = Mathf.Max(minDistance, transform.localPosition.magnitude);
            cachedCamera.orthographic = true;
            cachedCamera.orthographicSize = EditorCameraNavigationMath.CalculateOrthographicSizeForDistance(
                distance,
                cachedCamera.fieldOfView,
                minOrthographicSize,
                maxOrthographicSize);
            SyncOrthographicCameraDistance(cachedCamera.orthographicSize);
        }

        Notice?.Invoke(cachedCamera.orthographic ? "平行投影に切り替え（Oキーでもとに戻ります）" : "透視投影に切り替え");
        return cachedCamera.orthographic;
    }

    public bool IsOrthographic
    {
        get
        {
            EnsureCameraRig();
            return cachedCamera != null && cachedCamera.orthographic;
        }
    }

    void HandleViewShortcuts()
    {
        bool controlOrCommand =
            Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl) ||
            Input.GetKey(KeyCode.LeftCommand) || Input.GetKey(KeyCode.RightCommand);
        if (controlOrCommand) return;

        if (Input.GetKeyDown(KeyCode.F)) FocusSelected();
        if (Input.GetKeyDown(KeyCode.Home)) ShowAll();
        if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1)) SetViewPreset(ViewPreset.Front);
        if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3)) SetViewPreset(ViewPreset.Right);
        if (Input.GetKeyDown(KeyCode.Alpha7) || Input.GetKeyDown(KeyCode.Keypad7)) SetViewPreset(ViewPreset.Top);
        if (Input.GetKeyDown(KeyCode.O)) ToggleProjection();
    }

    // The view as it is now, for remembering where a lesson was left (SkillSyncViewMemory).
    public void CaptureView(SkillSyncViewMemory.State state)
    {
        EnsureCameraRig();
        if (state == null) return;
        var pose = transitionStart >= 0f ? transitionTo : CapturePose();
        state.hasCamera = true; state.orthographic = cachedCamera != null && cachedCamera.orthographic;
        state.pivot = pose.pivot; state.pitch = pose.pitch; state.yaw = pose.yaw; state.distance = pose.distance; state.size = pose.size;
    }

    public void RestoreView(SkillSyncViewMemory.State state)
    {
        EnsureCameraRig();
        if (state == null || !state.hasCamera) return;
        transitionStart = -1f;
        if (cachedCamera != null) cachedCamera.orthographic = state.orthographic;
        ApplyPose(new Pose { pivot = state.pivot, pitch = Mathf.Clamp(state.pitch, minPitch, maxPitch), yaw = state.yaw, distance = state.distance, size = state.size });
        if (cachedCamera != null && cachedCamera.orthographic) SyncOrthographicCameraDistance(Mathf.Clamp(state.size, minOrthographicSize, maxOrthographicSize));
    }

    // View changes from buttons and keys glide for a moment so the user can follow where the view went.
    // Any navigation input stops the glide where it is (the user never waits for it).
    const float TransitionSeconds = 0.3f;
    struct Pose { public Vector3 pivot; public float pitch, yaw, distance, size; }
    Pose transitionFrom, transitionTo;
    float transitionStart = -1f;

    Pose CapturePose() => new Pose
    {
        pivot = pivot.position, pitch = pitch, yaw = yaw, distance = transform.localPosition.magnitude,
        size = cachedCamera != null ? cachedCamera.orthographicSize : 0f,
    };

    void ApplyPose(Pose pose)
    {
        pivot.position = pose.pivot; pitch = pose.pitch; yaw = pose.yaw;
        if (cachedCamera != null && cachedCamera.orthographic) cachedCamera.orthographicSize = pose.size;
        ApplyRigPose(pose.distance);
    }

    void Transition(Action change)
    {
        EnsureCameraRig();
        var from = CapturePose();
        change();
        BeginTransition(from);
    }

    // The change is already applied; play it again from the pose before it.
    void BeginTransition(Pose from)
    {
        var to = CapturePose();
        bool moved = (from.pivot - to.pivot).sqrMagnitude > 1e-6f || Mathf.Abs(Mathf.DeltaAngle(from.yaw, to.yaw)) > .01f ||
            Mathf.Abs(from.pitch - to.pitch) > .01f || Mathf.Abs(from.distance - to.distance) > 1e-4f || Mathf.Abs(from.size - to.size) > 1e-4f;
        if (!moved) return;
        transitionFrom = from; transitionTo = to; transitionStart = Time.unscaledTime;
        ApplyPose(from);
    }

    void TickTransition()
    {
        if (transitionStart < 0f) return;
        float t = Mathf.Clamp01((Time.unscaledTime - transitionStart) / TransitionSeconds);
        float e = 1f - Mathf.Pow(1f - t, 3f);
        ApplyPose(new Pose
        {
            pivot = Vector3.Lerp(transitionFrom.pivot, transitionTo.pivot, e),
            pitch = Mathf.Lerp(transitionFrom.pitch, transitionTo.pitch, e),
            yaw = Mathf.LerpAngle(transitionFrom.yaw, transitionTo.yaw, e),
            distance = Mathf.Lerp(transitionFrom.distance, transitionTo.distance, e),
            size = Mathf.Lerp(transitionFrom.size, transitionTo.size, e),
        });
        if (t >= 1f) transitionStart = -1f;
    }

    void ApplyRigPose(float distance)
    {
        ApplyPivotRotation();
        transform.localRotation = Quaternion.identity;
        transform.localPosition = Vector3.back * Mathf.Clamp(distance, minDistance, maxDistance);
    }

    void SetCameraDistance(float distance)
    {
        Vector3 direction = transform.localPosition.sqrMagnitude > 0.0001f
            ? transform.localPosition.normalized
            : Vector3.back;
        transform.localPosition = direction * Mathf.Clamp(distance, minDistance, maxDistance);
    }

    void ApplySensitivityFloor()
    {
        if (!enforceHighSensitivity) return;

        orbitSpeed = Mathf.Max(orbitSpeed, 12f);
        orthographicZoomSpeed = Mathf.Clamp(orthographicZoomSpeed, 0.04f, 0.3f);
    }

    void NormalizeZoomSensitivity()
    {
        // Old scenes store an additive world-distance speed (normally 18).
        // Migrate those values before CatalogUI captures its sensitivity baseline.
        if (!float.IsFinite(zoomSpeed) || zoomSpeed <= 0f)
            zoomSpeed = DefaultPerspectiveZoomSpeed;
        else if (zoomSpeed > 0.5f)
            zoomSpeed = Mathf.Clamp(zoomSpeed / 18f * DefaultPerspectiveZoomSpeed, 0.01f, 0.3f);

        if (!float.IsFinite(orthographicZoomSpeed) || orthographicZoomSpeed <= 0f || orthographicZoomSpeed > 0.5f)
        {
            orthographicZoomSpeed = 0.12f;
        }
    }

    void SyncPivotAngles()
    {
        Vector3 euler = pivot.rotation.eulerAngles;
        pitch = EditorCameraNavigationMath.NormalizeAngle(euler.x);
        yaw = EditorCameraNavigationMath.NormalizeAngle(euler.y);
    }

    void HandleOrbit(Vector2 delta)
    {
        EditorCameraNavigationMath.CalculateOrbit(
            yaw,
            pitch,
            delta,
            orbitSpeed,
            minPitch,
            maxPitch,
            out yaw,
            out pitch);
        ApplyPivotRotation();
    }

    void ApplyPivotRotation()
    {
        pivot.rotation = Quaternion.Euler(pitch, yaw, 0f);
    }

    void HandleHorizontalPan(Vector2 delta)
    {
        if (!EditorCameraNavigationMath.TryCalculateHorizontalPan(
                transform.right,
                transform.forward,
                transform.localPosition.magnitude,
                minDistance,
                panSpeed,
                delta,
                out var move)) return;

        pivot.position += move;
    }

    // Mouse: the wheel zooms, Ctrl/Cmd or not. Trackpad: two fingers orbit, Shift pans, Ctrl/Cmd zooms.
    void HandleScroll(Vector2 scroll, bool shiftPressed)
    {
        if (scroll.sqrMagnitude <= 0.00000001f) { trackpadScrollFrames = 0; return; }
        bool primary = EditInput.PrimaryModifierPressed();
        if (EditorCameraInputSettings.Mode == EditorCameraScrollMode.Auto && !EditorCameraInputSettings.DetectedTrackpad)
        {
            // A wheel never scrolls sideways; a trackpad does almost every time two fingers move.
            trackpadScrollFrames = Mathf.Abs(scroll.x) > 0.02f ? trackpadScrollFrames + 1 : trackpadScrollFrames;
            if (trackpadScrollFrames >= 4)
            {
                EditorCameraInputSettings.MarkTrackpadDetected();
                Notice?.Invoke("トラックパッドの操作にしました：2本指で回転、Shift＋2本指で平行移動、" + SkillSyncDesignView.ShortcutModifierName + "＋2本指でズーム（マウスに戻すときは、配置画面の右下「詳細設定」で切り替えられます）");
            }
        }

        if (!EditorCameraInputSettings.UseTrackpad)
        {
            HandleZoom(EditorCameraNavigationMath.NormalizeScroll(scroll.y));
            return;
        }
        if (primary)
        {
            ApplyZoom(scroll.y * trackpadZoomGain);
            return;
        }
        if (shiftPressed)
        {
            HandleHorizontalPan(scroll * trackpadPanGain);
            return;
        }
        HandleOrbit(scroll * trackpadOrbitGain);
    }

    void HandleZoom(float scroll)
    {
        ApplyZoom(scroll);
    }

    void ApplyZoom(float scroll)
    {
        float rawScrollY = scroll;
        if (Mathf.Abs(scroll) < 0.0001f) return;

        if (cachedCamera != null && cachedCamera.orthographic)
        {
            float targetSize = EditorCameraNavigationMath.CalculateOrthographicSize(
                cachedCamera.orthographicSize,
                scroll,
                orthographicZoomSpeed,
                minOrthographicSize,
                maxOrthographicSize);

            cachedCamera.orthographicSize = targetSize;
            SyncOrthographicCameraDistance(targetSize);
            LogDiagnostics("Zoom", true, Vector2.zero, rawScrollY);
            return;
        }

        float targetDistance = EditorCameraNavigationMath.CalculatePerspectiveDistance(
            transform.localPosition.magnitude,
            scroll,
            zoomSpeed,
            minDistance,
            maxDistance);

        SetCameraDistance(targetDistance);
        LogDiagnostics("Zoom", true, Vector2.zero, rawScrollY);
    }

    void SyncOrthographicCameraDistance(float orthographicSize)
    {
        float distance = EditorCameraNavigationMath.CalculateOrthographicCameraDistance(
            orthographicSize,
            orthographicDistancePerSize,
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
