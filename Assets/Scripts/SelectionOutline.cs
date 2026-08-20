using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class SelectionOutline : MonoBehaviour
{
    [SerializeField] Material lineMat;
    [SerializeField] float handlePickRadiusPixels = 18f;
    [SerializeField] float minScaleAxis = 0.1f;
    [SerializeField] bool enableDiagnostics = true;

    readonly List<LineRenderer> lines = new();
    readonly Vector3[] corners = new Vector3[8];
    readonly Vector3[,] edges = new Vector3[12, 2];

    GameObject target;
    Camera cachedCamera;
    bool isScaling;
    bool scaleCursorActive;
    Vector3 dragStartScale;
    float dragStartScreenDistance;
    Material runtimeLineMaterial;
    Texture2D scaleCursorTexture;

    void Update()
    {
        if (target == null)
        {
            EnsureLines(0);
            ResetScaleState();
            SetScaleCursor(false);
            return;
        }

        UpdateOutline();
        UpdateScaleCursor();
        HandleScaleDrag();
    }

    public void ShowFor(GameObject t)
    {
        target = t;
        ResetScaleState();

        if (target == null)
        {
            EnsureLines(0);
            SetScaleCursor(false);
            return;
        }

        UpdateOutline();
    }

    public bool ShouldConsumeSelectionClick()
    {
        if (target == null || !IsScaleMode()) return false;
        if (!EditInput.LeftPressedThisFrame()) return false;
        if (PlacementController.IsScreenPositionOverBlockingUi(EditInput.MousePosition)) return false;

        var cam = ResolveCamera();
        if (cam == null) return false;

        UpdateOutline();
        return TryGetClosestCornerScreen(cam, EditInput.MousePosition, out _, out var distance) &&
               distance <= handlePickRadiusPixels;
    }

    void HandleScaleDrag()
    {
        if (!IsScaleMode())
        {
            if (isScaling)
            {
                CommitScaleIfNeeded();
            }
            return;
        }

        if (!isScaling)
        {
            if (!EditInput.LeftPressedThisFrame()) return;
            if (PlacementController.IsScreenPositionOverBlockingUi(EditInput.MousePosition)) return;
            TryBeginScaleDrag(EditInput.MousePosition);
            return;
        }

        if (!EditInput.LeftPressed())
        {
            CommitScaleIfNeeded();
            return;
        }

        ApplyScaleFromPointer(EditInput.MousePosition);
    }

    void UpdateScaleCursor()
    {
        bool shouldShow = isScaling;
        if (!shouldShow && IsScaleMode() &&
            !PlacementController.IsScreenPositionOverBlockingUi(EditInput.MousePosition))
        {
            var cam = ResolveCamera();
            shouldShow = cam != null &&
                         TryGetClosestCornerScreen(cam, EditInput.MousePosition, out _, out var distance) &&
                         distance <= handlePickRadiusPixels;
        }

        SetScaleCursor(shouldShow);
    }

    void TryBeginScaleDrag(Vector2 pointer)
    {
        var cam = ResolveCamera();
        if (cam == null) return;

        if (!TryGetCenterScreen(cam, out var centerScreen)) return;
        if (!TryGetClosestCornerScreen(cam, pointer, out var nearestCorner, out var nearestDistance)) return;
        if (nearestDistance > handlePickRadiusPixels) return;

        dragStartScale = target.transform.localScale;
        dragStartScreenDistance = Vector2.Distance(centerScreen, nearestCorner);
        if (dragStartScreenDistance <= 0.001f) return;

        isScaling = true;
        LogDebug($"Scale drag started. target={target.name}, pointer={pointer}");
    }

    void ApplyScaleFromPointer(Vector2 pointer)
    {
        var cam = ResolveCamera();
        if (cam == null) return;
        if (!TryGetCenterScreen(cam, out var centerScreen)) return;

        float currentDistance = Vector2.Distance(centerScreen, pointer);
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

            LogDebug($"Scale drag committed. target={target.name}, from={dragStartScale}, to={endScale}");
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
            lr.material = GetRuntimeLineMaterial();
            lr.positionCount = 2;
            lr.useWorldSpace = true;
            lr.widthMultiplier = 0.05f;
            lr.alignment = LineAlignment.View;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            lr.textureMode = LineTextureMode.Stretch;
            lr.sortingOrder = short.MaxValue;
            lr.startColor = DesignTokens.Accent;
            lr.endColor = DesignTokens.Accent;
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

        var bounds = CalculateTargetLocalBounds();
        Vector3 min = bounds.min;
        Vector3 max = bounds.max;

        corners[0] = target.transform.TransformPoint(new Vector3(min.x, min.y, min.z));
        corners[1] = target.transform.TransformPoint(new Vector3(max.x, min.y, min.z));
        corners[2] = target.transform.TransformPoint(new Vector3(max.x, min.y, max.z));
        corners[3] = target.transform.TransformPoint(new Vector3(min.x, min.y, max.z));
        corners[4] = target.transform.TransformPoint(new Vector3(min.x, max.y, min.z));
        corners[5] = target.transform.TransformPoint(new Vector3(max.x, max.y, min.z));
        corners[6] = target.transform.TransformPoint(new Vector3(max.x, max.y, max.z));
        corners[7] = target.transform.TransformPoint(new Vector3(min.x, max.y, max.z));

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

    Bounds CalculateTargetLocalBounds()
    {
        var bounds = new Bounds(Vector3.zero, Vector3.one * 0.5f);
        var renderers = target.GetComponentsInChildren<Renderer>(true);
        bool hasPoint = false;

        for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
        {
            var renderer = renderers[rendererIndex];
            var rendererBounds = renderer.localBounds;
            Vector3 min = rendererBounds.min;
            Vector3 max = rendererBounds.max;

            for (int cornerIndex = 0; cornerIndex < 8; cornerIndex++)
            {
                var rendererLocalPoint = new Vector3(
                    (cornerIndex & 1) == 0 ? min.x : max.x,
                    (cornerIndex & 2) == 0 ? min.y : max.y,
                    (cornerIndex & 4) == 0 ? min.z : max.z);
                Vector3 worldPoint = renderer.transform.TransformPoint(rendererLocalPoint);
                Vector3 targetLocalPoint = target.transform.InverseTransformPoint(worldPoint);

                if (!hasPoint)
                {
                    bounds = new Bounds(targetLocalPoint, Vector3.zero);
                    hasPoint = true;
                }
                else
                {
                    bounds.Encapsulate(targetLocalPoint);
                }
            }
        }

        return bounds;
    }

    bool TryGetCenterScreen(Camera cam, out Vector2 centerScreen)
    {
        centerScreen = default;
        if (target == null) return false;

        var centerWorld = target.transform.TransformPoint(CalculateTargetLocalBounds().center);
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

    bool IsScaleMode()
    {
        return EditModeService.I != null && EditModeService.I.Mode == EditMode.Scale;
    }

    Material GetRuntimeLineMaterial()
    {
        if (runtimeLineMaterial != null) return runtimeLineMaterial;

        if (lineMat != null)
        {
            runtimeLineMaterial = new Material(lineMat);
        }
        else
        {
            var shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            runtimeLineMaterial = new Material(shader);
        }

        runtimeLineMaterial.name = "SelectionOutline_Runtime";
        runtimeLineMaterial.hideFlags = HideFlags.HideAndDontSave;
        if (runtimeLineMaterial.HasProperty("_Color")) runtimeLineMaterial.SetColor("_Color", Color.white);
        if (runtimeLineMaterial.HasProperty("_BaseColor")) runtimeLineMaterial.SetColor("_BaseColor", Color.white);
        runtimeLineMaterial.renderQueue = (int)RenderQueue.Transparent;
        return runtimeLineMaterial;
    }

    void SetScaleCursor(bool active)
    {
        if (scaleCursorActive == active) return;

        scaleCursorActive = active;
        Cursor.SetCursor(active ? GetScaleCursorTexture() : null, active ? new Vector2(16f, 16f) : Vector2.zero, CursorMode.Auto);
    }

    Texture2D GetScaleCursorTexture()
    {
        if (scaleCursorTexture != null) return scaleCursorTexture;

        const int size = 32;
        scaleCursorTexture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "ScaleHorizontalCursor_Runtime",
            hideFlags = HideFlags.HideAndDontSave,
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
        };
        scaleCursorTexture.SetPixels(new Color[size * size]);

        var segments = new[]
        {
            new[] { new Vector2Int(5, 16), new Vector2Int(26, 16) },
            new[] { new Vector2Int(5, 16), new Vector2Int(11, 10) },
            new[] { new Vector2Int(5, 16), new Vector2Int(11, 22) },
            new[] { new Vector2Int(26, 16), new Vector2Int(20, 10) },
            new[] { new Vector2Int(26, 16), new Vector2Int(20, 22) },
        };

        for (int i = 0; i < segments.Length; i++)
        {
            DrawCursorLine(scaleCursorTexture, segments[i][0], segments[i][1], Color.black, 1);
        }
        for (int i = 0; i < segments.Length; i++)
        {
            DrawCursorLine(scaleCursorTexture, segments[i][0], segments[i][1], Color.white, 0);
        }

        scaleCursorTexture.Apply(false, false);
        return scaleCursorTexture;
    }

    static void DrawCursorLine(Texture2D texture, Vector2Int from, Vector2Int to, Color color, int radius)
    {
        int steps = Mathf.Max(Mathf.Abs(to.x - from.x), Mathf.Abs(to.y - from.y));
        for (int step = 0; step <= steps; step++)
        {
            float t = steps == 0 ? 0f : (float)step / steps;
            int x = Mathf.RoundToInt(Mathf.Lerp(from.x, to.x, t));
            int y = Mathf.RoundToInt(Mathf.Lerp(from.y, to.y, t));

            for (int offsetY = -radius; offsetY <= radius; offsetY++)
            {
                for (int offsetX = -radius; offsetX <= radius; offsetX++)
                {
                    int pixelX = x + offsetX;
                    int pixelY = y + offsetY;
                    if (pixelX < 0 || pixelX >= texture.width || pixelY < 0 || pixelY >= texture.height) continue;
                    texture.SetPixel(pixelX, pixelY, color);
                }
            }
        }
    }

    void OnDisable()
    {
        SetScaleCursor(false);
    }

    void OnDestroy()
    {
        SetScaleCursor(false);
        if (runtimeLineMaterial != null)
        {
            Destroy(runtimeLineMaterial);
        }

        if (scaleCursorTexture != null)
        {
            Destroy(scaleCursorTexture);
        }
    }

    void LogDebug(string message)
    {
        if (!enableDiagnostics) return;
        Debug.Log("[SelectionOutline] " + message);
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
