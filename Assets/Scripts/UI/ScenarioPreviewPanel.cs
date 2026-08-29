using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class ScenarioPreviewPanel : MonoBehaviour
{
    const float AutoAdvanceSeconds = 2f;

    CurriculumGraphService graph;
    readonly List<ScenarioNode> steps = new List<ScenarioNode>();
    int currentIndex;
    Coroutine autoPlayCoroutine;

    TMP_Text progressText;
    TMP_Text stepTitleText;
    TMP_Text durationText;
    TMP_Text bodyText;
    TMP_Text supplementText;
    TMP_Text cautionText;
    TMP_Text conditionsText;
    Button previousButton;
    Button nextButton;
    Button autoPlayButton;

    public static ScenarioPreviewPanel Ensure(RectTransform parent, ScenarioPreviewPanel existing = null)
    {
        if (existing != null)
        {
            existing.BuildUiIfNeeded();
            return existing;
        }
        if (parent == null) return null;

        var found = parent.Find("ScenarioPreviewPanel");
        var panel = found != null ? found.GetComponent<ScenarioPreviewPanel>() : null;
        if (panel == null)
        {
            var root = found != null
                ? found.gameObject
                : new GameObject("ScenarioPreviewPanel", typeof(RectTransform), typeof(Image), typeof(Outline));
            if (found == null) root.transform.SetParent(parent, false);
            panel = root.GetComponent<ScenarioPreviewPanel>();
            if (panel == null) panel = root.AddComponent<ScenarioPreviewPanel>();
        }

        panel.BuildUiIfNeeded();
        return panel;
    }

    public void Show(CurriculumGraphService service)
    {
        if (service == null) return;

        graph = service;
        steps.Clear();
        steps.AddRange(graph.GetDisplayOrderedSteps().Where(step => step != null));
        if (steps.Count == 0) return;

        StopAutoPlay();
        currentIndex = 0;
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        RenderCurrentStep();
    }

    public void Hide()
    {
        StopAutoPlay();
        if (gameObject.activeSelf) gameObject.SetActive(false);
    }

    void Update()
    {
        if (EditWorkspace.IsTypingIntoInputField()) return;
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Hide();
            return;
        }
        if (Input.GetKeyDown(KeyCode.LeftArrow)) ShowPrevious();
        if (Input.GetKeyDown(KeyCode.RightArrow)) ShowNext();
        if (Input.GetKeyDown(KeyCode.Space)) ToggleAutoPlay();
    }

    void OnDisable()
    {
        StopAutoPlay();
    }

    void BuildUiIfNeeded()
    {
        var root = transform as RectTransform;
        if (root == null) return;

        root.anchorMin = new Vector2(0.5f, 0f);
        root.anchorMax = new Vector2(0.5f, 1f);
        root.pivot = new Vector2(0.5f, 0.5f);
        root.sizeDelta = new Vector2(620f, -32f);
        root.anchoredPosition = Vector2.zero;

        var image = GetComponent<Image>();
        if (image == null) image = gameObject.AddComponent<Image>();
        image.color = DesignTokens.Surface;

        var outline = GetComponent<Outline>();
        if (outline == null) outline = gameObject.AddComponent<Outline>();
        outline.effectColor = DesignTokens.Divider;
        outline.effectDistance = new Vector2(2f, -2f);
        outline.useGraphicAlpha = false;

        if (progressText != null) return;

        var title = CreateText("Text_Title", transform, "シナリオプレビュー", DesignTokens.FontSizeSubheading, DesignTokens.TextPrimary);
        title.fontStyle = FontStyles.Bold;
        title.alignment = TextAlignmentOptions.MidlineLeft;
        SetRect(title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(16f, -46f), new Vector2(-150f, -8f));

        progressText = CreateText("Text_Progress", transform, "1 / 1", DesignTokens.FontSizeBody, DesignTokens.TextSecondary);
        progressText.alignment = TextAlignmentOptions.Center;
        SetRect(progressText.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-160f, -46f), new Vector2(-72f, -8f));

        var closeButton = CreateButton("Button_Close", transform, "閉じる", DesignTokens.BgSecondary, DesignTokens.TextPrimary);
        SetRect(closeButton.transform as RectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-68f, -46f), new Vector2(-12f, -8f));
        closeButton.onClick.AddListener(Hide);

        var scrollRoot = new GameObject("Scroll_Content", typeof(RectTransform), typeof(ScrollRect));
        var scrollRt = scrollRoot.GetComponent<RectTransform>();
        scrollRt.SetParent(transform, false);
        SetRect(scrollRt, Vector2.zero, Vector2.one, new Vector2(16f, 60f), new Vector2(-16f, -54f));

        var viewportGo = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D));
        var viewport = viewportGo.GetComponent<RectTransform>();
        viewport.SetParent(scrollRt, false);
        SetRect(viewport, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        viewportGo.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0f);

        var contentGo = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        var content = contentGo.GetComponent<RectTransform>();
        content.SetParent(viewport, false);
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.offsetMin = Vector2.zero;
        content.offsetMax = Vector2.zero;

        var layout = contentGo.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(4, 12, 4, 4);
        layout.spacing = DesignTokens.SpaceSm;
        layout.childControlHeight = true;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = true;

        var fitter = contentGo.GetComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var scroll = scrollRoot.GetComponent<ScrollRect>();
        scroll.viewport = viewport;
        scroll.content = content;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 24f;

        stepTitleText = CreateContentText("Text_StepTitle", content, 32f, DesignTokens.FontSizeSubheading, DesignTokens.TextPrimary);
        stepTitleText.fontStyle = FontStyles.Bold;
        durationText = CreateContentText("Text_Duration", content, 24f, DesignTokens.FontSizeCaption, DesignTokens.TextSecondary);
        bodyText = CreateContentText("Text_Body", content, 92f, DesignTokens.FontSizeBody, DesignTokens.TextPrimary);
        supplementText = CreateContentText("Text_Supplement", content, 64f, DesignTokens.FontSizeBody, DesignTokens.TextSecondary);
        cautionText = CreateContentText("Text_Caution", content, 64f, DesignTokens.FontSizeBody, DesignTokens.Error);
        conditionsText = CreateContentText("Text_Conditions", content, 88f, DesignTokens.FontSizeBody, DesignTokens.TextPrimary);

        previousButton = CreateButton("Button_Previous", transform, "前の手順", DesignTokens.BgSecondary, DesignTokens.TextPrimary);
        SetRect(previousButton.transform as RectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(16f, 12f), new Vector2(116f, 52f));
        previousButton.onClick.AddListener(ShowPrevious);

        autoPlayButton = CreateButton("Button_AutoPlay", transform, "自動再生", DesignTokens.Accent, DesignTokens.ButtonTextLight);
        SetRect(autoPlayButton.transform as RectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-60f, 12f), new Vector2(60f, 52f));
        autoPlayButton.onClick.AddListener(ToggleAutoPlay);

        nextButton = CreateButton("Button_Next", transform, "次の手順", DesignTokens.BgSecondary, DesignTokens.TextPrimary);
        SetRect(nextButton.transform as RectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-116f, 12f), new Vector2(-16f, 52f));
        nextButton.onClick.AddListener(ShowNext);

        gameObject.SetActive(false);
    }

    void RenderCurrentStep()
    {
        if (graph == null || steps.Count == 0) return;

        currentIndex = Mathf.Clamp(currentIndex, 0, steps.Count - 1);
        var node = steps[currentIndex];
        var data = node.step ?? new StepNodeData();

        progressText.text = $"{currentIndex + 1} / {steps.Count}";
        stepTitleText.text = string.IsNullOrWhiteSpace(data.title) ? $"手順 {currentIndex + 1}" : data.title;
        durationText.text = data.durationMinutes > 0 ? $"所要時間: {data.durationMinutes}分" : "所要時間: 未設定";
        bodyText.text = FormatSection("本文", data.body);
        supplementText.text = FormatSection("補足", data.supplement);
        cautionText.text = FormatSection("注意事項", data.caution);
        conditionsText.text = BuildConditionsText(node.nodeId);

        previousButton.interactable = currentIndex > 0;
        nextButton.interactable = currentIndex < steps.Count - 1;
    }

    string BuildConditionsText(string stepNodeId)
    {
        var labelById = PlacedObjectOptionProvider.GetOptions()
            .GroupBy(option => option.id)
            .ToDictionary(group => group.Key, group => group.First().label);
        var conditions = graph.GetConditionNodesForStep(stepNodeId);
        var builder = new StringBuilder("<b>達成条件</b>");
        for (int i = 0; i < conditions.Count; i++)
        {
            var condition = conditions[i]?.condition;
            if (condition == null) continue;
            builder.Append('\n');
            builder.Append(i + 1);
            builder.Append(". ");
            builder.Append(ResolveObjectLabel(condition.objectAId, labelById));
            builder.Append(" を ");
            builder.Append(ResolveObjectLabel(condition.objectBId, labelById));
            builder.Append(" に近づける");
        }
        return builder.ToString();
    }

    static string ResolveObjectLabel(string id, Dictionary<string, string> labelById)
    {
        if (string.IsNullOrWhiteSpace(id)) return "未設定";
        return labelById.TryGetValue(id, out var label) ? label : $"参照切れ: {id}";
    }

    static string FormatSection(string heading, string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? $"<b>{heading}</b>\n（未入力）"
            : $"<b>{heading}</b>\n{value}";
    }

    void ShowPrevious()
    {
        StopAutoPlay();
        if (currentIndex <= 0) return;
        currentIndex--;
        RenderCurrentStep();
    }

    void ShowNext()
    {
        StopAutoPlay();
        if (currentIndex >= steps.Count - 1) return;
        currentIndex++;
        RenderCurrentStep();
    }

    void ToggleAutoPlay()
    {
        if (autoPlayCoroutine != null)
        {
            StopAutoPlay();
            return;
        }

        if (currentIndex >= steps.Count - 1) currentIndex = 0;
        RenderCurrentStep();
        autoPlayCoroutine = StartCoroutine(AutoPlay());
        SetButtonLabel(autoPlayButton, "停止");
    }

    IEnumerator AutoPlay()
    {
        while (currentIndex < steps.Count - 1)
        {
            yield return new WaitForSecondsRealtime(AutoAdvanceSeconds);
            currentIndex++;
            RenderCurrentStep();
        }

        autoPlayCoroutine = null;
        SetButtonLabel(autoPlayButton, "自動再生");
    }

    void StopAutoPlay()
    {
        if (autoPlayCoroutine != null)
        {
            StopCoroutine(autoPlayCoroutine);
            autoPlayCoroutine = null;
        }
        SetButtonLabel(autoPlayButton, "自動再生");
    }

    static TMP_Text CreateContentText(string name, Transform parent, float height, float fontSize, Color color)
    {
        var text = CreateText(name, parent, string.Empty, fontSize, color);
        text.alignment = TextAlignmentOptions.TopLeft;
        text.enableWordWrapping = true;
        text.richText = true;
        var layout = text.gameObject.AddComponent<LayoutElement>();
        layout.minHeight = height;
        layout.preferredHeight = height;
        return text;
    }

    static TMP_Text CreateText(string name, Transform parent, string value, float fontSize, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        var text = go.GetComponent<TextMeshProUGUI>();
        text.text = value;
        text.fontSize = fontSize;
        text.color = color;
        text.raycastTarget = false;
        return text;
    }

    static Button CreateButton(string name, Transform parent, string label, Color background, Color foreground)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(Outline));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        go.GetComponent<Image>().color = background;
        var outline = go.GetComponent<Outline>();
        outline.effectColor = DesignTokens.Divider;
        outline.effectDistance = new Vector2(1f, -1f);

        var text = CreateText("Label", rt, label, DesignTokens.FontSizeBody, foreground);
        text.alignment = TextAlignmentOptions.Center;
        SetRect(text.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        return go.GetComponent<Button>();
    }

    static void SetButtonLabel(Button button, string value)
    {
        if (button == null) return;
        var label = button.GetComponentInChildren<TMP_Text>(true);
        if (label != null) label.text = value;
    }

    static void SetRect(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        if (rt == null) return;
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = offsetMin;
        rt.offsetMax = offsetMax;
    }
}
