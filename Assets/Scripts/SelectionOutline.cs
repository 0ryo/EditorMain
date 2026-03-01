using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class SelectionOutline : MonoBehaviour
{
    [SerializeField] Material lineMat;
    [SerializeField] float handlePickRadiusPixels = 18f;
    [SerializeField] float minScaleAxis = 0.1f;

    readonly List<LineRenderer> lines = new();
    readonly Vector3[] corners = new Vector3[8];
    readonly Vector3[,] edges = new Vector3[12, 2];

    GameObject target;
    Camera cachedCamera;
    bool isScaling;
    Vector3 dragStartScale;
    float dragStartScreenDistance;

    void Update()
    {
        if (target == null)
        {
            EnsureLines(0);
            ResetScaleState();
            return;
        }

        UpdateOutline();
        HandleScaleDrag();
    }

    public void ShowFor(GameObject t)
    {
        target = t;
        ResetScaleState();

        if (target == null)
        {
            EnsureLines(0);
            return;
        }

        UpdateOutline();
    }

    void HandleScaleDrag()
    {
        if (EditModeService.I == null || EditModeService.I.Mode != EditMode.Scale)
        {
            if (isScaling)
            {
                CommitScaleIfNeeded();
            }
            return;
        }

        if (!isScaling)
        {
            if (!Input.GetMouseButtonDown(0)) return;
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;
            TryBeginScaleDrag();
            return;
        }

        if (!Input.GetMouseButton(0))
        {
            CommitScaleIfNeeded();
            return;
        }

        ApplyScaleFromPointer();
    }

    void TryBeginScaleDrag()
    {
        var cam = ResolveCamera();
        if (cam == null) return;

        if (!TryGetCenterScreen(cam, out var centerScreen)) return;
        if (!TryGetClosestCornerScreen(cam, Input.mousePosition, out var nearestCorner, out var nearestDistance)) return;
        if (nearestDistance > handlePickRadiusPixels) return;

        dragStartScale = target.transform.localScale;
        dragStartScreenDistance = Vector2.Distance(centerScreen, nearestCorner);
        if (dragStartScreenDistance <= 0.001f) return;

        isScaling = true;
    }

    void ApplyScaleFromPointer()
    {
        var cam = ResolveCamera();
        if (cam == null) return;
        if (!TryGetCenterScreen(cam, out var centerScreen)) return;

        float currentDistance = Vector2.Distance(centerScreen, Input.mousePosition);
        float ratio = currentDistance / dragStartScreenDistance;
        if (!float.IsFinite(ratio)) return;

        float minRatio = CalculateMinScaleRatio(dragStartScale, minScaleAxis);
        ratio = Mathf.Max(ratio, minRatio);

        var scaled = dragStartScale * ratio;
        target.transform.localScale = ClampScale(scaled, minScaleAxis);
    }

    void CommitScaleIfNeeded()
    {
        if (!isScaling)
        {
            ResetScaleState();
            return;
        }

        var endScale = ClampScale(target.transform.localScale, minScaleAxis);
        target.transform.localScale = endScale;

        if ((endScale - dragStartScale).sqrMagnitude > 0.000001f)
        {
            if (CommandService.I != null)
            {
                var cmd = new ScaleObjectCommand(target, dragStartScale, endScale);
                CommandService.I.Stack.Execute(cmd);
            }
        }

        ResetScaleState();
    }

    void ResetScaleState()
    {
        isScaling = false;
        dragStartScale = Vector3.one;
        dragStartScreenDistance = 0f;
    }

    void EnsureLines(int count)
    {
        while (lines.Count < count)
        {
            var go = new GameObject("OutlineLine");
            go.transform.SetParent(transform, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.material = lineMat;
            lr.positionCount = 2;
            lr.useWorldSpace = true;
            lr.widthMultiplier = 0.05f;
            lr.alignment = LineAlignment.View;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            lr.startColor = Color.cyan;
            lr.endColor = Color.cyan;
            lines.Add(lr);
        }

        for (int i = 0; i < lines.Count; i++)
        {
            lines[i].gameObject.SetActive(i < count);
        }
    }

    void UpdateOutline()
    {
        if (target == null) return;

        var bounds = CalculateTargetBounds();
        Vector3 min = bounds.min;
        Vector3 max = bounds.max;

        corners[0] = new Vector3(min.x, min.y, min.z);
        corners[1] = new Vector3(max.x, min.y, min.z);
        corners[2] = new Vector3(max.x, min.y, max.z);
        corners[3] = new Vector3(min.x, min.y, max.z);
        corners[4] = new Vector3(min.x, max.y, min.z);
        corners[5] = new Vector3(max.x, max.y, min.z);
        corners[6] = new Vector3(max.x, max.y, max.z);
        corners[7] = new Vector3(min.x, max.y, max.z);

        edges[0, 0] = corners[0]; edges[0, 1] = corners[1];
        edges[1, 0] = corners[1]; edges[1, 1] = corners[2];
        edges[2, 0] = corners[2]; edges[2, 1] = corners[3];
        edges[3, 0] = corners[3]; edges[3, 1] = corners[0];
        edges[4, 0] = corners[4]; edges[4, 1] = corners[5];
        edges[5, 0] = corners[5]; edges[5, 1] = corners[6];
        edges[6, 0] = corners[6]; edges[6, 1] = corners[7];
        edges[7, 0] = corners[7]; edges[7, 1] = corners[4];
        edges[8, 0] = corners[0]; edges[8, 1] = corners[4];
        edges[9, 0] = corners[1]; edges[9, 1] = corners[5];
        edges[10, 0] = corners[2]; edges[10, 1] = corners[6];
        edges[11, 0] = corners[3]; edges[11, 1] = corners[7];

        EnsureLines(12);
        for (int i = 0; i < 12; i++)
        {
            lines[i].SetPosition(0, edges[i, 0]);
            lines[i].SetPosition(1, edges[i, 1]);
        }
    }

    Bounds CalculateTargetBounds()
    {
        var bounds = new Bounds(target.transform.position, Vector3.one * 0.5f);
        var renderers = target.GetComponentsInChildren<Renderer>();
        if (renderers != null && renderers.Length > 0)
        {
            bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }
        }
        return bounds;
    }

    bool TryGetCenterScreen(Camera cam, out Vector2 centerScreen)
    {
        centerScreen = default;
        if (target == null) return false;

        var centerWorld = CalculateTargetBounds().center;
        var screen = cam.WorldToScreenPoint(centerWorld);
        if (screen.z <= 0f) return false;

        centerScreen = screen;
        return true;
    }

    bool TryGetClosestCornerScreen(Camera cam, Vector2 pointer, out Vector2 nearestCorner, out float nearestDistance)
    {
        nearestCorner = default;
        nearestDistance = float.MaxValue;
        bool found = false;

        for (int i = 0; i < corners.Length; i++)
        {
            var screen = cam.WorldToScreenPoint(corners[i]);
            if (screen.z <= 0f) continue;

            float distance = Vector2.Distance(pointer, screen);
            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearestCorner = screen;
                found = true;
            }
        }

        return found;
    }

    Camera ResolveCamera()
    {
        if (cachedCamera != null) return cachedCamera;

        cachedCamera = Camera.main;
        if (cachedCamera == null)
        {
            cachedCamera = FindFirstObjectByType<Camera>();
        }
        return cachedCamera;
    }

    static float CalculateMinScaleRatio(Vector3 baseScale, float minAxis)
    {
        const float epsilon = 0.0001f;
        float minRatio = 0f;

        float x = Mathf.Abs(baseScale.x);
        float y = Mathf.Abs(baseScale.y);
        float z = Mathf.Abs(baseScale.z);

        if (x > epsilon) minRatio = Mathf.Max(minRatio, minAxis / x);
        if (y > epsilon) minRatio = Mathf.Max(minRatio, minAxis / y);
        if (z > epsilon) minRatio = Mathf.Max(minRatio, minAxis / z);

        return Mathf.Max(minRatio, 0f);
    }

    static Vector3 ClampScale(Vector3 scale, float minAxis)
    {
        return new Vector3(
            ClampScaleAxis(scale.x, minAxis),
            ClampScaleAxis(scale.y, minAxis),
            ClampScaleAxis(scale.z, minAxis));
    }

    static float ClampScaleAxis(float value, float minAxis)
    {
        float sign = value < 0f ? -1f : 1f;
        float abs = Mathf.Abs(value);
        if (abs < minAxis) abs = minAxis;
        return abs * sign;
    }
}
