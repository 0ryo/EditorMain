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
    const float MaximumWidth = 1100f;
    // Below the header (88) and the 3D screen toolbar row (to ~152), so the notice never covers tabs, Undo or camera buttons.
    const float InfoSeconds = 3f, ErrorSeconds = 8f, ActionWidth = 160f, HeaderBottom = 160f, ModalHeaderBottom = 92f, GizmoClearance = 100f, SlideDistance = 14f;
    // About 16 px on a 1440x900 window (the SkillSync root is scaled 1.44 and the canvas reference is 2560 wide).
    const int NoticeFontSize = 20, HistoryFontSize = 18, MaxHistory = 20;
    const int MaxQueued = 4;
    struct HistoryItem { public string message; public bool error; public DateTime time; }
    readonly List<HistoryItem> history = new List<HistoryItem>();
    RectTransform historyRoot;
    TMP_Text historyText;
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
        var go = new GameObject("TopCenterNotification", typeof(RectTransform), typeof(Canvas), typeof(UnityEngine.UI.GraphicRaycaster), typeof(CanvasGroup), typeof(UnityEngine.UI.Image), typeof(TopCenterNotification));
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
        text.fontSize = NoticeFontSize; text.alignment = TextAlignmentOptions.Center;
        text.textWrappingMode = TextWrappingModes.Normal; text.raycastTarget = false; text.richText = false;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.enableAutoSizing = true; text.fontSizeMax = NoticeFontSize; text.fontSizeMin = DesignTokens.FontSizeBody;
        text.text = ""; notice.label = text;
        return notice;
    }
    public void Show(string message, bool error, float displaySeconds = -1f) => Show(message, error, displaySeconds, null, null);

    public void Show(string message, bool error, float displaySeconds, string actionLabel, Action action)
    {
        if (group == null || label == null) return;
        var entry = new Entry { message = message, error = error, seconds = displaySeconds, actionLabel = actionLabel, action = action };
        bool repeat = duration > 0 && current.message == message && current.error == error && current.actionLabel == actionLabel;
        if (!repeat && !string.IsNullOrEmpty(message)) RecordHistory(message, error);
        // The same message only restarts the timer; a different one waits so an error is never overwritten.
        if (repeat) { current = entry; startPending = true; return; }
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
        EnsureContext();
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
        group.alpha = 0; // Update fades it in
        group.blocksRaycasts = group.interactable = hasAction;
        rect.anchoredPosition = new Vector2(shownX, shownY + SlideDistance * uiScale);
        transform.SetAsLastSibling();
    }

    void EnsureContext()
    {
        rect = (RectTransform)transform;
        canvasRect = GetComponentInParent<Canvas>().rootCanvas.transform as RectTransform;
        background = GetComponent<UnityEngine.UI.Image>();
        if (skillSync == null) skillSync = canvasRect.GetComponentInChildren<SkillSyncDesignView>(true);
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
            text.fontSizeMax = NoticeFontSize;
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
        // Centered over the 3D screen, narrower than it by the orientation gizmo at its top right, so the notice never covers it.
        var area = skillSync.viewport != null && skillSync.viewport.rect.width > 0f ? skillSync.viewport : root;
        var a = area.rect;
        // While a dialog is open the notice moves up under the header, so it never covers a dialog's title.
        float top = EditWorkspace.HasOpenModal ? ModalHeaderBottom : HeaderBottom;
        var anchor = canvasRect.InverseTransformPoint(root.TransformPoint(new Vector3(r.center.x, r.yMax - top, 0f)));
        var center = canvasRect.InverseTransformPoint(area.TransformPoint(new Vector3(a.center.x, 0f, 0f)));
        var left = canvasRect.InverseTransformPoint(area.TransformPoint(new Vector3(a.xMin, 0f, 0f)));
        var right = canvasRect.InverseTransformPoint(area.TransformPoint(new Vector3(a.xMax, 0f, 0f)));
        uiScale = SkillSyncDesignLayout.Scale;
        shownX = center.x - canvasRect.rect.center.x;
        shownY = anchor.y - canvasRect.rect.yMax - Margin * 0.5f * uiScale;
        float clearance = area == root ? 0f : GizmoClearance * uiScale;
        availableWidthLimit = Mathf.Max(1f, Mathf.Min(canvasWidth, right.x - left.x) - Margin * 2 * uiScale - clearance * 2f);
    }

    float availableWidthLimit;

    void ResizeToMessage()
    {
        canvasWidth = canvasRect.rect.width; canvasHeight = canvasRect.rect.height;
        ComputePlacement();
        float k = uiScale, pad = Padding * k, vertical = 12f * k;
        bool hasAction = actionRect != null && actionRect.gameObject.activeSelf;
        float actionSpace = hasAction ? (ActionWidth + 12f) * k : 0f;
        label.fontSizeMax = NoticeFontSize * k; label.fontSizeMin = DesignTokens.FontSizeBody * k;
        // Measure at the normal font size first, then wrap long paths and error details.
        label.enableAutoSizing = false; label.fontSize = NoticeFontSize * k;
        float preferredWidth = label.GetPreferredValues(label.text, Mathf.Infinity, Mathf.Infinity).x + pad * 2 + actionSpace;
        float width = Mathf.Min(availableWidthLimit, Mathf.Min(MaximumWidth * k, Mathf.Max(520f * k, preferredWidth)));
        float padding = Mathf.Min(pad, width / 4f);
        label.rectTransform.offsetMin = new Vector2(padding, vertical);
        label.rectTransform.offsetMax = new Vector2(-padding - actionSpace, -vertical);
        var preferred = label.GetPreferredValues(label.text, width - padding * 2 - actionSpace, Mathf.Infinity);
        float height = Mathf.Min(Mathf.Max(60f * k, preferred.y + vertical * 2), Mathf.Max(1f, canvasHeight - Margin * 2));
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1);
        rect.pivot = new Vector2(0.5f, 1);
        rect.sizeDelta = new Vector2(width, height);
        label.enableAutoSizing = true;
        if (hasAction)
        {
            actionRect.anchorMin = actionRect.anchorMax = new Vector2(1f, 0.5f); actionRect.pivot = new Vector2(1f, 0.5f);
            actionRect.sizeDelta = new Vector2(ActionWidth * k, Mathf.Min(40f * k, height - 8f * k));
            actionRect.anchoredPosition = new Vector2(-padding * 0.5f, 0f);
            actionText.fontSizeMax = NoticeFontSize * k; actionText.fontSizeMin = DesignTokens.FontSizeBody * k; actionText.fontSize = NoticeFontSize * k;
        }
    }

    void Update()
    {
        if (historyRoot != null && historyRoot.gameObject.activeSelf && Application.isFocused && EditInput.CancelPressedThisFrame()) CloseHistory();
        if (duration <= 0) return;
        if (!Application.isFocused) { startPending = true; return; }
        if (startPending) { started = Time.unscaledTime; startPending = false; }
        if (canvasWidth != canvasRect.rect.width || canvasHeight != canvasRect.rect.height) ResizeToMessage();
        else ComputePlacement();
        float elapsed = Time.unscaledTime - started;
        float progress = elapsed < SlideSeconds ? Mathf.Clamp01(elapsed / SlideSeconds) : Mathf.Clamp01((duration - elapsed) / SlideSeconds);
        float eased = 1f - Mathf.Pow(1f - progress, 3f);
        // Fade plus a short slide from just above the final spot: it never sweeps across the header.
        rect.anchoredPosition = new Vector2(shownX, shownY + (1f - eased) * SlideDistance * uiScale);
        group.alpha = eased;
        if (elapsed >= duration)
        {
            duration = 0; group.alpha = 0; label.text = "";
            group.blocksRaycasts = group.interactable = false; background.raycastTarget = false;
            current = default;
            if (queue.Count > 0) Display(queue.Dequeue());
        }
    }

    // ---- Notification history: the last MaxHistory messages, newest first, opened from the header's 通知履歴 button. ----
    void RecordHistory(string message, bool error)
    {
        history.Add(new HistoryItem { message = message, error = error, time = DateTime.Now });
        if (history.Count > MaxHistory) history.RemoveAt(0);
        if (historyRoot != null && historyRoot.gameObject.activeSelf) RefreshHistory();
    }

    public void ToggleHistory()
    {
        if (historyRoot != null && historyRoot.gameObject.activeSelf) CloseHistory();
        else OpenHistory();
    }

    public void CloseHistory()
    {
        if (historyRoot != null) historyRoot.gameObject.SetActive(false);
    }

    void OpenHistory()
    {
        EnsureContext();
        canvasWidth = canvasRect.rect.width; canvasHeight = canvasRect.rect.height;
        ComputePlacement();
        if (historyRoot == null) BuildHistory();
        var panel = (RectTransform)historyRoot.Find("Panel");
        panel.localScale = Vector3.one * uiScale;
        panel.anchoredPosition = new Vector2(shownX, shownY);
        RefreshHistory();
        historyRoot.gameObject.SetActive(true);
        historyRoot.SetAsLastSibling();
    }

    void BuildHistory()
    {
        var font = label != null ? label.font : null;
        var root = new GameObject("NotificationHistory", typeof(RectTransform), typeof(Canvas), typeof(UnityEngine.UI.GraphicRaycaster),
            typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.Button), typeof(EditorUiInputBlocker));
        root.transform.SetParent(canvasRect, false);
        historyRoot = (RectTransform)root.transform;
        historyRoot.anchorMin = Vector2.zero; historyRoot.anchorMax = Vector2.one; historyRoot.offsetMin = historyRoot.offsetMax = Vector2.zero;
        var overlay = root.GetComponent<Canvas>(); overlay.overrideSorting = true; overlay.sortingOrder = 30001;
        var dimmer = root.GetComponent<UnityEngine.UI.Image>(); dimmer.color = new Color(0f, 0f, 0f, 0.01f); dimmer.raycastTarget = true;
        root.GetComponent<UnityEngine.UI.Button>().onClick.AddListener(CloseHistory); // click outside closes

        var panel = new GameObject("Panel", typeof(RectTransform), typeof(SkillSyncRoundedGraphic), typeof(UnityEngine.UI.Button)).GetComponent<RectTransform>();
        panel.SetParent(historyRoot, false);
        panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(0.5f, 1f);
        panel.sizeDelta = new Vector2(760f, 480f);
        var surface = panel.GetComponent<SkillSyncRoundedGraphic>();
        surface.color = DesignTokens.Surface; surface.radius = 12f; surface.borderColor = DesignTokens.Divider; surface.raycastTarget = true;
        panel.GetComponent<UnityEngine.UI.Button>().transition = UnityEngine.UI.Selectable.Transition.None; // swallows clicks inside the panel

        var title = NewHistoryText("Title", panel, font, 22, DesignTokens.TextPrimary);
        title.text = "通知履歴"; title.fontStyle = FontStyles.Bold;
        title.rectTransform.anchorMin = title.rectTransform.anchorMax = title.rectTransform.pivot = new Vector2(0f, 1f);
        title.rectTransform.anchoredPosition = new Vector2(24f, -16f); title.rectTransform.sizeDelta = new Vector2(400f, 36f);
        var note = NewHistoryText("Note", panel, font, 16, DesignTokens.TextSecondary);
        note.text = "直近" + MaxHistory + "件（新しい順）";
        note.alignment = TextAlignmentOptions.MidlineRight;
        note.rectTransform.anchorMin = note.rectTransform.anchorMax = note.rectTransform.pivot = new Vector2(1f, 1f);
        note.rectTransform.anchoredPosition = new Vector2(-124f, -18f); note.rectTransform.sizeDelta = new Vector2(300f, 32f);

        var close = new GameObject("Button_Close", typeof(RectTransform), typeof(SkillSyncRoundedGraphic), typeof(UnityEngine.UI.Button)).GetComponent<RectTransform>();
        close.SetParent(panel, false);
        close.anchorMin = close.anchorMax = close.pivot = new Vector2(1f, 1f);
        close.anchoredPosition = new Vector2(-20f, -14f); close.sizeDelta = new Vector2(88f, 40f);
        var closeSurface = close.GetComponent<SkillSyncRoundedGraphic>();
        closeSurface.color = Color.white; closeSurface.radius = 8f; closeSurface.borderColor = DesignTokens.Divider;
        var closeButton = close.GetComponent<UnityEngine.UI.Button>(); closeButton.targetGraphic = closeSurface;
        var closeColors = closeButton.colors; closeColors.highlightedColor = new Color(0.9f, 0.9f, 0.9f, 1f); closeColors.pressedColor = new Color(0.78f, 0.78f, 0.78f, 1f); closeButton.colors = closeColors;
        closeButton.onClick.AddListener(CloseHistory);
        var closeLabel = NewHistoryText("Label", close, font, 18, DesignTokens.TextPrimary);
        closeLabel.text = "閉じる"; closeLabel.alignment = TextAlignmentOptions.Center;
        closeLabel.rectTransform.anchorMin = Vector2.zero; closeLabel.rectTransform.anchorMax = Vector2.one;
        closeLabel.rectTransform.offsetMin = closeLabel.rectTransform.offsetMax = Vector2.zero;

        var scrollRoot = new GameObject("List", typeof(RectTransform), typeof(UnityEngine.UI.ScrollRect)).GetComponent<RectTransform>();
        scrollRoot.SetParent(panel, false);
        scrollRoot.anchorMin = Vector2.zero; scrollRoot.anchorMax = Vector2.one;
        scrollRoot.offsetMin = new Vector2(24f, 20f); scrollRoot.offsetMax = new Vector2(-24f, -66f);
        var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.RectMask2D)).GetComponent<RectTransform>();
        viewport.SetParent(scrollRoot, false);
        viewport.anchorMin = Vector2.zero; viewport.anchorMax = Vector2.one; viewport.offsetMin = viewport.offsetMax = Vector2.zero;
        var viewportImage = viewport.GetComponent<UnityEngine.UI.Image>(); viewportImage.color = Color.clear; viewportImage.raycastTarget = true;
        historyText = NewHistoryText("Content", viewport, font, HistoryFontSize, DesignTokens.TextPrimary);
        historyText.richText = true; historyText.lineSpacing = 10f;
        var content = historyText.rectTransform;
        content.anchorMin = new Vector2(0f, 1f); content.anchorMax = new Vector2(1f, 1f); content.pivot = new Vector2(0.5f, 1f);
        content.offsetMin = content.offsetMax = Vector2.zero;
        historyText.gameObject.AddComponent<UnityEngine.UI.ContentSizeFitter>().verticalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
        var scroll = scrollRoot.GetComponent<UnityEngine.UI.ScrollRect>();
        scroll.viewport = viewport; scroll.content = content; scroll.horizontal = false; scroll.vertical = true;
        scroll.movementType = UnityEngine.UI.ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 30f;
        historyRoot.gameObject.SetActive(false);
    }

    static TMP_Text NewHistoryText(string name, Transform parent, TMP_FontAsset font, int size, Color color)
    {
        var text = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
        text.transform.SetParent(parent, false);
        if (font != null) text.font = font;
        text.fontSize = size; text.color = color; text.raycastTarget = false; text.richText = false;
        text.alignment = TextAlignmentOptions.TopLeft; text.textWrappingMode = TextWrappingModes.Normal;
        return text;
    }

    void RefreshHistory()
    {
        if (historyText == null) return;
        if (history.Count == 0) { historyText.text = "まだ通知はありません。"; return; }
        var builder = new System.Text.StringBuilder();
        string quiet = ColorUtility.ToHtmlStringRGB(DesignTokens.TextSecondary), error = ColorUtility.ToHtmlStringRGB(DesignTokens.Error);
        for (int i = history.Count - 1; i >= 0; i--)
        {
            var item = history[i];
            if (builder.Length > 0) builder.Append('\n');
            builder.Append("<color=#").Append(quiet).Append('>').Append(item.time.ToString("HH:mm:ss")).Append("</color>  ");
            if (item.error) builder.Append("<color=#").Append(error).Append('>');
            builder.Append("<noparse>").Append(item.message).Append("</noparse>");
            if (item.error) builder.Append("</color>");
        }
        historyText.text = builder.ToString();
    }
}
