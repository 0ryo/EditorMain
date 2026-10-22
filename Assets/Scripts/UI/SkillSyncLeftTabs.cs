using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Placement view: the left panel switches between the library ("部品を追加") and the placed-parts list ("配置した部品"),
// which used to sit under the 3D screen. The 3D screen takes that space down to its status bar (trial mode keeps its
// learner panel there). Built at runtime from the generated UI, so UIRoot.prefab stays untouched.
public sealed class SkillSyncLeftTabs
{
    // Left column of the design (1000 high): tab bar under the header, then the tab's content.
    const float Left = 16, Width = 232, TabTop = 100, TabHeight = 36, Gap = 12, SearchHeight = 45.81f, Bottom = 982.3f;
    const float ContentTop = TabTop + TabHeight + Gap;
    static readonly Color Track = new Color32(238, 242, 247, 255), Muted = new Color32(88, 103, 124, 255);

    readonly SkillSyncDesignView view;
    readonly RectTransform objectsRoot;
    readonly List<GameObject> libraryOnly = new List<GameObject>();
    readonly RectTransform bar;
    readonly (Button button, SkillSyncRoundedGraphic fill, TMP_Text label)[] tabs = new (Button, SkillSyncRoundedGraphic, TMP_Text)[2];
    TMP_InputField partsSearch;
    GameObject heading;
    readonly List<GameObject> stripLabels = new List<GameObject>();
    int selected;
    int shownCount = -1;
    public bool ShowingPlaced => selected == 1;
    public System.Action Changed;

    public SkillSyncLeftTabs(SkillSyncDesignView view, TMP_FontAsset regular, TMP_FontAsset bold)
    {
        this.view = view;
        objectsRoot = (RectTransform)view.objectsContent.parent.parent;
        var chrome = objectsRoot.parent;
        var libraryRoot = (RectTransform)view.libraryContent.parent.parent;

        // What the library tab owns: the placement-only controls in the left column below the header, and the list.
        // (Step-view items share the column but carry the step state in their mask, so they are never touched.)
        void Own(GameObject go, int mask)
        {
            if (go != null && go.transform.parent == chrome && (mask & 9) != 0 && (mask & 2) == 0 && Column(go.transform) && !libraryOnly.Contains(go))
                libraryOnly.Add(go);
        }
        foreach (var v in view.visuals) Own(v.target, v.mask);
        foreach (var c in view.controls) Own(c.button != null ? c.button.gameObject : null, c.mask);
        foreach (var f in view.fields) Own(f.input != null ? f.input.gameObject : null, f.mask);
        libraryOnly.Add(libraryRoot.gameObject);
        heading = view.visuals.FirstOrDefault(v => v.label != null && v.label.text == "部品を追加" && libraryOnly.Contains(v.target))?.target;

        // The library's search, chips and list move down by the tab bar, keeping their spacing and the list's bottom edge.
        float shift = ContentTop - 129.64f;
        foreach (var go in libraryOnly)
        {
            var rect = (RectTransform)go.transform;
            float top = -rect.anchoredPosition.y;
            if (go == heading || top < 120 || top > 900) continue; // the add-model button stays at the bottom
            rect.anchoredPosition += Vector2.down * shift;
            if (rect == libraryRoot) SetHeight(libraryRoot, libraryRoot.rect.height - shift);
        }

        bar = NewRect("LeftTabs", chrome, Left, TabTop, Width, TabHeight);
        var track = bar.gameObject.AddComponent<SkillSyncRoundedGraphic>();
        track.color = track.borderColor = Track; track.borderWidth = 0; track.radius = 10; track.raycastTarget = false;
        for (int i = 0; i < 2; i++)
        {
            int index = i;
            float half = (Width - 6) / 2;
            var tab = NewRect(i == 0 ? "Tab_Library" : "Tab_Placed", bar, 3 + i * half, 3, half, TabHeight - 6);
            var fill = tab.gameObject.AddComponent<SkillSyncRoundedGraphic>();
            fill.radius = 8; fill.borderWidth = 0; fill.raycastTarget = true;
            var button = tab.gameObject.AddComponent<Button>();
            button.targetGraphic = fill; button.transition = Selectable.Transition.None;
            button.onClick.AddListener(() => Select(index));
            tab.gameObject.AddComponent<EditorUiInputBlocker>();
            var label = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
            label.rectTransform.SetParent(tab, false);
            label.rectTransform.anchorMin = Vector2.zero; label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = new Vector2(6, 0); label.rectTransform.offsetMax = new Vector2(-6, 0);
            label.font = bold != null ? bold : regular; label.fontSize = 14; label.alignment = TextAlignmentOptions.Midline;
            label.textWrappingMode = TextWrappingModes.NoWrap; label.overflowMode = TextOverflowModes.Ellipsis; label.raycastTarget = false;
            label.text = i == 0 ? "部品を追加" : "配置した部品";
            tabs[i] = (button, fill, label);
        }

        // The placed-parts list fills the rest of the column, under its own search field.
        float listTop = ContentTop + SearchHeight + Gap;
        objectsRoot.anchoredPosition = new Vector2(Left, -listTop);
        objectsRoot.sizeDelta = new Vector2(Width, Bottom - listTop);
        var viewport = (RectTransform)view.objectsContent.parent;
        viewport.sizeDelta = objectsRoot.sizeDelta;
        view.objectsContent.sizeDelta = new Vector2(Width, view.objectsContent.sizeDelta.y);
        var layout = view.objectsContent.GetComponent<VerticalLayoutGroup>();
        if (layout != null) layout.spacing = 6;
        var libraryBar = libraryRoot.Find("Scrollbar_Vertical");
        var scroll = objectsRoot.GetComponent<ScrollRect>();
        if (libraryBar != null && scroll != null && objectsRoot.Find("Scrollbar_Vertical") == null)
        {
            var copy = Object.Instantiate(libraryBar.gameObject, objectsRoot, false);
            copy.name = "Scrollbar_Vertical";
            // In the panel's right margin, so the rows keep the search field's width and the bar never covers them.
            ((RectTransform)copy.transform).anchoredPosition = new Vector2(10, 0);
            scroll.verticalScrollbar = copy.GetComponent<Scrollbar>();
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
        }
        view.objectTemplate.compact = true;
        var template = (RectTransform)view.objectTemplate.transform;
        template.sizeDelta = new Vector2(template.sizeDelta.x, SkillSyncDesignRow.CompactHeight);
        var templateElement = view.objectTemplate.GetComponent<LayoutElement>();
        if (templateElement != null) templateElement.preferredHeight = SkillSyncDesignRow.CompactHeight;
        // The list's old heading and count under the 3D screen: the tab now names it and shows the count.
        foreach (var v in view.visuals)
            if (v.label != null && v.target.transform.parent == chrome && (v.role == "objectCount" || v.label.text == "配置した部品" || v.label.text == "配置の確認"))
                stripLabels.Add(v.target);

        // The bottom strip no longer holds the list: the splitter and the right-panel animation must leave it alone,
        // and the 3D screen grows down to the status bar.
        var split = view.transform.Find("WorkspaceSplit")?.GetComponent<SkillSyncVerticalSplit>();
        if (split != null)
        {
            var below = split.items.FirstOrDefault(i => i.rect != null && i.rect.name == "BelowViewport");
            split.items = split.items.Where(i => i.rect != objectsRoot && i.rect != viewport).ToArray();
            split.Collapse(below.rect != null ? below.height - 34 : 234);
        }
        var expansion = view.GetComponent<SkillSyncWorkspaceExpansion>();
        if (expansion != null)
            expansion.items = expansion.items.Where(i => i.rect != objectsRoot && i.rect != viewport && i.rect != view.objectsContent).ToArray();
        Paint();
    }

    public void SetPartsSearch(TMP_InputField input)
    {
        partsSearch = input;
        if (input == null) return;
        var rect = (RectTransform)input.transform;
        rect.anchorMin = new Vector2(0, 1); rect.anchorMax = new Vector2(1, 1); rect.pivot = new Vector2(.5f, 0);
        rect.anchoredPosition = new Vector2(0, Gap); rect.sizeDelta = new Vector2(0, SearchHeight);
        // Same text size and inset as the library's search field in the other tab.
        var library = view.fields.FirstOrDefault(f => f.role == "search")?.input;
        float size = library != null && library.placeholder is TMP_Text hint ? hint.fontSize : 16;
        foreach (var text in new[] { input.textComponent, input.placeholder as TMP_Text })
            if (text != null) { text.fontSize = size; text.alignment = TextAlignmentOptions.MidlineLeft; }
        if (input.textViewport != null) { input.textViewport.offsetMin = new Vector2(16, 2); input.textViewport.offsetMax = new Vector2(-16, -2); }
    }

    public void Select(int index)
    {
        if (selected == index) return;
        selected = index;
        Paint();
        Changed?.Invoke();
    }

    // After every SkillSyncDesignView.Show (which re-shows everything by its state mask).
    public void Apply(int placedCount, bool hasFilter)
    {
        bool placement = view.State == 0 || view.State == 3;
        if (bar.gameObject.activeSelf != placement) bar.gameObject.SetActive(placement);
        if (placement) bar.SetAsLastSibling();
        bool placed = placement && selected == 1;
        if (view.State != 2 && view.State != 4) foreach (var go in stripLabels) if (go != null && go.activeSelf) go.SetActive(false);
        // The tab bar names the library, so its old heading is not shown twice.
        if (placement && heading != null && heading.activeSelf) heading.SetActive(false);
        foreach (var go in libraryOnly) if (go != null && placed && go.activeSelf) go.SetActive(false);
        if (objectsRoot.gameObject.activeSelf != placed) objectsRoot.gameObject.SetActive(placed);
        if (placed) objectsRoot.SetAsLastSibling();
        if (partsSearch != null)
        {
            bool search = placed && (placedCount > 0 || hasFilter);
            if (partsSearch.gameObject.activeSelf != search) partsSearch.gameObject.SetActive(search);
        }
        if (shownCount != placedCount)
        {
            shownCount = placedCount;
            tabs[1].label.text = placedCount > 0 ? "配置した部品 " + placedCount : "配置した部品";
        }
    }

    void Paint()
    {
        for (int i = 0; i < tabs.Length; i++)
        {
            bool on = i == selected;
            // Same look as the header's mode switch (① 配置を編集 …): the chosen one is filled with the accent colour.
            tabs[i].fill.color = tabs[i].fill.borderColor = on ? (Color)new Color32(8, 102, 232, 255) : Track;
            tabs[i].label.color = on ? Color.white : Muted;
        }
    }

    static bool Column(Transform t)
    {
        var rect = (RectTransform)t;
        return rect.anchoredPosition.x < 264 && -rect.anchoredPosition.y >= 90;
    }

    static void SetHeight(RectTransform rect, float height)
    {
        rect.sizeDelta = new Vector2(rect.sizeDelta.x, height);
        foreach (Transform child in rect)
            if (child is RectTransform inner && inner.anchorMin.y == inner.anchorMax.y) inner.sizeDelta = new Vector2(inner.sizeDelta.x, height);
    }

    static RectTransform NewRect(string name, Transform parent, float x, float y, float w, float h)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = new Vector2(x, -y); rect.sizeDelta = new Vector2(w, h);
        return rect;
    }
}
