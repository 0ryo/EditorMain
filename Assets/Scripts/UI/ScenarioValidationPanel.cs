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
    const float MinimizedWidth = 448f;
    const float MinimizedHeight = 48f;

    [SerializeField] TMP_Text titleText;
    [SerializeField] TMP_Text summaryText;
    [SerializeField] RectTransform issueListRoot;
    [SerializeField] Button issueButtonTemplate;
    [SerializeField] Button closeButton;
    [SerializeField] Button previousButton;
    [SerializeField] Button nextButton;
    [SerializeField] TMP_Text navigationText;
    [SerializeField] TMP_Text compactStatusText;
    [SerializeField] Image panelImage;
    [SerializeField] Outline panelOutline;
    bool applyingResponsiveLayout;
    bool isMinimized;
    readonly List<GraphValidationIssue> navigableIssues = new List<GraphValidationIssue>();
    Action<string> nodeRequested;
    int currentIssueIndex = -1;
    bool hasIssues;

    public bool IsVisible => gameObject.activeSelf;
    public bool HasNavigableIssues => navigableIssues.Count > 0;
    public event Action Hidden;

    public static ScenarioValidationPanel Ensure(RectTransform parent, ScenarioValidationPanel existing = null)
    {
        if (parent == null) return null;
        if (existing != null)
        {
            if (existing.transform.parent != parent) existing.transform.SetParent(parent, false);
            existing.ResolveReferences();
            existing.EnsureNavigationControls();
            existing.WireCloseButton();
            existing.ApplyResponsiveLayout();
            return existing;
        }

        var found = parent.Find(PanelName);
        if (found != null)
        {
            var foundPanel = found.GetComponent<ScenarioValidationPanel>();
            if (foundPanel != null)
            {
                foundPanel.ResolveReferences();
                foundPanel.EnsureNavigationControls();
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

        var title = CreateText("Text_Title", root, "シナリオの問題", DesignTokens.FontSizeHeading, DesignTokens.TextPrimary);
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
        panel.panelImage = rootImage;
        panel.panelOutline = outline;
        panel.EnsureNavigationControls();
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
        string currentIssueKey = GetCurrentIssueKey();
        ResolveReferences();
        EnsureNavigationControls();
        if (validation == null || issueListRoot == null || issueButtonTemplate == null) return;
        if (!preserveMinimizedState) RestoreIssueList();

        nodeRequested = onNodeRequested;
        RebuildNavigableIssues(validation, currentIssueKey);

        ClearItems();
        int errorCount = validation.errors.Count;
        int warningCount = validation.warnings.Count;
        hasIssues = errorCount + warningCount > 0;
        ApplySeverityStyle(errorCount, warningCount);
        if (summaryText != null)
        {
            summaryText.text = warningCount > 0
                ? $"\u4FEE\u6B63\u304C\u5FC5\u8981\u306A\u9805\u76EE {errorCount}\u4EF6 / \u6CE8\u610F {warningCount}\u4EF6"
                : $"\u4FEE\u6B63\u304C\u5FC5\u8981\u306A\u9805\u76EE {errorCount}\u4EF6";
        }

        int index = 1;
        index = AddItems(validation.errors, index, getFriendlyMessage, onNodeRequested, true);
        AddItems(validation.warnings, index, getFriendlyMessage, onNodeRequested, false);
        UpdateNavigationControls();

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
        SetCloseButtonLabel("問題一覧");
        ApplyMinimizedLayout();
    }

    public void RestoreIssueList()
    {
        isMinimized = false;
        SetExpandedContentVisible(true);
        SetCloseButtonLabel(hasIssues ? "最小化" : "閉じる");
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
                    currentIssueIndex = navigableIssues.IndexOf(issue);
                    UpdateNavigationControls();
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
        EnsureNavigationControls();
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
        if (previousButton == null) previousButton = transform.Find("Button_PreviousIssue")?.GetComponent<Button>();
        if (nextButton == null) nextButton = transform.Find("Button_NextIssue")?.GetComponent<Button>();
        if (navigationText == null) navigationText = transform.Find("Text_IssuePosition")?.GetComponent<TMP_Text>();
        if (compactStatusText == null) compactStatusText = transform.Find("Text_CompactStatus")?.GetComponent<TMP_Text>();
        if (panelImage == null) panelImage = GetComponent<Image>();
        if (panelOutline == null) panelOutline = GetComponent<Outline>();
    }

    void WireCloseButton()
    {
        if (closeButton == null) return;
        closeButton.onClick.RemoveListener(OnClickClose);
        closeButton.onClick.AddListener(OnClickClose);
    }

    void EnsureNavigationControls()
    {
        if (!(transform is RectTransform root)) return;

        if (previousButton == null)
        {
            previousButton = CreateButton("Button_PreviousIssue", root, "前");
        }
        if (nextButton == null)
        {
            nextButton = CreateButton("Button_NextIssue", root, "次");
        }
        if (navigationText == null)
        {
            navigationText = CreateText(
                "Text_IssuePosition",
                root,
                "－ / 0",
                DesignTokens.FontSizeCaption,
                DesignTokens.TextSecondary);
            navigationText.alignment = TextAlignmentOptions.Center;
        }
        if (compactStatusText == null)
        {
            compactStatusText = CreateText(
                "Text_CompactStatus",
                root,
                "エラー 0件",
                DesignTokens.FontSizeBody,
                DesignTokens.Error);
            compactStatusText.alignment = TextAlignmentOptions.MidlineLeft;
        }

        previousButton.onClick.RemoveListener(OnClickPreviousIssue);
        previousButton.onClick.AddListener(OnClickPreviousIssue);
        nextButton.onClick.RemoveListener(OnClickNextIssue);
        nextButton.onClick.AddListener(OnClickNextIssue);
        if (isMinimized) ApplyMinimizedLayout();
        else ApplyExpandedNavigationLayout();
        UpdateNavigationControls();
    }

    void OnClickPreviousIssue()
    {
        NavigateIssue(-1);
    }

    void OnClickNextIssue()
    {
        NavigateIssue(1);
    }

    void NavigateIssue(int direction)
    {
        if (navigableIssues.Count == 0 || nodeRequested == null) return;
        if (currentIssueIndex < 0)
        {
            currentIssueIndex = direction < 0 ? navigableIssues.Count - 1 : 0;
        }
        else
        {
            currentIssueIndex = (currentIssueIndex + direction + navigableIssues.Count) % navigableIssues.Count;
        }

        UpdateNavigationControls();
        nodeRequested(navigableIssues[currentIssueIndex].nodeId);
        MinimizeForFocus();
    }

    void RebuildNavigableIssues(GraphValidationResult validation, string currentIssueKey)
    {
        navigableIssues.Clear();
        foreach (var issue in validation.errors)
        {
            if (!string.IsNullOrWhiteSpace(issue?.nodeId)) navigableIssues.Add(issue);
        }
        foreach (var issue in validation.warnings)
        {
            if (!string.IsNullOrWhiteSpace(issue?.nodeId)) navigableIssues.Add(issue);
        }

        currentIssueIndex = -1;
        if (string.IsNullOrEmpty(currentIssueKey)) return;
        for (int i = 0; i < navigableIssues.Count; i++)
        {
            if (!string.Equals(GetIssueKey(navigableIssues[i]), currentIssueKey, StringComparison.Ordinal)) continue;
            currentIssueIndex = i;
            break;
        }
    }

    string GetCurrentIssueKey()
    {
        return currentIssueIndex >= 0 && currentIssueIndex < navigableIssues.Count
            ? GetIssueKey(navigableIssues[currentIssueIndex])
            : null;
    }

    static string GetIssueKey(GraphValidationIssue issue)
    {
        return issue == null ? null : issue.code + "|" + issue.nodeId;
    }

    void UpdateNavigationControls()
    {
        bool canNavigate = navigableIssues.Count > 0 && nodeRequested != null;
        if (previousButton != null) previousButton.interactable = canNavigate;
        if (nextButton != null) nextButton.interactable = canNavigate;
        if (navigationText != null)
        {
            navigationText.text = currentIssueIndex >= 0
                ? $"{currentIssueIndex + 1} / {navigableIssues.Count}"
                : $"－ / {navigableIssues.Count}";
        }
    }

    void ApplySeverityStyle(int errorCount, int warningCount)
    {
        Color semanticColor = errorCount > 0 ? DesignTokens.Error : DesignTokens.Warning;
        if (titleText != null)
        {
            titleText.text = errorCount > 0 ? "シナリオエラー" : "シナリオの注意";
            titleText.color = semanticColor;
        }
        if (summaryText != null) summaryText.color = semanticColor;
        if (compactStatusText != null)
        {
            compactStatusText.text = errorCount > 0 ? $"エラー {errorCount}件" : $"注意 {warningCount}件";
            compactStatusText.color = semanticColor;
        }
        if (panelImage != null) panelImage.color = Color.Lerp(DesignTokens.Surface, semanticColor, 0.08f);
        if (panelOutline != null)
        {
            panelOutline.effectColor = semanticColor;
            panelOutline.effectDistance = new Vector2(2f, -2f);
        }
    }

    void ApplyExpandedNavigationLayout()
    {
        if (previousButton == null || nextButton == null || navigationText == null) return;
        SetRect(previousButton.transform as RectTransform, Vector2.one, Vector2.one, new Vector2(-268f, -64f), new Vector2(-228f, -24f));
        SetRect(navigationText.rectTransform, Vector2.one, Vector2.one, new Vector2(-220f, -64f), new Vector2(-160f, -24f));
        SetRect(nextButton.transform as RectTransform, Vector2.one, Vector2.one, new Vector2(-152f, -64f), new Vector2(-112f, -24f));
        if (titleText != null) titleText.rectTransform.offsetMax = new Vector2(-280f, -24f);
    }

    void OnClickClose()
    {
        if (isMinimized)
        {
            RestoreIssueList();
            return;
        }

        if (hasIssues)
        {
            MinimizeForFocus();
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
        ApplyExpandedNavigationLayout();
        applyingResponsiveLayout = false;
    }

    void ApplyMinimizedLayout()
    {
        if (applyingResponsiveLayout) return;
        if (!(transform is RectTransform root)) return;

        applyingResponsiveLayout = true;
        root.anchorMin = new Vector2(0f, 1f);
        root.anchorMax = new Vector2(0f, 1f);
        root.pivot = new Vector2(0f, 1f);
        root.sizeDelta = new Vector2(MinimizedWidth, MinimizedHeight);
        root.anchoredPosition = new Vector2(ViewportMargin, -ViewportMargin);
        SetRect(compactStatusText.rectTransform, Vector2.zero, Vector2.one, new Vector2(12f, 4f), new Vector2(-336f, -4f));
        SetRect(previousButton.transform as RectTransform, Vector2.zero, Vector2.one, new Vector2(120f, 4f), new Vector2(-288f, -4f));
        SetRect(navigationText.rectTransform, Vector2.zero, Vector2.one, new Vector2(168f, 4f), new Vector2(-224f, -4f));
        SetRect(nextButton.transform as RectTransform, Vector2.zero, Vector2.one, new Vector2(232f, 4f), new Vector2(-176f, -4f));
        SetRect(closeButton.transform as RectTransform, Vector2.zero, Vector2.one, new Vector2(280f, 4f), new Vector2(-8f, -4f));
        applyingResponsiveLayout = false;
    }

    void SetExpandedContentVisible(bool visible)
    {
        if (titleText != null) titleText.gameObject.SetActive(visible);
        if (summaryText != null) summaryText.gameObject.SetActive(visible);
        if (compactStatusText != null) compactStatusText.gameObject.SetActive(!visible);
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
