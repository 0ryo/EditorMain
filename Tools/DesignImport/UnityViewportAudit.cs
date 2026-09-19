using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;

public static class UnityViewportAudit
{
    static void Check(bool value,string message) {if(!value) throw new InvalidOperationException(message);}
    public static async Task<string> Verify()
    {
        var controller=UnityEngine.Object.FindFirstObjectByType<SkillSyncEditorController>();
        Check(Application.isPlaying && controller!=null && controller.gameObject.scene.path.EndsWith("ReferencePreview.unity"),"Use isolated functional verification scene");
        var view=controller.GetComponent<SkillSyncDesignView>();
        var selection=UnityEngine.Object.FindFirstObjectByType<SelectionService>();
        var obj=selection.Current;
        Check(obj!=null,"An actual object must be selected");
        var refresh=typeof(SkillSyncEditorController).GetMethod("Refresh",BindingFlags.Instance|BindingFlags.NonPublic);
        string original=obj.GetDisplayName();
        var field=view.fields.First(f=>f.role=="name").input;
        field.onEndEdit.Invoke("A");refresh.Invoke(controller,null);
        float shortWidth=((RectTransform)view.partALabel.transform.parent).rect.width;
        field.onEndEdit.Invoke("Toolbox_Basic_Proxy");refresh.Invoke(controller,null);
        var host=(RectTransform)view.partALabel.transform.parent;
        Check(host.rect.width>shortWidth+80,"Name background grows for long names");
        Check(Mathf.Abs(host.rect.width-view.partALabel.GetPreferredValues("Toolbox_Basic_Proxy").x-28)<1,"Name background follows preferred text width with 14px padding");
        Check(Screen.width==2560 && Screen.height==1440,"2560x1440 Game view");
        Check(view.orientation.sourceCamera.backgroundColor==EditWorkspace.BackgroundColor,"Original viewport background restored");
        var corners=new Vector3[4];((RectTransform)view.transform).GetWorldCorners(corners);
        Check(Vector3.Distance(corners[0],Vector3.zero)<1 && Vector3.Distance(corners[2],new Vector3(2560,1440,0))<1,"Canvas fills 16:9 without cropping or letterboxing");
        Check(!view.visuals.Any(v=>v.label!=null && v.label.text=="上" && v.target.activeInHierarchy),"Static orientation text removed");
        Canvas.ForceUpdateCanvases();var before=view.orientation.canvasRenderer.GetMesh().vertices;
        var camera=view.orientation.sourceCamera;var rotation=camera.transform.rotation;
        camera.transform.rotation=rotation*Quaternion.Euler(12,35,0);
        await Task.Delay(120);Canvas.ForceUpdateCanvases();
        Check(before.Length>0 && !before.SequenceEqual(view.orientation.canvasRenderer.GetMesh().vertices),"Orientation geometry follows camera orbit");
        camera.transform.rotation=rotation;
        field.onEndEdit.Invoke("A");refresh.Invoke(controller,null);
        Check(Mathf.Abs(host.rect.width-shortWidth)<1,"Name background shrinks again");
        field.onEndEdit.Invoke("Toolbox_Basic_Proxy");refresh.Invoke(controller,null);
        string result="PASS: 2560x1440, full 16:9 canvas, original #424242 background, long/short label sizing, static text removed, camera-relative gizmo geometry.";
        File.WriteAllText("Design/skillsync_codex_handoff/verification/viewport-results.txt",result);
        return result;
    }
}
