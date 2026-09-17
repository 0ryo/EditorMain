using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class ModelHierarchyReport
{
    public int version = 1;
    public string source;
    public int nodeCount;
    public int rendererCount;
    public int meshCount;
    public int staticCandidateCount;
    public int skinnedRendererCount;
    public List<string> warnings = new();
    public List<ModelHierarchyNode> nodes = new();
}

[Serializable]
public sealed class ModelHierarchyNode
{
    // Diagnostic addresses only. These must never become persistent part IDs.
    public string address;
    public string parentAddress;
    public string name;
    public Vector3 localPosition;
    public Quaternion localRotation;
    public Vector3 localScale;
    public bool activeSelf;
    public bool activeInHierarchy;
    public bool bone;
    public bool lodMember;
    public string classification;
    public List<string> renderers = new();
    public List<string> meshes = new();
}

// Read-only inspection shared by imported Prefabs and instantiated glTF scenes.
// Candidates describe geometry, not semantic parts such as tires or nuts.
public static class ModelHierarchyDiagnostics
{
    public static ModelHierarchyReport Inspect(GameObject root, string source = null)
    {
        if (!root) throw new ArgumentNullException(nameof(root));
        var report = new ModelHierarchyReport { source = source ?? root.name };
        var meshes = new HashSet<Mesh>();
        var bones = new HashSet<Transform>();
        var lodRenderers = new HashSet<Renderer>();
        foreach (var skin in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if (skin.rootBone) bones.Add(skin.rootBone);
            foreach (var bone in skin.bones) if (bone) bones.Add(bone);
        }
        foreach (var group in root.GetComponentsInChildren<LODGroup>(true))
            foreach (var lod in group.GetLODs())
                foreach (var renderer in lod.renderers) if (renderer) lodRenderers.Add(renderer);

        void Visit(Transform transform, string address, string parent)
        {
            var node = new ModelHierarchyNode
            {
                address = address, parentAddress = parent, name = transform.name,
                localPosition = transform.localPosition, localRotation = transform.localRotation,
                localScale = transform.localScale, activeSelf = transform.gameObject.activeSelf,
                activeInHierarchy = transform.gameObject.activeInHierarchy, bone = bones.Contains(transform)
            };
            bool staticMesh = false;
            bool skinned = false;
            foreach (var renderer in transform.GetComponents<Renderer>())
            {
                node.renderers.Add(renderer.GetType().Name);
                report.rendererCount++;
                node.lodMember |= lodRenderers.Contains(renderer);
                if (renderer is SkinnedMeshRenderer skin)
                {
                    skinned = true;
                    report.skinnedRendererCount++;
                    AddMesh(skin.sharedMesh, node, meshes);
                }
                else if (renderer is MeshRenderer)
                {
                    var filter = transform.GetComponent<MeshFilter>();
                    if (filter && filter.sharedMesh)
                    {
                        staticMesh = true;
                        AddMesh(filter.sharedMesh, node, meshes);
                    }
                }
            }
            node.classification = skinned ? "Skinned: separate review" : node.bone ? "Bone: excluded" :
                node.lodMember ? "LOD: group review" : staticMesh ? "Static geometry candidate: confirm grouping" :
                node.renderers.Count > 0 ? "Other renderer: review" : "Hierarchy container: not automatically a part";
            if (staticMesh && !skinned && !node.bone && !node.lodMember) report.staticCandidateCount++;
            report.nodes.Add(node);
            for (int i = 0; i < transform.childCount; i++) Visit(transform.GetChild(i), address + "/" + i, address);
        }

        Visit(root.transform, "0", null);
        report.nodeCount = report.nodes.Count;
        report.meshCount = meshes.Count;
        report.warnings.Add("Candidate counts do not identify tires, nuts, mount points or original part boundaries. Confirm names, grouping, units, axes and pivots visually.");
        report.warnings.Add("Node addresses are inspection-only; model replacement/reordering can change them. No stable part mapping is created by this report.");
        if (report.rendererCount <= 1)
            report.warnings.Add("At most one Renderer: original parts may be merged. Re-export separate nodes or use separate part assets; automatic restoration is not guaranteed.");
        if (report.skinnedRendererCount > 0)
            report.warnings.Add("Skinned geometry is excluded from static part extraction. Review a rigid-part alternative.");
        if (lodRenderers.Count > 0)
            report.warnings.Add("LOD renderers are excluded from individual candidates to avoid registering each level as a separate part.");
        return report;
    }

    static void AddMesh(Mesh mesh, ModelHierarchyNode node, HashSet<Mesh> meshes)
    {
        if (!mesh) return;
        meshes.Add(mesh);
        node.meshes.Add($"{mesh.name} (vertices={mesh.vertexCount}, submeshes={mesh.subMeshCount})");
    }
}
