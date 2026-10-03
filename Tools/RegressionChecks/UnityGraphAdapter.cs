// Test-only scene boundary: graph edits use production services and commands.
// Scene discovery is empty; this does not validate Unity object lifecycle or UI.
using System;
using System.Collections.Generic;

namespace UnityEngine
{
    public enum FindObjectsInactive { Exclude }
    public enum FindObjectsSortMode { None }
    public class Object
    {
        public static T[] FindObjectsByType<T>(FindObjectsInactive inactive, FindObjectsSortMode sort) => Array.Empty<T>();
    }
    public class MonoBehaviour : Object { }
    public class Transform
    {
        public Vector3 position;
        public Quaternion rotation = Quaternion.identity;
        public Vector3 localScale = Vector3.one;
    }
}

public sealed class CommandService
{
    public static CommandService I;
    public CommandStack Stack = new();
}

public sealed class PlacedObject
{
    public string id, typeId, sourceNodePath, sourceSignature;
    public PlacedObject modelRoot;
    public UnityEngine.Transform transform = new();
    public void EnsureHasId() { }
}

public static class ImportedModelParts
{
    public static List<ModelPartState> Capture(PlacedObject placed) => new();
}
