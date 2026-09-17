using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[Serializable]
public sealed class ModelPartState
{
    public string nodePath;
    public string sourceSignature;
    public string id;
    public string displayName;
    public string description;
    public bool hasDescriptionOverride;
    public Vector3 localPosition;
    public Quaternion localRotation = Quaternion.identity;
    public Vector3 localScale = Vector3.one;
    public bool active = true;
    public bool hidden;
    public bool locked;
}

// One source model is instantiated per placement. Child records bind existing nodes,
// never instantiate another copy of the parent Prefab. Paths are checked locators,
// while the independently persisted object IDs remain the condition references.
public static class ImportedModelParts
{
    public static bool IsImported(PlacedObject root) => root &&
        !string.IsNullOrEmpty(root.typeId) && root.typeId.StartsWith("Imported/", StringComparison.Ordinal);

    public static void Register(PlacedObject root)
    {
        if (!IsImported(root) || root.modelRoot != null) return;
        root.EnsureHasId();
        if (string.IsNullOrEmpty(root.sourceSignature)) root.sourceSignature = Signature(root.transform);
        // Rigid hierarchy only. Do not expose bones or LOD representations as parts.
        if (root.GetComponentInChildren<SkinnedMeshRenderer>(true) || root.GetComponentInChildren<LODGroup>(true)) return;
        var nodes = new HashSet<Transform>();
        foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
        {
            if (!filter.sharedMesh || !filter.GetComponent<MeshRenderer>()) continue;
            for (var node = filter.transform; node != null && node != root.transform; node = node.parent)
                nodes.Add(node);
        }
        foreach (var node in root.GetComponentsInChildren<Transform>(true))
        {
            if (!nodes.Contains(node)) continue;
            var part = node.GetComponent<PlacedObject>();
            if (!part) part = node.gameObject.AddComponent<PlacedObject>();
            if (part.modelRoot != root || string.IsNullOrEmpty(part.id))
                part.id = root.id + "/part-" + Guid.NewGuid().ToString("N");
            part.modelRoot = root;
            part.typeId = root.typeId;
            part.partNodePath = PathFrom(root.transform, node);
            if (string.IsNullOrEmpty(part.sourceSignature)) part.sourceSignature = Signature(node);
            if (string.IsNullOrWhiteSpace(part.displayName)) part.displayName = node.name;
        }
        EnsurePicking(root);
        foreach (var part in root.GetComponentsInChildren<PlacedObject>(true))
            if (!part.GetComponent<PlacedObjectEditState>()) part.gameObject.AddComponent<PlacedObjectEditState>();
    }

    public static void EnsurePicking(PlacedObject root)
    {
        foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
        {
            var owner = filter.GetComponent<PlacedObject>();
            if (!owner || !filter.sharedMesh || !filter.GetComponent<MeshRenderer>()) continue;
            if (filter.GetComponent<Collider>() == null)
            {
                var collider = filter.gameObject.AddComponent<MeshCollider>();
                collider.sharedMesh = filter.sharedMesh;
            }
        }
    }

    public static List<ModelPartState> Capture(PlacedObject root)
    {
        return root.GetComponentsInChildren<PlacedObject>(true)
            .Where(p => p != root && p.modelRoot == root)
            .Select(p =>
            {
                var state = p.GetComponent<PlacedObjectEditState>();
                return new ModelPartState
                {
                    nodePath = p.partNodePath, sourceSignature = p.sourceSignature, id = p.id, displayName = p.displayName,
                    description = p.description, hasDescriptionOverride = p.hasDescriptionOverride,
                    localPosition = p.transform.localPosition, localRotation = p.transform.localRotation,
                    localScale = p.transform.localScale, active = p.gameObject.activeSelf,
                    hidden = state && state.Hidden, locked = state && state.Locked
                };
            }).OrderBy(p => p.nodePath, StringComparer.Ordinal).ToList();
    }

    public static void Restore(PlacedObject root, List<ModelPartState> states)
    {
        if (states == null || states.Count == 0) return; // Legacy projects remain root-only.
        var resolved = new List<(Transform node, ModelPartState state)>();
        var paths = new HashSet<string>(StringComparer.Ordinal);
        foreach (var state in states)
        {
            if (state == null || string.IsNullOrEmpty(state.nodePath) || !paths.Add(state.nodePath))
                throw new InvalidOperationException("部品の対応情報が重複または欠損しています。");
            resolved.Add((Resolve(root.transform, state.nodePath), state));
        }
        foreach (var entry in resolved)
        {
            var node = entry.node;
            var data = entry.state;
            var part = node.GetComponent<PlacedObject>();
            if (!part) part = node.gameObject.AddComponent<PlacedObject>();
            part.modelRoot = root;
            part.partNodePath = data.nodePath;
            part.sourceSignature = data.sourceSignature;
            part.id = data.id;
            part.typeId = root.typeId;
            part.displayName = data.displayName;
            part.description = data.description;
            part.hasDescriptionOverride = data.hasDescriptionOverride;
            node.localPosition = data.localPosition;
            node.localRotation = data.localRotation;
            node.localScale = data.localScale;
            node.gameObject.SetActive(data.active);
        }
        EnsurePicking(root);
        // Apply flags only after every child has its ownership boundary.
        foreach (var entry in resolved)
        {
            var edit = entry.node.GetComponent<PlacedObjectEditState>();
            if (!edit) edit = entry.node.gameObject.AddComponent<PlacedObjectEditState>();
            edit.SetLocked(entry.state.locked);
            edit.SetVisible(!entry.state.hidden);
        }
    }

    public static Transform Resolve(Transform root, string path)
    {
        var node = root;
        if (string.IsNullOrEmpty(path)) return node;
        foreach (string segment in path.Split('/'))
        {
            int colon = segment.IndexOf(':');
            if (colon <= 0 || !int.TryParse(segment.Substring(0, colon), out int index) || index < 0 || index >= node.childCount)
                throw new InvalidOperationException("モデル内の部品が見つかりません: " + path);
            node = node.GetChild(index);
            if (node.name != Uri.UnescapeDataString(segment.Substring(colon + 1)))
                throw new InvalidOperationException("モデルの部品構造が変更されています: " + path);
        }
        return node;
    }

    // Conservative source-change detection, including same-name siblings at different
    // local poses. This is a structure/geometry-summary signature, not a file checksum.
    public static string Signature(Transform root)
    {
        var text = new System.Text.StringBuilder();
        foreach (var node in root.GetComponentsInChildren<Transform>(true))
        {
            text.Append(PathFrom(root, node)).Append('|');
            if (node != root)
                text.Append(JsonUtility.ToJson(node.localPosition)).Append(JsonUtility.ToJson(node.localRotation))
                    .Append(JsonUtility.ToJson(node.localScale));
            // Unity's missing-component wrapper is not a CLR null. Do not use ?.
            // on UnityEngine.Object: FBX roots and grouping nodes commonly have no mesh.
            var filter = node.GetComponent<MeshFilter>();
            var mesh = filter != null ? filter.sharedMesh : null;
            if (mesh) text.Append(mesh.name).Append('|').Append(mesh.vertexCount).Append('|')
                .Append(mesh.subMeshCount).Append(JsonUtility.ToJson(mesh.bounds));
            text.AppendLine();
        }
        using var hash = System.Security.Cryptography.SHA256.Create();
        return BitConverter.ToString(hash.ComputeHash(System.Text.Encoding.UTF8.GetBytes(text.ToString()))).Replace("-", "");
    }

    public static void ValidateSource(Transform source, string signature)
    {
        if (!string.IsNullOrEmpty(signature) && Signature(source) != signature)
            throw new InvalidOperationException("元モデルの部品構造が変更されています。保存時のモデルを戻してください。");
    }

    public static string PathFrom(Transform root, Transform node)
    {
        var segments = new Stack<string>();
        while (node != root)
        {
            if (!node) throw new InvalidOperationException("部品が元モデルの階層外にあります。");
            segments.Push(node.GetSiblingIndex() + ":" + Uri.EscapeDataString(node.name));
            node = node.parent;
        }
        return string.Join("/", segments);
    }

    public static void ReidentifyDuplicate(PlacedObject clone, PlacedObject source)
    {
        string sourcePath = source.modelRoot ? Join(source.modelRoot.sourceNodePath, source.partNodePath) : source.sourceNodePath;
        clone.modelRoot = null;
        clone.partNodePath = null;
        clone.sourceNodePath = sourcePath;
        clone.ForceNewId();
        foreach (var part in clone.GetComponentsInChildren<PlacedObject>(true))
        {
            if (part == clone) continue;
            part.modelRoot = clone;
            part.partNodePath = PathFrom(clone.transform, part.transform);
            part.id = clone.id + "/part-" + Guid.NewGuid().ToString("N");
        }
    }

    static string Join(string a, string b) => string.IsNullOrEmpty(a) ? b : a + "/" + b;

    public static int Depth(PlacedObject part)
    {
        int depth = 0;
        for (var node = part.transform.parent; node != null; node = node.parent)
            if (node.GetComponent<PlacedObject>()) depth++;
        return depth;
    }

    public static string Label(PlacedObject part) => part.modelRoot
        ? part.modelRoot.GetDisplayName() + " / " + part.GetDisplayName() + " [" +
            (part.id.Length > 6 ? part.id.Substring(part.id.Length - 6) : part.id) + "]"
        : part.GetDisplayName();
}
