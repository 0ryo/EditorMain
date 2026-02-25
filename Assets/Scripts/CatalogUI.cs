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
    GameObject runtimeImportedPrefab;

    class CardState
    {
        public string typeId;
        public string displayLabel;
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
        SetCardLabel(cardButton.gameObject, importedCardLabel);

        var importedTypeId = runtimeImportedTypeId;
        cardButton.onClick.RemoveAllListeners();
        cardButton.onClick.AddListener(() => OnClickCard(importedTypeId));

        var drag = cardButton.GetComponent<CatalogCardDragHandler>();
        if (drag == null) drag = cardButton.gameObject.AddComponent<CatalogCardDragHandler>();
        drag.Initialize(this, importedTypeId);

        cards.Add(new CardState
        {
            typeId = importedTypeId,
            displayLabel = importedCardLabel,
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
            var visible = showAll || matchesType || matchesLabel;
            card.root.SetActive(visible);
        }
    }

    void OnClickAdd()
    {
#if UNITY_EDITOR
        EnsureRuntimeBindings();

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

        var typeId = BuildImportedTypeId(assetPath);
        if (!placementController.RegisterRuntimePrefab(typeId, prefab))
        {
            SetStatus("Failed to register imported FBX.");
            return;
        }

        runtimeImportedTypeId = typeId;
        runtimeImportedPrefab = prefab;
        if (searchInput != null) searchInput.text = string.Empty;
        RebuildCards();
        SetStatus("New Object card added.");
#else
        SetStatus("FBX import is available in Unity Editor only.");
#endif
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

    static string BuildImportedTypeId(string assetPath)
    {
        var stem = Path.GetFileNameWithoutExtension(assetPath);
        var sanitized = SanitizeName(stem);
        if (string.IsNullOrWhiteSpace(sanitized))
        {
            sanitized = "Model";
        }

        return $"Imported/{sanitized}_{DateTime.UtcNow.Ticks}";
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
