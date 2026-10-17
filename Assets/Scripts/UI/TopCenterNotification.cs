using System;
using System.Collections.Generic;
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
    const float InfoSeconds = 3f, ErrorSeconds = 8f, ActionWidth = 160f, HeaderBottom = 88f;
    const int MaxQueued = 4;
    struct Entry { public string message; public bool error; public float seconds; public string actionLabel; public Action action; }
    readonly Queue<Entry> queue = new Queue<Entry>();
    Entry current;
    RectTransform rect, canvasRect, actionRect;
    UnityEngine.UI.Image background;
    UnityEngine.UI.Button actionButton;
    TMP_Text actionText;
    SkillSyncDesignView skillSync;
    float started, duration, canvasWidth, canvasHeight, uiScale = 1f, shownY = -Margin, shownX;
    bool startPending;

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
    public void Show(string message, bool error, float displaySeconds = -1f) => Show(message, error, displaySeconds, null, null);

    public void Show(string message, bool error, float displaySeconds, string actionLabel, Action action)
    {
        if (group == null || label == null) return;
        var entry = new Entry { message = message, error = error, seconds = displaySeconds, actionLabel = actionLabel, action = action };
        // The same message only restarts the timer; a different one waits so an error is never overwritten.
        if (duration > 0 && current.message == message && current.error == error && current.actionLabel == actionLabel) { current = entry; startPending = true; return; }
        if (duration > 0)
        {
            if (queue.Count >= MaxQueued && !DropOldestInfo(error)) return;
            queue.Enqueue(entry);
            return;
        }
        Display(entry);
    }

    // Makes room for a new message by dropping the oldest queued info; returns false if the new info should be discarded.
    bool DropOldestInfo(bool incomingError)
    {
        var kept = new List<Entry>(queue); queue.Clear();
        int drop = kept.FindIndex(e => !e.error);
        if (drop < 0) { foreach (var e in kept) queue.Enqueue(e); return false; }
        kept.RemoveAt(drop);
        foreach (var e in kept) queue.Enqueue(e);
        return true;
    }

    void Display(Entry entry)
    {
        current = entry;
        rect = (RectTransform)transform;
        canvasRect = GetComponentInParent<Canvas>().rootCanvas.transform as RectTransform;
        background = GetComponent<UnityEngine.UI.Image>();
        if (skillSync == null) skillSync = canvasRect.GetComponentInChildren<SkillSyncDesignView>(true);
        bool hasAction = entry.action != null && !string.IsNullOrEmpty(entry.actionLabel);
        EnsureActionButton();
        gameObject.SetActive(true);
        label.text = entry.message; label.color = entry.error ? DesignTokens.Error : DesignTokens.TextPrimary;
        label.textWrappingMode = TextWrappingModes.Normal; label.overflowMode = TextOverflowModes.Ellipsis;
        label.richText = false;
        background.color = entry.error ? DesignTokens.NotificationErrorBackground : DesignTokens.NotificationSuccessBackground;
        background.raycastTarget = hasAction;
        if (actionRect != null) { actionRect.gameObject.SetActive(hasAction); if (hasAction) actionText.text = entry.actionLabel; }
        ResizeToMessage();
        // A native file dialog can block the current frame for longer than the notice lifetime.
        startPending = true;
        duration = SlideSeconds * 2 + (entry.seconds > 0 ? entry.seconds : entry.error ? ErrorSeconds : InfoSeconds);
        group.alpha = 1;
        group.blocksRaycasts = group.interactable = hasAction;
        rect.anchoredPosition = new Vector2(shownX, rect.rect.height);
        transform.SetAsLastSibling();
    }

    void EnsureActionButton()
    {
        if (actionRect != null) return;
        var existing = transform.Find("ActionButton");
        if (existing != null) actionRect = (RectTransform)existing;
        else
        {
            var go = new GameObject("ActionButton", typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.Button), typeof(EditorUiInputBlocker));
            go.transform.SetParent(transform, false);
            go.GetComponent<UnityEngine.UI.Image>().color = DesignTokens.Accent;
            var text = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TMP_Text>();
            text.transform.SetParent(go.transform, false);
            text.rectTransform.anchorMin = Vector2.zero; text.rectTransform.anchorMax = Vector2.one;
            text.rectTransform.offsetMin = text.rectTransform.offsetMax = Vector2.zero;
            if (label.font != null) text.font = label.font;
            text.alignment = TextAlignmentOptions.Center; text.color = Color.white; text.raycastTarget = false; text.richText = false;
            text.enableAutoSizing = true; text.textWrappingMode = TextWrappingModes.NoWrap;
            actionRect = (RectTransform)go.transform;
        }
        actionButton = actionRect.GetComponent<UnityEngine.UI.Button>();
        actionText = actionRect.GetComponentInChildren<TMP_Text>(true);
        actionButton.onClick.RemoveAllListeners();
        actionButton.onClick.AddListener(OnActionClicked);
    }

    void OnActionClicked()
    {
        var action = current.action;
        // Finish the notice first so a failing callback cannot leave a clickable notice behind.
        duration = SlideSeconds * 2; startPending = false; started = Time.unscaledTime - SlideSeconds;
        group.blocksRaycasts = group.interactable = false;
        action?.Invoke();
    }

    // Below the SkillSync header, inside the SkillSync root rect; otherwise centered at the canvas top.
    void ComputePlacement()
    {
        uiScale = 1f; shownX = 0; shownY = -Margin; availableWidthLimit = Mathf.Max(1f, canvasWidth - Margin * 2);
        if (skillSync == null || !skillSync.gameObject.activeInHierarchy) return;
        var root = (RectTransform)skillSync.transform;
        var r = root.rect;
        if (r.width <= 0f || r.height <= 0f) return;
        var anchor = canvasRect.InverseTransformPoint(root.TransformPoint(new Vector3(r.center.x, r.yMax - HeaderBottom, 0f)));
        var left = canvasRect.InverseTransformPoint(root.TransformPoint(new Vector3(r.xMin, r.yMax, 0f)));
        var right = canvasRect.InverseTransformPoint(root.TransformPoint(new Vector3(r.xMax, r.yMax, 0f)));
        uiScale = SkillSyncDesignLayout.Scale;
        shownX = anchor.x - canvasRect.rect.center.x;
        shownY = anchor.y - canvasRect.rect.yMax - Margin * 0.5f * uiScale;
        availableWidthLimit = Mathf.Max(1f, Mathf.Min(canvasWidth, right.x - left.x) - Margin * 2 * uiScale);
    }

    float availableWidthLimit;

    void ResizeToMessage()
    {
        canvasWidth = canvasRect.rect.width; canvasHeight = canvasRect.rect.height;
        ComputePlacement();
        float k = uiScale, pad = Padding * k, vertical = 12f * k;
        bool hasAction = actionRect != null && actionRect.gameObject.activeSelf;
        float actionSpace = hasAction ? (ActionWidth + 12f) * k : 0f;
        label.fontSizeMax = DesignTokens.FontSizeBody * k; label.fontSizeMin = DesignTokens.FontSizeMicro * k;
        // Measure at the normal font size first, then wrap long paths and error details.
        label.enableAutoSizing = false; label.fontSize = DesignTokens.FontSizeBody * k;
        float preferredWidth = label.GetPreferredValues(label.text, Mathf.Infinity, Mathf.Infinity).x + pad * 2 + actionSpace;
        float width = Mathf.Min(availableWidthLimit, Mathf.Min(MaximumWidth * k, Mathf.Max(480f * k, preferredWidth)));
        float padding = Mathf.Min(pad, width / 4f);
        label.rectTransform.offsetMin = new Vector2(padding, vertical);
        label.rectTransform.offsetMax = new Vector2(-padding - actionSpace, -vertical);
        var preferred = label.GetPreferredValues(label.text, width - padding * 2 - actionSpace, Mathf.Infinity);
        float height = Mathf.Min(Mathf.Max(52f * k, preferred.y + vertical * 2), Mathf.Max(1f, canvasHeight - Margin * 2));
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1);
        rect.pivot = new Vector2(0.5f, 1);
        rect.sizeDelta = new Vector2(width, height);
        label.enableAutoSizing = true;
        if (hasAction)
        {
            actionRect.anchorMin = actionRect.anchorMax = new Vector2(1f, 0.5f); actionRect.pivot = new Vector2(1f, 0.5f);
            actionRect.sizeDelta = new Vector2(ActionWidth * k, Mathf.Min(40f * k, height - 8f * k));
            actionRect.anchoredPosition = new Vector2(-padding * 0.5f, 0f);
            actionText.fontSizeMax = DesignTokens.FontSizeBody * k; actionText.fontSizeMin = DesignTokens.FontSizeMicro * k; actionText.fontSize = DesignTokens.FontSizeBody * k;
        }
    }

    void Update()
    {
        if (duration <= 0) return;
        if (!Application.isFocused) { startPending = true; return; }
        if (startPending) { started = Time.unscaledTime; startPending = false; }
        if (canvasWidth != canvasRect.rect.width || canvasHeight != canvasRect.rect.height) ResizeToMessage();
        else ComputePlacement();
        float elapsed = Time.unscaledTime - started;
        float progress = elapsed < SlideSeconds ? Mathf.Clamp01(elapsed / SlideSeconds) : Mathf.Clamp01((duration - elapsed) / SlideSeconds);
        float eased = 1f - Mathf.Pow(1f - progress, 3f);
        rect.anchoredPosition = new Vector2(shownX, Mathf.Lerp(rect.rect.height, shownY, eased));
        if (elapsed >= duration)
        {
            duration = 0; group.alpha = 0; label.text = "";
            group.blocksRaycasts = group.interactable = false; background.raycastTarget = false;
            current = default;
            if (queue.Count > 0) Display(queue.Dequeue());
        }
    }
}
