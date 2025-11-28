using UnityEngine;
using UnityEngine.EventSystems;

public class SelectionService : MonoBehaviour {
    public Camera cam;
    public LayerMask pickMask = ~0; // すべて
    public PlacedObject Current;
    public SelectionOutline outline; // ハイライト描画

    public PrefabRegistry registry; // Undo時の再生成用

    void Update() {
        // Currentが外部(Undoなど)で削除されていたら選択解除
        if (Current != null && Current.gameObject == null) {
            Select(null);
        }

        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

        if (Input.GetMouseButtonDown(0)) {
            Ray ray = cam.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out var hit, 1000f, pickMask)) {
                var po = hit.collider.GetComponentInParent<PlacedObject>();
                if (po != null) Select(po); else Select(null);
            }
        }
        if (Current != null) {
            // 削除
            if (Input.GetKeyDown(KeyCode.Delete)) {
                // Undo用の再生成関数
                System.Func<string, GameObject> factory = (tId) => {
                    if (registry == null) return null;
                    var entry = registry.entries.Find(e => e.typeId == tId);
                    if (entry != null && entry.prefab != null) {
                        var g = Instantiate(entry.prefab);
                        var po = g.GetComponent<PlacedObject>();
                        if (po == null) po = g.AddComponent<PlacedObject>();
                        po.Init(tId);
                        Select(po);
                        return g;
                    }
                    return null;
                };

                var cmd = new DeleteObjectCommand(Current.gameObject, Current.typeId, factory);
                CommandService.I.Stack.Execute(cmd);
                
                Select(null);
            }
            // 複製
            if ((Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightCommand)) && Input.GetKeyDown(KeyCode.D)) {
                var dup = Instantiate(Current.gameObject, Current.transform.position + new Vector3(0.2f,0,0.2f), Current.transform.rotation);
                var po = dup.GetComponent<PlacedObject>(); po.id = null; po.Init(po.typeId);
                Select(po);
            }
        }
    }
    public void Select(PlacedObject po) {
        Current = po;
        if (outline != null) outline.ShowFor(po ? po.gameObject : null);
    }
}
