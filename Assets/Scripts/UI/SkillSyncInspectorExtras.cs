using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Runtime additions to the right-hand inspector, built in code like the other Ensure* controls so UIRoot.prefab stays
// untouched: the selected tab highlight and its one-line guide, the scale field, the group buttons, the note about
// what a selected group means, and the real state of the snap toggle. The 配置の補助 block slides down to make room.
public sealed class SkillSyncInspectorExtras
{
    static readonly Color Accent = new Color32(8, 102, 232, 255);
    static readonly Color Border = new Color32(205, 213, 224, 255);
    static readonly Color Ink = new Color32(23, 34, 54, 255);
    static readonly Color Muted = new Color32(88, 103, 124, 255);
    const float Gap = 8f, ButtonHeight = 40f, ButtonWidth = 184f, AssistGap = 14f;

    public Action<string> scaleEntered;
    public Action group, ungroup;

    readonly SkillSyncDesignView view;
    readonly bool ready;
    readonly RectTransform container;
    readonly TMP_InputField nameField;
    readonly float left, width, fieldBottom, assistLineTop;
    readonly TMP_Text heightLabel, angleLabel, boxTitle, boxBody, snapRow1, snapRow2;
    readonly List<(RectTransform rect, float y)> assist = new();
    readonly Dictionary<string, (Graphic fill, TMP_Text label, Color color)> tabs = new();
    TMP_Text note, scaleLabel, scaleGuide;
    TMP_InputField scaleField;
    Button groupButton, ungroupButton;
    SkillSyncRoundedGraphic snapBox;
    TMP_Text snapMark;
    string signature;
    bool? lastSnap;

    public SkillSyncInspectorExtras(SkillSyncDesignView view)
    {
        this.view = view;
        var name = view.fields?.FirstOrDefault(f => f.role == "name" && (f.mask & 1) != 0)?.input;
        var height = view.fields?.FirstOrDefault(f => f.role == "height")?.input;
        heightLabel = FindLabel(text => text == "高さ");
        angleLabel = FindLabel(text => text == "向き");
        if (name == null || height == null || heightLabel == null || angleLabel == null) return;
        nameField = name;
        var nameRect = (RectTransform)name.transform;
        var slide = nameRect.parent as RectTransform;
        if (slide == null) return;
        var heightRect = (RectTransform)height.transform;
        left = nameRect.anchoredPosition.x;
        width = nameRect.sizeDelta.x;
        fieldBottom = -heightRect.anchoredPosition.y + heightRect.sizeDelta.y;

        boxTitle = FindLabel(text => text.StartsWith("✓ 作業面に沿って移動", StringComparison.Ordinal));
        boxBody = FindLabel(text => text.StartsWith("上下の位置がずれないように", StringComparison.Ordinal));
        foreach (var box in new[] { boxTitle, boxBody })
        {
            if (box == null) continue;
            box.rectTransform.sizeDelta = new Vector2(width - 32f, box.rectTransform.sizeDelta.y);
            box.textWrappingMode = TextWrappingModes.NoWrap;
            box.overflowMode = TextOverflowModes.Ellipsis;
        }

        snapRow1 = FindLabel(text => text.StartsWith("✓ 作業面に吸着", StringComparison.Ordinal));
        snapRow2 = FindLabel(text => text.StartsWith("回転は", StringComparison.Ordinal));
        var assistTitle = FindLabel(text => text == "配置の補助");
        if (assistTitle != null)
        {
            // Everything of the assist block moves together; its line is the one thin visual just above the title.
            var line = view.visuals.FirstOrDefault(v => v.label == null && v.target != null && (v.mask & 1) != 0 &&
                v.target.transform is RectTransform r && r.sizeDelta.y <= 1.5f && Mathf.Abs(r.anchoredPosition.x - left) < 1f &&
                -r.anchoredPosition.y < -((RectTransform)assistTitle.transform).anchoredPosition.y);
            if (line != null) assist.Add(((RectTransform)line.target.transform, ((RectTransform)line.target.transform).anchoredPosition.y));
            assistLineTop = line != null ? -((RectTransform)line.target.transform).anchoredPosition.y : -((RectTransform)assistTitle.transform).anchoredPosition.y - 20f;
            foreach (var label in new[] { assistTitle, snapRow1, snapRow2 })
                if (label != null) assist.Add(((RectTransform)label.transform, ((RectTransform)label.transform).anchoredPosition.y));
            foreach (var action in new[] { "Snap", "詳細設定" })
            {
                var control = view.controls.FirstOrDefault(c => c.action == action && c.button != null);
                if (control != null) assist.Add(((RectTransform)control.button.transform, ((RectTransform)control.button.transform).anchoredPosition.y));
            }
        }
        else assistLineTop = fieldBottom + 100f;

        container = new GameObject("InspectorExtras", typeof(RectTransform)).GetComponent<RectTransform>();
        container.SetParent(slide, false);
        container.anchorMin = container.anchorMax = container.pivot = new Vector2(0, 1);
        container.anchoredPosition = Vector2.zero;
        container.sizeDelta = Vector2.zero;

        BuildTabs();
        BuildSnapBox();
        BuildScale();
        BuildNote();
        BuildGroupButtons();
        ready = true;
    }

    TMP_Text FindLabel(Func<string, bool> match) =>
        view.visuals.Where(v => v.label != null && (v.mask & 1) != 0 && !string.IsNullOrEmpty(v.label.text) && match(v.label.text))
            .Select(v => v.label).FirstOrDefault();

    // The tab buttons already exist in the prefab; each gets an accent fill behind its caption.
    void BuildTabs()
    {
        foreach (var name in new[] { "移動", "回転", "大きさ" })
        {
            var button = view.controls.FirstOrDefault(c => c.action == name && c.button != null)?.button;
            var label = button != null ? button.GetComponentInChildren<TMP_Text>(true) : null;
            if (label == null) continue;
            var go = new GameObject("ActiveTab", typeof(RectTransform), typeof(SkillSyncRoundedGraphic));
            var rect = (RectTransform)go.transform;
            rect.SetParent(button.transform, false);
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
            rect.SetAsFirstSibling();
            var fill = go.GetComponent<SkillSyncRoundedGraphic>();
            fill.radius = 10; fill.borderWidth = 1; fill.color = fill.borderColor = Accent; fill.raycastTarget = false;
            go.SetActive(false);
            tabs[name] = (fill, label, label.color);
        }
    }

    void BuildSnapBox()
    {
        var snap = view.controls.FirstOrDefault(c => c.action == "Snap" && c.button != null)?.button;
        if (snap == null) return;
        var go = new GameObject("SnapCheck", typeof(RectTransform), typeof(SkillSyncRoundedGraphic));
        var rect = (RectTransform)go.transform;
        rect.SetParent(snap.transform, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
        rect.sizeDelta = new Vector2(18, 18);
        rect.anchoredPosition = new Vector2(0, -(((RectTransform)snap.transform).sizeDelta.y - 18f) / 2f);
        snapBox = go.GetComponent<SkillSyncRoundedGraphic>();
        snapBox.radius = 4; snapBox.borderWidth = 1.5f; snapBox.raycastTarget = false;
        snapMark = NewLabel(go.transform, "Mark", 14, Color.white, TextAlignmentOptions.Midline);
        snapMark.text = "✓";
        var markRect = snapMark.rectTransform;
        markRect.anchorMin = Vector2.zero; markRect.anchorMax = Vector2.one; markRect.offsetMin = markRect.offsetMax = Vector2.zero;
        foreach (var row in new[] { snapRow1, snapRow2 })
        {
            if (row == null) continue;
            row.rectTransform.anchoredPosition += Vector2.right * 28f;
            row.rectTransform.sizeDelta = new Vector2(width - 28f, row.rectTransform.sizeDelta.y);
            row.textWrappingMode = TextWrappingModes.NoWrap;
            row.overflowMode = TextOverflowModes.Ellipsis;
        }
    }

    void BuildScale()
    {
        scaleLabel = NewLabel(container, "ScaleLabel", heightLabel.fontSize, heightLabel.color, TextAlignmentOptions.BaselineLeft);
        scaleLabel.text = "倍率";
        scaleLabel.rectTransform.sizeDelta = new Vector2(160, 20);

        var go = new GameObject("ScaleField", typeof(RectTransform), typeof(SkillSyncRoundedGraphic), typeof(TMP_InputField), typeof(EditorUiInputBlocker));
        var rect = (RectTransform)go.transform;
        rect.SetParent(container, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
        rect.sizeDelta = new Vector2(ButtonWidth, 47.89f);
        var background = go.GetComponent<SkillSyncRoundedGraphic>();
        background.color = Color.white; background.borderColor = Border; background.radius = 10; background.borderWidth = 1;
        var area = new GameObject("Text Area", typeof(RectTransform), typeof(RectMask2D));
        var areaRect = (RectTransform)area.transform;
        areaRect.SetParent(rect, false);
        areaRect.anchorMin = Vector2.zero; areaRect.anchorMax = Vector2.one;
        areaRect.offsetMin = new Vector2(14, 2); areaRect.offsetMax = new Vector2(-44, -2);
        var text = NewLabel(areaRect, "Text", 17, Ink, TextAlignmentOptions.MidlineLeft);
        var placeholder = NewLabel(areaRect, "Placeholder", 17, Muted, TextAlignmentOptions.MidlineLeft);
        placeholder.text = "100";
        foreach (var t in new[] { text, placeholder })
        {
            t.rectTransform.anchorMin = Vector2.zero; t.rectTransform.anchorMax = Vector2.one;
            t.rectTransform.offsetMin = t.rectTransform.offsetMax = Vector2.zero;
        }
        var unit = NewLabel(rect, "Unit", 14, Muted, TextAlignmentOptions.MidlineLeft);
        unit.text = "%";
        unit.rectTransform.anchorMin = new Vector2(1, 0); unit.rectTransform.anchorMax = Vector2.one;
        unit.rectTransform.offsetMin = new Vector2(-34, 0); unit.rectTransform.offsetMax = Vector2.zero;
        scaleField = go.GetComponent<TMP_InputField>();
        scaleField.textViewport = areaRect; scaleField.textComponent = text; scaleField.placeholder = placeholder; scaleField.targetGraphic = background;
        scaleField.lineType = TMP_InputField.LineType.SingleLine; scaleField.transition = Selectable.Transition.None;
        scaleField.onEndEdit.AddListener(value => scaleEntered?.Invoke(value));

        scaleGuide = NewLabel(container, "ScaleGuide", 13, Muted, TextAlignmentOptions.MidlineLeft);
        scaleGuide.text = "角をドラッグして大きさを変えます";
        scaleGuide.textWrappingMode = TextWrappingModes.Normal;
    }

    void BuildNote()
    {
        note = NewLabel(container, "SelectionNote", 13, Muted, TextAlignmentOptions.TopLeft);
        note.textWrappingMode = TextWrappingModes.Normal;
        note.overflowMode = TextOverflowModes.Overflow;
    }

    void BuildGroupButtons()
    {
        groupButton = NewButton("GroupButton", "グループにする", () => group?.Invoke());
        ungroupButton = NewButton("UngroupButton", "グループを解除", () => ungroup?.Invoke());
    }

    Button NewButton(string name, string caption, Action click)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(SkillSyncRoundedGraphic), typeof(Button), typeof(EditorUiInputBlocker));
        var rect = (RectTransform)go.transform;
        rect.SetParent(container, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
        rect.sizeDelta = new Vector2(ButtonWidth, ButtonHeight);
        var surface = go.GetComponent<SkillSyncRoundedGraphic>();
        surface.color = Color.white; surface.borderColor = Border; surface.radius = 10; surface.borderWidth = 1;
        var button = go.GetComponent<Button>();
        button.targetGraphic = surface;
        var colors = ColorBlock.defaultColorBlock;
        colors.normalColor = Color.white; colors.highlightedColor = new Color(.93f, .96f, 1f, 1f);
        colors.pressedColor = new Color(.85f, .9f, 1f, 1f); colors.selectedColor = Color.white;
        button.colors = colors;
        button.onClick.AddListener(() => click());
        var label = NewLabel(rect, "Label", 15, Ink, TextAlignmentOptions.Midline);
        label.text = caption;
        label.rectTransform.anchorMin = Vector2.zero; label.rectTransform.anchorMax = Vector2.one;
        label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;
        UiAccessibilityMetrics.EnsureButtonTarget(button);
        return button;
    }

    TMP_Text NewLabel(Transform parent, string name, float size, Color color, TextAlignmentOptions alignment)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
        var label = go.GetComponent<TextMeshProUGUI>();
        label.font = heightLabel.font; label.fontSharedMaterial = heightLabel.fontSharedMaterial;
        label.fontSize = size; label.color = color; label.alignment = alignment;
        label.textWrappingMode = TextWrappingModes.NoWrap; label.overflowMode = TextOverflowModes.Ellipsis;
        label.richText = false; label.raycastTarget = false; label.margin = Vector4.zero;
        return label;
    }

    // Keeps the extras above the prefab's own panel graphics, which SkillSyncDesignView.Show re-orders on every call.
    public void BringToFront()
    {
        if (ready && container.GetSiblingIndex() != container.parent.childCount - 1) container.SetAsLastSibling();
    }

    public void Refresh(string tab, SkillSyncSelectionInfo info, bool visible, string scaleText, bool nothingSelected = false)
    {
        if (!ready) return;
        ApplyTabs(tab);
        ApplyModeText(tab, nothingSelected);
        ApplySnap();
        bool relative = visible && info.multiple;
        heightLabel.text = relative ? "高さ（動かす量）" : "床からの高さ";
        angleLabel.text = relative ? "向き（回す角度）" : "向き";
        if (visible) nameField.interactable = !info.multiple;
        SetActive(container, visible);
        if (visible)
        {
            if (!scaleField.isFocused) scaleField.SetTextWithoutNotify(scaleText ?? "");
        }

        bool showScale = visible && tab == "大きさ";
        string noteText = visible ? NoteText(info) : "";
        string layoutKey = (visible ? "v" : "h") + (showScale ? "s" : "-") + "|" + noteText + "|" + (visible && info.canGroup) + (visible && info.canUngroup);
        if (layoutKey == signature) return;
        signature = layoutKey;

        float y = fieldBottom + Gap;
        // The blue box on the 3D screen already says "角をドラッグして大きさを変えます"; the line beside the field only repeated it.
        SetActive(scaleLabel, showScale); SetActive(scaleField, showScale); SetActive(scaleGuide, false);
        if (showScale)
        {
            Place(scaleLabel.rectTransform, left, y);
            Place((RectTransform)scaleField.transform, left, y + 22f);
            Place(scaleGuide.rectTransform, left + ButtonWidth + 24f, y + 22f);
            scaleGuide.rectTransform.sizeDelta = new Vector2(width - ButtonWidth - 24f, 47.89f);
            y += 22f + 47.89f + Gap + 2f;
        }
        SetActive(note, noteText.Length > 0);
        if (noteText.Length > 0)
        {
            note.text = noteText;
            float h = Mathf.Ceil(note.GetPreferredValues(noteText, width, Mathf.Infinity).y) + 2f;
            Place(note.rectTransform, left, y);
            note.rectTransform.sizeDelta = new Vector2(width, h);
            y += h + Gap + 2f;
        }
        bool both = visible && info.canGroup && info.canUngroup;
        SetActive(groupButton, visible && info.canGroup);
        SetActive(ungroupButton, visible && info.canUngroup);
        if (visible && info.canGroup) Place((RectTransform)groupButton.transform, left, y);
        if (visible && info.canUngroup) Place((RectTransform)ungroupButton.transform, left + (both ? ButtonWidth + 24f : 0f), y);
        if (visible && (info.canGroup || info.canUngroup)) y += ButtonHeight + Gap;

        float shift = visible ? Mathf.Max(0f, y + AssistGap - assistLineTop) : 0f;
        foreach (var (rect, originalY) in assist)
            rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, originalY - shift);
    }

    static string NoteText(SkillSyncSelectionInfo info)
    {
        var lines = new List<string>();
        if (info.multiple) lines.Add("複数の部品を選んでいます。高さ・向きは、入力した分だけ全員を動かします。");
        if (!string.IsNullOrEmpty(info.hint)) lines.Add(info.hint);
        return string.Join("\n", lines);
    }

    static void Place(RectTransform rect, float x, float top) => rect.anchoredPosition = new Vector2(x, -top);

    static void SetActive(Component part, bool active)
    {
        if (part != null && part.gameObject.activeSelf != active) part.gameObject.SetActive(active);
    }

    void ApplyTabs(string tab)
    {
        foreach (var pair in tabs)
        {
            bool active = pair.Key == tab;
            SetActive(pair.Value.fill, active);
            pair.Value.label.color = active ? Color.white : pair.Value.color;
        }
    }

    // The box under the tabs says what the chosen tab does, instead of a fixed, untrue "locked to the floor".
    void ApplyModeText(string tab, bool nothingSelected)
    {
        if (boxTitle == null || boxBody == null) return;
        if (nothingSelected)
        {
            boxTitle.text = "部品を選んでください";
            boxBody.text = "3D画面か「配置した部品」で選びます";
            return;
        }
        switch (tab)
        {
            case "回転":
                boxTitle.text = "輪をドラッグして回転します";
                boxBody.text = "向き欄に角度を入れても回せます";
                break;
            case "大きさ":
                boxTitle.text = "角をドラッグして大きさを変えます";
                boxBody.text = "下の倍率欄に％で入れても変えられます";
                break;
            default:
                boxTitle.text = "矢印か部品をドラッグして動かします";
                boxBody.text = "床に沿って動きます（上下は緑の矢印か高さ欄）";
                break;
        }
    }

    void ApplySnap()
    {
        bool on = EditSnapSettings.Enabled;
        string grid = (EditSnapSettings.GridSize * 100f).ToString("0.##"), turn = EditSnapSettings.RotationDegrees.ToString("0.##");
        if (snapBox != null && lastSnap != on)
        {
            lastSnap = on;
            snapBox.color = on ? Accent : Color.white;
            snapBox.borderColor = on ? Accent : Muted;
            snapBox.SetVerticesDirty();
            snapMark.gameObject.SetActive(on);
        }
        if (snapRow1 != null) snapRow1.text = on ? "吸着する（" + grid + " cm刻み）" : "吸着しない（押すとオン）";
        if (snapRow2 != null) snapRow2.text = (on ? "回転は" + turn + "°刻み" : "吸着オフ中") + " · 詳細設定 ›";
    }
}
