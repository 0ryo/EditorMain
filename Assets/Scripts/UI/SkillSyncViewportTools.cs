using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Runtime-built camera helpers on the 3D screen (kept out of UIRoot.prefab like the other Ensure* controls):
// "選択を表示" (disabled with a reason when nothing is selected), the mouse / trackpad switch, a clickable orientation gizmo,
// and the plain-language heading "3D画面".
public sealed class SkillSyncViewportTools : MonoBehaviour
{
    static readonly Color Ink = new Color32(23, 34, 54, 255);
    static readonly Color Muted = new Color32(130, 142, 160, 255);
    const float Margin = 14f;

    SkillSyncDesignView view;
    SelectionService selection;
    EditorCameraController cameraRig;
    TMP_FontAsset font;
    Button focusButton, modeButton;
    TMP_Text focusLabel, modeLabel;
    SkillSyncHoverTip focusTip, modeTip;
    SkillSyncRoundedGraphic focusSurface;

    public static SkillSyncViewportTools Ensure(SkillSyncDesignView view, TMP_FontAsset font, SelectionService selection, EditorCameraController cameraRig, System.Action focusSelected)
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
        tools.view = view; tools.font = font; tools.selection = selection; tools.cameraRig = cameraRig;
        tools.Build(focusSelected);
        return tools;
    }

    void Build(System.Action focusSelected)
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

        if (focusButton == null)
        {
            focusButton = NewButton("FocusSelected", "選択を表示", 112, -94, out focusLabel, out focusSurface, out focusTip);
            focusButton.onClick.AddListener(() => focusSelected?.Invoke());
        }
        if (modeButton == null)
        {
            modeButton = NewButton("CameraInputMode", "", 158, -130, out modeLabel, out _, out modeTip);
            modeButton.onClick.AddListener(EditorCameraInputSettings.Toggle);
        }
        Refresh();
    }

    static EditorCameraController.ViewPreset PresetFor(Vector3 axis)
    {
        if (axis.x > .5f) return EditorCameraController.ViewPreset.Right;
        if (axis.x < -.5f) return EditorCameraController.ViewPreset.Left;
        if (axis.y > .5f) return EditorCameraController.ViewPreset.Top;
        if (axis.y < -.5f) return EditorCameraController.ViewPreset.Bottom;
        return axis.z > .5f ? EditorCameraController.ViewPreset.Back : EditorCameraController.ViewPreset.Front;
    }

    Button NewButton(string name, string caption, float width, float y, out TMP_Text label, out SkillSyncRoundedGraphic surface, out SkillSyncHoverTip tip)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(SkillSyncRoundedGraphic), typeof(Button), typeof(EditorUiInputBlocker), typeof(SkillSyncHoverTip));
        var rect = (RectTransform)go.transform;
        rect.SetParent(transform, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one;
        rect.sizeDelta = new Vector2(width, 30); rect.anchoredPosition = new Vector2(-Margin, y);
        surface = go.GetComponent<SkillSyncRoundedGraphic>();
        surface.color = Color.white; surface.borderColor = new Color32(205, 213, 224, 255); surface.radius = 10; surface.borderWidth = 1;
        var button = go.GetComponent<Button>();
        button.targetGraphic = surface;
        var colors = ColorBlock.defaultColorBlock;
        colors.normalColor = Color.white; colors.highlightedColor = new Color(.88f, .93f, 1f, 1f);
        colors.pressedColor = new Color(.78f, .86f, 1f, 1f); colors.selectedColor = Color.white;
        colors.disabledColor = new Color(.94f, .95f, .97f, 1f);
        button.colors = colors;
        var labelObject = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        var labelRect = (RectTransform)labelObject.transform;
        labelRect.SetParent(rect, false);
        labelRect.anchorMin = Vector2.zero; labelRect.anchorMax = Vector2.one; labelRect.offsetMin = labelRect.offsetMax = Vector2.zero;
        label = labelObject.GetComponent<TextMeshProUGUI>();
        if (font != null) label.font = font;
        label.text = caption; label.fontSize = 13; label.color = Ink; label.alignment = TextAlignmentOptions.Midline;
        label.textWrappingMode = TextWrappingModes.NoWrap; label.richText = false; label.raycastTarget = false;
        tip = go.GetComponent<SkillSyncHoverTip>();
        tip.font = font;
        return button;
    }

    void OnEnable() { EditorCameraInputSettings.Changed += Refresh; }
    void OnDisable() { EditorCameraInputSettings.Changed -= Refresh; }

    void Update()
    {
        if (view == null || focusButton == null) return;
        Refresh();
    }

    void Refresh()
    {
        if (view == null || focusButton == null || modeButton == null) return;
        int state = view.State;
        bool editing = state == 0 || state == 1 || state == 3;
        SetActive(focusButton.gameObject, editing);
        SetActive(modeButton.gameObject, state != 5);

        bool hasSelection = selection != null && selection.Selected.Any(SelectionService.CanEdit);
        if (focusButton.interactable != hasSelection) focusButton.interactable = hasSelection;
        focusLabel.color = hasSelection ? Ink : Muted;
        focusTip.text = hasSelection ? "選んだ部品を画面に収めます（Fキー）" : "部品を選ぶと使えます。選んだ部品を画面に収めます";

        bool trackpad = EditorCameraInputSettings.UseTrackpad;
        string caption = trackpad ? "操作：トラックパッド" : "操作：マウス";
        if (modeLabel.text != caption) modeLabel.text = caption;
        modeTip.text = trackpad
            ? "クリックでマウスに切替｜2本指＝回転、Shift＋2本指＝移動、Cmd＋2本指＝ズーム"
            : "クリックでトラックパッドに切替｜ホイール＝ズーム、右ドラッグ＝回転";
    }

    static void SetActive(GameObject target, bool active)
    {
        if (target.activeSelf != active) target.SetActive(active);
    }
}
