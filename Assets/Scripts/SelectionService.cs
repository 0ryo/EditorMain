using UnityEngine;
using UnityEngine.EventSystems;

public class SelectionService : MonoBehaviour {
    public Camera cam;
    public LayerMask pickMask = ~0; // すべて
    public PlacedObject Current;
    public SelectionOutline outline; // ハイライト描画

    public PrefabRegistry registry; // Undo時の再生成用
    public PlacementController placementController; // ランタイム追加型の再生成用

    void Update() {
        if (placementController == null)
        {
            placementController = FindFirstObjectByType<PlacementController>();
        }

        // Currentが外部(Undoなど)で削除されていたら選択解除
        if (Current != null && Current.gameObject == null) {
            Select(null);
        }

        // UI操作中はピッキングしない
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

        // 左クリック：選択
        if (Input.GetMouseButtonDown(0)) {
            Ray ray = cam.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out var hit, 1000f, pickMask)) {
                var po = hit.collider.GetComponentInParent<PlacedObject>();
                Select(po != null ? po : null);
            }
        }

        if (Current == null) return;

        // 削除（Delete）
        if (Input.GetKeyDown(KeyCode.Delete)) {
            System.Func<string, GameObject> factory = (tId) => {
                if (registry != null)
                {
                    var entry = registry.entries.Find(e => e.typeId == tId);
                    if (entry != null && entry.prefab != null)
                    {
                        return InstantiatePlacedForUndo(entry.prefab, tId);
                    }
                }

                if (placementController != null && placementController.TryGetPrefab(tId, out var runtimePrefab))
                {
                    return InstantiatePlacedForUndo(runtimePrefab, tId);
                }

                return null;
            };

            var deleteCmd = new DeleteObjectCommand(Current.gameObject, Current.typeId, factory);
            CommandService.I.Stack.Execute(deleteCmd);

            Select(null);
            return;
        }

        // 複製（Ctrl/Cmd + D）
        bool controlKey = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
        bool commandKey = Input.GetKey(KeyCode.LeftCommand) || Input.GetKey(KeyCode.RightCommand);

        if ((controlKey || commandKey) && Input.GetKeyDown(KeyCode.D)) {
            var dup = Instantiate(
                Current.gameObject,
                Current.transform.position + new Vector3(0.2f, 0f, 0.2f),
                Current.transform.rotation
            );

            var po = dup.GetComponent<PlacedObject>();
            if (po == null) po = dup.AddComponent<PlacedObject>();

            // typeIdは通常複製でコピーされるが、念のため補完
            if (string.IsNullOrEmpty(po.typeId)) {
                po.typeId = Current.typeId;
            }

            // IDは必ず再発行（重複防止）
            po.ForceNewId();

            Select(po);
            return;
        }
    }

    public void Select(PlacedObject po) {
        Current = po;
        if (outline != null) outline.ShowFor(po ? po.gameObject : null);
    }

    GameObject InstantiatePlacedForUndo(GameObject prefab, string typeId)
    {
        if (prefab == null || string.IsNullOrWhiteSpace(typeId)) return null;

        var created = Instantiate(prefab);
        var placed = created.GetComponent<PlacedObject>();
        if (placed == null) placed = created.AddComponent<PlacedObject>();

        // 配置相当：typeIdセット + 新規ID
        placed.Init(typeId);
        Select(placed);
        return created;
    }
}
