using UnityEngine;
using UnityEngine.EventSystems;

public class MoveTool : MonoBehaviour {
    public Camera cam;
    public SelectionService sel;
    public float gridSize = 0.1f;
    public LayerMask floorMask;

    void Update() {
        if (EditModeService.I==null || EditModeService.I.Mode != EditMode.Move) return;
        if (sel.Current == null) return;
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

        // マウス位置の床ヒット
        if (Input.GetMouseButton(0)) {
            Ray ray = cam.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out var hit, 1000f, floorMask)) {
                Vector3 p = hit.point;
                p.x = Mathf.Round(p.x / gridSize) * gridSize;
                p.z = Mathf.Round(p.z / gridSize) * gridSize;
                p.y = sel.Current.transform.position.y; // 高さは維持
                sel.Current.transform.position = p;
            }
        }
        // 矢印キーで微調整（±1グリッド）
        Vector3 nudge = Vector3.zero;
        if (Input.GetKeyDown(KeyCode.UpArrow)) nudge += new Vector3(0,0,gridSize);
        if (Input.GetKeyDown(KeyCode.DownArrow)) nudge += new Vector3(0,0,-gridSize);
        if (Input.GetKeyDown(KeyCode.LeftArrow)) nudge += new Vector3(-gridSize,0,0);
        if (Input.GetKeyDown(KeyCode.RightArrow)) nudge += new Vector3(gridSize,0,0);
        if (nudge != Vector3.zero) sel.Current.transform.position += nudge;
    }
}