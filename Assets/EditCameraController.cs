// 置換版（新Input System）
using UnityEngine;
using UnityEngine.InputSystem; // ← 追加

public class EditorCameraController : MonoBehaviour {
    public float orbitSpeed = 3f;    // 右ドラッグの回転速度
    public float panSpeed   = 0.01f; // 中ドラッグの平行移動速度
    public float zoomSpeed  = 10f;   // ホイールのズーム速度
    public Transform pivot;

    void Start() {
        if (pivot == null) {
            var go = new GameObject("CameraPivot");
            go.transform.position = Vector3.zero;
            pivot = go.transform;
        }
        transform.parent = pivot;
        transform.localPosition = new Vector3(0, 5, -10);
        transform.LookAt(pivot.position);
    }

    void Update() {
        if (Mouse.current == null) return; // デバイス未接続ガード

        // ズーム（ホイール）
        // 新Input Systemはスクロール量がベクタ。通常はY成分を使う。
        float scrollY = Mouse.current.scroll.ReadValue().y;
        if (Mathf.Abs(scrollY) > 0.0001f) {
            transform.localPosition += transform.forward * (scrollY * (zoomSpeed * 0.01f));
        }

        // オービット（右ボタン押しながらマウス移動）
        if (Mouse.current.rightButton.isPressed) {
            Vector2 d = Mouse.current.delta.ReadValue(); // マウス移動量
            float dx = d.x * (orbitSpeed * 0.02f);
            float dy = -d.y * (orbitSpeed * 0.02f);
            pivot.Rotate(Vector3.up, dx, Space.World);
            pivot.Rotate(Vector3.right, dy, Space.Self);
        }

        // パン（中ボタン）
        if (Mouse.current.middleButton.isPressed) {
            Vector2 d = Mouse.current.delta.ReadValue();
            float dx = -d.x;
            float dy = -d.y;
            Vector3 right = pivot.right;
            Vector3 up = Vector3.up;
            float dist = Vector3.Distance(transform.position, pivot.position);
            pivot.position += (right * dx + up * dy) * (panSpeed * dist * 0.1f);
        }
    }
}
