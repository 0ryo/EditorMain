using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Runtime-built guidance around the library and the 3D screen (kept out of UIRoot.prefab like the other Ensure* controls):
// the placement guide and the loading banner over the 3D screen, the note about supported model formats, the "no match"
// message of the library, and the highlight of the chosen category chip.
public sealed class SkillSyncViewportGuide
{
    static readonly Color Accent = new Color32(8, 102, 232, 255);
    static readonly Color Ink = new Color32(23, 34, 54, 255);
    static readonly Color Muted = new Color32(88, 103, 124, 255);
    public const string FormatNote = "GLB/glTF形式に対応しています（FBX等はBlender等でGLBに書き出してください）";

    readonly SkillSyncDesignView view;
    readonly TMP_FontAsset font;
    RectTransform placementPill, loadingPill;
    TMP_Text placementLabel, loadingLabel, emptyLabel, formatLabel;
    Button cancelButton;
    GameObject formatHost;
    readonly Dictionary<string, (GameObject fill, TMP_Text label, Color color)> chips = new();
    string placementText = "";

    public Action cancelLoading;

    public SkillSyncViewportGuide(SkillSyncDesignView view, TMP_FontAsset font)
    {
        this.view = view; this.font = font;
        if (view.viewport != null)
        {
            placementPill = Pill("PlacementGuide", view.viewport, false, out placementLabel);
            loadingPill = Pill("ModelLoadingGuide", view.viewport, true, out loadingLabel);
            var cancel = view.viewport != null ? NewButton(loadingPill, "中止") : null;
            cancelButton = cancel;
            if (cancelButton != null) cancelButton.onClick.AddListener(() => cancelLoading?.Invoke());
            loadingPill.gameObject.SetActive(false);
            placementPill.gameObject.SetActive(false);
        }
        BuildLibraryTexts();
        BuildChips();
    }

    RectTransform Pill(string name, RectTransform parent, bool top, out TMP_Text label)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(SkillSyncRoundedGraphic));
        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, top ? 1f : 0f);
        rect.pivot = new Vector2(.5f, top ? 1f : 0f);
        rect.anchoredPosition = new Vector2(0, top ? -14f : 14f);
        var surface = go.GetComponent<SkillSyncRoundedGraphic>();
        surface.color = new Color32(23, 34, 54, 232); surface.borderColor = new Color32(23, 34, 54, 232);
        surface.radius = 14; surface.borderWidth = 0; surface.raycastTarget = false;
        label = NewLabel(rect, "Label", 15, Color.white, TextAlignmentOptions.Midline);
        return rect;
    }

    TMP_Text NewLabel(Transform parent, string name, float size, Color color, TextAlignmentOptions alignment)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
        var label = go.GetComponent<TextMeshProUGUI>();
        if (font != null) label.font = font;
        label.fontSize = size; label.color = color; label.alignment = alignment;
        label.textWrappingMode = TextWrappingModes.NoWrap; label.richText = false; label.raycastTarget = false;
        return label;
    }

    Button NewButton(RectTransform parent, string caption)
    {
        var go = new GameObject("CancelButton", typeof(RectTransform), typeof(SkillSyncRoundedGraphic), typeof(Button), typeof(EditorUiInputBlocker));
        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(1, .5f); rect.pivot = new Vector2(1, .5f);
        rect.sizeDelta = new Vector2(64, 30); rect.anchoredPosition = new Vector2(-10, 0);
        var surface = go.GetComponent<SkillSyncRoundedGraphic>();
        surface.color = Color.white; surface.borderColor = Color.white; surface.radius = 10; surface.borderWidth = 0;
        var button = go.GetComponent<Button>();
        button.targetGraphic = surface;
        var colors = ColorBlock.defaultColorBlock;
        colors.normalColor = Color.white; colors.highlightedColor = new Color(.88f, .93f, 1f, 1f);
        colors.pressedColor = new Color(.78f, .86f, 1f, 1f); colors.selectedColor = Color.white;
        button.colors = colors;
        NewLabel(rect, "Label", 14, Ink, TextAlignmentOptions.Midline).text = caption;
        return button;
    }

    static void Fit(RectTransform pill, TMP_Text label, float extra)
    {
        float width = Mathf.Min(label.GetPreferredValues(label.text).x + 40f + extra, 760f);
        pill.sizeDelta = new Vector2(width, 36);
        var rect = label.rectTransform;
        rect.offsetMin = new Vector2(20, 0); rect.offsetMax = new Vector2(-20 - extra, 0);
    }

    // Shown over the 3D screen for as long as a part is waiting to be placed.
    public void SetPlacement(string text)
    {
        if (placementPill == null) return;
        bool show = !string.IsNullOrEmpty(text);
        if (show && text != placementText)
        {
            placementText = text; placementLabel.text = text;
            Fit(placementPill, placementLabel, 0);
        }
        if (placementPill.gameObject.activeSelf != show) placementPill.gameObject.SetActive(show);
        if (show) placementPill.SetAsLastSibling();
    }

    // Stays up from the start of a 3D model import until it has finished or failed.
    public void SetLoading(bool loading)
    {
        if (loadingPill == null) return;
        if (loading && !loadingPill.gameObject.activeSelf)
        {
            loadingLabel.text = "3Dモデルを読み込んでいます…（大きなモデルは時間がかかります）";
            Fit(loadingPill, loadingLabel, 76);
        }
        if (loadingPill.gameObject.activeSelf != loading) loadingPill.gameObject.SetActive(loading);
        if (loading) loadingPill.SetAsLastSibling();
    }

    void BuildLibraryTexts()
    {
        var add = view.controls.FirstOrDefault(c => c.action == "＋ 3Dモデルを読み込む" && c.button != null)?.button;
        if (add != null)
        {
            var addRect = (RectTransform)add.transform;
            var host = new GameObject("ModelFormatNote", typeof(RectTransform));
            var rect = (RectTransform)host.transform;
            rect.SetParent(addRect.parent, false);
            rect.anchorMin = addRect.anchorMin; rect.anchorMax = addRect.anchorMax; rect.pivot = new Vector2(addRect.pivot.x, 0f);
            rect.sizeDelta = new Vector2(addRect.sizeDelta.x, 50);
            float bottom = addRect.anchoredPosition.y - addRect.pivot.y * addRect.sizeDelta.y;
            rect.anchoredPosition = new Vector2(addRect.anchoredPosition.x - addRect.pivot.x * addRect.sizeDelta.x + addRect.pivot.x * addRect.sizeDelta.x, bottom + addRect.sizeDelta.y + 4f);
            formatLabel = NewLabel(rect, "Text", 12, Muted, TextAlignmentOptions.BottomLeft);
            formatLabel.text = FormatNote; formatLabel.textWrappingMode = TextWrappingModes.Normal; formatLabel.overflowMode = TextOverflowModes.Ellipsis;
            formatHost = host;
        }
        var library = view.libraryContent != null ? view.libraryContent.parent.parent as RectTransform : null;
        if (library != null)
        {
            emptyLabel = NewLabel(library, "LibraryEmpty", 14, Muted, TextAlignmentOptions.Top);
            var rect = emptyLabel.rectTransform;
            rect.offsetMin = new Vector2(12, 12); rect.offsetMax = new Vector2(-12, -24);
            emptyLabel.textWrappingMode = TextWrappingModes.Normal;
            emptyLabel.gameObject.SetActive(false);
        }
    }

    public void Refresh(bool libraryVisible, string emptyMessage)
    {
        if (formatHost != null && formatHost.activeSelf != libraryVisible) formatHost.SetActive(libraryVisible);
        if (emptyLabel != null)
        {
            bool show = libraryVisible && !string.IsNullOrEmpty(emptyMessage);
            if (show) emptyLabel.text = emptyMessage;
            if (emptyLabel.gameObject.activeSelf != show) emptyLabel.gameObject.SetActive(show);
        }
    }

    // Each category chip gets an accent fill behind its caption while it is the chosen one.
    void BuildChips()
    {
        foreach (var name in new[] { "すべて", "部品", "環境" })
        {
            var button = view.controls.FirstOrDefault(c => c.action == name && c.button != null)?.button;
            var label = button != null ? button.GetComponentInChildren<TMP_Text>(true) : null;
            if (label == null) continue;
            var go = new GameObject("ActiveChip", typeof(RectTransform), typeof(SkillSyncRoundedGraphic));
            var rect = (RectTransform)go.transform;
            rect.SetParent(button.transform, false);
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
            rect.SetAsFirstSibling();
            var fill = go.GetComponent<SkillSyncRoundedGraphic>();
            fill.radius = 14; fill.borderWidth = 1; fill.color = fill.borderColor = Accent; fill.raycastTarget = false;
            go.SetActive(false);
            chips[name] = (go, label, label.color);
        }
    }

    public void SelectChip(string category)
    {
        foreach (var pair in chips)
        {
            bool active = pair.Key == category;
            if (pair.Value.fill != null && pair.Value.fill.activeSelf != active) pair.Value.fill.SetActive(active);
            if (pair.Value.label != null) pair.Value.label.color = active ? Color.white : pair.Value.color;
        }
    }
}

// Lets a library card be dragged out onto the 3D screen. Inside the library a drag still scrolls the list; once the
// pointer reaches the 3D screen the owner takes over (shows the placement ghost) and the scrolling stops.
public sealed class SkillSyncCardDrag : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public Action begin;
    public Func<Vector2, bool> moved;   // returns true while the owner has taken the drag over
    public Action<Vector2> ended;
    ScrollRect scroll;
    bool active;

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left) return;
        active = true;
        eventData.eligibleForClick = false;   // a drag is not a click on the card
        scroll = GetComponentInParent<ScrollRect>();
        begin?.Invoke();
        scroll?.OnBeginDrag(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!active) return;
        if (moved != null && moved(eventData.position)) return;
        scroll?.OnDrag(eventData);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!active) return;
        active = false;
        scroll?.OnEndDrag(eventData);
        ended?.Invoke(eventData.position);
    }
}
