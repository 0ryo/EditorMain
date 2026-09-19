using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

// Reads reference metadata only. Geometry/animation arrays are skipped, never decompressed.
public static class FbxTextureReferences
{
    public sealed class Binding
    {
        public string material, channel, file, textureName;
    }
    public sealed class MaterialValues
    {
        public string name;
        public readonly Dictionary<string, double[]> properties = new();
        public double? Scalar(string property) => properties.TryGetValue(property, out var value) && value.Length == 1 ? value[0] : (double?)null;
        public float? Metallic => Scalar("ReflectionFactor") is double m ? (float)Math.Max(0, Math.Min(1, m)) : (float?)null;
        public float? Smoothness => (Scalar("Shininess") ?? Scalar("ShininessExponent")) is double s ? (float)Math.Sqrt(Math.Max(0, Math.Min(100, s))) / 10f : (float?)null;
    }
    public sealed class Document
    {
        public bool isBlender;
        public readonly List<Binding> bindings = new();
        public readonly Dictionary<string, MaterialValues> materials = new(StringComparer.Ordinal);
    }
    sealed class Entry
    {
        public long id;
        public string kind, name, file;
        public MaterialValues values;
    }
    sealed class Connection
    {
        public long child, parent;
        public string channel;
    }
    public static List<Binding> Read(string path, CancellationToken cancellation = default)
        => ReadDocument(path, cancellation).bindings;

    public static Document ReadDocument(string path, CancellationToken cancellation = default)
    {
        var document = new Document();
        var defaults = new MaterialValues();
        using var stream = File.OpenRead(path);
        using var reader = new BinaryReader(stream, Encoding.UTF8);
        if (stream.Length < 27 || Encoding.ASCII.GetString(reader.ReadBytes(23)) != "Kaydara FBX Binary  \0\x1a\0")
            throw new NotSupportedException("テクスチャ再接続はバイナリFBXに対応しています。ASCII FBXはバイナリ形式で書き出してください。");
        bool wide = reader.ReadUInt32() >= 7500;
        int headerSize = wide ? 25 : 13;
        var entries = new Dictionary<long, Entry>();
        var connections = new List<Connection>();
        int visited = 0;
        bool Node(long parentEnd, string parent, Entry owner, int depth)
        {
            cancellation.ThrowIfCancellationRequested();
            if (++visited > 100000 || depth > 8) throw new InvalidDataException("FBXメタデータの上限を超えました。");
            long start = stream.Position;
            if (parentEnd - start < headerSize) throw new InvalidDataException("FBXノードが途中で終了しています。");
            ulong rawEnd = wide ? reader.ReadUInt64() : reader.ReadUInt32();
            ulong count = wide ? reader.ReadUInt64() : reader.ReadUInt32();
            ulong bytes = wide ? reader.ReadUInt64() : reader.ReadUInt32();
            byte nameLength = reader.ReadByte();
            if (rawEnd == 0) return false;
            if (rawEnd > (ulong)parentEnd || rawEnd <= (ulong)stream.Position || count > 1024 || bytes > (ulong)(parentEnd - stream.Position))
                throw new InvalidDataException("FBXノードの範囲が不正です。");
            long end = (long)rawEnd;
            string name = Encoding.UTF8.GetString(reader.ReadBytes(nameLength));
            bool template = name == "PropertyTemplate" && parent == "ObjectType";
            bool container = name == "Objects" || name == "Connections" || name == "FBXHeaderExtension" || name == "Definitions" || name == "ObjectType" || (name == "Properties70" && owner?.values != null);
            bool creator = name == "Creator" && parent == "FBXHeaderExtension";
            bool materialProperty = name == "P" && parent == "Properties70" && owner?.values != null;
            bool objectEntry = parent == "Objects" && (name == "Material" || name == "Texture" || name == "Video");
            bool property = owner != null && (name == "FileName" || name == "Filename" || name == "RelativeFilename");
            bool connection = parent == "Connections" && name == "C";
            if (!container && !objectEntry && !property && !connection && !creator && !template && !materialProperty) { stream.Position = end; return true; }
            long propertiesEnd = checked(stream.Position + (long)bytes);
            if (propertiesEnd > end) throw new InvalidDataException("FBXプロパティ範囲が不正です。");
            var values = new List<object>();
            for (ulong i = 0; i < count; i++)
            {
                char type = (char)reader.ReadByte();
                switch (type)
                {
                    case 'Y': values.Add((long)reader.ReadInt16()); break;
                    case 'C': values.Add(reader.ReadByte()); break;
                    case 'I': values.Add((long)reader.ReadInt32()); break;
                    case 'L': values.Add(reader.ReadInt64()); break;
                    case 'F': values.Add(reader.ReadSingle()); break;
                    case 'D': values.Add(reader.ReadDouble()); break;
                    case 'S':
                    case 'R':
                        uint length = reader.ReadUInt32();
                        if (length > 1048576 || length > propertiesEnd - stream.Position) throw new InvalidDataException("FBX文字列が不正です。");
                        if (type == 'S') values.Add(Encoding.UTF8.GetString(reader.ReadBytes((int)length)));
                        else { stream.Position += length; values.Add(null); }
                        break;
                    default: throw new InvalidDataException("未対応のFBX参照プロパティです。");
                }
                if (stream.Position > propertiesEnd) throw new InvalidDataException("FBXプロパティが途中で終了しています。");
            }
            stream.Position = propertiesEnd;
            if (objectEntry && values.Count >= 2 && values[0] is long id && values[1] is string objectName)
            {
                owner = new Entry { id = id, kind = name, name = CleanName(objectName) };
                if (name == "Material") owner.values = new MaterialValues { name = owner.name };
                entries[id] = owner;
            }
            if (creator && values.Count > 0 && values[0] is string creatorName)
                document.isBlender = creatorName.IndexOf("Blender", StringComparison.OrdinalIgnoreCase) >= 0;
            if (template && values.Count > 0 && values[0] is string templateName && (templateName == "FbxSurfacePhong" || templateName == "FbxSurfaceLambert"))
                owner = new Entry { values = defaults };
            if (materialProperty && values.Count >= 5 && values[0] is string propertyName)
            {
                var numbers = values.Skip(4).Where(v => v is double || v is float || v is long).Select(Convert.ToDouble).ToArray();
                if (numbers.Length == values.Count - 4 && numbers.All(n => !double.IsNaN(n) && !double.IsInfinity(n))) owner.values.properties[propertyName] = numbers;
            }
            if (property && values.Count > 0 && values[0] is string file && !string.IsNullOrWhiteSpace(file))
            {
                if (string.IsNullOrEmpty(owner.file) || (name != "RelativeFilename" && Path.IsPathRooted(file))) owner.file = file;
            }
            if (connection && values.Count >= 3 && values[1] is long child && values[2] is long target)
                connections.Add(new Connection { child = child, parent = target, channel = values.Count > 3 ? values[3] as string : null });
            if (container || objectEntry || template)
                while (stream.Position < end && Node(end, name, owner, depth + 1)) { }
            stream.Position = end;
            return true;
        }
        while (stream.Position + headerSize <= stream.Length && Node(stream.Length, "", null, 0)) { }
        // Some FBX writers put filenames only on the Video connected to a Texture.
        foreach (var link in connections)
            if (entries.TryGetValue(link.child, out var video) && video.kind == "Video" &&
                entries.TryGetValue(link.parent, out var texture) && texture.kind == "Texture" && string.IsNullOrEmpty(texture.file)) texture.file = video.file;
        foreach (var entry in entries.Values.Where(e => e.kind == "Material"))
        {
            foreach (var pair in defaults.properties)
                if (!entry.values.properties.ContainsKey(pair.Key)) entry.values.properties[pair.Key] = pair.Value;
            document.materials[entry.name] = entry.values;
        }
        var result = document.bindings;
        foreach (var link in connections)
            if (entries.TryGetValue(link.child, out var texture) && texture.kind == "Texture" && !string.IsNullOrWhiteSpace(texture.file) &&
                entries.TryGetValue(link.parent, out var material) && material.kind == "Material" && !string.IsNullOrEmpty(link.channel))
                result.Add(new Binding { material = material.name, channel = link.channel, file = texture.file, textureName = texture.name });
        return document;
    }

    public static string CleanName(string value)
    {
        int marker = value.IndexOf('\0');
        if (marker >= 0) value = value.Substring(0, marker);
        int prefix = value.IndexOf("::", StringComparison.Ordinal);
        if (prefix >= 0) value = value.Substring(prefix + 2);
        return value.Replace(" (Instance)", "");
    }

    public static string FileName(string value) => value.Replace('\\', '/').Split('/').Last().Normalize(NormalizationForm.FormC);

    public static string Resolve(string reference, IEnumerable<string> files, out bool ambiguous)
    {
        string name = FileName(reference);
        var candidates = files.Where(path => string.Equals(FileName(path), name, StringComparison.OrdinalIgnoreCase)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        ambiguous = candidates.Count > 1;
        return candidates.Count == 1 ? candidates[0] : null;
    }
}
