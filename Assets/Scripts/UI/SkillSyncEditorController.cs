using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

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
    EditModeService subscribedModes;
    Camera cameraView;
    Rect previousCameraRect;
    Color previousCameraColor;
    string stepId, conditionId, search = "", category = "すべて", pickTarget;
    string status = "", librarySignature = "", transformTab = "移動";
    int mode;
    int validationErrorCount;
    bool validating;
    readonly HashSet<string> stepsWithIssue = new HashSet<string>();
    float nextRefresh, trialTime;
    bool initialized;
    Transform trialSelected;
    MoveTool trialMoveTool;
    SkillSyncTrialSession trial;
    SkillSyncPlacementGhost ghost;
    PrefabEntry ghostEntry;
    readonly List<string> logs = new();
    readonly SkillSyncModelThumbnails thumbnails = new();
    readonly List<SkillSyncDesignRow> libraryRows = new(), stepRows = new(), objectRows = new();
    readonly Dictionary<Behaviour,bool> pausedTools = new();
    readonly Dictionary<object, SkillSyncDesignRow> placedRows = new();
    readonly HashSet<PlacedObject> expandedParts = new();
    readonly HashSet<string> collapsedGroups = new();
    readonly SkillSyncRightClick rightClick = new();
    SkillSyncInspectorExtras extras;
    SkillSyncSelectionInfo selectionInfo = new();
    readonly HashSet<SkillSyncDesignRow> expanderBound = new();
    readonly HashSet<PlacedObject> thumbnailTried = new();
    string partsFilter = "", objectCountText = "";
    readonly Dictionary<SkillSyncDesignRow, string> libraryTypes = new();
    readonly Dictionary<string, GameObject> thumbnailSources = new();
    readonly List<Renderer> labelRenderers = new();
    SkillSyncDesignRow ghostRow;
    string armedKey, scrolledStepId;
    float armedUntil;
    bool scrollObjectsPending;
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
        view.EnsureStepControls();
        SkillSyncConditionEditorView.Ensure(view);
        view.EnsureViewportLabels();
        TopCenterNotification.Ensure(transform, view.inspectorTitle);
        extras = new SkillSyncInspectorExtras(view) { scaleEntered = ApplyScale, group = () => GroupSelection(false), ungroup = () => GroupSelection(true) };
        TransformToolSettings.SetTool(transformTab == "回転" ? TransformTool.Rotate : TransformTool.Move);
        cameraView = EditWorkspace.ResolveCamera();
        if(view.orientation!=null) view.orientation.sourceCamera=cameraView;
        cameraController = FindFirstObjectByType<EditorCameraController>();
        if (cameraView != null) { previousCameraRect=cameraView.rect; previousCameraColor=cameraView.backgroundColor; }
        foreach(var control in view.controls)
        {
            var action=control.action;
            control.button.onClick.AddListener(()=>Act(action));
        }
        var tryControl=view.controls.FirstOrDefault(c=>c.action=="▶ この条件を試す");
        var tryLabel=tryControl?.button!=null?tryControl.button.GetComponentInChildren<TMP_Text>(true):null;
        if(tryLabel!=null) tryLabel.text="▶ この手順を試す";
        if (view.conditionTypeDropdown != null)
            view.conditionTypeDropdown.onValueChanged.AddListener(index =>
            {
                string type = SkillSyncConditionEditorView.TypeForDropdownIndex(view.conditionTypeDropdown, index);
                if (type != null) Act(SkillSyncConditionEditing.TypeActionPrefix + type);
                else Refresh();
            });
        foreach(var field in view.fields)
        {
            var role=field.role;
            field.input.onEndEdit.AddListener(value=>Edit(role,value));
            if(role=="search") field.input.onValueChanged.AddListener(value=>{search=value;RebuildLibrary();});
        }
        var partsSearch=SkillSyncPartsSearch.Ensure(view.objectsContent.parent.parent as RectTransform,view.objectTemplate.title.font);
        if(partsSearch!=null) partsSearch.onValueChanged.AddListener(value=>{partsFilter=value??"";RebuildObjects();});
        view.objectsContent.GetComponentInParent<ScrollRect>()?.onValueChanged.AddListener(_=>ShowVisiblePartThumbnails());
        graph.GraphChanged += GraphChanged;
        selection.OnSelectionChanged += SelectionChanged;
        selection.OperationMessage += SelectionMessage;
        selection.DeleteGuard = ConfirmDeleteObjects;
        placement.ObjectPlaced += ObjectPlaced;
        project.StatusChanged += ProjectStatus;
        if(CommandService.I != null) CommandService.I.Stack.HistoryChanged += HistoryChanged;
        if(EditModeService.I != null) {subscribedModes=EditModeService.I;subscribedModes.ModeChanged += EditModeChanged;}
        initialized=true;
        if(EditModeService.I!=null && EditModeService.I.Mode==EditMode.Browse) EditModeService.I.SetMode(EditMode.Transform);
        GraphChanged();RebuildLibrary();Refresh();GetComponent<CanvasGroup>().alpha=1;
        if (!string.IsNullOrEmpty(project.RecoveryProtectionWarning))
            EditorProjectPanel.Ensure(transform.root)?.ShowRecoveryProtectionWarning();
    }
    void OnDisable()
    {
        EndTrial(); CancelGhost();
        EditWorkspace.TransformShortcutsSuppressed=false;
        if(graph!=null) graph.GraphChanged-=GraphChanged;
        if(selection!=null) {selection.OnSelectionChanged-=SelectionChanged;selection.OperationMessage-=SelectionMessage;selection.DeleteGuard=null;}
        SkillSyncContextMenu.Close();
        if(placement!=null) placement.ObjectPlaced-=ObjectPlaced;
        if(project!=null) project.StatusChanged-=ProjectStatus;
        if(catalog!=null) catalog.StatusChanged-=CatalogStatus;
        if(CommandService.I!=null) CommandService.I.Stack.HistoryChanged-=HistoryChanged;
        if(subscribedModes!=null) subscribedModes.ModeChanged-=EditModeChanged;
        if(cameraView!=null) {cameraView.rect=previousCameraRect;cameraView.backgroundColor=previousCameraColor;}
        if(Active==this) Active=null;
        thumbnails.Dispose();
    }
    // Camera variant of BlocksWorkspace: ghost placement and target picking must not stop camera navigation.
    public bool BlocksCamera(Vector2 point)
    {
        if (!Application.isFocused || EditWorkspace.HasOpenModal) return true;
        if(!initialized) return false;
        if(GetComponent<SkillSyncObjectPicker>()?.BlocksPointer(point)==true) return true;
        if(Modal) return true;
        return !RectTransformUtility.RectangleContainsScreenPoint(view.viewport,point);
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
        EditWorkspace.TransformShortcutsSuppressed=mode==1 && trial==null;
        var expansion=GetComponent<SkillSyncWorkspaceExpansion>();
        if(expansion!=null) {
            // UD-4 option A: keep the camera rect fixed so selecting a part never opens/closes the right panel.
            // Flip back to "mode==0 && !Modal && trial==null && ghost==null && selection.Selected.Count!=1" to restore auto-expand.
            expansion.SetExpanded(false);
            expansion.Tick(Time.unscaledDeltaTime);
        }
        if(!BlocksEditingShortcuts)
        {
            if(EditInput.ProjectShortcutPressedThisFrame(true))
            {
                // Commit a focused input first (fires onEndEdit) so the typed value is saved in the same frame.
                if(TextInputFocused) UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);
                SaveProject(project.CurrentProjectName);
            }
            if(!TextInputFocused && EditInput.ProjectShortcutPressedThisFrame(false)) EditorProjectPanel.Ensure(transform.root)?.OpenForDesignUi();
        }
        bool inside=RectTransformUtility.RectangleContainsScreenPoint(view.viewport,EditInput.MousePosition);
        if(!TextInputFocused && EditInput.CancelPressedThisFrame() && SkillSyncInlineRename.EscapeFrame!=Time.frameCount)
        {
            if(GetComponent<SkillSyncObjectPicker>()?.IsOpen==true) GetComponent<SkillSyncObjectPicker>().Close();
            else if(Modal) {mode=1;view.Show(1);}
            else if(pickTarget!=null) pickTarget=null;
            else if(ghost!=null) CancelGhost();
            else if(trial!=null)
            {
                if(!trial.Paused) {trial.Paused=true;UpdateTrialGizmo();Notify("一時停止中：もう一度Escで編集に戻ります",false);}
                else {EndTrial();mode=1;}
            }
            else selection.Select(null);
            Refresh();
        }
        if(!Modal && inside && !TextInputFocused && GetComponent<SkillSyncObjectPicker>()?.BlocksPointer(EditInput.MousePosition)!=true)
        {
            if(ghost!=null)
            {
                ghost.Follow(cameraView,EditInput.MousePosition);
                if(EditInput.LeftPressedThisFrame()) ConfirmGhost(EditInput.ShiftPressed());
            }
            else if(pickTarget!=null)
            {
                var candidate=ScreenPickCandidate();
                GetComponent<SkillSyncObjectPicker>()?.ShowCandidate(candidate);
                if(candidate!=null && EditInput.LeftPressedThisFrame()) Pick(candidate.Id);
            }
            else if(trial!=null) SelectTrialObject();
        }
        else if(pickTarget!=null) GetComponent<SkillSyncObjectPicker>()?.ClearPreview();
        if(mode==0 && trial==null && rightClick.Tick(!Modal && !TextInputFocused && !BlocksWorkspace(EditInput.MousePosition),out var menuPoint)) OpenViewportMenu(menuPoint);
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
        if(action.StartsWith(SkillSyncConditionEditing.TypeActionPrefix,StringComparison.Ordinal))
        {
            if(trial!=null || Condition==null) return;
            string type=action.Substring(SkillSyncConditionEditing.TypeActionPrefix.Length);
            pickTarget=null;
            graph.UpdateConditionData(Condition.nodeId,"Set condition type",data=>
                SkillSyncConditionEditing.ChangeType(data,type,graph.curriculum.rules));
            Refresh();return;
        }
        switch(action)
        {
            case "配置を編集": EndTrial();CancelGhost();mode=0;pickTarget=null;EnsureToolForSelection();break;
            case "手順を編集": EndTrial();CancelGhost();mode=1;pickTarget=null;break;
            case "動作を確認": case "▶ この条件を試す": BeginTrial();return;
            case "↶ 元に戻す": if(trial==null) {var stack=CommandService.I?.Stack;string label=stack!=null?stack.UndoLabel:"";if(stack!=null && stack.CanUndo) {stack.Undo();Notify("元に戻しました："+JapaneseLabel(label),false);}}break;
            case "↷": if(trial==null) {var stack=CommandService.I?.Stack;string label=stack!=null?stack.RedoLabel:"";if(stack!=null && stack.CanRedo) {stack.Redo();Notify("やり直しました："+JapaneseLabel(label),false);}}break;
            case "全体を見る": cameraController?.ResetToDefaultView();break;
            case "上から見る": cameraController?.SetViewPreset(EditorCameraController.ViewPreset.Top);break;
            case "選択を表示": cameraController?.FocusSelected();break;
            case "移動": transformTab=action;TransformToolSettings.SetTool(TransformTool.Move);EditModeService.I?.SetMode(EditMode.Transform);break;
            case "回転": transformTab=action;TransformToolSettings.SetTool(TransformTool.Rotate);EditModeService.I?.SetMode(EditMode.Transform);break;
            case "大きさ": transformTab=action;EditModeService.I?.SetMode(EditMode.Scale);break;
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
            case "DeleteStep": DeleteStep();break;
            case "＋ 完了条件を追加": AddCondition();break;
            case "PickA": OpenObjectPicker("A");return;
            case "PickB": OpenObjectPicker("B");return;
            case "Snap":
                EditSnapSettings.Configure(EditSnapSettings.GridSize,EditSnapSettings.RotationDegrees,!EditSnapSettings.Enabled);
                Notify(EditSnapSettings.Enabled?"吸着をオンにしました（"+(EditSnapSettings.GridSize*100).ToString("0.##")+" cm刻み）":"吸着をオフにしました。自由な位置に置けます",false);break;
            case "PreviousCondition": MoveCondition(-1);break;
            case "NextCondition": MoveCondition(1);break;
            case "DeleteCondition": DeleteCondition();break;
            case "詳細設定": catalog?.ShowDesignSettings();return;
            case "ヘルプ": case "Guide": HintPanelController.Ensure(transform.root)?.Show();return;
            case "Save": if(trial!=null || ghost!=null) break; SaveProject(project.CurrentProjectName);break;
            case "教材を読み込む":
                if (!BlocksEditingShortcuts) EditorProjectPanel.Ensure(transform.root)?.OpenForDesignUi();
                return;
            case "↑ 教材を書き出す": Export();return;
            case "編集を続ける": mode=1;view.Show(1);break;
            case "該当箇所を修正": FocusIssue();return;
            case "編集に戻る": EndTrial();mode=1;break;
            case "Ⅱ 一時停止": if(trial!=null) {trial.Paused=!trial.Paused;UpdateTrialGizmo();Notify(trial.Paused?"一時停止中":"試行を再開しました",false);}break;
            case "初期配置に戻す": case "もう一度試す": ResetTrial();logs.Clear();trialTime=0;break;
            case "最初から試す":
                EndTrial();stepId=graph.GetDisplayOrderedSteps().FirstOrDefault()?.nodeId;conditionId=null;GraphChanged();BeginTrial();return;
            case "次の手順を確認": {EndTrial();var before=stepId;NextStep();mode=1;if(stepId!=before) {BeginTrial();return;}break;}
            case "すべて": case "部品": case "環境": category=action;RebuildLibrary();break;
        }
        Refresh();
    }
    void Edit(string role,string value)
    {
        if(trial!=null || Modal) return;
        if(role=="search") return;
        if(role=="project")
        {
            if(string.IsNullOrWhiteSpace(value)) {}
            else if(ProjectNameCollides(value.Trim())) {Notify("同じ名前の教材があります。別の名前にするか、教材一覧から上書きしてください",true);view.Value("project",project.CurrentProjectName);return;}
            else SaveProject(value.Trim());
        }
        else if(role=="name")
        {
            if(ghost!=null) ghost.DisplayName=value;
            else if(selection.Current!=null && !selectionInfo.multiple)
            {
                PlacedObjectMetadataService.SetDisplayName(selection.Current, value);
            }
        }
        else if(role=="height" || role=="angle")
        {
            if(!Number(value,out float n)) {Notify("数値を入力してください",true);Refresh();return;}
            Transform target=ghost!=null?ghost.Transform:selection.Current!=null?selection.Current.transform:null;
            if(target!=null && ghost==null && selectionInfo.multiple) ApplyRelative(role,n);
            else if(target!=null)
            {
                if(role=="height" && (n<0f || n>300f)) {n=Mathf.Clamp(n,0f,300f);Notify("高さは0〜300 cmの範囲に丸めました",false);}
                var session=ghost==null?new SelectionTransformSession(selection):null;
                if(ghost!=null)
                {
                    if(role=="height") ghost.SetHeight(n/100f);
                    else {var e=target.eulerAngles;target.rotation=Quaternion.Euler(e.x,n,e.z);}
                }
                else foreach(var item in selection.Selected.ToArray())
                {
                    if(item==null || !SelectionService.CanEdit(item)) continue;
                    var t=item.transform;
                    if(role=="height") t.position=new Vector3(t.position.x,n/100f,t.position.z);
                    else {var e=t.eulerAngles;t.rotation=Quaternion.Euler(e.x,n,e.z);}
                }
                session?.Commit("Change object transform");
            }
        }
        else if(role=="stepTitle" || role=="body")
        {
            if(Step!=null) graph.UpdateStepData(Step.nodeId,"Edit step",data=>{if(role=="stepTitle") data.title=value;else data.body=value;});
        }
        else if(role=="distance" || role=="hold")
        {
            if(Condition!=null)
            {
                bool accepted=false,clamped=false;int slot=role=="distance"?0:1;
                graph.UpdateConditionData(Condition.nodeId,"Edit condition",data=>
                    accepted=SkillSyncConditionEditing.TrySetValue(data,slot,value,out clamped));
                if(!accepted) Notify("条件の許容範囲内の数値を入力してください",true);
                else if(clamped) Notify("許容範囲内に丸めました（"+SkillSyncConditionEditing.RangeHint(Condition.condition,slot)+"）",false);
            }
        }
        Refresh();
    }
    // Several parts selected: the height/angle fields say "by how much", and the whole selection moves as one.
    void ApplyRelative(string role,float amount)
    {
        var primary=selection.Current;
        if(primary==null || !SelectionService.CanEdit(primary) || Mathf.Abs(amount)<0.0001f) return;
        var session=new SelectionTransformSession(selection);
        if(role=="height") primary.transform.position+=Vector3.up*(amount/100f);
        else
        {
            Bounds all=default;bool found=false;
            foreach(var item in selection.Selected)
            {
                if(!SelectionService.CanEdit(item) || !SelectionRegionUtility.TryGetBounds(item,out var itemBounds)) continue;
                if(found) all.Encapsulate(itemBounds);else {all=itemBounds;found=true;}
            }
            primary.transform.RotateAround(found?all.center:primary.transform.position,Vector3.up,amount);
        }
        session.Commit(role=="height"?"Move selection":"Rotate selection");
        Notify(role=="height"?"選んだ部品をまとめて"+(amount>0?"上へ ":"下へ ")+Mathf.Abs(amount).ToString("0.##")+" cm動かしました":"選んだ部品をまとめて"+amount.ToString("0.##")+"°回しました",false);
    }
    float BaseScale(PlacedObject part)
    {
        // 100 % is the size the library part had when it was placed.
        float scale=1f;
        if(part!=null && part.modelRoot==null && thumbnailSources.TryGetValue(part.TypeId??"",out var prefab) && prefab!=null) scale=prefab.transform.localScale.x;
        return Mathf.Abs(scale)<0.0001f?1f:scale;
    }
    string ScaleText(PlacedObject part)
    {
        if(part==null) return "";
        if(selectionInfo.multiple) return "100";
        return (Mathf.Round(part.transform.localScale.x/BaseScale(part)*1000f)/10f).ToString("0.#");
    }
    // The 大きさ tab's % field: one part is set to that size, several parts are scaled together by that factor.
    void ApplyScale(string value)
    {
        var primary=selection!=null?selection.Current:null;
        value=(value??"").Trim().TrimEnd('%','％').Trim();
        if(trial!=null || ghost!=null || primary==null || !SelectionService.CanEdit(primary) || value.Length==0) {RefreshValues();return;}
        if(!Number(value,out float percent)) {Notify("数値を入力してください",true);RefreshValues();return;}
        bool together=selectionInfo.multiple;
        float clamped=Mathf.Clamp(percent,10f,1000f);
        if(!Mathf.Approximately(clamped,percent)) Notify("大きさは10〜1000 %の範囲に丸めました",false);
        percent=clamped;
        var t=primary.transform;
        float currentPercent=together?100f:t.localScale.x/BaseScale(primary)*100f;
        if(Mathf.Abs(currentPercent)<0.01f) {RefreshValues();return;}
        float ratio=percent/currentPercent;
        if(Mathf.Abs(ratio-1f)<0.0001f) {RefreshValues();return;}
        var session=new SelectionTransformSession(selection);
        Vector3 before=BoundsCenter(primary);
        t.localScale*=ratio;
        // Scale around the middle of the part, like dragging a corner does.
        if(TransformToolSettings.PivotMode==TransformPivotMode.Center) t.position+=before-BoundsCenter(primary);
        session.Commit("Scale selection");
        Notify(together?"選んだ部品をまとめて"+percent.ToString("0.#")+" %にしました":"大きさを "+percent.ToString("0.#")+" %にしました",false);
        Refresh();
    }
    static Vector3 BoundsCenter(PlacedObject part) =>
        SelectionRegionUtility.TryGetBounds(part,out var bounds)?bounds.center:part.transform.position;
    static bool Number(string s,out float value) => float.TryParse(s,NumberStyles.Float,CultureInfo.CurrentCulture,out value) && !float.IsNaN(value) && !float.IsInfinity(value);
    void GroupSelection(bool ungroup)
    {
        if(trial!=null || ghost!=null || Modal) return;
        selection.GroupSelected(ungroup);
        Refresh();
    }
    // Selection feedback (group, copy, paste, overlap) was only meant for a status strip this screen does not show.
    void SelectionMessage(string message)
    {
        if(string.IsNullOrEmpty(message)) return;
        bool problem=message.Contains("できません") || message.Contains("してください") || message.Contains("ありません") || message.Contains("なっていません");
        Notify(message,problem);
    }
    void OpenViewportMenu(Vector2 point)
    {
        if(cameraView==null || selection==null) return;
        if(!PlacedObjectPicker.TryPick(cameraView.ScreenPointToRay(point),selection.pickMask,out var picked,out _,out _)) return;
        if(!selection.Selected.Any(item=>item==picked || picked.transform.IsChildOf(item.transform))) selection.SelectPicked(picked);
        ShowSelectionMenu(point,()=>view.Focus("name"));
    }
    void OpenRowMenu(SkillSyncDesignRow row,SkillSyncPartsTree.Row data,Vector2 point)
    {
        if(trial!=null || ghost!=null || pickTarget!=null || Modal || data.part==null) return;
        bool selected=data.IsGroup?selection.Selected.Count(item=>item.editorGroupId==data.groupId)>=2:selection.Contains(data.part);
        if(!selected) selection.SelectFromList(data.part,false,false,OrderedPlaced());
        var part=data.part;
        ShowSelectionMenu(point,data.IsGroup?(Action)null:()=>BeginRename(row,part));
    }
    void ShowSelectionMenu(Vector2 point,Action rename)
    {
        var info=SkillSyncSelectionInfo.Describe(selection);
        if(info.count==0) return;
        var items=new List<SkillSyncContextMenu.Item>();
        if(rename!=null && !info.multiple) items.Add(new SkillSyncContextMenu.Item{label="名前を変更",action=rename});
        items.Add(new SkillSyncContextMenu.Item{label="複製",action=()=>Act("複製")});
        if(info.canGroup) items.Add(new SkillSyncContextMenu.Item{label="グループにする",action=()=>GroupSelection(false)});
        if(info.canUngroup) items.Add(new SkillSyncContextMenu.Item{label="グループを解除",action=()=>GroupSelection(true)});
        items.Add(new SkillSyncContextMenu.Item{label="選択部分を表示",action=()=>Act("選択を表示")});
        items.Add(new SkillSyncContextMenu.Item{label="削除",action=()=>Act("削除"),danger=true});
        SkillSyncContextMenu.Show(transform,view.objectTemplate.title.font,point,items);
    }
    void BeginRename(SkillSyncDesignRow row,PlacedObject part)
    {
        if(trial!=null || ghost!=null || pickTarget!=null || part==null || row==null) return;
        SkillSyncInlineRename.Begin(row,part.GetDisplayName(),name=>
        {
            if(part==null) return;
            PlacedObjectMetadataService.SetDisplayName(part,name);
            Notify("名前を「"+name+"」に変えました",false);
            Refresh();
        });
    }
    void Nudge(Vector3 direction)
    {
        if(ghost!=null) {ghost.Transform.position+=direction*.1f;return;}
        if(selection.Current==null || !SelectionService.CanEdit(selection.Current)) return;
        var session=new SelectionTransformSession(selection);selection.Current.transform.position+=direction*.1f;session.Commit("Move 10 cm");
    }
    void AddStep()
    {
        if(!graph.TryInsertStepWithCondition(stepId, out var step, out var addedCondition, out var reason))
        {
            if(!string.IsNullOrWhiteSpace(reason)) Notify("手順を追加できません: "+reason,true);
            return;
        }
        stepId=step.nodeId;conditionId=addedCondition?.nodeId;
        GraphChanged();view.Focus("stepTitle");
    }
    bool TryLinear(out List<ScenarioNode> steps)
    {
        steps=graph.GetDisplayOrderedSteps();
        if(steps.Count==0) return true;
        if(graph.TryBuildLinearStepSequence(out steps,out var reason)) return true;
        Notify("既存の分岐を保持するため、この並べ替えは利用できません: "+reason,true);return false;
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
        if(Step==null) {Notify("先に手順を追加してください",true);return;}
        if(graph.GetConditionCountForStep(stepId)>=graph.GetMaxConditionsPerStep()) {Notify("条件数の上限です",true);return;}
        if(!graph.TryAddConditionToStep(stepId, out var condition, out var reason))
        {
            if(!string.IsNullOrWhiteSpace(reason)) Notify(reason,true);
            return;
        }
        conditionId=condition.nodeId;
    }
    void DeleteStep()
    {
        if(trial!=null || ghost!=null || Step==null) return;
        var steps=graph.GetDisplayOrderedSteps();
        int index=steps.FindIndex(step=>step.nodeId==stepId);
        if(index<0) return;
        string originalStepId=stepId, originalConditionId=conditionId;
        string nextStepId=index+1<steps.Count?steps[index+1].nodeId:index>0?steps[index-1].nodeId:null;
        string nextConditionId=nextStepId!=null?graph.GetConditionNodesForStep(nextStepId).FirstOrDefault()?.nodeId:null;
        bool removed=graph.TryRemoveLinearStep(stepId,out var reason,applied=>
        {
            if(this==null) return;
            EndTrial();CancelGhost();mode=1;
            GetComponent<SkillSyncObjectPicker>()?.Close();
            pickTarget=null;
            stepId=applied?nextStepId:originalStepId;
            conditionId=applied?nextConditionId:originalConditionId;
        });
        Notify(removed?"手順と所属する完了条件を削除しました":"手順を削除できません: "+reason,!removed);
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
        Notify("完了条件を削除しました",false);
        GraphChanged();
    }
    void OpenObjectPicker(string which)
    {
        if(Condition==null) {Notify("先に完了条件を追加してください",true);Refresh();return;}
        if(which=="B" && !ConditionTypeCatalog.RequiresObjectB(Condition.condition.type)) return;
        var picker=GetComponent<SkillSyncObjectPicker>();
        if(picker==null)return;
        pickTarget=null;picker.SetPicking(null);
        string otherId=which=="A"?Condition.condition.objectBId:Condition.condition.objectAId;
        picker.Show(Placed.Where(p=>p.Id!=otherId).ToArray(),(RectTransform)view.controls.First(c=>c.action=="Pick"+which).button.transform,id=>{pickTarget=which;Pick(id);},()=>{
            pickTarget=which;Notify("対象をビューポートでクリックしてください（Escで取消）",false);Refresh();
        });
    }
    void Pick(string id)
    {
        if(Condition==null) {pickTarget=null;Notify("先に完了条件を追加してください",true);Refresh();return;}
        string which=pickTarget;
        if(which!="A" && (which!="B" || !ConditionTypeCatalog.RequiresObjectB(Condition.condition.type)))
        {pickTarget=null;Refresh();return;}
        if(id==(which=="A"?Condition.condition.objectBId:Condition.condition.objectAId) && !string.IsNullOrEmpty(id))
        {pickTarget=null;Notify("動かすものと同じ部品は選べません",true);Refresh();return;}
        graph.UpdateConditionData(Condition.nodeId,"Choose condition object",data=>
        {
            if(which=="A") data.objectAId=id;else data.objectBId=id;
        });
        pickTarget=null;Notify("対象を設定しました",false);Refresh();
    }
    PlacedObject ScreenPickCandidate()
    {
        if(cameraView==null || Condition==null) return null;
        Physics.SyncTransforms();
        if(!PlacedObjectPicker.TryPick(cameraView.ScreenPointToRay(EditInput.MousePosition),~0,out var picked,out _,out _)) return null;
        string currentId=pickTarget=="A"?Condition.condition.objectAId:Condition.condition.objectBId;
        return PlacedObjectOptionProvider.ScreenCandidate(picked,FindPlaced(currentId));
    }
    void NextStep()
    {
        var next=ScenarioFlow.Next(graph.curriculum,stepId).Where(n=>n.nodeType==ScenarioNodeType.Step).ToList();
        if(next.Count==1) {stepId=next[0].nodeId;conditionId=null;GraphChanged();}
        else Notify(next.Count==0?"次の手順はありません":"分岐先の手順を左の一覧から選んでください",false);
    }
    void GraphChanged()
    {
        var steps=graph.GetDisplayOrderedSteps();
        if(Step==null) stepId=steps.FirstOrDefault()?.nodeId;
        var conditions=Step!=null?graph.GetConditionNodesForStep(stepId):new List<ScenarioNode>();
        if(!conditions.Any(c=>c.nodeId==conditionId)) conditionId=conditions.FirstOrDefault()?.nodeId;
        Revalidate();RebuildSteps();Refresh();
    }
    // The footer is hidden in the fixed UI, so every user-facing status also goes to the top notification.
    void Notify(string message,bool error)
    {
        status=message;
        if(!string.IsNullOrEmpty(message)) TopCenterNotification.Ensure(transform,view!=null?view.inspectorTitle:null)?.Show(message,error,error?8f:3f);
    }
    void SaveProject(string projectName)
    {
        if(!project.Save(projectName,out var message)) Notify(message,true);
        else status=message;
    }
    // Cheap enough for change events only; never call per frame.
    void Revalidate()
    {
        if(validating || graph==null) return;
        validating=true;
        try
        {
            validation=graph.ValidateGraph();
            validationErrorCount=validation.errors.Count;
            stepsWithIssue.Clear();
            foreach(var issue in validation.errors)
            {
                var node=graph.FindNode(issue.nodeId);
                if(node==null) continue;
                if(node.nodeType==ScenarioNodeType.Step) stepsWithIssue.Add(node.nodeId);
                else if(node.nodeType==ScenarioNodeType.Condition)
                {
                    string owner=graph.GetConditionBoundStepNodeId(node.nodeId);
                    if(!string.IsNullOrEmpty(owner)) stepsWithIssue.Add(owner);
                }
            }
        }
        finally {validating=false;}
    }
    // Object deletion and undo change validity without a graph event, so rebuild the step rows only when the result changed.
    void RevalidateAndSync()
    {
        string before=validationErrorCount+"|"+string.Join(",",stepsWithIssue.OrderBy(i=>i,StringComparer.Ordinal));
        Revalidate();
        string after=validationErrorCount+"|"+string.Join(",",stepsWithIssue.OrderBy(i=>i,StringComparer.Ordinal));
        if(before!=after) RebuildSteps();
    }
    string ValidationSummary() => validationErrorCount>0?"エラー"+validationErrorCount+"件":"問題なし";
    // Two-step confirm: the first press arms a 4 s window and the second press inside it proceeds.
    bool ConfirmTwice(string key,string message)
    {
        if(armedKey==key && Time.unscaledTime<armedUntil) {armedKey=null;return true;}
        armedKey=key;armedUntil=Time.unscaledTime+4f;
        Notify(message,true);return false;
    }
    bool ConfirmDeleteObjects(IReadOnlyList<PlacedObject> items)
    {
        if(graph==null) return true;
        var ids=new HashSet<string>(items.Select(p=>p.Id).Where(id=>!string.IsNullOrEmpty(id)));
        var steps=graph.GetDisplayOrderedSteps();var used=new List<int>();
        for(int i=0;i<steps.Count;i++)
            if(graph.GetConditionNodesForStep(steps[i].nodeId).Any(c=>ids.Contains(c.condition.objectAId) || ids.Contains(c.condition.objectBId))) used.Add(i+1);
        if(used.Count==0) return true;
        string key="obj:"+string.Join(",",ids.OrderBy(i=>i,StringComparer.Ordinal));
        return ConfirmTwice(key,string.Join("・",used.Select(n=>"手順"+n))+"の完了条件で使われています。もう一度削除すると削除します");
    }
    static bool CmdOrCtrl() => Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl) || Input.GetKey(KeyCode.LeftCommand) || Input.GetKey(KeyCode.RightCommand);
    // Brings the row into view only when it is outside the viewport; never fights manual scrolling.
    static void ScrollIntoView(RectTransform content,RectTransform row)
    {
        if(content==null || row==null) return;
        var scroll=content.GetComponentInParent<ScrollRect>();
        if(scroll==null) return;
        var viewport=scroll.viewport!=null?scroll.viewport:(RectTransform)scroll.transform;
        LayoutRebuilder.ForceRebuildLayoutImmediate(content);
        float scrollable=content.rect.height-viewport.rect.height;
        if(scrollable<=0f) return;
        var bounds=RectTransformUtility.CalculateRelativeRectTransformBounds(content,row);
        float top=content.rect.yMax-bounds.max.y,bottom=content.rect.yMax-bounds.min.y;
        float offset=(1f-scroll.verticalNormalizedPosition)*scrollable;
        if(top>=offset && bottom<=offset+viewport.rect.height) return;
        float target=Mathf.Clamp(top-(viewport.rect.height-(bottom-top))*.5f,0f,scrollable);
        scroll.verticalNormalizedPosition=Mathf.Clamp01(1f-target/scrollable);
    }
    void SelectionChanged(PlacedObject current) {EnsureToolForSelection();ExpandAncestors(current);scrollObjectsPending=true;RefreshLibrarySelection();RebuildObjects();RefreshValues();}
    // The right panel shows a tool tab as pressed, so selecting must always arm that tool (gizmo and key nudges).
    void EnsureToolForSelection()
    {
        var modes=EditModeService.I;
        if(modes==null || selection==null || selection.Current==null || mode!=0 || ghost!=null || trial!=null || modes.Mode==EditMode.Place) return;
        modes.SetMode(transformTab=="大きさ"?EditMode.Scale:EditMode.Transform);
    }
    void RefreshLibrarySelection()
    {
        string type = ghost != null ? ghost.TypeId : null;
        foreach (var pair in libraryTypes) pair.Key.SetSelected(type != null && pair.Value == type);
    }
    void ObjectPlaced(PlacedObject _,string __) {RevalidateAndSync();RebuildObjects();RefreshValues();}
    void HistoryChanged() {RevalidateAndSync();RebuildObjects();RefreshValues();}
    void ProjectStatus(string message,bool success) {if(!success && !string.IsNullOrEmpty(message)) Notify(message,true);else status=message;RefreshValues();}
    void CatalogStatus(string message)
    {
        if(string.IsNullOrEmpty(message)) return;
        bool error=message.Contains("失敗")||message.Contains("エラー")||message.Contains("できません")||message.Contains("見つかりません")||message.Contains("不正")||message.Contains("無効")||message.Contains("Error")||message.Contains("error");
        Notify(message,error);
        RefreshValues();
    }
    void EditModeChanged(EditMode m)
    {
        if(m==EditMode.Scale) transformTab="大きさ";
        else if(m==EditMode.Transform && transformTab=="大きさ") {transformTab="移動";TransformToolSettings.SetTool(TransformTool.Move);}
        RefreshValues();
    }
    bool ProjectNameCollides(string name)
    {
        string safe=ExportFileNameUtility.SanitizeProjectName(name,"VRCourseEditor");
        var current=project.CurrentProjectPath;
        foreach(var info in EditorProjectStore.ListProjects())
        {
            if(!string.Equals(info.DisplayName,safe,StringComparison.OrdinalIgnoreCase)) continue;
            if(!string.IsNullOrEmpty(current) && string.Equals(info.Path,current,StringComparison.OrdinalIgnoreCase)) continue;
            return true;
        }
        return false;
    }
    static string JapaneseLabel(string label)
    {
        if(string.IsNullOrEmpty(label)) return "操作";
        if(label.StartsWith("Move ")) return "移動";
        switch(label)
        {
            case "Edit step": return "手順の編集";
            case "Add step": return "手順の追加";
            case "Delete step": return "手順の削除";
            case "Edit condition": return "完了条件の編集";
            case "Set condition type": return "完了条件の種類の変更";
            case "Change object transform": return "部品の位置・向きの変更";
        }
        return "操作";
    }
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
            var row=Row(view.libraryTemplate,view.libraryContent,libraryRows);row.Set(title,detail,"",ghost!=null && ghost.TypeId==entry.typeId);
            if(row.thumbnail!=null) row.thumbnail.texture=thumbnails.Get(entry.prefab);
            libraryTypes[row]=entry.typeId;
            var chosen=entry;row.button.onClick.AddListener(()=>BeginGhost(chosen));
            var remove=row.GetComponent<SkillSyncLibraryRemove>();
            if(remove!=null) remove.remove.onClick.AddListener(()=>{
                if(!ConfirmTwice("lib:"+chosen.typeId,"もう一度押すとライブラリから外します（配置済みの部品は残ります）")) return;
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
            bool hasIssue=stepsWithIssue.Contains(step.nodeId);
            row.Set(hasIssue?"⚠ "+step.step.title+"（要修正）":step.step.title,step.step.body,(i+1).ToString(),step.nodeId==stepId,!hasIssue);
            row.button.onClick.AddListener(()=>{EndTrial();stepId=step.nodeId;conditionId=null;mode=1;GraphChanged();});
        }
        if(stepId!=scrolledStepId)
        {
            scrolledStepId=stepId;
            int at=steps.FindIndex(s=>s.nodeId==stepId);
            if(at>=0 && at<stepRows.Count) ScrollIntoView(view.stepsContent,(RectTransform)stepRows[at].transform);
        }
    }
    void RebuildObjects()
    {
        if(view==null) return;
        // Placement roots are listed first; parts inside a model are nested and collapsed until opened.
        // Only visible rows exist, so large models do not instantiate hundreds of rows.
        var rowsData=SkillSyncPartsTree.Build(Placed,expandedParts,collapsedGroups,partsFilter,out int rootCount,out int matchCount);
        objectCountText=string.IsNullOrWhiteSpace(partsFilter)?rootCount+"件":matchCount+"件が一致";
        var visible=new HashSet<object>(rowsData.Select(r=>r.Key));
        foreach (var removed in placedRows.Keys.Where(p => p == null || (p is PlacedObject placedKey && placedKey == null) || !visible.Contains(p)).ToArray())
        {
            var row = placedRows[removed];
            objectRows.Remove(row); row.gameObject.SetActive(false); Destroy(row.gameObject);
            placedRows.Remove(removed);
            if(removed is PlacedObject removedPart && removedPart!=null) thumbnailTried.Remove(removedPart);
        }
        if (ghost == null && ghostRow != null)
        {
            objectRows.Remove(ghostRow); ghostRow.gameObject.SetActive(false); Destroy(ghostRow.gameObject); ghostRow = null;
        }
        int offset=0;
        if(ghost!=null)
        {
            if (ghostRow == null) ghostRow=Row(view.objectTemplate,view.objectsContent,objectRows);
            ghostRow.transform.SetAsFirstSibling();
            ghostRow.SetTree(0,false,false);
            ghostRow.Set(ghost.DisplayName+"（仮配置）","","",true);
            ghostRow.status.text="未確定";
            ghostRow.thumbnail.texture=thumbnails.Get(ghost.Transform.gameObject);
            offset=1;
        }
        for(int i=0;i<rowsData.Count;i++)
        {
            var data=rowsData[i];var p=data.part;
            if (!placedRows.TryGetValue(data.Key, out var row))
            {
                row=Row(view.objectTemplate,view.objectsContent,objectRows); placedRows.Add(data.Key,row);
                if(row.thumbnail!=null)
                {
                    // Roots share their library thumbnail; parts are drawn only once scrolled into view.
                    if(p.modelRoot==null) row.thumbnail.texture=ObjectThumbnail(p);
                    else row.thumbnail.enabled=false;
                }
                var created=row;var rowData=data;
                row.button.onClick.AddListener(()=>{if(pickTarget!=null) {if(!rowData.IsGroup) Pick(p.Id);} else selection.SelectFromList(p,EditInput.ShiftPressed(),CmdOrCtrl(),OrderedPlaced());});
                var events=row.button.gameObject.AddComponent<SkillSyncRowEvents>();
                events.doubleClicked=()=>{if(!rowData.IsGroup) BeginRename(created,p);};
                events.rightClicked=point=>OpenRowMenu(created,rowData,point);
            }
            row.SetTree(Mathf.Min(data.depth,SkillSyncPartsTree.MaxIndentLevels),data.hasChildren,data.expanded);
            if(row.Expander!=null && !expanderBound.Contains(row))
            {
                expanderBound.Add(row);
                var rowKey=data;
                row.Expander.onClick.AddListener(()=>{if(rowKey.IsGroup) ToggleGroup(rowKey.groupId);else TogglePart(p);});
            }
            if(row.transform.GetSiblingIndex()!=i+offset) row.transform.SetSiblingIndex(i+offset);
            bool rowSelected=data.IsGroup?selection.Selected.Count(item=>item.editorGroupId==data.groupId)>=2:selection.Contains(p);
            row.Set(data.title,data.detail,"",rowSelected);
        }
        expanderBound.RemoveWhere(row=>row==null);
        if(scrollObjectsPending)
        {
            scrollObjectsPending=false;
            if(selection.Current!=null && placedRows.TryGetValue(selection.Current,out var currentRow)) ScrollIntoView(view.objectsContent,(RectTransform)currentRow.transform);
        }
        ShowVisiblePartThumbnails();
    }
    void ToggleGroup(string id)
    {
        if(string.IsNullOrEmpty(id)) return;
        if(!collapsedGroups.Remove(id)) collapsedGroups.Add(id);
        RebuildObjects();
    }
    void TogglePart(PlacedObject part)
    {
        if(part==null) return;
        if(!expandedParts.Remove(part)) expandedParts.Add(part);
        RebuildObjects();
    }
    void ExpandAncestors(PlacedObject part)
    {
        bool changed=false;
        for(var parent=SkillSyncPartsTree.ParentOf(part);parent!=null;parent=SkillSyncPartsTree.ParentOf(parent)) changed|=expandedParts.Add(parent);
        // Picking one member of a folded group opens the group so its row can be seen.
        if(part!=null && !string.IsNullOrEmpty(part.editorGroupId) && selection.Selected.Count==1) changed|=collapsedGroups.Remove(part.editorGroupId);
        if(changed && view!=null) scrollObjectsPending=true;
    }
    // Draws part thumbnails only for rows near the scrolled window (the panel shows about two rows).
    void ShowVisiblePartThumbnails()
    {
        if(view==null || view.objectsContent==null) return;
        var scroll=view.objectsContent.GetComponentInParent<ScrollRect>();
        if(scroll==null || scroll.viewport==null) return;
        var content=view.objectsContent;
        float rowHeight=view.objectTemplate.GetComponent<RectTransform>().sizeDelta.y,spacing=content.GetComponent<VerticalLayoutGroup>()?.spacing??0;
        float step=Mathf.Max(1f,rowHeight+spacing),top=content.anchoredPosition.y,bottom=top+scroll.viewport.rect.height;
        foreach(var pair in placedRows)
        {
            var row=pair.Value;var part=pair.Key as PlacedObject;
            if(row==null || part==null || row.thumbnail==null || row.thumbnail.enabled || thumbnailTried.Contains(part)) continue;
            float rowTop=row.transform.GetSiblingIndex()*step;
            if(rowTop+step<top-step*2 || rowTop>bottom+step*2) continue;
            thumbnailTried.Add(part);
            row.thumbnail.texture=ObjectThumbnail(part);
            row.thumbnail.enabled=row.thumbnail.texture!=null;
        }
    }
    // Row order on screen, so Shift range selection follows the list the user sees.
    List<PlacedObject> OrderedPlaced() => placedRows.Where(pair=>pair.Key is PlacedObject).OrderBy(pair=>pair.Value.transform.GetSiblingIndex()).Select(pair=>(PlacedObject)pair.Key).Where(p=>p!=null).ToList();
    void Refresh()
    {
        if(!initialized) return;
        int state=Modal?5:trial!=null?(trial.Complete?4:2):ghost!=null?3:mode;
        view.Show(state);extras?.BringToFront();RebuildObjects();RefreshValues();
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
        string footerStatus = status;
        view.Text("footer",trial!=null?"試行中の操作は教材の初期配置に保存されません":(string.IsNullOrEmpty(footerStatus)?project.IsDirty?"未保存の変更があります":"✓ すべての変更を保存しました":footerStatus)+" ｜ "+ValidationSummary());
        view.Text("objectCount",objectCountText+(ghost!=null?" ＋ 仮配置1件":""));
        view.Text("stepCount",graph.GetDisplayOrderedSteps().Count+"つの手順 ｜ "+ValidationSummary());
        var current=selection.Current;
        Transform target=ghost!=null?ghost.Transform:current!=null?current.transform:null;
        selectionInfo=SkillSyncSelectionInfo.Describe(selection);
        // Several parts move together, so their height/angle fields start at 0 and mean "by how much".
        bool relative=ghost==null && current!=null && selectionInfo.multiple;
        view.compactSelectionTitle=ghost==null && current!=null && selectionInfo.compactTitle;
        view.Text("selectionTitle",ghost!=null?"置く場所を選ぶ":current!=null?selectionInfo.title:"部品を選択");
        view.Value("name",ghost!=null?ghost.DisplayName:current!=null?(relative?selectionInfo.title:current.GetDisplayName()):"");
        view.Value("height",relative?"0":target!=null?(target.position.y*100).ToString("0.##"):"");
        view.Value("angle",relative?"0":target!=null?target.eulerAngles.y.ToString("0.##"):"");
        int index=graph.GetDisplayOrderedSteps().FindIndex(s=>s.nodeId==stepId)+1;
        view.Text("inspectorLabel",mode==1?Step!=null?"選択中の手順："+index:"手順を選択してください":ghost!=null?"仮配置":current!=null?"選択中":"未選択");
        view.UpdateSelectionHeader();
        view.Value("stepTitle",Step?.step.title??"");view.Value("body",Step?.step.body??"");
        view.Value("distance",SkillSyncConditionEditing.Value(Condition?.condition,0));
        view.Value("hold",SkillSyncConditionEditing.Value(Condition?.condition,1));
        bool requiresB=Condition!=null && ConditionTypeCatalog.RequiresObjectB(Condition.condition.type);
        bool distanceCondition=SkillSyncConditionEditing.SupportsPcTrial(Condition?.condition);
        var a=FindPlaced(Condition?.condition.objectAId);var b=requiresB?FindPlaced(Condition?.condition.objectBId):null;
        // Retain the selected condition, but only show its targets in the step view.
        bool showConditionLabels=mode==1 || trial!=null;
        PositionLabel(view.partALabel,labelTargetA=trial!=null?trial.Find(Condition?.condition.objectAId):ghost!=null?ghost.Transform:showConditionLabels&&a!=null?a.transform:target,
            trial!=null&&a!=null?a.GetDisplayName():ghost!=null?ghost.DisplayName:showConditionLabels&&a!=null?a.GetDisplayName():current!=null?current.GetDisplayName():"");
        PositionLabel(view.partBLabel,labelTargetB=showConditionLabels&&b!=null?b.transform:null,b!=null?b.GetDisplayName():"");
        if(view.objectAThumbnail!=null) {view.objectAThumbnail.texture=ObjectThumbnail(a);view.objectAThumbnail.enabled=a!=null;}
        if(view.objectBThumbnail!=null) {view.objectBThumbnail.texture=ObjectThumbnail(b);view.objectBThumbnail.enabled=b!=null;}
        if(view.conditionOverlay!=null) view.conditionOverlay.Configure(cameraView,
            trial!=null?trial.Find(Condition?.condition.objectAId):mode==1 && distanceCondition && a!=null?a.transform:null,
            trial!=null?trial.Find(Condition?.condition.objectBId):mode==1 && distanceCondition && b!=null?b.transform:null,DistanceLimit(),trial!=null && trial.InRange);
        view.Text("objectA",a!=null?ImportedModelParts.Label(a):"対象を選択してください");
        view.Text("objectB",b!=null?ImportedModelParts.Label(b):"対象を選択してください");
        bool valid=SkillSyncConditionEditing.TargetsValid(Condition?.condition,id=>FindPlaced(id)!=null);
        var conditions=Step!=null?graph.GetConditionNodesForStep(stepId):new List<ScenarioNode>();
        bool canTrial=valid && conditions.Count>0 && conditions.All(c=>SkillSyncConditionEditing.SupportsPcTrial(c.condition) &&
            SkillSyncConditionEditing.TargetsValid(c.condition,id=>FindPlaced(id)!=null));
        int conditionIndex=conditions.FindIndex(c=>c.nodeId==conditionId);
        view.Text("conditionHeading","完了する条件");
        view.Text("conditionCounter",conditions.Count==0?"0件":conditionIndex>=0?$"{conditionIndex+1}/{conditions.Count}":"");
        view.SetVisible("PreviousCondition",conditions.Count>1);
        view.SetVisible("NextCondition",conditions.Count>1);
        view.SetVisible("DeleteCondition",conditionIndex>=0);
        view.Enable("PreviousCondition",conditionIndex>0);
        view.Enable("NextCondition",conditionIndex>=0 && conditionIndex<conditions.Count-1);
        view.Enable("DeleteCondition",conditionIndex>=0);
        view.Enable("DeleteStep",Step!=null && trial==null && ghost==null);
        view.Text("conditionSummary",Step==null?"先に手順を追加してください":Condition==null?"完了条件がありません。下の＋で追加してください":
            SkillSyncConditionEditing.Summary(Condition.condition)+(valid?"":"\n\n⚠ 対象未設定・参照不正"));
        SkillSyncConditionEditorView.Refresh(view,Condition?.condition,canTrial);
        view.Enable("▶ この条件を試す",true);
        view.Enable("動作を確認",true);
        view.Enable("↑ 上へ",index>1);view.Enable("↓ 下へ",index>0 && index<graph.GetDisplayOrderedSteps().Count);
        view.Enable("↶ 元に戻す",trial==null && CommandService.I?.Stack.CanUndo==true);view.Enable("↷",trial==null && CommandService.I?.Stack.CanRedo==true);
        view.Enable("↑ 教材を書き出す",trial==null && ghost==null);
        view.Enable("Save",trial==null && ghost==null);
        view.Enable("教材を読み込む",!BlocksEditingShortcuts);
        view.Text("workspaceHint",trial!=null?"部品を選び、ギズモで操作 / 中・右ドラッグで視点移動":mode==1?$"手順{index}で使う部品を表示":"部品を選んで、作業面に配置します");
        view.Text("status",pickTarget!=null?status:ghost!=null?"＋ 仮配置中：クリックで確定 / Escで中止":trial!=null?trial.Complete?"✓ 条件が成立しました":"▶ 試行中：ギズモで部品を動かして条件を確かめます":current!=null?(selectionInfo.multiple && selectionInfo.title.EndsWith("選択中",StringComparison.Ordinal)?"● "+selectionInfo.title:"● 選択中："+selectionInfo.title)+" | Escで選択解除":"部品を選択してください");
        if(trial==null) view.viewportDistance.text=valid && distanceCondition?$"現在の距離 {Vector3.Distance(SkillSyncTrialSession.Center(a.transform),SkillSyncTrialSession.Center(b.transform))*100:0} cm":"";
        view.viewportDistance.transform.parent.gameObject.SetActive(valid && distanceCondition && (view.State==1 || trial!=null));
        view.viewportGrid.text=$"グリッド {EditSnapSettings.GridSize*100:0.##} cm";
        view.viewportGrid.color=new Color32(88,103,124,255);
        view.Text("viewportMode",mode==1||trial!=null?valid?distanceCondition?"判定範囲を表示中":"操作条件（PC試行は未対応）":"対象を選択してください":EditSnapSettings.ShouldSnap?"作業面に吸着：ON":"作業面に吸着：OFF");
        extras?.Refresh(transformTab,selectionInfo,view.State==0 && ghost==null && trial==null && current!=null,ScaleText(current));
        if(valid && distanceCondition && cameraView!=null)
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
        float lift=host.rect.height>0?host.rect.height:34f;
        if(point.z<=0) {host.gameObject.SetActive(false);return;}
        host.gameObject.SetActive(true);
        var root=(RectTransform)transform;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(root,point,null,out var local);
        host.anchoredPosition=new Vector2(Mathf.Clamp(local.x-root.rect.xMin-host.rect.width/2,288,288+view.viewport.rect.width-host.rect.width),Mathf.Clamp(local.y-root.rect.yMax+lift+16,-670,view.viewport.anchoredPosition.y));
    }
    float DistanceLimit() => Condition!=null?ConditionTypeCatalog.GetNumber(Condition.condition,ConditionTypeCatalog.DistanceKey,.1f):.1f;
    float HoldLimit() => Condition?.condition.type==ConditionTypeCatalog.SnapHold?ConditionTypeCatalog.GetNumber(Condition.condition,ConditionTypeCatalog.HoldSecondsKey,0):0;
    void BeginTrial()
    {
        CancelGhost();
        if(catalog!=null && catalog.IsRestoringModels) {Notify("モデルの復元完了を待ってから試行してください",true);Refresh();return;}
        if(!SkillSyncConditionEditing.TargetsValid(Condition?.condition,id=>FindPlaced(id)!=null))
        {Notify("試す条件の対象を設定してください",true);Refresh();return;}
        var conditions=graph.GetConditionNodesForStep(stepId);
        if(conditions.Any(c=>!SkillSyncConditionEditing.SupportsPcTrial(c.condition)))
        {Notify("この画面で試行できるのは距離・保持条件です。既存の条件データは保持しています。",true);Refresh();return;}
        if(conditions.Any(c=>!SkillSyncConditionEditing.TargetsValid(c.condition,id=>FindPlaced(id)!=null)))
        {Notify("手順内のすべての条件に対象を設定してください",true);Refresh();return;}
        EndTrial();pickTarget=null;
        trialMoveTool = FindFirstObjectByType<MoveTool>();
        PauseTool(trialMoveTool);
        try {trial=new SkillSyncTrialSession(Placed);}
        catch(Exception e) {EndTrial();Notify("試行を開始できません: "+e.Message,true);Debug.LogException(e);Refresh();return;}
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
        bool holds=Condition?.condition.type==ConditionTypeCatalog.SnapHold;
        view.Text("trialNote",holds?trial.Complete?$"{DistanceLimit()*100:0.##} cm以内に{HoldLimit():0.##}秒間とどまり、":"範囲から離れると、保持時間は":
            $"{DistanceLimit()*100:0.##} cm以内に近づけると完了します。");
        view.Text("trialNote2",holds?trial.Complete?"この条件が成立しました。":"0秒から数え直します。":trial.Complete?"この条件が成立しました。":"保持時間の条件はありません。");
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
    // Name shown on the library card; placed parts use it so card and list agree.
    string LibraryTitle(PrefabEntry entry)
    {
        string title=null;
        if(catalog!=null) catalog.TryGetTypeInfo(entry.typeId,out title,out _);
        return string.IsNullOrWhiteSpace(title) || title==entry.typeId ? entry.prefab.name : title.Trim();
    }
    void BeginGhost(PrefabEntry entry)
    {
        if(ghost!=null && ghost.TypeId==entry.typeId) {CancelGhost();Refresh();return;}
        EndTrial();CancelGhost();placement.CancelPlacement();
        try {ghost=new SkillSyncPlacementGhost(entry);ghostEntry=entry;ghost.DisplayName=LibraryTitle(entry);}
        catch(Exception e) {Notify("仮配置を開始できません: "+e.Message,true);Debug.LogException(e);Refresh();return;}
        mode=0;
        ghost.Follow(cameraView,new Vector2(cameraView.pixelRect.center.x,cameraView.pixelRect.center.y));
        RefreshLibrarySelection();Refresh();
    }
    void CancelGhost() {ghost?.Dispose();ghost=null;if(selection!=null) RefreshLibrarySelection();}
    void ConfirmGhost(bool continuePlacing=false)
    {
        if(ghost==null) return;
        var entry=ghostEntry;
        var pose=ghost.Transform;var position=pose.position;var rotation=pose.rotation;var scale=pose.localScale;string label=ghost.DisplayName;
        if(placement.PlaceForDesignUi(ghost.TypeId,position,rotation,scale,label))
        {
            CancelGhost();
            if(continuePlacing && entry!=null) BeginGhost(entry);
            Refresh();
        }
    }
    void Export()
    {
        if(trial!=null || ghost!=null) return;
        var result=TeachingMaterialExportWorkflow.Run(
            (out string message)=>project.Save(project.CurrentProjectName,out message),
            ()=>{validation=graph.ValidateGraph();return validation.CanExport;},
            ()=>TeachingMaterialExportService.Export(graph.BuildScenarioExport(),placement));
        status=result.message;
        if(result.outcome==TeachingMaterialExportWorkflow.Outcome.InvalidGraph)
        {
            TopCenterNotification.Ensure(transform, view.inspectorTitle)?.Show("教材を書き出せません: " + ScenarioValidationText.GetFriendlyMessage(validation.errors[0]), true);
            view.Show(5);RefreshValues();
            var issue=validation.errors[0];
            int shown=Math.Min(5,validation.errors.Count);
            view.Text("errorCount","要修正 "+validation.errors.Count+"件"+(validation.errors.Count>shown?"（ほか "+(validation.errors.Count-shown)+"件）":""));
            view.Text("errorSummary",ScenarioValidationText.GetFriendlyMessage(issue));
            foreach(var v in view.visuals) if((v.role=="errorTitle"||v.role=="errorSummary") && v.label!=null) {v.label.textWrappingMode=TextWrappingModes.Normal;v.label.overflowMode=TextOverflowModes.Ellipsis;}
            view.Text("draftStatus","再編集用の下書きは保存されています。修正後に「教材を書き出す」を再試行してください。");
            view.Text("errorTitle",ScenarioValidationText.GetFriendlyMessage(issue));
            view.Text("errorDetail",string.Join("\n",validation.errors.Take(shown).Select(e=>ErrorLine(e))));
            var fix=view.controls.FirstOrDefault(c=>c.action=="該当箇所を修正");
            if(fix!=null) UnityEngine.EventSystems.EventSystem.current?.SetSelectedGameObject(fix.button.gameObject);
            return;
        }
        if(result.outcome==TeachingMaterialExportWorkflow.Outcome.Exported)
        {
            string dir=System.IO.Path.GetDirectoryName(result.exportPath);
            if(string.IsNullOrEmpty(dir) || !System.IO.Path.IsPathRooted(dir))
                TopCenterNotification.Ensure(transform, view.inspectorTitle)?.Show("再編集用データを保存し、教材を書き出しました", false, 8f);
            else
                TopCenterNotification.Ensure(transform, view.inspectorTitle)?.Show("再編集用データを保存し、教材を書き出しました: "+dir, false, 8f, "フォルダを開く", ()=>Application.OpenURL(new System.Uri(dir).AbsoluteUri));
        }
        else
        {
            TopCenterNotification.Ensure(transform, view.inspectorTitle)?.Show(status, true);
            if(result.exception!=null) Debug.LogException(result.exception);
        }
        RefreshValues();
    }
    string ErrorLine(GraphValidationIssue e)
    {
        string text=ScenarioValidationText.GetFriendlyMessage(e),owner=graph.FindNode(e.nodeId)?.nodeType==ScenarioNodeType.Condition?graph.GetConditionBoundStepNodeId(e.nodeId):e.nodeId;
        int n=graph.GetDisplayOrderedSteps().FindIndex(x=>x.nodeId==owner);
        return n>=0?"手順"+(n+1)+"："+text:text;
    }
    void FocusIssue()
    {
        var issue=validation?.errors.FirstOrDefault();var node=graph.FindNode(issue?.nodeId);
        if(node?.nodeType==ScenarioNodeType.Condition) {conditionId=node.nodeId;stepId=graph.GetConditionBoundStepNodeId(node.nodeId);}
        else if(node?.nodeType==ScenarioNodeType.Step) {stepId=node.nodeId;conditionId=null;}
        view.Show(1);mode=1;GraphChanged();
        switch(issue?.code)
        {
            case "E-08": case "E-10":
                if(Condition!=null) {pickTarget=string.IsNullOrEmpty(Condition.condition.objectAId)?"A":"B";Notify("対象を3D空間または配置一覧から選んでください",false);RefreshValues();return;}
                break;
        }
        if(Condition!=null) {Notify(ScenarioValidationText.GetFriendlyMessage(issue),true);}
        else {Notify(issue!=null?ScenarioValidationText.GetFriendlyMessage(issue):"手順を確認してください",true);view.Focus("stepTitle");}
        RefreshValues();
    }
}
