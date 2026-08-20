using UnityEngine;
using UnityEngine.Rendering;

public class WorkspaceFloorGrid : MonoBehaviour
{
    const string RuntimeName = "WorkspaceFloorGrid_Runtime";
    const string FloorSurfaceName = "Floor_Surface";
    const int BuildRevision = 4;
    const int HalfLineCount = 80;
    const float GridStep = 1f;
    const float SurfaceY = -0.012f;
    const float GridY = 0.012f;
    const float LineWidth = 0.006f;

    static readonly Color SurfaceColor = new Color(0.93f, 0.95f, 0.98f, 0.42f);
    static readonly Color GridLineColor = new Color(0.50f, 0.58f, 0.68f, 0.37f);
    static readonly Color XAxisColor = new Color(0.72f, 0.40f, 0.40f, 0.41f);
    static readonly Color ZAxisColor = new Color(0.38f, 0.50f, 0.72f, 0.41f);

    Material surfaceMaterial;
    Material lineMaterial;
    Material xAxisMaterial;
    Material zAxisMaterial;
    [SerializeField] int builtRevision;

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
        if (builtRevision == BuildRevision && transform.Find(FloorSurfaceName) != null) return;

        ClearGeneratedChildren();
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

            CreateGroundLine(
                $"Grid_X_{i + HalfLineCount:000}",
                new Vector3(-extent, GridY, offset),
                new Vector3(extent, GridY, offset),
                LineWidth,
                lineMaterial);
            CreateGroundLine(
                $"Grid_Z_{i + HalfLineCount:000}",
                new Vector3(offset, GridY, -extent),
                new Vector3(offset, GridY, extent),
                LineWidth,
                lineMaterial);
        }

        CreateGroundLine("Axis_X", new Vector3(-extent, GridY + 0.004f, 0f), new Vector3(extent, GridY + 0.004f, 0f), LineWidth, xAxisMaterial);
        CreateGroundLine("Axis_Z", new Vector3(0f, GridY + 0.006f, -extent), new Vector3(0f, GridY + 0.006f, extent), LineWidth, zAxisMaterial);
        builtRevision = BuildRevision;
    }

    void ClearGeneratedChildren()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            var child = transform.GetChild(i);
            if (Application.isPlaying)
            {
                Destroy(child.gameObject);
            }
            else
            {
                DestroyImmediate(child.gameObject);
            }
        }
    }

    void EnsureMaterials()
    {
        if (surfaceMaterial == null) surfaceMaterial = CreateMaterial("WorkspaceFloor_Surface", SurfaceColor);
        if (lineMaterial == null) lineMaterial = CreateMaterial("WorkspaceFloor_Line", GridLineColor);
        if (xAxisMaterial == null) xAxisMaterial = CreateMaterial("WorkspaceFloor_XAxis", XAxisColor);
        if (zAxisMaterial == null) zAxisMaterial = CreateMaterial("WorkspaceFloor_ZAxis", ZAxisColor);

        lineMaterial.renderQueue = (int)RenderQueue.Transparent + 1;
        xAxisMaterial.renderQueue = (int)RenderQueue.Transparent + 1;
        zAxisMaterial.renderQueue = (int)RenderQueue.Transparent + 1;
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
        renderer.shadowCastingMode = ShadowCastingMode.Off;
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
        ConfigureTransparentMaterial(material);
        return material;
    }

    static void ConfigureTransparentMaterial(Material material)
    {
        material.SetOverrideTag("RenderType", "Transparent");
        material.renderQueue = (int)RenderQueue.Transparent;

        if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 1f);
        if (material.HasProperty("_Blend")) material.SetFloat("_Blend", 0f);
        if (material.HasProperty("_SrcBlend")) material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        if (material.HasProperty("_DstBlend")) material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", 0f);

        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.EnableKeyword("_ALPHABLEND_ON");
    }
}
