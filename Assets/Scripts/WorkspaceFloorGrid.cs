using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class WorkspaceFloorGrid : MonoBehaviour
{
    const string RuntimeName = "WorkspaceFloorGrid_Runtime";
    const string FloorSurfaceName = "Floor_Surface";
    const int HalfLineCount = 16;
    const float GridStep = 1f;
    const float SurfaceY = -0.012f;
    const float GridY = 0.018f;
    const float LabelY = 0.18f;
    const float MinorWidth = 0.018f;
    const float MajorWidth = 0.035f;
    const float AxisWidth = 0.075f;
    const float ArrowSize = 0.7f;

    static readonly Color SurfaceColor = new Color(0.90f, 0.93f, 0.97f, 1f);
    static readonly Color MinorColor = new Color(0.70f, 0.76f, 0.84f, 1f);
    static readonly Color MajorColor = new Color(0.52f, 0.60f, 0.70f, 1f);
    static readonly Color XAxisColor = new Color(0.86f, 0.25f, 0.22f, 1f);
    static readonly Color ZAxisColor = new Color(0.13f, 0.39f, 0.92f, 1f);
    static readonly Color OriginColor = new Color(0.12f, 0.12f, 0.13f, 1f);

    readonly List<Transform> labels = new();
    Material surfaceMaterial;
    Material minorMaterial;
    Material majorMaterial;
    Material xAxisMaterial;
    Material zAxisMaterial;
    Material originMaterial;

    public static WorkspaceFloorGrid EnsureExists()
    {
        var existing = UnityEngine.Object.FindFirstObjectByType<WorkspaceFloorGrid>();
        if (existing != null)
        {
            existing.BuildGrid();
            return existing;
        }

        var go = new GameObject(RuntimeName);
        var grid = go.AddComponent<WorkspaceFloorGrid>();
        grid.BuildGrid();
        return grid;
    }

    void Awake()
    {
        BuildGrid();
    }

    void LateUpdate()
    {
        var cam = Camera.main;
        if (cam == null) return;

        foreach (var label in labels)
        {
            if (label == null) continue;
            var toCamera = label.position - cam.transform.position;
            if (toCamera.sqrMagnitude <= 0.0001f) continue;
            label.rotation = Quaternion.LookRotation(toCamera.normalized, Vector3.up);
        }
    }

    void BuildGrid()
    {
        if (transform.Find(FloorSurfaceName) != null) return;

        EnsureMaterials();
        labels.Clear();

        float extent = HalfLineCount * GridStep;
        CreateQuad(
            FloorSurfaceName,
            new[]
            {
                new Vector3(-extent, SurfaceY, -extent),
                new Vector3(-extent, SurfaceY, extent),
                new Vector3(extent, SurfaceY, extent),
                new Vector3(extent, SurfaceY, -extent),
            },
            surfaceMaterial);

        for (int i = -HalfLineCount; i <= HalfLineCount; i++)
        {
            float offset = i * GridStep;
            bool isMajor = i == 0 || i % 5 == 0;
            var material = isMajor ? majorMaterial : minorMaterial;
            float width = isMajor ? MajorWidth : MinorWidth;

            CreateGroundLine(
                $"Grid_X_{i + HalfLineCount:00}",
                new Vector3(-extent, GridY, offset),
                new Vector3(extent, GridY, offset),
                width,
                material);
            CreateGroundLine(
                $"Grid_Z_{i + HalfLineCount:00}",
                new Vector3(offset, GridY, -extent),
                new Vector3(offset, GridY, extent),
                width,
                material);
        }

        CreateGroundLine("Axis_X_Positive", Vector3.zero, new Vector3(extent, GridY + 0.012f, 0f), AxisWidth, xAxisMaterial);
        CreateGroundLine("Axis_Z_Positive", Vector3.zero, new Vector3(0f, GridY + 0.016f, extent), AxisWidth, zAxisMaterial);
        CreateArrowHead("Arrow_X_Positive", new Vector3(extent, GridY + 0.014f, 0f), Vector3.right, xAxisMaterial);
        CreateArrowHead("Arrow_Z_Positive", new Vector3(0f, GridY + 0.018f, extent), Vector3.forward, zAxisMaterial);
        CreateGroundLine("Origin_Mark_X", new Vector3(-0.28f, GridY + 0.024f, 0f), new Vector3(0.28f, GridY + 0.024f, 0f), AxisWidth, originMaterial);
        CreateGroundLine("Origin_Mark_Z", new Vector3(0f, GridY + 0.026f, -0.28f), new Vector3(0f, GridY + 0.026f, 0.28f), AxisWidth, originMaterial);

        CreateLabel("Label_X_Positive", "X+", new Vector3(extent + 1.15f, LabelY, 0f), XAxisColor);
        CreateLabel("Label_Z_Positive", "Z+", new Vector3(0f, LabelY, extent + 1.15f), ZAxisColor);
        CreateLabel("Label_Origin", "Origin", new Vector3(0.9f, LabelY, 0.9f), OriginColor);
    }

    void EnsureMaterials()
    {
        if (surfaceMaterial == null) surfaceMaterial = CreateMaterial("WorkspaceFloor_Surface", SurfaceColor);
        if (minorMaterial == null) minorMaterial = CreateMaterial("WorkspaceFloor_MinorLine", MinorColor);
        if (majorMaterial == null) majorMaterial = CreateMaterial("WorkspaceFloor_MajorLine", MajorColor);
        if (xAxisMaterial == null) xAxisMaterial = CreateMaterial("WorkspaceFloor_XAxis", XAxisColor);
        if (zAxisMaterial == null) zAxisMaterial = CreateMaterial("WorkspaceFloor_ZAxis", ZAxisColor);
        if (originMaterial == null) originMaterial = CreateMaterial("WorkspaceFloor_Origin", OriginColor);
    }

    void CreateGroundLine(string objectName, Vector3 from, Vector3 to, float width, Material material)
    {
        var direction = to - from;
        if (direction.sqrMagnitude <= 0.0001f) return;

        direction.Normalize();
        var side = Vector3.Cross(Vector3.up, direction).normalized * (width * 0.5f);
        CreateQuad(
            objectName,
            new[]
            {
                from - side,
                to - side,
                to + side,
                from + side,
            },
            material);
    }

    void CreateArrowHead(string objectName, Vector3 tip, Vector3 direction, Material material)
    {
        if (direction.sqrMagnitude <= 0.0001f) return;

        direction.Normalize();
        var side = Vector3.Cross(Vector3.up, direction).normalized * (ArrowSize * 0.42f);
        var back = tip - (direction * ArrowSize);
        CreateMesh(
            objectName,
            new[]
            {
                tip,
                back - side,
                back + side,
            },
            new[] { 0, 1, 2, 2, 1, 0 },
            material);
    }

    void CreateQuad(string objectName, Vector3[] vertices, Material material)
    {
        CreateMesh(
            objectName,
            vertices,
            new[] { 0, 1, 2, 0, 2, 3, 2, 1, 0, 3, 2, 0 },
            material);
    }

    void CreateMesh(string objectName, Vector3[] vertices, int[] triangles, Material material)
    {
        var go = new GameObject(objectName, typeof(MeshFilter), typeof(MeshRenderer));
        go.transform.SetParent(transform, false);

        var mesh = new Mesh { name = objectName + "_Mesh" };
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        go.GetComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = go.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
    }

    void CreateLabel(string objectName, string textValue, Vector3 position, Color color)
    {
        var go = new GameObject(objectName, typeof(TextMeshPro));
        go.transform.SetParent(transform, false);
        go.transform.position = position;

        var text = go.GetComponent<TextMeshPro>();
        text.text = textValue;
        text.fontSize = 1.1f;
        text.alignment = TextAlignmentOptions.Center;
        text.color = color;
        text.enableWordWrapping = false;

        labels.Add(go.transform);
    }

    static Material CreateMaterial(string name, Color color)
    {
        var shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Unlit/Color");
        if (shader == null) shader = Shader.Find("Standard");

        var material = new Material(shader)
        {
            name = name,
            color = color
        };

        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color")) material.SetColor("_Color", color);
        return material;
    }
}
