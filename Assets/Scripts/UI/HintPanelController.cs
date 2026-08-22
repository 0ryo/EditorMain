using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class HintPanelController : MonoBehaviour
{
    const string ButtonName = "Button_Hints";
    const string PanelName = "Panel_Hints";

    [SerializeField] Button openButton;
    [SerializeField] Button closeButton;

    public static HintPanelController Ensure(Transform parent)
    {
        if (parent == null) return null;

        var existingPanel = parent.Find(PanelName);
        var controller = existingPanel != null ? existingPanel.GetComponent<HintPanelController>() : null;
        if (controller == null)
        {
            controller = BuildPanel(parent);
        }

        controller.EnsureOpenButton(parent);
        controller.WireButtons();
        UiRoundedTheme.ApplyToHierarchy(controller.transform, DesignTokens.CornerRadius);
        if (controller.openButton != null)
        {
            UiRoundedTheme.ApplyToHierarchy(controller.openButton.transform, DesignTokens.CornerRadius);
        }
        controller.openButton?.transform.SetAsLastSibling();
        controller.transform.SetAsLastSibling();
        return controller;
    }

    void Awake()
    {
        EnsureOpenButton(transform.parent);
        WireButtons();
    }

    void EnsureOpenButton(Transform parent)
    {
        if (openButton == null)
        {
            openButton = parent?.Find(ButtonName)?.GetComponent<Button>();
        }

        if (openButton == null && parent != null)
        {
            openButton = CreateButton(ButtonName, parent, "\u30D2\u30F3\u30C8");
        }

        var rect = openButton != null ? openButton.transform as RectTransform : null;
        if (rect == null) return;
        rect.anchorMin = new Vector2(1f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(1f, 1f);
        rect.offsetMin = new Vector2(-180f, -52f);
        rect.offsetMax = new Vector2(-100f, -12f);
    }

    void WireButtons()
    {
        if (openButton != null)
        {
            openButton.onClick.RemoveListener(Show);
            openButton.onClick.AddListener(Show);
        }

        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(Hide);
            closeButton.onClick.AddListener(Hide);
        }
    }

    public void Show()
    {
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    static HintPanelController BuildPanel(Transform parent)
    {
        var root = CreateRect(PanelName, parent);
        root.anchorMin = new Vector2(0.5f, 0.5f);
        root.anchorMax = new Vector2(0.5f, 0.5f);
        root.pivot = new Vector2(0.5f, 0.5f);
        root.sizeDelta = new Vector2(480f, 280f);
        root.anchoredPosition = Vector2.zero;

        var image = root.gameObject.AddComponent<Image>();
        image.color = DesignTokens.Surface;
        var outline = root.gameObject.AddComponent<Outline>();
        outline.effectColor = DesignTokens.Divider;
        outline.effectDistance = new Vector2(1f, -1f);

        var title = CreateText("Text_Title", root, "\u30D2\u30F3\u30C8", DesignTokens.FontSizeHeading, DesignTokens.TextPrimary);
        SetRect(title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(24f, -64f), new Vector2(-24f, -24f));

        var body = CreateText("Text_Body", root, "\u30D2\u30F3\u30C8\u306F\u6E96\u5099\u4E2D\u3067\u3059\u3002", DesignTokens.FontSizeBody, DesignTokens.TextSecondary);
        body.alignment = TextAlignmentOptions.TopLeft;
        body.enableWordWrapping = true;
        SetRect(body.rectTransform, Vector2.zero, Vector2.one, new Vector2(24f, 80f), new Vector2(-24f, -88f));

        var close = CreateButton("Button_Close", root, "\u9589\u3058\u308B");
        SetRect(close.GetComponent<RectTransform>(), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-104f, 24f), new Vector2(-24f, 64f));

        var controller = root.gameObject.AddComponent<HintPanelController>();
        controller.closeButton = close;
        root.gameObject.SetActive(false);
        return controller;
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
