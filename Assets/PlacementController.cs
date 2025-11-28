using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class PlacementController : MonoBehaviour
{
    public PrefabRegistry registry;
    public Camera cam;
    public float gridSize = 0.1f;   // 10cm
    public LayerMask floorMask;     // Floorレイヤー

    // 追加：配置した直後に選択するための参照
    public SelectionService selection;

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

    // 配置モード終了用ヘルパー
    void CancelPlacement()
    {
        if (!string.IsNullOrEmpty(currentTypeId))
        {
            Debug.Log($"[Placement] CancelPlacement: {currentTypeId}");
        }
        currentTypeId = null;
    }

    // カタログのボタンから呼ばれる
    public void EnterPlacement(string typeId)
    {
        // いったん前のモードをクリア
        CancelPlacement();

        if (string.IsNullOrEmpty(typeId))
        {
            Debug.LogWarning("[Placement] EnterPlacement called with null/empty typeId");
            return;
        }

        if (map.ContainsKey(typeId))
        {
            currentTypeId = typeId;
            Debug.Log($"[Placement] EnterPlacement OK: {currentTypeId}");
        }
        else
        {
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

                    // プレハブに PlacedObject が無ければ追加
                    var po = go.GetComponent<PlacedObject>();
                    if (po == null)
                    {
                        po = go.AddComponent<PlacedObject>();
                    }
                    po.Init(currentTypeId);

                    Debug.Log($"[Placement] Placed {currentTypeId} as {go.name} at {p}");

                    // 追加：配置したオブジェクトを自動選択
                    if (selection != null)
                    {
                        selection.Select(po);
                    }
                    else
                    {
                        Debug.LogWarning("[Placement] selection is not assigned, auto-selection skipped");
                    }

                    // 追加：1回置いたら配置モード終了
                    CancelPlacement();
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
