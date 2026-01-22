using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class PlacementController : MonoBehaviour
{
    public PrefabRegistry registry;
    public Camera cam;
    public float gridSize = 0.1f;   // 10cm
    public LayerMask floorMask;     // Floorレイヤー

    // 配置した直後に選択するための参照
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

            // ★配置モードに入ったことを EditModeService に伝える
            if (EditModeService.I != null)
                EditModeService.I.Mode = EditMode.Place;

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
                    // Command経由で配置
                    // factory: (typeId) => Instantiate(...)
                    System.Func<string, GameObject> factory = (tId) =>
                    {
                        if (map.TryGetValue(tId, out var pfb) && pfb != null)
                        {
                            var g = Object.Instantiate(pfb);

                            // PlacedObject を必ず付ける
                            var po = g.GetComponent<PlacedObject>();
                            if (po == null) po = g.AddComponent<PlacedObject>();

                            // typeId設定
                            po.InitType(tId);

                            // 配置時は必ず新規ID（Prefabにidが入ってても強制上書き）
                            po.ForceNewId();

                            // 配置したオブジェクトを自動選択 (Command実行時に呼ばれる)
                            if (selection != null) selection.Select(po);

                            return g;
                        }
                        return null;
                    };

                    var cmd = new PlaceObjectCommand(currentTypeId, p, Quaternion.identity, factory);
                    CommandService.I.Stack.Execute(cmd);

                    Debug.Log($"[Placement] Placed {currentTypeId} at {p} via Command");

                    // 1回置いたら配置モード終了
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
    // 既存コードとの互換のため public のまま維持
    public string id;
    public string typeId;

    // フォールバック連番（IdGeneratorが無い場合用）
    static int fallbackSeq = 0;

    // 互換用プロパティ（以降の実装で使うと安全）
    public string Id => id;
    public string TypeId => typeId;

    /// <summary>
    /// typeIdのみ設定（IDはここでは触らない）
    /// </summary>
    public void InitType(string t)
    {
        typeId = t;
    }

    /// <summary>
    /// IDが空なら採番してセット（既に入っているなら維持）
    /// </summary>
    public void EnsureHasId()
    {
        if (!string.IsNullOrEmpty(id)) return;

        // シーンに IdGenerator があればそれを使用
        // （IdGenerator.cs はフェーズ1-2で作成した想定）
        if (IdGenerator.I != null)
        {
            id = IdGenerator.I.NewObjectId();
            return;
        }

        // 無い場合はフォールバック連番（MVPの安全策）
        id = "obj-" + (++fallbackSeq).ToString("D4");
        Debug.LogWarning("[PlacedObject] IdGenerator not found. Fallback sequence is used.");
    }

    /// <summary>
    /// 複製などでID重複しうるケース用：強制的に新規IDを付与
    /// </summary>
    public void ForceNewId()
    {
        id = null;
        EnsureHasId();
    }

    /// <summary>
    /// 既存コード互換：MVP-1の Init(tId) 呼び出しが残っていても動くようにする。
    /// 配置用途として扱い、typeId設定＋新規ID付与を行う。
    /// </summary>
    public void Init(string t)
    {
        InitType(t);
        ForceNewId();
    }
}
