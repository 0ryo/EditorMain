using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class HintPanelController : MonoBehaviour
{
    const string ButtonName = "Button_Hints";
    const string PanelName = "Panel_Hints";
    const string HintBody =
        "視点操作\n" +
        "・右ドラッグ または 中ドラッグ: 回転\n" +
        "・Shift + 右/中ドラッグ: 平行移動　ホイール: ズーム\n" +
        "・F: 選択へ　1/3/7: 正面/右/上　O: 平行/透視　Home: 初期化\n\n" +
        "オブジェクト編集\n" +
        "・W/A/S/D または 矢印: グリッド幅ずつ移動\n" +
        "・Delete: 削除　Ctrl/Cmd + D: 複製\n" +
        "・Altを押している間: 配置・移動・回転スナップを一時解除\n\n" +
        "各ボタンにポインターを重ねると、その操作の説明を確認できます。";

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
        controller.RefreshContent();
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
        RefreshContent();
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

    void RefreshContent()
    {
        var rect = transform as RectTransform;
        if (rect != null)
        {
            rect.sizeDelta = new Vector2(560f, 420f);
        }

        var title = transform.Find("Text_Title")?.GetComponent<TMP_Text>();
        if (title != null) title.text = "操作ヒント";

        var body = transform.Find("Text_Body")?.GetComponent<TMP_Text>();
        if (body == null) return;
        body.text = HintBody;
        body.fontSize = DesignTokens.FontSizeBody;
        body.color = DesignTokens.TextSecondary;
        body.alignment = TextAlignmentOptions.TopLeft;
        body.enableWordWrapping = true;
    }

    static HintPanelController BuildPanel(Transform parent)
    {
        var root = CreateRect(PanelName, parent);
        root.anchorMin = new Vector2(0.5f, 0.5f);
        root.anchorMax = new Vector2(0.5f, 0.5f);
        root.pivot = new Vector2(0.5f, 0.5f);
        root.sizeDelta = new Vector2(560f, 420f);
        root.anchoredPosition = Vector2.zero;

        var image = root.gameObject.AddComponent<Image>();
        image.color = DesignTokens.Surface;
        var outline = root.gameObject.AddComponent<Outline>();
        outline.effectColor = DesignTokens.Divider;
        outline.effectDistance = new Vector2(1f, -1f);

        var title = CreateText("Text_Title", root, "操作ヒント", DesignTokens.FontSizeHeading, DesignTokens.TextPrimary);
        SetRect(title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(24f, -64f), new Vector2(-24f, -24f));

        var body = CreateText("Text_Body", root, HintBody, DesignTokens.FontSizeBody, DesignTokens.TextSecondary);
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
