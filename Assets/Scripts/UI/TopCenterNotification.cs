using TMPro;
using UnityEngine;

// Lives on the root canvas, so closing the detail panel cannot hide the result.
public sealed class TopCenterNotification : MonoBehaviour
{
    [SerializeField] CanvasGroup group;
    [SerializeField] TMP_Text label;
    const float SlideSeconds = 0.2f;
    const float Margin = 16f;
    const float Padding = 24f;
    const float MaximumWidth = 960f;
    RectTransform rect, canvasRect;
    UnityEngine.UI.Image background;
    float started, duration, canvasWidth, canvasHeight;

    public static TopCenterNotification Ensure(Transform context, TMP_Text fontSource)
    {
        var canvas = context.GetComponentInParent<Canvas>(true);
        if (canvas == null) return null;
        canvas = canvas.rootCanvas;
        var existing = canvas.transform.Find("TopCenterNotification");
        if (existing != null) return existing.GetComponent<TopCenterNotification>();
        var go = new GameObject("TopCenterNotification", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup), typeof(UnityEngine.UI.Image), typeof(TopCenterNotification));
        go.transform.SetParent(canvas.transform, false);
        var rect = (RectTransform)go.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1);
        rect.pivot = new Vector2(0.5f, 1); rect.anchoredPosition = new Vector2(0, -Margin); rect.sizeDelta = new Vector2(MaximumWidth, 52);
        var overlay = go.GetComponent<Canvas>(); overlay.overrideSorting = true; overlay.sortingOrder = 30000;
        var background = go.GetComponent<UnityEngine.UI.Image>(); background.color = DesignTokens.NotificationSuccessBackground; background.raycastTarget = false;
        var notice = go.GetComponent<TopCenterNotification>();
        notice.group = go.GetComponent<CanvasGroup>(); notice.group.alpha = 0;
        notice.group.blocksRaycasts = false; notice.group.interactable = false;
        var text = new GameObject("Message", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TMP_Text>();
        text.transform.SetParent(go.transform, false);
        text.rectTransform.anchorMin = Vector2.zero; text.rectTransform.anchorMax = Vector2.one;
        text.rectTransform.offsetMin = new Vector2(Padding, 12); text.rectTransform.offsetMax = new Vector2(-Padding, -12);
        if (fontSource != null && fontSource.font != null) text.font = fontSource.font;
        text.fontSize = DesignTokens.FontSizeBody; text.alignment = TextAlignmentOptions.Center;
        text.textWrappingMode = TextWrappingModes.Normal; text.raycastTarget = false; text.richText = false;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.enableAutoSizing = true; text.fontSizeMax = DesignTokens.FontSizeBody; text.fontSizeMin = DesignTokens.FontSizeMicro;
        text.text = ""; notice.label = text;
        return notice;
    }
    public void Show(string message, bool error, float displaySeconds = -1f)
    {
        if (group == null || label == null) return;
        rect = (RectTransform)transform;
        canvasRect = GetComponentInParent<Canvas>().rootCanvas.transform as RectTransform;
        background = GetComponent<UnityEngine.UI.Image>();
        gameObject.SetActive(true);
        label.text = message; label.color = error ? DesignTokens.Error : DesignTokens.TextPrimary;
        label.textWrappingMode = TextWrappingModes.Normal; label.overflowMode = TextOverflowModes.Ellipsis;
        label.richText = false; label.fontSizeMax = DesignTokens.FontSizeBody; label.fontSizeMin = DesignTokens.FontSizeMicro;
        background.color = error ? DesignTokens.NotificationErrorBackground : DesignTokens.NotificationSuccessBackground;
        ResizeToMessage();
        started = Time.unscaledTime;
        duration = SlideSeconds * 2 + (displaySeconds > 0 ? displaySeconds : error ? 8f : 1f);
        group.alpha = 1;
        rect.anchoredPosition = new Vector2(0, rect.rect.height);
        transform.SetAsLastSibling();
    }

    void ResizeToMessage()
    {
        canvasWidth = canvasRect.rect.width; canvasHeight = canvasRect.rect.height;
        float availableWidth = Mathf.Max(1f, canvasWidth - Margin * 2);
        // Measure at the normal font size first, then wrap long paths and error details.
        label.enableAutoSizing = false; label.fontSize = DesignTokens.FontSizeBody;
        float preferredWidth = label.GetPreferredValues(label.text, Mathf.Infinity, Mathf.Infinity).x + Padding * 2;
        float width = Mathf.Min(availableWidth, Mathf.Min(MaximumWidth, Mathf.Max(480f, preferredWidth)));
        float padding = Mathf.Min(Padding, width / 4f);
        label.rectTransform.offsetMin = new Vector2(padding, 12);
        label.rectTransform.offsetMax = new Vector2(-padding, -12);
        var preferred = label.GetPreferredValues(label.text, width - padding * 2, Mathf.Infinity);
        float height = Mathf.Min(Mathf.Max(52f, preferred.y + 24f), Mathf.Max(1f, canvasHeight - Margin * 2));
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1);
        rect.pivot = new Vector2(0.5f, 1);
        rect.sizeDelta = new Vector2(width, height);
        label.enableAutoSizing = true;
    }

    void Update()
    {
        if (duration <= 0) return;
        if (canvasWidth != canvasRect.rect.width || canvasHeight != canvasRect.rect.height) ResizeToMessage();
        float elapsed = Time.unscaledTime - started;
        float progress = elapsed < SlideSeconds ? Mathf.Clamp01(elapsed / SlideSeconds) : Mathf.Clamp01((duration - elapsed) / SlideSeconds);
        float eased = 1f - Mathf.Pow(1f - progress, 3f);
        rect.anchoredPosition = new Vector2(0, Mathf.Lerp(rect.rect.height, -Margin, eased));
        if (elapsed >= duration) { duration = 0; group.alpha = 0; label.text = ""; }
    }
}
