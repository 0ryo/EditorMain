using System;
using System.Linq;
using TMPro;
using UnityEngine;

// Shared by the Prefab generator/updater and the existing Prefab compatibility path.
public static class SkillSyncConditionEditorView
{
    const float CardTop = 394, CardWidth = 392, ContentWidth = 360;
    const float Padding = 16, RowGap = 12, LabelHeight = 20, LabelGap = 8;

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

        // Source IDs are resolved once. Layout thereafter uses roles and the active definition.
        foreach (var visual in view.visuals)
        {
            bool Source(string id) => visual.sourceNodeIds?.Contains(id) == true;
            if (Source("26:175") || Source("26:1869")) visual.role = "conditionEditorCard";
            if (Source("26:176")) visual.role = "conditionTargetALabel";
            if (Source("26:177")) visual.role = "conditionTargetABox";
            if (Source("26:187")) visual.role = "conditionTargetAArrow";
            if (Source("26:188")) visual.role = "conditionTargetBLabel";
            if (Source("26:189") || Source("26:1883")) visual.role = "conditionTargetBBox";
            if (Source("26:194")) visual.role = "conditionTargetBArrow";
            if (Source("26:196")) visual.role = "conditionParameter0Label";
            if (Source("26:197")) visual.role = "conditionParameter0Box";
            if (Source("26:199")) visual.role = "conditionParameter0Unit";
            if (Source("26:201")) visual.role = "conditionParameter1Label";
            if (Source("26:202")) visual.role = "conditionParameter1Box";
            if (Source("26:204")) visual.role = "conditionParameter1Unit";
            if (Source("26:205") || Source("26:1896")) visual.role = "conditionEditorNote";
            if (Source("26:206") || Source("26:1897")) visual.role = "conditionSummaryBox";
            if (Source("26:209")) visual.role = "conditionAddBox";
            if (Source("26:210")) visual.role = "conditionAddLabel";
            if (Source("26:212") || Source("26:1903")) visual.role = "conditionTestBox";
            if (Source("26:213") || Source("26:1904")) visual.role = "conditionTestLabel";
            if (Source("26:600") || Source("26:1001") || Source("26:604")) visual.role = "conditionTrialHoldBox";
            if (visual.role == "conditionEditorCard") EnsureSurface(visual, Color.white, 1);
            if (visual.role == "conditionSummaryBox") EnsureSurface(visual, new Color32(233, 242, 255, 255), 0);
            if (visual.role == "inspectorLabel" && (visual.mask & 2) != 0)
            {
                Bounds(visual.label.rectTransform, X(1184), 108, CardWidth, 24);
                CenterText(visual.label, TextAlignmentOptions.MidlineLeft, false);
            }
            if (visual.role == "conditionEditorNote")
            {
                visual.label.fontSize = 12;
                CenterText(visual.label, TextAlignmentOptions.MidlineLeft, true);
            }
            if (visual.role == "conditionSummary")
            {
                visual.label.fontSize = 14;
                CenterText(visual.label, TextAlignmentOptions.MidlineLeft, true);
            }
            if (visual.role == "conditionAddLabel" || visual.role == "conditionTestLabel")
            {
                // Use one TMP label, including the icon, so its whole caption is centered.
                visual.label.text = visual.role == "conditionAddLabel" ? "＋ 完了条件を追加" : "▶ この条件を試す";
                visual.label.richText = false;
                foreach (Transform child in visual.label.transform) child.gameObject.SetActive(false);
                CenterText(visual.label, TextAlignmentOptions.Midline, false);
            }
        }
        foreach (var field in view.fields.Where(f => f.role == "distance" || f.role == "hold"))
        {
            field.input.contentType = TMP_InputField.ContentType.DecimalNumber;
            CenterText(field.input.textComponent, TextAlignmentOptions.MidlineLeft, false);
            Stretch(field.input.textViewport, 12, 0, -44, 0);
            Stretch(field.input.textComponent.rectTransform, 0, 0, 0, 0);
        }
        Layout(view, new ConditionNodeData { type = ConditionTypeCatalog.SnapHold });
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
            if (visual.role == "objectA" || visual.role?.StartsWith("conditionTargetA", StringComparison.Ordinal) == true)
                visual.target.SetActive(inspector && condition != null && (visual.mask & (1 << view.State)) != 0);
            if (visual.role == "objectB" || visual.role?.StartsWith("conditionTargetB", StringComparison.Ordinal) == true)
                visual.target.SetActive(inspector && condition != null && requiresB && (visual.mask & (1 << view.State)) != 0);
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
        view.SetVisible("PickA", condition != null);
        view.SetVisible("PickB", condition != null && requiresB);
        view.Enable("PickA", condition != null); view.Enable("PickB", requiresB);
        if (view.objectAThumbnail != null) view.objectAThumbnail.gameObject.SetActive(inspector && condition != null);
        if (view.objectBThumbnail != null) view.objectBThumbnail.gameObject.SetActive(inspector && condition != null && requiresB);
        view.holdProgress.gameObject.SetActive(holdTrial && (view.State == 2 || view.State == 4));
        var definition = ConditionTypeCatalog.Find(condition?.type);
        string label = definition?.label ?? condition?.type;
        view.Text("conditionEditorNote", condition == null ? "種類を選び、対象を設定してください" : definition == null ? "未対応の条件です。種類を確認してください" : !SkillSyncConditionEditing.SupportsPcTrial(condition) ?
            label + "：保存・出力に対応（PC試行は未対応）" : !canTrial ? "PC試行には全条件の種類・対象を設定してください" : "PC試行：中心どうしの距離で判定します");
        if (inspector) Layout(view, condition);
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
        var visual = view.visuals.FirstOrDefault(v => v.role == "conditionTypeDropdown");
        if (visual == null)
        {
            visual = new SkillSyncDesignView.Visual { target = dropdown.gameObject, role = "conditionTypeDropdown" };
            view.visuals = view.visuals.Concat(new[] { visual }).ToArray();
        }
        visual.mask = 2 | 32;
        // A generated control must be above the source-owned card in every Show() state.
        visual.orders = Enumerable.Repeat(1001, 6).ToArray();
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

    static void Layout(SkillSyncDesignView view, ConditionNodeData condition)
    {
        float X(float x) => view.wideLayout ? SkillSyncDesignLayout.MapX(x) : x;
        void PlaceRole(string role, float x, float y, float width, float height)
        {
            foreach (var visual in view.visuals.Where(v => v.role == role))
            {
                var rect = (RectTransform)visual.target.transform;
                // Shared refinements can parent captions to buttons/inputs. Coordinates
                // belong to the inspector, so convert them instead of applying them twice.
                var parent = (RectTransform)rect.parent;
                var relative = Matrix4x4.identity;
                for (var current = rect.parent; current != view.conditionTypeDropdown.transform.parent; current = current.parent)
                    relative = Matrix4x4.TRS(current.localPosition, current.localRotation, current.localScale) * relative;
                // Use the local hierarchy, since a saved overlay Canvas can have scale 0.
                var point = relative.inverse.MultiplyPoint3x4(new Vector3(X(x), -y, 0));
                Bounds(rect, point.x - parent.rect.xMin, parent.rect.yMax - point.y, width, height);
            }
        }
        void PlaceControl(string action, float x, float y, float width, float height)
        {
            foreach (var control in view.controls.Where(c => c.action == action))
                Bounds((RectTransform)control.button.transform, X(x), y, width, height);
        }
        float cursor = CardTop + Padding;
        Bounds((RectTransform)view.conditionTypeDropdown.transform, X(1200), cursor, ContentWidth, 44);
        cursor += 44;
        void TargetRow(string target, UnityEngine.UI.RawImage thumbnail)
        {
            cursor += RowGap;
            PlaceRole("conditionTarget" + target + "Label", 1200, cursor, ContentWidth, LabelHeight);
            cursor += LabelHeight + LabelGap;
            PlaceControl("Pick" + target, 1200, cursor, ContentWidth, 48);
            PlaceRole("conditionTarget" + target + "Box", 1200, cursor, ContentWidth, 48);
            bool hasThumbnail = thumbnail != null && thumbnail.enabled;
            PlaceRole("object" + target, hasThumbnail ? 1276 : 1214, cursor, hasThumbnail ? 244 : 308, 48);
            PlaceRole("conditionTarget" + target + "Arrow", 1530, cursor, 20, 48);
            foreach (var visual in view.visuals.Where(v => v.role == "object" + target || v.role == "conditionTarget" + target + "Label" || v.role == "conditionTarget" + target + "Arrow"))
            {
                CenterText(visual.label, TextAlignmentOptions.MidlineLeft, false);
                if (visual.role.EndsWith("Arrow", StringComparison.Ordinal))
                    foreach (RectTransform glyph in visual.label.transform)
                    {
                        glyph.anchorMin = glyph.anchorMax = glyph.pivot = new Vector2(.5f, .5f);
                        glyph.anchoredPosition = Vector2.zero;
                    }
            }
            if (thumbnail != null) Bounds(thumbnail.rectTransform, X(1212), cursor + 7, 50, 34);
            cursor += 48;
        }
        if (condition != null)
        {
            TargetRow("A", view.objectAThumbnail);
            if (ConditionTypeCatalog.RequiresObjectB(condition.type)) TargetRow("B", view.objectBThumbnail);
        }
        if (SkillSyncConditionEditing.Parameter(condition, 0) != null)
        {
            cursor += RowGap;
            for (int slot = 0; slot < 2; slot++)
            {
                float x = slot == 0 ? 1200 : 1390;
                var input = view.fields.FirstOrDefault(f => f.role == (slot == 0 ? "distance" : "hold"))?.input;
                if (input != null) Bounds((RectTransform)input.transform, X(x), cursor + LabelHeight + LabelGap, 170, 46);
                PlaceRole("conditionParameter" + slot + "Label", x, cursor, 170, LabelHeight);
                PlaceRole("conditionParameter" + slot + "Box", x, cursor + LabelHeight + LabelGap, 170, 46);
                PlaceRole("conditionParameter" + slot + "Unit", x + 128, cursor + LabelHeight + LabelGap, 30, 46);
                foreach (var visual in view.visuals.Where(v => v.role == "conditionParameter" + slot + "Label" || v.role == "conditionParameter" + slot + "Unit"))
                    CenterText(visual.label, TextAlignmentOptions.MidlineLeft, false);
            }
            cursor += LabelHeight + LabelGap + 46;
        }
        float PreferredHeight(string role, float minimum) => view.visuals.Where(v => v.role == role && v.label != null)
            .Select(v => Mathf.Ceil(v.label.GetPreferredValues(v.label.text, ContentWidth, Mathf.Infinity).y)).DefaultIfEmpty(minimum).Max();
        cursor += RowGap;
        float noteHeight = Mathf.Max(LabelHeight, PreferredHeight("conditionEditorNote", LabelHeight));
        PlaceRole("conditionEditorNote", 1200, cursor, ContentWidth, noteHeight);
        cursor += noteHeight + Padding;
        PlaceRole("conditionEditorCard", 1184, CardTop, CardWidth, cursor - CardTop);
        cursor += Padding;
        float summaryHeight = Mathf.Max(54, PreferredHeight("conditionSummary", LabelHeight) + 24);
        PlaceRole("conditionSummaryBox", 1184, cursor, CardWidth, summaryHeight);
        PlaceRole("conditionSummary", 1200, cursor + 12, ContentWidth, summaryHeight - 24);
        cursor += summaryHeight + Padding;
        PlaceControl("＋ 完了条件を追加", 1184, cursor, CardWidth, 44);
        PlaceRole("conditionAddBox", 1184, cursor, CardWidth, 44);
        PlaceRole("conditionAddLabel", 1184, cursor, CardWidth, 44);
        cursor += 44 + Padding;
        PlaceControl("▶ この条件を試す", 1184, cursor, CardWidth, 44);
        PlaceRole("conditionTestBox", 1184, cursor, CardWidth, 44);
        PlaceRole("conditionTestLabel", 1184, cursor, CardWidth, 44);
    }

    static void CenterText(TMP_Text text, TextAlignmentOptions alignment, bool wrap)
    {
        if (text == null) return;
        text.alignment = alignment;
        text.margin = Vector4.zero;
        text.textWrappingMode = wrap ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.raycastTarget = false;
    }

    static void EnsureSurface(SkillSyncDesignView.Visual visual, Color color, float borderWidth)
    {
        var original = visual.target.GetComponent<UnityEngine.UI.Image>();
        if (original != null) original.enabled = false;
        var child = visual.target.transform.Find("ConditionSurface");
        if (child == null)
        {
            child = new GameObject("ConditionSurface", typeof(RectTransform), typeof(SkillSyncRoundedGraphic)).transform;
            child.SetParent(visual.target.transform, false);
        }
        Stretch((RectTransform)child, 0, 0, 0, 0);
        var surface = child.GetComponent<SkillSyncRoundedGraphic>();
        surface.color = color;
        surface.borderWidth = borderWidth;
        surface.borderColor = new Color32(205, 213, 224, 255);
        surface.radius = 10;
        surface.raycastTarget = false;
    }

    static void Bounds(RectTransform rect, float x, float y, float width, float height)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = new Vector2(x, -y);
        rect.sizeDelta = new Vector2(width, height);
    }
}
