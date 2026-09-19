using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Lives on the root canvas, so closing the detail panel cannot hide the result.
public sealed class TopCenterNotification : MonoBehaviour
{
    [SerializeField] CanvasGroup group;
    [SerializeField] TMP_Text label;
    float started, duration;

    public static TopCenterNotification Ensure(Transform context, TMP_Text fontSource)
    {
        var canvas = context.GetComponentInParent<Canvas>(true);
        if (canvas == null) return null;
        canvas = canvas.rootCanvas;
        var existing = canvas.transform.Find("TopCenterNotification");
        if (existing != null) return existing.GetComponent<TopCenterNotification>();
        var go = new GameObject("TopCenterNotification", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup), typeof(Image), typeof(TopCenterNotification));
        go.transform.SetParent(canvas.transform, false);
        var rect = (RectTransform)go.transform;
        rect.anchorMin = new Vector2(0.1f, 1); rect.anchorMax = new Vector2(0.9f, 1);
        rect.pivot = new Vector2(0.5f, 1); rect.anchoredPosition = new Vector2(0, -16); rect.sizeDelta = new Vector2(0, 76);
        var overlay = go.GetComponent<Canvas>(); overlay.overrideSorting = true; overlay.sortingOrder = 30000;
        var background = go.GetComponent<Image>(); background.color = DesignTokens.BgSecondary; background.raycastTarget = false;
        var notice = go.GetComponent<TopCenterNotification>();
        notice.group = go.GetComponent<CanvasGroup>(); notice.group.alpha = 0;
        notice.group.blocksRaycasts = false; notice.group.interactable = false;
        var text = new GameObject("Message", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TMP_Text>();
        text.transform.SetParent(go.transform, false);
        text.rectTransform.anchorMin = Vector2.zero; text.rectTransform.anchorMax = Vector2.one;
        text.rectTransform.offsetMin = new Vector2(16, 8); text.rectTransform.offsetMax = new Vector2(-16, -8);
        if (fontSource != null && fontSource.font != null) text.font = fontSource.font;
        text.fontSize = DesignTokens.FontSizeBody; text.alignment = TextAlignmentOptions.Center;
        text.textWrappingMode = TextWrappingModes.Normal; text.raycastTarget = false; text.richText = false;
        text.text = ""; notice.label = text;
        return notice;
    }
    public void Show(string message, bool error)
    {
        label.text = message; label.color = error ? DesignTokens.Error : DesignTokens.TextPrimary;
        started = Time.unscaledTime; duration = error ? 8 : 6;
        group.alpha = 0;
        transform.SetAsLastSibling();
    }
    void Update()
    {
        float elapsed = Time.unscaledTime - started;
        group.alpha = duration <= 0 ? 0 : Mathf.Min(Mathf.Clamp01(elapsed / 0.15f), Mathf.Clamp01((duration - elapsed) / 0.25f));
        if (elapsed >= duration) { duration = 0; label.text = ""; }
    }
}
