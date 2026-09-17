using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Option indices include TMP's unset item at index zero. Identity never comes from a label.
public sealed class ObjectDropdownBrowser : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    TMP_Dropdown dropdown;
    readonly List<string> ids = new();
    readonly Dictionary<string, PlacedObject> objects = new();
    ObjectCandidatePreview preview;
    RectTransform configuredList;
    int openingFrames;
    int hovered = -1;
    Toggle[] rows;
    ScrollRect listScroll;
    TMP_Text emptyMessage;

    public static void Bind(TMP_Dropdown dropdown, List<PlacedObjectOptionProvider.Option> options)
    {
        Prepare(dropdown);
        var browser = dropdown.GetComponent<ObjectDropdownBrowser>();
        browser.Clear();
        ObjectScreenPicker.Cancel(browser);
        browser.ids.Clear();
        browser.ids.Add(null);
        if (options != null) foreach (var option in options) browser.ids.Add(option.id);
    }

    public static void Prepare(TMP_Dropdown dropdown)
    {
        if (!dropdown) return;
        var browser = dropdown.GetComponent<ObjectDropdownBrowser>();
        if (!browser) browser = dropdown.gameObject.AddComponent<ObjectDropdownBrowser>();
        browser.dropdown = dropdown;
        browser.enabled = true;
        if (dropdown.captionText) dropdown.captionText.richText = false;
        if (dropdown.template)
        {
            var scroll = dropdown.template.GetComponentInChildren<ScrollRect>(true);
            if (scroll) scroll.scrollSensitivity = 240f;
            PrepareControls(dropdown);
        }
    }

    void Update()
    {
        if (!dropdown) return;
        if (!dropdown.IsExpanded)
        {
            if (openingFrames > 0) Clear();
            openingFrames = 0;
            configuredList = null;
            return;
        }
        // Also handles keyboard Submit, which does not invoke the pointer fixer.
        if (++openingFrames == 1) Clear();
        if (openingFrames < 4) return;
        var list = dropdown.transform.Find("Dropdown List") as RectTransform;
        if (list && list != configuredList) ConfigureList(list);
    }

    public void ConfigureList(RectTransform list)
    {
        if (!enabled || list == configuredList) return;
        configuredList = list;
        RefreshObjects();
        float scale = Mathf.Max(0.01f, Mathf.Abs(list.lossyScale.x));
        float width = Mathf.Min(640f, Screen.width - 24f) / scale;
        list.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
        var scroll = list.GetComponentInChildren<ScrollRect>(true);
        if (!scroll || !scroll.content) return;
        scroll.scrollSensitivity = 240f / Mathf.Max(0.01f, Mathf.Abs(scroll.content.lossyScale.y));
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.horizontal = false;
        // AutoHideAndExpandViewport drives viewport offsets during every layout rebuild,
        // undoing the reserved search header and letting rows overlap it.
        scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
        scroll.horizontalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
        var toggles = scroll.content.GetComponentsInChildren<Toggle>(false);
        rows = toggles;
        listScroll = scroll;
        float totalHeight = 0;
        for (int i = 0; i < toggles.Length; i++)
        {
            var toggle = toggles[i];
            var text = toggle.GetComponentInChildren<TMP_Text>(true);
            float height = DesignTokens.DropdownItemH;
            if (text)
            {
                text.richText = false;
                text.rectTransform.anchorMin = Vector2.zero;
                text.rectTransform.anchorMax = Vector2.one;
                text.enableWordWrapping = true;
                text.overflowMode = TextOverflowModes.Overflow;
                text.rectTransform.offsetMin = new Vector2(12, 4);
                text.rectTransform.offsetMax = new Vector2(-28, -4);
                height = Mathf.Max(height, text.GetPreferredValues(text.text, Mathf.Max(40, width - 40), 0).y + 12);
            }
            var layout = toggle.GetComponent<LayoutElement>();
            if (!layout) layout = toggle.gameObject.AddComponent<LayoutElement>();
            layout.minHeight = layout.preferredHeight = height;
            ((RectTransform)toggle.transform).SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
            totalHeight += height;
            var item = toggle.GetComponent<ObjectDropdownCandidateItem>();
            if (!item) item = toggle.gameObject.AddComponent<ObjectDropdownCandidateItem>();
            item.Bind(this, i);
        }
        var search = list.Find("ObjectSearch").GetComponent<TMP_InputField>();
        search.gameObject.SetActive(true);
        search.onValueChanged.RemoveAllListeners();
        search.SetTextWithoutNotify(string.Empty);
        search.onValueChanged.AddListener(Filter);
        scroll.viewport.anchorMin = Vector2.zero;
        scroll.viewport.anchorMax = Vector2.one;
        scroll.viewport.offsetMax = new Vector2(scroll.viewport.offsetMax.x, -48 / scale);
        if (scroll.verticalScrollbar)
        {
            var bar = (RectTransform)scroll.verticalScrollbar.transform;
            bar.offsetMax = new Vector2(bar.offsetMax.x, -48 / scale);
        }
        var searchRect = (RectTransform)search.transform;
        searchRect.anchorMin = new Vector2(0, 1);
        searchRect.anchorMax = Vector2.one;
        searchRect.pivot = new Vector2(0.5f, 1);
        searchRect.offsetMin = new Vector2(4 / scale, -40 / scale);
        searchRect.offsetMax = new Vector2(-4 / scale, -4 / scale);
        emptyMessage = list.Find("NoMatches").GetComponent<TMP_Text>();
        emptyMessage.gameObject.SetActive(false);
        var pickerButton = list.Find("ScreenPick").GetComponent<Button>();
        pickerButton.gameObject.SetActive(true);
        pickerButton.onClick.RemoveAllListeners();
        pickerButton.onClick.AddListener(BeginScreenPick);
        StylePickButton(pickerButton);
        search.transform.SetAsLastSibling();
        LayoutRebuilder.ForceRebuildLayoutImmediate(scroll.content);
        LayoutRebuilder.ForceRebuildLayoutImmediate(list);
        Canvas.ForceUpdateCanvases();
        var canvas = list.GetComponentInParent<Canvas>().rootCanvas;
        var camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        var corners = new Vector3[4];
        ((RectTransform)dropdown.transform).GetWorldCorners(corners);
        var fieldMin = RectTransformUtility.WorldToScreenPoint(camera, corners[0]);
        var fieldMax = RectTransformUtility.WorldToScreenPoint(camera, corners[2]);
        // Preserve TMP's opening direction, then fit the list on that side of the field.
        list.GetWorldCorners(corners);
        var min = RectTransformUtility.WorldToScreenPoint(camera, corners[0]);
        var max = RectTransformUtility.WorldToScreenPoint(camera, corners[2]);
        bool opensAbove = (min.y + max.y) > (fieldMin.y + fieldMax.y);
        float available = opensAbove ? Screen.height - fieldMax.y - 12 : fieldMin.y - 12;
        if (available < 100) { opensAbove = !opensAbove; available = opensAbove ? Screen.height - fieldMax.y - 12 : fieldMin.y - 12; }
        list.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Mathf.Min(totalHeight + 52 / scale, Mathf.Max(80, Mathf.Min(360, available)) / scale));
        Canvas.ForceUpdateCanvases();
        list.GetWorldCorners(corners);
        min = RectTransformUtility.WorldToScreenPoint(camera, corners[0]);
        max = RectTransformUtility.WorldToScreenPoint(camera, corners[2]);
        var delta = new Vector2(Mathf.Max(0, 8 - min.x) - Mathf.Max(0, max.x - Screen.width + 8),
            opensAbove ? fieldMax.y + 4 - min.y : fieldMin.y - 4 - max.y);
        var screen = RectTransformUtility.WorldToScreenPoint(camera, list.position);
        if (RectTransformUtility.ScreenPointToWorldPointInRectangle((RectTransform)list.parent, screen + delta, camera, out var position))
            list.position = position;
        var pickRect = (RectTransform)pickerButton.transform;
        pickRect.anchorMin = pickRect.anchorMax = new Vector2(0, 1);
        pickRect.pivot = new Vector2(0, 1);
        float buttonWidth = fieldMax.x - fieldMin.x;
        float buttonHeight = DesignTokens.MinTouchTarget;
        pickRect.sizeDelta = new Vector2(buttonWidth / scale, buttonHeight / scale);
        var pickScreen = new Vector2(fieldMin.x,
            opensAbove ? fieldMin.y - DesignTokens.SpaceSm : fieldMax.y + DesignTokens.SpaceSm + buttonHeight);
        pickScreen.y = Mathf.Clamp(pickScreen.y, buttonHeight + 8, Screen.height - 8);
        if (RectTransformUtility.ScreenPointToWorldPointInRectangle(list, pickScreen, camera, out position)) pickRect.position = position;
        pickerButton.transform.SetAsLastSibling();
    }

    void Filter(string query)
    {
        Clear();
        if (rows == null || !listScroll) return;
        int matches = 0;
        for (int i = 0; i < rows.Length; i++)
        {
            bool show = Matches(dropdown.options[i].text, query);
            rows[i].gameObject.SetActive(show);
            if (show) matches++;
        }
        if (emptyMessage) emptyMessage.gameObject.SetActive(matches == 0);
        LayoutRebuilder.ForceRebuildLayoutImmediate(listScroll.content);
        listScroll.StopMovement();
        listScroll.verticalNormalizedPosition = 1;
    }

    public static bool Matches(string label, string query) => string.IsNullOrWhiteSpace(query) ||
        (label ?? string.Empty).IndexOf(query.Trim(), System.StringComparison.OrdinalIgnoreCase) >= 0;

    void BeginScreenPick()
    {
        Clear();
        dropdown.Hide();
        ObjectScreenPicker.Begin(this, SelectObject, dropdown.captionText ? dropdown.captionText.font : TMP_Settings.defaultFontAsset);
    }

    bool SelectObject(string id)
    {
        if (!dropdown || !isActiveAndEnabled) return false;
        int index = ids.IndexOf(id);
        if (index <= 0 || index >= dropdown.options.Count) return false;
        dropdown.value = index; // Existing listener routes through the condition edit/Undo service.
        dropdown.RefreshShownValue();
        return true;
    }

    // Shared by prefab automation and runtime fallback; TMP clones these with its popup.
    static void PrepareControls(TMP_Dropdown dropdown)
    {
        var template = dropdown.template;
        if (template.Find("ObjectSearch"))
        {
            var existingButton = template.Find("ScreenPick");
            if (existingButton) StylePickButton(existingButton.GetComponent<Button>());
            return;
        }
        TMP_FontAsset font = dropdown.captionText ? dropdown.captionText.font : TMP_Settings.defaultFontAsset;
        var searchObject = new GameObject("ObjectSearch", typeof(RectTransform), typeof(Image), typeof(TMP_InputField));
        searchObject.transform.SetParent(template, false);
        searchObject.GetComponent<Image>().color = DesignTokens.Surface;
        var input = searchObject.GetComponent<TMP_InputField>();
        var searchRect = (RectTransform)searchObject.transform;
        searchRect.anchorMin = new Vector2(0, 1);
        searchRect.anchorMax = Vector2.one;
        searchRect.offsetMin = new Vector2(4, -40);
        searchRect.offsetMax = new Vector2(-4, -4);
        var viewport = new GameObject("Text Area", typeof(RectTransform), typeof(RectMask2D)).GetComponent<RectTransform>();
        viewport.SetParent(searchObject.transform, false);
        Stretch(viewport, 8, 4);
        var text = MakeText("Text", viewport, font, string.Empty);
        var placeholder = MakeText("Placeholder", viewport, font, "オブジェクト名を検索…");
        placeholder.color = new Color(0.5f, 0.5f, 0.5f);
        input.textViewport = viewport;
        input.textComponent = text;
        input.placeholder = placeholder;
        input.lineType = TMP_InputField.LineType.SingleLine;
        var buttonObject = new GameObject("ScreenPick", typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(template, false);
        buttonObject.GetComponent<Image>().color = DesignTokens.Surface;
        ((RectTransform)buttonObject.transform).sizeDelta = new Vector2(260, 36);
        MakeText("Label", buttonObject.transform, font, "画面上から選択");
        StylePickButton(buttonObject.GetComponent<Button>());
        var empty = MakeText("NoMatches", template, font, "一致するオブジェクトがありません");
        empty.rectTransform.offsetMax = new Vector2(-8, -48);
        empty.gameObject.SetActive(false);
    }

    static TMP_Text MakeText(string name, Transform parent, TMP_FontAsset font, string value)
    {
        var text = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TMP_Text>();
        text.transform.SetParent(parent, false);
        text.font = font;
        text.fontSize = 18;
        text.text = value;
        text.color = DesignTokens.TextPrimary;
        text.richText = false;
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.raycastTarget = false;
        Stretch(text.rectTransform, 8, 2);
        return text;
    }

    static void StylePickButton(Button button)
    {
        if (!button) return;
        var image = button.GetComponent<Image>();
        image.color = DesignTokens.Accent;
        image.raycastTarget = true;
        button.targetGraphic = image;
        button.transition = Selectable.Transition.ColorTint;
        var colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(0.85f, 0.9f, 1f);
        colors.selectedColor = colors.normalColor;
        colors.pressedColor = new Color(0.7f, 0.8f, 1f);
        button.colors = colors;
        var text = button.GetComponentInChildren<TMP_Text>(true);
        if (text)
        {
            text.text = "画面上から選択";
            text.alignment = TextAlignmentOptions.Center;
            text.color = DesignTokens.ButtonTextLight;
            text.enableWordWrapping = false;
            text.enableAutoSizing = true;
            text.fontSizeMin = DesignTokens.FontSizeCaption;
            text.fontSizeMax = 18;
            Stretch(text.rectTransform, DesignTokens.SpaceSm, 4);
        }
    }
    static void Stretch(RectTransform rect, float x, float y)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(x, y);
        rect.offsetMax = new Vector2(-x, -y);
    }

    void RefreshObjects()
    {
        objects.Clear();
        foreach (var obj in FindObjectsByType<PlacedObject>(FindObjectsSortMode.None))
            if (!string.IsNullOrEmpty(obj.id)) objects[obj.id] = obj;
    }

    public void Show(int index)
    {
        if (!dropdown || index <= 0 || index >= ids.Count || index >= dropdown.options.Count) { Clear(); return; }
        hovered = index;
        if (!preview) preview = gameObject.AddComponent<ObjectCandidatePreview>();
        PlacedObject target = null;
        if (!string.IsNullOrEmpty(ids[index])) objects.TryGetValue(ids[index], out target);
        preview.Show(target, dropdown.options[index].text, dropdown.captionText ? dropdown.captionText.font : TMP_Settings.defaultFontAsset);
    }

    public void Hide(int index) { if (hovered == index) Clear(); }
    void Clear() { hovered = -1; if (preview) preview.Clear(); }
    public void OnPointerEnter(PointerEventData data)
    {
        if (!dropdown || dropdown.IsExpanded) return;
        RefreshObjects();
        Show(dropdown.value);
    }
    public void OnPointerExit(PointerEventData data) { Clear(); }
    void OnDisable() { ObjectScreenPicker.Cancel(this); Clear(); openingFrames = 0; configuredList = null; }
}

public sealed class ObjectDropdownCandidateItem : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    ObjectDropdownBrowser browser;
    int index;
    public void Bind(ObjectDropdownBrowser owner, int optionIndex) { browser = owner; index = optionIndex; }
    public void OnPointerEnter(PointerEventData data) { if (browser) browser.Show(index); }
    public void OnPointerExit(PointerEventData data) { if (browser) browser.Hide(index); }
    void OnDisable() { if (browser) browser.Hide(index); }
}
