using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

// Runs against the real prefab, without entering Play Mode or editing authored data.
public static class ConditionEditorLayoutChecks
{
    const string PrefabPath = "Assets/UI/Prefabs/UIRoot.prefab";
    static int assertions;

    [MenuItem("Tools/Automation/SkillSync/Check Condition Editor Layout")]
    public static void Run() => Validate(false);

    [MenuItem("Tools/Automation/SkillSync/Render Condition Editor Layout Checks")]
    public static void RunAndRender() => Validate(true);

    static void Require(bool result, string message)
    {
        if (!result) throw new InvalidOperationException("Condition editor layout: " + message);
        assertions++;
    }

    static void Validate(bool render)
    {
        assertions = 0;
        var root = PrefabUtility.LoadPrefabContents(PrefabPath);
        var captures = new List<Texture2D>();
        try
        {
            var view = root.GetComponentInChildren<SkillSyncDesignView>(true);
            Require(view != null, "Prefab contains the design view");
            // A saved overlay Canvas has zero size/scale until attached to a display.
            // Give the isolated prefab a real reference viewport for geometry/render checks.
            foreach (var scaler in root.GetComponentsInChildren<UnityEngine.UI.CanvasScaler>(true)) scaler.enabled = false;
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            var viewport = (RectTransform)root.transform;
            viewport.localScale = Vector3.one;
            viewport.sizeDelta = new Vector2(SkillSyncDesignLayout.Width, SkillSyncDesignLayout.Height);
            view.EnsureConditionControls();
            view.EnsureStepControls();
            SkillSyncConditionEditorView.Ensure(view);
            var group = view.GetComponent<CanvasGroup>();
            if (group != null) group.alpha = 1;
            var types = ConditionTypeCatalog.Definitions.Select(d => d.id).ToArray();
            for (int pass = 0; pass < 2; pass++)
            {
                bool configured = pass == 0;
                foreach (string type in types)
                {
                    // Reuse one view: transitions expose stale visibility and sizing.
                    Configure(view, type, configured);
                    Check(view, type);
                    foreach (int state in new[] { 0, 2, 4, 5, 1 })
                    {
                        view.Show(state);
                        SkillSyncConditionEditorView.Refresh(view, Condition(type), configured);
                        if (state == 1 || state == 5) Check(view, type);
                    }
                    if (render) captures.Add(Render(root, view));
                }
            }
            foreach (string type in types.Reverse().Concat(new[] { ConditionTypeCatalog.Separation, ConditionTypeCatalog.RotationMatch, ConditionTypeCatalog.Released, "CustomType" }))
            {
                Configure(view, type, false);
                Check(view, type);
            }
            view.Show(1);
            view.Text("conditionSummary", "完了条件がありません。下の＋で追加してください");
            SkillSyncConditionEditorView.Refresh(view, null, false);
            Require(!Active(view, "objectA") && !Active(view, "objectB"), "No condition leaves no target rows");
            Require(!view.conditionTypeDropdown.interactable, "No condition disables type edits");
            Require(!Active(view, "conditionEditorCard") && !Active(view, "conditionEditorNote"), "Empty condition hides the editor card and its note");
            Require(Active(view, "conditionSummary"), "Empty condition explains what to do in the summary");
            CheckConditionList(view);
            CheckTrialList(view);
            if (render)
            {
                var shots = RenderConditionLists(root, view);
                SaveContactSheet(shots.Take(6).ToArray(), "condition-list");
                SaveContactSheet(shots.Skip(6).ToArray(), "trial-list");
            }
            if (render)
            {
                SaveContactSheet(captures.Take(6).ToArray(), "configured");
                SaveContactSheet(captures.Skip(6).Take(6).ToArray(), "missing-targets");
            }
            Debug.Log($"[Condition editor layout] {assertions} visibility / overlap / compactness / text geometry checks passed.");
        }
        finally
        {
            foreach (var texture in captures) UnityEngine.Object.DestroyImmediate(texture);
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    static ConditionNodeData Condition(string type)
    {
        var condition = new ConditionNodeData { type = type, objectAId = "fixture-a", objectBId = "fixture-b" };
        ConditionTypeCatalog.Normalize(condition);
        return condition;
    }

    static void Configure(SkillSyncDesignView view, string type, bool configured)
    {
        var condition = Condition(type);
        view.Show(1);
        view.Text("inspectorLabel", "手順2の題名");
        view.Value("stepTitle", "ボルトを取り外す");
        view.Value("body", "対象の部品を選び、手順に沿って作業してください。");
        view.Value("distance", SkillSyncConditionEditing.Value(condition, 0));
        view.Value("hold", SkillSyncConditionEditing.Value(condition, 1));
        view.Text("objectA", configured ? "Bonnet" : "対象を選択してください");
        view.Text("objectB", configured ? "長い名前の対象部品でも欄からはみ出さないことを確認する" : "対象を選択してください");
        foreach (var thumbnail in new[] { view.objectAThumbnail, view.objectBThumbnail })
            if (thumbnail != null)
            {
                thumbnail.enabled = configured;
                thumbnail.texture = Texture2D.whiteTexture;
                thumbnail.color = new Color32(188, 202, 223, 255);
            }
        view.Text("conditionSummary", SkillSyncConditionEditing.Summary(condition) + (configured ? "" : "\n対象未設定・参照不正"));
        SkillSyncConditionEditorView.Refresh(view, condition, configured && SkillSyncConditionEditing.SupportsPcTrial(condition));
        view.Enable("▶ この条件を試す", configured && SkillSyncConditionEditing.SupportsPcTrial(condition));
    }

    static bool Active(SkillSyncDesignView view, string role) => view.visuals.Any(v => v.role == role && v.target.activeInHierarchy);
    static SkillSyncDesignView.Visual Visual(SkillSyncDesignView view, string role) => view.visuals.First(v => v.role == role && v.target.activeInHierarchy);
    static RectTransform Rect(SkillSyncDesignView view, string role) => (RectTransform)Visual(view, role).target.transform;
    static float Right(RectTransform rect) => rect.anchoredPosition.x + rect.rect.width;
    static float Top(RectTransform rect) => -rect.anchoredPosition.y;
    static float Bottom(RectTransform rect) => Top(rect) + rect.rect.height;

    static void Check(SkillSyncDesignView view, string type)
    {
        Canvas.ForceUpdateCanvases();
        var definition = ConditionTypeCatalog.Find(type);
        bool requiresB = ConditionTypeCatalog.RequiresObjectB(type);
        // Count the slots the editor shows: "近づける" edits distance and hold time even when stored as Proximity.
        int parameters = Enumerable.Range(0, 2).Count(slot => SkillSyncConditionEditing.Parameter(Condition(type), slot) != null);
        var dropdown = view.conditionTypeDropdown;
        var card = Rect(view, "conditionEditorCard");
        var note = Rect(view, "conditionEditorNote");
        var summaryBox = Rect(view, "conditionSummaryBox");
        Require(Active(view, "conditionTypeDropdown"), type + " has a visible type selector");
        Require(dropdown.captionText.text == (definition?.label ?? "未対応の条件：" + type), type + " shows its actual selection");
        Require(dropdown.transform.parent == card.parent && dropdown.transform.GetSiblingIndex() > card.GetSiblingIndex(), type + " selector renders above the card");
        Require(Active(view, "objectA") && Active(view, "objectB") == requiresB, type + " target visibility matches the definition");
        Require(Active(view, "conditionTargetBBox") == requiresB && Active(view, "conditionTargetBLabel") == requiresB && Active(view, "conditionTargetBArrow") == requiresB, type + " hides the whole inactive target row");
        for (int slot = 0; slot < 2; slot++)
        {
            bool active = slot < parameters;
            Require(new[] { "Label", "Box", "Unit" }.All(suffix => Active(view, "conditionParameter" + slot + suffix) == active), type + " parameter visuals are consistent");
            Require(view.fields.First(f => f.role == (slot == 0 ? "distance" : "hold")).input.gameObject.activeInHierarchy == active, type + " parameter input visibility");
        }
        var typeRect = (RectTransform)dropdown.transform;
        var lastRow = Rect(view, requiresB ? "conditionTargetBBox" : "conditionTargetABox");
        Require(Top(Rect(view, "conditionTargetALabel")) >= Bottom(typeRect) + 8, type + " type and target rows do not overlap");
        if (parameters > 0 && SkillSyncConditionEditing.Parameter(Condition(type), 0) != null) lastRow = Rect(view, "conditionParameter0Box");
        if (SkillSyncConditionEditing.ChoiceParameter(Condition(type)) != null)
        {
            var directions = view.controls.Where(c => c.action.StartsWith(SkillSyncConditionEditorView.DirectionActionPrefix, StringComparison.Ordinal)).ToArray();
            Require(directions.Length == 3 && directions.All(c => c.button.gameObject.activeInHierarchy), type + " shows the three turn directions");
            lastRow = (RectTransform)directions[0].button.transform;
            foreach (var direction in directions) CheckText(direction.button.GetComponentInChildren<TMP_Text>(), (RectTransform)direction.button.transform, type + ": direction");
            Require(Active(view, "conditionChoiceLabel"), type + " labels the direction row");
        }
        else Require(view.controls.Where(c => c.action.StartsWith(SkillSyncConditionEditorView.DirectionActionPrefix, StringComparison.Ordinal)).All(c => !c.button.gameObject.activeInHierarchy), type + " hides the turn directions");
        CheckRuleFits(Visual(view, "conditionRule").label, type + " rule");
        Require(Active(view, "conditionRule") && Bottom(Rect(view, "conditionRule")) <= Top(typeRect) - 4, type + " states the all-conditions rule above the card");
        for (int slot = 0; slot < 2; slot++)
            if (SkillSyncConditionEditing.Parameter(Condition(type), slot) != null)
            {
                var hint = Visual(view, "conditionParameter" + slot + "Hint").label;
                var labelRect = Rect(view, "conditionParameter" + slot + "Label");
                Require(hint.text.Contains("〜"), type + " shows the allowed range for parameter " + slot);
                Require(Right((RectTransform)hint.transform) <= Right(labelRect) + 1 && hint.GetPreferredValues(hint.text).x <= hint.rectTransform.rect.width + 1, type + " range hint fits beside its label");
            }
        Require(Top(note) >= Bottom(lastRow) + 8 && Top(note) - Bottom(lastRow) <= 24, type + " note follows the last visible input without an empty row");
        Require(Bottom(card) - Bottom(note) >= 12 && Bottom(card) - Bottom(note) <= 24, type + " card ends with normal padding");
        Require(Top(summaryBox) >= Bottom(card) + 12 && Top(summaryBox) - Bottom(card) <= 24, type + " summary follows the resized card");
        if (!requiresB && parameters == 0) Require(card.rect.height < 220, type + " single-object card has no reserved B/parameter space");
        var add = (RectTransform)view.controls.First(c => c.action == "＋ 完了条件を追加").button.transform;
        var trial = (RectTransform)view.controls.First(c => c.action == "▶ この条件を試す").button.transform;
        // "Add" and "trial" share one row below the summary / condition list.
        Require(Top(add) >= Bottom(summaryBox) + 12 && Mathf.Abs(Top(trial) - Top(add)) < 1 && Bottom(trial) <= 985 && Bottom(add) <= 985, type + " actions do not overlap or leave the inspector");
        Require(trial.anchoredPosition.x >= add.anchoredPosition.x + add.rect.width + 8 && trial.anchoredPosition.x + trial.rect.width <= add.anchoredPosition.x + 392 + 1, type + " add and trial buttons sit side by side");
        foreach (string role in new[] { "conditionSummary", "objectA", "objectB", "conditionAddLabel", "conditionTestLabel" })
            foreach (var visual in view.visuals.Where(v => v.role == role && v.label != null && v.target.activeInHierarchy))
                CheckText(visual.label, role == "conditionSummary" ? summaryBox : role == "conditionAddLabel" ? add : role == "conditionTestLabel" ? trial :
                    (RectTransform)view.controls.First(c => c.action == (role == "objectA" ? "PickA" : "PickB")).button.transform, type + ": " + role);
        for (int slot = 0; slot < parameters; slot++)
        {
            var input = view.fields.First(f => f.role == (slot == 0 ? "distance" : "hold")).input;
            CheckText(input.textComponent, (RectTransform)input.transform, type + ": parameter " + slot);
            CheckText(Visual(view, "conditionParameter" + slot + "Unit").label, (RectTransform)input.transform, type + ": unit " + slot);
        }
        CheckText(dropdown.captionText, typeRect, type + ": type caption");
        Require(view.visuals.Any(v => v.role == "inspectorLabel" && v.target.activeInHierarchy && v.label.text == "手順2の題名" && v.label.rectTransform.rect.width >= 300), type + " selected step is visible and has room");
    }

    // Every condition of a step is listed under the editor card: shortened to the space left, never overlapping.
    static void CheckConditionList(SkillSyncDesignView view)
    {
        var list = view.conditionList;
        Require(list != null, "Condition list exists");
        // One condition: the editor card says it all, so neither the list nor the summary card follows it.
        foreach (string type in new[] { ConditionTypeCatalog.SnapHold, ConditionTypeCatalog.Turn })
        {
            Configure(view, type, true);
            list.SetItems(new List<SkillSyncItemList.Item> { new SkillSyncItemList.Item { id = "condition-0", number = 1, selected = true, title = "近づけて保持：部品 → ジャッキ" } });
            SkillSyncConditionEditorView.Refresh(view, Condition(type), true);
            Canvas.ForceUpdateCanvases();
            Require(!list.gameObject.activeInHierarchy && !Active(view, "conditionSummary") && !Active(view, "conditionSummaryBox"), type + ": a single condition has no summary card");
        }
        foreach (int count in new[] { 2, 3, 8 })
        foreach (bool warning in new[] { false, true })
        foreach (string type in new[] { ConditionTypeCatalog.SnapHold, ConditionTypeCatalog.Proximity, ConditionTypeCatalog.Turn })
        {
            Configure(view, type, true);
            var items = Enumerable.Range(0, count).Select(i => new SkillSyncItemList.Item
            {
                id = "condition-" + i, number = i + 1, selected = i == 0, tag = i == 0 ? "編集中" : null, warn = i == 0 && warning,
                title = "近づけて保持：長い名前の対象部品でも欄からはみ出さないことを確認する → ジャッキ",
                detail = i == 0 && warning ? "⚠ 対象の部品が見つかりません" : SkillSyncConditionEditing.PcUnsupportedText
            }).ToList();
            list.SetItems(items);
            SkillSyncConditionEditorView.Refresh(view, Condition(type), true);
            Check(view, type);
            Canvas.ForceUpdateCanvases();
            string name = $"list of {count} ({type}, warning={warning})";
            var box = Rect(view, "conditionSummaryBox");
            var card = Rect(view, "conditionEditorCard");
            var listRect = (RectTransform)list.transform;
            var add = (RectTransform)view.controls.First(c => c.action == "＋ 完了条件を追加").button.transform;
            Require(list.gameObject.activeInHierarchy && !Active(view, "conditionSummary"), name + " replaces the single summary");
            Require(Top(listRect) >= Top(box) + 7 && Bottom(listRect) <= Bottom(box) - 7 && Top(box) >= Bottom(card) + 12, name + " sits inside its box below the card");
            Require(Top(add) >= Bottom(box) + 12 && Bottom(add) <= 985, name + " leaves room for the action row");
            float visible = Mathf.Min(list.ContentHeight, listRect.rect.height);
            Require(listRect.rect.height >= Mathf.Min(list.ContentHeight, SkillSyncItemList.RowHeight) - .5f, name + " shows at least one row");
            // A tall editor (two targets plus two values) leaves room for one row and a peek of the next, which invites scrolling.
            float rowsShown = (listRect.rect.height + SkillSyncItemList.RowGap) / (SkillSyncItemList.RowHeight + SkillSyncItemList.RowGap);
            Require(Mathf.Abs(rowsShown - Mathf.Round(rowsShown)) < .01f, name + " shows whole rows only");
            var row = listRect.Find("Viewport/Content/Row_0");
            Require(row != null && row.Find("Tag") != null, name + " marks the condition being edited");
            foreach (string part in new[] { "Title", "Detail" })
            {
                var text = row.Find(part).GetComponent<TMP_Text>();
                CheckText(text, (RectTransform)text.transform, name + " " + part);
            }
            Require(Right((RectTransform)row.Find("Title")) <= ((RectTransform)row.Find("Tag")).anchoredPosition.x, name + " title does not run under the edit tag");
        }
        list.SetItems(new List<SkillSyncItemList.Item>());
        Configure(view, ConditionTypeCatalog.SnapHold, false);
    }

    // Contact sheet of the condition list in typical and worst cases (long names, warning, 8 conditions).
    static Texture2D[] RenderConditionLists(GameObject root, SkillSyncDesignView view)
    {
        var captures = new List<Texture2D>();
        foreach (var (count, type, warning) in new[]
        {
            (2, ConditionTypeCatalog.SnapHold, false), (3, ConditionTypeCatalog.Proximity, true),
            (2, ConditionTypeCatalog.SnapHold, true), (8, ConditionTypeCatalog.Proximity, false), (4, ConditionTypeCatalog.Turn, false)
        })
        {
            Configure(view, type, true);
            view.Text("conditionCounter", count + "件（最大8件）");
            view.conditionList.SetItems(Enumerable.Range(0, count).Select(i => new SkillSyncItemList.Item
            {
                id = "condition-" + i, number = i + 1, selected = i == 0, tag = i == 0 ? "編集中" : null, warn = i == 0 && warning,
                title = (i % 2 == 0 ? "近づけて保持" : "回す") + "：ジャッキ → 設置枠" + (i == 1 ? "（長い名前の対象部品でも欄からはみ出さない）" : ""),
                detail = i == 0 && warning ? "⚠ 対象の部品が見つかりません" : i % 2 == 1 ? SkillSyncConditionEditing.PcUnsupportedText : "10 cm以内に1秒とどまると完了"
            }).ToList());
            SkillSyncConditionEditorView.Refresh(view, Condition(type), true);
            Canvas.ForceUpdateCanvases();
            captures.Add(Render(root, view));
        }
        view.conditionList.SetItems(new List<SkillSyncItemList.Item>());
        // Trial: nothing measurable (gauges hidden, full-height list), and a gauge with one unjudgeable condition below it.
        for (int variant = 0; variant < 2; variant++)
        {
            view.Show(2);
            view.Text("trialStep", "手順3 実行中");
            view.Text("trialBody", "反時計回りに緩め、引き抜く");
            var list = view.trialList;
            if (variant == 0)
            {
                SkillSyncConditionEditorView.HideTrialGauges(view);
                list.SetItems(new List<SkillSyncItemList.Item>
                {
                    new SkillSyncItemList.Item { id = "a", number = 1, title = "回す：ナット1", detail = SkillSyncConditionEditing.PcUnsupportedText, actionLabel = "成立にする" },
                    new SkillSyncItemList.Item { id = "b", number = 2, title = "引く：ナット1", detail = SkillSyncConditionEditing.PcUnsupportedText, actionLabel = "成立として次へ" }
                });
                SkillSyncConditionEditorView.LayoutTrial(view, false, false, 2);
            }
            else
            {
                list.SetItems(new List<SkillSyncItemList.Item>
                { new SkillSyncItemList.Item { id = "a", number = 2, done = true, title = "押す：ジャッキのハンドル", detail = "✓ 成立しました" } });
                SkillSyncConditionEditorView.LayoutTrial(view, true, true, 1);
            }
            Canvas.ForceUpdateCanvases();
            captures.Add(Render(root, view));
        }
        view.trialList.SetItems(new List<SkillSyncItemList.Item>());
        view.Show(1);
        return captures.ToArray();
    }

    // The trial lists the step's conditions; the ones the PC cannot judge get a manual-success button.
    static void CheckTrialList(SkillSyncDesignView view)
    {
        var list = view.trialList;
        Require(list != null, "Trial condition list exists");
        view.Show(2);
        list.SetBounds(view.wideLayout ? SkillSyncDesignLayout.MapX(1184) : 1184, 330, 392, 440);
        list.SetItems(new List<SkillSyncItemList.Item>
        {
            new SkillSyncItemList.Item { id = "a", number = 1, title = "回す：ナット1", detail = SkillSyncConditionEditing.PcUnsupportedText, actionLabel = "成立として次へ" },
            new SkillSyncItemList.Item { id = "b", number = 2, title = "引く：ナット1", detail = "✓ 成立しました", done = true }
        });
        Canvas.ForceUpdateCanvases();
        var first = list.transform.Find("Viewport/Content/Row_0");
        var action = (RectTransform)first.Find("Action");
        Require(action != null && list.transform.Find("Viewport/Content/Row_1/Action") == null, "Only a pending, unjudgeable condition has the manual button");
        CheckText(action.Find("Text").GetComponent<TMP_Text>(), action, "trial row action");
        var title = first.Find("Title").GetComponent<TMP_Text>();
        Require(Right((RectTransform)first.Find("Title")) <= action.anchoredPosition.x, "trial row title does not run under the action button");
        CheckText(title, (RectTransform)title.transform, "trial row title");
        list.SetItems(new List<SkillSyncItemList.Item>());
        view.Text("trialRule", SkillSyncConditionEditorView.RuleLine + "\n編集中の配置から始まります。次の手順へは位置を引き継ぎます");
        var rule = Visual(view, "trialRule").label;
        CheckRuleFits(rule, "trial rule");
        view.Show(1);
        // A finished step keeps just its number inside the green badge (no "✓3").
        foreach (string index in new[] { "3", "14" })
        {
            var row = UnityEngine.Object.Instantiate(view.stepTemplate, view.stepsContent);
            row.gameObject.SetActive(true);
            row.Set("手順 " + index, "条件1件", index, false, true);
            Require(row.number.text == index, "Finished step row keeps its number: " + index);
            row.number.ForceMeshUpdate(true, true);
            Require(row.number.textBounds.size.x <= row.number.rectTransform.rect.width + 1, "Finished step number " + index + " fits its badge");
            UnityEngine.Object.DestroyImmediate(row.gameObject);
        }
    }

    // Two caption lines must fit their box without wrapping into a third line.
    static void CheckRuleFits(TMP_Text text, string message)
    {
        text.ForceMeshUpdate(true, true);
        Require(text.textInfo.lineCount <= 2 && text.textBounds.size.y <= text.rectTransform.rect.height + 1, message + " fits two lines");
    }

    static void CheckText(TMP_Text text, RectTransform box, string message)
    {
        text.ForceMeshUpdate(true, true);
        var rect = text.rectTransform.rect;
        // TMP.textBounds uses font ascender/descender metrics, including empty space.
        // Check the actual rendered glyph vertices instead (Midline uses this geometry).
        var glyphs = text.textInfo.characterInfo.Take(text.textInfo.characterCount).Where(c => c.isVisible).ToArray();
        Require(glyphs.Length > 0, message + " has visible glyphs");
        var min = new Vector3(glyphs.Min(c => c.bottomLeft.x), glyphs.Min(c => c.bottomLeft.y), 0);
        var max = new Vector3(glyphs.Max(c => c.topRight.x), glyphs.Max(c => c.topRight.y), 0);
        var bounds = new Bounds((min + max) / 2, max - min);
        Require(Mathf.Abs(bounds.center.y - rect.center.y) < 1.5f,
            message + $" centers the actual glyph block for any line count (glyph={bounds.center.y}, rect={rect.center.y}, height={rect.height}, text={text.text})");
        Require(bounds.min.y >= rect.yMin - 1 && bounds.max.y <= rect.yMax + 1, message + " glyphs stay inside the text area");
        var corners = new Vector3[4];
        box.GetWorldCorners(corners);
        float center = (corners[0].y + corners[1].y) / 2;
        float textCenter = text.rectTransform.TransformPoint(bounds.center).y;
        Require(Mathf.Abs(textCenter - center) < 1.5f * text.rectTransform.lossyScale.y,
            message + $" centers inside its background (glyph={textCenter}, box={center}, text parent={text.transform.parent.name}, box parent={box.parent.name})");
        Require(text.textInfo.characterInfo.All(c => !c.isVisible || c.topRight.x <= rect.xMax + 1), message + " clips long captions inside the field");
    }

    static Texture2D Render(GameObject root, SkillSyncDesignView view)
    {
        const int width = 400, height = 890;
        var cameraObject = new GameObject("ConditionLayoutCheckCamera", typeof(Camera));
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(cameraObject, root.scene);
        var target = new RenderTexture(width, height, 24);
        var previous = RenderTexture.active;
        try
        {
            foreach (var transform in root.GetComponentsInChildren<Transform>(true)) transform.gameObject.layer = 31;
            foreach (var scaler in root.GetComponentsInChildren<UnityEngine.UI.CanvasScaler>(true)) scaler.enabled = false;
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            var camera = cameraObject.GetComponent<Camera>();
            camera.scene = root.scene;
            canvas.worldCamera = camera;
            camera.cullingMask = 1 << 31;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.white;
            camera.orthographic = true;
            float x = view.wideLayout ? SkillSyncDesignLayout.MapX(1180) : 1180;
            var inspector = view.conditionTypeDropdown.transform.parent;
            var topLeft = inspector.TransformPoint(new Vector3(x, -100, 0));
            var bottomRight = inspector.TransformPoint(new Vector3(x + width, -990, 0));
            var center = (topLeft + bottomRight) / 2;
            camera.transform.position = center + Vector3.back * 100;
            camera.orthographicSize = (topLeft.y - bottomRight.y) / 2;
            camera.nearClipPlane = .1f;
            camera.farClipPlane = 200;
            camera.targetTexture = target;
            Canvas.ForceUpdateCanvases();
            camera.Render();
            RenderTexture.active = target;
            var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            texture.Apply();
            Require(texture.GetPixels32().Count(p => p.r < 160 && p.b > 180) > 200,
                "Offscreen capture contains the blue action button, rather than an empty viewport");
            return texture;
        }
        finally
        {
            RenderTexture.active = previous;
            target.Release();
            UnityEngine.Object.DestroyImmediate(target);
            UnityEngine.Object.DestroyImmediate(cameraObject);
        }
    }

    static void SaveContactSheet(Texture2D[] images, string name)
    {
        var sheet = new Texture2D(images[0].width * 3, images[0].height * 2, TextureFormat.RGB24, false);
        try
        {
            for (int i = 0; i < images.Length; i++)
                sheet.SetPixels(i % 3 * images[i].width, (1 - i / 3) * images[i].height, images[i].width, images[i].height, images[i].GetPixels());
            sheet.Apply();
            string folder = Path.GetFullPath("Logs/condition-layout-checks");
            Directory.CreateDirectory(folder);
            File.WriteAllBytes(Path.Combine(folder, name + ".png"), sheet.EncodeToPNG());
        }
        finally { UnityEngine.Object.DestroyImmediate(sheet); }
    }
}

// Layout invariants are a Player build gate, so a successful compile is insufficient.
public sealed class ConditionEditorLayoutBuildCheck : IPreprocessBuildWithReport
{
    public int callbackOrder => 0;
    public void OnPreprocessBuild(BuildReport report) => ConditionEditorLayoutChecks.Run();
}
