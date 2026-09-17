using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Captures one click without changing the editor selection or transform tool state.
public sealed class ObjectScreenPicker : MonoBehaviour, IPointerClickHandler
{
    static ObjectScreenPicker active;
    static int consumedFrame = -1;
    public static bool Capturing => active || consumedFrame == Time.frameCount;
    ObjectDropdownBrowser owner;
    Func<string, bool> accept;
    ObjectCandidatePreview preview;
    Texture2D cursor;
    TMP_FontAsset font;

    public static void Begin(ObjectDropdownBrowser owner, Func<string, bool> accept, TMP_FontAsset font)
    {
        if (active) active.Close();
        var go = new GameObject("Object screen picker", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster), typeof(Image));
        go.hideFlags = HideFlags.DontSave;
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32760;
        go.GetComponent<Image>().color = Color.clear;
        active = go.AddComponent<ObjectScreenPicker>();
        active.owner = owner;
        active.accept = accept;
        active.font = font;
        active.preview = go.AddComponent<ObjectCandidatePreview>();
        active.cursor = MakeCursor();
        Cursor.SetCursor(active.cursor, new Vector2(5, 27), CursorMode.Auto);
        if (EventSystem.current) EventSystem.current.SetSelectedGameObject(null);
        consumedFrame = Time.frameCount;
    }

    public static void Cancel(ObjectDropdownBrowser owner)
    {
        if (active && active.owner == owner) active.Close();
    }

    void Update()
    {
        if (!owner || !owner.isActiveAndEnabled || EditInput.CancelPressedThisFrame()) { Close(); return; }
        var candidate = Pick(EditInput.MousePosition);
        preview.Show(candidate, candidate ? ImportedModelParts.Label(candidate) : "画面上のオブジェクトを選択（Esc / 右クリックでキャンセル）", font);
    }

    static PlacedObject Pick(Vector2 point)
    {
        var camera = EditWorkspace.ResolveCamera();
        if (!camera || !camera.pixelRect.Contains(point) || PlacementController.IsScreenPositionOverBlockingUi(point)) return null;
        Physics.SyncTransforms();
        return PlacedObjectPicker.TryPick(camera.ScreenPointToRay(point), ~0, out var picked, out _, out _) ? picked : null;
    }

    public void OnPointerClick(PointerEventData data)
    {
        if (data.button == PointerEventData.InputButton.Right) { Close(); return; }
        if (data.button != PointerEventData.InputButton.Left) return;
        var candidate = Pick(data.position);
        if (candidate && accept != null && accept(candidate.id)) Close();
    }

    void Close()
    {
        consumedFrame = Time.frameCount;
        if (active == this) active = null;
        gameObject.SetActive(false);
        Destroy(gameObject);
    }

    void OnDisable()
    {
        if (active == this) active = null;
        consumedFrame = Time.frameCount;
        Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
    }
    void OnDestroy() { if (cursor) Destroy(cursor); }

    static Texture2D MakeCursor()
    {
        var texture = new Texture2D(32, 32, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
        // Diagonal pipette: fine tip, outlined tube and a wider rubber bulb.
        var pixels = new Color32[32 * 32];
        for (int y = 0; y < 32; y++) for (int x = 0; x < 32; x++)
        {
            float along = (x + y - 10) * 0.7071f;
            float across = Mathf.Abs(x - y) * 0.7071f;
            float radius = along < 3 ? 1 : along < 20 ? 3 : 5;
            if (along < 0 || along > 31 || across > radius) continue;
            pixels[y * 32 + x] = across > radius - 1.3f || along > 20 ? new Color32(30, 30, 30, 255) : new Color32(255, 220, 145, 255);
        }
        texture.SetPixels32(pixels);
        texture.Apply();
        return texture;
    }
}
