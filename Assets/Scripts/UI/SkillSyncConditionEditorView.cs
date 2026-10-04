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
        foreach (var definition in definitions)
        {
            string action = SkillSyncConditionEditing.TypeActionPrefix + definition.id;
            var control = view.controls.FirstOrDefault(c => c.action == action);
            if (control == null) continue;
            control.mask = 0;
            control.button.gameObject.SetActive(false);
        }
        EnsureTypeDropdown(view, parent, heading.label, X(1200));

        // Assign semantic roles to source-owned islands, including backgrounds and units.
        foreach (var visual in view.visuals)
        {
            bool Source(string id) => visual.sourceNodeIds?.Contains(id) == true;
            var rect = (RectTransform)visual.target.transform;
            if (Source("26:175")) { rect.sizeDelta = new Vector2(rect.sizeDelta.x, 379); }
            if (Source("26:176")) { visual.role = "conditionTargetALabel"; Place(visual, 483); }
            if (Source("26:177")) { visual.role = "conditionTargetABox"; Place(visual, 508); }
            if (visual.role == "objectA") CenterPickerText(visual, 508);
            if (Source("26:187")) Place(visual, 520);
            if (Source("26:188")) { visual.role = "conditionTargetBLabel"; Place(visual, 568); }
            if (Source("26:189")) { visual.role = "conditionTargetBBox"; Place(visual, 593); }
            if (visual.role == "objectB") CenterPickerText(visual, 593);
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
                visual.label.fontSize = 12;
                Place(visual, 747);
                rect.sizeDelta = new Vector2(360, 24);
                visual.label.textWrappingMode = TextWrappingModes.NoWrap;
                visual.label.overflowMode = TextOverflowModes.Ellipsis;
            }
            if (Source("26:600") || Source("26:1001") || Source("26:604")) visual.role = "conditionTrialHoldBox";
            if (visual.role == "conditionSummary")
            {
                rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, -783);
                rect.sizeDelta = new Vector2(360, 44);
                visual.label.textWrappingMode = TextWrappingModes.Normal;
                visual.label.fontSize = 14;
                visual.label.alignment = TextAlignmentOptions.TopLeft;
                visual.label.overflowMode = TextOverflowModes.Ellipsis;
            }
            if (Source("26:206")) rect.sizeDelta = new Vector2(rect.sizeDelta.x, 62);
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
        RefreshTypeDropdown(view, condition, editing);
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

    public static string TypeForDropdownIndex(TMP_Dropdown dropdown, int index)
    {
        int typeIndex = index - (dropdown.options.Count > ConditionTypeCatalog.Definitions.Count ? 1 : 0);
        return typeIndex >= 0 && typeIndex < ConditionTypeCatalog.Definitions.Count ?
            ConditionTypeCatalog.Definitions[typeIndex].id : null;
    }

    static void EnsureTypeDropdown(SkillSyncDesignView view, Transform parent, TMP_Text fontSource, float x)
    {
        var dropdown = view.conditionTypeDropdown;
        if (dropdown == null)
        {
            var existing = parent.Find("Dropdown_ConditionType");
            dropdown = existing != null ? existing.GetComponent<TMP_Dropdown>() : null;
        }
        if (dropdown == null)
        {
            var root = new GameObject("Dropdown_ConditionType", typeof(RectTransform), typeof(SkillSyncRoundedGraphic),
                typeof(TMP_Dropdown), typeof(EditorUiInputBlocker));
            root.transform.SetParent(parent, false);
            dropdown = root.GetComponent<TMP_Dropdown>();
            var background = root.GetComponent<SkillSyncRoundedGraphic>();
            background.color = Color.white;
            background.radius = 8;
            background.borderWidth = 1;
            background.borderColor = new Color32(205, 213, 224, 255);
            dropdown.targetGraphic = background;
            var caption = AddText("Caption", root.transform, fontSource, 16, TextAlignmentOptions.MidlineLeft);
            Stretch(caption.rectTransform, 16, 0, -36, 0);
            caption.overflowMode = TextOverflowModes.Ellipsis;
            var arrow = AddText("Arrow", root.transform, fontSource, 14, TextAlignmentOptions.Midline);
            arrow.text = "v";
            arrow.color = new Color32(88, 103, 124, 255);
            Stretch(arrow.rectTransform, 328, 0, -12, 0);

            var template = new GameObject("Template", typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.ScrollRect));
            template.transform.SetParent(root.transform, false);
            var templateRect = (RectTransform)template.transform;
            templateRect.anchorMin = new Vector2(0, 0);
            templateRect.anchorMax = new Vector2(1, 0);
            templateRect.pivot = new Vector2(.5f, 1);
            templateRect.offsetMin = new Vector2(0, -252);
            templateRect.offsetMax = Vector2.zero;
            template.GetComponent<UnityEngine.UI.Image>().color = Color.white;
            var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.Mask));
            viewport.transform.SetParent(template.transform, false);
            Stretch((RectTransform)viewport.transform, 0, 0, 0, 0);
            viewport.GetComponent<UnityEngine.UI.Image>().color = Color.white;
            viewport.GetComponent<UnityEngine.UI.Mask>().showMaskGraphic = false;
            var content = new GameObject("Content", typeof(RectTransform), typeof(UnityEngine.UI.VerticalLayoutGroup), typeof(UnityEngine.UI.ContentSizeFitter));
            content.transform.SetParent(viewport.transform, false);
            var contentRect = (RectTransform)content.transform;
            contentRect.anchorMin = contentRect.anchorMax = new Vector2(0, 1);
            contentRect.anchorMax = Vector2.one;
            contentRect.pivot = new Vector2(.5f, 1);
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.sizeDelta = Vector2.zero;
            var layout = content.GetComponent<UnityEngine.UI.VerticalLayoutGroup>();
            layout.childControlHeight = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;
            content.GetComponent<UnityEngine.UI.ContentSizeFitter>().verticalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
            var item = new GameObject("Item", typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.Toggle), typeof(UnityEngine.UI.LayoutElement));
            item.transform.SetParent(content.transform, false);
            var itemImage = item.GetComponent<UnityEngine.UI.Image>();
            itemImage.color = Color.white;
            item.GetComponent<UnityEngine.UI.Toggle>().targetGraphic = itemImage;
            var element = item.GetComponent<UnityEngine.UI.LayoutElement>();
            element.minHeight = element.preferredHeight = 36;
            var itemLabel = AddText("Item Label", item.transform, fontSource, 15, TextAlignmentOptions.MidlineLeft);
            Stretch(itemLabel.rectTransform, 16, 0, -16, 0);
            var scroll = template.GetComponent<UnityEngine.UI.ScrollRect>();
            scroll.viewport = (RectTransform)viewport.transform;
            scroll.content = contentRect;
            scroll.horizontal = false;
            dropdown.captionText = caption;
            dropdown.template = templateRect;
            dropdown.itemText = itemLabel;
            template.SetActive(false);
        }
        view.conditionTypeDropdown = dropdown;
        Bounds((RectTransform)dropdown.transform, x, 394, 360, 44);
        if (!view.visuals.Any(v => v.role == "conditionTypeDropdown"))
            view.visuals = view.visuals.Concat(new[] { new SkillSyncDesignView.Visual
                { target = dropdown.gameObject, role = "conditionTypeDropdown", mask = 1 << 1 } }).ToArray();
    }

    static void RefreshTypeDropdown(SkillSyncDesignView view, ConditionNodeData condition, bool editing)
    {
        var dropdown = view.conditionTypeDropdown;
        if (dropdown == null) return;
        var definition = ConditionTypeCatalog.Find(condition?.type);
        int selectedType = ConditionTypeCatalog.Definitions.ToList().FindIndex(d => d.id == condition?.type);
        var labels = ConditionTypeCatalog.Definitions.Select(d => d.label).ToList();
        if (selectedType < 0)
            labels.Insert(0, condition == null ? "種類を選択してください" :
                definition?.label ?? "未対応の条件：" + condition.type);
        if (dropdown.options.Count != labels.Count || !dropdown.options.Select(o => o.text).SequenceEqual(labels))
        {
            dropdown.ClearOptions();
            dropdown.AddOptions(labels);
            if (dropdown.template != null)
            {
                var offset = dropdown.template.offsetMin;
                dropdown.template.offsetMin = new Vector2(offset.x, -36 * labels.Count);
            }
        }
        dropdown.SetValueWithoutNotify(Mathf.Max(0, selectedType));
        dropdown.RefreshShownValue();
        dropdown.interactable = editing && condition != null;
    }

    static TextMeshProUGUI AddText(string name, Transform parent, TMP_Text source, float size, TextAlignmentOptions alignment)
    {
        var text = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
        text.transform.SetParent(parent, false);
        text.font = source.font;
        text.fontSharedMaterial = source.fontSharedMaterial;
        text.fontSize = size;
        text.color = new Color32(23, 34, 54, 255);
        text.alignment = alignment;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.raycastTarget = false;
        return text;
    }

    static void Stretch(RectTransform rect, float left, float bottom, float right, float top)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(left, bottom);
        rect.offsetMax = new Vector2(right, top);
    }

    static void CenterPickerText(SkillSyncDesignView.Visual visual, float y)
    {
        var rect = (RectTransform)visual.target.transform;
        rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, -y);
        rect.sizeDelta = new Vector2(rect.sizeDelta.x, 48);
        visual.label.alignment = TextAlignmentOptions.MidlineLeft;
        visual.label.textWrappingMode = TextWrappingModes.NoWrap;
        visual.label.overflowMode = TextOverflowModes.Ellipsis;
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
