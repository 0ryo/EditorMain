using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

// Shared by the Prefab generator/updater and the existing Prefab compatibility path.
public static class SkillSyncConditionEditorView
{
    const float CardTop = 424, CardWidth = 392, ContentWidth = 360;
    const float Padding = 16, RowGap = 12, LabelHeight = 20, LabelGap = 8, ListPadding = 8, BottomLimit = 984;
    // One glyph, size and weight for every dropdown/picker arrow.
    const string ArrowGlyph = "v";
    const float ArrowSize = 14;
    static readonly Color32 ArrowColor = new Color32(88, 103, 124, 255);
    public const string RuleLine = "すべての条件がそろうと次の手順へ進みます（順不同）";
    public const string RuleText = RuleLine + "\n順番が必要なときは、手順を分けてください";
    public const string DirectionActionPrefix = "TurnDirection:";
    static readonly Color32 Blue = new Color32(8, 102, 232, 255), LineColor = new Color32(205, 213, 224, 255);

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
        // All conditions of a step are listed below the editor card; the old previous/next pager is retired.
        foreach (var control in view.controls.Where(c => c.action == "PreviousCondition" || c.action == "NextCondition"))
        {
            control.mask = 0;
            control.button.gameObject.SetActive(false);
        }
        EnsureConditionList(view, parent, heading.label);
        EnsureTrialList(view, parent, heading.label);
        EnsureLabel(view, parent, heading.label, "conditionRule", 12, TextAlignmentOptions.TopLeft, 2 | 32, true).text = RuleText;
        Bounds(EnsureLabel(view, parent, heading.label, "trialRule", 12, TextAlignmentOptions.TopLeft, 4 | 16, true).rectTransform, X(1184), 276, CardWidth, 30);
        for (int slot = 0; slot < 2; slot++)
            EnsureLabel(view, parent, heading.label, "conditionParameter" + slot + "Hint", 11, TextAlignmentOptions.MidlineRight, 2 | 32, false);
        EnsureLabel(view, parent, heading.label, "conditionChoiceLabel", 14, TextAlignmentOptions.MidlineLeft, 2 | 32, false);
        EnsureDirectionButtons(view, parent, heading.label);

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
            // Trial screen: the step's text is already in the learner panel below, the "動作確認中" pill repeats the
            // status strip and the header, and the captions under "最初から試す" repeat the rule line and the footer.
            if (visual.role == "trialBody" || visual.role == "trialBody2" || Source("26:586") || Source("26:587") || Source("26:987") || Source("26:988") ||
                visual.label != null && (visual.label.text == "編集中の内容は保持されます" || visual.label.text == "終了すると元の配置に戻ります"))
            { visual.mask = 0; visual.target.SetActive(false); }
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
                visual.label.richText = true;
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
            // Standard, not DecimalNumber: the decimal filter silently drops full-width digits typed with the IME on.
            field.input.contentType = TMP_InputField.ContentType.Standard;
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
        // State 1 without a condition (no step): show only the summary text.
        bool hideEmpty = editing && condition == null;
        foreach (var visual in view.visuals)
        {
            if (editing && visual.target != null && (visual.role == "conditionCounter" || visual.role == "conditionTestBox" || visual.role == "conditionTestLabel" ||
                visual.role == "conditionEditorCard" || visual.role == "conditionEditorNote"))
                visual.target.SetActive(!hideEmpty && (visual.mask & (1 << view.State)) != 0);
            if (visual.role == "holdStatus" || visual.role == "holdValue" || visual.role == "holdLimit" || visual.role == "conditionTrialHoldBox")
                visual.target.SetActive(holdTrial && (visual.mask & (1 << view.State)) != 0);
            if (visual.role == "objectA" || visual.role?.StartsWith("conditionTargetA", StringComparison.Ordinal) == true)
                visual.target.SetActive(inspector && condition != null && (visual.mask & (1 << view.State)) != 0);
            if (visual.role == "objectB" || visual.role?.StartsWith("conditionTargetB", StringComparison.Ordinal) == true)
                visual.target.SetActive(inspector && condition != null && requiresB && (visual.mask & (1 << view.State)) != 0);
            if (visual.role == "conditionTargetALabel") visual.label.text = SkillSyncConditionEditing.TargetLabelA(condition?.type);
            if (visual.role == "conditionTargetBLabel") visual.label.text = SkillSyncConditionEditing.TargetLabelB(condition?.type);
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
            if (field.input.placeholder is TMP_Text hint) hint.text = SkillSyncConditionEditing.RangeHint(condition, field.role == "distance" ? 0 : 1);
        }
        foreach (var visual in view.visuals)
        {
            if (visual.role == "conditionRule") visual.target.SetActive(inspector && condition != null && (visual.mask & (1 << view.State)) != 0);
            for (int slot = 0; slot < 2; slot++)
            {
                if (visual.role != "conditionParameter" + slot + "Hint") continue;
                var parameter = SkillSyncConditionEditing.Parameter(condition, slot);
                visual.target.SetActive(inspector && parameter != null && (visual.mask & (1 << view.State)) != 0);
                if (parameter != null) visual.label.text = SkillSyncConditionEditing.RangeHint(condition, slot).Replace(" ", "");
            }
            if (visual.role == "conditionChoiceLabel")
            {
                var choice = SkillSyncConditionEditing.ChoiceParameter(condition);
                visual.target.SetActive(inspector && choice != null && (visual.mask & (1 << view.State)) != 0);
                if (choice != null) visual.label.text = choice.label;
            }
        }
        RefreshDirectionButtons(view, condition, editing);
        view.SetVisible("▶ この条件を試す", !hideEmpty);
        view.SetVisible("PickA", condition != null);
        view.SetVisible("PickB", condition != null && requiresB);
        view.Enable("PickA", condition != null); view.Enable("PickB", requiresB);
        if (view.objectAThumbnail != null) view.objectAThumbnail.gameObject.SetActive(inspector && condition != null);
        if (view.objectBThumbnail != null) view.objectBThumbnail.gameObject.SetActive(inspector && condition != null && requiresB);
        view.holdProgress.gameObject.SetActive(holdTrial && (view.State == 2 || view.State == 4));
        var definition = ConditionTypeCatalog.Find(condition?.type);
        view.Text("conditionEditorNote", NoteText(condition, definition));
        if (view.conditionList != null)
            view.conditionList.gameObject.SetActive((editing || view.State == 5) && condition != null && view.conditionList.Count > 1);
        if (inspector) Layout(view, condition);
    }

    static string NoteText(ConditionNodeData condition, ConditionTypeCatalog.Definition definition)
    {
        if (condition == null) return "種類を選び、対象を設定してください";
        if (definition == null) return "未対応の条件です。種類を確認してください";
        return SkillSyncConditionEditing.Criteria(condition);
    }

    // A caption-style TMP visual owned by the code (no Prefab change), shown by mask like the generated lists.
    static TMP_Text EnsureLabel(SkillSyncDesignView view, Transform parent, TMP_Text font, string role, float size, TextAlignmentOptions alignment, int mask, bool wrap)
    {
        var visual = view.visuals.FirstOrDefault(v => v.role == role);
        if (visual == null || visual.label == null)
        {
            var existing = parent.Find("Label_" + role);
            var text = existing != null ? existing.GetComponent<TextMeshProUGUI>() : AddText("Label_" + role, parent, font, size, alignment);
            text.color = new Color32(88, 103, 124, 255);
            text.richText = false;
            text.textWrappingMode = wrap ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Ellipsis;
            text.lineSpacing = 0;
            Bounds(text.rectTransform, 0, 0, 100, 20);
            if (visual == null)
            {
                visual = new SkillSyncDesignView.Visual { role = role };
                view.visuals = view.visuals.Concat(new[] { visual }).ToArray();
            }
            visual.target = text.gameObject;
            visual.label = text;
        }
        visual.mask = mask;
        visual.orders = Enumerable.Repeat(1001, 6).ToArray();
        return visual.label;
    }

    // Segmented control for the turn direction; the labels come from the parameter definition.
    static void EnsureDirectionButtons(SkillSyncDesignView view, Transform parent, TMP_Text font)
    {
        var choices = ConditionTypeCatalog.Find(ConditionTypeCatalog.Turn).parameters.First(p => p.choices != null).choices;
        for (int i = 0; i < choices.Length; i++)
        {
            string action = DirectionActionPrefix + i;
            var control = view.controls.FirstOrDefault(c => c.action == action);
            if (control == null)
            {
                var go = new GameObject("Button_" + action, typeof(RectTransform), typeof(SkillSyncRoundedGraphic), typeof(UnityEngine.UI.Button), typeof(EditorUiInputBlocker));
                go.transform.SetParent(parent, false);
                var graphic = go.GetComponent<SkillSyncRoundedGraphic>();
                graphic.radius = 8; graphic.borderWidth = 1;
                go.GetComponent<UnityEngine.UI.Button>().targetGraphic = graphic;
                var caption = AddText("Label", go.transform, font, 14, TextAlignmentOptions.Midline);
                Stretch(caption.rectTransform, 0, 0, 0, 0);
                caption.fontWeight = FontWeight.Medium;
                caption.text = choices[i];
                control = new SkillSyncDesignView.Control { button = go.GetComponent<UnityEngine.UI.Button>(), action = action };
                view.controls = view.controls.Concat(new[] { control }).ToArray();
            }
            control.mask = 2 | 32;
        }
    }

    static void RefreshDirectionButtons(SkillSyncDesignView view, ConditionNodeData condition, bool editing)
    {
        var choice = SkillSyncConditionEditing.ChoiceParameter(condition);
        for (int i = 0; ; i++)
        {
            var control = view.controls.FirstOrDefault(c => c.action == DirectionActionPrefix + i);
            if (control == null) break;
            view.SetVisible(control.action, choice != null && (view.State == 1 || view.State == 5));
            control.button.interactable = editing && choice != null;
            bool selected = choice != null && SkillSyncConditionEditing.Choice(condition) == i;
            var graphic = control.button.GetComponent<SkillSyncRoundedGraphic>();
            graphic.color = selected ? Blue : Color.white;
            graphic.borderColor = selected ? Blue : LineColor;
            graphic.SetAllDirty();
            var caption = control.button.GetComponentInChildren<TMP_Text>(true);
            if (caption != null) caption.color = selected ? Color.white : new Color32(23, 34, 54, 255);
        }
    }

    static void EnsureConditionList(SkillSyncDesignView view, Transform parent, TMP_Text fontSource)
    {
        if (view.conditionList == null)
        {
            var existing = parent.Find("ConditionList");
            view.conditionList = existing != null ? existing.GetComponent<SkillSyncItemList>() : null;
        }
        if (view.conditionList == null) view.conditionList = SkillSyncItemList.Create(parent, "ConditionList", fontSource);
        var visual = view.visuals.FirstOrDefault(v => v.role == "conditionList");
        if (visual == null)
        {
            visual = new SkillSyncDesignView.Visual { target = view.conditionList.gameObject, role = "conditionList" };
            view.visuals = view.visuals.Concat(new[] { visual }).ToArray();
        }
        visual.mask = 2 | 32;
        // Above the source-owned summary surface in every Show() state.
        visual.orders = Enumerable.Repeat(1001, 6).ToArray();
    }

    // Steps with nothing the PC can measure show no distance/hold gauges (instead of zeros).
    public static void HideTrialGauges(SkillSyncDesignView view)
    {
        string[] roles = { "distanceStatus", "distanceValue", "distanceLimit", "conditionTrialHoldBox", "holdStatus", "holdValue", "holdLimit", "trialNote", "trialNote2" };
        foreach (var visual in view.visuals)
            if (Array.IndexOf(roles, visual.role) >= 0 || visual.sourceNodeIds?.Contains("26:596") == true) visual.target.SetActive(false);
        view.holdProgress.gameObject.SetActive(false);
    }

    // The trial screen's right panel flows from the top by what is shown: the step pill, the heading, the rule,
    // the gauges that apply, the condition list, then the log only when it fits above "編集に戻る".
    // The design file placed every block for one fixed case, which left gaps or overlaps for the others.
    const float TrialBottom = 887, TrialGap = 16;
    static readonly Dictionary<RectTransform, Vector2> trialOrigin = new Dictionary<RectTransform, Vector2>();
    public static void LayoutTrial(SkillSyncDesignView view, bool gauge, bool holds, int rows)
    {
        var chrome = view.conditionTypeDropdown != null ? view.conditionTypeDropdown.transform.parent : null;
        if (chrome == null) return;
        bool Source(SkillSyncDesignView.Visual v, params string[] ids) => v.sourceNodeIds != null && ids.Any(id => v.sourceNodeIds.Contains(id));
        IEnumerable<SkillSyncDesignView.Visual> Shown(Func<SkillSyncDesignView.Visual, bool> match) =>
            view.visuals.Where(v => v.target != null && (v.mask & (1 << view.State)) != 0 && match(v));
        void Move(float designTop, float top, IEnumerable<RectTransform> rects)
        {
            foreach (var rect in rects) ShiftFromOrigin(rect, chrome, top - designTop);
        }

        // Pill: as wide as its text, the text centred in it.
        var pillText = Shown(v => v.role == "trialStep").FirstOrDefault()?.label;
        var pill = Shown(v => Source(v, "26:591")).FirstOrDefault();
        if (pillText != null && pill != null)
        {
            float width = Mathf.Ceil(pillText.GetPreferredValues(pillText.text).x) + 24;
            Place(view, (RectTransform)pill.target.transform, 1184, 116, width, 28);
            Place(view, pillText.rectTransform, 1184, 116, width, 28);
            CenterText(pillText, TextAlignmentOptions.Midline, false);
        }
        float cursor = 206;
        var rule = Shown(v => v.role == "trialRule").FirstOrDefault()?.label;
        if (rule != null)
        {
            rule.fontSize = 13; rule.lineSpacing = 4;
            float height = Mathf.Ceil(rule.GetPreferredValues(rule.text, CardWidth, Mathf.Infinity).y);
            Place(view, rule.rectTransform, 1184, cursor, CardWidth, height);
            cursor += height + TrialGap;
        }
        if (gauge)
        {
            Move(311, cursor, Shown(v => Source(v, "26:596", "26:997") || v.role == "distanceStatus" || v.role == "distanceValue" || v.role == "distanceLimit")
                .Select(v => (RectTransform)v.target.transform));
            cursor += 126 + 12;
            if (holds)
            {
                var hold = Shown(v => v.role == "conditionTrialHoldBox" || v.role == "holdStatus" || v.role == "holdValue" || v.role == "holdLimit")
                    .Select(v => (RectTransform)v.target.transform).ToList();
                if (view.holdProgress != null) hold.Add(view.holdProgress.rectTransform);
                Move(457, cursor, hold);
                cursor += 154 + 12;
            }
            Move(643, cursor, Shown(v => v.role == "trialNote" || v.role == "trialNote2").Select(v => (RectTransform)v.target.transform));
            cursor += 41 + TrialGap;
        }
        if (view.trialList != null && rows > 0)
        {
            float height = Mathf.Min(rows * (SkillSyncItemList.RowHeight + SkillSyncItemList.RowGap) - SkillSyncItemList.RowGap, TrialBottom - cursor);
            view.trialList.SetBounds(view.wideLayout ? SkillSyncDesignLayout.MapX(1184) : 1184, cursor, CardWidth, Mathf.Max(SkillSyncItemList.RowHeight, height));
            cursor += height + TrialGap + 8;
        }
        // The log (line, heading, two entries: 115 high in the design) follows the list, or gives way when it does not fit.
        var log = view.visuals.Where(v => v.target != null && (v.mask & (1 << view.State)) != 0 &&
            (Source(v, "26:608", "26:1009", "26:609", "26:1010") || v.role == "log1" || v.role == "log2")).ToList();
        bool fits = cursor + 115 <= TrialBottom;
        foreach (var v in log) if (v.target.activeSelf != fits) v.target.SetActive(fits);
        if (fits) Move(718, cursor, log.Select(v => (RectTransform)v.target.transform));
    }

    static void ShiftFromOrigin(RectTransform rect, Transform chrome, float designDelta)
    {
        if (rect == null) return;
        if (!trialOrigin.TryGetValue(rect, out var origin)) trialOrigin[rect] = origin = rect.anchoredPosition;
        // Child units per design unit: the inverse of the scales between the design space and this rect's parent.
        float scale = 1f;
        for (var t = rect.parent; t != null && t != chrome; t = t.parent) scale *= t.localScale.y;
        rect.anchoredPosition = new Vector2(origin.x, origin.y - designDelta / (Mathf.Abs(scale) < 1e-5f ? 1f : scale));
    }

    // Trial-only list of the step's conditions (manual success for those the PC cannot judge).
    static void EnsureTrialList(SkillSyncDesignView view, Transform parent, TMP_Text fontSource)
    {
        if (view.trialList == null)
        {
            var existing = parent.Find("TrialConditionList");
            view.trialList = existing != null ? existing.GetComponent<SkillSyncItemList>() : null;
        }
        if (view.trialList == null) view.trialList = SkillSyncItemList.Create(parent, "TrialConditionList", fontSource);
        var visual = view.visuals.FirstOrDefault(v => v.role == "trialConditionList");
        if (visual == null)
        {
            visual = new SkillSyncDesignView.Visual { target = view.trialList.gameObject, role = "trialConditionList" };
            view.visuals = view.visuals.Concat(new[] { visual }).ToArray();
        }
        visual.mask = 4 | 16;
        visual.orders = Enumerable.Repeat(1001, 6).ToArray();
    }

    public static string TypeForDropdownIndex(TMP_Dropdown dropdown, int index)
    {
        var choices = ConditionTypeCatalog.EditorChoices;
        int typeIndex = index - (dropdown.options.Count > choices.Count ? 1 : 0);
        return typeIndex >= 0 && typeIndex < choices.Count ? choices[typeIndex].id : null;
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
            StyleArrow(arrow);
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
        StyleDropdownList(dropdown);
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

    static void StyleArrow(TMP_Text arrow)
    {
        arrow.text = ArrowGlyph;
        arrow.fontSize = ArrowSize;
        arrow.fontWeight = FontWeight.Medium;
        arrow.color = ArrowColor;
        arrow.richText = false;
    }

    // Idempotent: rounded white list surface with border and shadow, above the parameter fields, full-width item highlight.
    static void StyleDropdownList(TMP_Dropdown dropdown)
    {
        var arrow = dropdown.transform.Find("Arrow");
        if (arrow != null && arrow.GetComponent<TMP_Text>() != null) StyleArrow(arrow.GetComponent<TMP_Text>());
        var template = dropdown.template;
        if (template == null) return;
        var image = template.GetComponent<UnityEngine.UI.Image>();
        if (image != null) image.color = Color.clear;
        var canvas = template.GetComponent<Canvas>();
        if (!canvas) canvas = template.gameObject.AddComponent<Canvas>();
        canvas.overrideSorting = true;
        canvas.sortingOrder = 30000;
        if (template.GetComponent<UnityEngine.UI.GraphicRaycaster>() == null) template.gameObject.AddComponent<UnityEngine.UI.GraphicRaycaster>();
        void Surface(string name, Color32 fill, Color32 border, float borderWidth, Vector2 offset, int index)
        {
            var child = template.Find(name);
            if (child == null)
            {
                child = new GameObject(name, typeof(RectTransform), typeof(SkillSyncRoundedGraphic)).transform;
                child.SetParent(template, false);
            }
            child.SetSiblingIndex(index);
            var rect = (RectTransform)child;
            Stretch(rect, offset.x, offset.y, offset.x, offset.y);
            var graphic = child.GetComponent<SkillSyncRoundedGraphic>();
            graphic.color = fill; graphic.borderColor = border; graphic.borderWidth = borderWidth; graphic.radius = 8; graphic.raycastTarget = false;
        }
        Surface("Shadow", new Color32(23, 34, 54, 40), new Color32(23, 34, 54, 0), 0, new Vector2(2, -4), 0);
        Surface("Surface", Color.white, new Color32(205, 213, 224, 255), 1, Vector2.zero, 1);
        var item = template.Find("Viewport/Content/Item") as RectTransform;
        if (item == null) return;
        item.anchorMin = new Vector2(0, 1); item.anchorMax = new Vector2(1, 1);
        // The list's layout sizes each row to the full width; otherwise rows keep a default 100 px width and the
        // highlight of the hovered or selected row stops partway across.
        var rows = item.parent.GetComponent<UnityEngine.UI.VerticalLayoutGroup>();
        if (rows != null) { rows.childControlWidth = true; rows.childForceExpandWidth = true; rows.padding = new RectOffset(0, 0, 4, 4); }
        item.sizeDelta = new Vector2(0, item.sizeDelta.y);
        var toggle = item.GetComponent<UnityEngine.UI.Toggle>();
        if (toggle == null) return;
        var colors = toggle.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = colors.selectedColor = new Color32(233, 242, 255, 255);
        colors.pressedColor = new Color32(214, 229, 252, 255);
        toggle.colors = colors;
    }

    static void RefreshTypeDropdown(SkillSyncDesignView view, ConditionNodeData condition, bool editing)
    {
        var dropdown = view.conditionTypeDropdown;
        if (dropdown == null) return;
        var definition = ConditionTypeCatalog.Find(condition?.type);
        var choices = ConditionTypeCatalog.EditorChoices;
        string shown = ConditionTypeCatalog.EditorChoiceOf(condition?.type);
        int selectedType = choices.ToList().FindIndex(d => d.id == shown);
        var labels = choices.Select(d => d.label).ToList();
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
                dropdown.template.offsetMin = new Vector2(offset.x, -36 * labels.Count - 8); // rows plus the list's 4 px top and bottom padding
            }
        }
        dropdown.SetValueWithoutNotify(Mathf.Max(0, selectedType));
        dropdown.RefreshShownValue();
        dropdown.interactable = editing && condition != null;
        // Without a condition (no step) the type selector is hidden; otherwise Show() masks decide.
        if (editing) dropdown.gameObject.SetActive(condition != null);
        else if (condition == null) dropdown.gameObject.SetActive(false);
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
                Place(view, (RectTransform)visual.target.transform, x, y, width, height);
        }
        void PlaceControl(string action, float x, float y, float width, float height)
        {
            foreach (var control in view.controls.Where(c => c.action == action))
                Bounds((RectTransform)control.button.transform, X(x), y, width, height);
        }
        PlaceRole("conditionRule", 1184, CardTop - 38, CardWidth, 30);
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
                {
                    // Same glyph/size/weight as the type dropdown arrow; the source vector glyphs are hidden.
                    StyleArrow(visual.label);
                    CenterText(visual.label, TextAlignmentOptions.Midline, false);
                    foreach (Transform glyph in visual.label.transform) glyph.gameObject.SetActive(false);
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
                var labelVisual = view.visuals.FirstOrDefault(v => v.role == "conditionParameter" + slot + "Label" && v.label != null);
                float labelWidth = labelVisual != null ? Mathf.Ceil(labelVisual.label.GetPreferredValues(labelVisual.label.text).x) + 8 : 40;
                PlaceRole("conditionParameter" + slot + "Hint", x + labelWidth, cursor, 170 - labelWidth, LabelHeight);
                foreach (var visual in view.visuals.Where(v => v.role == "conditionParameter" + slot + "Label" || v.role == "conditionParameter" + slot + "Unit"))
                    CenterText(visual.label, TextAlignmentOptions.MidlineLeft, false);
            }
            cursor += LabelHeight + LabelGap + 46;
        }
        if (SkillSyncConditionEditing.ChoiceParameter(condition) != null)
        {
            cursor += RowGap;
            PlaceRole("conditionChoiceLabel", 1200, cursor, ContentWidth, LabelHeight);
            cursor += LabelHeight + LabelGap;
            float choiceWidth = (ContentWidth - 2 * 8) / 3;
            for (int i = 0; i < 3; i++)
                PlaceControl(DirectionActionPrefix + i, 1200 + i * (choiceWidth + 8), cursor, choiceWidth, 44);
            cursor += 44;
        }
        float PreferredHeight(string role, float minimum) => view.visuals.Where(v => v.role == role && v.label != null)
            .Select(v => Mathf.Ceil(v.label.GetPreferredValues(v.label.text, ContentWidth, Mathf.Infinity).y)).DefaultIfEmpty(minimum).Max();
        cursor += RowGap;
        float noteHeight = Mathf.Max(LabelHeight, PreferredHeight("conditionEditorNote", LabelHeight));
        PlaceRole("conditionEditorNote", 1200, cursor, ContentWidth, noteHeight);
        cursor += noteHeight + Padding;
        PlaceRole("conditionEditorCard", 1184, CardTop, CardWidth, cursor - CardTop);
        cursor += Padding;
        var list = view.conditionList;
        // With a single condition the editor above already says everything, so no summary card follows it.
        bool single = condition != null && list != null && list.Count == 1;
        bool useList = condition != null && list != null && list.Count > 1;
        foreach (var summary in view.visuals.Where(v => v.role == "conditionSummary" || v.role == "conditionSummaryBox"))
        {
            bool show = summary.role == "conditionSummaryBox" ? !single : !useList && !single;
            summary.target.SetActive(show && (summary.mask & (1 << view.State)) != 0);
        }
        if (useList)
        {
            // The list is the summary: one card per condition, shortened to the space the inspector has left.
            float available = BottomLimit - (44 + Padding) - cursor - 2 * ListPadding;
            // Whole rows only: a row cut in half at an edge reads as broken, and the list scrolls row by row.
            float step = SkillSyncItemList.RowHeight + SkillSyncItemList.RowGap;
            int rows = Mathf.Max(1, Mathf.FloorToInt((Mathf.Max(0, available) + SkillSyncItemList.RowGap) / step));
            float visible = Mathf.Min(list.ContentHeight, rows * step - SkillSyncItemList.RowGap);
            float boxHeight = visible + 2 * ListPadding;
            PlaceRole("conditionSummaryBox", 1184, cursor, CardWidth, boxHeight);
            list.SetBounds(X(1184) + ListPadding, cursor + ListPadding, CardWidth - 2 * ListPadding, visible);
            cursor += boxHeight + Padding;
        }
        else if (!single)
        {
            float summaryHeight = Mathf.Max(54, PreferredHeight("conditionSummary", LabelHeight) + 24);
            PlaceRole("conditionSummaryBox", 1184, cursor, CardWidth, summaryHeight);
            PlaceRole("conditionSummary", 1200, cursor + 12, ContentWidth, summaryHeight - 24);
            cursor += summaryHeight + Padding;
        }
        // Add and trial share one row; without a condition only "add" is shown, full width.
        float half = (CardWidth - RowGap) / 2;
        float addWidth = condition != null ? half : CardWidth;
        // The action row never leaves the inspector, whatever the editor above it needs.
        cursor = Mathf.Min(cursor, BottomLimit - 44);
        PlaceControl("＋ 完了条件を追加", 1184, cursor, addWidth, 44);
        PlaceRole("conditionAddBox", 1184, cursor, addWidth, 44);
        PlaceRole("conditionAddLabel", 1184, cursor, addWidth, 44);
        float trialX = 1184 + half + RowGap;
        PlaceControl("▶ この条件を試す", trialX, cursor, half, 44);
        PlaceRole("conditionTestBox", trialX, cursor, half, 44);
        PlaceRole("conditionTestLabel", trialX, cursor, half, 44);
    }

    // Places a rect at inspector design coordinates, whatever it is parented to.
    static void Place(SkillSyncDesignView view, RectTransform rect, float x, float y, float width, float height)
    {
        float X(float value) => view.wideLayout ? SkillSyncDesignLayout.MapX(value) : value;
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
