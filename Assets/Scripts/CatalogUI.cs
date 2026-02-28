using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class CatalogUI : MonoBehaviour
{
    [SerializeField] PrefabRegistry registry;
    [SerializeField] PlacementController placementController;
    [SerializeField] RectTransform content;
    [SerializeField] Button buttonTemplate;
    [SerializeField] InputField searchInput;
    [SerializeField] Button addButton;
    [SerializeField] Text statusText;
    [SerializeField] RectTransform newObjectSettingsPanel;
    [SerializeField] InputField newObjectNameInput;
    [SerializeField] InputField newObjectDescriptionInput;
    [SerializeField] Button newObjectApplyButton;
    [SerializeField] Button newObjectCancelButton;
    [SerializeField] Text newObjectPathText;
    [SerializeField] float statusAutoClearSeconds = 2f;
    [SerializeField] float cornerRadius = DesignTokens.CornerRadius;
    [SerializeField] string importedCardLabel = "New Object";

    [Serializable]
    public class StringEvent : UnityEvent<string> { }

    [SerializeField] StringEvent onSelectType;
    bool runtimeListenerBound;
    Coroutine clearStatusCoroutine;
    readonly List<CardState> cards = new();
    string runtimeImportedTypeId;
    string runtimeImportedCardLabel;
    string runtimeImportedDescription;
    GameObject runtimeImportedPrefab;
    GameObject pendingImportedPrefab;
    string pendingImportedAssetPath;

    class CardState
    {
        public string typeId;
        public string displayLabel;
        public string displayDescription;
        public GameObject root;
    }

    void Start()
    {
        cornerRadius = DesignTokens.CornerRadius;
        EnsureSingleEventSystem();
        EnsureRuntimeBindings();
        EnsureRuntimeCatalogControls();
        EnsureContentTopAligned();
        EnsureTemplateCardHeight();
        WireUiEvents();
        RebuildCards();
        ApplyRoundedTheme();
        DesignTokenApplier.ApplyCatalogPanel(transform);
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

        if (newObjectApplyButton != null)
        {
            newObjectApplyButton.onClick.RemoveListener(OnClickApplyNewObjectSettings);
            newObjectApplyButton.onClick.AddListener(OnClickApplyNewObjectSettings);
        }

        if (newObjectCancelButton != null)
        {
            newObjectCancelButton.onClick.RemoveListener(OnClickCancelNewObjectSettings);
            newObjectCancelButton.onClick.AddListener(OnClickCancelNewObjectSettings);
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
                displayLabel = typeId,
                displayDescription = string.Empty,
                root = cardButton.gameObject
            });
        }

        AddRuntimeImportedCardIfNeeded();

        ApplyFilter(searchInput != null ? searchInput.text : string.Empty);
        ApplyRoundedTheme();
        DesignTokenApplier.ApplyCatalogPanel(transform);
    }

    void AddRuntimeImportedCardIfNeeded()
    {
        if (string.IsNullOrWhiteSpace(runtimeImportedTypeId) || runtimeImportedPrefab == null) return;
        if (buttonTemplate == null || content == null) return;

        var cardButton = Instantiate(buttonTemplate, content);
        cardButton.gameObject.name = "Card_NewObject";
        cardButton.gameObject.SetActive(true);
        EnsureCardHeight(cardButton.gameObject);
        var cardLabel = string.IsNullOrWhiteSpace(runtimeImportedCardLabel) ? importedCardLabel : runtimeImportedCardLabel;
        SetCardLabel(cardButton.gameObject, cardLabel);

        var importedTypeId = runtimeImportedTypeId;
        cardButton.onClick.RemoveAllListeners();
        cardButton.onClick.AddListener(() => OnClickCard(importedTypeId));

        var drag = cardButton.GetComponent<CatalogCardDragHandler>();
        if (drag == null) drag = cardButton.gameObject.AddComponent<CatalogCardDragHandler>();
        drag.Initialize(this, importedTypeId);

        cards.Add(new CardState
        {
            typeId = importedTypeId,
            displayLabel = cardLabel,
            displayDescription = runtimeImportedDescription,
            root = cardButton.gameObject
        });
    }

    void ApplyRoundedTheme()
    {
        UiRoundedTheme.ApplyToHierarchy(transform, cornerRadius);
    }

    void EnsureRuntimeCatalogControls()
    {
        var panel = transform as RectTransform;
        if (panel == null) return;

        EnsureRuntimeSearchInput(panel);
        EnsureRuntimeBottomAddButton(panel);
        EnsureRuntimeNewObjectSettingsDialog(panel);
        EnsureScrollBottomPadding(56f);
    }

    void EnsureRuntimeSearchInput(RectTransform panel)
    {
        if (searchInput != null) return;

        var searchRow = new GameObject("SearchRow_Runtime", typeof(RectTransform), typeof(Image));
        var searchRowRt = searchRow.GetComponent<RectTransform>();
        searchRowRt.SetParent(panel, false);
        searchRowRt.anchorMin = new Vector2(0f, 1f);
        searchRowRt.anchorMax = new Vector2(1f, 1f);
        searchRowRt.offsetMin = new Vector2(10f, -44f);
        searchRowRt.offsetMax = new Vector2(-10f, -8f);
        searchRow.GetComponent<Image>().color = DesignTokens.BgPrimary;

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

    void EnsureRuntimeBottomAddButton(RectTransform panel)
    {
        if (addButton != null)
        {
            var existingRt = addButton.transform as RectTransform;
            var isBottomAnchored = existingRt != null &&
                                   Mathf.Approximately(existingRt.anchorMin.y, 0f) &&
                                   Mathf.Approximately(existingRt.anchorMax.y, 0f);

            if (isBottomAnchored)
            {
                EnsureScrollBottomPadding(56f);
                return;
            }

            addButton.gameObject.SetActive(false);
            addButton = null;
        }

        var buttonRoot = new GameObject("Button_AddObjectBottom_Runtime", typeof(RectTransform), typeof(Image), typeof(Button));
        var buttonRt = buttonRoot.GetComponent<RectTransform>();
        buttonRt.SetParent(panel, false);
        buttonRt.anchorMin = new Vector2(0f, 0f);
        buttonRt.anchorMax = new Vector2(1f, 0f);
        buttonRt.offsetMin = new Vector2(10f, 10f);
        buttonRt.offsetMax = new Vector2(-10f, 48f);

        var image = buttonRoot.GetComponent<Image>();
        image.color = DesignTokens.BgSecondary;
        addButton = buttonRoot.GetComponent<Button>();

        var labelGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
        var labelRt = labelGo.GetComponent<RectTransform>();
        labelRt.SetParent(buttonRt, false);
        labelRt.anchorMin = Vector2.zero;
        labelRt.anchorMax = Vector2.one;
        labelRt.offsetMin = Vector2.zero;
        labelRt.offsetMax = Vector2.zero;

        var label = labelGo.GetComponent<Text>();
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.color = DesignTokens.TextPrimary;
        label.fontSize = 14;
        label.alignment = TextAnchor.MiddleCenter;
        label.text = "Import FBX";

        EnsureScrollBottomPadding(56f);
    }

    void EnsureRuntimeNewObjectSettingsDialog(RectTransform panel)
    {
        var host = ResolveNewObjectSettingsHost(panel);
        if (host == null) return;

        var existing = FindExistingNewObjectSettingsPanel(panel, host);
        if (existing != null)
        {
            if (existing.parent != host)
            {
                existing.SetParent(host, false);
            }

            newObjectSettingsPanel = existing;
            ConfigureNewObjectSettingsOverlayRect(newObjectSettingsPanel);
            BindNewObjectSettingsReferences(newObjectSettingsPanel);
            ApplyNewObjectSettingsDesign(newObjectSettingsPanel);
            newObjectSettingsPanel.gameObject.SetActive(false);
            return;
        }

        var overlayRoot = new GameObject("Panel_NewObjectSettings", typeof(RectTransform), typeof(Image));
        var overlayRt = overlayRoot.GetComponent<RectTransform>();
        overlayRt.SetParent(host, false);
        ConfigureNewObjectSettingsOverlayRect(overlayRt);

        var window = new GameObject("Window", typeof(RectTransform), typeof(Image));
        var windowRt = window.GetComponent<RectTransform>();
        windowRt.SetParent(overlayRt, false);
        ConfigureNewObjectSettingsWindowRect(windowRt);
        var windowImage = window.GetComponent<Image>();
        windowImage.color = DesignTokens.Surface;

        var title = new GameObject("Text_Title", typeof(RectTransform), typeof(Text));
        var titleRt = title.GetComponent<RectTransform>();
        titleRt.SetParent(windowRt, false);
        var titleText = title.GetComponent<Text>();
        titleText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        titleText.color = DesignTokens.TextPrimary;
        titleText.fontSize = 16;
        titleText.alignment = TextAnchor.MiddleLeft;
        titleText.text = "\u30AA\u30D6\u30B8\u30A7\u30AF\u30C8\u8A2D\u5B9A";

        var pathTextObj = new GameObject("Text_FilePath", typeof(RectTransform), typeof(Text));
        var pathTextRt = pathTextObj.GetComponent<RectTransform>();
        pathTextRt.SetParent(windowRt, false);
        var pathText = pathTextObj.GetComponent<Text>();
        pathText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        pathText.color = DesignTokens.TextSecondary;
        pathText.fontSize = 12;
        pathText.alignment = TextAnchor.UpperLeft;
        pathText.horizontalOverflow = HorizontalWrapMode.Wrap;
        pathText.verticalOverflow = VerticalWrapMode.Truncate;
        pathText.text = string.Empty;

        var nameLabelObj = new GameObject("Text_NameLabel", typeof(RectTransform), typeof(Text));
        var nameLabelRt = nameLabelObj.GetComponent<RectTransform>();
        nameLabelRt.SetParent(windowRt, false);
        var nameLabel = nameLabelObj.GetComponent<Text>();
        nameLabel.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        nameLabel.color = DesignTokens.TextPrimary;
        nameLabel.fontSize = 13;
        nameLabel.alignment = TextAnchor.MiddleLeft;
        nameLabel.text = "\u30AA\u30D6\u30B8\u30A7\u30AF\u30C8\u540D";

        var input = CreateInputField(windowRt, "Input_NewObjectName", "New Object");

        var descriptionLabelObj = new GameObject("Text_DescriptionLabel", typeof(RectTransform), typeof(Text));
        var descriptionLabelRt = descriptionLabelObj.GetComponent<RectTransform>();
        descriptionLabelRt.SetParent(windowRt, false);
        var descriptionLabel = descriptionLabelObj.GetComponent<Text>();
        descriptionLabel.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        descriptionLabel.color = DesignTokens.TextPrimary;
        descriptionLabel.fontSize = 13;
        descriptionLabel.alignment = TextAnchor.MiddleLeft;
        descriptionLabel.text = "\u8AAC\u660E";

        var descriptionInput = CreateMultilineInputField(windowRt, "Input_NewObjectDescription", "\u8AAC\u660E\u3092\u5165\u529B...");

        var buttonsRow = new GameObject("ButtonsRow", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        var buttonsRowRt = buttonsRow.GetComponent<RectTransform>();
        buttonsRowRt.SetParent(windowRt, false);
        var buttonsLayout = buttonsRow.GetComponent<HorizontalLayoutGroup>();
        buttonsLayout.spacing = 8f;
        buttonsLayout.childControlWidth = true;
        buttonsLayout.childControlHeight = true;
        buttonsLayout.childForceExpandWidth = true;

        var applyButton = new GameObject("Button_Apply", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        var applyRt = applyButton.GetComponent<RectTransform>();
        applyRt.SetParent(buttonsRowRt, false);
        applyButton.GetComponent<Image>().color = DesignTokens.Accent;
        var applyLayout = applyButton.GetComponent<LayoutElement>();
        applyLayout.minHeight = 40f;
        applyLayout.preferredHeight = 40f;
        var applyLabelObj = new GameObject("Label", typeof(RectTransform), typeof(Text));
        var applyLabelRt = applyLabelObj.GetComponent<RectTransform>();
        applyLabelRt.SetParent(applyRt, false);
        applyLabelRt.anchorMin = Vector2.zero;
        applyLabelRt.anchorMax = Vector2.one;
        applyLabelRt.offsetMin = Vector2.zero;
        applyLabelRt.offsetMax = Vector2.zero;
        var applyLabel = applyLabelObj.GetComponent<Text>();
        applyLabel.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        applyLabel.color = DesignTokens.Surface;
        applyLabel.fontSize = 14;
        applyLabel.alignment = TextAnchor.MiddleCenter;
        applyLabel.text = "\u8FFD\u52A0";

        var cancelButton = new GameObject("Button_Cancel", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        var cancelRt = cancelButton.GetComponent<RectTransform>();
        cancelRt.SetParent(buttonsRowRt, false);
        cancelButton.GetComponent<Image>().color = DesignTokens.BgSecondary;
        var cancelLayout = cancelButton.GetComponent<LayoutElement>();
        cancelLayout.minHeight = 40f;
        cancelLayout.preferredHeight = 40f;
        var cancelLabelObj = new GameObject("Label", typeof(RectTransform), typeof(Text));
        var cancelLabelRt = cancelLabelObj.GetComponent<RectTransform>();
        cancelLabelRt.SetParent(cancelRt, false);
        cancelLabelRt.anchorMin = Vector2.zero;
        cancelLabelRt.anchorMax = Vector2.one;
        cancelLabelRt.offsetMin = Vector2.zero;
        cancelLabelRt.offsetMax = Vector2.zero;
        var cancelLabel = cancelLabelObj.GetComponent<Text>();
        cancelLabel.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        cancelLabel.color = DesignTokens.TextPrimary;
        cancelLabel.fontSize = 14;
        cancelLabel.alignment = TextAnchor.MiddleCenter;
        cancelLabel.text = "\u30AD\u30E3\u30F3\u30BB\u30EB";

        newObjectSettingsPanel = overlayRt;
        newObjectNameInput = input;
        newObjectDescriptionInput = descriptionInput;
        newObjectApplyButton = applyButton.GetComponent<Button>();
        newObjectCancelButton = cancelButton.GetComponent<Button>();
        newObjectPathText = pathText;

        ApplyNewObjectSettingsDesign(newObjectSettingsPanel);
        newObjectSettingsPanel.gameObject.SetActive(false);
    }

    RectTransform ResolveNewObjectSettingsHost(RectTransform panel)
    {
        if (panel == null) return null;
        var root = panel.root as RectTransform;
        return root != null ? root : panel;
    }

    RectTransform FindExistingNewObjectSettingsPanel(RectTransform panel, RectTransform host)
    {
        if (newObjectSettingsPanel != null) return newObjectSettingsPanel;

        if (host != null)
        {
            var inHost = host.Find("Panel_NewObjectSettings") as RectTransform;
            if (inHost != null) return inHost;
        }

        if (panel != null && panel != host)
        {
            var inPanel = panel.Find("Panel_NewObjectSettings") as RectTransform;
            if (inPanel != null) return inPanel;
        }

        return null;
    }

    void ConfigureNewObjectSettingsOverlayRect(RectTransform overlayRt)
    {
        if (overlayRt == null) return;

        overlayRt.anchorMin = Vector2.zero;
        overlayRt.anchorMax = Vector2.one;
        overlayRt.pivot = new Vector2(0.5f, 0.5f);
        overlayRt.offsetMin = Vector2.zero;
        overlayRt.offsetMax = Vector2.zero;

        var overlayImage = overlayRt.GetComponent<Image>();
        if (overlayImage == null) overlayImage = overlayRt.gameObject.AddComponent<Image>();
        var overlayColor = DesignTokens.TextPrimary;
        overlayColor.a = 0.32f;
        overlayImage.color = overlayColor;
        overlayImage.raycastTarget = true;
    }

    void ConfigureNewObjectSettingsWindowRect(RectTransform windowRt)
    {
        if (windowRt == null) return;

        windowRt.anchorMin = new Vector2(0.5f, 0.5f);
        windowRt.anchorMax = new Vector2(0.5f, 0.5f);
        windowRt.pivot = new Vector2(0.5f, 0.5f);
        windowRt.sizeDelta = new Vector2(560f, 430f);
        windowRt.anchoredPosition = Vector2.zero;
    }

    void BindNewObjectSettingsReferences(RectTransform overlayRt)
    {
        if (overlayRt == null) return;

        var nameInputTr = overlayRt.Find("Window/Input_NewObjectName");
        if (nameInputTr != null) newObjectNameInput = nameInputTr.GetComponent<InputField>();

        var descInputTr = overlayRt.Find("Window/Input_NewObjectDescription");
        if (descInputTr != null) newObjectDescriptionInput = descInputTr.GetComponent<InputField>();

        var applyTr = overlayRt.Find("Window/ButtonsRow/Button_Apply");
        if (applyTr != null) newObjectApplyButton = applyTr.GetComponent<Button>();

        var cancelTr = overlayRt.Find("Window/ButtonsRow/Button_Cancel");
        if (cancelTr != null) newObjectCancelButton = cancelTr.GetComponent<Button>();

        var pathTextTr = overlayRt.Find("Window/Text_FilePath");
        if (pathTextTr != null) newObjectPathText = pathTextTr.GetComponent<Text>();
    }

    void ApplyNewObjectSettingsDesign(RectTransform overlayRt)
    {
        if (overlayRt == null) return;

        ConfigureNewObjectSettingsOverlayRect(overlayRt);

        var windowRt = overlayRt.Find("Window") as RectTransform;
        if (windowRt == null) return;

        ConfigureNewObjectSettingsWindowRect(windowRt);

        var windowImage = windowRt.GetComponent<Image>();
        if (windowImage == null) windowImage = windowRt.gameObject.AddComponent<Image>();
        windowImage.color = DesignTokens.Surface;

        var title = windowRt.Find("Text_Title")?.GetComponent<Text>();
        if (title != null)
        {
            title.text = "\u30AA\u30D6\u30B8\u30A7\u30AF\u30C8\u8A2D\u5B9A";
            title.fontSize = 16;
            title.color = DesignTokens.TextPrimary;
            title.alignment = TextAnchor.MiddleLeft;
            var rt = title.rectTransform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.offsetMin = new Vector2(16f, -44f);
            rt.offsetMax = new Vector2(-16f, -16f);
        }

        if (newObjectPathText != null)
        {
            newObjectPathText.fontSize = 12;
            newObjectPathText.color = DesignTokens.TextSecondary;
            newObjectPathText.alignment = TextAnchor.UpperLeft;
            newObjectPathText.horizontalOverflow = HorizontalWrapMode.Wrap;
            newObjectPathText.verticalOverflow = VerticalWrapMode.Truncate;
            var rt = newObjectPathText.rectTransform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.offsetMin = new Vector2(16f, -84f);
            rt.offsetMax = new Vector2(-16f, -52f);
        }

        var nameLabel = windowRt.Find("Text_NameLabel")?.GetComponent<Text>();
        if (nameLabel != null)
        {
            nameLabel.text = "\u30AA\u30D6\u30B8\u30A7\u30AF\u30C8\u540D";
            nameLabel.fontSize = 13;
            nameLabel.color = DesignTokens.TextPrimary;
            var rt = nameLabel.rectTransform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.offsetMin = new Vector2(16f, -116f);
            rt.offsetMax = new Vector2(-16f, -92f);
        }

        if (newObjectNameInput != null)
        {
            var rt = newObjectNameInput.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.anchorMin = new Vector2(0f, 1f);
                rt.anchorMax = new Vector2(1f, 1f);
                rt.offsetMin = new Vector2(16f, -160f);
                rt.offsetMax = new Vector2(-16f, -120f);
            }
        }

        var descriptionLabel = windowRt.Find("Text_DescriptionLabel")?.GetComponent<Text>();
        if (descriptionLabel != null)
        {
            descriptionLabel.text = "\u8AAC\u660E";
            descriptionLabel.fontSize = 13;
            descriptionLabel.color = DesignTokens.TextPrimary;
            var rt = descriptionLabel.rectTransform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.offsetMin = new Vector2(16f, -196f);
            rt.offsetMax = new Vector2(-16f, -172f);
        }

        if (newObjectDescriptionInput != null)
        {
            newObjectDescriptionInput.lineType = InputField.LineType.MultiLineNewline;
            var rt = newObjectDescriptionInput.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.anchorMin = new Vector2(0f, 1f);
                rt.anchorMax = new Vector2(1f, 1f);
                rt.offsetMin = new Vector2(16f, -324f);
                rt.offsetMax = new Vector2(-16f, -208f);
            }
        }

        var buttonsRowRt = windowRt.Find("ButtonsRow") as RectTransform;
        if (buttonsRowRt != null)
        {
            buttonsRowRt.anchorMin = new Vector2(0f, 0f);
            buttonsRowRt.anchorMax = new Vector2(1f, 0f);
            buttonsRowRt.offsetMin = new Vector2(16f, 16f);
            buttonsRowRt.offsetMax = new Vector2(-16f, 56f);
        }

        if (newObjectApplyButton != null)
        {
            var image = newObjectApplyButton.GetComponent<Image>();
            if (image != null) image.color = DesignTokens.Accent;
            var layout = newObjectApplyButton.GetComponent<LayoutElement>();
            if (layout == null) layout = newObjectApplyButton.gameObject.AddComponent<LayoutElement>();
            layout.minHeight = 40f;
            layout.preferredHeight = 40f;
            var label = newObjectApplyButton.GetComponentInChildren<Text>(true);
            if (label != null)
            {
                label.text = "\u8FFD\u52A0";
                label.fontSize = 14;
                label.color = DesignTokens.Surface;
                label.alignment = TextAnchor.MiddleCenter;
            }
        }

        if (newObjectCancelButton != null)
        {
            var image = newObjectCancelButton.GetComponent<Image>();
            if (image != null) image.color = DesignTokens.BgSecondary;
            var layout = newObjectCancelButton.GetComponent<LayoutElement>();
            if (layout == null) layout = newObjectCancelButton.gameObject.AddComponent<LayoutElement>();
            layout.minHeight = 40f;
            layout.preferredHeight = 40f;
            var label = newObjectCancelButton.GetComponentInChildren<Text>(true);
            if (label != null)
            {
                label.text = "\u30AD\u30E3\u30F3\u30BB\u30EB";
                label.fontSize = 14;
                label.color = DesignTokens.TextPrimary;
                label.alignment = TextAnchor.MiddleCenter;
            }
        }

        UiRoundedTheme.ApplyToHierarchy(overlayRt, cornerRadius);
    }

    void EnsureScrollBottomPadding(float bottomPaddingMin)
    {
        if (content == null) return;

        var scroll = content.parent != null ? content.parent.parent as RectTransform : null;
        if (scroll == null) return;

        var offsetMin = scroll.offsetMin;
        if (offsetMin.y < bottomPaddingMin)
        {
            offsetMin.y = bottomPaddingMin;
            scroll.offsetMin = offsetMin;
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
            var matchesType = card.typeId.IndexOf(normalized, StringComparison.OrdinalIgnoreCase) >= 0;
            var matchesLabel = !string.IsNullOrWhiteSpace(card.displayLabel) &&
                               card.displayLabel.IndexOf(normalized, StringComparison.OrdinalIgnoreCase) >= 0;
            var matchesDescription = !string.IsNullOrWhiteSpace(card.displayDescription) &&
                                     card.displayDescription.IndexOf(normalized, StringComparison.OrdinalIgnoreCase) >= 0;
            var visible = showAll || matchesType || matchesLabel || matchesDescription;
            card.root.SetActive(visible);
        }
    }

    void OnClickAdd()
    {
#if UNITY_EDITOR
        EnsureRuntimeBindings();
        EnsureRuntimeCatalogControls();
        WireUiEvents();

        if (placementController == null)
        {
            SetStatus("PlacementController is not found.");
            return;
        }

        var selectedPath = EditorUtility.OpenFilePanel("Select FBX", GetDefaultFbxDirectory(), "fbx");
        if (string.IsNullOrWhiteSpace(selectedPath))
        {
            SetStatus("FBX selection canceled.");
            return;
        }

        if (!TryLoadFbxAsset(selectedPath, out var prefab, out var assetPath, out var errorMessage))
        {
            SetStatus(errorMessage);
            return;
        }

        OpenNewObjectSettings(prefab, assetPath);
#else
        SetStatus("FBX import is available in Unity Editor only.");
#endif
    }

    void OpenNewObjectSettings(GameObject prefab, string assetPath)
    {
        if (prefab == null || string.IsNullOrWhiteSpace(assetPath))
        {
            SetStatus("Failed to prepare new object settings.");
            return;
        }

        pendingImportedPrefab = prefab;
        pendingImportedAssetPath = assetPath;

        if (newObjectNameInput != null)
        {
            var defaultName = GetDefaultNewObjectNameFromAssetPath(assetPath);
            if (string.IsNullOrWhiteSpace(defaultName))
            {
                defaultName = importedCardLabel;
            }

            newObjectNameInput.text = defaultName;
        }

        if (newObjectDescriptionInput != null)
        {
            newObjectDescriptionInput.text = string.Empty;
        }

        if (newObjectPathText != null)
        {
            newObjectPathText.text = $"FBX: {assetPath}";
        }

        if (newObjectSettingsPanel != null)
        {
            ApplyNewObjectSettingsDesign(newObjectSettingsPanel);
            newObjectSettingsPanel.gameObject.SetActive(true);
        }

        SetStatus("Open object settings.");
    }

    void OnClickApplyNewObjectSettings()
    {
        if (pendingImportedPrefab == null || string.IsNullOrWhiteSpace(pendingImportedAssetPath))
        {
            SetStatus("No imported FBX is pending.");
            CloseNewObjectSettings(clearPending: true);
            return;
        }

        if (placementController == null)
        {
            SetStatus("PlacementController is not found.");
            return;
        }

        var displayLabel = importedCardLabel;
        if (newObjectNameInput != null)
        {
            var typed = newObjectNameInput.text?.Trim();
            if (!string.IsNullOrWhiteSpace(typed))
            {
                displayLabel = typed;
            }
        }

        var typeId = BuildImportedTypeId(pendingImportedAssetPath, displayLabel);
        if (!placementController.RegisterRuntimePrefab(typeId, pendingImportedPrefab))
        {
            SetStatus("Failed to register imported FBX.");
            return;
        }

        runtimeImportedTypeId = typeId;
        runtimeImportedPrefab = pendingImportedPrefab;
        runtimeImportedCardLabel = displayLabel;
        runtimeImportedDescription = newObjectDescriptionInput != null
            ? (newObjectDescriptionInput.text ?? string.Empty).Trim()
            : string.Empty;

        if (searchInput != null) searchInput.text = string.Empty;
        RebuildCards();

        CloseNewObjectSettings(clearPending: true);
        SetStatus("New object card added.");
    }

    void OnClickCancelNewObjectSettings()
    {
        CloseNewObjectSettings(clearPending: true);
        SetStatus("Object settings canceled.");
    }

    void CloseNewObjectSettings(bool clearPending)
    {
        if (newObjectSettingsPanel != null)
        {
            newObjectSettingsPanel.gameObject.SetActive(false);
        }

        if (clearPending)
        {
            pendingImportedPrefab = null;
            pendingImportedAssetPath = null;
        }
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
        root.GetComponent<Image>().color = DesignTokens.BgPrimary;
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
        text.color = DesignTokens.TextPrimary;
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
        placeholder.color = DesignTokens.TextTertiary;
        placeholder.fontSize = 14;
        placeholder.alignment = TextAnchor.MiddleLeft;
        placeholder.text = placeholderText;

        input.textComponent = text;
        input.placeholder = placeholder;
        return input;
    }

    InputField CreateMultilineInputField(Transform parent, string name, string placeholderText)
    {
        var input = CreateInputField(parent, name, placeholderText);
        if (input == null) return null;

        input.lineType = InputField.LineType.MultiLineNewline;

        if (input.textComponent != null)
        {
            input.textComponent.alignment = TextAnchor.UpperLeft;
            var textRt = input.textComponent.rectTransform;
            textRt.offsetMin = new Vector2(8f, 8f);
            textRt.offsetMax = new Vector2(-8f, -8f);
        }

        if (input.placeholder is Text placeholderTextComp)
        {
            placeholderTextComp.alignment = TextAnchor.UpperLeft;
            var placeholderRt = placeholderTextComp.rectTransform;
            placeholderRt.offsetMin = new Vector2(8f, 8f);
            placeholderRt.offsetMax = new Vector2(-8f, -8f);
        }

        return input;
    }

#if UNITY_EDITOR
    static string GetDefaultFbxDirectory()
    {
        var importedRoot = Path.Combine(Application.dataPath, "ImportedFbx");
        if (!Directory.Exists(importedRoot))
        {
            Directory.CreateDirectory(importedRoot);
        }

        return importedRoot;
    }

    static bool TryLoadFbxAsset(string absolutePath, out GameObject prefab, out string assetPath, out string errorMessage)
    {
        prefab = null;
        assetPath = null;
        errorMessage = null;

        if (string.IsNullOrWhiteSpace(absolutePath))
        {
            errorMessage = "FBX path is empty.";
            return false;
        }

        if (!string.Equals(Path.GetExtension(absolutePath), ".fbx", StringComparison.OrdinalIgnoreCase))
        {
            errorMessage = "Please select an .fbx file.";
            return false;
        }

        if (!File.Exists(absolutePath))
        {
            errorMessage = "Selected FBX file does not exist.";
            return false;
        }

        if (!TryToAssetPath(absolutePath, out assetPath))
        {
            var targetDir = EnsureImportedAssetFolders();
            var fileStem = SanitizeName(Path.GetFileNameWithoutExtension(absolutePath));
            if (string.IsNullOrWhiteSpace(fileStem))
            {
                fileStem = "ImportedModel";
            }

            var uniqueSuffix = DateTime.UtcNow.Ticks.ToString();
            assetPath = $"{targetDir}/{fileStem}_{uniqueSuffix}.fbx";
            FileUtil.CopyFileOrDirectory(absolutePath, assetPath);
        }

        AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
        prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
        if (prefab == null)
        {
            errorMessage = "Failed to import selected FBX.";
            return false;
        }

        return true;
    }

    static string EnsureImportedAssetFolders()
    {
        const string rootFolder = "Assets/ImportedFbx";
        if (!AssetDatabase.IsValidFolder(rootFolder))
        {
            AssetDatabase.CreateFolder("Assets", "ImportedFbx");
        }

        return rootFolder;
    }

    static bool TryToAssetPath(string absolutePath, out string assetPath)
    {
        assetPath = null;

        var normalizedAbsolute = NormalizePath(Path.GetFullPath(absolutePath));
        var normalizedDataPath = NormalizePath(Application.dataPath);
        if (!normalizedAbsolute.StartsWith(normalizedDataPath, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var tail = normalizedAbsolute.Substring(normalizedDataPath.Length);
        assetPath = "Assets" + tail;
        return true;
    }

    static string NormalizePath(string path)
    {
        return path.Replace('\\', '/');
    }

    static string BuildImportedTypeId(string assetPath, string displayLabel)
    {
        var stem = string.IsNullOrWhiteSpace(displayLabel)
            ? Path.GetFileNameWithoutExtension(assetPath)
            : displayLabel;
        var sanitized = SanitizeName(stem);
        if (string.IsNullOrWhiteSpace(sanitized))
        {
            sanitized = "Model";
        }

        return $"Imported/{sanitized}_{DateTime.UtcNow.Ticks}";
    }

    static string GetDefaultNewObjectNameFromAssetPath(string assetPath)
    {
        if (string.IsNullOrWhiteSpace(assetPath)) return string.Empty;

        var stem = Path.GetFileNameWithoutExtension(assetPath);
        if (string.IsNullOrWhiteSpace(stem)) return string.Empty;

        return stem;
    }

    static string SanitizeName(string source)
    {
        if (string.IsNullOrWhiteSpace(source)) return string.Empty;

        var chars = source.ToCharArray();
        for (int i = 0; i < chars.Length; i++)
        {
            var ch = chars[i];
            var valid = char.IsLetterOrDigit(ch) || ch == '_' || ch == '-';
            if (!valid)
            {
                chars[i] = '_';
            }
        }

        return new string(chars).Trim('_');
    }
#endif
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
