using UnityEngine;
using UnityEngine.EventSystems;

public class MoveTool : MonoBehaviour
{
    public Camera cam;
    public SelectionService sel;
    public float gridSize = 0.1f;
    public LayerMask floorMask;

    bool isDragging = false;
    Vector3 startPos;

    void Update()
    {
        // モードがMoveじゃなければ何もしない
        if (EditModeService.I == null || EditModeService.I.Mode != EditMode.Transform)
        {
            isDragging = false;
            return;
        }

        // UI上は無視
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            return;

        // 左クリック押した瞬間：ドラッグ開始 or 選択解除判定
        if (Input.GetMouseButtonDown(0))
        {
            Ray ray = cam.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out var hit, 1000f))
            {
                // まず「何に当たったか」を見る
                var po = hit.collider.GetComponentInParent<PlacedObject>();

                if (po != null)
                {
                    if (po == sel.Current)
                    {
                        // 選択中オブジェクトの上を押した → ドラッグ開始
                        isDragging = true;
                        startPos = sel.Current.transform.position;
                    }
                    else
                    {
                        // 別のオブジェクトを押した → MoveToolは何もしない
                        // SelectionService 側のクリック処理に任せる
                        isDragging = false;
                        return;
                    }
                }
                else
                {
                    // PlacedObject 以外（床など）をクリック → 選択解除＋モード終了
                    sel.Select(null);
                    EditModeService.I.SetMode(EditMode.Browse);
                    isDragging = false;
                    return;
                }
            }
            else
            {
                // 何にも当たらない空間をクリック → 選択解除＋モード終了
                sel.Select(null);
                EditModeService.I.SetMode(EditMode.Browse);
                isDragging = false;
                return;
            }
        }

        // ボタン離したらドラッグ終了
        if (Input.GetMouseButtonUp(0))
        {
            if (isDragging && sel.Current != null)
            {
                var endPos = sel.Current.transform.position;
                if (Vector3.Distance(startPos, endPos) > 0.001f)
                {
                    var cmd = new MoveObjectCommand(sel.Current.gameObject, startPos, endPos);
                    CommandService.I.Stack.Execute(cmd);
                }
            }
            isDragging = false;
        }

        // ここまでの処理で Current が消えていたら何もしない
        if (sel.Current == null) return;

        // ドラッグ中だけ床に沿って移動
        if (isDragging && Input.GetMouseButton(0))
        {
            Ray ray = cam.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out var hit, 1000f, floorMask))
            {
                Vector3 p = hit.point;
                p.x = Mathf.Round(p.x / gridSize) * gridSize;
                p.z = Mathf.Round(p.z / gridSize) * gridSize;
                p.y = sel.Current.transform.position.y; // 高さは維持
                sel.Current.transform.position = p;
            }
        }

        // 矢印キーで微調整（±1グリッド）は今まで通り
        Vector3 nudge = Vector3.zero;
        bool modifierPressed =
            Input.GetKey(KeyCode.LeftControl) ||
            Input.GetKey(KeyCode.RightControl) ||
            Input.GetKey(KeyCode.LeftCommand) ||
            Input.GetKey(KeyCode.RightCommand) ||
            Input.GetKey(KeyCode.LeftAlt) ||
            Input.GetKey(KeyCode.RightAlt);

        if (!modifierPressed)
        {
            if (Input.GetKeyDown(KeyCode.W)) nudge += new Vector3(0, 0, gridSize);
            if (Input.GetKeyDown(KeyCode.S)) nudge += new Vector3(0, 0, -gridSize);
            if (Input.GetKeyDown(KeyCode.A)) nudge += new Vector3(-gridSize, 0, 0);
            if (Input.GetKeyDown(KeyCode.D)) nudge += new Vector3(gridSize, 0, 0);
        }

        if (Input.GetKeyDown(KeyCode.UpArrow)) nudge += new Vector3(0, 0, gridSize);
        if (Input.GetKeyDown(KeyCode.DownArrow)) nudge += new Vector3(0, 0, -gridSize);
        if (Input.GetKeyDown(KeyCode.LeftArrow)) nudge += new Vector3(-gridSize, 0, 0);
        if (Input.GetKeyDown(KeyCode.RightArrow)) nudge += new Vector3(gridSize, 0, 0);

        if (nudge != Vector3.zero && sel.Current != null)
        {
            sel.Current.transform.position += nudge;
        }
    }
}
