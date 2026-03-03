using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class ObjectDetailPanel : MonoBehaviour
{
    [SerializeField] Text textPrefabLabel;
    [SerializeField] InputField inputObjectName;
    [SerializeField] InputField inputDescription;
    [SerializeField] Text textDescription; // Legacy fallback.
    [SerializeField] GameObject rowDescription;

    SelectionService selectionService;
    CatalogUI catalogUI;
    RectTransform rt;
    PlacedObject currentPo;

    Vector2 restOffsetMin;
    Vector2 restOffsetMax;
    Coroutine slideCoroutine;

    const float SlideDuration = 0.2f;
    const float DescriptionInputMinHeight = 96f;

    void Start()
    {
        rt = (RectTransform)transform;
        restOffsetMin = rt.offsetMin;
        restOffsetMax = rt.offsetMax;

        selectionService = FindFirstObjectByType<SelectionService>();
        catalogUI = FindFirstObjectByType<CatalogUI>();

        if (selectionService != null)
        {
            selectionService.OnSelectionChanged += OnSelectionChanged;
        }

        if (inputObjectName != null)
        {
            inputObjectName.onEndEdit.RemoveListener(OnNameInputEndEdit);
            inputObjectName.onEndEdit.AddListener(OnNameInputEndEdit);
        }

        EnsureDescriptionInputField();
        if (inputDescription != null)
        {
            inputDescription.onEndEdit.RemoveListener(OnDescriptionInputEndEdit);
            inputDescription.onEndEdit.AddListener(OnDescriptionInputEndEdit);
        }

        DesignTokenApplier.ApplyDetailPanel(transform);
        gameObject.SetActive(false);
    }

    void OnDestroy()
    {
        if (selectionService != null)
        {
            selectionService.OnSelectionChanged -= OnSelectionChanged;
        }

        if (inputObjectName != null)
        {
            inputObjectName.onEndEdit.RemoveListener(OnNameInputEndEdit);
        }

        if (inputDescription != null)
        {
            inputDescription.onEndEdit.RemoveListener(OnDescriptionInputEndEdit);
        }
    }

    void OnSelectionChanged(PlacedObject po)
    {
        if (po == null)
        {
            currentPo = null;
            if (gameObject.activeSelf)
            {
                if (slideCoroutine != null) StopCoroutine(slideCoroutine);
                slideCoroutine = StartCoroutine(SlideOut());
            }
            return;
        }

        currentPo = po;
        Populate(po);

        if (!gameObject.activeSelf)
        {
            gameObject.SetActive(true);
            if (slideCoroutine != null) StopCoroutine(slideCoroutine);
            slideCoroutine = StartCoroutine(SlideIn());
        }
    }

    void Populate(PlacedObject po)
    {
        string label = po.typeId ?? string.Empty;
        string defaultDescription = string.Empty;

        if (catalogUI != null)
        {
            catalogUI.TryGetTypeInfo(po.typeId, out label, out defaultDescription);
        }

        if (textPrefabLabel != null)
        {
            textPrefabLabel.text = label;
        }

        if (inputObjectName != null)
        {
            inputObjectName.SetTextWithoutNotify(po.GetDisplayName());
        }

        var showDescription = po.GetDisplayDescription(defaultDescription);

        if (rowDescription != null)
        {
            rowDescription.SetActive(true);
        }

        if (inputDescription != null)
        {
            inputDescription.SetTextWithoutNotify(showDescription);
        }
        else if (textDescription != null)
        {
            textDescription.text = showDescription;
        }
    }

    void OnNameInputEndEdit(string value)
    {
        if (currentPo == null) return;

        currentPo.SetDisplayName(value);
        if (inputObjectName != null)
        {
            inputObjectName.SetTextWithoutNotify(currentPo.GetDisplayName());
        }
    }

    void OnDescriptionInputEndEdit(string value)
    {
        if (currentPo == null) return;

        currentPo.SetDescription(value);
        if (inputDescription != null)
        {
            inputDescription.SetTextWithoutNotify(currentPo.GetDescription());
        }
        if (textDescription != null)
        {
            textDescription.text = currentPo.GetDescription();
        }
    }

    void EnsureDescriptionInputField()
    {
        if (rowDescription == null)
        {
            var row = transform.Find("Scroll_Detail/Viewport/Content/Row_Description");
            if (row != null) rowDescription = row.gameObject;
        }

        if (inputDescription == null)
        {
            inputDescription = GetComponentInChildren<InputField>(true);
            if (inputDescription != null && inputDescription == inputObjectName)
            {
                inputDescription = null;
            }
        }

        if (inputDescription == null && rowDescription != null)
        {
            inputDescription = FindDescriptionInputInRow(rowDescription.transform);
        }

        if (inputDescription == null && rowDescription != null)
        {
            inputDescription = CreateDescriptionInput(rowDescription.transform);
        }

        if (inputDescription != null)
        {
            ConfigureDescriptionInput(inputDescription);
            if (textDescription != null) textDescription.gameObject.SetActive(false);
            return;
        }

        if (textDescription == null && rowDescription != null)
        {
            textDescription = rowDescription.GetComponentInChildren<Text>(true);
        }
    }

    static InputField FindDescriptionInputInRow(Transform row)
    {
        if (row == null) return null;

        foreach (var input in row.GetComponentsInChildren<InputField>(true))
        {
            if (input == null) continue;
            if (input.gameObject.name == "Input_Description") return input;
        }

        return null;
    }

    static InputField CreateDescriptionInput(Transform row)
    {
        var inputGo = new GameObject("Input_Description", typeof(RectTransform), typeof(Image), typeof(InputField), typeof(LayoutElement));
        var inputRt = inputGo.GetComponent<RectTransform>();
        inputRt.SetParent(row, false);

        var image = inputGo.GetComponent<Image>();
        image.color = DesignTokens.BgPrimary;

        var input = inputGo.GetComponent<InputField>();
        input.lineType = InputField.LineType.MultiLineNewline;

        var layout = inputGo.GetComponent<LayoutElement>();
        layout.minHeight = DescriptionInputMinHeight;
        layout.preferredHeight = DescriptionInputMinHeight;

        var text = CreateInputText(inputRt, "Text", string.Empty, DesignTokens.TextPrimary);
        text.alignment = TextAnchor.UpperLeft;

        var placeholder = CreateInputText(inputRt, "Placeholder", "\u8AAC\u660E\u3092\u5165\u529B...", DesignTokens.TextTertiary);
        placeholder.alignment = TextAnchor.UpperLeft;

        input.textComponent = text;
        input.placeholder = placeholder;
        return input;
    }

    static Text CreateInputText(RectTransform parent, string name, string value, Color color)
    {
        var textGo = new GameObject(name, typeof(RectTransform), typeof(Text));
        var textRt = textGo.GetComponent<RectTransform>();
        textRt.SetParent(parent, false);
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.pivot = new Vector2(0.5f, 0.5f);
        textRt.offsetMin = new Vector2(8f, 8f);
        textRt.offsetMax = new Vector2(-8f, -8f);

        var text = textGo.GetComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = DesignTokens.FontSizeBody;
        text.color = color;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.alignment = TextAnchor.MiddleLeft;
        text.text = value;
        return text;
    }

    static void ConfigureDescriptionInput(InputField input)
    {
        if (input == null) return;

        input.lineType = InputField.LineType.MultiLineNewline;

        var image = input.GetComponent<Image>();
        if (image != null)
        {
            image.color = DesignTokens.BgPrimary;
        }

        var layout = input.GetComponent<LayoutElement>();
        if (layout == null) layout = input.gameObject.AddComponent<LayoutElement>();
        if (layout.minHeight < DescriptionInputMinHeight) layout.minHeight = DescriptionInputMinHeight;
        if (layout.preferredHeight < DescriptionInputMinHeight) layout.preferredHeight = DescriptionInputMinHeight;

        if (input.textComponent != null)
        {
            input.textComponent.alignment = TextAnchor.UpperLeft;
            input.textComponent.horizontalOverflow = HorizontalWrapMode.Wrap;
            input.textComponent.verticalOverflow = VerticalWrapMode.Overflow;
            var textRt = input.textComponent.rectTransform;
            textRt.offsetMin = new Vector2(8f, 8f);
            textRt.offsetMax = new Vector2(-8f, -8f);
        }

        if (input.placeholder is Text placeholderText)
        {
            placeholderText.alignment = TextAnchor.UpperLeft;
            var placeholderRt = placeholderText.rectTransform;
            placeholderRt.offsetMin = new Vector2(8f, 8f);
            placeholderRt.offsetMax = new Vector2(-8f, -8f);
        }
    }

    IEnumerator SlideIn()
    {
        float panelWidth = restOffsetMax.x - restOffsetMin.x;
        float elapsed = 0f;

        rt.offsetMin = new Vector2(restOffsetMin.x + panelWidth, restOffsetMin.y);
        rt.offsetMax = new Vector2(restOffsetMax.x + panelWidth, restOffsetMax.y);

        while (elapsed < SlideDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / SlideDuration);
            float eased = 1f - (1f - t) * (1f - t);

            float shift = Mathf.Lerp(panelWidth, 0f, eased);
            rt.offsetMin = new Vector2(restOffsetMin.x + shift, restOffsetMin.y);
            rt.offsetMax = new Vector2(restOffsetMax.x + shift, restOffsetMax.y);
            yield return null;
        }

        rt.offsetMin = restOffsetMin;
        rt.offsetMax = restOffsetMax;
        slideCoroutine = null;
    }

    IEnumerator SlideOut()
    {
        float panelWidth = restOffsetMax.x - restOffsetMin.x;
        float elapsed = 0f;
        float startShift = rt.offsetMin.x - restOffsetMin.x;

        while (elapsed < SlideDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / SlideDuration);
            float eased = t * t;

            float shift = Mathf.Lerp(startShift, panelWidth, eased);
            rt.offsetMin = new Vector2(restOffsetMin.x + shift, restOffsetMin.y);
            rt.offsetMax = new Vector2(restOffsetMax.x + shift, restOffsetMax.y);
            yield return null;
        }

        rt.offsetMin = restOffsetMin;
        rt.offsetMax = restOffsetMax;
        slideCoroutine = null;
        gameObject.SetActive(false);
    }
}
