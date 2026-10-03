using System;
using System.Linq;
using TMPro;
using UnityEngine;

// Shared by the Prefab generator/updater and the existing Prefab compatibility path.
public static class SkillSyncConditionEditorView
{
    public const float ObjectAPickerY = 556, ObjectBPickerY = 641;

    public static void Ensure(SkillSyncDesignView view)
    {
        var heading = view.visuals?.FirstOrDefault(v => v.role == "conditionHeading" && v.label != null);
        if (heading == null || view.controls == null || view.fields == null) return;
        var parent = heading.target.transform.parent;
        float X(float x) => view.wideLayout ? SkillSyncDesignLayout.MapX(x) : x;
        var definitions = ConditionTypeCatalog.Definitions;
        for (int i = 0; i < definitions.Count; i++)
        {
            var definition = definitions[i];
            string action = SkillSyncConditionEditing.TypeActionPrefix + definition.id;
            var control = view.controls.FirstOrDefault(c => c.action == action);
            if (control == null)
            {
                var go = new GameObject("Button_ConditionType_" + definition.id, typeof(RectTransform), typeof(UnityEngine.UI.Image),
                    typeof(UnityEngine.UI.Button), typeof(EditorUiInputBlocker));
                go.transform.SetParent(parent, false);
                var image = go.GetComponent<UnityEngine.UI.Image>();
                image.color = Color.white; image.raycastTarget = true;
                var button = go.GetComponent<UnityEngine.UI.Button>();
                button.targetGraphic = image;
                var label = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
                label.transform.SetParent(go.transform, false);
                label.font = heading.label.font; label.fontSharedMaterial = heading.label.fontSharedMaterial;
                label.fontSize = 14; label.alignment = TextAlignmentOptions.Midline; label.raycastTarget = false;
                label.richText = false; label.textWrappingMode = TextWrappingModes.NoWrap;
                label.rectTransform.anchorMin = Vector2.zero; label.rectTransform.anchorMax = Vector2.one;
                label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;
                control = new SkillSyncDesignView.Control { action = action, button = button, mask = 2 };
                view.controls = view.controls.Concat(new[] { control }).ToArray();
            }
            Bounds((RectTransform)control.button.transform, X(1200 + i % 3 * 122), 394 + i / 3 * 44, 116, 40);
            control.button.GetComponentInChildren<TMP_Text>(true).text = definition.label;
            control.mask = 2;
        }

        // Assign semantic roles to source-owned islands, including backgrounds and units.
        foreach (var visual in view.visuals)
        {
            bool Source(string id) => visual.sourceNodeIds?.Contains(id) == true;
            var rect = (RectTransform)visual.target.transform;
            if (Source("26:175")) { rect.sizeDelta = new Vector2(rect.sizeDelta.x, 379); }
            if (Source("26:176")) { visual.role = "conditionTargetALabel"; Place(visual, 483); }
            if (Source("26:177")) { visual.role = "conditionTargetABox"; Place(visual, 508); }
            if (visual.role == "objectA") Place(visual, 519);
            if (Source("26:187")) Place(visual, 520);
            if (Source("26:188")) { visual.role = "conditionTargetBLabel"; Place(visual, 568); }
            if (Source("26:189")) { visual.role = "conditionTargetBBox"; Place(visual, 593); }
            if (visual.role == "objectB") Place(visual, 605);
            if (Source("26:194")) { visual.role = "conditionTargetBArrow"; Place(visual, 606); }
            if (Source("26:196")) { visual.role = "conditionParameter0Label"; Place(visual, 659); }
            if (Source("26:197")) { visual.role = "conditionParameter0Box"; Place(visual, 686); }
            if (Source("26:199")) visual.role = "conditionParameter0Unit";
            if (Source("26:201")) { visual.role = "conditionParameter1Label"; Place(visual, 659); }
            if (Source("26:202")) { visual.role = "conditionParameter1Box"; Place(visual, 686); }
            if (Source("26:204")) visual.role = "conditionParameter1Unit";
            if (Source("26:205"))
            {
                visual.role = "conditionEditorNote";
                Place(visual, 747);
                rect.sizeDelta = new Vector2(360, 24);
            }
            if (Source("26:600") || Source("26:1001") || Source("26:604")) visual.role = "conditionTrialHoldBox";
            if (visual.role == "conditionSummary")
            {
                rect.sizeDelta = new Vector2(360, 36);
                visual.label.textWrappingMode = TextWrappingModes.Normal;
                visual.label.fontSize = 14;
            }
        }
        foreach (var field in view.fields.Where(f => f.role == "distance" || f.role == "hold"))
        {
            var rect = (RectTransform)field.input.transform;
            rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, -686);
            field.input.contentType = TMP_InputField.ContentType.DecimalNumber;
        }
        foreach (var control in view.controls.Where(c => c.action == "PickA" || c.action == "PickB"))
        {
            var rect = (RectTransform)control.button.transform;
            rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, control.action == "PickA" ? -508 : -593);
        }
        if (view.objectAThumbnail != null) SetY(view.objectAThumbnail.rectTransform, 516);
        if (view.objectBThumbnail != null) SetY(view.objectBThumbnail.rectTransform, 601);
    }

    public static void Refresh(SkillSyncDesignView view, ConditionNodeData condition, bool canTrial)
    {
        bool requiresB = condition != null && ConditionTypeCatalog.RequiresObjectB(condition.type);
        bool editing = view.State == 1, inspector = editing || view.State == 5;
        bool holdTrial = condition?.type == ConditionTypeCatalog.SnapHold;
        foreach (var control in view.controls.Where(c => c.action.StartsWith(SkillSyncConditionEditing.TypeActionPrefix, StringComparison.Ordinal)))
        {
            string type = control.action.Substring(SkillSyncConditionEditing.TypeActionPrefix.Length);
            var image = control.button.GetComponent<UnityEngine.UI.Image>();
            image.color = condition?.type == type ? new Color32(8, 102, 232, 255) : new Color32(238, 242, 247, 255);
            control.button.GetComponentInChildren<TMP_Text>(true).color = condition?.type == type ? Color.white : new Color32(23, 34, 54, 255);
            control.button.interactable = editing && condition != null;
        }
        foreach (var visual in view.visuals)
        {
            if (visual.role == "holdStatus" || visual.role == "holdValue" || visual.role == "holdLimit" || visual.role == "conditionTrialHoldBox")
                visual.target.SetActive(holdTrial && (visual.mask & (1 << view.State)) != 0);
            if (visual.role == "objectB" || visual.role?.StartsWith("conditionTargetB", StringComparison.Ordinal) == true)
                visual.target.SetActive(inspector && requiresB && (visual.mask & (1 << view.State)) != 0);
            if (visual.role == "conditionTargetALabel") visual.label.text = requiresB ? "動かすもの" : "対象";
            if (visual.role == "conditionTargetBLabel") visual.label.text = condition?.type == ConditionTypeCatalog.RotationMatch ? "向きの基準" :
                condition?.type == ConditionTypeCatalog.Separation ? "離す基準" : "近づける先";
            for (int slot = 0; slot < 2; slot++)
            {
                if (visual.role?.StartsWith("conditionParameter" + slot, StringComparison.Ordinal) != true) continue;
                var parameter = SkillSyncConditionEditing.Parameter(condition, slot);
                visual.target.SetActive(inspector && parameter != null && (visual.mask & (1 << view.State)) != 0);
                if (parameter == null || visual.label == null) continue;
                if (visual.role.EndsWith("Label", StringComparison.Ordinal))
                {
                    visual.label.text = parameter.label.Split(' ')[0];
                    ((RectTransform)visual.target.transform).sizeDelta = new Vector2(170, 24);
                }
                else if (visual.role.EndsWith("Unit", StringComparison.Ordinal))
                    visual.label.text = parameter.key == ConditionTypeCatalog.DistanceKey ? "cm" : parameter.key == ConditionTypeCatalog.AngleKey ? "度" : "秒";
            }
        }
        foreach (var field in view.fields.Where(f => f.role == "distance" || f.role == "hold"))
        {
            bool active = inspector && SkillSyncConditionEditing.Parameter(condition, field.role == "distance" ? 0 : 1) != null;
            field.input.gameObject.SetActive(active);
            field.input.interactable = editing && condition != null;
        }
        view.SetVisible("PickB", requiresB);
        view.Enable("PickA", condition != null); view.Enable("PickB", requiresB);
        if (view.objectBThumbnail != null) view.objectBThumbnail.gameObject.SetActive(editing && requiresB);
        view.holdProgress.gameObject.SetActive(holdTrial && (view.State == 2 || view.State == 4));
        var definition = ConditionTypeCatalog.Find(condition?.type);
        string label = definition?.label ?? condition?.type;
        view.Text("conditionEditorNote", condition == null ? "種類を選び、対象を設定してください" : definition == null ? "未対応の条件です。種類を確認してください" : !SkillSyncConditionEditing.SupportsPcTrial(condition) ?
            label + "：保存・出力に対応（PC試行は未対応）" : !canTrial ? "PC試行には手順内の全条件の種類・対象を設定してください" : "PC試行：中心どうしの距離で判定します");
    }

    static void Bounds(RectTransform rect, float x, float y, float width, float height)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = new Vector2(x, -y); rect.sizeDelta = new Vector2(width, height);
    }
    static void SetY(RectTransform rect, float y) => rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, -y);
    static void Place(SkillSyncDesignView.Visual visual, float y)
    {
        var rect = (RectTransform)visual.target.transform;
        if (visual.label == null) SetY(rect, y);
        else
        {
            // Keep the existing baseline offset while placing text by its visible top.
            SetY(rect, y + visual.label.fontSize - 20);
        }
    }
}
