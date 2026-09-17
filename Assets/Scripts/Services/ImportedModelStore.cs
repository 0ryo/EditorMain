using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

[Serializable]
public sealed class ImportedModelRecord
{
    public int version = 1;
    public string typeId;
    public string displayName;
    public string description;
    public string modelPath;
    public bool editorAsset;
    public bool hidden;
    [NonSerialized] public string recordPath;
    [NonSerialized] public GameObject prefab;
}

// Each import has its own directory and manifest. A failed copy never publishes a record.
public static class ImportedModelStore
{
    [Serializable] sealed class GltfDocument { public Resource[] buffers; public Resource[] images; }
    [Serializable] sealed class Resource { public string uri; }
    public static string Root => Path.Combine(Application.persistentDataPath, "ImportedModels");

    public static IEnumerable<ImportedModelRecord> ReadAll(Action<string> warning)
    {
        if (!Directory.Exists(Root)) return Array.Empty<ImportedModelRecord>();
        var result = new List<ImportedModelRecord>();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (string path in Directory.GetDirectories(Root).Select(d => Path.Combine(d, "model.json")).Where(File.Exists).OrderBy(p => p))
        {
            try
            {
                var record = JsonUtility.FromJson<ImportedModelRecord>(File.ReadAllText(path));
                if (record == null || record.version != 1 || string.IsNullOrWhiteSpace(record.typeId))
                    throw new IOException("モデル情報の形式が不正です。");
                record.recordPath = path;
                if (!record.editorAsset) ResolveModelPath(record);
                if (!ids.Add(record.typeId)) throw new IOException("モデルIDが重複しています。");
                result.Add(record);
            }
            catch (Exception ex) { warning?.Invoke(Path.GetFileName(Path.GetDirectoryName(path)) + ": " + ex.Message); }
        }
        return result;
    }

    public static string ResolveModelPath(ImportedModelRecord record)
    {
        return record.editorAsset ? record.modelPath : Within(Path.GetDirectoryName(record.recordPath), record.modelPath);
    }

    public static ImportedModelRecord Save(string sourcePath, string typeId, string name, string description)
    {
        string directory = Path.Combine(Root, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var record = new ImportedModelRecord
        {
            typeId = typeId, displayName = name, description = description,
            recordPath = Path.Combine(directory, "model.json")
        };
        if (string.Equals(Path.GetExtension(sourcePath), ".fbx", StringComparison.OrdinalIgnoreCase))
        {
#if UNITY_EDITOR
            record.editorAsset = true;
            record.modelPath = sourcePath.Replace('\\', '/');
            if (!record.modelPath.StartsWith("Assets/", StringComparison.Ordinal) || !File.Exists(sourcePath))
                throw new IOException("FBXの保存先アセットが見つかりません。");
#else
            throw new NotSupportedException("配布アプリではGLBまたはglTFを使用してください。");
#endif
        }
        else
        {
            if (!RuntimeModelLoader.IsSupportedExtension(sourcePath)) throw new NotSupportedException("未対応のモデル形式です。");
            string sourceDirectory = Path.GetDirectoryName(Path.GetFullPath(sourcePath));
            string payloadDirectory = Path.Combine(directory, "payload");
            Directory.CreateDirectory(payloadDirectory);
            record.modelPath = "payload/" + Path.GetFileName(sourcePath);
            // glTF may reference separate buffers and textures. Copy only referenced local files.
            var document = ReadDocument(sourcePath);
            if (document != null)
            {
                foreach (var resource in (document.buffers ?? Array.Empty<Resource>()).Concat(document.images ?? Array.Empty<Resource>()))
                {
                    if (resource == null || string.IsNullOrEmpty(resource.uri) || resource.uri.StartsWith("data:", StringComparison.OrdinalIgnoreCase)) continue;
                    string relative = Uri.UnescapeDataString(resource.uri);
                    if (Uri.TryCreate(relative, UriKind.Absolute, out _) || relative.Contains("?") || relative.Contains("#"))
                        throw new IOException("外部URLの素材は保存できません。素材を同じフォルダー内に置くかGLBを使用してください。");
                    string source = Within(sourceDirectory, relative);
                    string target = Within(payloadDirectory, relative);
                    Directory.CreateDirectory(Path.GetDirectoryName(target));
                    File.Copy(source, target, true);
                }
            }
            File.Copy(sourcePath, Within(directory, record.modelPath), true);
        }
        Write(record);
        return record;
    }

    static GltfDocument ReadDocument(string path)
    {
        if (string.Equals(Path.GetExtension(path), ".gltf", StringComparison.OrdinalIgnoreCase))
            return JsonUtility.FromJson<GltfDocument>(File.ReadAllText(path)) ?? throw new IOException("glTFを読み取れません。");
        // GLB can also contain external texture/buffer URIs in its JSON chunk.
        using var stream = File.OpenRead(path);
        using var reader = new BinaryReader(stream);
        if (stream.Length < 20 || reader.ReadUInt32() != 0x46546C67 || reader.ReadUInt32() != 2 || reader.ReadUInt32() != stream.Length)
            throw new IOException("GLBのヘッダーが不正です。");
        uint length = reader.ReadUInt32();
        if (reader.ReadUInt32() != 0x4E4F534A || length > stream.Length - stream.Position || length > int.MaxValue)
            throw new IOException("GLBのJSONが不正です。");
        string json = System.Text.Encoding.UTF8.GetString(reader.ReadBytes((int)length)).TrimEnd('\0', ' ');
        return JsonUtility.FromJson<GltfDocument>(json) ?? throw new IOException("GLBを読み取れません。");
    }

    public static void Write(ImportedModelRecord record)
    {
        string temp = record.recordPath + ".tmp";
        File.WriteAllText(temp, JsonUtility.ToJson(record, true));
        if (File.Exists(record.recordPath)) File.Replace(temp, record.recordPath, record.recordPath + ".bak");
        else File.Move(temp, record.recordPath);
    }

    static string Within(string directory, string relative)
    {
        if (string.IsNullOrWhiteSpace(relative) || Path.IsPathRooted(relative)) throw new IOException("モデルの相対パスが不正です。");
        string root = Path.GetFullPath(directory) + Path.DirectorySeparatorChar;
        string path = Path.GetFullPath(Path.Combine(directory, relative));
        if (!path.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            throw new IOException("モデルの外側を参照する素材は保存できません。素材をモデル配下に置くかGLBを使用してください。");
        return path;
    }
}
