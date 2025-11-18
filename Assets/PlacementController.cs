using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class PlacementController : MonoBehaviour {
    public PrefabRegistry registry;
    public Camera cam;
    public float gridSize = 0.1f;   // 10cm
    public LayerMask floorMask;     // Floorレイヤー

    string currentTypeId = null;

    Dictionary<string, GameObject> map;
    void Awake() {
        map = new Dictionary<string, GameObject>();
        foreach (var e in registry.entries) {
            if (!map.ContainsKey(e.typeId) && e.prefab != null)
                map.Add(e.typeId, e.prefab);
        }
    }

    public void EnterPlacement(string typeId) {
        currentTypeId = map.ContainsKey(typeId) ? typeId : null;
    }

    void Update() {
        if (string.IsNullOrEmpty(currentTypeId)) return;
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

        if (Input.GetMouseButtonDown(0)) {
            Ray ray = cam.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out var hit, 1000f, floorMask)) {
                Vector3 p = hit.point;
                p.x = Mathf.Round(p.x / gridSize) * gridSize;
                p.y = hit.point.y; // 床の高さ
                p.z = Mathf.Round(p.z / gridSize) * gridSize;

                var prefab = map[currentTypeId];
                var go = Instantiate(prefab, p, Quaternion.identity);
                go.AddComponent<PlacedObject>().Init(currentTypeId);
            }
        }
    }
}

public class PlacedObject : MonoBehaviour {
    public string id;
    public string typeId;
    static int seq = 0;
    public void Init(string t) { typeId = t; id = "obj-" + (++seq).ToString("D4"); }
}