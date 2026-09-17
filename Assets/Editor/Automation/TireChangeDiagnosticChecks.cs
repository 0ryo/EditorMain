using System;
using UnityEditor;
using UnityEngine;

public static class TireChangeDiagnosticChecks
{
    [MenuItem("Tools/Automation/Tire Change/Check Diagnostics")]
    public static void Run()
    {
        var preview = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
        var root = new GameObject("Diagnostic fixture");
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, preview);
        var mesh = new Mesh { name = "Shared geometry" };
        try
        {
            var first = AddMesh(root.transform, "Nut", mesh);
            var second = AddMesh(root.transform, "Nut", mesh);
            second.transform.localPosition = new Vector3(1f, 2f, 3f);
            second.SetActive(false);
            var empty = new GameObject("Container");
            empty.transform.SetParent(root.transform, false);
            var report = ModelHierarchyDiagnostics.Inspect(root);
            Require(report.nodeCount == 4 && report.rendererCount == 2 && report.meshCount == 1, "Inactive nodes and shared meshes");
            Require(report.staticCandidateCount == 2, "Containers are not mesh candidates");
            Require(report.nodes[1].address != report.nodes[2].address, "Duplicate names remain distinguishable");
            Require(report.nodes[2].parentAddress == "0" && report.nodes[2].localPosition == new Vector3(1f, 2f, 3f), "Parent and local transform retained");
            Require(!second.activeSelf && root.transform.childCount == 3, "Inspection does not modify source");

            var lod = root.AddComponent<LODGroup>();
            lod.SetLODs(new[] { new LOD(0.5f, new[] { first.GetComponent<Renderer>() }) });
            report = ModelHierarchyDiagnostics.Inspect(root);
            Require(report.staticCandidateCount == 1 && report.nodes[1].lodMember, "LOD geometry excluded from individual candidates");
            var skin = empty.AddComponent<SkinnedMeshRenderer>();
            skin.sharedMesh = mesh;
            skin.bones = new[] { second.transform };
            report = ModelHierarchyDiagnostics.Inspect(root);
            Require(report.skinnedRendererCount == 1 && report.staticCandidateCount == 0, "Skinned geometry and bone transforms excluded");
            Require(report.nodes[2].bone, "Bone association recorded");
            var roundTrip = JsonUtility.FromJson<ModelHierarchyReport>(JsonUtility.ToJson(report));
            Require(roundTrip.nodes.Count == 4 && roundTrip.nodes[2].name == "Nut", "Diagnostic JSON round trip");

            // A single Renderer is intentionally not interpreted as a recoverable assembly.
            var single = ModelHierarchyDiagnostics.Inspect(first);
            Require(single.nodeCount == 1 && single.rendererCount == 1 && single.warnings.Count >= 3, "Single-mesh limitation reported");
            Debug.Log("[TireChange] All diagnostic checks passed. Synthetic geometry verifies reporting only; inspect actual FBX/GLB materials separately.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
            UnityEngine.Object.DestroyImmediate(mesh);
            UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(preview);
        }
    }

    static GameObject AddMesh(Transform parent, string name, Mesh mesh)
    {
        var child = new GameObject(name);
        child.transform.SetParent(parent, false);
        child.AddComponent<MeshFilter>().sharedMesh = mesh;
        child.AddComponent<MeshRenderer>();
        return child;
    }

    static void Require(bool value, string label)
    {
        if (!value) throw new InvalidOperationException("[TireChange] " + label);
    }
}
