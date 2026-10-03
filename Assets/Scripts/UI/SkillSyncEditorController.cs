using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TMPro;
using UnityEngine;

[DefaultExecutionOrder(200)]
[RequireComponent(typeof(SkillSyncDesignView))]
public sealed class SkillSyncEditorController : MonoBehaviour
{
    public static SkillSyncEditorController Active { get; private set; }
    SkillSyncDesignView view;
    CatalogUI catalog;
    PlacementController placement;
    SelectionService selection;
    CurriculumGraphService graph;
    EditorProjectService project;
    EditorCameraController cameraController;
    Camera cameraView;
    Rect previousCameraRect;
    Color previousCameraColor;
    string stepId, conditionId, search = "", category = "すべて", pickTarget;
    string status = "", catalogStatus = "", librarySignature = "";
    int mode;
    float nextRefresh, trialTime;
    bool initialized;
    Transform trialSelected;
    MoveTool trialMoveTool;
    SkillSyncTrialSession trial;
    SkillSyncPlacementGhost ghost;
    readonly List<string> logs = new();
    readonly SkillSyncModelThumbnails thumbnails = new();
    readonly List<SkillSyncDesignRow> libraryRows = new(), stepRows = new(), objectRows = new();
    readonly Dictionary<Behaviour,bool> pausedTools = new();
    readonly Dictionary<PlacedObject, SkillSyncDesignRow> placedRows = new();
    readonly Dictionary<SkillSyncDesignRow, string> libraryTypes = new();
    readonly Dictionary<string, GameObject> thumbnailSources = new();
    readonly List<Renderer> labelRenderers = new();
    SkillSyncDesignRow ghostRow;
    Transform labelTargetA, labelTargetB;
    readonly Vector3[] corners = new Vector3[4];
    GraphValidationResult validation;
    ScenarioNode Step => graph != null ? graph.FindNode(stepId) : null;
    ScenarioNode Condition => graph != null ? graph.FindNode(conditionId) : null;
    PlacedObject[] Placed => FindObjectsByType<PlacedObject>(FindObjectsSortMode.None);
    bool Modal => view != null && view.State == 5;
    public bool BlocksEditingShortcuts => !Application.isFocused || EditWorkspace.HasOpenModal || Modal || trial != null || ghost != null || pickTarget != null;
    public bool CapturesTextSensitiveInput => !Application.isFocused || EditWorkspace.HasOpenModal || Modal || ghost != null || pickTarget != null;
    bool TextInputFocused => UnityEngine.EventSystems.EventSystem.current != null &&
        UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject != null &&
        UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject.GetComponent<TMP_InputField>() is TMP_InputField input && input.isFocused;
    void Awake()
    {
        var group = GetComponent<CanvasGroup>();
        if (group == null) group = gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0;
    }

    IEnumerator Start()
    {
        view = GetComponent<SkillSyncDesignView>();
        UiScaleController.Ensure(GetComponentInParent<Canvas>().rootCanvas.transform)?.Apply(1f);
        yield return null; // CatalogUI completes its existing service bootstrap first.
        Active = this;
        catalog = transform.root.GetComponentInChildren<CatalogUI>(true);
        if (catalog != null)
        {
            catalogStatus = catalog.StatusMessage;
            catalog.StatusChanged += CatalogStatus;
        }
        placement = FindFirstObjectByType<PlacementController>();
        if (placement == null) placement = RuntimeEditComposition.ResolvePlacementController(null, PrefabRegistry.LoadDefault());
        selection = FindFirstObjectByType<SelectionService>();
        graph = FindFirstObjectByType<CurriculumGraphService>();
        if (graph == null) graph = placement.gameObject.AddComponent<CurriculumGraphService>();
        project = EditorProjectService.Ensure(placement.transform);
        view.EnsureProjectLoadControl();
        view.EnsureConditionControls();
        view.EnsureViewportLabels();
        TopCenterNotification.Ensure(transform, view.inspectorTitle);
        cameraView = EditWorkspace.ResolveCamera();
        if(view.orientation!=null) view.orientation.sourceCamera=cameraView;
        cameraController = FindFirstObjectByType<EditorCameraController>();
        if (cameraView != null) { previousCameraRect=cameraView.rect; previousCameraColor=cameraView.backgroundColor; }
        foreach(var control in view.controls)
        {
            var action=control.action;
            control.button.onClick.AddListener(()=>Act(action));
        }
        foreach(var field in view.fields)
        {
            var role=field.role;
            field.input.onEndEdit.AddListener(value=>Edit(role,value));
            if(role=="search") field.input.onValueChanged.AddListener(value=>{search=value;RebuildLibrary();});
        }
        graph.GraphChanged += GraphChanged;
        selection.OnSelectionChanged += SelectionChanged;
        placement.ObjectPlaced += ObjectPlaced;
        project.StatusChanged += ProjectStatus;
        if(CommandService.I != null) CommandService.I.Stack.HistoryChanged += HistoryChanged;
        initialized=true;
        GraphChanged();RebuildLibrary();Refresh();GetComponent<CanvasGroup>().alpha=1;
        if (!string.IsNullOrEmpty(project.RecoveryProtectionWarning))
            EditorProjectPanel.Ensure(transform.root)?.ShowRecoveryProtectionWarning();
    }
    void OnDisable()
    {
        EndTrial(); CancelGhost();
        if(graph!=null) graph.GraphChanged-=GraphChanged;
        if(selection!=null) selection.OnSelectionChanged-=SelectionChanged;
        if(placement!=null) placement.ObjectPlaced-=ObjectPlaced;
        if(project!=null) project.StatusChanged-=ProjectStatus;
        if(catalog!=null) catalog.StatusChanged-=CatalogStatus;
        if(CommandService.I!=null) CommandService.I.Stack.HistoryChanged-=HistoryChanged;
        if(cameraView!=null) {cameraView.rect=previousCameraRect;cameraView.backgroundColor=previousCameraColor;}
        if(Active==this) Active=null;
        thumbnails.Dispose();
    }
    public bool BlocksWorkspace(Vector2 point)
    {
        if (!Application.isFocused || EditWorkspace.HasOpenModal) return true;
        if(!initialized) return false;
        if(GetComponent<SkillSyncObjectPicker>()?.BlocksPointer(point)==true) return true;
        if(Modal || pickTarget!=null || ghost!=null) return true;
        return !RectTransformUtility.RectangleContainsScreenPoint(view.viewport,point);
    }
    void Update()
    {
        if(!initialized) return;
        if(!Application.isFocused || EditWorkspace.HasOpenModal)
        {
            return;
        }
        var expansion=GetComponent<SkillSyncWorkspaceExpansion>();
        if(expansion!=null) {
            expansion.SetExpanded(mode==0 && !Modal && trial==null && ghost==null && selection.Selected.Count!=1);
            expansion.Tick(Time.unscaledDeltaTime);
        }
        if(!BlocksEditingShortcuts && !TextInputFocused)
        {
            if(EditInput.ProjectShortcutPressedThisFrame(true)) project.Save(project.CurrentProjectName,out status);
            if(EditInput.ProjectShortcutPressedThisFrame(false)) EditorProjectPanel.Ensure(transform.root)?.OpenForDesignUi();
        }
        bool inside=RectTransformUtility.RectangleContainsScreenPoint(view.viewport,EditInput.MousePosition);
        if(!TextInputFocused && EditInput.CancelPressedThisFrame())
        {
            if(GetComponent<SkillSyncObjectPicker>()?.IsOpen==true) GetComponent<SkillSyncObjectPicker>().Close();
            else if(Modal) {mode=1;view.Show(1);}
            else if(pickTarget!=null) pickTarget=null;
            else if(ghost!=null) CancelGhost();
            else if(trial!=null) {EndTrial();mode=1;}
            else selection.Select(null);
            Refresh();
        }
        if(!Modal && inside && !TextInputFocused && GetComponent<SkillSyncObjectPicker>()?.BlocksPointer(EditInput.MousePosition)!=true)
        {
            if(ghost!=null)
            {
                ghost.Follow(cameraView,EditInput.MousePosition);
                if(EditInput.LeftPressedThisFrame()) ConfirmGhost();
            }
            else if(pickTarget!=null && EditInput.LeftPressedThisFrame())
            {
                if(Physics.Raycast(cameraView.ScreenPointToRay(EditInput.MousePosition),out var hit,10000))
                {
                    var p=hit.collider.GetComponentInParent<PlacedObject>();
                    if(p!=null) Pick(p.Id);
                }
            }
            else if(trial!=null) SelectTrialObject();
        }
        if(trial!=null && Condition!=null)
        {
            bool before=trial.InRange, completed=trial.Complete;
            if(!trial.Paused) trialTime+=Time.unscaledDeltaTime;
            trial.Tick(graph.GetConditionNodesForStep(stepId),conditionId,Time.unscaledDeltaTime);
            UpdateTrialGizmo();
            if(before!=trial.InRange) Log(trial.InRange?"距離の条件が成立":"範囲外：保持時間をリセット");
            if(!completed && trial.Complete) Log("条件が成立しました");
            RefreshTrial();
        }
        if(Time.unscaledTime>=nextRefresh)
        {
            nextRefresh=Time.unscaledTime+.25f;
            RefreshValues();
            var signature=string.Join("|",Entries().Select(e=>e.typeId));
            if(signature!=librarySignature) RebuildLibrary();
        }
    }
    void LateUpdate()
    {
        // Presentation must still follow CanvasScaler while input is blocked or
        // the Editor is unfocused. LateUpdate observes the current canvas size.
        if(!initialized) return;
        if(cameraView==null) return;
        view.viewport.GetWorldCorners(corners);
        var min=RectTransformUtility.WorldToScreenPoint(null,corners[0]);
        var max=RectTransformUtility.WorldToScreenPoint(null,corners[2]);
        cameraView.rect=new Rect(min.x/Screen.width,min.y/Screen.height,(max.x-min.x)/Screen.width,(max.y-min.y)/Screen.height);
        cameraView.backgroundColor=EditWorkspace.BackgroundColor;
        MoveLabel(view.partALabel, labelTargetA);
        MoveLabel(view.partBLabel, labelTargetB);
    }
    void Act(string action)
    {
        if(Modal && action!="編集を続ける" && action!="該当箇所を修正") return;
        if(action!="PickA" && action!="PickB") GetComponent<SkillSyncObjectPicker>()?.Close();
        switch(action)
        {
            case "配置を編集": EndTrial();CancelGhost();mode=0;pickTarget=null;break;
            case "手順を編集": EndTrial();CancelGhost();mode=1;pickTarget=null;break;
            case "動作を確認": case "▶ この条件を試す": BeginTrial();return;
            case "↶ 元に戻す": if(trial==null) CommandService.I?.Stack.Undo();break;
            case "↷": if(trial==null) CommandService.I?.Stack.Redo();break;
            case "全体を見る": cameraController?.ResetToDefaultView();break;
            case "上から見る": cameraController?.SetViewPreset(EditorCameraController.ViewPreset.Top);break;
            case "選択を表示": cameraController?.FocusSelected();break;
            case "移動": EditModeService.I?.SetMode(EditMode.Transform);break;
            case "回転": EditModeService.I?.SetMode(EditMode.Transform);view.Focus("angle");break;
            case "大きさ": EditModeService.I?.SetMode(EditMode.Scale);break;
            case "←": Nudge(Vector3.left);break;
            case "→": Nudge(Vector3.right);break;
            case "↑": Nudge(Vector3.forward);break;
            case "↓": Nudge(Vector3.back);break;
            case "複製": selection.DuplicateSelected();break;
            case "削除": selection.DeleteSelected();break;
            case "キャンセル": CancelGhost();break;
            case "ここに配置": ConfirmGhost();break;
            case "＋ 3Dモデルを読み込む": catalog?.ImportForDesignUi();return;
            case "＋ 手順を追加": AddStep();break;
            case "↑ 上へ": Reorder(-1);break;
            case "↓ 下へ": Reorder(1);break;
            case "＋ 完了条件を追加": AddCondition();break;
            case "PickA": OpenObjectPicker("A");return;
            case "PickB": OpenObjectPicker("B");return;
            case "Snap": EditSnapSettings.Configure(.1f,15,!EditSnapSettings.Enabled);break;
            case "PreviousCondition": MoveCondition(-1);break;
            case "NextCondition": MoveCondition(1);break;
            case "DeleteCondition": DeleteCondition();break;
            case "詳細設定": catalog?.ShowDesignSettings();return;
            case "ヘルプ": case "Guide": HintPanelController.Ensure(transform.root)?.Show();return;
            case "Save": project.Save(project.CurrentProjectName,out status);break;
            case "教材を読み込む":
                if (!BlocksEditingShortcuts) EditorProjectPanel.Ensure(transform.root)?.OpenForDesignUi();
                return;
            case "↑ 教材を書き出す": Export();return;
            case "編集を続ける": mode=1;view.Show(1);break;
            case "該当箇所を修正": FocusIssue();return;
            case "編集に戻る": EndTrial();mode=1;break;
            case "Ⅱ 一時停止": if(trial!=null) {trial.Paused=!trial.Paused;UpdateTrialGizmo();status=trial.Paused?"一時停止中":"試行を再開しました";}break;
            case "初期配置に戻す": case "もう一度試す": ResetTrial();logs.Clear();trialTime=0;break;
            case "最初から試す":
                EndTrial();stepId=graph.GetDisplayOrderedSteps().FirstOrDefault()?.nodeId;conditionId=null;GraphChanged();BeginTrial();return;
            case "次の手順を確認": EndTrial();NextStep();mode=1;break;
            case "すべて": case "部品": case "環境": category=action;RebuildLibrary();break;
        }
        Refresh();
    }
    void Edit(string role,string value)
    {
        if(trial!=null || Modal) return;
        if(role=="search") return;
        if(role=="project") {if(!string.IsNullOrWhiteSpace(value)) project.Save(value.Trim(),out status);}
        else if(role=="name")
        {
            if(ghost!=null) ghost.DisplayName=value;
            else if(selection.Current!=null)
            {
                PlacedObjectMetadataService.SetDisplayName(selection.Current, value);
            }
        }
        else if(role=="height" || role=="angle")
        {
            if(!Number(value,out float n)) {status="数値を入力してください";Refresh();return;}
            Transform target=ghost!=null?ghost.Transform:selection.Current!=null?selection.Current.transform:null;
            if(target!=null)
            {
                var session=ghost==null?new SelectionTransformSession(selection):null;
                if(role=="height")
                {
                    if(ghost!=null) ghost.SetHeight(n/100f);
                    else target.position=new Vector3(target.position.x,n/100f,target.position.z);
                }
                else target.rotation=Quaternion.Euler(0,n,0);
                session?.Commit("Change object transform");
            }
        }
        else if(role=="stepTitle" || role=="body")
        {
            if(Step!=null) graph.UpdateStepData(Step.nodeId,"Edit step",data=>{if(role=="stepTitle") data.title=value;else data.body=value;});
        }
        else if(role=="distance" || role=="hold")
        {
            if(Condition!=null && Number(value,out float n))
            {
                float v=role=="distance"?n/100:n;
                string key=role=="distance"?ConditionTypeCatalog.DistanceKey:ConditionTypeCatalog.HoldSecondsKey;
                var definition=ConditionTypeCatalog.Find(Condition.condition.type)?.parameters.FirstOrDefault(p=>p.key==key);
                if(definition==null || v<definition.minValue || v>definition.maxValue) status="条件の許容範囲内の数値を入力してください";
                else graph.UpdateConditionData(Condition.nodeId,"Edit condition",data=>ConditionTypeCatalog.SetNumber(data,key,v));
            }
            else status="数値を入力してください";
        }
        Refresh();
    }
    static bool Number(string s,out float value) => float.TryParse(s,NumberStyles.Float,CultureInfo.CurrentCulture,out value) && !float.IsNaN(value) && !float.IsInfinity(value);
    void Nudge(Vector3 direction)
    {
        if(ghost!=null) {ghost.Transform.position+=direction*.1f;return;}
        if(selection.Current==null || !SelectionService.CanEdit(selection.Current)) return;
        var session=new SelectionTransformSession(selection);selection.Current.transform.position+=direction*.1f;session.Commit("Move 10 cm");
    }
    void AddStep()
    {
        if(!graph.TryAddStepAtEnd(out var step, out var reason))
        {
            if(!string.IsNullOrWhiteSpace(reason)) status="手順を追加できません: "+reason;
            return;
        }
        stepId=step.nodeId;
        AddCondition();
    }
    bool TryLinear(out List<ScenarioNode> steps)
    {
        steps=graph.GetDisplayOrderedSteps();
        if(steps.Count==0) return true;
        if(graph.TryBuildLinearStepSequence(out steps,out var reason)) return true;
        status="既存の分岐を保持するため、この並べ替えは利用できません: "+reason;return false;
    }
    void Reorder(int offset)
    {
        if(!TryLinear(out var steps)) return;
        int i=steps.FindIndex(s=>s.nodeId==stepId),j=i+offset;
        if(i<0 || j<0 || j>=steps.Count) return;
        (steps[i],steps[j])=(steps[j],steps[i]);
        graph.ReorderLinearSteps(steps.Select(s=>s.nodeId).ToList());
    }
    void AddCondition()
    {
        if(Step==null) {status="先に手順を追加してください";return;}
        if(graph.GetConditionCountForStep(stepId)>=graph.GetMaxConditionsPerStep()) {status="条件数の上限です";return;}
        if(!graph.TryAddConditionToStep(stepId, out var condition, out var reason))
        {
            if(!string.IsNullOrWhiteSpace(reason)) status=reason;
            return;
        }
        conditionId=condition.nodeId;
    }
    void MoveCondition(int offset)
    {
        var conditions=Step!=null?graph.GetConditionNodesForStep(stepId):new List<ScenarioNode>();
        if(conditions.Count<=1) return;
        int index=conditions.FindIndex(c=>c.nodeId==conditionId);
        int next=index+offset;
        if(index<0 || next<0 || next>=conditions.Count) return;
        conditionId=conditions[next].nodeId;
    }
    void DeleteCondition()
    {
        var conditions=Step!=null?graph.GetConditionNodesForStep(stepId):new List<ScenarioNode>();
        int index=conditions.FindIndex(c=>c.nodeId==conditionId);
        if(index<0) return;
        string deletingId=conditionId;
        var remaining=conditions.Where(c=>c.nodeId!=deletingId).ToList();
        string nextId=remaining.Count==0?null:remaining[Mathf.Clamp(index-1,0,remaining.Count-1)].nodeId;
        bool removed=graph.TryRemoveNode(deletingId);
        if(!removed) return;
        conditionId=nextId;
        status="完了条件を削除しました";
        GraphChanged();
    }
    void OpenObjectPicker(string which)
    {
        if(Condition==null) {status="先に完了条件を追加してください";Refresh();return;}
        var picker=GetComponent<SkillSyncObjectPicker>();
        if(picker==null)return;
        pickTarget=null;picker.SetPicking(null);
        picker.Show(Placed,which=="A"?491:588,id=>{pickTarget=which;Pick(id);},()=>{
            pickTarget=which;status="対象をビューポートでクリックしてください（Escで取消）";Refresh();
        });
    }
    void Pick(string id)
    {
        if(Condition==null) {pickTarget=null;status="先に完了条件を追加してください";Refresh();return;}
        string which=pickTarget;
        graph.UpdateConditionData(Condition.nodeId,"Choose condition object",data=>
        {
            if(which=="A") data.objectAId=id;else data.objectBId=id;
        });
        pickTarget=null;status="対象を設定しました";Refresh();
    }
    void NextStep()
    {
        var next=ScenarioFlow.Next(graph.curriculum,stepId).Where(n=>n.nodeType==ScenarioNodeType.Step).ToList();
        if(next.Count==1) {stepId=next[0].nodeId;conditionId=null;GraphChanged();}
        else status=next.Count==0?"次の手順はありません":"分岐先の手順を左の一覧から選んでください";
    }
    void GraphChanged()
    {
        var steps=graph.GetDisplayOrderedSteps();
        if(Step==null) stepId=steps.FirstOrDefault()?.nodeId;
        var conditions=Step!=null?graph.GetConditionNodesForStep(stepId):new List<ScenarioNode>();
        if(!conditions.Any(c=>c.nodeId==conditionId)) conditionId=conditions.FirstOrDefault()?.nodeId;
        RebuildSteps();Refresh();
    }
    void SelectionChanged(PlacedObject _) {RefreshLibrarySelection();RebuildObjects();RefreshValues();}
    void RefreshLibrarySelection()
    {
        string type = ghost != null ? ghost.TypeId : selection.Current != null ? selection.Current.TypeId : null;
        foreach (var pair in libraryTypes) pair.Key.SetSelected(pair.Value == type);
    }
    void ObjectPlaced(PlacedObject _,string __) {RebuildObjects();RefreshValues();}
    void HistoryChanged() {RebuildObjects();RefreshValues();}
    void ProjectStatus(string message,bool success) {status=message;RefreshValues();}
    void CatalogStatus(string message) {catalogStatus=message;RefreshValues();}
    List<PrefabEntry> Entries() => catalog!=null?catalog.GetDesignLibraryEntries():placement.registry?.entries??new List<PrefabEntry>();
    static void Clear(List<SkillSyncDesignRow> rows)
    {foreach(var row in rows) if(row!=null) {row.gameObject.SetActive(false);Destroy(row.gameObject);} rows.Clear();}
    SkillSyncDesignRow Row(SkillSyncDesignRow template,RectTransform parent,List<SkillSyncDesignRow> rows)
    {var row=Instantiate(template,parent);row.gameObject.SetActive(true);rows.Add(row);return row;}
    void RebuildLibrary()
    {
        if(view==null) return;
        Clear(libraryRows);libraryTypes.Clear();thumbnailSources.Clear();var entries=Entries();librarySignature=string.Join("|",entries.Select(e=>e.typeId));
        foreach(var entry in entries)
        {
            if(entry.prefab==null) continue;
            thumbnailSources[entry.typeId] = entry.prefab;
            string title=entry.prefab.name,detail=entry.typeId;
            if(catalog!=null) catalog.TryGetTypeInfo(entry.typeId,out title,out detail);
            if(!string.IsNullOrEmpty(search) && (title+" "+entry.typeId).IndexOf(search,StringComparison.OrdinalIgnoreCase)<0) continue;
            bool env=entry.typeId.StartsWith("Env/",StringComparison.Ordinal);
            if(category=="環境" && !env || category=="部品" && env) continue;
            var row=Row(view.libraryTemplate,view.libraryContent,libraryRows);row.Set(title,detail,"",ghost!=null?ghost.TypeId==entry.typeId:selection.Current!=null && selection.Current.TypeId==entry.typeId);
            if(row.thumbnail!=null) row.thumbnail.texture=thumbnails.Get(entry.prefab);
            libraryTypes[row]=entry.typeId;
            var chosen=entry;row.button.onClick.AddListener(()=>BeginGhost(chosen));
            var remove=row.GetComponent<SkillSyncLibraryRemove>();
            if(remove!=null) remove.remove.onClick.AddListener(()=>{
                if(ghost!=null && ghost.TypeId==chosen.typeId)CancelGhost();
                catalog?.RemoveDesignLibraryEntry(chosen.typeId);RebuildLibrary();Refresh();
            });
        }
    }
    void RebuildSteps()
    {
        if(view==null || graph==null) return;
        Clear(stepRows);var steps=graph.GetDisplayOrderedSteps();
        for(int i=0;i<steps.Count;i++)
        {
            var step=steps[i];var row=Row(view.stepTemplate,view.stepsContent,stepRows);
            row.Set(step.step.title,step.step.body,(i+1).ToString(),step.nodeId==stepId);
            row.button.onClick.AddListener(()=>{EndTrial();stepId=step.nodeId;conditionId=null;mode=1;GraphChanged();});
        }
    }
    void RebuildObjects()
    {
        if(view==null) return;
        // Selection changes update the existing rows. Only additions/removals
        // instantiate or destroy UI and render a new thumbnail.
        foreach (var removed in placedRows.Keys.Where(p => p == null || !p.gameObject.activeInHierarchy).ToArray())
        {
            var row = placedRows[removed];
            objectRows.Remove(row); row.gameObject.SetActive(false); Destroy(row.gameObject);
            placedRows.Remove(removed);
        }
        if (ghost == null && ghostRow != null)
        {
            objectRows.Remove(ghostRow); ghostRow.gameObject.SetActive(false); Destroy(ghostRow.gameObject); ghostRow = null;
        }
        if(ghost!=null)
        {
            if (ghostRow == null) ghostRow=Row(view.objectTemplate,view.objectsContent,objectRows);
            ghostRow.transform.SetAsFirstSibling();
            ghostRow.Set(ghost.DisplayName+"（仮配置）","未確定","",true);
            ghostRow.status.text="未確定";
            ghostRow.thumbnail.texture=thumbnails.Get(ghost.Transform.gameObject);
        }
        foreach(var p in Placed)
        {
            if (!placedRows.TryGetValue(p, out var row))
            {
                row=Row(view.objectTemplate,view.objectsContent,objectRows); placedRows.Add(p,row);
                if(row.thumbnail!=null) row.thumbnail.texture=ObjectThumbnail(p);
                row.button.onClick.AddListener(()=>{if(pickTarget!=null) Pick(p.Id);else selection.Select(p);});
            }
            row.Set(p.GetDisplayName(),p.GetDescription(),"",selection.Contains(p));
        }
    }
    void Refresh()
    {
        if(!initialized) return;
        int state=Modal?5:trial!=null?(trial.Complete?4:2):ghost!=null?3:mode;
        view.Show(state);RebuildObjects();RefreshValues();
    }
    void RefreshValues()
    {
        if(!initialized) return;
        var picker=GetComponent<SkillSyncObjectPicker>();
        if(picker!=null) {
            picker.CancelPicking=()=>{pickTarget=null;Refresh();};
            picker.SetPicking(pickTarget);
        }
        view.Value("project",project.CurrentProjectName);
        view.Text("saved",project.IsDirty?"未保存の変更":"✓ 保存済み");
        string footerStatus = string.IsNullOrEmpty(catalogStatus) ? status : catalogStatus;
        view.Text("footer",trial!=null?"試行中の操作は教材の初期配置に保存されません":string.IsNullOrEmpty(footerStatus)?project.IsDirty?"未保存の変更があります":"✓ すべての変更を保存しました":footerStatus);
        view.Text("objectCount",Placed.Length+"件"+(ghost!=null?" ＋ 仮配置1件":""));
        view.Text("stepCount",graph.GetDisplayOrderedSteps().Count+"つの手順");
        var current=selection.Current;
        Transform target=ghost!=null?ghost.Transform:current!=null?current.transform:null;
        view.Text("selectionTitle",ghost!=null?"置く場所を選ぶ":current!=null?current.GetDisplayName():"部品を選択");
        view.Value("name",ghost!=null?ghost.DisplayName:current!=null?current.GetDisplayName():"");
        view.Value("height",target!=null?(target.position.y*100).ToString("0.##"):"");
        view.Value("angle",target!=null?target.eulerAngles.y.ToString("0.##"):"");
        int index=graph.GetDisplayOrderedSteps().FindIndex(s=>s.nodeId==stepId)+1;
        view.Text("inspectorLabel",mode==1?"手順"+index:ghost!=null?"仮配置":current!=null?"選択中":"未選択");
        view.UpdateSelectionHeader();
        view.Value("stepTitle",Step?.step.title??"");view.Value("body",Step?.step.body??"");
        view.Value("distance",Condition!=null?(DistanceLimit()*100).ToString("0.##"):"");
        view.Value("hold",Condition!=null?HoldLimit().ToString("0.##"):"");
        var a=FindPlaced(Condition?.condition.objectAId);var b=FindPlaced(Condition?.condition.objectBId);
        PositionLabel(view.partALabel,labelTargetA=trial!=null?trial.Find(Condition?.condition.objectAId):ghost!=null?ghost.Transform:a!=null?a.transform:target,
            trial!=null&&a!=null?a.GetDisplayName():ghost!=null?ghost.DisplayName:a!=null?a.GetDisplayName():current!=null?current.GetDisplayName():"");
        PositionLabel(view.partBLabel,labelTargetB=b!=null?b.transform:null,b!=null?b.GetDisplayName():"");
        if(view.objectAThumbnail!=null) {view.objectAThumbnail.texture=ObjectThumbnail(a);view.objectAThumbnail.enabled=a!=null;}
        if(view.objectBThumbnail!=null) {view.objectBThumbnail.texture=ObjectThumbnail(b);view.objectBThumbnail.enabled=b!=null;}
        if(view.conditionOverlay!=null) view.conditionOverlay.Configure(cameraView,
            trial!=null?trial.Find(Condition?.condition.objectAId):mode==1 && a!=null?a.transform:null,
            trial!=null?trial.Find(Condition?.condition.objectBId):mode==1 && b!=null?b.transform:null,DistanceLimit(),trial!=null && trial.InRange);
        view.Text("objectA",a!=null?a.GetDisplayName():"対象を選択してください");
        view.Text("objectB",b!=null?b.GetDisplayName():"対象を選択してください");
        bool valid=a!=null && b!=null && a!=b && Condition!=null;
        var conditions=Step!=null?graph.GetConditionNodesForStep(stepId):new List<ScenarioNode>();
        int conditionIndex=conditions.FindIndex(c=>c.nodeId==conditionId);
        view.Text("conditionHeading","完了する条件");
        view.Text("conditionCounter",conditions.Count==0?"0件":conditionIndex>=0?$"{conditionIndex+1}/{conditions.Count}":"");
        view.SetVisible("PreviousCondition",conditions.Count>1);
        view.SetVisible("NextCondition",conditions.Count>1);
        view.SetVisible("DeleteCondition",conditionIndex>=0);
        view.Enable("PreviousCondition",conditionIndex>0);
        view.Enable("NextCondition",conditionIndex>=0 && conditionIndex<conditions.Count-1);
        view.Enable("DeleteCondition",conditionIndex>=0);
        view.Text("conditionSummary",Step==null?"先に手順を追加してください":Condition==null?"完了条件がありません。下の＋で追加してください":valid?$"{DistanceLimit()*100:0.##} cm以内で{HoldLimit():0.##}秒間保つと完了":"未設定の対象があります");
        view.Enable("▶ この条件を試す",valid);
        view.Enable("↶ 元に戻す",trial==null);view.Enable("↷",trial==null);
        view.Enable("↑ 教材を書き出す",trial==null && ghost==null);
        view.Enable("教材を読み込む",!BlocksEditingShortcuts);
        view.Text("workspaceHint",trial!=null?"部品を選び、ギズモで操作 / 中・右ドラッグで視点移動":mode==1?$"手順{index}で使う部品を表示":"部品を選んで、作業面に配置します");
        view.Text("status",pickTarget!=null?status:ghost!=null?"＋ 仮配置中：クリックで確定 / Escで中止":trial!=null?trial.Complete?"✓ 条件が成立しました":"▶ 試行中：ギズモで部品を動かして条件を確かめます":current!=null?"● 選択中："+current.GetDisplayName()+" | Escで選択解除":"部品を選択してください");
        if(trial==null) view.viewportDistance.text=valid?$"現在の距離 {Vector3.Distance(SkillSyncTrialSession.Center(a.transform),SkillSyncTrialSession.Center(b.transform))*100:0} cm":"対象を選択してください";
        view.viewportGrid.text=$"グリッド {EditSnapSettings.GridSize*100:0.##} cm";
        view.viewportGrid.color=new Color32(88,103,124,255);
        view.Text("viewportMode",mode==1||trial!=null?valid?"判定範囲を表示中":"対象を選択してください":EditSnapSettings.ShouldSnap?"作業面に吸着：ON":"作業面に吸着：OFF");
        if(valid && cameraView!=null)
        {
            var ta=trial!=null?trial.Find(a.Id):a.transform;var tb=trial!=null?trial.Find(b.Id):b.transform;
            if(ta!=null && tb!=null)
            {
                var point=cameraView.WorldToScreenPoint((SkillSyncTrialSession.Center(ta)+SkillSyncTrialSession.Center(tb))*.5f);
                var rootRect=(RectTransform)transform;var badge=(RectTransform)view.viewportDistance.transform.parent;
                if(point.z>0 && RectTransformUtility.ScreenPointToLocalPointInRectangle(rootRect,point,null,out var local))
                {
                    var anchored=local-new Vector2(rootRect.rect.xMin,rootRect.rect.yMax)+new Vector2(-badge.rect.width/2,24);
                    badge.anchoredPosition=new Vector2(Mathf.Clamp(anchored.x,288,288+view.viewport.rect.width-badge.rect.width),Mathf.Clamp(anchored.y,-684,view.viewport.anchoredPosition.y));
                }
            }
        }
    }
    PlacedObject FindPlaced(string id) => string.IsNullOrEmpty(id)?null:Placed.FirstOrDefault(p=>p.Id==id);
    RenderTexture ObjectThumbnail(PlacedObject placed)
    {
        if (placed == null) return null;
        // A root's library thumbnail is shared by its instances. Parts retain
        // their own geometry preview instead of showing the entire model.
        var source = placed.gameObject;
        if (placed.modelRoot == null && thumbnailSources.TryGetValue(placed.TypeId, out var prefab) && prefab != null) source = prefab;
        return thumbnails.Get(source);
    }
    void PositionLabel(TMP_Text label,Transform target,string caption)
    {
        if(label==null) return;
        var host=(RectTransform)label.transform.parent;
        host.gameObject.SetActive(target!=null && !Modal);
        if(target==null || cameraView==null) return;
        label.text=caption;
        float width=Mathf.Clamp(label.GetPreferredValues(caption).x+28,48,view.viewport.rect.width-16);
        host.sizeDelta=new Vector2(width,34);
        label.rectTransform.sizeDelta=new Vector2(width-28,22);
        label.overflowMode=TextOverflowModes.Ellipsis;
    }
    void MoveLabel(TMP_Text label, Transform target)
    {
        if (label == null) return;
        var host = (RectTransform)label.transform.parent;
        if (target == null || cameraView == null || Modal) {host.gameObject.SetActive(false);return;}
        var position=target.position;
        target.GetComponentsInChildren<Renderer>(true, labelRenderers);
        bool found = false; Bounds bounds = default;
        foreach (var renderer in labelRenderers)
        {
            if (renderer == null) continue;
            if (!found) { bounds=renderer.bounds; found=true; } else bounds.Encapsulate(renderer.bounds);
        }
        if (found) position=new Vector3(bounds.center.x,bounds.max.y,bounds.center.z);
        var point=cameraView.WorldToScreenPoint(position);
        if(point.z<=0) {host.gameObject.SetActive(false);return;}
        host.gameObject.SetActive(true);
        var root=(RectTransform)transform;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(root,point,null,out var local);
        host.anchoredPosition=new Vector2(Mathf.Clamp(local.x-root.rect.xMin-host.rect.width/2,288,288+view.viewport.rect.width-host.rect.width),Mathf.Clamp(local.y-root.rect.yMax+50,-670,view.viewport.anchoredPosition.y));
    }
    float DistanceLimit() => Condition!=null?ConditionTypeCatalog.GetNumber(Condition.condition,ConditionTypeCatalog.DistanceKey,.1f):.1f;
    float HoldLimit() => Condition!=null?ConditionTypeCatalog.GetNumber(Condition.condition,ConditionTypeCatalog.HoldSecondsKey,0):0;
    void BeginTrial()
    {
        CancelGhost();
        if(catalog!=null && catalog.IsRestoringModels) {status="モデルの復元完了を待ってから試行してください";Refresh();return;}
        if(Condition==null || FindPlaced(Condition.condition.objectAId)==null || FindPlaced(Condition.condition.objectBId)==null || Condition.condition.objectAId==Condition.condition.objectBId)
        {status="試す条件の対象を設定してください";Refresh();return;}
        var conditions=graph.GetConditionNodesForStep(stepId);
        if(conditions.Any(c=>c.condition.type!=ConditionTypeCatalog.SnapHold && c.condition.type!=ConditionTypeCatalog.Proximity))
        {status="この画面で試行できるのは距離・保持条件です。既存の条件データは保持しています。";Refresh();return;}
        if(conditions.Any(c=>FindPlaced(c.condition.objectAId)==null || FindPlaced(c.condition.objectBId)==null || c.condition.objectAId==c.condition.objectBId))
        {status="手順内のすべての条件に対象を設定してください";Refresh();return;}
        EndTrial();pickTarget=null;
        trialMoveTool = FindFirstObjectByType<MoveTool>();
        PauseTool(trialMoveTool);
        try {trial=new SkillSyncTrialSession(Placed);}
        catch(Exception e) {EndTrial();status="試行を開始できません: "+e.Message;Debug.LogException(e);Refresh();return;}
        logs.Clear();trialTime=0;
        PauseTool(selection);PauseTool(placement);PauseTool(FindFirstObjectByType<RotateTool>());PauseTool(CommandService.I);
        PauseTool(FindFirstObjectByType<SelectionOutline>());
        var first = FindPlaced(Condition.condition.objectAId);
        trialSelected = SelectionService.CanEdit(first) ? trial.Find(first.Id) : null;
        UpdateTrialGizmo();
        if (trialMoveTool != null) trialMoveTool.enabled = true;
        Log("条件の試行を開始");Refresh();
    }
    void PauseTool(Behaviour tool) {if(tool!=null) {pausedTools[tool]=tool.enabled;tool.enabled=false;}}
    void EndTrial()
    {
        if (trialMoveTool != null) trialMoveTool.EndPreview();
        trialMoveTool=null;trialSelected=null;
        trial?.Dispose();trial=null;
        foreach(var p in pausedTools) if(p.Key!=null) p.Key.enabled=p.Value;
        pausedTools.Clear();
    }
    void UpdateTrialGizmo()
    {
        if (trialMoveTool != null && trial != null)
            trialMoveTool.SetPreviewTarget(trialSelected, !trial.Paused && !trial.Complete);
    }
    void ResetTrial()
    {
        if (trial == null) return;
        if (trialMoveTool != null) trialMoveTool.SetPreviewTarget(null, false);
        trial.Reset();
        UpdateTrialGizmo();
    }
    void SelectTrialObject()
    {
        if(trial.Paused || trial.Complete || !EditInput.LeftPressedThisFrame()) return;
        if(trialMoveTool != null && trialMoveTool.ShouldConsumeSelectionClick()) return;
        var ray=cameraView.ScreenPointToRay(EditInput.MousePosition);
        trialSelected=null;float nearest=float.PositiveInfinity;
        foreach(var p in Placed)
        {
            if(!SelectionService.CanEdit(p)) continue;
            var candidate=trial.Find(p.Id);
            if(candidate!=null && PlacedObjectGrounding.TryGetRendererBounds(candidate,out var bounds) && bounds.IntersectRay(ray,out float distance) && distance<nearest)
            {nearest=distance;trialSelected=candidate;}
        }
        UpdateTrialGizmo();
    }
    void Log(string message) {logs.Add(TimeSpan.FromSeconds(trialTime).ToString(@"mm\:ss")+" "+message);if(logs.Count>2) logs.RemoveAt(0);}
    void RefreshTrial()
    {
        view.Show(trial.Complete?4:2);
        int index=graph.GetDisplayOrderedSteps().FindIndex(s=>s.nodeId==stepId)+1;
        view.Text("trialStep",$"手順{index} "+(trial.Complete?"完了":"実行中"));
        view.Text("trialBody",Step?.step.body??"");view.Text("trialBody2","");
        view.Text("learner",trial.Complete?"条件を満たしました。次の手順を確認できます。":Step?.step.body??"");
        view.Text("distanceValue",(trial.Distance*100).ToString("0"));view.Text("distanceLimit",$"cm / {DistanceLimit()*100:0.##} cm以内");
        view.Text("distanceStatus",trial.InRange?"✓ 距離の条件を満たしています":"距離の条件を確認しています");
        view.Text("holdValue",trial.HeldSeconds.ToString("0.0"));view.Text("holdLimit",$"秒 / {HoldLimit():0.0}秒");
        view.Text("holdStatus",trial.Complete?"✓ 保持時間の条件を満たしました":trial.Paused?"一時停止中":"保持時間を計測中");
        view.Text("trialNote",trial.Complete?$"{DistanceLimit()*100:0.##} cm以内に{HoldLimit():0.##}秒間とどまり、":"範囲から離れると、保持時間は");
        view.Text("trialNote2",trial.Complete?"この条件が成立しました。":"0秒から数え直します。");
        view.Text("log1",logs.Count>0?logs[0]:"");view.Text("log2",logs.Count>1?logs[1]:"");
        view.holdProgress.rectTransform.sizeDelta=new Vector2(352*(HoldLimit()<=0?trial.Complete?1:0:trial.HeldSeconds/HoldLimit()),8);
        view.holdProgress.color=trial.Complete?new Color32(21,115,74,255):new Color32(8,102,232,255);
        view.viewportDistance.text=$"現在の距離 {trial.Distance*100:0} cm";
        RefreshValues();
        if(trial.Complete) view.Text("status",$"✓ 手順{index}が完了しました");
        var steps=graph.GetDisplayOrderedSteps();
        for(int i=0;i<stepRows.Count && i<steps.Count;i++)
            stepRows[i].Set(steps[i].step.title,steps[i].nodeId==stepId?trial.Complete?"完了":"実行中 · 条件を確認":"待機中",(i+1).ToString(),
                trial.Complete?i==index:steps[i].nodeId==stepId,trial.Complete && steps[i].nodeId==stepId);
    }
    void BeginGhost(PrefabEntry entry)
    {
        EndTrial();CancelGhost();placement.CancelPlacement();
        try {ghost=new SkillSyncPlacementGhost(entry);}
        catch(Exception e) {status="仮配置を開始できません: "+e.Message;Debug.LogException(e);Refresh();return;}
        mode=0;
        ghost.Follow(cameraView,new Vector2(cameraView.pixelRect.center.x,cameraView.pixelRect.center.y));
        RefreshLibrarySelection();Refresh();
    }
    void CancelGhost() {ghost?.Dispose();ghost=null;if(selection!=null) RefreshLibrarySelection();}
    void ConfirmGhost()
    {
        if(ghost==null) return;
        var pose=ghost.Transform;var position=pose.position;var rotation=pose.rotation;var scale=pose.localScale;string label=ghost.DisplayName;
        if(placement.PlaceForDesignUi(ghost.TypeId,position,rotation,scale,label))
        {
            CancelGhost();Refresh();
        }
    }
    void Export()
    {
        if(trial!=null || ghost!=null) return;
        bool saved=project.Save(project.CurrentProjectName,out status);
        validation=graph.ValidateGraph();
        if(!validation.CanExport)
        {
            TopCenterNotification.Ensure(transform, view.inspectorTitle)?.Show("教材を書き出せません: " + ScenarioValidationText.GetFriendlyMessage(validation.errors[0]), true);
            view.Show(5);RefreshValues();
            var issue=validation.errors[0];
            view.Text("errorCount","要修正 "+validation.errors.Count+"件");
            view.Text("errorSummary",ScenarioValidationText.GetFriendlyMessage(issue));
            view.Text("draftStatus",saved?"下書きは保存されています。":"下書きを保存できませんでした。 "+status);
            view.Text("errorTitle",ScenarioValidationText.GetFriendlyMessage(issue));
            view.Text("errorDetail",issue.nodeId??issue.code);
            var fix=view.controls.FirstOrDefault(c=>c.action=="該当箇所を修正");
            if(fix!=null) UnityEngine.EventSystems.EventSystem.current?.SetSelectedGameObject(fix.button.gameObject);
            return;
        }
        try
        {
            var export=graph.BuildScenarioExport();
            string path=TeachingMaterialExportService.Export(export,placement);
            catalogStatus=null;
            status="教材を出力しました: "+path;
            status+=" / XR配布用: "+TeachingMaterialArchive.GetPath(path);
            TopCenterNotification.Ensure(transform, view.inspectorTitle)?.Show("教材を出力しました: "+System.IO.Path.GetDirectoryName(path), false);
        }
        catch(Exception e) {
            status="書き出しに失敗しました: "+e.Message;
            TopCenterNotification.Ensure(transform, view.inspectorTitle)?.Show(status, true);
            Debug.LogException(e);
        }
        RefreshValues();
    }
    void FocusIssue()
    {
        var issue=validation?.errors.FirstOrDefault();var node=graph.FindNode(issue?.nodeId);
        if(node?.nodeType==ScenarioNodeType.Condition) {conditionId=node.nodeId;stepId=graph.GetConditionBoundStepNodeId(node.nodeId);}
        else if(node?.nodeType==ScenarioNodeType.Step) {stepId=node.nodeId;conditionId=null;}
        view.Show(1);mode=1;GraphChanged();
        if(Condition!=null) {pickTarget=string.IsNullOrEmpty(Condition.condition.objectAId)?"A":"B";status="対象を3D空間または配置一覧から選んでください";}
        else {status=issue!=null?ScenarioValidationText.GetFriendlyMessage(issue):"手順を確認してください";view.Focus("stepTitle");}
        RefreshValues();
    }
}
