using System;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[Serializable]
public class PrefabEntry {
    public string typeId;      // 例: "Vehicle/Car_Proxy"
    public GameObject prefab;  // Prefab参照
}

[CreateAssetMenu(menuName = "CourseEditor/PrefabRegistry")]
public class PrefabRegistry : ScriptableObject {
    public const string DefaultAssetPath = "Assets/Data/DefaultRegistry.asset";

    public List<PrefabEntry> entries = new();

    public bool HasEntries => entries != null && entries.Count > 0;

    public static PrefabRegistry LoadDefault()
    {
#if UNITY_EDITOR
        return AssetDatabase.LoadAssetAtPath<PrefabRegistry>(DefaultAssetPath);
#else
        return null;
#endif
    }
}
