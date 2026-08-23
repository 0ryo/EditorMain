using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ScenarioValidationPanel : MonoBehaviour
{
    const string PanelName = "Panel_SaveValidation";
    const string RuntimeItemPrefix = "ValidationItem_";
    const float PreferredWidth = 560f;
    const float PreferredHeight = 360f;
    const float MinWidth = 320f;
    const float MinHeight = 240f;
    const float ViewportMargin = 16f;
    const float MinimizedWidth = 180f;
    const float MinimizedHeight = 44f;

    [SerializeField] TMP_Text titleText;
    [SerializeField] TMP_Text summaryText;
    [SerializeField] RectTransform issueListRoot;
    [SerializeField] Button issueButtonTemplate;
    [SerializeField] Button closeButton;
    bool applyingResponsiveLayout;
    bool isMinimized;

    public bool IsVisible => gameObject.activeSelf;
    public event Action Hidden;

    public static ScenarioValidationPanel Ensure(RectTransform parent, ScenarioValidationPanel existing = null)
    {
        if (existing != null) return existing;
        if (parent == null) return null;

        var found = parent.Find(PanelName);
        if (found != null)
        {
            var foundPanel = found.GetComponent<ScenarioValidationPanel>();
            if (foundPanel != null)
            {
                foundPanel.ResolveReferences();
                foundPanel.WireCloseButton();
                foundPanel.ApplyResponsiveLayout();
                return foundPanel;
            }
        }

        var root = CreateRect(PanelName, parent);
        root.anchorMin = new Vector2(0.5f, 0.5f);
        root.anchorMax = new Vector2(0.5f, 0.5f);
        root.pivot = new Vector2(0.5f, 0.5f);
        root.sizeDelta = new Vector2(560f, 360f);
        root.anchoredPosition = Vector2.zero;

        var rootImage = root.gameObject.AddComponent<Image>();
        rootImage.color = DesignTokens.Surface;
        var outline = root.gameObject.AddComponent<Outline>();
        outline.effectColor = DesignTokens.Divider;
        outline.effectDistance = new Vector2(1f, -1f);

        var title = CreateText("Text_Title", root, "\u4FDD\u5B58\u524D\u306B\u78BA\u8A8D\u3057\u3066\u304F\u3060\u3055\u3044", DesignTokens.FontSizeHeading, DesignTokens.TextPrimary);
        SetRect(title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(24f, -56f), new Vector2(-112f, -24f));

        var close = CreateButton("Button_Close", root, "\u9589\u3058\u308B");
        SetRect(close.GetComponent<RectTransform>(), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-104f, -64f), new Vector2(-24f, -24f));

        var summary = CreateText("Text_Summary", root, string.Empty, DesignTokens.FontSizeBody, DesignTokens.TextSecondary);
        SetRect(summary.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(24f, -88f), new Vector2(-24f, -64f));

        var scrollRoot = CreateRect("Scroll_Issues", root);
        SetRect(scrollRoot, Vector2.zero, Vector2.one, new Vector2(24f, 24f), new Vector2(-24f, -104f));
        var scrollRect = scrollRoot.gameObject.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.vertical = true;

        var viewport = CreateRect("Viewport", scrollRoot);
        SetRect(viewport, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        var viewportImage = viewport.gameObject.AddComponent<Image>();
        viewportImage.color = DesignTokens.BgPrimary;
        viewport.gameObject.AddComponent<RectMask2D>();

        var content = CreateRect("Content", viewport);
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.offsetMin = new Vector2(8f, 0f);
        content.offsetMax = new Vector2(-8f, 0f);
        var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(0, 0, 8, 8);
        layout.spacing = 8f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var template = CreateButton("ValidationItem_Template", content, string.Empty);
        var templateLayout = template.gameObject.AddComponent<LayoutElement>();
        templateLayout.minHeight = 52f;
        templateLayout.preferredHeight = 52f;
        var templateLabel = template.GetComponentInChildren<TMP_Text>(true);
        templateLabel.alignment = TextAlignmentOptions.MidlineLeft;
        templateLabel.enableWordWrapping = true;
        templateLabel.margin = new Vector4(12f, 4f, 12f, 4f);
        template.gameObject.SetActive(false);

        scrollRect.viewport = viewport;
        scrollRect.content = content;

        var panel = root.gameObject.AddComponent<ScenarioValidationPanel>();
        panel.titleText = title;
        panel.summaryText = summary;
        panel.issueListRoot = content;
        panel.issueButtonTemplate = template;
        panel.closeButton = close;
        panel.WireCloseButton();
        panel.ApplyResponsiveLayout();
        root.SetAsLastSibling();
        root.gameObject.SetActive(false);
        return panel;
    }

    public void Show(
        GraphValidationResult validation,
        Func<GraphValidationIssue, string> getFriendlyMessage,
        Action<string> onNodeRequested)
    {
        bool preserveMinimizedState = gameObject.activeSelf && isMinimized;
        ResolveReferences();
        if (validation == null || issueListRoot == null || issueButtonTemplate == null) return;
        if (!preserveMinimizedState) RestoreIssueList();

        ClearItems();
        int errorCount = validation.errors.Count;
        int warningCount = validation.warnings.Count;
        if (titleText != null) titleText.text = "\u4FDD\u5B58\u524D\u306B\u78BA\u8A8D\u3057\u3066\u304F\u3060\u3055\u3044";
        if (summaryText != null)
        {
            summaryText.text = warningCount > 0
                ? $"\u4FEE\u6B63\u304C\u5FC5\u8981\u306A\u9805\u76EE {errorCount}\u4EF6 / \u6CE8\u610F {warningCount}\u4EF6"
                : $"\u4FEE\u6B63\u304C\u5FC5\u8981\u306A\u9805\u76EE {errorCount}\u4EF6";
        }

        int index = 1;
        index = AddItems(validation.errors, index, getFriendlyMessage, onNodeRequested, true);
        AddItems(validation.warnings, index, getFriendlyMessage, onNodeRequested, false);

        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        if (preserveMinimizedState) MinimizeForFocus();
    }

    public void Hide()
    {
        if (!gameObject.activeSelf) return;
        isMinimized = false;
        gameObject.SetActive(false);
        Hidden?.Invoke();
    }

    public void MinimizeForFocus()
    {
        isMinimized = true;
        SetExpandedContentVisible(false);
        SetCloseButtonLabel("問題一覧に戻る");
        ApplyMinimizedLayout();
    }

    public void RestoreIssueList()
    {
        isMinimized = false;
        SetExpandedContentVisible(true);
        SetCloseButtonLabel("閉じる");
        ApplyResponsiveLayout();
    }

    int AddItems(
        IReadOnlyList<GraphValidationIssue> issues,
        int startIndex,
        Func<GraphValidationIssue, string> getFriendlyMessage,
        Action<string> onNodeRequested,
        bool isError)
    {
        int index = startIndex;
        for (int i = 0; i < issues.Count; i++, index++)
        {
            var issue = issues[i];
            var button = Instantiate(issueButtonTemplate, issueListRoot);
            button.gameObject.name = RuntimeItemPrefix + index.ToString("D2");
            button.gameObject.SetActive(true);
            button.onClick.RemoveAllListeners();

            string friendly = getFriendlyMessage != null ? getFriendlyMessage(issue) : issue.message;
            string nodeSuffix = string.IsNullOrWhiteSpace(issue.nodeId) ? string.Empty : $"  [{issue.nodeId}]";
            var label = button.GetComponentInChildren<TMP_Text>(true);
            if (label != null)
            {
                label.text = $"{index}. {friendly}{nodeSuffix}";
                label.color = isError ? DesignTokens.Error : DesignTokens.TextPrimary;
            }

            bool canFocus = !string.IsNullOrWhiteSpace(issue.nodeId) && onNodeRequested != null;
            button.interactable = canFocus;
            var colors = button.colors;
            colors.normalColor = DesignTokens.BgSecondary;
            colors.highlightedColor = DesignTokens.BgTertiary;
            colors.pressedColor = DesignTokens.Divider;
            colors.disabledColor = DesignTokens.BgSecondary;
            button.colors = colors;
            if (canFocus)
            {
                string capturedNodeId = issue.nodeId;
                button.onClick.AddListener(() =>
                {
                    onNodeRequested(capturedNodeId);
                    MinimizeForFocus();
                });
            }
        }

        return index;
    }

    void Awake()
    {
        ResolveReferences();
        WireCloseButton();
        ApplyResponsiveLayout();
    }

    void OnEnable()
    {
        ApplyResponsiveLayout();
    }

    void OnRectTransformDimensionsChange()
    {
        ApplyResponsiveLayout();
    }

    void ResolveReferences()
    {
        if (titleText == null) titleText = transform.Find("Text_Title")?.GetComponent<TMP_Text>();
        if (summaryText == null) summaryText = transform.Find("Text_Summary")?.GetComponent<TMP_Text>();
        if (issueListRoot == null) issueListRoot = transform.Find("Scroll_Issues/Viewport/Content") as RectTransform;
        if (issueButtonTemplate == null) issueButtonTemplate = issueListRoot?.Find("ValidationItem_Template")?.GetComponent<Button>();
        if (closeButton == null) closeButton = transform.Find("Button_Close")?.GetComponent<Button>();
    }

    void WireCloseButton()
    {
        if (closeButton == null) return;
        closeButton.onClick.RemoveListener(OnClickClose);
        closeButton.onClick.AddListener(OnClickClose);
    }

    void OnClickClose()
    {
        if (isMinimized)
        {
            RestoreIssueList();
            return;
        }

        Hide();
    }

    void ApplyResponsiveLayout()
    {
        if (applyingResponsiveLayout) return;
        if (!(transform is RectTransform root) || !(root.parent is RectTransform parent)) return;
        if (isMinimized)
        {
            ApplyMinimizedLayout();
            return;
        }

        var parentRect = parent.rect;
        float availableWidth = Mathf.Max(0f, parentRect.width - (ViewportMargin * 2f));
        float availableHeight = Mathf.Max(0f, parentRect.height - (ViewportMargin * 2f));
        if (availableWidth <= 1f || availableHeight <= 1f) return;

        float minWidth = Mathf.Min(MinWidth, availableWidth);
        float minHeight = Mathf.Min(MinHeight, availableHeight);
        float width = Mathf.Clamp(PreferredWidth, minWidth, availableWidth);
        float height = Mathf.Clamp(PreferredHeight, minHeight, availableHeight);

        applyingResponsiveLayout = true;
        root.anchorMin = new Vector2(0.5f, 0.5f);
        root.anchorMax = new Vector2(0.5f, 0.5f);
        root.pivot = new Vector2(0.5f, 0.5f);
        root.sizeDelta = new Vector2(width, height);
        root.anchoredPosition = Vector2.zero;
        if (closeButton != null)
        {
            SetRect(
                closeButton.transform as RectTransform,
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(-104f, -64f),
                new Vector2(-24f, -24f));
        }
        applyingResponsiveLayout = false;
    }

    void ApplyMinimizedLayout()
    {
        if (applyingResponsiveLayout) return;
        if (!(transform is RectTransform root)) return;

        applyingResponsiveLayout = true;
        root.anchorMin = Vector2.one;
        root.anchorMax = Vector2.one;
        root.pivot = Vector2.one;
        root.sizeDelta = new Vector2(MinimizedWidth, MinimizedHeight);
        root.anchoredPosition = new Vector2(-ViewportMargin, -ViewportMargin);
        if (closeButton != null)
        {
            SetRect(closeButton.transform as RectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        }
        applyingResponsiveLayout = false;
    }

    void SetExpandedContentVisible(bool visible)
    {
        if (titleText != null) titleText.gameObject.SetActive(visible);
        if (summaryText != null) summaryText.gameObject.SetActive(visible);
        var scrollRoot = issueListRoot != null && issueListRoot.parent != null
            ? issueListRoot.parent.parent
            : null;
        if (scrollRoot != null) scrollRoot.gameObject.SetActive(visible);
    }

    void SetCloseButtonLabel(string value)
    {
        if (closeButton == null) return;
        var label = closeButton.GetComponentInChildren<TMP_Text>(true);
        if (label != null) label.text = value;
    }

    void ClearItems()
    {
        for (int i = issueListRoot.childCount - 1; i >= 0; i--)
        {
            var child = issueListRoot.GetChild(i);
            if (child == issueButtonTemplate.transform) continue;
            if (!child.name.StartsWith(RuntimeItemPrefix, StringComparison.Ordinal)) continue;
            child.gameObject.SetActive(false);
            Destroy(child.gameObject);
        }
    }

    static RectTransform CreateRect(string objectName, Transform parent)
    {
        var go = new GameObject(objectName, typeof(RectTransform));
        var rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        return rect;
    }

    static TMP_Text CreateText(string objectName, Transform parent, string value, float fontSize, Color color)
    {
        var rect = CreateRect(objectName, parent);
        var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        text.text = value;
        text.fontSize = fontSize;
        text.color = color;
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.raycastTarget = false;
        return text;
    }

    static Button CreateButton(string objectName, Transform parent, string labelValue)
    {
        var rect = CreateRect(objectName, parent);
        var image = rect.gameObject.AddComponent<Image>();
        image.color = DesignTokens.BgSecondary;
        var button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;

        var label = CreateText("Label", rect, labelValue, DesignTokens.FontSizeBody, DesignTokens.TextPrimary);
        SetRect(label.rectTransform, Vector2.zero, Vector2.one, new Vector2(12f, 0f), new Vector2(-12f, 0f));
        label.alignment = TextAlignmentOptions.Center;
        return button;
    }

    static void SetRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
    }
}
