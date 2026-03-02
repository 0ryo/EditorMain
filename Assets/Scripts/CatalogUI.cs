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
    [SerializeField] RectTransform editModeRow;
    [SerializeField] Button browseModeButton;
    [SerializeField] Button transformModeButton;
    [SerializeField] Button scaleModeButton;
    [SerializeField] Button settingsButton;
    [SerializeField] RectTransform settingsPanel;
    [SerializeField] Button settingsTabGeneralButton;
    [SerializeField] Button settingsTabIntegrationButton;
    [SerializeField] Button settingsTabAccountButton;
    [SerializeField] RectTransform settingsWindow;
    [SerializeField] RectTransform settingsGeneralContent;
    [SerializeField] RectTransform settingsIntegrationContent;
    [SerializeField] RectTransform settingsAccountContent;
    [SerializeField] Slider settingsSensitivitySlider;
    [SerializeField] Text settingsSensitivityValueText;
    [SerializeField] Button settingsIntegrationLinkButton;
    [SerializeField] Button settingsApplyButton;
    [SerializeField] Text settingsAccountUserNameText;
    [SerializeField] Text settingsAccountEmailText;
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
    EditModeService boundEditModeService;
    EditorCameraController settingsCameraController;
    float settingsBaseOrbitSpeed = -1f;
    float settingsBasePanSpeed = -1f;
    float settingsBaseZoomSpeed = -1f;
    float settingsBaseOrthographicZoomSpeed = -1f;
    bool settingsHasPendingChanges;
    bool settingsInitializingUi;
    SettingsTab activeSettingsTab = SettingsTab.General;

    enum SettingsTab
    {
        General,
        Integration,
        Account
    }

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
        EnsureEditModeServiceBinding();
        EnsureContentTopAligned();
        EnsureTemplateCardHeight();
        WireUiEvents();
        RebuildCards();
        ApplyRoundedTheme();
        DesignTokenApplier.ApplyCatalogPanel(transform);
        RefreshModeButtons();
    }

    void OnDestroy()
    {
        UnbindEditModeService();
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

        if (browseModeButton != null)
        {
            browseModeButton.onClick.RemoveListener(OnClickModeBrowse);
            browseModeButton.onClick.AddListener(OnClickModeBrowse);
        }

        if (transformModeButton != null)
        {
            transformModeButton.onClick.RemoveListener(OnClickModeTransform);
            transformModeButton.onClick.AddListener(OnClickModeTransform);
        }

        if (scaleModeButton != null)
        {
            scaleModeButton.onClick.RemoveListener(OnClickModeScale);
            scaleModeButton.onClick.AddListener(OnClickModeScale);
        }

        if (settingsButton != null)
        {
            settingsButton.onClick.RemoveListener(OnClickSettings);
            settingsButton.onClick.AddListener(OnClickSettings);
        }

        if (settingsTabGeneralButton != null)
        {
            settingsTabGeneralButton.onClick.RemoveListener(OnClickSettingsTabGeneral);
            settingsTabGeneralButton.onClick.AddListener(OnClickSettingsTabGeneral);
        }

        if (settingsTabIntegrationButton != null)
        {
            settingsTabIntegrationButton.onClick.RemoveListener(OnClickSettingsTabIntegration);
            settingsTabIntegrationButton.onClick.AddListener(OnClickSettingsTabIntegration);
        }

        if (settingsTabAccountButton != null)
        {
            settingsTabAccountButton.onClick.RemoveListener(OnClickSettingsTabAccount);
            settingsTabAccountButton.onClick.AddListener(OnClickSettingsTabAccount);
        }

        if (settingsSensitivitySlider != null)
        {
            settingsSensitivitySlider.onValueChanged.RemoveListener(OnSettingsSensitivityChanged);
            settingsSensitivitySlider.onValueChanged.AddListener(OnSettingsSensitivityChanged);
        }

        if (settingsIntegrationLinkButton != null)
        {
            settingsIntegrationLinkButton.onClick.RemoveListener(OnClickSettingsIntegrationLink);
            settingsIntegrationLinkButton.onClick.AddListener(OnClickSettingsIntegrationLink);
        }

        if (settingsApplyButton != null)
        {
            settingsApplyButton.onClick.RemoveListener(OnClickSettingsApply);
            settingsApplyButton.onClick.AddListener(OnClickSettingsApply);
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

        RefreshModeButtons();
        RefreshSettingsTabs();
        UpdateSettingsApplyButtonVisibility();
    }

    void RebuildCards()
    {
        EnsureRuntimeBindings();
        EnsureEditModeServiceBinding();
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
        RefreshModeButtons();
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
        if (editModeRow != null)
        {
            UiRoundedTheme.ApplyToHierarchy(editModeRow, cornerRadius);
        }
        if (settingsButton != null)
        {
            UiRoundedTheme.ApplyToHierarchy(settingsButton.transform, cornerRadius);
        }
        if (settingsPanel != null)
        {
            UiRoundedTheme.ApplyToHierarchy(settingsPanel, cornerRadius);
        }
    }

    void EnsureRuntimeCatalogControls()
    {
        var panel = transform as RectTransform;
        if (panel == null) return;

        EnsureRuntimeEditModeButtons(panel);
        EnsureRuntimeSettingsButton(panel);
        EnsureRuntimeSettingsDialog(panel);
        EnsureEditModeDockSync(panel);
        EnsureRuntimeSearchInput(panel);
        EnsureRuntimeBottomAddButton(panel);
        EnsureRuntimeNewObjectSettingsDialog(panel);
        ApplyCatalogTopLayout(panel);
        EnsureScrollBottomPadding(56f);
        RefreshModeButtons();
    }

    void EnsureEditModeDockSync(RectTransform panel)
    {
        if (panel == null || editModeRow == null) return;

        var root = panel.root;
        if (root == null) return;

        var dockSync = root.GetComponent<UiPanelDockSync>();
        if (dockSync == null) return;

        if (dockSync.editModePanel == null)
        {
            dockSync.editModePanel = editModeRow;
        }

        if (settingsButton != null && dockSync.settingsButtonPanel == null)
        {
            dockSync.settingsButtonPanel = settingsButton.transform as RectTransform;
        }
    }

    void EnsureRuntimeEditModeButtons(RectTransform panel)
    {
        if (panel == null) return;
        var host = panel.root as RectTransform;
        if (host == null) host = panel;

        if (editModeRow == null)
        {
            editModeRow = host.Find("EditModeRow") as RectTransform;
            if (editModeRow == null)
            {
                editModeRow = host.Find("EditModeRow_Runtime") as RectTransform;
            }

            if (editModeRow == null)
            {
                var legacyRow = panel.Find("EditModeRow") as RectTransform;
                if (legacyRow == null) legacyRow = panel.Find("EditModeRow_Runtime") as RectTransform;
                if (legacyRow != null)
                {
                    editModeRow = legacyRow;
                    if (editModeRow.parent != host)
                    {
                        editModeRow.SetParent(host, false);
                    }
                }
            }
        }

        if (editModeRow == null)
        {
            var rowGo = new GameObject("EditModeRow_Runtime", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            editModeRow = rowGo.GetComponent<RectTransform>();
            editModeRow.SetParent(host, false);
        }

        var staleLegacyRow = panel.Find("EditModeRow") as RectTransform;
        if (staleLegacyRow == null) staleLegacyRow = panel.Find("EditModeRow_Runtime") as RectTransform;
        if (staleLegacyRow != null && staleLegacyRow != editModeRow)
        {
            staleLegacyRow.gameObject.SetActive(false);
        }

        var layout = editModeRow.GetComponent<HorizontalLayoutGroup>();
        if (layout == null) layout = editModeRow.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 8f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = true;
        layout.padding = new RectOffset(0, 0, 0, 0);

        browseModeButton = FindOrCreateModeButton(editModeRow, browseModeButton, "Button_ModeBrowse", "\u95b2\u89a7");
        transformModeButton = FindOrCreateModeButton(editModeRow, transformModeButton, "Button_ModeTransform", "\u79fb\u52d5");
        scaleModeButton = FindOrCreateModeButton(editModeRow, scaleModeButton, "Button_ModeScale", "\u30b9\u30b1\u30fc\u30eb");
    }

    Button FindOrCreateModeButton(RectTransform row, Button existingButton, string objectName, string labelText)
    {
        if (existingButton != null) return existingButton;

        var found = row.Find(objectName);
        if (found != null)
        {
            var foundButton = found.GetComponent<Button>();
            if (foundButton != null)
            {
                EnsureModeButtonAppearance(foundButton, labelText);
                return foundButton;
            }
        }

        var buttonGo = new GameObject(objectName, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        var buttonRt = buttonGo.GetComponent<RectTransform>();
        buttonRt.SetParent(row, false);

        var layout = buttonGo.GetComponent<LayoutElement>();
        layout.minHeight = 40f;
        layout.preferredHeight = 40f;
        layout.minWidth = 70f;
        layout.preferredWidth = 70f;
        layout.flexibleWidth = 1f;

        var button = buttonGo.GetComponent<Button>();
        EnsureModeButtonAppearance(button, labelText);
        return button;
    }

    void EnsureModeButtonAppearance(Button button, string labelText)
    {
        if (button == null) return;

        var image = button.GetComponent<Image>();
        if (image != null) image.color = DesignTokens.BgSecondary;

        var label = button.GetComponentInChildren<Text>(true);
        if (label == null)
        {
            var labelGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
            var labelRt = labelGo.GetComponent<RectTransform>();
            labelRt.SetParent(button.transform, false);
            labelRt.anchorMin = Vector2.zero;
            labelRt.anchorMax = Vector2.one;
            labelRt.offsetMin = Vector2.zero;
            labelRt.offsetMax = Vector2.zero;
            label = labelGo.GetComponent<Text>();
        }

        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.fontSize = 12;
        label.alignment = TextAnchor.MiddleCenter;
        label.text = labelText;
        label.color = DesignTokens.TextPrimary;
    }

    void EnsureRuntimeSettingsButton(RectTransform panel)
    {
        if (panel == null) return;
        var host = panel.root as RectTransform;
        if (host == null) host = panel;

        if (settingsButton == null)
        {
            var found = host.Find("Button_Settings") as RectTransform;
            if (found == null) found = host.Find("Button_Settings_Runtime") as RectTransform;
            if (found == null && panel != host)
            {
                found = panel.Find("Button_Settings") as RectTransform;
                if (found == null) found = panel.Find("Button_Settings_Runtime") as RectTransform;
            }

            if (found != null)
            {
                settingsButton = found.GetComponent<Button>();
                if (settingsButton != null && found.parent != host)
                {
                    found.SetParent(host, false);
                }
            }
        }

        if (settingsButton == null)
        {
            var go = new GameObject("Button_Settings_Runtime", typeof(RectTransform), typeof(Image), typeof(Button));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(host, false);
            settingsButton = go.GetComponent<Button>();
        }

        var buttonRt = settingsButton.transform as RectTransform;
        if (buttonRt != null)
        {
            buttonRt.anchorMin = new Vector2(1f, 1f);
            buttonRt.anchorMax = new Vector2(1f, 1f);
            buttonRt.pivot = new Vector2(1f, 1f);
            buttonRt.offsetMin = new Vector2(-82f, -52f);
            buttonRt.offsetMax = new Vector2(-12f, -12f);
        }

        EnsureSettingsButtonAppearance(settingsButton);
    }

    void EnsureSettingsButtonAppearance(Button button)
    {
        if (button == null) return;

        var image = button.GetComponent<Image>();
        if (image != null)
        {
            image.color = DesignTokens.BgSecondary;
            button.targetGraphic = image;
        }

        var label = button.GetComponentInChildren<Text>(true);
        if (label == null)
        {
            var labelGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
            var labelRt = labelGo.GetComponent<RectTransform>();
            labelRt.SetParent(button.transform, false);
            labelRt.anchorMin = Vector2.zero;
            labelRt.anchorMax = Vector2.one;
            labelRt.offsetMin = Vector2.zero;
            labelRt.offsetMax = Vector2.zero;
            label = labelGo.GetComponent<Text>();
        }

        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.fontSize = 22;
        label.alignment = TextAnchor.MiddleCenter;
        label.text = "\u2699";
        label.color = DesignTokens.TextPrimary;
    }

    void EnsureRuntimeSettingsDialog(RectTransform panel)
    {
        var host = ResolveSettingsHost(panel);
        if (host == null) return;

        var existing = FindExistingSettingsPanel(panel, host);
        RectTransform overlayRt = existing;
        if (overlayRt == null)
        {
            var overlayRoot = new GameObject("Panel_Settings", typeof(RectTransform), typeof(Image));
            overlayRt = overlayRoot.GetComponent<RectTransform>();
            overlayRt.SetParent(host, false);
        }
        else if (overlayRt.parent != host)
        {
            overlayRt.SetParent(host, false);
        }

        settingsPanel = overlayRt;
        ApplySettingsPanelDesign(settingsPanel);
        BindSettingsReferences(settingsPanel);
        settingsPanel.gameObject.SetActive(false);
    }

    RectTransform ResolveSettingsHost(RectTransform panel)
    {
        if (panel == null) return null;
        var root = panel.root as RectTransform;
        return root != null ? root : panel;
    }

    RectTransform FindExistingSettingsPanel(RectTransform panel, RectTransform host)
    {
        if (settingsPanel != null) return settingsPanel;

        if (host != null)
        {
            var inHost = host.Find("Panel_Settings") as RectTransform;
            if (inHost == null) inHost = host.Find("Panel_Settings_Runtime") as RectTransform;
            if (inHost != null) return inHost;
        }

        if (panel != null && panel != host)
        {
            var inPanel = panel.Find("Panel_Settings") as RectTransform;
            if (inPanel == null) inPanel = panel.Find("Panel_Settings_Runtime") as RectTransform;
            if (inPanel != null) return inPanel;
        }

        return null;
    }

    void BindSettingsReferences(RectTransform overlayRt)
    {
        if (overlayRt == null) return;

        var generalTabTr = overlayRt.Find("Window/Tabs/Tab_General");
        if (generalTabTr != null) settingsTabGeneralButton = generalTabTr.GetComponent<Button>();

        var integrationTabTr = overlayRt.Find("Window/Tabs/Tab_Integration");
        if (integrationTabTr != null) settingsTabIntegrationButton = integrationTabTr.GetComponent<Button>();

        var accountTabTr = overlayRt.Find("Window/Tabs/Tab_Account");
        if (accountTabTr != null) settingsTabAccountButton = accountTabTr.GetComponent<Button>();

        settingsGeneralContent = overlayRt.Find("Window/Content/Content_General") as RectTransform;
        settingsIntegrationContent = overlayRt.Find("Window/Content/Content_Integration") as RectTransform;
        settingsAccountContent = overlayRt.Find("Window/Content/Content_Account") as RectTransform;
        settingsWindow = overlayRt.Find("Window") as RectTransform;

        var sliderTr = overlayRt.Find("Window/Content/Content_General/SliderRow/Slider_Sensitivity");
        if (sliderTr != null) settingsSensitivitySlider = sliderTr.GetComponent<Slider>();

        var sliderValueTr = overlayRt.Find("Window/Content/Content_General/SliderRow/Text_SensitivityValue");
        if (sliderValueTr != null) settingsSensitivityValueText = sliderValueTr.GetComponent<Text>();

        var linkTr = overlayRt.Find("Window/Content/Content_Integration/Button_WebLink");
        if (linkTr != null) settingsIntegrationLinkButton = linkTr.GetComponent<Button>();

        var applyTr = overlayRt.Find("Window/Button_ApplySettings");
        if (applyTr != null) settingsApplyButton = applyTr.GetComponent<Button>();

        var userNameTr = overlayRt.Find("Window/Content/Content_Account/Text_UserName");
        if (userNameTr != null) settingsAccountUserNameText = userNameTr.GetComponent<Text>();

        var emailTr = overlayRt.Find("Window/Content/Content_Account/Text_Email");
        if (emailTr != null) settingsAccountEmailText = emailTr.GetComponent<Text>();
    }

    void ApplySettingsPanelDesign(RectTransform overlayRt)
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

        var windowRt = FindOrCreateSettingsRect(overlayRt, "Window");
        settingsWindow = windowRt;
        windowRt.anchorMin = new Vector2(0.5f, 0.5f);
        windowRt.anchorMax = new Vector2(0.5f, 0.5f);
        windowRt.pivot = new Vector2(0.5f, 0.5f);
        windowRt.sizeDelta = new Vector2(760f, 460f);
        windowRt.anchoredPosition = Vector2.zero;

        var windowImage = windowRt.GetComponent<Image>();
        if (windowImage == null) windowImage = windowRt.gameObject.AddComponent<Image>();
        windowImage.color = DesignTokens.Surface;

        var tabsRt = FindOrCreateSettingsRect(windowRt, "Tabs");
        tabsRt.anchorMin = new Vector2(0f, 0f);
        tabsRt.anchorMax = new Vector2(0f, 1f);
        tabsRt.pivot = new Vector2(0f, 1f);
        tabsRt.offsetMin = new Vector2(16f, 16f);
        tabsRt.offsetMax = new Vector2(172f, -16f);
        var tabsImage = tabsRt.GetComponent<Image>();
        if (tabsImage == null) tabsImage = tabsRt.gameObject.AddComponent<Image>();
        tabsImage.color = DesignTokens.BgPrimary;
        var tabsLayout = tabsRt.GetComponent<VerticalLayoutGroup>();
        if (tabsLayout == null) tabsLayout = tabsRt.gameObject.AddComponent<VerticalLayoutGroup>();
        tabsLayout.spacing = 8f;
        tabsLayout.padding = new RectOffset(8, 8, 8, 8);
        tabsLayout.childControlWidth = true;
        tabsLayout.childControlHeight = false;
        tabsLayout.childForceExpandWidth = true;
        tabsLayout.childForceExpandHeight = false;

        settingsTabGeneralButton = EnsureSettingsTabButton(tabsRt, settingsTabGeneralButton, "Tab_General", "一般");
        settingsTabIntegrationButton = EnsureSettingsTabButton(tabsRt, settingsTabIntegrationButton, "Tab_Integration", "連携");
        settingsTabAccountButton = EnsureSettingsTabButton(tabsRt, settingsTabAccountButton, "Tab_Account", "アカウント");

        var contentRt = FindOrCreateSettingsRect(windowRt, "Content");
        contentRt.anchorMin = new Vector2(0f, 0f);
        contentRt.anchorMax = new Vector2(1f, 1f);
        contentRt.pivot = new Vector2(0f, 1f);
        contentRt.offsetMin = new Vector2(188f, 16f);
        contentRt.offsetMax = new Vector2(-16f, -16f);
        var contentImage = contentRt.GetComponent<Image>();
        if (contentImage == null) contentImage = contentRt.gameObject.AddComponent<Image>();
        contentImage.color = DesignTokens.BgPrimary;

        settingsGeneralContent = FindOrCreateSettingsRect(contentRt, "Content_General");
        settingsIntegrationContent = FindOrCreateSettingsRect(contentRt, "Content_Integration");
        settingsAccountContent = FindOrCreateSettingsRect(contentRt, "Content_Account");

        EnsureGeneralSettingsContent(settingsGeneralContent);
        EnsureIntegrationSettingsContent(settingsIntegrationContent);
        EnsureAccountSettingsContent(settingsAccountContent);
        settingsApplyButton = FindOrCreateSettingsButton(windowRt, settingsApplyButton, "Button_ApplySettings", "適用");
        if (settingsApplyButton != null)
        {
            var applyRt = settingsApplyButton.transform as RectTransform;
            if (applyRt != null)
            {
                applyRt.anchorMin = new Vector2(1f, 0f);
                applyRt.anchorMax = new Vector2(1f, 0f);
                applyRt.pivot = new Vector2(1f, 0f);
                applyRt.sizeDelta = new Vector2(136f, 40f);
                applyRt.anchoredPosition = new Vector2(-16f, 16f);
            }

            var applyImage = settingsApplyButton.GetComponent<Image>();
            if (applyImage != null) applyImage.color = DesignTokens.Accent;

            var applyLabel = settingsApplyButton.GetComponentInChildren<Text>(true);
            if (applyLabel != null)
            {
                applyLabel.fontSize = 14;
                applyLabel.alignment = TextAnchor.MiddleCenter;
                applyLabel.color = DesignTokens.Surface;
                applyLabel.text = "適用";
            }
        }
        BindSettingsReferences(overlayRt);

        if (settingsSensitivitySlider != null)
        {
            settingsSensitivitySlider.minValue = 0.2f;
            settingsSensitivitySlider.maxValue = 2.5f;
            if (settingsSensitivitySlider.value < settingsSensitivitySlider.minValue ||
                settingsSensitivitySlider.value > settingsSensitivitySlider.maxValue)
            {
                settingsSensitivitySlider.SetValueWithoutNotify(1f);
            }
        }

        if (settingsAccountUserNameText != null)
        {
            settingsAccountUserNameText.text = "User Name";
            settingsAccountUserNameText.fontSize = 28;
            settingsAccountUserNameText.color = DesignTokens.TextPrimary;
            settingsAccountUserNameText.alignment = TextAnchor.MiddleCenter;
        }

        if (settingsAccountEmailText != null)
        {
            settingsAccountEmailText.text = "user@example.com";
            settingsAccountEmailText.fontSize = 14;
            settingsAccountEmailText.color = DesignTokens.TextSecondary;
            settingsAccountEmailText.alignment = TextAnchor.MiddleCenter;
        }

        EnsureSettingsCameraBinding();
        settingsInitializingUi = true;
        RefreshSettingsTabs();
        if (settingsSensitivitySlider != null)
        {
            OnSettingsSensitivityChanged(settingsSensitivitySlider.value);
        }
        settingsInitializingUi = false;
        UpdateSettingsApplyButtonVisibility();

        EnsureSettingsOverlayCloseHandler(overlayRt, windowRt);

        UiRoundedTheme.ApplyToHierarchy(overlayRt, cornerRadius);
    }

    void EnsureSettingsOverlayCloseHandler(RectTransform overlayRt, RectTransform windowRt)
    {
        if (overlayRt == null || windowRt == null) return;

        var closer = overlayRt.GetComponent<SettingsOverlayClickCatcher>();
        if (closer == null) closer = overlayRt.gameObject.AddComponent<SettingsOverlayClickCatcher>();
        closer.Configure(this, windowRt);
    }

    RectTransform FindOrCreateSettingsRect(Transform parent, string name)
    {
        if (parent == null) return null;

        var found = parent.Find(name) as RectTransform;
        if (found != null) return found;

        var go = new GameObject(name, typeof(RectTransform));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        return rt;
    }

    Button EnsureSettingsTabButton(RectTransform parent, Button button, string objectName, string labelText)
    {
        if (parent == null) return button;

        if (button == null)
        {
            var found = parent.Find(objectName);
            if (found != null) button = found.GetComponent<Button>();
        }

        if (button == null)
        {
            var go = new GameObject(objectName, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            button = go.GetComponent<Button>();
        }

        var layout = button.GetComponent<LayoutElement>();
        if (layout == null) layout = button.gameObject.AddComponent<LayoutElement>();
        layout.minHeight = 40f;
        layout.preferredHeight = 40f;
        layout.flexibleHeight = 0f;

        var image = button.GetComponent<Image>();
        if (image == null) image = button.gameObject.AddComponent<Image>();
        image.color = DesignTokens.BgSecondary;

        var label = button.GetComponentInChildren<Text>(true);
        if (label == null)
        {
            var labelGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
            var labelRt = labelGo.GetComponent<RectTransform>();
            labelRt.SetParent(button.transform, false);
            labelRt.anchorMin = Vector2.zero;
            labelRt.anchorMax = Vector2.one;
            labelRt.offsetMin = Vector2.zero;
            labelRt.offsetMax = Vector2.zero;
            label = labelGo.GetComponent<Text>();
        }

        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.text = labelText;
        label.fontSize = 14;
        label.alignment = TextAnchor.MiddleCenter;
        label.color = DesignTokens.TextPrimary;
        return button;
    }

    void EnsureGeneralSettingsContent(RectTransform contentRt)
    {
        if (contentRt == null) return;
        contentRt.anchorMin = Vector2.zero;
        contentRt.anchorMax = Vector2.one;
        contentRt.offsetMin = Vector2.zero;
        contentRt.offsetMax = Vector2.zero;

        var title = FindOrCreateSettingsText(contentRt, "Text_Title", "視点操作感度");
        title.fontSize = 16;
        title.alignment = TextAnchor.MiddleLeft;
        title.color = DesignTokens.TextPrimary;
        var titleRt = title.rectTransform;
        titleRt.anchorMin = new Vector2(0f, 1f);
        titleRt.anchorMax = new Vector2(1f, 1f);
        titleRt.offsetMin = new Vector2(24f, -56f);
        titleRt.offsetMax = new Vector2(-24f, -24f);

        var sliderRow = FindOrCreateSettingsRect(contentRt, "SliderRow");
        sliderRow.anchorMin = new Vector2(0f, 1f);
        sliderRow.anchorMax = new Vector2(1f, 1f);
        sliderRow.offsetMin = new Vector2(24f, -116f);
        sliderRow.offsetMax = new Vector2(-24f, -76f);

        settingsSensitivitySlider = EnsureSettingsSlider(sliderRow, settingsSensitivitySlider, "Slider_Sensitivity");
        if (settingsSensitivitySlider != null)
        {
            var sliderRt = settingsSensitivitySlider.transform as RectTransform;
            if (sliderRt != null)
            {
                sliderRt.anchorMin = new Vector2(0f, 0f);
                sliderRt.anchorMax = new Vector2(1f, 1f);
                sliderRt.offsetMin = new Vector2(0f, 0f);
                sliderRt.offsetMax = new Vector2(-90f, 0f);
            }
        }

        settingsSensitivityValueText = FindOrCreateSettingsText(sliderRow, "Text_SensitivityValue", "1.00x");
        settingsSensitivityValueText.fontSize = 14;
        settingsSensitivityValueText.alignment = TextAnchor.MiddleRight;
        settingsSensitivityValueText.color = DesignTokens.TextSecondary;
        var valueRt = settingsSensitivityValueText.rectTransform;
        valueRt.anchorMin = new Vector2(1f, 0f);
        valueRt.anchorMax = new Vector2(1f, 1f);
        valueRt.offsetMin = new Vector2(-84f, 0f);
        valueRt.offsetMax = new Vector2(0f, 0f);
    }

    void EnsureIntegrationSettingsContent(RectTransform contentRt)
    {
        if (contentRt == null) return;
        contentRt.anchorMin = Vector2.zero;
        contentRt.anchorMax = Vector2.one;
        contentRt.offsetMin = Vector2.zero;
        contentRt.offsetMax = Vector2.zero;

        settingsIntegrationLinkButton = FindOrCreateSettingsButton(contentRt, settingsIntegrationLinkButton, "Button_WebLink", "ここからウェブサイトへ遷移");
        if (settingsIntegrationLinkButton != null)
        {
            var rt = settingsIntegrationLinkButton.transform as RectTransform;
            if (rt != null)
            {
                rt.anchorMin = new Vector2(0f, 1f);
                rt.anchorMax = new Vector2(1f, 1f);
                rt.offsetMin = new Vector2(24f, -80f);
                rt.offsetMax = new Vector2(-24f, -32f);
            }

            var image = settingsIntegrationLinkButton.GetComponent<Image>();
            if (image != null) image.color = DesignTokens.BgSecondary;

            var label = settingsIntegrationLinkButton.GetComponentInChildren<Text>(true);
            if (label != null)
            {
                label.text = "ここからウェブサイトへ遷移";
                label.fontSize = 16;
                label.color = DesignTokens.Accent;
                label.alignment = TextAnchor.MiddleCenter;
            }
        }
    }

    void EnsureAccountSettingsContent(RectTransform contentRt)
    {
        if (contentRt == null) return;
        contentRt.anchorMin = Vector2.zero;
        contentRt.anchorMax = Vector2.one;
        contentRt.offsetMin = Vector2.zero;
        contentRt.offsetMax = Vector2.zero;

        var icon = FindOrCreateSettingsText(contentRt, "Text_AvatarIcon", "\u25CF");
        icon.fontSize = 72;
        icon.alignment = TextAnchor.MiddleCenter;
        icon.color = DesignTokens.Accent;
        var iconRt = icon.rectTransform;
        iconRt.anchorMin = new Vector2(0.5f, 0.5f);
        iconRt.anchorMax = new Vector2(0.5f, 0.5f);
        iconRt.pivot = new Vector2(0.5f, 0.5f);
        iconRt.sizeDelta = new Vector2(120f, 120f);
        iconRt.anchoredPosition = new Vector2(0f, 80f);

        settingsAccountUserNameText = FindOrCreateSettingsText(contentRt, "Text_UserName", "User Name");
        var nameRt = settingsAccountUserNameText.rectTransform;
        nameRt.anchorMin = new Vector2(0.5f, 0.5f);
        nameRt.anchorMax = new Vector2(0.5f, 0.5f);
        nameRt.pivot = new Vector2(0.5f, 0.5f);
        nameRt.sizeDelta = new Vector2(360f, 44f);
        nameRt.anchoredPosition = new Vector2(0f, -6f);

        settingsAccountEmailText = FindOrCreateSettingsText(contentRt, "Text_Email", "user@example.com");
        var emailRt = settingsAccountEmailText.rectTransform;
        emailRt.anchorMin = new Vector2(0.5f, 0.5f);
        emailRt.anchorMax = new Vector2(0.5f, 0.5f);
        emailRt.pivot = new Vector2(0.5f, 0.5f);
        emailRt.sizeDelta = new Vector2(360f, 32f);
        emailRt.anchoredPosition = new Vector2(0f, -42f);
    }

    Slider EnsureSettingsSlider(RectTransform parent, Slider slider, string objectName)
    {
        if (parent == null) return slider;

        if (slider == null)
        {
            var found = parent.Find(objectName);
            if (found != null) slider = found.GetComponent<Slider>();
        }

        if (slider == null)
        {
            var root = new GameObject(objectName, typeof(RectTransform), typeof(Image), typeof(Slider));
            var rootRt = root.GetComponent<RectTransform>();
            rootRt.SetParent(parent, false);
            slider = root.GetComponent<Slider>();
        }

        var backgroundImage = slider.GetComponent<Image>();
        if (backgroundImage == null) backgroundImage = slider.gameObject.AddComponent<Image>();
        backgroundImage.color = DesignTokens.BgSecondary;

        var fillArea = FindOrCreateSettingsRect(slider.transform, "Fill Area");
        fillArea.anchorMin = new Vector2(0f, 0f);
        fillArea.anchorMax = new Vector2(1f, 1f);
        fillArea.offsetMin = new Vector2(8f, 10f);
        fillArea.offsetMax = new Vector2(-24f, -10f);

        var fill = FindOrCreateSettingsRect(fillArea, "Fill");
        fill.anchorMin = new Vector2(0f, 0f);
        fill.anchorMax = new Vector2(1f, 1f);
        fill.offsetMin = Vector2.zero;
        fill.offsetMax = Vector2.zero;
        var fillImage = fill.GetComponent<Image>();
        if (fillImage == null) fillImage = fill.gameObject.AddComponent<Image>();
        fillImage.color = DesignTokens.Accent;

        var handleSlideArea = FindOrCreateSettingsRect(slider.transform, "Handle Slide Area");
        handleSlideArea.anchorMin = new Vector2(0f, 0f);
        handleSlideArea.anchorMax = new Vector2(1f, 1f);
        handleSlideArea.offsetMin = new Vector2(8f, 0f);
        handleSlideArea.offsetMax = new Vector2(-8f, 0f);

        var handle = FindOrCreateSettingsRect(handleSlideArea, "Handle");
        handle.anchorMin = new Vector2(0.5f, 0.5f);
        handle.anchorMax = new Vector2(0.5f, 0.5f);
        handle.sizeDelta = new Vector2(18f, 30f);
        var handleImage = handle.GetComponent<Image>();
        if (handleImage == null) handleImage = handle.gameObject.AddComponent<Image>();
        handleImage.color = DesignTokens.Surface;

        slider.fillRect = fill;
        slider.handleRect = handle;
        slider.targetGraphic = handleImage;
        slider.direction = Slider.Direction.LeftToRight;
        return slider;
    }

    Text FindOrCreateSettingsText(Transform parent, string objectName, string defaultText)
    {
        if (parent == null) return null;

        var found = parent.Find(objectName);
        Text text = null;
        if (found != null) text = found.GetComponent<Text>();
        if (text == null)
        {
            var go = new GameObject(objectName, typeof(RectTransform), typeof(Text));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            text = go.GetComponent<Text>();
        }

        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.text = defaultText;
        return text;
    }

    Button FindOrCreateSettingsButton(Transform parent, Button button, string objectName, string labelText)
    {
        if (parent == null) return button;

        if (button == null)
        {
            var found = parent.Find(objectName);
            if (found != null) button = found.GetComponent<Button>();
        }

        if (button == null)
        {
            var go = new GameObject(objectName, typeof(RectTransform), typeof(Image), typeof(Button));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            button = go.GetComponent<Button>();
        }

        var label = button.GetComponentInChildren<Text>(true);
        if (label == null)
        {
            var labelGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
            var labelRt = labelGo.GetComponent<RectTransform>();
            labelRt.SetParent(button.transform, false);
            labelRt.anchorMin = Vector2.zero;
            labelRt.anchorMax = Vector2.one;
            labelRt.offsetMin = Vector2.zero;
            labelRt.offsetMax = Vector2.zero;
            label = labelGo.GetComponent<Text>();
        }

        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.text = labelText;
        return button;
    }

    void ApplyCatalogTopLayout(RectTransform panel)
    {
        if (panel == null) return;

        if (editModeRow != null)
        {
            float left = panel.offsetMax.x + 12f;
            float right = left + 236f;
            editModeRow.anchorMin = new Vector2(0f, 1f);
            editModeRow.anchorMax = new Vector2(0f, 1f);
            editModeRow.pivot = new Vector2(0f, 1f);
            editModeRow.offsetMin = new Vector2(left, -52f);
            editModeRow.offsetMax = new Vector2(right, -12f);
        }

        var header = panel.Find("Header") as RectTransform;
        if (header != null)
        {
            header.anchorMin = new Vector2(0f, 1f);
            header.anchorMax = new Vector2(1f, 1f);
            header.offsetMin = new Vector2(10f, -48f);
            header.offsetMax = new Vector2(-10f, -10f);
        }

        var searchRow = panel.Find("SearchRow") as RectTransform;
        if (searchRow == null) searchRow = panel.Find("SearchRow_Runtime") as RectTransform;
        if (searchRow != null)
        {
            searchRow.anchorMin = new Vector2(0f, 1f);
            searchRow.anchorMax = new Vector2(1f, 1f);
            searchRow.offsetMin = new Vector2(10f, -44f);
            searchRow.offsetMax = new Vector2(-10f, -8f);
        }

        if (statusText != null)
        {
            var statusRt = statusText.rectTransform;
            statusRt.anchorMin = new Vector2(0f, 1f);
            statusRt.anchorMax = new Vector2(1f, 1f);
            statusRt.offsetMin = new Vector2(14f, -66f);
            statusRt.offsetMax = new Vector2(-14f, -46f);
        }

        if (content != null)
        {
            var scroll = content.parent != null ? content.parent.parent as RectTransform : null;
            if (scroll != null)
            {
                scroll.anchorMin = new Vector2(0f, 0f);
                scroll.anchorMax = new Vector2(1f, 1f);
                scroll.offsetMin = new Vector2(8f, 56f);
                scroll.offsetMax = new Vector2(-8f, -72f);
            }
        }
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

    void EnsureEditModeServiceBinding()
    {
        var service = EditModeService.I != null ? EditModeService.I : FindFirstObjectByType<EditModeService>();
        if (service == boundEditModeService) return;

        UnbindEditModeService();
        boundEditModeService = service;
        if (boundEditModeService != null)
        {
            boundEditModeService.ModeChanged += OnEditModeChanged;
        }
    }

    void UnbindEditModeService()
    {
        if (boundEditModeService == null) return;
        boundEditModeService.ModeChanged -= OnEditModeChanged;
        boundEditModeService = null;
    }

    void OnEditModeChanged(EditMode _)
    {
        RefreshModeButtons();
    }

    void OnClickModeBrowse()
    {
        EnsureEditModeServiceBinding();
        if (boundEditModeService == null) return;
        boundEditModeService.SetMode(EditMode.Browse);
        RefreshModeButtons();
    }

    void OnClickModeTransform()
    {
        EnsureEditModeServiceBinding();
        if (boundEditModeService == null) return;
        boundEditModeService.SetMode(EditMode.Transform);
        RefreshModeButtons();
    }

    void OnClickModeScale()
    {
        EnsureEditModeServiceBinding();
        if (boundEditModeService == null) return;
        boundEditModeService.SetMode(EditMode.Scale);
        RefreshModeButtons();
    }

    void OnClickSettings()
    {
        EnsureRuntimeCatalogControls();
        if (settingsPanel == null) return;

        ApplySettingsPanelDesign(settingsPanel);
        RefreshSettingsTabs();
        settingsPanel.gameObject.SetActive(true);
    }

    void CloseSettingsPanel()
    {
        if (settingsPanel == null) return;
        settingsPanel.gameObject.SetActive(false);
    }

    public void CloseSettingsPanelFromOverlayClick()
    {
        CloseSettingsPanel();
    }

    void OnClickSettingsTabGeneral()
    {
        activeSettingsTab = SettingsTab.General;
        RefreshSettingsTabs();
    }

    void OnClickSettingsTabIntegration()
    {
        activeSettingsTab = SettingsTab.Integration;
        RefreshSettingsTabs();
    }

    void OnClickSettingsTabAccount()
    {
        activeSettingsTab = SettingsTab.Account;
        RefreshSettingsTabs();
    }

    void RefreshSettingsTabs()
    {
        if (settingsGeneralContent != null)
        {
            settingsGeneralContent.gameObject.SetActive(activeSettingsTab == SettingsTab.General);
        }

        if (settingsIntegrationContent != null)
        {
            settingsIntegrationContent.gameObject.SetActive(activeSettingsTab == SettingsTab.Integration);
        }

        if (settingsAccountContent != null)
        {
            settingsAccountContent.gameObject.SetActive(activeSettingsTab == SettingsTab.Account);
        }

        ApplySettingsTabVisual(settingsTabGeneralButton, activeSettingsTab == SettingsTab.General);
        ApplySettingsTabVisual(settingsTabIntegrationButton, activeSettingsTab == SettingsTab.Integration);
        ApplySettingsTabVisual(settingsTabAccountButton, activeSettingsTab == SettingsTab.Account);
    }

    void ApplySettingsTabVisual(Button button, bool isActive)
    {
        if (button == null) return;

        var image = button.GetComponent<Image>();
        if (image != null)
        {
            image.color = isActive ? DesignTokens.Accent : DesignTokens.BgSecondary;
        }

        var label = button.GetComponentInChildren<Text>(true);
        if (label != null)
        {
            label.color = isActive ? DesignTokens.Surface : DesignTokens.TextPrimary;
        }
    }

    void EnsureSettingsCameraBinding()
    {
        if (settingsCameraController == null)
        {
            settingsCameraController = FindFirstObjectByType<EditorCameraController>();
        }

        if (settingsCameraController == null) return;
        if (settingsBaseOrbitSpeed >= 0f) return;

        settingsBaseOrbitSpeed = settingsCameraController.orbitSpeed;
        settingsBasePanSpeed = settingsCameraController.panSpeed;
        settingsBaseZoomSpeed = settingsCameraController.zoomSpeed;
        settingsBaseOrthographicZoomSpeed = settingsCameraController.orthographicZoomSpeed;
    }

    void OnSettingsSensitivityChanged(float sliderValue)
    {
        float clamped = Mathf.Clamp(sliderValue, 0.2f, 2.5f);

        if (settingsSensitivityValueText != null)
        {
            settingsSensitivityValueText.text = $"{clamped:0.00}x";
        }

        if (!settingsInitializingUi)
        {
            SetSettingsDirty(true);
        }

        EnsureSettingsCameraBinding();
        if (settingsCameraController == null || settingsBaseOrbitSpeed < 0f) return;

        settingsCameraController.orbitSpeed = settingsBaseOrbitSpeed * clamped;
        settingsCameraController.panSpeed = settingsBasePanSpeed * clamped;
        settingsCameraController.zoomSpeed = settingsBaseZoomSpeed * clamped;
        settingsCameraController.orthographicZoomSpeed = settingsBaseOrthographicZoomSpeed * clamped;
    }

    void SetSettingsDirty(bool dirty)
    {
        settingsHasPendingChanges = dirty;
        UpdateSettingsApplyButtonVisibility();
    }

    void UpdateSettingsApplyButtonVisibility()
    {
        if (settingsApplyButton == null) return;
        settingsApplyButton.gameObject.SetActive(settingsHasPendingChanges);
    }

    void OnClickSettingsApply()
    {
        SetSettingsDirty(false);
        CloseSettingsPanel();
    }

    void OnClickSettingsIntegrationLink()
    {
        Application.OpenURL("https://unity.com/");
    }

    void RefreshModeButtons()
    {
        EnsureEditModeServiceBinding();

        var mode = boundEditModeService != null ? boundEditModeService.Mode : EditMode.Browse;
        if (mode != EditMode.Transform && mode != EditMode.Scale)
        {
            mode = EditMode.Browse;
        }

        ApplyModeButtonVisual(browseModeButton, mode == EditMode.Browse);
        ApplyModeButtonVisual(transformModeButton, mode == EditMode.Transform);
        ApplyModeButtonVisual(scaleModeButton, mode == EditMode.Scale);
    }

    void ApplyModeButtonVisual(Button button, bool isActive)
    {
        if (button == null) return;

        var image = button.GetComponent<Image>();
        if (image != null)
        {
            image.color = isActive ? DesignTokens.Accent : DesignTokens.BgSecondary;
        }

        var label = button.GetComponentInChildren<Text>(true);
        if (label != null)
        {
            label.color = isActive ? DesignTokens.Surface : DesignTokens.TextPrimary;
        }
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

public class SettingsOverlayClickCatcher : MonoBehaviour, IPointerClickHandler
{
    CatalogUI owner;
    RectTransform window;

    public void Configure(CatalogUI ownerUi, RectTransform windowRect)
    {
        owner = ownerUi;
        window = windowRect;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (owner == null || window == null) return;

        bool clickedInsideWindow = RectTransformUtility.RectangleContainsScreenPoint(
            window,
            eventData.position,
            eventData.pressEventCamera);

        if (clickedInsideWindow) return;
        owner.CloseSettingsPanelFromOverlayClick();
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
