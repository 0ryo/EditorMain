using UnityEngine;
using UnityEngine.EventSystems;

public class SelectionService : MonoBehaviour {
    public Camera cam;
    public LayerMask pickMask = ~0; // すべて
    public PlacedObject Current;
    public SelectionOutline outline; // ハイライト描画

    void Update() {
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
                Destroy(Current.gameObject);
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
