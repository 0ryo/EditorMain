using System;
using System.IO;
using System.Text;
using System.Threading;

static class FbxReferenceChecks
{
    static int checks;
    static void Check(bool value) { if (!value) throw new Exception("FBX check failed: " + (checks + 1)); checks++; }
    public static void Run()
    {
        Check(ModelAssetPath.FromFile(@"C:\EditorMain\Assets\ImportedFbx\car.fbx", "C:/EditorMain/Assets") == "Assets/ImportedFbx/car.fbx");
        Check(ModelAssetPath.FromFile("C:/EditorMain/Assets/ImportedFbx/car.fbx", "C:/EditorMain/Assets") == "Assets/ImportedFbx/car.fbx");
        Check(ModelAssetPath.FromFile(@"Assets\ImportedFbx\car.fbx", "C:/EditorMain/Assets") == "Assets/ImportedFbx/car.fbx");
        Check(ModelAssetPath.FromFile("C:/EditorMain/AssetsOther/car.fbx", "C:/EditorMain/Assets") == null);
        Check(ModelAssetPath.FromFile("Assets/../../car.fbx", "C:/EditorMain/Assets") == null);
        Check(ModelAssetPath.FromFile(null, "C:/EditorMain/Assets") == null);
        string path = Path.Combine(Path.GetTempPath(), "FbxReferenceCheck-" + Guid.NewGuid().ToString("N") + ".fbx");
        try
        {
            foreach (bool wide in new[] { false, true })
            {
                using (var stream = File.Create(path))
                using (var writer = new BinaryWriter(stream, Encoding.UTF8))
                {
                    writer.Write(Encoding.ASCII.GetBytes("Kaydara FBX Binary  \0\x1a\0")); writer.Write(wide ? 7500 : 7400);
                    void Node(string name, object[] values, Action children = null)
                    {
                        long start = stream.Position;
                        int header = wide ? 25 : 13;
                        writer.Write(new byte[header]); writer.Write(Encoding.UTF8.GetBytes(name));
                        long properties = stream.Position;
                        foreach (object value in values)
                            if (value is long id) { writer.Write((byte)'L'); writer.Write(id); }
                            else if (value is double number) { writer.Write((byte)'D'); writer.Write(number); }
                            else { byte[] bytes = Encoding.UTF8.GetBytes((string)value); writer.Write((byte)'S'); writer.Write(bytes.Length); writer.Write(bytes); }
                        long bytesCount = stream.Position - properties;
                        if (children != null) { children(); writer.Write(new byte[header]); }
                        long end = stream.Position; stream.Position = start;
                        if (wide) { writer.Write(end); writer.Write((long)values.Length); writer.Write(bytesCount); }
                        else { writer.Write((uint)end); writer.Write((uint)values.Length); writer.Write((uint)bytesCount); }
                        writer.Write((byte)name.Length); stream.Position = end;
                    }
                    Node("FBXHeaderExtension", Array.Empty<object>(), () => Node("Creator", new object[] { "Blender 4.5" }));
                    Node("Definitions", Array.Empty<object>(), () => Node("ObjectType", new object[] { "Material" }, () =>
                        Node("PropertyTemplate", new object[] { "FbxSurfacePhong" }, () => Node("Properties70", Array.Empty<object>(), () =>
                            Node("P", new object[] { "ReflectionFactor", "Number", "", "A", 0.0 })))));
                    Node("Objects", Array.Empty<object>(), () =>
                    {
                        Node("Material", new object[] { 1L, "Material::Paint.001", "" }, () => Node("Properties70", Array.Empty<object>(), () =>
                            Node("P", new object[] { "Shininess", "Number", "", "A", 9.0 })));
                        Node("Material", new object[] { 4L, "Material::Gold", "" }, () => Node("Properties70", Array.Empty<object>(), () =>
                        {
                            Node("P", new object[] { "Shininess", "Number", "", "A", 100.0 });
                            Node("P", new object[] { "ReflectionFactor", "Number", "", "A", 1.0 });
                            Node("P", new object[] { "DiffuseColor", "Color", "", "A", 0.8, 0.68, 0.06 });
                        }));
                        Node("Texture", new object[] { 2L, "base_color_texture\0\u0001Texture", "" });
                        Node("Video", new object[] { 3L, "Video::Image", "" }, () => Node("Filename", new object[] { "C:/old/Body.png" }));
                    });
                    Node("Connections", Array.Empty<object>(), () =>
                    {
                        Node("C", new object[] { "OO", 3L, 2L });
                        Node("C", new object[] { "OP", 2L, 1L, "DiffuseColor" });
                    });
                    writer.Write(new byte[headerSize(wide)]);
                }
                var document = FbxTextureReferences.ReadDocument(path);
                var refs = document.bindings;
                Check(document.isBlender && document.materials.Count == 2);
                Check(document.materials["Gold"].Metallic == 1 && document.materials["Gold"].Smoothness == 1);
                Check(document.materials["Gold"].properties["DiffuseColor"][2] == 0.06);
                Check(document.materials["Paint.001"].Metallic == 0 && Math.Abs(document.materials["Paint.001"].Smoothness.Value - 0.3f) < 0.00001f);
                Check(refs.Count == 1 && refs[0].material == "Paint.001" && refs[0].textureName == "base_color_texture" && refs[0].file == "C:/old/Body.png" && refs[0].channel == "DiffuseColor");
                bool cancelled = false;
                try { FbxTextureReferences.Read(path, new CancellationToken(true)); } catch (OperationCanceledException) { cancelled = true; }
                Check(cancelled);
                byte[] invalid = File.ReadAllBytes(path); for (int i = 27; i < 31; i++) invalid[i] = 255; File.WriteAllBytes(path, invalid);
                bool rejected = false;
                try { FbxTextureReferences.Read(path); } catch (InvalidDataException) { rejected = true; }
                Check(rejected);
            }
            Check(FbxTextureReferences.Resolve("C:/old/body.png", new[] { "D:/new/BODY.PNG", "D:/new/body.jpg" }, out bool ambiguous) == "D:/new/BODY.PNG" && !ambiguous);
            Check(FbxTextureReferences.Resolve("body.png", new[] { "D:/a/body.png", "D:/b/body.png" }, out ambiguous) == null && ambiguous);
            Check(FbxTextureReferences.Resolve("body.png", new[] { "D:/a/body.jpg" }, out ambiguous) == null && !ambiguous);
            Check(FbxTextureReferences.FileName("C:\\a\\カ\u3099ラス.png") == "ガラス.png");
            Console.WriteLine($"[Regression] {checks} FBX reference checks passed.");
        }
        finally { File.Delete(path); }
    }
    static int headerSize(bool wide) => wide ? 25 : 13;
}
