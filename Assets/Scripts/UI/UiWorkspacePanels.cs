using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Runtime compatibility and the prefab builder share the same controls.
public sealed class UiWorkspacePanels : MonoBehaviour
{
    RectTransform catalog, scenario;
    RectTransform controls;
    ObjectDetailPanel detail;
    Button catalogButton, scenarioButton;
    bool initialized;
    bool manuallyToggled;
    int lastWidth;

    public static void Ensure(Transform root)
    {
        var owner = root.GetComponent<UiWorkspacePanels>();
        if (owner == null) owner = root.gameObject.AddComponent<UiWorkspacePanels>();
        owner.Prepare();
    }

    public void Prepare()
    {
        catalog = transform.Find("Panel_Catalog") as RectTransform;
        scenario = transform.Find("Panel_ScenarioGraph") as RectTransform;
        if (catalog == null || scenario == null) return;
        if (detail == null) detail = GetComponentInChildren<ObjectDetailPanel>(true);
        controls = transform.Find("WorkspacePanelControls") as RectTransform;
        if (controls == null)
        {
            var go = new GameObject("WorkspacePanelControls", typeof(RectTransform));
            controls = go.GetComponent<RectTransform>();
            controls.SetParent(transform, false);
            controls.anchorMin = controls.anchorMax = controls.pivot = new Vector2(0, 1);
            controls.sizeDelta = new Vector2(224, 44);
        }
        catalogButton = Button("ToggleCatalog", "一覧 F9", 0);
        scenarioButton = Button("ToggleScenario", "手順 F10", 76);
        var detailButton = Button("ToggleDetail", "詳細 F8", 152);
        detailButton.onClick.RemoveListener(ToggleDetail);
        detailButton.onClick.AddListener(ToggleDetail);
        catalogButton.onClick.RemoveListener(ToggleCatalog);
        catalogButton.onClick.AddListener(ToggleCatalog);
        scenarioButton.onClick.RemoveListener(ToggleScenario);
        scenarioButton.onClick.AddListener(ToggleScenario);
        initialized = true;
    }

    Button Button(string name, string label, float x)
    {
        var child = controls.Find(name);
        if (child != null) return child.GetComponent<Button>();
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        var rect = go.GetComponent<RectTransform>();
        rect.SetParent(controls, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = new Vector2(x, 0);
        rect.sizeDelta = new Vector2(72, 44);
        go.GetComponent<Image>().color = DesignTokens.Surface;
        var textGo = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        var textRect = textGo.GetComponent<RectTransform>();
        textRect.SetParent(rect, false);
        textRect.anchorMin = Vector2.zero; textRect.anchorMax = Vector2.one;
        textRect.offsetMin = textRect.offsetMax = Vector2.zero;
        var text = textGo.GetComponent<TMP_Text>();
        text.text = label; text.fontSize = 16; text.color = DesignTokens.TextPrimary;
        text.alignment = TextAlignmentOptions.Center; text.raycastTarget = false;
        return go.GetComponent<Button>();
    }

    void ToggleDetail() { if (detail != null) detail.ToggleUserCollapsed(); }
    void ToggleCatalog() { manuallyToggled = true; catalog.gameObject.SetActive(!catalog.gameObject.activeSelf); }
    void ToggleScenario() { manuallyToggled = true; scenario.gameObject.SetActive(!scenario.gameObject.activeSelf); }

    void Update()
    {
        if (!initialized) Prepare();
        if (!initialized) return;
        if (lastWidth != Screen.width)
        {
            lastWidth = Screen.width;
            if (!manuallyToggled) catalog.gameObject.SetActive(Screen.width >= 1366);
        }
        controls.anchoredPosition = new Vector2(catalog.gameObject.activeSelf ? catalog.offsetMax.x + 12 : 12, -64);
#if ENABLE_LEGACY_INPUT_MANAGER
        if (EditWorkspace.IsTypingIntoInputField()) return;
        if (Input.GetKeyDown(KeyCode.F8)) ToggleDetail();
        if (Input.GetKeyDown(KeyCode.F9)) ToggleCatalog();
        if (Input.GetKeyDown(KeyCode.F10)) ToggleScenario();
#endif
    }
}
