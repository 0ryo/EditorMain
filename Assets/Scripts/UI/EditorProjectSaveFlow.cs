using System;

// The one way to save the current lesson from the UI (Cmd+S, Save button, export, quit, open, new):
//  - a lesson that already has its own file is saved there with no questions;
//  - a lesson that was never saved asks for a name first ("名前を付けて保存");
//  - saving under another lesson's name needs an explicit choice in the central confirm dialog.
public static class EditorProjectSaveFlow
{
    // onFinished(true) after a successful save, onFinished(false) when the user cancelled or the save failed.
    public static void Run(EditorProjectService service, Action<bool> onFinished = null)
    {
        if (service == null) { onFinished?.Invoke(false); return; }
        if (service.HasSavedFile)
        {
            onFinished?.Invoke(service.Save(service.CurrentProjectName, out _));
            return;
        }
        AskName(service, service.CurrentProjectName, onFinished);
    }

    static void AskName(EditorProjectService service, string suggestedName, Action<bool> onFinished)
    {
        SkillSyncSaveAsDialog.Show(suggestedName, service.NameInUse,
            name =>
            {
                if (!service.NameInUse(name)) { onFinished?.Invoke(service.Save(name, out _)); return; }
                // The central dialog makes replacing an explicit decision; "名前を変える" returns to the name field.
                SkillSyncConfirmDialog.Show("「" + name + "」を置き換えますか？",
                    "同じ名前の教材がすでに保存されています。置き換えると、保存済みの内容は今の内容に変わります。",
                    "置き換える", () => onFinished?.Invoke(service.Save(name, out _)),
                    cancelLabel: "名前を変える", destructive: true, onCancel: () => AskName(service, name, onFinished));
            },
            () => onFinished?.Invoke(false));
    }
}
