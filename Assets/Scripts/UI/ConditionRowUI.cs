using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class ConditionRowUI : MonoBehaviour
{
    public Dropdown dropdownA;
    public Dropdown dropdownB;
    public Text textAfterA;
    public Text textAfterB;

    static readonly Color DropdownBackground = new Color(0.92f, 0.92f, 0.92f, 1f);
    static readonly Color DropdownTemplateBackground = new Color(0.97f, 0.97f, 0.97f, 1f);

    const string LabelUnset = "\u672A\u8A2D\u5B9A";
    const string LabelParticleA = "\u3092";
    const string LabelParticleB = "\u306B\u8FD1\u3065\u3051\u305F\u3089";

    public void Bind(
        List<PlacedObjectOptionProvider.Option> options,
        string currentAId,
        string currentBId,
        Action<string> onAChanged,
        Action<string> onBChanged
    )
    {
        if (dropdownA == null || dropdownB == null) return;

        EnsureDropdownReferences(dropdownA);
        EnsureDropdownReferences(dropdownB);

        var labels = new List<string> { LabelUnset };
        if (options != null)
        {
            foreach (var option in options)
            {
                labels.Add(option.label);
            }
        }

        RebindDropdown(dropdownA, labels, IdToIndex(options, currentAId), v => onAChanged?.Invoke(IndexToId(options, v)));
        RebindDropdown(dropdownB, labels, IdToIndex(options, currentBId), v => onBChanged?.Invoke(IndexToId(options, v)));

        ApplyDropdownVisualStyle(dropdownA);
        ApplyDropdownVisualStyle(dropdownB);

        Debug.Log(
            $"[ConditionRowUI] bind labels={labels.Count} " +
            $"aValue={dropdownA.value} bValue={dropdownB.value} " +
            $"captionA={(dropdownA.captionText != null)} itemA={(dropdownA.itemText != null)} templateA={(dropdownA.template != null)} " +
            $"labels={string.Join(",", labels)}");

        if (textAfterA != null) textAfterA.text = LabelParticleA;
        if (textAfterB != null) textAfterB.text = LabelParticleB;
    }

    static void RebindDropdown(Dropdown dropdown, List<string> labels, int value, Action<int> onChanged)
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

    static void ApplyDropdownVisualStyle(Dropdown dropdown)
    {
        if (dropdown == null) return;

        dropdown.interactable = true;

        var rootImage = dropdown.GetComponent<Image>();
        if (rootImage != null) rootImage.color = DropdownBackground;

        // Remove border effect if it exists.
        var outline = dropdown.GetComponent<Outline>();
        if (outline != null)
        {
            UnityEngine.Object.Destroy(outline);
        }

        if (dropdown.captionText != null)
        {
            EnsureTextReadable(dropdown.captionText);
            dropdown.captionText.color = Color.black;
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

            var viewport = templateRt.Find("Viewport");
            if (viewport != null)
            {
                var viewportImage = viewport.GetComponent<Image>();
                if (viewportImage != null) viewportImage.color = DropdownTemplateBackground;
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
                    if (itemRt.sizeDelta.y < 24f) itemRt.sizeDelta = new Vector2(itemRt.sizeDelta.x, 24f);
                }

                var layout = item.GetComponent<LayoutElement>();
                if (layout == null) layout = item.gameObject.AddComponent<LayoutElement>();
                layout.minHeight = 24f;
                layout.preferredHeight = 24f;

                var itemImage = item.GetComponent<Image>();
                if (itemImage != null) itemImage.color = new Color(0.93f, 0.93f, 0.93f, 1f);

                var itemLabel = item.Find("Item Label");
                if (itemLabel != null)
                {
                    var txt = itemLabel.GetComponent<Text>();
                    if (txt != null)
                    {
                        var txtRt = txt.rectTransform;
                        txtRt.anchorMin = Vector2.zero;
                        txtRt.anchorMax = Vector2.one;
                        txtRt.offsetMin = new Vector2(8f, 0f);
                        txtRt.offsetMax = new Vector2(-8f, 0f);
                        txt.alignment = TextAnchor.MiddleLeft;
                        EnsureTextReadable(txt);
                        txt.color = Color.black;
                    }
                }
            }

            var allTexts = templateRt.GetComponentsInChildren<Text>(true);
            foreach (var t in allTexts)
            {
                if (t == null) continue;
                EnsureTextReadable(t);
                t.color = Color.black;
            }
        }

        var colors = dropdown.colors;
        colors.colorMultiplier = 1f;
        colors.normalColor = DropdownBackground;
        colors.highlightedColor = new Color(0.88f, 0.88f, 0.88f, 1f);
        colors.pressedColor = new Color(0.83f, 0.83f, 0.83f, 1f);
        colors.selectedColor = new Color(0.88f, 0.88f, 0.88f, 1f);
        dropdown.colors = colors;
    }

    static void EnsureTextReadable(Text text)
    {
        if (text == null) return;

        // Always override to a known-good built-in font in Unity 6.
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.enabled = true;
        text.gameObject.SetActive(true);
        text.canvasRenderer.SetAlpha(1f);
        if (text.fontSize <= 0) text.fontSize = 14;
        text.alignment = TextAnchor.MiddleLeft;
        text.supportRichText = false;
    }

    static void EnsureDropdownReferences(Dropdown dropdown)
    {
        if (dropdown == null) return;

        if (dropdown.captionText == null)
        {
            var caption = dropdown.transform.Find("Caption");
            if (caption != null) dropdown.captionText = caption.GetComponent<Text>();
        }

        if (dropdown.template == null)
        {
            var template = dropdown.transform.Find("Template") as RectTransform;
            if (template != null) dropdown.template = template;
        }

        if (dropdown.itemText == null && dropdown.template != null)
        {
            var itemLabel = dropdown.template.Find("Viewport/Content/Item/Item Label");
            if (itemLabel != null) dropdown.itemText = itemLabel.GetComponent<Text>();
        }

        if (dropdown.captionText != null)
        {
            EnsureTextReadable(dropdown.captionText);
            dropdown.captionText.color = Color.black;
        }

        if (dropdown.itemText != null)
        {
            EnsureTextReadable(dropdown.itemText);
            dropdown.itemText.color = Color.black;
            dropdown.itemText.alignment = TextAnchor.MiddleLeft;
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
        else
        {
            Debug.Log(
                $"[ConditionRowUI] dropdown refs ok caption={dropdown.captionText.name} item={dropdown.itemText.name} template={dropdown.template.name}");
        }
    }

    static void ForceRebuildDropdownLayout(Dropdown dropdown)
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
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            for (int i = 0; i < content.childCount; i++)
            {
                var child = content.GetChild(i) as RectTransform;
                if (child == null) continue;
                var layout = child.GetComponent<LayoutElement>();
                if (layout == null) layout = child.gameObject.AddComponent<LayoutElement>();
                layout.minHeight = 24f;
                layout.preferredHeight = 24f;

                var label = child.Find("Item Label");
                if (label != null)
                {
                    var text = label.GetComponent<Text>();
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
    Dropdown dropdown;
    Coroutine fixRoutine;

    public void Bind(Dropdown target)
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
        if (listImage != null) listImage.color = new Color(0.97f, 0.97f, 0.97f, 1f);

        var texts = list.GetComponentsInChildren<Text>(true);
        foreach (var t in texts)
        {
            if (t == null) continue;
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.color = Color.black;
            t.enabled = true;
            t.alignment = TextAnchor.MiddleLeft;
            t.canvasRenderer.SetAlpha(1f);
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
                le.minHeight = 24f;
                le.preferredHeight = 24f;
                itemRt.sizeDelta = new Vector2(itemRt.sizeDelta.x, Mathf.Max(24f, itemRt.sizeDelta.y));
            }

            var label = toggle.transform.Find("Item Label");
            if (label != null)
            {
                var txt = label.GetComponent<Text>();
                if (txt != null)
                {
                    txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                    txt.color = Color.black;
                    txt.enabled = true;
                    txt.canvasRenderer.SetAlpha(1f);
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
            listRt.SetAsLastSibling();
            LayoutRebuilder.ForceRebuildLayoutImmediate(listRt);
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
