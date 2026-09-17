using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class TireChangeModelDiagnostics
{
    static string OutputDirectory => Path.GetFullPath("Library/TireChangeDiagnostics");

    [MenuItem("Tools/Automation/Tire Change/Diagnose Selected Model")]
    public static void DiagnoseSelected()
    {
        var root = Selection.activeGameObject;
        if (!root) throw new InvalidOperationException("Select a model asset or an instantiated model root first.");
        string source = AssetDatabase.GetAssetPath(root);
        Save(ModelHierarchyDiagnostics.Inspect(root, string.IsNullOrEmpty(source) ? root.name : source));
    }

    [MenuItem("Tools/Automation/Tire Change/Diagnose Imported FBX Models")]
    public static void DiagnoseImportedFbx()
    {
        int count = 0;
        foreach (string path in AssetDatabase.GetAllAssetPaths())
        {
            if (!path.StartsWith("Assets/ImportedFbx/", StringComparison.Ordinal) ||
                !path.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase)) continue;
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (!root) throw new InvalidOperationException("Could not load imported model: " + path);
            Save(ModelHierarchyDiagnostics.Inspect(root, path));
            count++;
        }
        Debug.Log($"[TireChange] Diagnosed {count} FBX models. Reports: {OutputDirectory}");
    }

    [MenuItem("Tools/Automation/Tire Change/Diagnose GLB or glTF File")]
    public static async void DiagnoseGltf()
    {
        string path = EditorUtility.OpenFilePanel("Select GLB or glTF", "", "");
        if (string.IsNullOrEmpty(path)) return;
        if (!RuntimeModelLoader.IsSupportedExtension(path))
        {
            Debug.LogError("Select a .glb or .gltf file.");
            return;
        }
        // Preview scene avoids modifying the user's open scene or saving temporary objects.
        var preview = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
        GameObject root = null;
        using var loader = new GLTFast.GltfImport();
        try
        {
            if (!await loader.Load(new Uri(Path.GetFullPath(path)).AbsoluteUri))
                throw new IOException("Could not load glTF: " + path);
            root = new GameObject(Path.GetFileNameWithoutExtension(path));
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, preview);
            if (!await loader.InstantiateMainSceneAsync(root.transform))
                throw new IOException("Could not instantiate glTF: " + path);
            Save(ModelHierarchyDiagnostics.Inspect(root, path));
        }
        catch (Exception ex) { Debug.LogException(ex); }
        finally
        {
            if (root) UnityEngine.Object.DestroyImmediate(root);
            UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(preview);
        }
    }

    static void Save(ModelHierarchyReport report)
    {
        Directory.CreateDirectory(OutputDirectory);
        string stem = CatalogModelImportNaming.SanitizeName(Path.GetFileNameWithoutExtension(report.source));
        string prefix = Path.Combine(OutputDirectory, stem + "-" + Guid.NewGuid().ToString("N"));
        File.WriteAllText(prefix + ".json", JsonUtility.ToJson(report, true), Encoding.UTF8);
        var text = new StringBuilder();
        text.AppendLine("Model hierarchy diagnostic: " + report.source);
        var importer = AssetImporter.GetAtPath(report.source) as ModelImporter;
        if (importer != null)
            text.AppendLine($"Importer: preserveHierarchy={importer.preserveHierarchy}, globalScale={importer.globalScale}, useFileScale={importer.useFileScale}");
        text.AppendLine($"Nodes={report.nodeCount}, Renderers={report.rendererCount}, unique Meshes={report.meshCount}, static geometry candidates={report.staticCandidateCount}");
        foreach (string warning in report.warnings) text.AppendLine("NOTE: " + warning);
        foreach (var node in report.nodes)
        {
            text.AppendLine($"{node.address} parent={node.parentAddress ?? "-"} name={node.name} [{node.classification}]");
            text.AppendLine($"  local position={node.localPosition:F5}, rotation={node.localRotation:F5}, scale={node.localScale:F5}, activeSelf={node.activeSelf}");
            foreach (string mesh in node.meshes) text.AppendLine("  " + mesh);
        }
        text.AppendLine("Review: target wheel / required nut count / mount points / grouping / units / axes / pivots / replacement material needed.");
        text.AppendLine("FBX: Editor import or pre-registered Prefab. Windows Player: GLB/glTF. No Player FBX loader is provided.");
        File.WriteAllText(prefix + ".txt", text.ToString(), Encoding.UTF8);
        Debug.Log("[TireChange] Report written: " + prefix + ".txt");
    }
}
