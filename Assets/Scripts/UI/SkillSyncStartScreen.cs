using System;
using System.Linq;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Start screen shown when the app launches: new lesson, recent lessons, templates, the bundled sample, and the
// "previous session ended without saving" notice. Runtime-built central modal (no Prefab or Scene change).
// Esc closes it and the editor continues with the empty lesson that is already open.
[DefaultExecutionOrder(999)] // Before SkillSyncConfirmDialog (1000), so Esc that closes a confirm never also closes this screen.
public sealed class SkillSyncStartScreen : MonoBehaviour
{
    const string ObjectName = "SkillSyncStartScreen";
    const float PanelWidth = 1000f;
    const int RecentCount = 5;
    enum Page { Home, Templates }

    static SkillSyncStartScreen instance;
    static bool shownAtLaunch;

    TMP_FontAsset font;
    RectTransform panel;
    EditorProjectService project;
    Transform uiRoot;
    Page page;
    bool busy;
    int openedFrame;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { instance = null; shownAtLaunch = false; }

    public static bool IsOpen => instance != null && instance.gameObject.activeSelf;

    public static Transform GetOpenModalRoot(Transform within = null)
    {
        if (!IsOpen) return null;
        var root = instance.transform;
        return within == null || root == within || root.IsChildOf(within) ? root : null;
    }

    // Once per app launch. Skipped in batch mode (automated runs).
    public static void ShowAtLaunch(SkillSyncEditorController owner, EditorProjectService service)
    {
        if (shownAtLaunch || Application.isBatchMode || owner == null || service == null) return;
        shownAtLaunch = true;
        Show(owner.transform.root, service);
    }

    public static void Show(Transform uiRoot, EditorProjectService service)
    {
        if (uiRoot == null || service == null) return;
        var screen = Resolve();
        if (screen != null) screen.Open(uiRoot, service);
    }

    static SkillSyncStartScreen Resolve()
    {
        var canvas = SkillSyncModalKit.ResolveCanvas(out var font);
        if (canvas == null) return null;
        if (instance != null && instance.transform.parent != canvas.transform) { Destroy(instance.gameObject); instance = null; }
        if (instance != null) return instance;
        var go = SkillSyncModalKit.NewModalRoot(ObjectName, canvas, typeof(SkillSyncStartScreen));
        var screen = go.GetComponent<SkillSyncStartScreen>();
        screen.font = font;
        screen.panel = SkillSyncModalKit.NewPanel(go.transform, PanelWidth, new RectOffset(40, 40, 32, 28), 0f);
        go.SetActive(false);
        return instance = screen;
    }

    void Open(Transform root, EditorProjectService service)
    {
        uiRoot = root; project = service;
        page = Page.Home; busy = false;
        gameObject.SetActive(true);
        SkillSyncModalKit.ClearSelection();
        openedFrame = Time.frameCount;
        Rebuild();
        transform.SetAsLastSibling();
    }

    void Close()
    {
        gameObject.SetActive(false);
        SkillSyncModalKit.ClearSelection();
    }

    void Update()
    {
        if (!Application.isFocused || Time.frameCount <= openedFrame) return;
        if (SkillSyncConfirmDialog.IsOpen || SkillSyncSaveAsDialog.IsOpen) return;
        if (EditInput.CancelPressedThisFrame()) { if (page == Page.Templates) { page = Page.Home; Rebuild(); } else Close(); }
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    // ---- content -------------------------------------------------------------------------------------------

    void Rebuild()
    {
        for (int i = panel.childCount - 1; i >= 0; i--) { panel.GetChild(i).gameObject.SetActive(false); Destroy(panel.GetChild(i).gameObject); }
        if (page == Page.Templates) BuildTemplates(); else BuildHome();
        LayoutRebuilder.ForceRebuildLayoutImmediate(panel);
    }

    void BuildHome()
    {
        AddText("Title", "XR教材エディタ", 36, DesignTokens.TextPrimary, FontStyles.Bold);
        Spacer(4f);
        AddText("Subtitle", "何から始めますか？", 22, DesignTokens.TextSecondary);
        Spacer(20f);

        var notice = project.StartupRecovery;
        bool hasNotice = notice != null && EditorProjectStore.TryGetFileInfo(notice.path, out _);
        if (hasNotice) { BuildRecoveryNotice(notice); Spacer(20f); }

        // The lesson used last comes first, opened where it was left (its view is remembered per lesson).
        var last = EditorProjectRecents.Get(1);
        var continued = last.Count > 0 ? last[0] : null;
        if (continued != null)
        {
            var where = SkillSyncViewMemory.Load(continued.Path)?.label;
            var resume = Row("Continue", 16f, 92f);
            Card(resume, "▶  前回の続き：「" + continued.DisplayName + "」",
                (string.IsNullOrEmpty(where) ? "" : where + "・") + continued.LastWriteTimeUtc.ToLocalTime().ToString("M月d日 HH:mm") + "に保存", true, () => OpenRecent(continued),
                centered: true);
            Spacer(16f);
        }
        var cards = Row("Cards", 16f, 124f);
        Card(cards, "＋  新しい教材を作る", "空の教材から始めます。", continued == null, Close);
        // One card for "start from a copy": the bundled sample and the user's own templates are listed together.
        Card(cards, "見本・テンプレートから作る", "完成例や保存したテンプレートのコピーから始めます。", false, () => { page = Page.Templates; Rebuild(); });
        Spacer(24f);

        AddText("RecentTitle", "最近使った教材", 22, DesignTokens.TextPrimary, FontStyles.Bold);
        Spacer(8f);
        // The continue card already offers the first one.
        var recent = EditorProjectRecents.Get((hasNotice ? RecentCount - 1 : RecentCount) + (continued != null ? 1 : 0)).Skip(continued != null ? 1 : 0).ToList();
        if (recent.Count == 0)
        {
            var empty = AddText("RecentEmpty", continued != null ? "ほかに最近使った教材はありません。" : "まだ保存した教材はありません。作った教材はここに表示されます。", 20, DesignTokens.TextSecondary);
            SkillSyncModalKit.WithHeight(empty, 48f);
        }
        foreach (var info in recent) { ListRow(info.DisplayName, info.LastWriteTimeUtc.ToLocalTime().ToString("yyyy/MM/dd HH:mm"), () => OpenRecent(info)); Spacer(6f); }
        Spacer(14f);

        var footer = Row("Footer", 12f, 52f);
        var all = SkillSyncModalKit.NewButton("Button_All", footer, font);
        SkillSyncModalKit.Style(all, "すべての教材を見る", SkillSyncModalKit.ButtonKind.Quiet);
        all.button.onClick.AddListener(() => { Close(); EditorProjectPanel.Ensure(uiRoot)?.OpenForDesignUi(); });
        FlexibleSpace(footer);
        var close = SkillSyncModalKit.NewButton("Button_Close", footer, font);
        SkillSyncModalKit.Style(close, "閉じる", SkillSyncModalKit.ButtonKind.Secondary);
        close.button.onClick.AddListener(Close);
    }

    void BuildRecoveryNotice(StartupRecoveryNotice notice)
    {
        var box = new GameObject("RecoveryNotice", typeof(RectTransform), typeof(SkillSyncRoundedGraphic), typeof(VerticalLayoutGroup)).GetComponent<RectTransform>();
        box.SetParent(panel, false);
        var surface = box.GetComponent<SkillSyncRoundedGraphic>();
        surface.color = DesignTokens.BadgeBg(DesignTokens.Warning); surface.borderColor = DesignTokens.Warning; surface.radius = 10f; surface.borderWidth = 1.5f;
        surface.raycastTarget = false;
        var layout = box.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(20, 20, 16, 16); layout.spacing = 10f;
        layout.childControlWidth = layout.childControlHeight = true; layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;
        string name = string.IsNullOrWhiteSpace(notice.originalName) ? EditorProjectStore.UntitledProjectName : notice.originalName;
        var message = SkillSyncModalKit.NewText("Message", box, font, 21, DesignTokens.TextPrimary, FontStyles.Bold);
        message.text = "前回、保存されずに終了した教材「" + name + "」があります。";
        var detail = SkillSyncModalKit.NewText("Detail", box, font, 18, DesignTokens.TextSecondary);
        detail.text = notice.savedAtUtc.ToLocalTime().ToString("M月d日 HH:mm") + "までの内容を復旧データとして残してあります。";

        var row = new GameObject("Buttons", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement)).GetComponent<RectTransform>();
        row.SetParent(box, false);
        var rowLayout = row.GetComponent<HorizontalLayoutGroup>();
        rowLayout.spacing = 12f; rowLayout.childAlignment = TextAnchor.MiddleLeft;
        rowLayout.childControlWidth = rowLayout.childControlHeight = true; rowLayout.childForceExpandWidth = rowLayout.childForceExpandHeight = false;
        row.GetComponent<LayoutElement>().preferredHeight = 48f;
        var open = SkillSyncModalKit.NewButton("Button_OpenRecovery", row, font);
        SkillSyncModalKit.Style(open, "復旧して開く", SkillSyncModalKit.ButtonKind.Primary);
        open.button.onClick.AddListener(() => OpenRecovery(notice));
        var discard = SkillSyncModalKit.NewButton("Button_DiscardRecovery", row, font);
        SkillSyncModalKit.Style(discard, "破棄", SkillSyncModalKit.ButtonKind.Secondary);
        discard.button.onClick.AddListener(() => DiscardRecovery(notice, name));
        var later = SkillSyncModalKit.NewButton("Button_LaterRecovery", row, font);
        SkillSyncModalKit.Style(later, "あとで", SkillSyncModalKit.ButtonKind.Quiet);
        later.button.onClick.AddListener(() =>
        {
            project.DismissStartupRecovery();
            Notify("復旧データは「" + System.IO.Path.GetFileNameWithoutExtension(System.IO.Path.GetFileNameWithoutExtension(notice.path)) + "」として、「教材を開く」の一覧に残っています。");
            Rebuild();
        });
    }

    void BuildTemplates()
    {
        var header = Row("Header", 12f, 52f);
        var back = SkillSyncModalKit.NewButton("Button_Back", header, font);
        SkillSyncModalKit.Style(back, "← 戻る", SkillSyncModalKit.ButtonKind.Quiet);
        back.button.onClick.AddListener(() => { page = Page.Home; Rebuild(); });
        Spacer(6f);
        AddText("Title", "見本・テンプレートから作る", 34, DesignTokens.TextPrimary, FontStyles.Bold);
        Spacer(4f);
        AddText("Subtitle", "選んだもののコピーを、新しい教材として開きます。元の見本やテンプレートは変わりません。", 20, DesignTokens.TextSecondary);
        Spacer(18f);
        var sample = SkillSyncSamples.TireChange();
        if (sample != null)
        {
            TMP_Text sampleDetail = null;
            sampleDetail = ListRow(sample.title, "見本", () => OpenSample(sample, sampleDetail));
            Spacer(6f);
        }
        var templates = EditorProjectStore.ListTemplates();
        if (templates.Count == 0)
        {
            AddText("Empty", "保存したテンプレートはまだありません。「教材を開く」の一覧で、保存済みの教材を選んで［テンプレートにする］を押すと、ここに追加されます。", 20, DesignTokens.TextPrimary);
            Spacer(14f);
            var open = SkillSyncModalKit.NewButton("Button_OpenLibrary", panel, font);
            SkillSyncModalKit.Style(open, "教材を開く", SkillSyncModalKit.ButtonKind.Secondary);
            open.button.onClick.AddListener(() => { Close(); EditorProjectPanel.Ensure(uiRoot)?.OpenForDesignUi(); });
        }
        int shown = 0;
        foreach (var info in templates)
        {
            if (shown++ >= 8) break;
            string baseName = EditorProjectPanel.TemplateBaseName(info.DisplayName);
            ListRow(info.DisplayName, info.LastWriteTimeUtc.ToLocalTime().ToString("yyyy/MM/dd HH:mm"),
                () => CreateFrom(info.Path, baseName));
            Spacer(6f);
        }
    }

    // ---- actions -------------------------------------------------------------------------------------------

    void OpenRecent(EditorProjectFileInfo info)
    {
        EditorProjectPanel.Ensure(uiRoot)?.RequestOpen(info, opened => { if (opened) Close(); });
    }

    void CreateFrom(string path, string baseName)
    {
        EditorProjectPanel.Ensure(uiRoot)?.RequestCreateFrom(path, baseName, created => { if (created) Close(); });
    }

    void OpenRecovery(StartupRecoveryNotice notice)
    {
        EditorProjectPanel.Ensure(uiRoot)?.RequestOpenStartupRecovery(opened => { if (opened) Close(); else Rebuild(); });
    }

    void DiscardRecovery(StartupRecoveryNotice notice, string name)
    {
        EditorProjectPanel.Ensure(uiRoot)?.RequestDiscardStartupRecovery(Rebuild);
    }

    // The pressed row answers at once: it says it is preparing (with moving dots) and the page stops taking clicks,
    // so a second press is never silently swallowed.
    async void OpenSample(SkillSyncSamples.Sample sample, TMP_Text detail = null)
    {
        if (busy) return;
        busy = true;
        CanvasGroup group = null;
        if (panel != null) { group = panel.GetComponent<CanvasGroup>(); if (group == null) group = panel.gameObject.AddComponent<CanvasGroup>(); }
        if (group != null) group.interactable = false;
        string before = detail != null ? detail.text : null;
        Color beforeColor = detail != null ? detail.color : Color.clear;
        var dots = detail != null ? StartCoroutine(Preparing(detail)) : null;
        Notify("見本を準備しています。初めて開くときは少し時間がかかります。");
        try
        {
            var catalog = FindFirstObjectByType<CatalogUI>();
            bool ready = await SkillSyncSamples.PrepareModelsAsync(sample, catalog);
            if (this == null) return;
            if (!ready)
            {
                Notify("見本の3Dモデルを準備できませんでした。もう一度試しても開けない場合は、アプリを再起動してください。", true);
                return;
            }
            var panelController = EditorProjectPanel.Ensure(uiRoot);
            if (panelController == null) return;
            panelController.RequestCreateFrom(sample.projectPath, sample.title, created => { if (created) Close(); });
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            Notify("見本を開けませんでした: " + ex.Message, true);
        }
        finally
        {
            busy = false;
            if (this != null)
            {
                if (dots != null) StopCoroutine(dots);
                if (detail != null) { detail.text = before; detail.color = beforeColor; }
                if (group != null) group.interactable = true;
            }
        }
    }

    System.Collections.IEnumerator Preparing(TMP_Text detail)
    {
        detail.color = DesignTokens.Accent;
        for (int i = 0; ; i = (i + 1) % 4)
        {
            detail.text = "準備しています" + new string('.', i);
            yield return new WaitForSecondsRealtime(.35f);
        }
    }

    void Notify(string message, bool error = false)
    {
        if (string.IsNullOrEmpty(message)) return;
        TopCenterNotification.Ensure(uiRoot, null)?.Show(message, error, error ? 8f : 4f);
    }

    // ---- small builders ------------------------------------------------------------------------------------

    TMP_Text AddText(string name, string text, int size, Color color, FontStyles style = FontStyles.Normal)
    {
        var label = SkillSyncModalKit.NewText(name, panel, font, size, color, style);
        label.text = text;
        return label;
    }

    void Spacer(float height)
    {
        var spacer = new GameObject("Spacer", typeof(RectTransform), typeof(LayoutElement)).GetComponent<LayoutElement>();
        spacer.transform.SetParent(panel, false);
        spacer.preferredHeight = spacer.minHeight = height;
    }

    RectTransform Row(string name, float spacing, float height)
    {
        var row = new GameObject(name, typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement)).GetComponent<RectTransform>();
        row.SetParent(panel, false);
        var layout = row.GetComponent<HorizontalLayoutGroup>();
        layout.spacing = spacing; layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = layout.childControlHeight = true; layout.childForceExpandWidth = false; layout.childForceExpandHeight = true;
        var element = row.GetComponent<LayoutElement>();
        element.preferredHeight = element.minHeight = height;
        return row;
    }

    static void FlexibleSpace(Transform parent)
    {
        var space = new GameObject("Flexible", typeof(RectTransform), typeof(LayoutElement)).GetComponent<LayoutElement>();
        space.transform.SetParent(parent, false);
        space.flexibleWidth = 1f;
    }

    // centered: a one-row card (the continue card) keeps its two lines in the middle instead of leaving space below.
    void Card(Transform parent, string title, string description, bool primary, Action onClick, bool centered = false)
    {
        var button = SkillSyncModalKit.NewButton("Card", parent, font);
        button.label.gameObject.SetActive(false); Destroy(button.label.gameObject);
        button.layout.flexibleWidth = 1f; button.layout.minWidth = 0f; button.layout.preferredWidth = 0f; // equal-width cards
        button.surface.radius = 12f; button.surface.borderWidth = 1.5f;
        Color fill = primary ? DesignTokens.Accent : DesignTokens.Surface;
        button.surface.color = fill;
        button.surface.borderColor = primary ? DesignTokens.Accent : DesignTokens.Divider;
        var layout = button.button.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(20, 20, 18, 16); layout.spacing = 8f;
        if (centered) { layout.padding = new RectOffset(24, 24, 0, 0); layout.spacing = 6f; layout.childAlignment = TextAnchor.MiddleLeft; }
        layout.childControlWidth = layout.childControlHeight = true; layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;
        var titleText = SkillSyncModalKit.NewText("CardTitle", button.button.transform, font, 24, primary ? DesignTokens.ButtonTextLight : DesignTokens.TextPrimary, FontStyles.Bold);
        titleText.text = title;
        var body = SkillSyncModalKit.NewText("CardBody", button.button.transform, font, 18, primary ? DesignTokens.ButtonTextLight : DesignTokens.TextSecondary);
        body.text = description;
        button.button.onClick.AddListener(() => onClick());
    }

    TMP_Text ListRow(string name, string detail, Action onClick)
    {
        var button = SkillSyncModalKit.NewButton("Row", panel, font);
        button.label.gameObject.SetActive(false); Destroy(button.label.gameObject);
        button.layout.preferredHeight = button.layout.minHeight = 56f;
        button.surface.color = DesignTokens.Surface; button.surface.borderColor = DesignTokens.Divider;
        var layout = button.button.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(20, 20, 0, 0); layout.spacing = 12f; layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = layout.childControlHeight = true; layout.childForceExpandWidth = false; layout.childForceExpandHeight = true;
        var title = SkillSyncModalKit.NewText("RowName", button.button.transform, font, 22, DesignTokens.TextPrimary, FontStyles.Normal, TextAlignmentOptions.MidlineLeft, false);
        title.text = name;
        title.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
        var date = SkillSyncModalKit.NewText("RowDate", button.button.transform, font, 18, DesignTokens.TextSecondary, FontStyles.Normal, TextAlignmentOptions.MidlineRight, false);
        date.text = detail;
        var dateLayout = date.gameObject.AddComponent<LayoutElement>();
        dateLayout.preferredWidth = dateLayout.minWidth = 200f;
        button.button.onClick.AddListener(() => onClick());
        return date;
    }
}
