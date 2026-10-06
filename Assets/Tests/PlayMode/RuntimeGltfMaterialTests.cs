using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class RuntimeGltfMaterialTests
{
    string directory;
    GameObject model;

    static Type Loader => AppDomain.CurrentDomain.GetAssemblies()
        .Select(a => a.GetType("RuntimeModelLoader")).First(t => t != null);

    [TearDown]
    public void TearDown()
    {
        if (model != null) UnityEngine.Object.DestroyImmediate(model);
        if (directory != null && Directory.Exists(directory)) Directory.Delete(directory, true);
    }

    [UnityTest]
    public IEnumerator EmbeddedMaterialsLoadWithTexturesAndSupportedShaders()
    {
        directory = Path.Combine(Path.GetTempPath(), "GlbRendering-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "materials.glb");
        WriteFixture(path);
        var task = (Task<GameObject>)Loader.GetMethod("LoadModelAsync").Invoke(null,
            new object[] { path, CancellationToken.None, null });
        while (!task.IsCompleted) yield return null;
        if (task.IsFaulted) throw task.Exception;
        model = task.Result;
        Assert.That(model, Is.Not.Null);
        var materials = model.GetComponentsInChildren<Renderer>(true).SelectMany(r => r.sharedMaterials).Distinct().ToArray();
        Assert.That(materials.Length, Is.EqualTo(4));
        var expected = new[] { "glTF-pbrMetallicRoughness", "glTF-unlit", "glTF-pbrSpecularGlossiness", "glTF-pbrMetallicRoughness-Clearcoat" };
        foreach (var material in materials)
        {
            Assert.That(material, Is.Not.Null);
            Assert.That(material.shader.isSupported, Is.True, material.name);
            Assert.That(expected, Does.Contain(material.shader.name.Replace("Shader Graphs/", "")));
            Assert.That(material.GetTexturePropertyNames().Any(key => material.GetTexture(key) != null),
                Is.True, "Embedded image must be assigned: " + material.name);
        }
        Assert.That(materials.Select(m => m.shader.name).Distinct().Count(), Is.EqualTo(4));
        Assert.That(materials.Single(m => m.name == "PBR").GetColor("baseColorFactor").r,
            Is.EqualTo(Mathf.LinearToGammaSpace(0.25f)).Within(0.01f));
    }

    [Test]
    public void MissingMaterialIsRejectedBeforeRegistration()
    {
        model = GameObject.CreatePrimitive(PrimitiveType.Cube);
        model.GetComponent<Renderer>().sharedMaterials = new Material[] { null };
        var method = Loader.GetMethod("ValidateRuntimeModel", BindingFlags.NonPublic | BindingFlags.Static);
        var exception = Assert.Throws<TargetInvocationException>(() => method.Invoke(null, new object[] { model }));
        Assert.That(exception.InnerException, Is.TypeOf<IOException>());
        Assert.That(exception.InnerException.Message, Does.Contain("マテリアル"));
    }

    static void WriteFixture(string path)
    {
        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        texture.SetPixels(new[] { Color.red, Color.green, Color.blue, Color.white });
        texture.Apply();
        byte[] png = texture.EncodeToPNG();
        UnityEngine.Object.DestroyImmediate(texture);
        byte[] binary;
        using (var stream = new MemoryStream())
        {
            using (var writer = new BinaryWriter(stream, Encoding.UTF8, true))
            {
                foreach (float value in new[] { 0f, 0f, 0f, 1f, 0f, 0f, 0f, 1f, 0f }) writer.Write(value);
                foreach (float value in new[] { 0f, 0f, 1f, 0f, 0f, 1f }) writer.Write(value);
                writer.Write(png);
                while (stream.Length % 4 != 0) writer.Write((byte)0);
            }
            binary = stream.ToArray();
        }
        string pbr = "\"pbrMetallicRoughness\":{\"baseColorFactor\":[0.25,0.5,0.75,1],\"baseColorTexture\":{\"index\":0},\"metallicFactor\":0}";
        string json = "{\"asset\":{\"version\":\"2.0\"},\"extensionsUsed\":[\"KHR_materials_unlit\",\"KHR_materials_pbrSpecularGlossiness\",\"KHR_materials_clearcoat\"]," +
            "\"buffers\":[{\"byteLength\":" + binary.Length + "}],\"bufferViews\":[{\"buffer\":0,\"byteLength\":36},{\"buffer\":0,\"byteOffset\":36,\"byteLength\":24},{\"buffer\":0,\"byteOffset\":60,\"byteLength\":" + png.Length + "}]," +
            "\"accessors\":[{\"bufferView\":0,\"componentType\":5126,\"count\":3,\"type\":\"VEC3\",\"min\":[0,0,0],\"max\":[1,1,0]},{\"bufferView\":1,\"componentType\":5126,\"count\":3,\"type\":\"VEC2\"}]," +
            "\"images\":[{\"bufferView\":2,\"mimeType\":\"image/png\"}],\"textures\":[{\"source\":0}]," +
            "\"materials\":[{\"name\":\"PBR\"," + pbr + "},{\"name\":\"Unlit\"," + pbr + ",\"extensions\":{\"KHR_materials_unlit\":{}}}," +
            "{\"name\":\"Specular\",\"extensions\":{\"KHR_materials_pbrSpecularGlossiness\":{\"diffuseTexture\":{\"index\":0}}}}," +
            "{\"name\":\"Clearcoat\"," + pbr + ",\"doubleSided\":true,\"alphaMode\":\"BLEND\",\"extensions\":{\"KHR_materials_clearcoat\":{\"clearcoatFactor\":1}}}]," +
            "\"meshes\":[{\"primitives\":[" + string.Join(",", Enumerable.Range(0, 4).Select(i => "{\"attributes\":{\"POSITION\":0,\"TEXCOORD_0\":1},\"material\":" + i + "}")) +
            "]}],\"nodes\":[{\"mesh\":0}],\"scenes\":[{\"nodes\":[0]}],\"scene\":0}";
        byte[] jsonBytes = Encoding.UTF8.GetBytes(json);
        int jsonLength = (jsonBytes.Length + 3) & ~3;
        using var output = new BinaryWriter(File.Create(path));
        output.Write(0x46546C67u);
        output.Write(2u);
        output.Write((uint)(12 + 8 + jsonLength + 8 + binary.Length));
        output.Write((uint)jsonLength);
        output.Write(0x4E4F534Au);
        output.Write(jsonBytes);
        for (int i = jsonBytes.Length; i < jsonLength; i++) output.Write((byte)' ');
        output.Write((uint)binary.Length);
        output.Write(0x004E4942u);
        output.Write(binary);
    }
}
