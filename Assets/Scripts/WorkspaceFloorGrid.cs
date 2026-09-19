using UnityEngine;
using UnityEngine.Rendering;

public class WorkspaceFloorGrid : MonoBehaviour
{
    const string RuntimeName = "WorkspaceFloorGrid_Runtime";
    const string FloorSurfaceName = "Floor_Surface";
    const int BuildRevision = 7;
    const int HalfLineCount = 80;
    const float GridStep = 1f;
    const float GridY = 0.012f;
    const float BaseLineWidth = 0.006f;
    const float DefaultWorldUnitsPerPixel = 14f / 1080f;
    const float GridLineWidth = BaseLineWidth;
    const float AxisLineWidth = BaseLineWidth + DefaultWorldUnitsPerPixel * 2f;

    static readonly Color GridLineColor = new Color32(0x4F, 0x4F, 0x4F, 0xFF);
    static readonly Color XAxisColor = new Color(0.72f, 0.40f, 0.40f, 0.41f);
    static readonly Color ZAxisColor = new Color(0.38f, 0.50f, 0.72f, 0.41f);

    Material lineMaterial;
    Material xAxisMaterial;
    Material zAxisMaterial;
    [SerializeField] int builtRevision;
    Renderer legacyFloor;
    bool legacyFloorWasEnabled;

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
        // Only the legacy workspace floor; never hide imported or placed models.
        foreach (var root in gameObject.scene.GetRootGameObjects())
        {
            if (root.name != "Floor" || root.GetComponentInChildren<PlacedObject>(true) != null) continue;
            legacyFloor = root.GetComponent<Renderer>();
            if (legacyFloor == null) continue;
            legacyFloorWasEnabled = legacyFloor.enabled;
            legacyFloor.enabled = false;
            break;
        }
    }

    void OnDestroy()
    {
        if (legacyFloor != null) legacyFloor.enabled = legacyFloorWasEnabled;
        ReleaseGeneratedMeshes();
        ReleaseObject(lineMaterial);
        ReleaseObject(xAxisMaterial);
        ReleaseObject(zAxisMaterial);
    }

    static void ReleaseObject(Object value)
    {
        if (value == null) return;
        if (Application.isPlaying) Destroy(value);
        else DestroyImmediate(value);
    }

    void ReleaseGeneratedMeshes()
    {
        foreach (var filter in GetComponentsInChildren<MeshFilter>(true))
            if (filter.sharedMesh != null && filter.sharedMesh.name == filter.name + "_Mesh")
                ReleaseObject(filter.sharedMesh);
    }

    void BuildGrid()
    {
        if (builtRevision == BuildRevision && transform.Find("Axis_X") != null && transform.Find(FloorSurfaceName) == null) return;

        ClearGeneratedChildren();
        EnsureMaterials();
        float extent = HalfLineCount * GridStep;

        for (int i = -HalfLineCount; i <= HalfLineCount; i++)
        {
            float offset = i * GridStep;

            CreateGroundLine(
                $"Grid_X_{i + HalfLineCount:000}",
                new Vector3(-extent, GridY, offset),
                new Vector3(extent, GridY, offset),
                GridLineWidth,
                lineMaterial);
            CreateGroundLine(
                $"Grid_Z_{i + HalfLineCount:000}",
                new Vector3(offset, GridY, -extent),
                new Vector3(offset, GridY, extent),
                GridLineWidth,
                lineMaterial);
        }

        CreateGroundLine("Axis_X", new Vector3(-extent, GridY + 0.004f, 0f), new Vector3(extent, GridY + 0.004f, 0f), AxisLineWidth, xAxisMaterial);
        CreateGroundLine("Axis_Z", new Vector3(0f, GridY + 0.006f, -extent), new Vector3(0f, GridY + 0.006f, extent), AxisLineWidth, zAxisMaterial);
        builtRevision = BuildRevision;
    }

    void ClearGeneratedChildren()
    {
        ReleaseGeneratedMeshes();
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            var child = transform.GetChild(i);
            child.gameObject.SetActive(false);
            // Destroy is deferred in PlayMode; detach so another Ensure in this frame is idempotent.
            if (Application.isPlaying) child.SetParent(null, true);
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
