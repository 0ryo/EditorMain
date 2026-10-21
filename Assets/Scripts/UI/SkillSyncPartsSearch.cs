using TMPro;
using UnityEngine;

// Runtime search box above the "配置した部品" list, created in code like the other Ensure* controls
// so the UIRoot prefab stays untouched.
public static class SkillSyncPartsSearch
{
    const string Name = "PartsSearch";

    public static TMP_InputField Ensure(RectTransform objectsRoot, TMP_FontAsset font)
    {
        if (objectsRoot == null) return null;
        var existing = objectsRoot.Find(Name);
        if (existing != null) return existing.GetComponent<TMP_InputField>();

        var go = new GameObject(Name, typeof(RectTransform), typeof(SkillSyncRoundedGraphic), typeof(TMP_InputField), typeof(EditorUiInputBlocker));
        var rect = (RectTransform)go.transform;
        rect.SetParent(objectsRoot, false);
        // Right-aligned on the header line ("配置した部品  N件") above the list.
        rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one;
        rect.sizeDelta = new Vector2(230, 30);
        rect.anchoredPosition = new Vector2(0, 40);
        var background = go.GetComponent<SkillSyncRoundedGraphic>();
        background.color = Color.white; background.borderColor = new Color32(205, 213, 224, 255); background.radius = 8; background.borderWidth = 1;

        var area = new GameObject("Text Area", typeof(RectTransform), typeof(UnityEngine.UI.RectMask2D));
        var areaRect = (RectTransform)area.transform;
        areaRect.SetParent(rect, false);
        areaRect.anchorMin = Vector2.zero; areaRect.anchorMax = Vector2.one;
        areaRect.offsetMin = new Vector2(10, 2); areaRect.offsetMax = new Vector2(-10, -2);
        var text = Label("Text", areaRect, font, new Color32(23, 34, 54, 255), "");
        var placeholder = Label("Placeholder", areaRect, font, new Color32(120, 133, 150, 255), "部品名で探す");

        var input = go.GetComponent<TMP_InputField>();
        input.textViewport = areaRect; input.textComponent = text; input.placeholder = placeholder; input.targetGraphic = background;
        input.lineType = TMP_InputField.LineType.SingleLine; input.transition = UnityEngine.UI.Selectable.Transition.None;
        return input;
    }

    static TextMeshProUGUI Label(string name, RectTransform parent, TMP_FontAsset font, Color color, string value)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
        var label = go.GetComponent<TextMeshProUGUI>();
        if (font != null) label.font = font;
        label.fontSize = 13; label.color = color; label.text = value; label.alignment = TextAlignmentOptions.MidlineLeft;
        label.textWrappingMode = TextWrappingModes.NoWrap; label.overflowMode = TextOverflowModes.Ellipsis; label.raycastTarget = false;
        return label;
    }
}
