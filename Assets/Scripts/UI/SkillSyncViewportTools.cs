using TMPro;
using UnityEngine;

// Runtime-built camera helpers on the 3D screen (kept out of UIRoot.prefab like the other Ensure* controls):
// a clickable orientation gizmo and the plain-language heading "3D画面". Focusing the selection is the F key or the
// right-click menu, and the mouse / trackpad switch lives in 詳細設定, so the corner of the 3D screen stays clear.
public sealed class SkillSyncViewportTools : MonoBehaviour
{
    SkillSyncDesignView view;
    EditorCameraController cameraRig;
    TMP_FontAsset font;

    public static SkillSyncViewportTools Ensure(SkillSyncDesignView view, TMP_FontAsset font, EditorCameraController cameraRig)
    {
        if (view == null || view.viewport == null) return null;
        var tools = view.viewport.GetComponentInChildren<SkillSyncViewportTools>(true);
        if (tools == null)
        {
            var host = new GameObject("ViewportTools", typeof(RectTransform));
            var rect = (RectTransform)host.transform;
            rect.SetParent(view.viewport, false);
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
            tools = host.AddComponent<SkillSyncViewportTools>();
        }
        tools.view = view; tools.font = font; tools.cameraRig = cameraRig;
        tools.Build();
        return tools;
    }

    void Build()
    {
        // Plain-language heading (the design file calls the area "3Dビューポート").
        foreach (var visual in view.visuals)
            if (visual.label != null && visual.label.text == "3Dビューポート") visual.label.text = "3D画面";

        if (view.orientation != null)
        {
            var gizmo = view.orientation;
            gizmo.raycastTarget = true;
            if (gizmo.GetComponent<EditorUiInputBlocker>() == null) gizmo.gameObject.AddComponent<EditorUiInputBlocker>();
            var tip = gizmo.GetComponent<SkillSyncHoverTip>() ?? gizmo.gameObject.AddComponent<SkillSyncHoverTip>();
            tip.font = font;
            tip.text = "軸の丸をクリックすると、その向きから見ます（Y＝上から）";
            gizmo.axisClicked = axis => { if (cameraRig != null) cameraRig.SetViewPreset(PresetFor(axis)); };
        }
    }

    static EditorCameraController.ViewPreset PresetFor(Vector3 axis)
    {
        if (axis.x > .5f) return EditorCameraController.ViewPreset.Right;
        if (axis.x < -.5f) return EditorCameraController.ViewPreset.Left;
        if (axis.y > .5f) return EditorCameraController.ViewPreset.Top;
        if (axis.y < -.5f) return EditorCameraController.ViewPreset.Bottom;
        return axis.z > .5f ? EditorCameraController.ViewPreset.Back : EditorCameraController.ViewPreset.Front;
    }
}
