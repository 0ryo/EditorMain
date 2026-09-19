using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

// Runs only in the isolated reference scene, with all project saves redirected to verification/.
public static class UnityInteractionAudit
{
    static readonly BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    static readonly List<string> checks=new();
    static void Check(bool value,string name) {if(!value) throw new InvalidOperationException(name);checks.Add(name);}
    static T Field<T>(object obj,string name)=>(T)obj.GetType().GetField(name,Private).GetValue(obj);
    static void Invoke(object obj,string name,params object[] args)=>obj.GetType().GetMethod(name,Private).Invoke(obj,args);
    static void Click(SkillSyncDesignView view,string action)
    {
        var control=view.controls.First(c=>c.action==action && c.button.gameObject.activeInHierarchy);
        Check(control.button.interactable,"Enabled: "+action);control.button.onClick.Invoke();
    }
    public static async Task<string> Run()
    {
        checks.Clear();
        var fixture=UnityEngine.Object.FindFirstObjectByType<SkillSyncReferencePreview>();
        if(!Application.isPlaying || fixture==null || !fixture.gameObject.scene.path.EndsWith("ReferencePreview.unity"))
            throw new InvalidOperationException("Play the isolated reference scene first.");
        Application.runInBackground=true;
        EditorProjectStore.EditorVerificationProjectsDirectory=Path.GetFullPath("Design/skillsync_codex_handoff/verification/test-projects");
        var view=fixture.view;
        fixture.StopAllCoroutines();
        fixture.ApplyState(0);
        var part=fixture.samplePart.gameObject;var marker=fixture.sampleMarker.gameObject;
        part.name="基本部品";marker.name="配置の目印";
        part.transform.position=marker.transform.position=new Vector3(10000,10000,10000);
        view.transform.Find("ReferenceLists_EditorOnly").gameObject.SetActive(false);
        var registry=ScriptableObject.CreateInstance<PrefabRegistry>();
        registry.entries=new List<PrefabEntry>{new PrefabEntry{typeId="Verification/Part",prefab=part},new PrefabEntry{typeId="Verification/Marker",prefab=marker}};
        var catalog=view.transform.root.GetComponentInChildren<CatalogUI>(true);
        typeof(CatalogUI).GetField("registry",Private).SetValue(catalog,registry);
        UnityEngine.Object.DestroyImmediate(fixture);
        catalog.gameObject.SetActive(true);
        var controller=view.gameObject.AddComponent<SkillSyncEditorController>();
        await Task.Delay(1000);
        for(int i=0;i<100 && catalog.IsRestoringModels;i++) await Task.Delay(200);
        Check(!catalog.IsRestoringModels,"Model restoration finished");
        FrameVerificationCamera();
        var placement=UnityEngine.Object.FindFirstObjectByType<PlacementController>();
        placement.RegisterRuntimePrefab("Verification/Part",part);placement.RegisterRuntimePrefab("Verification/Marker",marker);
        Check(placement.PlaceForDesignUi("Verification/Part",new Vector3(-.1f,.9f,0),Quaternion.identity,new Vector3(.3f,.2f,.25f),"部品A"),"Place part through command service");
        Check(placement.PlaceForDesignUi("Verification/Marker",new Vector3(.55f,.9f,0),Quaternion.identity,new Vector3(.3f,.025f,.24f),"目印B"),"Place marker through command service");
        var placed=UnityEngine.Object.FindObjectsByType<PlacedObject>(FindObjectsSortMode.None);
        var a=placed.First(p=>p.TypeId=="Verification/Part");var b=placed.First(p=>p.TypeId=="Verification/Marker");
        a.SetDescription("動かせる部品");b.SetDescription("配置先の目印");
        var selection=UnityEngine.Object.FindFirstObjectByType<SelectionService>();selection.Select(a);
        var grid=UnityEngine.Object.FindFirstObjectByType<WorkspaceFloorGrid>();if(grid!=null) grid.gameObject.SetActive(false);
        var original=a.transform.position;
        Invoke(controller,"Nudge",Vector3.right);Check(a.transform.position!=original,"Nudge command changes placement (PDF removes its button)");
        Click(view,"↶ 元に戻す");Check(a.transform.position==original,"Undo restores placement");
        var name=view.fields.First(f=>f.role=="name").input;
        name.onEndEdit.Invoke("部品A_変更");Check(a.GetDisplayName()=="部品A_変更","Name field updates actual object");
        Click(view,"↶ 元に戻す");Check(a.GetDisplayName()=="部品A","Undo restores name");
        Invoke(controller,"BeginGhost",registry.entries[0]);Check(view.State==3,"Ghost state");
        int count=placed.Length;Click(view,"キャンセル");Check(UnityEngine.Object.FindObjectsByType<PlacedObject>(FindObjectsSortMode.None).Length==count,"Cancel ghost preserves data");
        Invoke(controller,"BeginGhost",registry.entries[0]);Click(view,"ここに配置");
        Check(UnityEngine.Object.FindObjectsByType<PlacedObject>(FindObjectsSortMode.None).Length==count+1,"Confirm ghost creates exactly one object");
        Click(view,"↶ 元に戻す");await Task.Delay(100);
        Check(UnityEngine.Object.FindObjectsByType<PlacedObject>(FindObjectsSortMode.None).Length==count,"Ghost confirmation undo");
        selection.Select(a);
        var search=view.fields.First(f=>f.role=="search").input;
        search.text="存在しない部品";
        Check(Field<List<SkillSyncDesignRow>>(controller,"libraryRows").Count==0,"Search filters the actual library");
        Check(!search.placeholder.isActiveAndEnabled,"Search placeholder hides while typing");
        Invoke(controller,"Refresh");
        Check(!search.placeholder.isActiveAndEnabled,"Refresh preserves search placeholder state");
        search.text="";
        var graph=UnityEngine.Object.FindFirstObjectByType<CurriculumGraphService>();
        ScenarioNode current=null,condition=null;
        graph.ExecuteCommand("Verification lesson",()=>{
            graph.curriculum=new Curriculum();graph.EnsureGraphInitialized();
            var steps=new List<ScenarioNode>();
            foreach(var title in new[]{"部品を確認する","部品を目印に置く","配置を確認する"}) {
                var step=graph.AddStep();step.step.title=title;step.step.body="部品Aを目印Bに近づけ、\nそのまま3秒間保ってください。";steps.Add(step);
                var c=graph.AddCondition();c.condition.type=ConditionTypeCatalog.SnapHold;c.condition.objectAId=a.Id;c.condition.objectBId=b.Id;
                ConditionTypeCatalog.SetNumber(c.condition,ConditionTypeCatalog.DistanceKey,.5f);ConditionTypeCatalog.SetNumber(c.condition,ConditionTypeCatalog.HoldSecondsKey,3);
                graph.TryBindConditionToStep(c.nodeId,step.nodeId,out _);
                if(steps.Count==2) {current=step;condition=c;}
            }
            graph.AddEdge(graph.GetStartNode().nodeId,steps[0].nodeId);graph.AddEdge(steps[0].nodeId,steps[1].nodeId);
            graph.AddEdge(steps[1].nodeId,steps[2].nodeId);graph.AddEdge(steps[2].nodeId,graph.GetEndNode().nodeId);return true;
        });
        Click(view,"手順を編集");
        var rows=Field<List<SkillSyncDesignRow>>(controller,"stepRows");rows[1].button.onClick.Invoke();
        Check(view.State==1,"Step edit state");
        Check(Field<string>(controller,"stepId")==current.nodeId,"Step selection connects to graph");
        return await VerifyTrial(controller,view,graph,current,condition,a,b,original);
    }
    public static async Task<string> Resume()
    {
        var controller=UnityEngine.Object.FindFirstObjectByType<SkillSyncEditorController>();
        var view=controller.GetComponent<SkillSyncDesignView>();
        var graph=UnityEngine.Object.FindFirstObjectByType<CurriculumGraphService>();
        var current=graph.FindNode(Field<string>(controller,"stepId"));
        var condition=graph.GetConditionNodesForStep(current.nodeId)[0];
        var placed=UnityEngine.Object.FindObjectsByType<PlacedObject>(FindObjectsSortMode.None);
        var a=placed.First(p=>p.Id==condition.condition.objectAId);var b=placed.First(p=>p.Id==condition.condition.objectBId);
        return await VerifyTrial(controller,view,graph,current,condition,a,b,a.transform.position);
    }
    public static void FrameVerificationCamera()
    {
        var camera=UnityEngine.Object.FindFirstObjectByType<SkillSyncEditorController>();
        var rig=UnityEngine.Object.FindFirstObjectByType<EditorCameraController>();
        if(camera==null || !camera.gameObject.scene.path.EndsWith("ReferencePreview.unity")) throw new InvalidOperationException("Verification scene only");
        var target=Field<Camera>(camera,"cameraView");
        rig.pivot.position=new Vector3(0,.925f,0);
        rig.pivot.rotation=Quaternion.LookRotation(new Vector3(-4,-2.028f,2));
        target.transform.localPosition=new Vector3(0,0,-4.91f);target.transform.localRotation=Quaternion.identity;
        target.orthographic=true;target.orthographicSize=.765f;
        Invoke(rig,"SyncPivotAngles");
    }
    static async Task<string> VerifyTrial(SkillSyncEditorController controller,SkillSyncDesignView view,CurriculumGraphService graph,ScenarioNode current,ScenarioNode condition,PlacedObject a,PlacedObject b,Vector3 original)
    {
        List<SkillSyncDesignRow> rows;
        var selection=UnityEngine.Object.FindFirstObjectByType<SelectionService>();
        Click(view,"▶ この条件を試す");Check(view.State==2,"Trial state");
        var trial=Field<SkillSyncTrialSession>(controller,"trial");
        Check(trial!=null,"Real condition evaluator session");
        trial.Find(a.Id).position=trial.Find(b.Id).position+Vector3.left*.32f;
        await Task.Delay(600);Check(trial.HeldSeconds>0,"Hold progresses in range");
        Click(view,"Ⅱ 一時停止");float paused=trial.HeldSeconds;
        await Task.Delay(250);Check(trial.Paused && trial.HeldSeconds==paused,"Pause freezes the actual hold evaluator");
        Click(view,"Ⅱ 一時停止");Check(!trial.Paused,"Trial resumes");
        Click(view,"初期配置に戻す");Check(trial.HeldSeconds==0 && trial.Find(a.Id).position==original,"Reset restores trial pose and timer");
        trial.Find(a.Id).position=trial.Find(b.Id).position+Vector3.left*.32f;
        await Task.Delay(150);Check(trial.HeldSeconds>0,"Holding resumes after reset");
        trial.Find(a.Id).position=trial.Find(b.Id).position+Vector3.left;
        await Task.Delay(150);Check(trial.HeldSeconds==0,"Leaving range resets hold");
        trial.Find(a.Id).position=trial.Find(b.Id).position+Vector3.left*.32f;
        await Task.Delay(3300);Check(view.State==4 && trial.Complete,"Condition completion state");
        Check(a.transform.position==original,"Trial does not mutate original pose");
        Click(view,"次の手順を確認");Check(view.State==1,"Completion returns to next step");
        Check(!a.GetComponent<Renderer>().forceRenderingOff,"Original rendering restored after trial");
        rows=Field<List<SkillSyncDesignRow>>(controller,"stepRows");rows[1].button.onClick.Invoke();
        graph.ExecuteCommand("Unset target",()=>{condition.condition.objectBId="";return true;});
        Click(view,"↑ 教材を書き出す");Check(view.State==5 && view.modal.activeInHierarchy,"Validation modal opens for missing target");
        Check(Directory.GetFiles(EditorProjectStore.ProjectsDirectory,"*.skillsync.json").Length>0,"Draft saved to isolated verification directory");
        Check(controller.BlocksWorkspace(new Vector2(600,400)),"Modal blocks workspace input");
        Click(view,"該当箇所を修正");Check(view.State==1 && Field<string>(controller,"pickTarget")=="B","Validation navigates to missing target");
        Invoke(controller,"Pick",b.Id);Check(condition.condition.objectBId==b.Id,"Target picking updates graph");
        Click(view,"配置を編集");selection.Select(a);
        File.WriteAllText("Design/skillsync_codex_handoff/verification/interaction-results.txt",string.Join("\n",checks));
        return string.Join("\n",checks);
    }
}
