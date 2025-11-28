using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class PlacementController : MonoBehaviour
{
    public PrefabRegistry registry;
    public Camera cam;
    public float gridSize = 0.1f;   // 10cm
    public LayerMask floorMask;     // Floorレイヤー

    string currentTypeId = null;
    Dictionary<string, GameObject> map;

    void Awake()
    {
        map = new Dictionary<string, GameObject>();

        if (registry == null)
        {
            Debug.LogError("PlacementController: registry not set");
            return;
        }

        foreach (var e in registry.entries)
        {
            if (!map.ContainsKey(e.typeId) && e.prefab != null)
            {
                map.Add(e.typeId, e.prefab);
            }
        }

        Debug.Log($"[Placement] Registry loaded. entries={map.Count}");
    }

    // カタログのボタンから呼ばれる
    public void EnterPlacement(string typeId)
    {
        if (string.IsNullOrEmpty(typeId))
        {
            Debug.LogWarning("[Placement] EnterPlacement called with null/empty typeId");
            currentTypeId = null;
            return;
        }

        if (map.ContainsKey(typeId))
        {
            currentTypeId = typeId;
            Debug.Log($"[Placement] EnterPlacement OK: {currentTypeId}");
        }
        else
        {
            currentTypeId = null;
            Debug.LogWarning($"[Placement] EnterPlacement NG: {typeId} is not in registry");
        }
    }

    void Update()
    {
        // 何も選択されてなければ配置モードじゃない
        if (string.IsNullOrEmpty(currentTypeId)) return;

        // UI 上をクリックしてるときは無視
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        {
            if (Input.GetMouseButtonDown(0))
            {
                Debug.Log("[Placement] Click ignored because pointer is over UI");
            }
            return;
        }

        // 左クリックされたら Raycast
        if (Input.GetMouseButtonDown(0))
        {
            Debug.Log("[Placement] Click received, doing raycast");

            Ray ray = cam.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out var hit, 1000f, floorMask))
            {
                Debug.Log($"[Placement] Ray hit: {hit.collider.gameObject.name} at {hit.point}");

                // スナップ付き位置計算
                Vector3 p = hit.point;
                p.x = Mathf.Round(p.x / gridSize) * gridSize;
                p.z = Mathf.Round(p.z / gridSize) * gridSize;
                p.y = hit.point.y + 0.5f;  // 床から少し浮かせる（見やすくする用）

                if (map.TryGetValue(currentTypeId, out var prefab) && prefab != null)
                {
                    var go = Object.Instantiate(prefab, p, Quaternion.identity);
                    var po = go.AddComponent<PlacedObject>();
                    po.Init(currentTypeId);

                    Debug.Log($"[Placement] Placed {currentTypeId} as {go.name} at {p}");
                }
                else
                {
                    Debug.LogError($"[Placement] currentTypeId {currentTypeId} not found in map at placement time");
                }
            }
            else
            {
                Debug.LogWarning("[Placement] Raycast did not hit floor");
            }
        }
    }
}

public class PlacedObject : MonoBehaviour
{
    public string id;
    public string typeId;
    static int seq = 0;
    public void Init(string t)
    {
        typeId = t;
        id = "obj-" + (++seq).ToString("D4");
    }
}
