using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

public static class UnityExpansionAudit
{
    public static async Task<string> Verify()
    {
        Application.runInBackground=true;
        var view=UnityEngine.Object.FindFirstObjectByType<SkillSyncDesignView>();
        var expansion=view.GetComponent<SkillSyncWorkspaceExpansion>();
        var selection=UnityEngine.Object.FindFirstObjectByType<SelectionService>();
        var selected=selection.Current;
        if(selected==null) throw new Exception("Run interaction audit first");
        await Task.Delay(350);
        float narrow=view.viewport.rect.width;
        var second=UnityEngine.Object.FindObjectsByType<PlacedObject>(FindObjectsSortMode.None).First(p=>p!=selected && SelectionService.CanEdit(p));
        selection.Select(second,true);await Task.Delay(350);
        if(selection.Selected.Count!=2 || expansion.Amount!=1 || expansion.inspector.alpha!=0) throw new Exception("Multiple selection did not close inspector");
        selection.Select(selected);await Task.Delay(350);
        if(expansion.Amount!=0 || expansion.inspector.alpha!=1) throw new Exception("Single selection did not reopen inspector");
        selection.Select(null);
        await Task.Delay(80);
        if(expansion.Amount<=0 || expansion.Amount>=1) throw new Exception("Missing intermediate animation frame");
        await Task.Delay(350);
        if(Mathf.Abs(view.viewport.rect.width-narrow-440)>.1f) throw new Exception("Viewport did not expand by inspector width");
        if(expansion.inspector.alpha!=0 || expansion.inspector.blocksRaycasts) throw new Exception("Hidden inspector blocks input");
        if(Mathf.Abs(view.objectsContent.rect.width-view.viewport.rect.width)>.1f) throw new Exception("Object list did not follow");
        foreach(RectTransform row in view.objectsContent)
            if(row.gameObject.activeSelf && Mathf.Abs(row.rect.width-view.objectsContent.rect.width)>.1f) throw new Exception("Row width mismatch");
        var corners=new Vector3[4];view.viewport.GetWorldCorners(corners);
        if(SkillSyncEditorController.Active.BlocksWorkspace((Vector2)corners[2]+new Vector2(-40,-40))) throw new Exception("Expanded viewport input blocked");
        var label=view.visuals.First(v=>v.role=="viewportMode" && v.target.activeInHierarchy).label;
        label.ForceMeshUpdate();
        if(Mathf.Abs(label.textBounds.center.y-label.rectTransform.rect.center.y)>.35f) throw new Exception("Snap badge ink not centered");
        File.WriteAllText("Design/skillsync_codex_handoff/verification/expansion-results.txt","PASS: intermediate animation; +440 logical px viewport/list/rows; hidden inspector does not block; expanded viewport accepts input; snap caption centered.");
        selection.Select(selected);await Task.Delay(350);
        if(expansion.Amount!=0 || Mathf.Abs(view.viewport.rect.width-narrow)>.1f) throw new Exception("Selection did not restore inspector");
        selection.Select(null);await Task.Delay(350);
        view.controls.First(c=>c.action=="手順を編集").button.onClick.Invoke();await Task.Delay(350);
        if(expansion.Amount!=0 || expansion.inspector.alpha!=1) throw new Exception("Step inspector hidden without object selection");
        view.controls.First(c=>c.action=="配置を編集").button.onClick.Invoke();await Task.Delay(350);
        return "PASS: deselect/expand, select/restore, unselected step mode, rows, input area, snap alignment. Left unselected for capture.";
    }
}
