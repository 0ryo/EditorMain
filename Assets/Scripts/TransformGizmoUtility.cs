using UnityEngine;
using UnityEngine.Rendering;

internal enum GizmoAxis
{
    None = -1,
    X = 0,
    Y = 1,
    Z = 2
}

internal enum GizmoDragMode
{
    None = 0,
    Move = 1,
    Rotate = 2
}

internal static class TransformGizmoUtility
{
    public static float DistanceToSegment(Vector2 point, Vector2 start, Vector2 end, out float t)
    {
        Vector2 segment = end - start;
        float lenSq = segment.sqrMagnitude;
        if (lenSq <= 0.00001f)
        {
            t = 0f;
            return Vector2.Distance(point, start);
        }

        t = Mathf.Clamp01(Vector2.Dot(point - start, segment) / lenSq);
        Vector2 projection = start + segment * t;
        return Vector2.Distance(point, projection);
    }

    public static Vector3 AxisDirection(GizmoAxis axis, Quaternion rotation)
    {
        Vector3 localAxis = axis switch
        {
            GizmoAxis.X => Vector3.right,
            GizmoAxis.Y => Vector3.up,
            GizmoAxis.Z => Vector3.forward,
            _ => Vector3.right
        };

        var axisRotation = TransformToolSettings.CoordinateSpace == TransformCoordinateSpace.Local
            ? rotation
            : Quaternion.identity;
        return (axisRotation * localAxis).normalized;
    }

    public static Vector3 EvaluateArcPoint(Vector3 center, Vector3 axisA, Vector3 axisB, float radius, float t)
    {
        float radians = Mathf.Clamp01(t) * Mathf.PI * 0.5f;
        Vector3 radial = (axisA * Mathf.Cos(radians)) + (axisB * Mathf.Sin(radians));
        return center + radial.normalized * radius;
    }

    public static GizmoAxis GetArcStartAxis(int arcIndex)
    {
        return arcIndex switch
        {
            0 => GizmoAxis.X,
            1 => GizmoAxis.Y,
            2 => GizmoAxis.Z,
            _ => GizmoAxis.X
        };
    }

    public static GizmoAxis GetArcEndAxis(int arcIndex)
    {
        return arcIndex switch
        {
            0 => GizmoAxis.Y,
            1 => GizmoAxis.Z,
            2 => GizmoAxis.X,
            _ => GizmoAxis.Y
        };
    }

    public static GizmoAxis GetArcRotationAxis(int arcIndex)
    {
        return arcIndex switch
        {
            0 => GizmoAxis.Z,
            1 => GizmoAxis.X,
            2 => GizmoAxis.Y,
            _ => GizmoAxis.None
        };
    }

    public static string GetArcLabel(int arcIndex)
    {
        return arcIndex switch
        {
            0 => "XY",
            1 => "YZ",
            2 => "ZX",
            _ => "Unknown"
        };
    }

    public static Mesh CreateConeMesh(int segmentCount)
    {
        int segments = Mathf.Max(8, segmentCount);
        int vertexCount = segments + 2;
        var vertices = new Vector3[vertexCount];
        var normals = new Vector3[vertexCount];
        var uvs = new Vector2[vertexCount];
        int triangleCount = segments * 2;
        var triangles = new int[triangleCount * 3];

        vertices[0] = new Vector3(0f, 0f, 1f);
        normals[0] = Vector3.forward;
        uvs[0] = new Vector2(0.5f, 1f);

        vertices[1] = Vector3.zero;
        normals[1] = Vector3.back;
        uvs[1] = new Vector2(0.5f, 0.5f);

        for (int i = 0; i < segments; i++)
        {
            float t = i / (float)segments;
            float angle = t * Mathf.PI * 2f;
            float x = Mathf.Cos(angle);
            float y = Mathf.Sin(angle);
            int v = i + 2;
            vertices[v] = new Vector3(x, y, 0f);
            normals[v] = new Vector3(x, y, 0.35f).normalized;
            uvs[v] = new Vector2((x + 1f) * 0.5f, (y + 1f) * 0.5f);
        }

        int tri = 0;
        for (int i = 0; i < segments; i++)
        {
            int current = i + 2;
            int next = ((i + 1) % segments) + 2;

            triangles[tri++] = 0;
            triangles[tri++] = current;
            triangles[tri++] = next;

            triangles[tri++] = 1;
            triangles[tri++] = next;
            triangles[tri++] = current;
        }

        var mesh = new Mesh
        {
            name = "RuntimeGizmoCone"
        };
        mesh.vertices = vertices;
        mesh.normals = normals;
        mesh.uv = uvs;
        mesh.triangles = triangles;
        mesh.RecalculateBounds();
        return mesh;
    }

    public static void ConfigureAlwaysOnTopMaterial(Material material)
    {
        if (material == null) return;

        material.renderQueue = (int)RenderQueue.Overlay;
        SetMaterialIntIfPresent(material, "_ZWrite", 0);
        SetMaterialIntIfPresent(material, "_ZTest", (int)CompareFunction.Always);
        SetMaterialIntIfPresent(material, "_Cull", (int)CullMode.Off);
        SetMaterialIntIfPresent(material, "_SrcBlend", (int)BlendMode.SrcAlpha);
        SetMaterialIntIfPresent(material, "_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
    }

    public static void SetMaterialColor(Material material, Color color)
    {
        if (material == null) return;

        if (material.HasProperty("_Color"))
        {
            material.SetColor("_Color", color);
        }

        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", color);
        }
    }

    static void SetMaterialIntIfPresent(Material material, string propertyName, int value)
    {
        if (material == null || !material.HasProperty(propertyName)) return;
        material.SetInt(propertyName, value);
    }
}
