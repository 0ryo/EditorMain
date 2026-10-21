using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class HintPanelController : MonoBehaviour
{
    const string ButtonName = "Button_Hints";
    const string PanelName = "Panel_Hints";
    // Every key and gesture below was checked against the input code (EditInput, EditorCameraController, SelectionService, MoveTool, CommandService).
    static string HintBody
    {
        get
        {
            string m = SkillSyncDesignView.ShortcutModifierName;
            string alt = SkillSyncDesignView.ShortcutModifierName == "Cmd" ? "Option" : "Alt";
            return
                Heading("教材を作る流れ") +
                "① 配置：「配置を編集」で、左の部品を3D画面に置きます\n" +
                "② 手順：「手順を編集」で手順を足し、「完了する条件」（何をしたら次へ進むか）を決めます\n" +
                "③ 動作確認：「動作を確認」で、学習者になって通して試します\n" +
                "④ 保存と書き出し：" + m + "+S で保存し、「教材を書き出す」で作ったZIPをXRアプリへ渡します\n" +
                Heading("部品を置く") +
                "・左の部品をクリックして、3D画面の置きたい場所をクリック\n" +
                "・部品を3D画面へドラッグして離しても置けます\n" +
                "・Shift+クリックで、続けて何個も置けます　Esc：置くのをやめる\n" +
                "・「＋ 3Dモデルを追加」：自分のモデル（GLB/glTF形式）を部品に加えます\n" +
                Heading("部品を動かす") +
                "・部品の本体をドラッグ：床に沿って動きます\n" +
                "・「移動」：矢印をドラッグ（緑の矢印は上下）\n" +
                "・「回転」：輪をドラッグ（向きの欄に角度を入れても回せます）\n" +
                "・「大きさ」：角をドラッグ（倍率の欄に％を入れても変えられます）\n" +
                "・矢印キー（W/A/S/D も可）：吸着の間隔ぶん動きます（「移動」のとき）\n" +
                "・吸着：位置や角度を決めた間隔に揃えます。" + alt + "を押している間は一時的にオフ。間隔は「詳細設定」で変えられます\n" +
                Heading("部品を選ぶ") +
                "・クリック：1回目はグループ全体、もう一度クリックで中の部品を選びます\n" +
                "・何もない所をドラッグ：枠に触れた部品を選びます（Shiftを押しながらで追加）\n" +
                "・Shift+クリック／" + m + "+クリック：選択に追加・解除\n" +
                "・" + m + "+A：すべて選ぶ　Esc：選択を解除\n" +
                "・左の「配置した部品」：クリックで選択、Shift+クリックで範囲選択\n" +
                Heading("グループ・複製・削除") +
                "・" + m + "+G：グループにする　" + m + "+Shift+G：グループを解除\n" +
                "・" + m + "+D：複製　" + m + "+C／" + m + "+V：コピー／貼り付け　" + m + "+Shift+V：元の位置に貼り付け\n" +
                "・Delete：削除\n" +
                "・右クリック：メニュー（名前を変更・複製・グループ・隠す・ロック・削除）\n" +
                "・「配置した部品」の行をダブルクリック：名前を変更\n" +
                "・行の目：隠す／表示　鍵：ロック（ロック中の部品は選べず、編集できません）\n" +
                Heading("3D画面の見方") +
                "・右ドラッグ（中ボタンも可）：回転　Shift+右ドラッグ：平行移動　ホイール：ズーム\n" +
                "・トラックパッド：2本指で回転、Shift+2本指で平行移動、" + m + "+2本指でズーム（右上の「操作」ボタンでマウスと切り替え）\n" +
                "・" + alt + "+ドラッグ（部品のない所）：回転。Shiftも押すと平行移動\n" +
                "・1／3／7：正面／右／上から　O：平行投影と透視投影を切り替え\n" +
                "・F：選んだ部品を画面に収める　Home：全体を見る\n" +
                "・右上の向きの図で軸の丸をクリック：その向きから見ます\n" +
                Heading("手順と完了条件") +
                "・「＋ 手順を追加」で手順を足します。手順の行をドラッグするか「↑ 上へ」「↓ 下へ」で並べ替え\n" +
                "・手順ごとに「＋ 完了条件を追加」。対象は3D画面か一覧でクリックして仮選択し、「この対象に決定」（Enter）で決めます\n" +
                "・すべての完了条件がそろうと次の手順へ進みます（順不同）。順番が必要なときは手順を分けてください\n" +
                Heading("動作確認") +
                "・「動作を確認」で、部品を動かして完了条件が成立するか確かめます\n" +
                "・パソコンで判定できない条件は「成立にする」で成立として進められます\n" +
                "・「▶ 最初から通して確認」：最初の手順から続けて確認　Esc：一時停止、もう一度Escで編集に戻る\n" +
                "・確認中に動かした位置は、教材の配置には保存されません\n" +
                Heading("教材のファイル") +
                "・" + m + "+N：新しい教材　" + m + "+S：保存　" + m + "+O：教材を開く\n" +
                "・初めて保存するときは教材の名前を入力します。上の名前欄を書き換えてEnterで名前を変えられます\n" +
                "・「復旧データ」は、異常終了したときのための自動の控えです。保存の代わりにはなりません\n" +
                "・「↑ 教材を書き出す」：書き出し先を選ぶと、教材名のフォルダとZIPができます。XRアプリにはZIPを渡します\n" +
                "・書き出せない問題があるときは、直す場所の一覧が出ます\n" +
                Heading("元に戻す") +
                "・" + m + "+Z：元に戻す　" + m + "+Shift+Z（" + m + "+Y）：やり直す。ヘッダーの ↶ ↷ でも操作できます";
        }
    }

    static string Heading(string title)
    {
        return "\n\n<size=118%><b><color=#" + ColorUtility.ToHtmlStringRGB(DesignTokens.Accent) + ">" + title + "</color></b></size>\n";
    }

    [SerializeField] Button openButton;
    [SerializeField] Button closeButton;
    const string ScrimName = "Scrim_Hints";
    const float PanelWidth = 720f, PanelHeight = 820f; // title 64 + scrolling body + close 64
    const float BodyFontSize = 17f;
    const string ScrollName = "Scroll_Body";
    [SerializeField] RectTransform scrim;
    readonly Vector3[] worldCorners = new Vector3[4];

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
        controller.PositionInViewport();
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

    void Update()
    {
        if (Application.isFocused && EditInput.CancelPressedThisFrame()) Hide();
    }

    void LateUpdate()
    {
        PositionInViewport();
    }

    void EnsureOpenButton(Transform parent)
    {
        // SkillSync has its own Help entry. Do not recreate the legacy overlay.
        if (parent != null && parent.GetComponentInChildren<SkillSyncDesignView>(true) != null)
        {
            var legacy = parent.Find(ButtonName);
            if (legacy != null) legacy.gameObject.SetActive(false);
            if (openButton != null) openButton.gameObject.SetActive(false);
            return;
        }
        if (openButton == null)
        {
            openButton = parent?.Find(ButtonName)?.GetComponent<Button>();
        }

        if (openButton == null && parent != null)
        {
            openButton = CreateButton(ButtonName, parent, "ヘルプ");
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
        EnsureScrim();
        if (scrim != null) { scrim.gameObject.SetActive(true); scrim.SetAsLastSibling(); }
        gameObject.SetActive(true);
        var scrollRect = GetComponentInChildren<ScrollRect>(true);
        if (scrollRect != null) scrollRect.verticalNormalizedPosition = 1f;
        PositionInViewport();
        transform.SetAsLastSibling();
    }

    void EnsureScrim()
    {
        if (scrim == null && transform.parent != null)
        {
            var found = transform.parent.Find(ScrimName);
            if (found != null) scrim = found as RectTransform;
        }
        if (scrim != null || transform.parent == null) return;
        scrim = CreateRect(ScrimName, transform.parent);
        SetRect(scrim, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        var image = scrim.gameObject.AddComponent<Image>();
        image.color = new Color(0f, 0f, 0f, 0.35f);
        image.raycastTarget = true;
        scrim.gameObject.AddComponent<EditorUiInputBlocker>();
        scrim.gameObject.SetActive(false);
    }

    public void Hide()
    {
        if (scrim != null) scrim.gameObject.SetActive(false);
        gameObject.SetActive(false);
    }

    void OnDisable()
    {
        if (scrim != null) scrim.gameObject.SetActive(false);
    }

    void RefreshContent()
    {
        var rect = transform as RectTransform;
        if (rect != null)
        {
            rect.sizeDelta = new Vector2(PanelWidth, PanelHeight);
            // The SkillSync canvas uses a 2560x1440 reference, so legacy dialogs need its 1.44x correction.
            bool skillSync = transform.parent != null &&
                transform.parent.GetComponentInChildren<SkillSyncDesignView>(true) != null;
            rect.localScale = Vector3.one * (skillSync ? SkillSyncDesignLayout.Scale : 1f);
        }

        var title = transform.Find("Text_Title")?.GetComponent<TMP_Text>();
        if (title != null) title.text = "ヘルプ";

        var body = transform.Find(ScrollName + "/Viewport/Text_Body")?.GetComponent<TMP_Text>();
        if (body == null) return;
        body.text = HintBody.TrimStart('\n');
        body.fontSize = Mathf.Max(DesignTokens.FontSizeBody, BodyFontSize);
        body.color = DesignTokens.TextPrimary;
        body.richText = true;
        body.lineSpacing = 8f;
        body.alignment = TextAlignmentOptions.TopLeft;
        body.textWrappingMode = TMPro.TextWrappingModes.Normal;
    }

    void PositionInViewport()
    {
        var root = transform.parent as RectTransform;
        var rect = transform as RectTransform;
        if (root == null || rect == null) return;

        float left = root.rect.xMin;
        float right = root.rect.xMax;
        float bottom = root.rect.yMin;
        float top = root.rect.yMax;

        var catalog = root.Find("Panel_Catalog") as RectTransform;
        if (TryGetVisibleBounds(root, catalog, out _, out var catalogRight, out _, out _))
        {
            left = Mathf.Max(left, catalogRight);
        }

        var detail = root.Find("Panel_Detail") as RectTransform;
        if (TryGetVisibleBounds(root, detail, out var detailLeft, out _, out _, out _))
        {
            right = Mathf.Min(right, detailLeft);
        }

        var scenario = root.Find("Panel_ScenarioGraph") as RectTransform;
        if (TryGetVisibleBounds(root, scenario, out _, out _, out _, out var scenarioTop))
        {
            bottom = Mathf.Max(bottom, scenarioTop);
        }

        Vector2 viewportCenter = new Vector2((left + right) * 0.5f, (bottom + top) * 0.5f);
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = viewportCenter - root.rect.center;
    }

    bool TryGetVisibleBounds(
        RectTransform root,
        RectTransform target,
        out float left,
        out float right,
        out float bottom,
        out float top)
    {
        left = right = bottom = top = 0f;
        if (root == null || target == null || !target.gameObject.activeInHierarchy) return false;

        var canvasGroup = target.GetComponent<CanvasGroup>();
        if (canvasGroup != null && canvasGroup.alpha <= 0.01f) return false;

        target.GetWorldCorners(worldCorners);
        Vector3 bottomLeft = root.InverseTransformPoint(worldCorners[0]);
        Vector3 topRight = root.InverseTransformPoint(worldCorners[2]);
        left = bottomLeft.x;
        right = topRight.x;
        bottom = bottomLeft.y;
        top = topRight.y;
        return right > left && top > bottom;
    }

    static HintPanelController BuildPanel(Transform parent)
    {
        var root = CreateRect(PanelName, parent);
        root.anchorMin = new Vector2(0.5f, 0.5f);
        root.anchorMax = new Vector2(0.5f, 0.5f);
        root.pivot = new Vector2(0.5f, 0.5f);
        root.sizeDelta = new Vector2(PanelWidth, PanelHeight);
        root.anchoredPosition = Vector2.zero;

        var image = root.gameObject.AddComponent<Image>();
        image.color = DesignTokens.Surface;
        var outline = root.gameObject.AddComponent<Outline>();
        outline.effectColor = DesignTokens.Divider;
        outline.effectDistance = new Vector2(1f, -1f);

        var title = CreateText("Text_Title", root, "ヘルプ", DesignTokens.FontSizeHeading, DesignTokens.TextPrimary);
        SetRect(title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(28f, -64f), new Vector2(-28f, -24f));

        // The guide is longer than the panel, so the body scrolls (wheel or the bar on the right).
        var scroll = CreateRect(ScrollName, root);
        SetRect(scroll, Vector2.zero, Vector2.one, new Vector2(28f, 88f), new Vector2(-28f, -72f));
        var viewport = CreateRect("Viewport", scroll);
        SetRect(viewport, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(-14f, 0f));
        viewport.gameObject.AddComponent<RectMask2D>();
        var body = CreateText("Text_Body", viewport, HintBody.TrimStart('\n'), Mathf.Max(DesignTokens.FontSizeBody, BodyFontSize), DesignTokens.TextPrimary);
        body.alignment = TextAlignmentOptions.TopLeft;
        body.textWrappingMode = TMPro.TextWrappingModes.Normal;
        body.richText = true;
        body.rectTransform.anchorMin = new Vector2(0f, 1f);
        body.rectTransform.anchorMax = new Vector2(1f, 1f);
        body.rectTransform.pivot = new Vector2(0.5f, 1f);
        body.rectTransform.offsetMin = new Vector2(0f, 0f);
        body.rectTransform.offsetMax = new Vector2(0f, 0f);
        var fitter = body.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        var bar = CreateRect("Scrollbar", scroll);
        bar.anchorMin = new Vector2(1f, 0f);
        bar.anchorMax = new Vector2(1f, 1f);
        bar.pivot = new Vector2(1f, 0.5f);
        bar.sizeDelta = new Vector2(8f, 0f);
        bar.anchoredPosition = Vector2.zero;
        var track = bar.gameObject.AddComponent<Image>();
        track.color = DesignTokens.Divider;
        var slide = CreateRect("Sliding Area", bar);
        SetRect(slide, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        var handle = CreateRect("Handle", slide);
        SetRect(handle, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        var handleImage = handle.gameObject.AddComponent<Image>();
        handleImage.color = DesignTokens.TextSecondary;
        var scrollbar = bar.gameObject.AddComponent<Scrollbar>();
        scrollbar.direction = Scrollbar.Direction.BottomToTop;
        scrollbar.handleRect = handle;
        scrollbar.targetGraphic = handleImage;
        var scrollRect = scroll.gameObject.AddComponent<ScrollRect>();
        scrollRect.viewport = viewport;
        scrollRect.content = body.rectTransform;
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.scrollSensitivity = 40f;
        scrollRect.verticalScrollbar = scrollbar;
        scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;

        var close = CreateButton("Button_Close", root, "\u9589\u3058\u308B");
        SetRect(close.GetComponent<RectTransform>(), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-108f, 24f), new Vector2(-28f, 64f));

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
        UiAccessibilityMetrics.EnsureButtonTarget(button);
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
