using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class PrefabEntry {
    public string typeId;      // 例: "Vehicle/Car_Proxy"
    public GameObject prefab;  // Prefab参照
}

[CreateAssetMenu(menuName = "CourseEditor/PrefabRegistry")]
public class PrefabRegistry : ScriptableObject {
    public List<PrefabEntry> entries = new();
}
