using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// Audit patch B (panels and lists). Every step assigns absolute values or finds-or-creates by name,
// so a second run leaves UIRoot.prefab unchanged. Action strings are never touched.
public static class SkillSyncAuditPanelPatch
{
    const string PrefabPath = "Assets/UI/Prefabs/UIRoot.prefab";
    const string HandoffPath = "Assets/UI/SkillSyncDesign/handoff.json";
    const string ScrollbarName = "Scrollbar_Vertical";
    const float MinHit = 32f, MinFont = 14f;
    // Absolute tops (source-space y) of the "配置の補助" block after closing the gap (was 780 / 806.36 / 848 / 883.4).
    const float AssistLineY = 620f, AssistTitleTop = 646.36f, AssistRow1Top = 688f, AssistRow2Top = 723.4f;
    const float SnapY = 684f, SnapH = 34f, DetailY = 719f, DetailH = 32f;
    const float TitleBaselineY = 128f; // 88 (header bottom) + 16 padding + cap height of the 25px title

    [Serializable] class Document { public Source[] visuals; }
    [Serializable] class Source { public float x, y, w, h, size; public string kind, text; public string[] sources; }

    [MenuItem("Tools/Automation/SkillSync/Apply Audit Panel Patch")]
    public static void ApplyToPrefab()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
        var root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            var view = root.GetComponentInChildren<SkillSyncDesignView>(true);
            if (view == null) throw new InvalidOperationException("SkillSyncDesignView not found in UIRoot.");
            Apply(view);
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath, out bool saved);
            if (!saved) throw new InvalidOperationException("UIRoot save failed");
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        AssetDatabase.SaveAssets();
    }

    public static void Apply(SkillSyncDesignView view)
    {
        if (view.visuals == null || view.controls == null || view.fields == null) throw new InvalidOperationException("SkillSyncDesignView is not built.");
        var source = LoadSources();
        RaiseFontSizes(view);
        MoveInspectorHeader(view);
        AlignUnitLabels(view);
        CloseAssistGap(view, source);
        EnlargeHitAreas(view);
        EllipsizeStatusLabels(view);
        PadNameField(view);
        EnsureScrollbar(view.libraryContent, "Library");
        EnsureScrollbar(view.stepsContent, "Steps");
    }

    static Dictionary<string, Source> LoadSources()
    {
        var doc = JsonUtility.FromJson<Document>(File.ReadAllText(HandoffPath));
        var map = new Dictionary<string, Source>();
        foreach (var v in doc.visuals) if (v.sources != null) foreach (var id in v.sources) map[id] = v;
        return map;
    }

    static string PathOf(Transform t, Transform root)
    {
        var sb = new StringBuilder(t.name);
        for (var p = t.parent; p != null && p != root.parent; p = p.parent) sb.Insert(0, p.name + "/");
        return sb.ToString();
    }

    static bool TopLeft(RectTransform r) => r.anchorMin == new Vector2(0, 1) && r.anchorMax == new Vector2(0, 1);

    // Text sizes: every static TMP under SkillSyncDesign below 14 becomes 14.
    // Excluded because runtime or other patches size them: conditionEditorNote (SkillSyncConditionEditorView sets 12),
    // list-row templates (SkillSyncDesignRow / WP-10), TopCenterNotification (scales its own text), auto-sized labels.
    static void RaiseFontSizes(SkillSyncDesignView view)
    {
        var skip = new HashSet<TMP_Text>();
        foreach (var v in view.visuals) if (v.label != null && v.role == "conditionEditorNote") skip.Add(v.label);
        var changed = new List<string>();
        foreach (var t in view.GetComponentsInChildren<TMP_Text>(true))
        {
            if (skip.Contains(t) || t.enableAutoSizing || t.fontSize <= 0 || t.fontSize >= MinFont) continue;
            if (t.GetComponentInParent<SkillSyncDesignRow>(true) != null || t.GetComponentInParent<TopCenterNotification>(true) != null) continue;
            changed.Add(PathOf(t.transform, view.transform) + " " + t.fontSize + "->" + MinFont);
            t.fontSize = MinFont;
        }
        Debug.Log("[AuditPanelPatch] font size raised to 14 on " + changed.Count + " objects:\n" + string.Join("\n", changed));
    }

    // L-07: title and 選択中/仮配置 share one baseline (TMP Baseline alignment puts it at the rect center). Only y; x is owned by UpdateSelectionHeader.
    static void MoveInspectorHeader(SkillSyncDesignView view)
    {
        foreach (var label in new[] { view.inspectorTitle, view.inspectorSelection })
        {
            if (label == null) continue;
            var r = label.rectTransform;
            if (!TopLeft(r)) { Debug.LogWarning("[AuditPanelPatch] skipped non top-left header label " + label.name); continue; }
            r.anchoredPosition = new Vector2(r.anchoredPosition.x, -(TitleBaselineY - r.sizeDelta.y / 2));
        }
    }

    // L-08: unit labels (cm, deg, sec) fill their field height and are centered vertically.
    static void AlignUnitLabels(SkillSyncDesignView view)
    {
        foreach (var field in view.fields)
        {
            if (field.role != "height" && field.role != "angle" && field.role != "distance" && field.role != "hold") continue;
            var fieldRect = (RectTransform)field.input.transform;
            foreach (var t in field.input.GetComponentsInChildren<TMP_Text>(true))
            {
                if (t == field.input.textComponent || t == field.input.placeholder) continue;
                if (t.text != "cm" && t.text != "°" && t.text != "秒") continue;
                var r = t.rectTransform;
                if (!TopLeft(r)) continue;
                r.anchoredPosition = new Vector2(r.anchoredPosition.x, 0);
                r.sizeDelta = new Vector2(r.sizeDelta.x, fieldRect.rect.height);
                t.alignment = TextAlignmentOptions.MidlineLeft;
                t.margin = Vector4.zero;
            }
        }
    }

    // L-09 / L-10: close the gap above 配置の補助, keep Snap / 詳細設定 hit areas with their text rows, separate the two ticks.
    static void CloseAssistGap(SkillSyncDesignView view, Dictionary<string, Source> source)
    {
        float Y(float top, float size) => -(top + size - 20);
        foreach (var v in view.visuals)
        {
            if (v.sourceNodeIds == null || v.sourceNodeIds.Length == 0 || v.target == null || !source.TryGetValue(v.sourceNodeIds[0], out var s)) continue;
            if (s.x != 1184) continue;
            var r = (RectTransform)v.target.transform;
            if (!TopLeft(r)) continue;
            if (s.kind == "LINE" && s.y == 753) r.anchoredPosition = new Vector2(r.anchoredPosition.x, -AssistLineY);
            else if (v.label == null) continue;
            else if (s.text == "配置の補助") r.anchoredPosition = new Vector2(r.anchoredPosition.x, Y(AssistTitleTop, v.label.fontSize));
            else if (s.y == 818)
            {
                v.label.text = "✓ 作業面に吸着 ・ ✓ 10 cm刻み";
                r.sizeDelta = new Vector2(392, r.sizeDelta.y);
                r.anchoredPosition = new Vector2(r.anchoredPosition.x, Y(AssistRow1Top, v.label.fontSize));
            }
            else if (s.y == 852) r.anchoredPosition = new Vector2(r.anchoredPosition.x, Y(AssistRow2Top, v.label.fontSize));
        }
        foreach (var c in view.controls)
        {
            if (c.button == null || (c.action != "Snap" && c.action != "詳細設定")) continue;
            var r = (RectTransform)c.button.transform;
            if (!TopLeft(r)) continue;
            r.anchoredPosition = new Vector2(r.anchoredPosition.x, -(c.action == "Snap" ? SnapY : DetailY));
            r.sizeDelta = new Vector2(r.sizeDelta.x, c.action == "Snap" ? SnapH : DetailH);
        }
    }

    // Hit areas: grow only the raycast area (Graphic.raycastPadding) so visuals and layout do not move.
    static void EnlargeHitAreas(SkillSyncDesignView view)
    {
        var log = new List<string>();
        foreach (var c in view.controls)
        {
            if (c.button == null) continue;
            var graphic = c.button.targetGraphic != null ? c.button.targetGraphic : c.button.GetComponent<Graphic>();
            if (graphic == null) continue;
            var rect = ((RectTransform)c.button.transform).rect;
            float px = Mathf.Max(0, (MinHit - rect.width) / 2), py = Mathf.Max(0, (MinHit - rect.height) / 2);
            var padding = new Vector4(-px, -py, -px, -py); // left, bottom, right, top
            if (graphic.raycastPadding == padding) continue;
            graphic.raycastTarget = true;
            graphic.raycastPadding = padding;
            if (px > 0 || py > 0) log.Add(c.action + " " + rect.width + "x" + rect.height);
        }
        Debug.Log("[AuditPanelPatch] hit area padded for: " + string.Join(", ", log));
    }

    // L-12: no wrapping and an ellipsis for status / viewportMode / objectCount; fixed width only when the rect is not stretched.
    static void EllipsizeStatusLabels(SkillSyncDesignView view)
    {
        foreach (var v in view.visuals)
        {
            if (v.label == null || (v.role != "status" && v.role != "viewportMode" && v.role != "objectCount")) continue;
            v.label.textWrappingMode = TextWrappingModes.NoWrap;
            v.label.overflowMode = TextOverflowModes.Ellipsis;
            var r = v.label.rectTransform;
            if (r.anchorMin.x != r.anchorMax.x) continue;
            float width = v.role == "status" ? 580f : v.role == "viewportMode" ? 158f : 260f;
            r.sizeDelta = new Vector2(width, r.sizeDelta.y);
        }
    }

    // L-12: the name field text keeps a 14 px left padding (placeholder follows).
    static void PadNameField(SkillSyncDesignView view)
    {
        foreach (var field in view.fields)
        {
            if (field.role != "name" || field.input == null) continue;
            var graphics = new List<Graphic> { field.input.textComponent, field.input.placeholder };
            foreach (var g in graphics)
            {
                if (g == null) continue;
                var r = g.rectTransform;
                if (r.anchorMin.x != 0 || r.anchorMax.x != 1) continue;
                r.offsetMin = new Vector2(14, r.offsetMin.y);
            }
            if (field.input.textComponent != null) field.input.textComponent.margin = Vector4.zero;
        }
    }

    // L-06 / L-17: a vertical Scrollbar for the list ScrollRect (find-or-create by name, absolute values).
    static void EnsureScrollbar(RectTransform content, string listName)
    {
        var scroll = content != null ? content.GetComponentInParent<ScrollRect>(true) : null;
        if (scroll == null) throw new InvalidOperationException(listName + " ScrollRect not found.");
        var host = (RectTransform)scroll.transform;
        var existing = host.Find(ScrollbarName);
        GameObject go;
        if (existing != null) go = existing.gameObject;
        else
        {
            go = new GameObject(ScrollbarName, typeof(RectTransform), typeof(Image), typeof(Scrollbar));
            go.transform.SetParent(host, false);
        }
        var rect = (RectTransform)go.transform;
        rect.anchorMin = new Vector2(1, 0); rect.anchorMax = new Vector2(1, 1); rect.pivot = new Vector2(1, .5f);
        rect.sizeDelta = new Vector2(6, 0); rect.anchoredPosition = new Vector2(-2, 0);
        var track = go.GetComponent<Image>();
        track.color = new Color32(205, 213, 224, 90);
        track.raycastTarget = true;

        var area = go.transform.Find("Sliding Area") as RectTransform;
        if (area == null)
        {
            area = new GameObject("Sliding Area", typeof(RectTransform)).GetComponent<RectTransform>();
            area.SetParent(rect, false);
        }
        area.anchorMin = Vector2.zero; area.anchorMax = Vector2.one; area.offsetMin = area.offsetMax = Vector2.zero;
        var handle = area.Find("Handle") as RectTransform;
        if (handle == null)
        {
            handle = new GameObject("Handle", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            handle.SetParent(area, false);
        }
        handle.anchorMin = Vector2.zero; handle.anchorMax = Vector2.one; handle.offsetMin = handle.offsetMax = Vector2.zero;
        var handleImage = handle.GetComponent<Image>();
        handleImage.color = new Color32(120, 134, 154, 255);

        var bar = go.GetComponent<Scrollbar>();
        bar.handleRect = handle;
        bar.targetGraphic = handleImage;
        bar.direction = Scrollbar.Direction.BottomToTop;
        rect.SetAsLastSibling();

        scroll.vertical = true;
        scroll.verticalScrollbar = bar;
        scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
        scroll.verticalScrollbarSpacing = 0;
        Debug.Log("[AuditPanelPatch] vertical scrollbar assigned: " + listName);
    }
}
