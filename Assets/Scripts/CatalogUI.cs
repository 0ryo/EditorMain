using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class CatalogUI : MonoBehaviour
{
    [SerializeField] PrefabRegistry registry;
    [SerializeField] PlacementController placementController;
    [SerializeField] RectTransform content;
    [SerializeField] Button buttonTemplate;
    [SerializeField] InputField searchInput;
    [SerializeField] Button addButton;
    [SerializeField] Text statusText;
    [SerializeField] float statusAutoClearSeconds = 2f;
    [SerializeField] float cornerRadius = 14f;

    [Serializable]
    public class StringEvent : UnityEvent<string> { }

    [SerializeField] StringEvent onSelectType;
    bool runtimeListenerBound;
    Coroutine clearStatusCoroutine;
    readonly List<CardState> cards = new();

    class CardState
    {
        public string typeId;
        public GameObject root;
    }

    void Start()
    {
        EnsureSingleEventSystem();
        EnsureRuntimeBindings();
        EnsureRuntimeCatalogControls();
        EnsureContentTopAligned();
        EnsureTemplateCardHeight();
        WireUiEvents();
        RebuildCards();
        ApplyRoundedTheme();
    }

    void OnDestroy()
    {
        NotifyDragState(false);
    }

    public void NotifyDragState(bool isDragging)
    {
        PlacementController.SetUiDragInProgress(isDragging);
    }

    public void HandleCardDrop(string typeId, Vector2 screenPosition)
    {
        EnsureRuntimeBindings();

        if (placementController == null)
        {
            SetStatus("PlacementController is not found.");
            return;
        }

        if (!placementController.PlaceOnceAtScreenPoint(typeId, screenPosition))
        {
            SetStatus("Placement failed.");
        }
    }
    void EnsureRuntimeBindings()
    {
        if (onSelectType == null) onSelectType = new StringEvent();

        if (placementController == null)
        {
            placementController = FindFirstObjectByType<PlacementController>();
        }

        if (registry == null && placementController != null)
        {
            registry = placementController.registry;
        }

        if (!runtimeListenerBound && onSelectType.GetPersistentEventCount() == 0 && placementController != null)
        {
            onSelectType.AddListener(placementController.EnterPlacement);
            runtimeListenerBound = true;
        }
    }

    void EnsureSingleEventSystem()
    {
        var all = FindObjectsByType<EventSystem>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        if (all == null || all.Length <= 1) return;

        EventSystem keep = EventSystem.current != null ? EventSystem.current : all[0];
        foreach (var es in all)
        {
            if (es == null || es == keep) continue;
            es.gameObject.SetActive(false);
            Debug.LogWarning($"[CatalogUI] Disabled duplicate EventSystem: {es.gameObject.name}");
        }
    }

    void WireUiEvents()
    {
        if (searchInput != null)
        {
            searchInput.onValueChanged.RemoveListener(OnSearchChanged);
            searchInput.onValueChanged.AddListener(OnSearchChanged);
        }

        if (addButton != null)
        {
            addButton.onClick.RemoveListener(OnClickAdd);
            addButton.onClick.AddListener(OnClickAdd);
        }
    }

    void RebuildCards()
    {
        EnsureRuntimeBindings();
        EnsureContentTopAligned();

        if (!registry) { Debug.LogError("CatalogUI: registry not set"); return; }
        if (!content) { Debug.LogError("CatalogUI: content not set"); return; }
        if (!buttonTemplate) { Debug.LogError("CatalogUI: buttonTemplate not set"); return; }

        foreach (Transform child in content)
        {
            if (child == buttonTemplate.transform) continue;
            Destroy(child.gameObject);
        }

        cards.Clear();

        foreach (var entry in registry.entries)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.typeId)) continue;

            var cardButton = Instantiate(buttonTemplate, content);
            cardButton.gameObject.name = $"Card_{entry.typeId}";
            cardButton.gameObject.SetActive(true);
            EnsureCardHeight(cardButton.gameObject);

            var typeId = entry.typeId;
            SetCardLabel(cardButton.gameObject, typeId);

            cardButton.onClick.RemoveAllListeners();
            cardButton.onClick.AddListener(() => OnClickCard(typeId));

            var drag = cardButton.GetComponent<CatalogCardDragHandler>();
            if (drag == null) drag = cardButton.gameObject.AddComponent<CatalogCardDragHandler>();
            drag.Initialize(this, typeId);

            cards.Add(new CardState
            {
                typeId = typeId,
                root = cardButton.gameObject
            });
        }

        ApplyFilter(searchInput != null ? searchInput.text : string.Empty);
        ApplyRoundedTheme();
    }

    void ApplyRoundedTheme()
    {
        UiRoundedTheme.ApplyToHierarchy(transform, cornerRadius);
    }

    void EnsureRuntimeCatalogControls()
    {
        if (searchInput != null) return;

        var panel = transform as RectTransform;
        if (panel == null) return;

        var searchRow = new GameObject("SearchRow_Runtime", typeof(RectTransform), typeof(Image));
        var searchRowRt = searchRow.GetComponent<RectTransform>();
        searchRowRt.SetParent(panel, false);
        searchRowRt.anchorMin = new Vector2(0f, 1f);
        searchRowRt.anchorMax = new Vector2(1f, 1f);
        searchRowRt.offsetMin = new Vector2(10f, -44f);
        searchRowRt.offsetMax = new Vector2(-10f, -8f);
        searchRow.GetComponent<Image>().color = Color.white;

        searchInput = CreateInputField(searchRowRt, "Input_Search_Runtime", "\u30AA\u30D6\u30B8\u30A7\u30AF\u30C8\u3092\u691C\u7D22...");
        var inputRt = searchInput.GetComponent<RectTransform>();
        inputRt.anchorMin = Vector2.zero;
        inputRt.anchorMax = Vector2.one;
        inputRt.offsetMin = new Vector2(8f, 4f);
        inputRt.offsetMax = new Vector2(-8f, -4f);

        if (content != null)
        {
            var scroll = content.parent != null ? content.parent.parent as RectTransform : null;
            if (scroll != null)
            {
                scroll.offsetMax = new Vector2(scroll.offsetMax.x, Mathf.Min(scroll.offsetMax.y, -52f));
            }
        }
    }

    void EnsureContentTopAligned()
    {
        if (content == null) return;

        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.anchoredPosition = Vector2.zero;

        var layout = content.GetComponent<VerticalLayoutGroup>();
        if (layout != null)
        {
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childForceExpandHeight = false;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
        }
    }

    void EnsureTemplateCardHeight()
    {
        if (buttonTemplate == null) return;
        EnsureCardHeight(buttonTemplate.gameObject);
    }

    void EnsureCardHeight(GameObject cardObject)
    {
        if (cardObject == null) return;

        var layout = cardObject.GetComponent<LayoutElement>();
        if (layout == null) layout = cardObject.AddComponent<LayoutElement>();
        layout.minHeight = 84f;
        layout.preferredHeight = 84f;
    }

    void SetCardLabel(GameObject root, string typeId)
    {
        var explicitMain = root.transform.Find("LabelMain");
        if (explicitMain != null)
        {
            var txt = explicitMain.GetComponent<Text>();
            if (txt != null) txt.text = typeId;

            var tmp = explicitMain.GetComponent<TMP_Text>();
            if (tmp != null) tmp.text = typeId;
            return;
        }

        var legacy = root.GetComponentInChildren<Text>(true);
        if (legacy != null) legacy.text = typeId;
        var tmps = root.GetComponentsInChildren<TMP_Text>(true);
        if (tmps.Length > 0) tmps[0].text = typeId;
    }

    void OnClickCard(string typeId)
    {
        if (string.IsNullOrWhiteSpace(typeId)) return;
        onSelectType?.Invoke(typeId);
        ClearStatus();
    }

    void OnSearchChanged(string text)
    {
        ApplyFilter(text);
    }

    void ApplyFilter(string query)
    {
        var normalized = query?.Trim() ?? string.Empty;
        var showAll = normalized.Length == 0;

        foreach (var card in cards)
        {
            if (card?.root == null) continue;
            var visible = showAll || card.typeId.IndexOf(normalized, StringComparison.OrdinalIgnoreCase) >= 0;
            card.root.SetActive(visible);
        }
    }

    void OnClickAdd()
    {
        SetStatus("Add action is not configured.");
    }
    void SetStatus(string message)
    {
        if (statusText == null)
        {
            Debug.LogWarning("[CatalogUI] " + message);
            return;
        }

        statusText.text = message;

        if (clearStatusCoroutine != null)
        {
            StopCoroutine(clearStatusCoroutine);
            clearStatusCoroutine = null;
        }

        if (statusAutoClearSeconds > 0f)
        {
            clearStatusCoroutine = StartCoroutine(ClearStatusAfterDelay(statusAutoClearSeconds));
        }
    }

    IEnumerator ClearStatusAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        clearStatusCoroutine = null;
        if (statusText != null) statusText.text = string.Empty;
    }

    void ClearStatus()
    {
        if (statusText != null) statusText.text = string.Empty;
        if (clearStatusCoroutine != null)
        {
            StopCoroutine(clearStatusCoroutine);
            clearStatusCoroutine = null;
        }
    }

    InputField CreateInputField(Transform parent, string name, string placeholderText)
    {
        var root = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(InputField));
        var rootRt = root.GetComponent<RectTransform>();
        rootRt.SetParent(parent, false);
        root.GetComponent<Image>().color = Color.white;
        var input = root.GetComponent<InputField>();

        var textGo = new GameObject("Text", typeof(RectTransform), typeof(Text));
        var textRt = textGo.GetComponent<RectTransform>();
        textRt.SetParent(rootRt, false);
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = new Vector2(8f, 0f);
        textRt.offsetMax = new Vector2(-8f, 0f);
        var text = textGo.GetComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.color = Color.black;
        text.fontSize = 14;
        text.alignment = TextAnchor.MiddleLeft;
        text.text = "";

        var placeholderGo = new GameObject("Placeholder", typeof(RectTransform), typeof(Text));
        var placeholderRt = placeholderGo.GetComponent<RectTransform>();
        placeholderRt.SetParent(rootRt, false);
        placeholderRt.anchorMin = Vector2.zero;
        placeholderRt.anchorMax = Vector2.one;
        placeholderRt.offsetMin = new Vector2(8f, 0f);
        placeholderRt.offsetMax = new Vector2(-8f, 0f);
        var placeholder = placeholderGo.GetComponent<Text>();
        placeholder.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        placeholder.color = new Color(0.55f, 0.55f, 0.55f, 1f);
        placeholder.fontSize = 14;
        placeholder.alignment = TextAnchor.MiddleLeft;
        placeholder.text = placeholderText;

        input.textComponent = text;
        input.placeholder = placeholder;
        return input;
    }
}

public class CatalogCardDragHandler : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    CatalogUI owner;
    string typeId;
    bool isDragging;

    public void Initialize(CatalogUI ownerUi, string selectedTypeId)
    {
        owner = ownerUi;
        typeId = selectedTypeId;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (owner == null || string.IsNullOrEmpty(typeId)) return;
        isDragging = true;
        owner.NotifyDragState(true);
    }

    public void OnDrag(PointerEventData eventData)
    {
        // No ghost preview in this phase.
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (owner == null) return;

        bool droppedOverUi = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(eventData.pointerId);
        if (isDragging && !droppedOverUi)
        {
            owner.HandleCardDrop(typeId, eventData.position);
        }

        owner.NotifyDragState(false);
        isDragging = false;
    }
}

