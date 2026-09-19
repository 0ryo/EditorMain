using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine.Rendering;
#endif

// Editor FBX repair persists external-material remaps, so project reload and builds use the repair.
public static class FbxMaterialRepair
{
    public sealed class Result
    {
        public int restored, already, missing;
        public string details;
        public string Message => restored > 0
            ? $"材質の画像・設定を{restored}件復元しました。" + (missing > 0 ? $" 未復元: {missing}件。画像の所在・同名ファイルを確認してください。" : "")
            : already > 0 && missing == 0 ? "対象の材質の画像・設定は反映済みです。"
            : "テクスチャを復元できませんでした。元画像との対応、画像の所在・同名ファイルを確認してください。";
    }
    public static Result Apply(PlacedObject selected, string sourcePath, FbxTextureReferences.Document document,
        MaterialFileSearch.Result search)
    {
#if UNITY_EDITOR
        if (selected == null) throw new InvalidOperationException("選択したオブジェクトがありません。");
        // A viewport click selects a part. Repair the owning imported model, not only that renderer.
        if (selected.modelRoot != null) selected = selected.modelRoot;
        string modelPath = ModelAssetPath.FromFile(sourcePath, Application.dataPath);
        var importer = string.IsNullOrEmpty(modelPath) ? null : AssetImporter.GetAtPath(modelPath) as ModelImporter;
        if (importer == null) throw new InvalidOperationException("選択モデルの取込済みFBXを取得できません。モデルを取り込み直してください。");
        string modelGuid = AssetDatabase.AssetPathToGUID(modelPath);
        string destination = "Assets/RecoveredMaterials/" + modelGuid;
        var bindings = document.bindings;
        var log = new StringBuilder();
        var files = search.matches.Concat(search.nearby).ToList();
        int restored = 0, already = 0, missing = 0;
        var textureCache = new Dictionary<string, Texture2D>(StringComparer.OrdinalIgnoreCase);
        var combinedCache = new Dictionary<string, Texture2D>(StringComparer.Ordinal);
        Texture2D Load(string path, bool normal, bool srgb)
        {
            string key = Path.GetFullPath(path) + "|" + normal + "|" + srgb;
            if (!textureCache.TryGetValue(key, out var value)) textureCache[key] = value = ImportTexture(path, destination, normal, srgb);
            return value;
        }
        Texture2D Pack(Texture2D first, Texture2D second, bool metallic, float fallback, string name, float fallbackSmoothness = 1)
        {
            string key = (first != null ? first.GetInstanceID() : 0) + "|" + (second != null ? second.GetInstanceID() : 0) + "|" + metallic + "|" + fallback + "|" + fallbackSmoothness;
            if (!combinedCache.TryGetValue(key, out var value))
                combinedCache[key] = value = SaveGenerated(Combine(first, second, metallic, fallback, fallbackSmoothness), destination, name, !metallic);
            return value;
        }
        var replacements = new Dictionary<string, Material>(StringComparer.Ordinal);
        var sourceNames = new Dictionary<Material, string>();
        foreach (var remap in importer.GetExternalObjectMap())
            if (remap.Key.type == typeof(Material) && remap.Value is Material material) sourceNames[material] = remap.Key.name;
        string SourceName(Material material) => sourceNames.TryGetValue(material, out string sourceName) ? sourceName : FbxTextureReferences.CleanName(material.name);
        var affected = UnityEngine.Object.FindObjectsByType<PlacedObject>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Where(p => p == selected || (p.modelRoot == null && !string.IsNullOrEmpty(selected.typeId) && p.typeId == selected.typeId)).ToList();
        var originals = selected.GetComponentsInChildren<Renderer>(true).SelectMany(r => r.sharedMaterials)
            .Where(m => m != null).Distinct().ToList();
        foreach (var original in originals)
        {
            string name = SourceName(original);
            var references = bindings.Where(b => b.material == name).ToList();
            document.materials.TryGetValue(name, out var values);
            if (references.Count == 0 && (!document.isBlender || values == null))
            {
                missing++;
                log.AppendLine("[参照なし] " + name + ": FBX内に元画像との接続情報がありません。");
                continue;
            }
            foreach (var reference in references)
                if (reference.channel != "DiffuseColor" && reference.channel != "NormalMap" && reference.channel != "EmissiveColor" &&
                    !(reference.channel == "TransparencyFactor" && reference.textureName == "alpha_texture") &&
                    !(reference.channel == "ReflectionFactor" && reference.textureName == "metallic_texture") &&
                    !(reference.channel == "ShininessExponent" && reference.textureName == "roughness_texture"))
                { missing++; log.AppendLine("[未対応の接続] " + name + " / " + reference.channel); }
            Material repaired = null;
            int count = 0;
            Material Target()
            {
                if (repaired != null) return repaired;
                repaired = new Material(original) { name = original.name };
                if (repaired.shader == null || repaired.shader.name != "Universal Render Pipeline/Lit")
                {
                    var shader = Shader.Find("Universal Render Pipeline/Lit");
                    if (shader == null) throw new InvalidOperationException("URP Litシェーダーがありません。");
                    Color color = original.HasProperty("_BaseColor") ? original.GetColor("_BaseColor") : original.HasProperty("_Color") ? original.GetColor("_Color") : Color.white;
                    var baseTexture = original.HasProperty("_BaseMap") ? original.GetTexture("_BaseMap") : original.HasProperty("_MainTex") ? original.GetTexture("_MainTex") : null;
                    repaired.shader = shader;
                    repaired.SetColor("_BaseColor", color);
                    if (baseTexture != null) repaired.SetTexture("_BaseMap", baseTexture);
                }
                return repaired;
            }
            string Locate(FbxTextureReferences.Binding reference)
            {
                if (reference == null) return null;
                // Retained FBX absolute path takes precedence over basename search.
                string direct = reference.file.Replace('\\', Path.DirectorySeparatorChar);
                if (Path.IsPathRooted(direct) && File.Exists(direct)) return direct;
                direct = direct.Normalize(NormalizationForm.FormC);
                if (Path.IsPathRooted(direct) && File.Exists(direct)) return direct;
                if (!Path.IsPathRooted(direct))
                {
                    string relative = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(sourcePath), direct));
                    if (File.Exists(relative)) return relative;
                }
                if (search.limited) { missing++; log.AppendLine("[未復元] 検索上限のためフォルダーを絞ってください: " + FbxTextureReferences.FileName(reference.file)); return null; }
                string found = FbxTextureReferences.Resolve(reference.file, files, out bool ambiguous);
                if (found == null) { missing++; log.AppendLine((ambiguous ? "[同名複数・未復元] " : "[見つからず] ") + FbxTextureReferences.FileName(reference.file)); }
                return found;
            }
            try
            {
                // Blender serializes constant Principled inputs as Phong properties too.
                // Only interpret this convention when the FBX declares Blender as its creator.
                if (document.isBlender && values != null)
                {
                    var target = Target();
                    void Scalar(string property, float value)
                    {
                        if (!target.HasProperty(property) || Mathf.Approximately(target.GetFloat(property), value)) return;
                        target.SetFloat(property, value); count++;
                        log.AppendLine("[材質設定] " + name + " / " + property + " = " + value);
                    }
                    Scalar("_WorkflowMode", 1); target.DisableKeyword("_SPECULAR_SETUP");
                    if (values.Metallic.HasValue) Scalar("_Metallic", values.Metallic.Value);
                    bool packedMap = target.GetTexture("_MetallicGlossMap") != null;
                    if (values.Smoothness.HasValue) Scalar("_Smoothness", packedMap ? 1 : values.Smoothness.Value);
                    Scalar("_SmoothnessTextureChannel", 0); target.DisableKeyword("_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A");
                    target.DisableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
                    if (values.properties.TryGetValue("DiffuseColor", out var rgb) && rgb.Length == 3)
                    {
                        // A connected Blender image replaces the socket value; it is not a color multiplier.
                        Color color = references.Any(r => r.channel == "DiffuseColor") ? Color.white : new Color((float)rgb[0], (float)rgb[1], (float)rgb[2], 1).gamma;
                        color.a = target.GetColor("_BaseColor").a;
                        if (target.GetColor("_BaseColor") != color)
                        {
                            target.SetColor("_BaseColor", color); target.SetColor("_Color", color); count++;
                            log.AppendLine("[材質設定] " + name + " / ベースカラー");
                        }
                    }
                    if (target.shader != original.shader) count++;
                }
                foreach (var group in references.Where(b => b.channel == "DiffuseColor" || b.channel == "NormalMap" || b.channel == "EmissiveColor").GroupBy(b => b.channel))
                {
                    if (group.Count() != 1) { missing++; log.AppendLine("[未復元] 複数テクスチャの合成: " + name + " / " + group.Key); continue; }
                    var reference = group.First();
                    string property = reference.channel == "DiffuseColor" ? "_BaseMap" : reference.channel == "NormalMap" ? "_BumpMap" : "_EmissionMap";
                    string originalProperty = property == "_BaseMap" && !original.HasProperty(property) ? "_MainTex" : property;
                    if (original.HasProperty(originalProperty) && original.GetTexture(originalProperty) != null) { already++; continue; }
                    string found = Locate(reference);
                    if (found == null) continue;
                    var texture = Load(found, reference.channel == "NormalMap", reference.channel == "DiffuseColor" || reference.channel == "EmissiveColor");
                    var target = Target();
                    if (!target.HasProperty(property)) { missing++; log.AppendLine("[未対応shader] " + name + " / " + property); continue; }
                    target.SetTexture(property, texture);
                    if (property == "_BaseMap" && target.HasProperty("_MainTex")) target.SetTexture("_MainTex", texture);
                    if (property == "_BumpMap") target.EnableKeyword("_NORMALMAP");
                    if (property == "_EmissionMap") { target.EnableKeyword("_EMISSION"); target.globalIlluminationFlags &= ~MaterialGlobalIlluminationFlags.EmissiveIsBlack; }
                    count++; log.AppendLine("[再接続] " + name + " / " + reference.channel + " → " + Path.GetFileName(found));
                    // FBX exported by Blender puts its alpha image on TransparencyFactor.
                    if (property == "_BaseMap")
                    {
                        var alphaReferences = references.Where(b => b.channel == "TransparencyFactor" && b.textureName == "alpha_texture").ToList();
                        if (alphaReferences.Count == 1)
                        {
                            string alphaFile = Locate(alphaReferences[0]);
                            if (alphaFile != null)
                            {
                                var alpha = Load(alphaFile, false, false);
                                var combinedAsset = Pack(texture, alpha, false, 0, name + "-base-alpha");
                                target.SetTexture("_BaseMap", combinedAsset); target.SetTexture("_MainTex", combinedAsset);
                                target.SetFloat("_Surface", 1); target.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                                target.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha); target.SetFloat("_ZWrite", 0);
                                target.SetOverrideTag("RenderType", "Transparent"); target.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                                target.renderQueue = (int)RenderQueue.Transparent;
                                count++; log.AppendLine("[再接続] " + name + " / 透明度 → " + Path.GetFileName(alphaFile));
                            }
                        }
                    }
                }
                // Blender FBX conventions: metallic -> ReflectionFactor; roughness -> ShininessExponent.
                var metalRef = references.FirstOrDefault(b => b.textureName == "metallic_texture" && b.channel == "ReflectionFactor");
                var roughRef = references.FirstOrDefault(b => b.textureName == "roughness_texture" && b.channel == "ShininessExponent");
                if (metalRef != null || roughRef != null)
                {
                    string metalFile = Locate(metalRef), roughFile = Locate(roughRef);
                    if ((metalRef == null || metalFile != null) && (roughRef == null || roughFile != null))
                    {
                        Texture2D metal = metalFile == null ? null : Load(metalFile, false, false);
                        Texture2D rough = roughFile == null ? null : Load(roughFile, false, false);
                        var target = Target();
                        if (target.HasProperty("_MetallicGlossMap"))
                        {
                            float metallic = document.isBlender && values?.Metallic != null ? values.Metallic.Value : original.HasProperty("_Metallic") ? original.GetFloat("_Metallic") : 0;
                            float smoothness = document.isBlender && values?.Smoothness != null ? values.Smoothness.Value : original.HasProperty("_Smoothness") ? original.GetFloat("_Smoothness") : 0.5f;
                            target.SetTexture("_MetallicGlossMap", Pack(metal, rough, true, metallic, name + "-metal-smooth", smoothness));
                            target.SetFloat("_Smoothness", 1); target.EnableKeyword("_METALLICSPECGLOSSMAP");
                            count += (metal != null ? 1 : 0) + (rough != null ? 1 : 0);
                            log.AppendLine("[再接続] " + name + " / 金属度・粗さ（URP用に変換）");
                        }
                    }
                }
                if (repaired != null && count > 0)
                {
                    EnsureFolder(destination);
                    string existingPath = AssetDatabase.GetAssetPath(original);
                    if (existingPath.StartsWith(destination + "/", StringComparison.Ordinal))
                    {
                        EditorUtility.CopySerialized(repaired, original);
                        UnityEngine.Object.DestroyImmediate(repaired);
                        repaired = original;
                        EditorUtility.SetDirty(repaired);
                    }
                    else
                    {
                        string path = AssetDatabase.GenerateUniqueAssetPath(destination + "/" + CatalogModelImportNaming.SanitizeName(name) + ".mat");
                        AssetDatabase.CreateAsset(repaired, path);
                    }
                    importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), name), repaired);
                    replacements[name] = repaired;
                    restored += count;
                }
                else if (repaired != null) { already++; UnityEngine.Object.DestroyImmediate(repaired); }
            }
            catch (Exception ex)
            {
                if (repaired != null && !AssetDatabase.Contains(repaired)) UnityEngine.Object.DestroyImmediate(repaired);
                missing++; log.AppendLine("[復元失敗] " + name + ": " + ex.Message);
            }
        }
        if (replacements.Count > 0)
        {
            AssetDatabase.SaveAssets();
            // Same source model shares its restored material assets across existing instances.
            foreach (var placed in affected)
            {
                foreach (var renderer in placed.GetComponentsInChildren<Renderer>(true))
                {
                    var slots = renderer.sharedMaterials;
                    for (int i = 0; i < slots.Length; i++)
                        if (slots[i] != null && replacements.TryGetValue(SourceName(slots[i]), out var replacement)) slots[i] = replacement;
                    renderer.sharedMaterials = slots;
                }
            }
            var meshBindings = new List<(MeshFilter filter, long id)>();
            foreach (var placed in affected)
            {
                foreach (var filter in placed.GetComponentsInChildren<MeshFilter>(true))
                    if (filter.sharedMesh != null && AssetDatabase.TryGetGUIDAndLocalFileIdentifier(filter.sharedMesh, out string guid, out long id) && guid == modelGuid)
                        meshBindings.Add((filter, id));
            }
            // Runtime MeshCollider cooking needs readable source data after reimport.
            if (selected.GetComponentInChildren<MeshCollider>(true) != null) importer.isReadable = true;
            try { importer.SaveAndReimport(); }
            catch (Exception ex) { log.AppendLine("[保存失敗] 表示には反映しましたが、モデルの再インポートに失敗しました: " + ex.Message); missing++; }
            var importedMeshes = new Dictionary<long, Mesh>();
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(modelPath))
                if (asset is Mesh mesh && AssetDatabase.TryGetGUIDAndLocalFileIdentifier(mesh, out string guid, out long id)) importedMeshes[id] = mesh;
            foreach (var binding in meshBindings)
                if (binding.filter != null && importedMeshes.TryGetValue(binding.id, out var mesh)) binding.filter.sharedMesh = mesh;
            // Reimport can invalidate/cancel the cooked collision mesh even when renderers survive.
            foreach (var placed in affected)
            {
                ImportedModelParts.EnsurePicking(placed, true);
                PlacedObjectPickability.EnsurePickable(placed);
            }
            Physics.SyncTransforms();
            log.AppendLine("同じモデルの配置済みオブジェクトにも復元を反映しました。");
        }
        string summary = $"画像・材質設定の復元: {restored}件 / 接続済み: {already}件 / 未復元: {missing}件\n";
        if (restored == 0) summary += "見た目を変更できたテクスチャはありません。下の理由と検索フォルダーを確認してください。\n";
        return new Result { restored = restored, already = already, missing = missing, details = summary + log };
#else
        throw new NotSupportedException("FBXテクスチャの復元はUnity Editorで実行してください。");
#endif
    }
#if UNITY_EDITOR
    static Texture2D ImportTexture(string source, string directory, bool normal, bool srgb)
    {
        var info = new FileInfo(source);
        if (!info.Exists || info.Length > 64L * 1024 * 1024) throw new IOException("画像は64 MB以下にしてください。");
        byte[] bytes = File.ReadAllBytes(source);
        return ImportBytes(bytes, Path.GetExtension(source).ToLowerInvariant(), directory, normal, srgb);
    }
    static Texture2D ImportBytes(byte[] bytes, string extension, string directory, bool normal, bool srgb)
    {
        using var hash = System.Security.Cryptography.SHA256.Create();
        string id = BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", "");
        EnsureFolder(directory);
        string path = directory + "/" + id + (normal ? "-normal" : srgb ? "-color" : "-linear") + extension;
        if (!File.Exists(path)) File.WriteAllBytes(path, bytes);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) throw new IOException("Unityで読み込めない画像です: " + path);
        var type = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
        bool changed = importer.textureType != type || importer.sRGBTexture != srgb || importer.isReadable != !normal ||
            importer.textureCompression != TextureImporterCompression.Uncompressed || importer.maxTextureSize != 4096;
        importer.textureType = type;
        importer.sRGBTexture = srgb; importer.isReadable = !normal;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.maxTextureSize = 4096;
        if (changed) importer.SaveAndReimport();
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (texture == null) throw new IOException("画像を読み込めません: " + path);
        return texture;
    }

    static Texture2D Combine(Texture2D first, Texture2D second, bool metallic, float fallbackMetal, float fallbackSmoothness)
    {
        int width = Math.Max(first != null ? first.width : 1, second != null ? second.width : 1);
        int height = Math.Max(first != null ? first.height : 1, second != null ? second.height : 1);
        var output = new Texture2D(width, height, TextureFormat.RGBA32, false, metallic);
        var pixels = new Color32[width * height];
        var firstPixels = first != null ? first.GetPixels32() : null;
        var secondPixels = second != null ? second.GetPixels32() : null;
        Color Sample(Texture2D texture, Color32[] data, int x, int y, Color fallback)
        {
            if (data == null) return fallback;
            return data[Math.Min(texture.height - 1, y * texture.height / height) * texture.width + Math.Min(texture.width - 1, x * texture.width / width)];
        }
        for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
        {
            Color a = Sample(first, firstPixels, x, y, new Color(fallbackMetal,0,0,1));
            Color b = Sample(second, secondPixels, x, y, new Color(1 - fallbackSmoothness, 0, 0, 1));
            pixels[y * width + x] = metallic ? new Color(a.r,0,0,1-b.r) : new Color(a.r,a.g,a.b,a.a*b.r);
        }
        output.SetPixels32(pixels); output.Apply();
        return output;
    }
    static Texture2D SaveGenerated(Texture2D texture, string directory, string name, bool srgb)
    {
        byte[] bytes;
        try { bytes = texture.EncodeToPNG(); }
        finally { UnityEngine.Object.DestroyImmediate(texture); }
        return ImportBytes(bytes, ".png", directory, false, srgb);
    }
    static void EnsureFolder(string directory)
    {
        if (AssetDatabase.IsValidFolder(directory)) return;
        string parent = Path.GetDirectoryName(directory).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(directory));
    }
#endif
}
