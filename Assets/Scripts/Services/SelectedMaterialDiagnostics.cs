using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public static class SelectedMaterialDiagnostics
{
    public sealed class Snapshot
    {
        public string label, sourcePath, searchDirectory, summary;
        public readonly HashSet<string> expectedNames = new(StringComparer.OrdinalIgnoreCase);
    }

    public static Snapshot Capture(PlacedObject selected)
    {
        if (selected == null) throw new InvalidOperationException("オブジェクトを選択してください。");
        var snapshot = new Snapshot { label = selected.GetDisplayName(), searchDirectory = Application.dataPath };
        var text = new StringBuilder();
        int missing = 0, shaderIssues = 0, rendererCount = 0;
        var materials = new HashSet<Material>();
        foreach (var renderer in selected.GetComponentsInChildren<Renderer>(true))
        {
            rendererCount++;
            var slots = renderer.sharedMaterials;
            var filter = renderer.GetComponent<MeshFilter>();
            var skin = renderer as SkinnedMeshRenderer;
            var mesh = skin != null ? skin.sharedMesh : filter != null ? filter.sharedMesh : null;
            int slotCount = Math.Max(slots.Length, mesh != null ? mesh.subMeshCount : 0);
            for (int i = 0; i < slotCount; i++)
            {
                var material = i < slots.Length ? slots[i] : null;
                if (material == null)
                { missing++; text.AppendLine($"[欠損] {renderer.name} / マテリアル枠 {i + 1}"); continue; }
                snapshot.expectedNames.Add(material.name);
                if (!materials.Add(material)) continue;
                if (material.shader == null || material.shader.name == "Hidden/InternalErrorShader" || !material.shader.isSupported)
                { shaderIssues++; text.AppendLine("[シェーダー要確認] " + material.name); }
#if UNITY_EDITOR
                var serialized = new SerializedObject(material);
                var textures = serialized.FindProperty("m_SavedProperties.m_TexEnvs");
                if (textures != null && textures.isArray)
                    for (int t = 0; t < textures.arraySize; t++)
                    {
                        var entry = textures.GetArrayElementAtIndex(t);
                        var reference = entry.FindPropertyRelative("second.m_Texture");
                        if (reference != null && reference.objectReferenceValue == null && reference.objectReferenceInstanceIDValue != 0)
                        { missing++; text.AppendLine("[画像参照切れ] " + material.name + " / " + entry.FindPropertyRelative("first").stringValue); }
                    }
                if (string.IsNullOrEmpty(snapshot.sourcePath) && mesh != null) snapshot.sourcePath = AssetDatabase.GetAssetPath(mesh);
#endif
            }
#if UNITY_EDITOR
            if (string.IsNullOrEmpty(snapshot.sourcePath) && mesh != null) snapshot.sourcePath = AssetDatabase.GetAssetPath(mesh);
#endif
        }
        var record = ImportedModelStore.ReadAll(null).FirstOrDefault(item => item.typeId == selected.typeId);
        if (record != null)
        {
            // Prefer the selected mesh's actual asset over a stale library manifest path.
            if (string.IsNullOrEmpty(snapshot.sourcePath)) snapshot.sourcePath = ImportedModelStore.ResolveModelPath(record);
            if (!string.IsNullOrWhiteSpace(record.originalSourcePath))
            {
                var folder = Path.GetDirectoryName(record.originalSourcePath);
                if (Directory.Exists(folder)) snapshot.searchDirectory = folder;
            }
        }
        if (!string.IsNullOrEmpty(snapshot.sourcePath))
        {
            snapshot.sourcePath = Path.GetFullPath(snapshot.sourcePath);
            if (record == null || string.IsNullOrWhiteSpace(record.originalSourcePath))
                snapshot.searchDirectory = record != null && record.editorAsset ? Application.dataPath : Path.GetDirectoryName(snapshot.sourcePath);
            if (RuntimeModelLoader.IsSupportedExtension(snapshot.sourcePath) && File.Exists(snapshot.sourcePath))
            {
                foreach (string uri in ImportedModelStore.GetImageReferences(snapshot.sourcePath))
                {
                    if (uri.StartsWith("data:", StringComparison.OrdinalIgnoreCase)) continue;
                    var relative = Uri.UnescapeDataString(uri);
                    snapshot.expectedNames.Add(relative);
                    if (!Uri.TryCreate(relative, UriKind.Absolute, out _) && !File.Exists(Path.Combine(Path.GetDirectoryName(snapshot.sourcePath), relative)))
                    { missing++; text.AppendLine("[参照画像なし] " + relative); }
                }
            }
#if UNITY_EDITOR
            string assetPath = ModelAssetPath.FromFile(snapshot.sourcePath, Application.dataPath);
            var importer = string.IsNullOrEmpty(assetPath) ? null : AssetImporter.GetAtPath(assetPath) as ModelImporter;
            if (importer != null)
            {
                var sourceMaterials = new SerializedObject(importer).FindProperty("m_Materials");
                if (sourceMaterials != null && sourceMaterials.isArray)
                    for (int i = 0; i < sourceMaterials.arraySize; i++)
                    {
                        var name = sourceMaterials.GetArrayElementAtIndex(i).FindPropertyRelative("name");
                        if (name != null && !string.IsNullOrEmpty(name.stringValue)) snapshot.expectedNames.Add(name.stringValue);
                    }
                foreach (var mapping in importer.GetExternalObjectMap())
                    if (mapping.Key.type == typeof(Material) && mapping.Value == null)
                    { snapshot.expectedNames.Add(mapping.Key.name); missing++; text.AppendLine("[割当先なし] " + mapping.Key.name); }
            }
#endif
        }
        snapshot.summary = $"対象: {snapshot.label}\n元モデル: {snapshot.sourcePath ?? "不明"}\nRenderer: {rendererCount} / 欠損参照: {missing} / シェーダー要確認: {shaderIssues}\n" + text +
            "白色・単色やテクスチャ未使用だけでは欠損とは判定しません。\n元のファイル名を取得できない欠損は、フォルダー内の候補から確認してください。\n";
        return snapshot;
    }
}
