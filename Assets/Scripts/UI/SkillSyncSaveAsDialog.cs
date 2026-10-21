using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// "名前を付けて保存": asks for a lesson name. Esc = cancel, Enter = save.
// Name collisions are shown while typing; the decision to replace is made in the central confirm dialog by the caller.
[DefaultExecutionOrder(1000)]
public sealed class SkillSyncSaveAsDialog : MonoBehaviour
{
    const string ObjectName = "SkillSyncSaveAsDialog";
    const float PanelWidth = 640f;
    static SkillSyncSaveAsDialog instance;

    TMP_Text titleText, messageText, hintText;
    TMP_InputField input;
    SkillSyncModalKit.KitButton cancelButton, saveButton;
    Func<string, bool> nameInUse;
    Action<string> onSave;
    Action onCancel;
    int openedFrame;
    bool needFocus;

    public static bool IsOpen => instance != null && instance.gameObject.activeSelf;

    public static Transform GetOpenModalRoot(Transform within = null)
    {
        if (!IsOpen) return null;
        var root = instance.transform;
        return within == null || root == within || root.IsChildOf(within) ? root : null;
    }

    // nameInUse: true when saving under that name would replace another lesson. onSave gets the trimmed name.
    public static void Show(string defaultName, Func<string, bool> nameInUse, Action<string> onSave, Action onCancel = null)
    {
        var dialog = Resolve();
        if (dialog == null) { Debug.LogWarning("[SkillSyncSaveAsDialog] No canvas found."); onCancel?.Invoke(); return; }
        dialog.Open(defaultName, nameInUse, onSave, onCancel);
    }

    static SkillSyncSaveAsDialog Resolve()
    {
        var canvas = SkillSyncModalKit.ResolveCanvas(out var font);
        if (canvas == null) return null;
        if (instance != null && instance.transform.parent != canvas.transform) { Destroy(instance.gameObject); instance = null; }
        if (instance != null) return instance;
        var existing = canvas.transform.Find(ObjectName);
        if (existing != null && existing.GetComponent<SkillSyncSaveAsDialog>() != null) return instance = existing.GetComponent<SkillSyncSaveAsDialog>();

        var go = SkillSyncModalKit.NewModalRoot(ObjectName, canvas, typeof(SkillSyncSaveAsDialog));
        var dialog = go.GetComponent<SkillSyncSaveAsDialog>();
        dialog.Build(font);
        go.SetActive(false);
        return instance = dialog;
    }

    void Build(TMP_FontAsset font)
    {
        var panel = SkillSyncModalKit.NewPanel(transform, PanelWidth, new RectOffset(32, 32, 28, 24), 14f);
        titleText = SkillSyncModalKit.NewText("Title", panel, font, 26, DesignTokens.TextPrimary, FontStyles.Bold);
        messageText = SkillSyncModalKit.NewText("Message", panel, font, 20, DesignTokens.TextPrimary);
        messageText.lineSpacing = 8f;
        input = SkillSyncModalKit.NewInput("Input_Name", panel, font, 22, "教材の名前");
        input.onValueChanged.AddListener(_ => Refresh());
        input.onSubmit.AddListener(_ => Submit());
        hintText = SkillSyncModalKit.NewText("Hint", panel, font, 18, DesignTokens.Warning, FontStyles.Normal, TextAlignmentOptions.TopLeft, true);
        SkillSyncModalKit.WithHeight(hintText, 28f);

        var row = new GameObject("Buttons", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement)).GetComponent<RectTransform>();
        row.SetParent(panel, false);
        var rowLayout = row.GetComponent<HorizontalLayoutGroup>();
        rowLayout.spacing = 12f; rowLayout.childAlignment = TextAnchor.MiddleRight;
        rowLayout.childControlWidth = rowLayout.childControlHeight = true;
        rowLayout.childForceExpandWidth = rowLayout.childForceExpandHeight = false;
        rowLayout.padding = new RectOffset(0, 0, 10, 0);
        row.GetComponent<LayoutElement>().preferredHeight = 58f;
        cancelButton = SkillSyncModalKit.NewButton("Button_Cancel", row, font);
        saveButton = SkillSyncModalKit.NewButton("Button_Save", row, font);
        SkillSyncModalKit.Style(cancelButton, "キャンセル", SkillSyncModalKit.ButtonKind.Secondary);
        SkillSyncModalKit.Style(saveButton, "保存", SkillSyncModalKit.ButtonKind.Primary);
        cancelButton.button.onClick.AddListener(Cancel);
        saveButton.button.onClick.AddListener(Submit);
    }

    void Open(string defaultName, Func<string, bool> inUse, Action<string> save, Action cancel)
    {
        nameInUse = inUse; onSave = save; onCancel = cancel;
        gameObject.SetActive(true);
        titleText.text = "名前を付けて保存";
        messageText.text = "教材の名前を入力してください。保存した教材は、あとから「教材を開く」で開けます。";
        input.SetTextWithoutNotify(defaultName ?? string.Empty);
        Refresh();
        SkillSyncModalKit.ClearSelection();
        openedFrame = Time.frameCount;
        needFocus = true;
        transform.SetAsLastSibling();
        LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)titleText.transform.parent);
    }

    void Refresh()
    {
        string name = input.text.Trim();
        bool blank = name.Length == 0;
        bool replaces = !blank && nameInUse != null && nameInUse(name);
        hintText.text = blank ? "教材名を入力してください。" : replaces ? "同じ名前の教材があります。保存すると置き換えます。" : string.Empty;
        hintText.color = blank ? DesignTokens.Error : DesignTokens.Warning;
        SkillSyncModalKit.SetInteractable(saveButton, !blank);
    }

    void Update()
    {
        if (!Application.isFocused || Time.frameCount <= openedFrame) return;
        if (needFocus)
        {
            needFocus = false;
            input.Select(); input.ActivateInputField();
        }
        if (EditInput.CancelPressedThisFrame()) Cancel();
        // While typing, Enter reaches us through the field's own submit event.
        else if (EditInput.ConfirmPressedThisFrame() && !input.isFocused) Submit();
    }

    void Submit()
    {
        string name = input.text.Trim();
        if (name.Length == 0) { Refresh(); input.ActivateInputField(); return; }
        var save = onSave;
        Hide();
        save?.Invoke(name);
    }

    void Cancel()
    {
        var cancel = onCancel;
        Hide();
        cancel?.Invoke();
    }

    void Hide()
    {
        onSave = null; onCancel = null; nameInUse = null;
        SkillSyncModalKit.ClearSelection();
        gameObject.SetActive(false);
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
    }
}
