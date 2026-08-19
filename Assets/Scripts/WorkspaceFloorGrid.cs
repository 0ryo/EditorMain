using UnityEngine;

public class WorkspaceFloorGrid : MonoBehaviour
{
    const string RuntimeName = "WorkspaceFloorGrid_Runtime";
    const int HalfLineCount = 12;
    const float GridStep = 1f;
    const float GridY = 0.015f;
    const float MinorWidth = 0.012f;
    const float MajorWidth = 0.02f;

    static readonly Color MinorColor = new Color(0.72f, 0.72f, 0.72f, 0.42f);
    static readonly Color MajorColor = new Color(0.56f, 0.56f, 0.56f, 0.50f);

    Material lineMaterial;

    public static WorkspaceFloorGrid EnsureExists()
    {
        var existing = UnityEngine.Object.FindFirstObjectByType<WorkspaceFloorGrid>();
        if (existing != null) return existing;

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
        if (transform.childCount > 0) return;

        lineMaterial = CreateLineMaterial();
        float extent = HalfLineCount * GridStep;
        for (int i = -HalfLineCount; i <= HalfLineCount; i++)
        {
            float offset = i * GridStep;
            bool isMajor = i == 0 || i % 5 == 0;
            CreateLine(
                $"Grid_X_{i + HalfLineCount:00}",
                new Vector3(-extent, GridY, offset),
                new Vector3(extent, GridY, offset),
                isMajor);
            CreateLine(
                $"Grid_Z_{i + HalfLineCount:00}",
                new Vector3(offset, GridY, -extent),
                new Vector3(offset, GridY, extent),
                isMajor);
        }
    }

    void CreateLine(string objectName, Vector3 from, Vector3 to, bool isMajor)
    {
        var go = new GameObject(objectName);
        go.transform.SetParent(transform, false);

        var line = go.AddComponent<LineRenderer>();
        line.material = lineMaterial;
        line.positionCount = 2;
        line.useWorldSpace = true;
        line.SetPosition(0, from);
        line.SetPosition(1, to);
        line.widthMultiplier = isMajor ? MajorWidth : MinorWidth;
        line.numCapVertices = 0;
        line.numCornerVertices = 0;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
        line.startColor = isMajor ? MajorColor : MinorColor;
        line.endColor = isMajor ? MajorColor : MinorColor;
    }

    static Material CreateLineMaterial()
    {
        var shader = Shader.Find("Sprites/Default");
        if (shader == null) shader = Shader.Find("Unlit/Color");
        if (shader == null) shader = Shader.Find("Standard");

        var material = new Material(shader);
        material.name = "WorkspaceFloorGrid_Material";
        material.color = Color.white;
        return material;
    }
}
