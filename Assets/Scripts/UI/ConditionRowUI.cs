using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class ConditionRowUI : MonoBehaviour
{
    public TMP_Dropdown dropdownA;
    public TMP_Dropdown dropdownB;
    public TMP_Text textAfterA;
    public TMP_Text textAfterB;

    static readonly Color DropdownBackground = DesignTokens.Surface;
    static readonly Color DropdownTemplateBackground = DesignTokens.Surface;

    const string LabelUnset = "\u672A\u8A2D\u5B9A";
    const string LabelMissingPrefix = "参照切れ: ";
    const string LabelParticleA = "\u3092";
    const string LabelParticleB = "\u306B\u8FD1\u3065\u3051\u308B";

    // 再描画のために保持する最後の Bind 引数
    List<PlacedObjectOptionProvider.Option> lastOptions;
    System.Action<string> lastOnAChanged;
    System.Action<string> lastOnBChanged;
    bool subscribed;

    void OnDestroy()
    {
        if (subscribed)
        {
            PlacedObject.OnDisplayNameChanged -= HandleDisplayNameChanged;
            subscribed = false;
        }
    }

    /// <summary>
    /// 表示名変更イベントを受け取り、現在の選択 ID を保ちながらドロップダウンを再描画する。
    /// </summary>
    void HandleDisplayNameChanged(PlacedObject _)
    {
        if (dropdownA == null || dropdownB == null) return;
        string aId = IndexToId(lastOptions, dropdownA.value);
        string bId = IndexToId(lastOptions, dropdownB.value);
        var freshOptions = PlacedObjectOptionProvider.GetOptions();
        Bind(freshOptions, aId, bId, lastOnAChanged, lastOnBChanged);
    }

    public void Bind(
        List<PlacedObjectOptionProvider.Option> options,
        string currentAId,
        string currentBId,
        Action<string> onAChanged,
        Action<string> onBChanged
    )
    {
        if (dropdownA == null || dropdownB == null) return;

        var displayOptions = BuildDisplayOptions(options, currentAId, currentBId);

        // 再描画用に引数を保持
        lastOptions    = displayOptions;
        lastOnAChanged = onAChanged;
        lastOnBChanged = onBChanged;

        // 表示名変更イベントを初回のみ購読
        if (!subscribed)
        {
            PlacedObject.OnDisplayNameChanged += HandleDisplayNameChanged;
            subscribed = true;
        }

        EnsureDropdownReferences(dropdownA);
        EnsureDropdownReferences(dropdownB);

        var labels = new List<string> { LabelUnset };
        if (displayOptions != null)
        {
            foreach (var option in displayOptions)
            {
                labels.Add(option.label);
            }
        }

        RebindDropdown(dropdownA, labels, IdToIndex(displayOptions, currentAId), v => onAChanged?.Invoke(IndexToId(displayOptions, v)));
        RebindDropdown(dropdownB, labels, IdToIndex(displayOptions, currentBId), v => onBChanged?.Invoke(IndexToId(displayOptions, v)));

        ApplyDropdownVisualStyle(dropdownA);
        ApplyDropdownVisualStyle(dropdownB);

        if (textAfterA != null) textAfterA.text = LabelParticleA;
        if (textAfterB != null) textAfterB.text = LabelParticleB;
        if (textAfterA != null) textAfterA.fontStyle = FontStyles.Bold;
        if (textAfterB != null) textAfterB.fontStyle = FontStyles.Bold;
    }

    static List<PlacedObjectOptionProvider.Option> BuildDisplayOptions(
        List<PlacedObjectOptionProvider.Option> options,
        string currentAId,
        string currentBId)
    {
        var result = options != null
            ? new List<PlacedObjectOptionProvider.Option>(options)
            : new List<PlacedObjectOptionProvider.Option>();

        AddMissingOption(result, currentAId);
        AddMissingOption(result, currentBId);
        return result;
    }

    static void AddMissingOption(List<PlacedObjectOptionProvider.Option> options, string id)
    {
        if (string.IsNullOrWhiteSpace(id) || options.Exists(option => option.id == id)) return;

        options.Add(new PlacedObjectOptionProvider.Option
        {
            id = id,
            label = LabelMissingPrefix + id
        });
    }

    static void RebindDropdown(TMP_Dropdown dropdown, List<string> labels, int value, Action<int> onChanged)
    {
        if (dropdown == null) return;

        dropdown.onValueChanged.RemoveAllListeners();
        dropdown.ClearOptions();
        dropdown.AddOptions(labels);
        dropdown.value = Mathf.Clamp(value, 0, labels.Count - 1);
        dropdown.RefreshShownValue();
        if (dropdown.captionText != null)
        {
            dropdown.captionText.text = labels[dropdown.value];
        }
        dropdown.onValueChanged.AddListener(v =>
        {
            if (dropdown.captionText != null && v >= 0 && v < labels.Count)
            {
                dropdown.captionText.text = labels[v];
            }
            onChanged?.Invoke(v);
        });

        ForceRebuildDropdownLayout(dropdown);
    }

    public static void PrepareDropdown(TMP_Dropdown dropdown)
    {
        if (dropdown == null) return;
        EnsureDropdownReferences(dropdown);
        ApplyDropdownVisualStyle(dropdown);
        ForceRebuildDropdownLayout(dropdown);
    }

    static void ApplyDropdownVisualStyle(TMP_Dropdown dropdown)
    {
        if (dropdown == null) return;

        dropdown.interactable = true;

        var rootImage = dropdown.GetComponent<Image>();
        if (rootImage != null) rootImage.color = DropdownBackground;
        EnsureThinOutline(dropdown.transform);

        if (dropdown.captionText != null)
        {
            EnsureTextReadable(dropdown.captionText);
            dropdown.captionText.color = DesignTokens.TextPrimary;
            dropdown.captionText.enableWordWrapping = false;
            dropdown.captionText.overflowMode = TextOverflowModes.Ellipsis;
        }

        if (dropdown.template != null)
        {
            var templateRt = dropdown.template;
            templateRt.anchorMin = new Vector2(0f, 0f);
            templateRt.anchorMax = new Vector2(1f, 0f);
            templateRt.pivot = new Vector2(0.5f, 1f);
            templateRt.anchoredPosition = Vector2.zero;
            templateRt.offsetMin = new Vector2(0f, templateRt.offsetMin.y <= -8f ? templateRt.offsetMin.y : -120f);
            templateRt.offsetMax = new Vector2(templateRt.offsetMax.x, 0f);
            templateRt.SetAsLastSibling();

            var templateImage = templateRt.GetComponent<Image>();
            if (templateImage != null) templateImage.color = DropdownTemplateBackground;
            EnsureThinOutline(templateRt);

            var viewport = templateRt.Find("Viewport");
            if (viewport != null)
            {
                var viewportImage = viewport.GetComponent<Image>();
                if (viewportImage != null) viewportImage.color = DropdownTemplateBackground;
                EnsureThinOutline(viewport);
            }

            var item = templateRt.Find("Viewport/Content/Item");
            if (item != null)
            {
                var itemRt = item as RectTransform;
                if (itemRt != null)
                {
                    itemRt.anchorMin = new Vector2(0f, 1f);
                    itemRt.anchorMax = new Vector2(1f, 1f);
                    itemRt.offsetMin = new Vector2(0f, itemRt.offsetMin.y);
                    itemRt.offsetMax = new Vector2(0f, itemRt.offsetMax.y);
                    if (itemRt.sizeDelta.y < DesignTokens.DropdownItemH)
                    {
                        itemRt.sizeDelta = new Vector2(itemRt.sizeDelta.x, DesignTokens.DropdownItemH);
                    }
                }

                var layout = item.GetComponent<LayoutElement>();
                if (layout == null) layout = item.gameObject.AddComponent<LayoutElement>();
                layout.minHeight = DesignTokens.DropdownItemH;
                layout.preferredHeight = DesignTokens.DropdownItemH;

                var itemImage = item.GetComponent<Image>();
                if (itemImage != null) itemImage.color = DesignTokens.Surface;
                EnsureThinOutline(item);

                var itemLabel = item.Find("Item Label");
                if (itemLabel != null)
                {
                    var txt = itemLabel.GetComponent<TMP_Text>();
                    if (txt != null)
                    {
                        var txtRt = txt.rectTransform;
                        txtRt.anchorMin = Vector2.zero;
                        txtRt.anchorMax = Vector2.one;
                        txtRt.offsetMin = new Vector2(8f, 0f);
                        txtRt.offsetMax = new Vector2(-8f, 0f);
                        txt.alignment = TextAlignmentOptions.MidlineLeft;
                        txt.enableWordWrapping = false;
                        txt.overflowMode = TextOverflowModes.Ellipsis;
                        EnsureTextReadable(txt);
                        txt.color = DesignTokens.TextPrimary;
                    }
                }
            }

            var allTexts = templateRt.GetComponentsInChildren<TMP_Text>(true);
            foreach (var t in allTexts)
            {
                if (t == null) continue;
                EnsureTextReadable(t);
                t.color = DesignTokens.TextPrimary;
            }
        }

        var colors = dropdown.colors;
        colors.colorMultiplier = 1f;
        colors.normalColor = DropdownBackground;
        colors.highlightedColor = DropdownBackground;
        colors.pressedColor = DropdownBackground;
        colors.selectedColor = DropdownBackground;
        dropdown.colors = colors;
    }

    static void EnsureThinOutline(Transform target)
    {
        if (target == null) return;
        if (target.GetComponent<Graphic>() == null) return;

        var outline = target.GetComponent<Outline>();
        if (outline == null) outline = target.gameObject.AddComponent<Outline>();
        outline.effectColor = DesignTokens.Divider;
        outline.effectDistance = new Vector2(1f, -1f);
        outline.useGraphicAlpha = false;
    }

    static void EnsureTextReadable(TMP_Text text)
    {
        if (text == null) return;

        text.enabled = true;
        text.gameObject.SetActive(true);
        text.alpha = 1f;
        if (text.fontSize <= 0) text.fontSize = 14;
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.richText = false;
    }

    static void EnsureDropdownReferences(TMP_Dropdown dropdown)
    {
        if (dropdown == null) return;

        if (dropdown.captionText == null)
        {
            var caption = dropdown.transform.Find("Caption");
            if (caption != null) dropdown.captionText = caption.GetComponent<TMP_Text>();
        }

        if (dropdown.template == null)
        {
            var template = dropdown.transform.Find("Template") as RectTransform;
            if (template != null) dropdown.template = template;
        }

        if (dropdown.itemText == null && dropdown.template != null)
        {
            var itemLabel = dropdown.template.Find("Viewport/Content/Item/Item Label");
            if (itemLabel != null) dropdown.itemText = itemLabel.GetComponent<TMP_Text>();
        }

        if (dropdown.captionText != null)
        {
            EnsureTextReadable(dropdown.captionText);
            dropdown.captionText.color = DesignTokens.TextPrimary;
        }

        if (dropdown.itemText != null)
        {
            EnsureTextReadable(dropdown.itemText);
            dropdown.itemText.color = DesignTokens.TextPrimary;
            dropdown.itemText.alignment = TextAlignmentOptions.MidlineLeft;
            dropdown.itemText.enableWordWrapping = false;
            dropdown.itemText.overflowMode = TextOverflowModes.Ellipsis;
            var rt = dropdown.itemText.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(8f, 0f);
            rt.offsetMax = new Vector2(-8f, 0f);
        }

        var openFixer = dropdown.GetComponent<DropdownOpenFixer>();
        if (openFixer == null) openFixer = dropdown.gameObject.AddComponent<DropdownOpenFixer>();
        openFixer.Bind(dropdown);

        if (dropdown.captionText == null || dropdown.itemText == null || dropdown.template == null)
        {
            Debug.LogWarning(
                $"[ConditionRowUI] dropdown refs missing " +
                $"caption={(dropdown.captionText != null)} item={(dropdown.itemText != null)} template={(dropdown.template != null)}");
        }
    }

    static void ForceRebuildDropdownLayout(TMP_Dropdown dropdown)
    {
        if (dropdown == null || dropdown.template == null) return;

        var templateRt = dropdown.template;
        var viewport = templateRt.Find("Viewport") as RectTransform;
        var content = templateRt.Find("Viewport/Content") as RectTransform;

        if (viewport != null)
        {
            var mask = viewport.GetComponent<Mask>();
            if (mask == null) mask = viewport.gameObject.AddComponent<Mask>();
            mask.showMaskGraphic = false;
        }

        if (content != null)
        {
            var v = content.GetComponent<VerticalLayoutGroup>();
            if (v == null) v = content.gameObject.AddComponent<VerticalLayoutGroup>();
            v.childControlHeight = true;
            v.childControlWidth = true;
            v.childForceExpandHeight = false;
            v.childForceExpandWidth = true;
            v.spacing = 0f;

            var fitter = content.GetComponent<ContentSizeFitter>();
            if (fitter == null) fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            for (int i = 0; i < content.childCount; i++)
            {
                var child = content.GetChild(i) as RectTransform;
                if (child == null) continue;
                var layout = child.GetComponent<LayoutElement>();
                if (layout == null) layout = child.gameObject.AddComponent<LayoutElement>();
                layout.minHeight = DesignTokens.DropdownItemH;
                layout.preferredHeight = DesignTokens.DropdownItemH;

                var label = child.Find("Item Label");
                if (label != null)
                {
                    var text = label.GetComponent<TMP_Text>();
                    EnsureTextReadable(text);
                }
            }

            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(templateRt);
    }

    static int IdToIndex(List<PlacedObjectOptionProvider.Option> options, string id)
    {
        if (string.IsNullOrEmpty(id) || options == null) return 0;
        for (int i = 0; i < options.Count; i++)
        {
            if (options[i].id == id) return i + 1;
        }
        return 0;
    }

    static string IndexToId(List<PlacedObjectOptionProvider.Option> options, int index)
    {
        if (index <= 0 || options == null) return null;
        int optionIndex = index - 1;
        if (optionIndex < 0 || optionIndex >= options.Count) return null;
        return options[optionIndex].id;
    }
}

public class DropdownOpenFixer : MonoBehaviour, IPointerClickHandler
{
    TMP_Dropdown dropdown;
    Coroutine fixRoutine;

    public void Bind(TMP_Dropdown target)
    {
        dropdown = target;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (dropdown == null || !dropdown.IsActive() || !dropdown.IsInteractable()) return;

        if (fixRoutine != null) StopCoroutine(fixRoutine);
        fixRoutine = StartCoroutine(FixAfterOpen());
    }

    IEnumerator FixAfterOpen()
    {
        yield return null;
        yield return null;

        if (dropdown == null) yield break;

        var root = dropdown.transform.root;
        var list = FindOpenDropdownList(root);
        if (list == null) yield break;

        var listImage = list.GetComponent<Image>();
        if (listImage != null) listImage.color = DesignTokens.Surface;

        var texts = list.GetComponentsInChildren<TMP_Text>(true);
        foreach (var t in texts)
        {
            if (t == null) continue;
            t.color = DesignTokens.TextPrimary;
            t.enabled = true;
            t.alignment = TextAlignmentOptions.MidlineLeft;
        }

        var toggles = list.GetComponentsInChildren<Toggle>(false);
        int visibleIndex = 0;
        for (int i = 0; i < toggles.Length; i++)
        {
            var toggle = toggles[i];
            if (toggle == null) continue;

            var itemRt = toggle.transform as RectTransform;
            if (itemRt != null)
            {
                var le = toggle.GetComponent<LayoutElement>();
                if (le == null) le = toggle.gameObject.AddComponent<LayoutElement>();
                le.minHeight = DesignTokens.DropdownItemH;
                le.preferredHeight = DesignTokens.DropdownItemH;
                itemRt.sizeDelta = new Vector2(
                    itemRt.sizeDelta.x,
                    Mathf.Max(DesignTokens.DropdownItemH, itemRt.sizeDelta.y));
            }

            var label = toggle.transform.Find("Item Label");
            if (label != null)
            {
                var txt = label.GetComponent<TMP_Text>();
                if (txt != null)
                {
                    txt.color = DesignTokens.TextPrimary;
                    txt.enabled = true;
                    txt.text = visibleIndex < dropdown.options.Count
                        ? dropdown.options[visibleIndex].text
                        : string.Empty;
                }
            }

            visibleIndex++;
        }

        var listRt = list.transform as RectTransform;
        if (listRt != null)
        {
            var dropdownRt = dropdown.transform as RectTransform;
            if (dropdownRt != null && dropdownRt.rect.width > 1f)
            {
                listRt.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, dropdownRt.rect.width);
            }

            StretchDropdownListContent(listRt);
            listRt.SetAsLastSibling();
            LayoutRebuilder.ForceRebuildLayoutImmediate(listRt);
        }
    }

    static void StretchDropdownListContent(RectTransform listRt)
    {
        var viewportRt = listRt.Find("Viewport") as RectTransform;
        var contentRt = listRt.Find("Viewport/Content") as RectTransform;

        if (viewportRt != null)
        {
            viewportRt.anchorMin = Vector2.zero;
            viewportRt.anchorMax = Vector2.one;
            viewportRt.offsetMin = new Vector2(0f, viewportRt.offsetMin.y);
            viewportRt.offsetMax = new Vector2(0f, viewportRt.offsetMax.y);
        }

        if (contentRt == null) return;
        contentRt.anchorMin = new Vector2(0f, 1f);
        contentRt.anchorMax = new Vector2(1f, 1f);
        contentRt.offsetMin = new Vector2(0f, contentRt.offsetMin.y);
        contentRt.offsetMax = new Vector2(0f, contentRt.offsetMax.y);

        for (int i = 0; i < contentRt.childCount; i++)
        {
            var itemRt = contentRt.GetChild(i) as RectTransform;
            if (itemRt == null) continue;
            itemRt.anchorMin = new Vector2(0f, 1f);
            itemRt.anchorMax = new Vector2(1f, 1f);
            itemRt.offsetMin = new Vector2(0f, itemRt.offsetMin.y);
            itemRt.offsetMax = new Vector2(0f, itemRt.offsetMax.y);

            var label = itemRt.Find("Item Label")?.GetComponent<TMP_Text>();
            if (label == null) continue;
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = new Vector2(12f, 0f);
            label.rectTransform.offsetMax = new Vector2(-12f, 0f);
            label.enableWordWrapping = false;
            label.overflowMode = TextOverflowModes.Ellipsis;
        }
    }

    static GameObject FindOpenDropdownList(Transform root)
    {
        if (root == null) return null;
        var all = root.GetComponentsInChildren<Transform>(true);
        foreach (var t in all)
        {
            if (t == null) continue;
            if (!t.gameObject.activeInHierarchy) continue;
            if (t.name == "Dropdown List")
            {
                return t.gameObject;
            }
        }

        return null;
    }
}
