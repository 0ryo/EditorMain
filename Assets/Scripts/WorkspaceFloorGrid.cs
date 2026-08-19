using UnityEngine;

public class WorkspaceFloorGrid : MonoBehaviour
{
    const string RuntimeName = "WorkspaceFloorGrid_Runtime";
    const string FloorSurfaceName = "Floor_Surface";
    const int HalfLineCount = 12;
    const float GridStep = 1f;
    const float SurfaceY = -0.012f;
    const float GridY = 0.012f;
    const float MinorWidth = 0.008f;
    const float MajorWidth = 0.014f;
    const float AxisWidth = 0.02f;

    static readonly Color SurfaceColor = new Color(0.935f, 0.945f, 0.958f, 1f);
    static readonly Color MinorColor = new Color(0.78f, 0.81f, 0.86f, 1f);
    static readonly Color MajorColor = new Color(0.68f, 0.72f, 0.78f, 1f);
    static readonly Color XAxisColor = new Color(0.78f, 0.45f, 0.43f, 1f);
    static readonly Color ZAxisColor = new Color(0.42f, 0.55f, 0.78f, 1f);

    Material surfaceMaterial;
    Material minorMaterial;
    Material majorMaterial;
    Material xAxisMaterial;
    Material zAxisMaterial;

    public static WorkspaceFloorGrid EnsureExists()
    {
        var existing = Object.FindFirstObjectByType<WorkspaceFloorGrid>();
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

    void BuildGrid()
    {
        if (transform.Find(FloorSurfaceName) != null) return;

        EnsureMaterials();
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
            bool isMajor = i != 0 && i % 4 == 0;
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

        CreateGroundLine("Axis_X", new Vector3(-extent, GridY + 0.004f, 0f), new Vector3(extent, GridY + 0.004f, 0f), AxisWidth, xAxisMaterial);
        CreateGroundLine("Axis_Z", new Vector3(0f, GridY + 0.006f, -extent), new Vector3(0f, GridY + 0.006f, extent), AxisWidth, zAxisMaterial);
    }

    void EnsureMaterials()
    {
        if (surfaceMaterial == null) surfaceMaterial = CreateMaterial("WorkspaceFloor_Surface", SurfaceColor);
        if (minorMaterial == null) minorMaterial = CreateMaterial("WorkspaceFloor_MinorLine", MinorColor);
        if (majorMaterial == null) majorMaterial = CreateMaterial("WorkspaceFloor_MajorLine", MajorColor);
        if (xAxisMaterial == null) xAxisMaterial = CreateMaterial("WorkspaceFloor_XAxis", XAxisColor);
        if (zAxisMaterial == null) zAxisMaterial = CreateMaterial("WorkspaceFloor_ZAxis", ZAxisColor);
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

    void CreateQuad(string objectName, Vector3[] vertices, Material material)
    {
        var go = new GameObject(objectName, typeof(MeshFilter), typeof(MeshRenderer));
        go.transform.SetParent(transform, false);

        var mesh = new Mesh { name = objectName + "_Mesh" };
        mesh.vertices = vertices;
        mesh.triangles = new[] { 0, 1, 2, 0, 2, 3, 2, 1, 0, 3, 2, 0 };
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        go.GetComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = go.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
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
