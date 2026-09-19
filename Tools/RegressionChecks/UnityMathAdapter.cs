// Test-only managed substitutes. These do not validate Unity serialization or engine behavior.
using System;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
namespace UnityEditor { public sealed class MenuItem : Attribute { public MenuItem(string path) {} } }
namespace UnityEngine
{
    public class GameObject {}
    public static class Application { public static string persistentDataPath; }
    public static class Debug
    {
        public static void Log(object message) => Console.WriteLine(message);
        public static void LogError(object message) => Console.WriteLine(message);
        public static void LogWarning(object message) => Console.WriteLine(message);
        public static void LogException(Exception error) => Console.WriteLine(error);
    }
    public static class JsonUtility
    {
        static JsonSerializerOptions Options(bool pretty = false)
        {
            var resolver = new DefaultJsonTypeInfoResolver();
            resolver.Modifiers.Add(info => { for (int i = info.Properties.Count - 1; i >= 0; i--)
                if (info.Properties[i].AttributeProvider is System.Reflection.PropertyInfo) info.Properties.RemoveAt(i); });
            return new JsonSerializerOptions { IncludeFields = true, WriteIndented = pretty, TypeInfoResolver = resolver };
        }
        public static string ToJson(object value, bool pretty = false) => JsonSerializer.Serialize(value, Options(pretty));
        public static T FromJson<T>(string value) => JsonSerializer.Deserialize<T>(value, Options());
    }
    public struct Vector3
    {
        public float x,y,z;
        public Vector3(float x,float y,float z) { this.x=x;this.y=y;this.z=z; }
        public static Vector3 zero => new(0,0,0);
        public static Vector3 one => new(1,1,1);
        public static float Distance(Vector3 a,Vector3 b) => MathF.Sqrt((a.x-b.x)*(a.x-b.x)+(a.y-b.y)*(a.y-b.y)+(a.z-b.z)*(a.z-b.z));
    }
    public struct Quaternion
    {
        public float x,y,z,w;
        System.Numerics.Quaternion Value => new(x,y,z,w);
        static Quaternion From(System.Numerics.Quaternion q) => new() { x=q.X,y=q.Y,z=q.Z,w=q.W };
        public static Quaternion identity => new() {w=1};
        public static Quaternion Euler(float x,float y,float z) => From(System.Numerics.Quaternion.CreateFromYawPitchRoll(y*MathF.PI/180,x*MathF.PI/180,z*MathF.PI/180));
        public static Quaternion Inverse(Quaternion q) => From(System.Numerics.Quaternion.Inverse(q.Value));
        public static Quaternion operator *(Quaternion a,Quaternion b) => From(a.Value*b.Value);
        public static bool operator ==(Quaternion a,Quaternion b) => a.Value==b.Value;
        public static bool operator !=(Quaternion a,Quaternion b) => !(a==b);
        public override bool Equals(object o) => o is Quaternion q && this==q;
        public override int GetHashCode() => Value.GetHashCode();
        public static float Angle(Quaternion a,Quaternion b) => 2*MathF.Acos(Math.Clamp(MathF.Abs(System.Numerics.Quaternion.Dot(a.Value,b.Value)),0,1))*180/MathF.PI;
    }
    public static class Mathf
    {
        public const float Rad2Deg = 180/MathF.PI;
        public static int Clamp(int x,int a,int b) => Math.Clamp(x,a,b);
        public static float Max(float a,float b) => Math.Max(a,b);
        public static int RoundToInt(float x) => (int)MathF.Round(x);
        public static float Atan2(float y,float x) => MathF.Atan2(y,x);
        public static float DeltaAngle(float a,float b) { float d=(b-a)%360; if(d<0)d+=360; return d>180?d-360:d; }
    }
}

// Import service uses this predicate; the glTF engine itself is outside this harness.
public static class RuntimeModelLoader
{
    public static bool IsSupportedExtension(string path) => System.IO.Path.GetExtension(path).ToLowerInvariant() is ".glb" or ".gltf";
}
