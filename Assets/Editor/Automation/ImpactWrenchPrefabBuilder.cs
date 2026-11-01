using System;
using UnityEditor;
using UnityEngine;

// Builds the impact wrench used in the tire-change task from built-in meshes and registers it in DefaultRegistry.
// Real size (about 26 cm long, standing on its battery) with a 21 mm wheel-nut socket on the 1/2" anvil.
public static class ImpactWrenchPrefabBuilder
{
    public const string TypeId = "Tool/ImpactWrench";
    const string PrefabPath = "Assets/Prefabs/Tool_ImpactWrench.prefab";
    const string MaterialFolder = "Assets/Materials/ImpactWrench";

    [MenuItem("Tools/Automation/Tire Change/Build Impact Wrench")]
    public static void Build()
    {
        var body = Material("Body", new Color(0.92f, 0.45f, 0.10f), 0f, 0.45f);
        var rubber = Material("Rubber", new Color(0.08f, 0.08f, 0.09f), 0f, 0.2f);
        var housing = Material("Housing", new Color(0.55f, 0.56f, 0.58f), 0.9f, 0.55f);
        var chrome = Material("Chrome", new Color(0.80f, 0.81f, 0.83f), 1f, 0.85f);

        var root = new GameObject("Tool_ImpactWrench");
        try
        {
            // Pivot at the bottom centre; +Z is the direction the socket points.
            Part(root, "Battery", PrimitiveType.Cube, rubber, new Vector3(0f, 0.03f, -0.01f), Vector3.zero, new Vector3(0.075f, 0.06f, 0.11f));
            Part(root, "BatteryMount", PrimitiveType.Cube, body, new Vector3(0f, 0.07f, -0.01f), Vector3.zero, new Vector3(0.065f, 0.02f, 0.09f));
            Part(root, "Grip", PrimitiveType.Cube, rubber, new Vector3(0f, 0.135f, -0.02f), new Vector3(-12f, 0f, 0f), new Vector3(0.038f, 0.12f, 0.05f));
            Part(root, "Trigger", PrimitiveType.Cube, rubber, new Vector3(0f, 0.17f, 0.015f), Vector3.zero, new Vector3(0.012f, 0.03f, 0.015f));
            Part(root, "MotorBody", PrimitiveType.Cylinder, body, new Vector3(0f, 0.225f, -0.005f), new Vector3(90f, 0f, 0f), new Vector3(0.065f, 0.06f, 0.065f));
            Part(root, "RearCap", PrimitiveType.Cylinder, rubber, new Vector3(0f, 0.225f, -0.075f), new Vector3(90f, 0f, 0f), new Vector3(0.06f, 0.01f, 0.06f));
            Part(root, "HammerCase", PrimitiveType.Cylinder, housing, new Vector3(0f, 0.225f, 0.08f), new Vector3(90f, 0f, 0f), new Vector3(0.055f, 0.025f, 0.055f));
            Part(root, "Anvil", PrimitiveType.Cube, chrome, new Vector3(0f, 0.225f, 0.115f), Vector3.zero, new Vector3(0.0127f, 0.0127f, 0.02f));
            Part(root, "Socket21mm", PrimitiveType.Cylinder, chrome, new Vector3(0f, 0.225f, 0.145f), new Vector3(90f, 0f, 0f), new Vector3(0.03f, 0.025f, 0.03f));

            var bounds = new Bounds(root.transform.position, Vector3.zero);
            foreach (var renderer in root.GetComponentsInChildren<Renderer>()) bounds.Encapsulate(renderer.bounds);
            var box = root.AddComponent<BoxCollider>();
            box.center = bounds.center - root.transform.position;
            box.size = bounds.size;

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath, out bool saved);
            if (!saved || prefab == null) throw new InvalidOperationException("[ImpactWrench] Failed to save " + PrefabPath);
            Register(prefab);
            Debug.Log($"[ImpactWrench] Built {PrefabPath} size={bounds.size} and registered {TypeId}");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    static void Part(GameObject root, string name, PrimitiveType type, Material material, Vector3 position, Vector3 euler, Vector3 scale)
    {
        var part = GameObject.CreatePrimitive(type);
        UnityEngine.Object.DestroyImmediate(part.GetComponent<Collider>());
        part.name = name;
        part.transform.SetParent(root.transform, false);
        part.transform.localPosition = position;
        part.transform.localRotation = Quaternion.Euler(euler);
        part.transform.localScale = scale;
        part.GetComponent<MeshRenderer>().sharedMaterial = material;
    }

    static Material Material(string name, Color color, float metallic, float smoothness)
    {
        if (!AssetDatabase.IsValidFolder("Assets/Materials")) AssetDatabase.CreateFolder("Assets", "Materials");
        if (!AssetDatabase.IsValidFolder(MaterialFolder)) AssetDatabase.CreateFolder("Assets/Materials", "ImpactWrench");
        string path = MaterialFolder + "/ImpactWrench_" + name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) throw new InvalidOperationException("[ImpactWrench] URP Lit shader not found");
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
        }
        material.SetColor("_BaseColor", color);
        material.SetFloat("_Metallic", metallic);
        material.SetFloat("_Smoothness", smoothness);
        EditorUtility.SetDirty(material);
        return material;
    }

    static void Register(GameObject prefab)
    {
        var registry = AssetDatabase.LoadAssetAtPath<PrefabRegistry>(PrefabRegistry.DefaultAssetPath);
        if (registry == null) throw new InvalidOperationException("[ImpactWrench] Missing " + PrefabRegistry.DefaultAssetPath);
        var entry = registry.entries.Find(e => e != null && e.typeId == TypeId);
        if (entry == null) registry.entries.Add(entry = new PrefabEntry { typeId = TypeId });
        entry.prefab = prefab;
        EditorUtility.SetDirty(registry);
        AssetDatabase.SaveAssets();
    }
}
