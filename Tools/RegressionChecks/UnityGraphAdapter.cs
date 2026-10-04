// Test-only engine boundary. Graph mutations, snapshots, validation and export use production code.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnityEngine
{
    public class MonoBehaviour : Object { }
    public enum FindObjectsInactive { Exclude }
    public enum FindObjectsSortMode { None }
    public class Object
    {
        public static PlacedObject[] placed = Array.Empty<PlacedObject>();
        public static T[] FindObjectsByType<T>(FindObjectsInactive inactive, FindObjectsSortMode sort) => (T[])(object)placed;
    }
    public class Transform
    {
        public Vector3 position, localScale = Vector3.one;
        public Quaternion rotation = Quaternion.identity;
    }
}

public class CommandService
{
    public static CommandService I;
    public CommandStack Stack = new();
}

public class PlacedObject
{
    public string id, typeId = "box", sourceNodePath, sourceSignature;
    public PlacedObject modelRoot;
    public Transform transform = new();
    public void EnsureHasId() { if (string.IsNullOrEmpty(id)) id = Guid.NewGuid().ToString(); }
}

public static class ImportedModelParts
{
    public static List<ModelPartState> Capture(PlacedObject placed) => new();
}
