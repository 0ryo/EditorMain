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
    string nameHint;
    int nameHintTone;
    float nameHintUntil;
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
    readonly Dictionary<GameObject, Color> proxyTints = new();
    static readonly Color[] ProxyPalette = { new Color32(59,130,246,255), new Color32(234,120,60,255), new Color32(34,150,110,255), new Color32(150,100,200,255), new Color32(210,160,40,255), new Color32(90,100,115,255) };
    readonly List<Renderer> labelRenderers = new();
    SkillSyncDesignRow ghostRow;
    SkillSyncViewportGuide guide;
    PrefabEntry dragEntry;
    bool dragAborted, importWatch, objectsDirty;
    HashSet<string> knownLibraryTypes;
    string highlightType, libraryEmptyText = "";
    float highlightUntil, lockedNoticeAt;
    readonly Dictionary<string, List<PlacedObject>> groupMembers = new();
    string scrolledStepId;
    bool scrollObjectsPending;
    Transform labelTargetA, labelTargetB;
    readonly Vector3[] corners = new Vector3[4];
    GraphValidationResult validation;
    // The errors as the user reads them, in step order; the last one jumped to is remembered for "next problem".
    List<ValidationEntry> issues=new();
    readonly Dictionary<string,List<ValidationEntry>> issuesByStep=new();
    readonly Dictionary<string,ValidationEntry> issueByCondition=new();
    string lastIssueKey;int lastIssueOrder=-1;
    // Distance conditions whose limit is smaller than the two parts' sizes allow (condition node id -> message).
    readonly Dictionary<string,string> distanceWarnings = new();
    readonly HashSet<string> stepsWithWarning = new();
    float nextWarningCheck;
    string warningSignature = "";
    bool stepRowsShowTrial, stepRebuildPending, trialCarried;
    string pickConditionId, trialSelectedId;
    SkillSyncStepReorder reorder;
    TMP_Text pauseLabel, deleteConditionLabel;
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
        view.EnsureProjectFileControls();
        view.EnsureConditionControls();
        view.EnsureStepControls();
        view.EnsureWalkthroughControl();
        view.EnsureIssueControls();
        view.SetCaption("該当箇所を修正","最初の問題へ移動");
        if(view.issueList!=null) view.issueList.Selected=SelectIssue;
        SkillSyncConditionEditorView.Ensure(view);
        if(view.conditionList!=null) view.conditionList.Selected=SelectCondition;
        if(view.trialList!=null) view.trialList.ActionClicked=ManualSucceed;
        guide = new SkillSyncViewportGuide(view, view.objectTemplate.title.font) { cancelLoading = () => catalog?.CancelModelImport() };
        // The eye and padlock sit at the right end of a list row, so the status text moves left to make room.
        if (view.objectTemplate.status != null) view.objectTemplate.status.rectTransform.anchoredPosition += Vector2.left * SkillSyncRowToggles.Reserved;
        view.EnsureViewportLabels();
        TopCenterNotification.Ensure(transform, view.inspectorTitle);
        extras = new SkillSyncInspectorExtras(view) { scaleEntered = ApplyScale, group = () => GroupSelection(false), ungroup = () => GroupSelection(true) };
        TransformToolSettings.SetTool(transformTab == "回転" ? TransformTool.Rotate : TransformTool.Move);
        cameraView = EditWorkspace.ResolveCamera();
        if(view.orientation!=null) view.orientation.sourceCamera=cameraView;
        cameraController = FindFirstObjectByType<EditorCameraController>();
        SkillSyncViewportTools.Ensure(view,view.objectTemplate.title.font,selection,cameraController,()=>Act("選択を表示"));
        if (cameraView != null) { previousCameraRect=cameraView.rect; previousCameraColor=cameraView.backgroundColor; }
        foreach(var control in view.controls)
        {
            var action=control.action;
            control.button.onClick.AddListener(()=>Act(action));
        }
        reorder=gameObject.AddComponent<SkillSyncStepReorder>();
        reorder.Setup(view.stepsContent,()=>trial==null && !Modal && ghost==null,MoveStep,()=>{if(stepRebuildPending) {stepRebuildPending=false;RebuildSteps();}});
        pauseLabel=view.controls.FirstOrDefault(c=>c.action=="Ⅱ 一時停止")?.button.GetComponentInChildren<TMP_Text>(true);
        deleteConditionLabel=view.controls.FirstOrDefault(c=>c.action=="DeleteCondition")?.button.GetComponentInChildren<TMP_Text>(true);
        var resetLabel=view.controls.FirstOrDefault(c=>c.action=="初期配置に戻す")?.button.GetComponentInChildren<TMP_Text>(true);
        if(resetLabel!=null) resetLabel.text="手順の最初に戻す";
        var titleField=view.fields.FirstOrDefault(f=>f.role=="stepTitle");
        if(titleField?.input.placeholder is TMP_Text titleHint) titleHint.text="手順の題名（例：ナット1を外す）";
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
            if(role=="project")
            {
                var projectInput=field.input;
                projectInput.onSelect.AddListener(_=>ShowNameHint(projectInput.text));
                projectInput.onValueChanged.AddListener(ShowNameHint);
            }
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
        PlacedObjectEditState.StateChanged += EditStateChanged;
        project.StatusChanged += ProjectStatus;
        EditorCameraController.Notice += CameraNotice;
        if(CommandService.I != null) {CommandService.I.Stack.HistoryChanged += HistoryChanged;CommandService.I.Stack.HistoryApplied += HistoryApplied;}
        if(EditModeService.I != null) {subscribedModes=EditModeService.I;subscribedModes.ModeChanged += EditModeChanged;}
        view.disabledReason=DisabledReason;view.AttachDisabledLooks();
        initialized=true;
        if(EditModeService.I!=null && EditModeService.I.Mode==EditMode.Browse) EditModeService.I.SetMode(EditMode.Transform);
        GraphChanged();RebuildLibrary();Refresh();GetComponent<CanvasGroup>().alpha=1;
        if (!string.IsNullOrEmpty(project.RecoveryProtectionWarning))
            EditorProjectPanel.Ensure(transform.root)?.ShowRecoveryProtectionWarning();
        else SkillSyncStartScreen.ShowAtLaunch(this, project);
    }
    void OnDisable()
    {
        EndTrial(); CancelGhost();
        EditWorkspace.TransformShortcutsSuppressed=false;
        if(graph!=null) graph.GraphChanged-=GraphChanged;
        if(selection!=null) {selection.OnSelectionChanged-=SelectionChanged;selection.OperationMessage-=SelectionMessage;selection.DeleteGuard=null;}
        SkillSyncContextMenu.Close();
        if(placement!=null) placement.ObjectPlaced-=ObjectPlaced;
        PlacedObjectEditState.StateChanged-=EditStateChanged;
        if(project!=null) project.StatusChanged-=ProjectStatus;
        EditorCameraController.Notice-=CameraNotice;
        if(catalog!=null) catalog.StatusChanged-=CatalogStatus;
        if(CommandService.I!=null) {CommandService.I.Stack.HistoryChanged-=HistoryChanged;CommandService.I.Stack.HistoryApplied-=HistoryApplied;}
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
                // Commit a focused input first (fires onEndEdit) so the typed value is applied in the same frame.
                if(TextInputFocused) UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);
                RequestSave();
            }
            else if(EditInput.NewProjectShortcutPressedThisFrame())
            {
                if(TextInputFocused) UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);
                RequestNew();
            }
            if(!TextInputFocused && EditInput.ProjectShortcutPressedThisFrame(false)) EditorProjectPanel.Ensure(transform.root)?.OpenForDesignUi();
        }
        // A target pick belongs to the condition it was started for; any other step or condition cancels it.
        if(pickTarget!=null && conditionId!=pickConditionId) CancelPick();
        bool inside=RectTransformUtility.RectangleContainsScreenPoint(view.viewport,EditInput.MousePosition);
        if(pickTarget!=null && !TextInputFocused && SkillSyncObjectPicker.ConfirmPressedThisFrame()) CommitProvisional();
        if(!TextInputFocused && EditInput.CancelPressedThisFrame() && SkillSyncInlineRename.EscapeFrame!=Time.frameCount)
        {
            if(GetComponent<SkillSyncObjectPicker>()?.IsOpen==true) GetComponent<SkillSyncObjectPicker>().Close();
            else if(Modal) {mode=1;view.Show(1);}
            else if(pickTarget!=null) pickTarget=null;
            else if(ghost!=null) {if(dragEntry!=null) dragAborted=true;CancelGhost();}
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
                // The first click only marks a provisional target; "この対象に決定" (or Enter) commits it.
                var candidate=ScreenPickCandidate();
                var picker=GetComponent<SkillSyncObjectPicker>();
                picker?.ShowCandidate(candidate);
                if(candidate!=null && EditInput.LeftPressedThisFrame()) picker?.SetProvisional(candidate);
            }
            else if(trial!=null) SelectTrialObject();
            else if(mode==0 && EditInput.LeftPressedThisFrame()) NoticeLockedClick();
        }
        else if(pickTarget!=null) GetComponent<SkillSyncObjectPicker>()?.ClearPreview();
        if(mode==0 && trial==null && rightClick.Tick(!Modal && !TextInputFocused && !BlocksWorkspace(EditInput.MousePosition),out var menuPoint)) OpenViewportMenu(menuPoint);
        if(trial!=null && Condition!=null)
        {
            bool before=trial.InRange, completed=trial.Complete;
            if(!trial.Paused) trialTime+=Time.unscaledDeltaTime;
            trial.Tick(graph.GetConditionNodesForStep(stepId),conditionId,Time.unscaledDeltaTime);
            UpdateTrialGizmo();
            if(before!=trial.InRange) Log(trial.InRange?"距離の条件が成立":"範囲外：近づけた時間をリセット");
            if(!completed && trial.Complete) Log("条件が成立しました");
            RefreshTrial();
        }
        if(Time.unscaledTime>=nextRefresh)
        {
            nextRefresh=Time.unscaledTime+.25f;
            // Part sizes change without a graph event, so the distance warnings are polled slowly.
            if(trial==null && ghost==null && Time.unscaledTime>=nextWarningCheck)
            {
                nextWarningCheck=Time.unscaledTime+1f;
                if(RecomputeDistanceWarnings()) RebuildSteps();
            }
            if(dragEntry!=null && !Input.GetMouseButton(0)) {dragEntry=null;dragAborted=false;}
            if(highlightType!=null && Time.unscaledTime>highlightUntil) {highlightType=null;RefreshLibrarySelection();}
            if(objectsDirty) {objectsDirty=false;RebuildObjects();}
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
        LiftDistanceBadge();
    }
    // The "現在の距離" badge sits between the two parts, so it would hide behind a part-name label that is nearby on screen.
    void LiftDistanceBadge()
    {
        var badge=view.viewportDistance!=null?view.viewportDistance.transform.parent as RectTransform:null;
        if(badge==null || !badge.gameObject.activeInHierarchy) return;
        for(int pass=0;pass<2;pass++)
            foreach(var label in new[]{view.partALabel,view.partBLabel})
            {
                var host=label!=null?label.transform.parent as RectTransform:null;
                if(host==null || !host.gameObject.activeInHierarchy) continue;
                var a=WorldRect(badge);var b=WorldRect(host);
                if(!a.Overlaps(b)) continue;
                float lift=(b.yMax-a.yMin+6f)/Mathf.Max(.01f,badge.lossyScale.y);
                float top=view.viewport.anchoredPosition.y;
                badge.anchoredPosition=new Vector2(badge.anchoredPosition.x,Mathf.Min(top,badge.anchoredPosition.y+lift));
            }
    }
    static Rect WorldRect(RectTransform rect)
    {
        rect.GetWorldCorners(worldCorners);
        return Rect.MinMaxRect(worldCorners[0].x,worldCorners[0].y,worldCorners[2].x,worldCorners[2].y);
    }
    static readonly Vector3[] worldCorners=new Vector3[4];
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
        if(action.StartsWith(SkillSyncConditionEditorView.DirectionActionPrefix,StringComparison.Ordinal))
        {
            if(trial!=null || Condition==null || !int.TryParse(action.Substring(SkillSyncConditionEditorView.DirectionActionPrefix.Length),out int direction)) return;
            graph.UpdateConditionData(Condition.nodeId,"Edit condition",data=>SkillSyncConditionEditing.SetChoice(data,direction));
            Refresh();return;
        }
        switch(action)
        {
            case "配置を編集": EndTrial();CancelGhost();mode=0;pickTarget=null;EnsureToolForSelection();break;
            case "手順を編集": EndTrial();CancelGhost();mode=1;pickTarget=null;break;
            case "動作を確認": case "▶ この条件を試す": BeginTrial();return;
            case "↶ 元に戻す": if(trial==null) CommandService.I?.Stack.Undo();break;
            case "↷": if(trial==null) CommandService.I?.Stack.Redo();break;
            case "全体を見る": cameraController?.ShowAll();break;
            case "上から見る": cameraController?.SetViewPreset(EditorCameraController.ViewPreset.Top);break;
            case "選択を表示": cameraController?.FocusSelected();break;
            case "移動": transformTab=action;TransformToolSettings.SetTool(TransformTool.Move);EditModeService.I?.SetMode(EditMode.Transform);break;
            case "回転": transformTab=action;TransformToolSettings.SetTool(TransformTool.Rotate);EditModeService.I?.SetMode(EditMode.Transform);break;
            case "大きさ": transformTab=action;EditModeService.I?.SetMode(EditMode.Scale);break;
            case "←": Nudge(Vector2.left);break;
            case "→": Nudge(Vector2.right);break;
            case "↑": Nudge(Vector2.up);break;
            case "↓": Nudge(Vector2.down);break;
            case "複製": selection.DuplicateSelected();break;
            case "削除": selection.DeleteSelected();break;
            case "キャンセル": CancelGhost();break;
            case "ここに配置": ConfirmGhost();break;
            case "＋ 3Dモデルを読み込む": importWatch=true;catalog?.ImportForDesignUi();return;
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
            case "DeleteCondition": DeleteCondition();break;
            case "詳細設定": catalog?.ShowDesignSettings();return;
            case "通知履歴": TopCenterNotification.Ensure(transform,view.inspectorTitle)?.ToggleHistory();return;
            case "ヘルプ": case "Guide": HintPanelController.Ensure(transform.root)?.Show();return;
            case "保存": case "Save": RequestSave();break;
            case "新規": RequestNew();break;
            case "教材を読み込む": // The caption is "教材を開く"; the action key is kept for the existing control wiring.
                if (!BlocksEditingShortcuts) EditorProjectPanel.Ensure(transform.root)?.OpenForDesignUi();
                return;
            case "↑ 教材を書き出す": Export();return;
            case "編集を続ける": mode=1;view.Show(1);break;
            case "該当箇所を修正": if(issues.Count>0) JumpToIssue(issues[0]);return;
            case "IssueChip": NextIssue();return;
            case "編集に戻る": EndTrial();mode=1;break;
            case "Ⅱ 一時停止": if(trial!=null) {trial.Paused=!trial.Paused;UpdateTrialGizmo();Notify(trial.Paused?"一時停止中です。「再開」で続けます":"再開しました",false);}break;
            case "初期配置に戻す": case "もう一度試す": ResetTrial();logs.Clear();trialTime=0;break;
            case "最初から試す": case "最初から通して確認":
                if(graph.GetDisplayOrderedSteps().Count==0) {Notify("先に手順を追加してください",true);break;}
                EndTrial();pickTarget=null;stepId=graph.GetDisplayOrderedSteps().FirstOrDefault()?.nodeId;conditionId=null;GraphChanged();BeginTrial();return;
            case "次の手順を確認": AdvanceTrial();return;
            case "すべて": case "部品": case "環境": category=action;RebuildLibrary();break;
        }
        Refresh();
    }
    void Edit(string role,string value)
    {
        if(trial!=null || Modal) return;
        if(role=="search") return;
        if(role=="project") CommitProjectName(value);
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
                    if(role=="height") t.position+=Vector3.up*(EditWorkspace.GroundY+n/100f-BottomY(t));
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
    // The height field is the part's lowest point above the floor, so a part resting on the floor reads 0 cm.
    static float BottomY(Transform t) => PlacedObjectGrounding.TryGetRendererBounds(t,out var bounds)?bounds.min.y:t.position.y;
    static string HeightText(Transform t)
    {
        float cm=Mathf.Round((BottomY(t)-EditWorkspace.GroundY)*10000f)/100f;
        if(cm==0f) cm=0f;
        return cm.ToString("0.##");
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
        var ray=cameraView.ScreenPointToRay(point);
        if(!PlacedObjectPicker.TryPick(ray,selection.pickMask,out var picked,out _,out _))
        {
            if(SkillSyncSurfaceProbe.TryFindLocked(ray,out var lockedPart,out _)) ShowBlockedMenu(point,new List<PlacedObject>{lockedPart});
            return;
        }
        if(!selection.Selected.Any(item=>item==picked || picked.transform.IsChildOf(item.transform))) selection.SelectPicked(picked);
        ShowSelectionMenu(point,()=>view.Focus("name"));
    }
    void OpenRowMenu(SkillSyncDesignRow row,SkillSyncPartsTree.Row data,Vector2 point)
    {
        if(trial!=null || ghost!=null || pickTarget!=null || Modal || data.part==null) return;
        var rowTargets=RowTargets(data);
        if(rowTargets.Any(item=>PartState(item,false) || PartState(item,true))) {ShowBlockedMenu(point,rowTargets);return;}
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
        var chosen=selection.Selected.ToList();
        items.Add(new SkillSyncContextMenu.Item{label="隠す",action=()=>SetPartState(chosen,false,true)});
        items.Add(new SkillSyncContextMenu.Item{label="ロック",action=()=>SetPartState(chosen,true,true)});
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
    void Nudge(Vector2 screenDirection)
    {
        var direction=EditorCameraNavigationMath.ScreenDirectionToGroundAxis(cameraView,screenDirection);
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
        int i=steps.FindIndex(s=>s.nodeId==stepId);
        if(i>=0) MoveStep(i,i+offset);
    }
    // One move is one command (one Undo). The moved step becomes the selected one and the list scrolls to it.
    void MoveStep(int from,int to)
    {
        if(trial!=null || ghost!=null || !TryLinear(out var steps)) return;
        if(from<0 || to<0 || from>=steps.Count || to>=steps.Count || from==to) return;
        var moving=steps[from];steps.RemoveAt(from);steps.Insert(to,moving);
        if(stepId!=moving.nodeId) {CancelPick();stepId=moving.nodeId;conditionId=null;}
        scrolledStepId=null;
        if(graph.ReorderLinearSteps(steps.Select(s=>s.nodeId).ToList())) Notify("手順を"+(to+1)+"番目へ移動しました",false);
    }
    void AddCondition()
    {
        if(Step==null) {Notify("先に手順を追加してください",true);return;}
        if(graph.GetConditionCountForStep(stepId)>=graph.GetMaxConditionsPerStep()) {Notify("1つの手順に付けられる条件は最大"+graph.GetMaxConditionsPerStep()+"件です",true);return;}
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
        if(removed) NotifyWithUndo("手順と所属する完了条件を削除しました",originalStepId,originalConditionId);
        else Notify("手順を削除できません: "+reason,true);
    }
    void DeleteCondition(bool confirmed=false)
    {
        var conditions=Step!=null?graph.GetConditionNodesForStep(stepId):new List<ScenarioNode>();
        int index=conditions.FindIndex(c=>c.nodeId==conditionId);
        if(index<0) return;
        string deletingId=conditionId;
        // The last condition leaves the step without one (it cannot be exported); say so before deleting.
        if(conditions.Count==1 && !confirmed)
        {
            SkillSyncConfirmDialog.Show("最後の完了条件を削除しますか？","この手順の完了条件がなくなり、条件を追加するまで書き出せません。「元に戻す」で復元できます。",
                "削除",()=>{if(conditionId==deletingId) DeleteCondition(true);},destructive:true);
            return;
        }
        var remaining=conditions.Where(c=>c.nodeId!=deletingId).ToList();
        string nextId=remaining.Count==0?null:remaining[Mathf.Clamp(index-1,0,remaining.Count-1)].nodeId;
        bool removed=graph.TryRemoveNode(deletingId);
        if(!removed) return;
        string deletedStep=stepId;
        conditionId=nextId;
        GraphChanged();
        NotifyWithUndo(remaining.Count==0?"完了条件を削除しました。この手順は条件を追加するまで書き出せません":"完了条件を削除しました",deletedStep,deletingId);
    }
    // "元に戻す" on a deletion notice undoes that deletion only while nothing else has happened since.
    void NotifyWithUndo(string message,string restoreStepId,string restoreConditionId)
    {
        var stack=CommandService.I?.Stack;
        if(stack==null) {Notify(message,false);return;}
        int count=stack.UndoCount;
        status=message;
        TopCenterNotification.Ensure(transform,view!=null?view.inspectorTitle:null)?.Show(message,false,8f,"元に戻す",()=>
        {
            if(this==null || trial!=null || ghost!=null) return;
            if(stack.UndoCount!=count || !stack.Undo()) {Notify("その後に別の操作をしたため、ここからは戻せません。上の「元に戻す」で順に戻せます",true);return;}
            CancelPick();mode=1;stepId=restoreStepId;conditionId=restoreConditionId;GraphChanged();
            Notify("削除を元に戻しました",false);
        });
    }
    void OpenObjectPicker(string which)
    {
        if(Condition==null) {Notify("先に完了条件を追加してください",true);Refresh();return;}
        if(which=="B" && !ConditionTypeCatalog.RequiresObjectB(Condition.condition.type)) return;
        var picker=GetComponent<SkillSyncObjectPicker>();
        if(picker==null)return;
        pickTarget=null;picker.SetPicking(null);
        string otherId=which=="A"?Condition.condition.objectBId:Condition.condition.objectAId;
        string currentId=which=="A"?Condition.condition.objectAId:Condition.condition.objectBId;
        picker.Show(Placed,(RectTransform)view.controls.First(c=>c.action=="Pick"+which).button.transform,id=>{BeginPick(which);Pick(id);},()=>{
            BeginPick(which);Notify("対象を3D画面でクリックしてください。「この対象に決定」を押すまで確定しません（Escで取消）",false);Refresh();
        },currentId,otherId);
    }
    void BeginPick(string which) {pickTarget=which;pickConditionId=conditionId;}
    void CancelPick()
    {
        if(pickTarget==null) return;
        pickTarget=null;GetComponent<SkillSyncObjectPicker>()?.Close();
    }
    void Pick(string id)
    {
        if(pickTarget!=null && conditionId!=pickConditionId) {CancelPick();Refresh();return;}
        if(Condition==null) {pickTarget=null;Notify("先に完了条件を追加してください",true);Refresh();return;}
        string which=pickTarget;
        if(which!="A" && (which!="B" || !ConditionTypeCatalog.RequiresObjectB(Condition.condition.type)))
        {pickTarget=null;Refresh();return;}
        if(id==(which=="A"?Condition.condition.objectBId:Condition.condition.objectAId) && !string.IsNullOrEmpty(id))
        {pickTarget=null;Notify(which=="A"?"近づける先と同じ部品は選べません":"動かすものと同じ部品は選べません",true);Refresh();return;}
        graph.UpdateConditionData(Condition.nodeId,"Choose condition object",data=>
        {
            if(which=="A") data.objectAId=id;else data.objectBId=id;
        });
        pickTarget=null;GetComponent<SkillSyncObjectPicker>()?.SetPicking(null);Notify("対象を設定しました",false);Refresh();
    }
    void CommitProvisional()
    {
        var picker=GetComponent<SkillSyncObjectPicker>();
        var chosen=picker!=null?picker.Provisional:null;
        if(chosen==null || pickTarget==null) return;
        Pick(chosen.Id);
    }
    void SelectCondition(string id)
    {
        if(trial!=null || Modal || string.IsNullOrEmpty(id) || id==conditionId) return;
        // A target pick in progress belongs to the previous condition and must not land in this one.
        pickTarget=null;
        GetComponent<SkillSyncObjectPicker>()?.Close();
        conditionId=id;
        Refresh();
    }
    PlacedObject ScreenPickCandidate()
    {
        if(cameraView==null || Condition==null) return null;
        Physics.SyncTransforms();
        if(!PlacedObjectPicker.TryPick(cameraView.ScreenPointToRay(EditInput.MousePosition),~0,out var picked,out _,out _)) return null;
        // Once something is provisional, the next click goes inside that group.
        var provisional=GetComponent<SkillSyncObjectPicker>()?.Provisional;
        string currentId=pickTarget=="A"?Condition.condition.objectAId:Condition.condition.objectBId;
        return PlacedObjectOptionProvider.ScreenCandidate(picked,provisional?provisional:FindPlaced(currentId));
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
    // Why a control is unavailable right now (hover text of SkillSyncDisabledLook). Keys are control actions or "field:<role>".
    string DisabledReason(string key)
    {
        if(Modal && key!="編集を続ける" && key!="該当箇所を修正") return "開いている画面を閉じると使えます";
        var steps=graph.GetDisplayOrderedSteps();
        int index=steps.FindIndex(x=>x.nodeId==stepId)+1;
        bool part=selection.Current!=null || ghost!=null;
        switch(key)
        {
            case "保存": case "↑ 教材を書き出す": case "新規": case "教材を読み込む":
                if(trial!=null) return "動作確認中は使えません。「編集に戻る」を押してください";
                if(ghost!=null) return "部品の仮配置中は使えません。置くか、Escでやめてください";
                if(BlocksEditingShortcuts) return "開いている画面を閉じると使えます";
                break;
            case "↶ 元に戻す": return trial!=null?"動作確認中は使えません":"元に戻せる操作がありません";
            case "↷": return trial!=null?"動作確認中は使えません":"やり直せる操作がありません";
            case "複製": case "削除": return "部品を選ぶと使えます";
            case "移動": case "回転": case "大きさ": return "部品を選ぶと使えます";
            case "DeleteStep": return Step==null?"手順を選ぶと使えます":"動作確認中、仮配置中は削除できません";
            case "DeleteCondition": return "削除できる完了条件がありません";
            case "＋ 完了条件を追加": return Step==null?"先に手順を追加してください":"完了条件は最大"+graph.GetMaxConditionsPerStep()+"件までです";
            case "↑ 上へ": return Step==null?"手順を選ぶと使えます":"すでに先頭の手順です";
            case "↓ 下へ": return Step==null?"手順を選ぶと使えます":"すでに最後の手順です";
            case "最初から通して確認": case "最初から試す":
                if(steps.Count==0) return "手順を追加すると使えます";
                if(trial!=null) return "動作確認中は使えません";
                if(ghost!=null) return "部品の仮配置中は使えません";
                break;
            case "PickA": return "完了条件を選ぶと使えます";
            case "PickB": return Condition==null?"完了条件を選ぶと使えます":"この条件では2つ目の部品を使いません";
            case "field:name": return ghost==null && selection.Current!=null?"複数の部品を選んでいる間は、名前を変えられません":"3D画面か「配置した部品」で部品を選んでください";
            case "field:height": case "field:angle": return "3D画面か「配置した部品」で部品を選んでください";
            case "field:stepTitle": case "field:body": return "手順を選ぶと使えます";
        }
        if(trial!=null) return "動作確認中は編集できません";
        if(ghost!=null) return "部品の仮配置中は使えません。置くか、Escでやめてください";
        return "";
    }
    // Cmd+S / the 保存 button. A never-saved lesson asks for a name first; the result is announced with a way to find the file.
    void RequestSave()
    {
        if(trial!=null || ghost!=null || Modal || EditWorkspace.HasOpenModal) return;
        EditorProjectSaveFlow.Run(project,saved=>{if(saved) AnnounceSaved();RefreshValues();});
    }
    void AnnounceSaved()
    {
        string path=project.CurrentProjectPath;
        status="保存しました: "+project.CurrentProjectName;
        TopCenterNotification.Ensure(transform,view.inspectorTitle)?.Show("「"+project.CurrentProjectName+"」を保存しました",false,6f,ProjectFileLocation.RevealLabel,()=>ProjectFileLocation.Reveal(path));
    }
    void RequestNew()
    {
        if(trial!=null || ghost!=null || Modal || EditWorkspace.HasOpenModal) return;
        EditorProjectPanel.Ensure(transform.root)?.RequestNewProject();
    }
    // Name field: Enter or leaving the field renames the same lesson (a saved lesson keeps its file); Esc restores the old name.
    // Nothing here saves the lesson's content.
    void CommitProjectName(string value)
    {
        string name=(value??"").Trim();
        nameHint=null;
        if(EditInput.CancelPressedThisFrame() || name.Length==0 || name==project.CurrentProjectName) {view.Value("project",project.CurrentProjectName);return;}
        if(project.Rename(name,out var message)) {Notify(message,false);return;}
        if(project.NameInUse(name))
        {
            bool enter=EditInput.ConfirmPressedThisFrame();
            nameHint="同じ名前があります";nameHintTone=2;nameHintUntil=Time.unscaledTime+(enter?float.MaxValue/2:4f);
            if(enter) StartCoroutine(RefocusProjectName(name));
            else view.Value("project",project.CurrentProjectName);
            return;
        }
        view.Value("project",project.CurrentProjectName);
        Notify(message,true);
    }
    IEnumerator RefocusProjectName(string typed)
    {
        yield return null;
        var field=view.fields.FirstOrDefault(f=>f.role=="project");
        if(field==null || !field.input.gameObject.activeInHierarchy) yield break;
        field.input.SetTextWithoutNotify(typed);
        field.input.Select();field.input.ActivateInputField();
        ShowNameHint(typed);
    }
    // The pill under the lesson name says "未保存" (never saved), "未保存の変更あり" or "✓ 保存済み"; while the name is edited it shows a one-line hint instead.
    void RefreshSavedPill()
    {
        var saveState=project.SaveState;
        bool nameFocused=view.fields.Any(f=>f.role=="project" && f.input.isFocused);
        if(nameHint!=null && !nameFocused && Time.unscaledTime>=nameHintUntil) nameHint=null;
        if(nameHint!=null) view.SetSavedState(nameHint,nameHintTone);
        else
        {
            bool neverSaved=saveState==EditorProjectService.SaveStatus.NeverSaved;
            view.SetSavedState(neverSaved?"未保存":saveState==EditorProjectService.SaveStatus.Unsaved?"未保存の変更あり":"✓ 保存済み",
                saveState==EditorProjectService.SaveStatus.Saved || neverSaved && !project.IsDirty?0:1);
        }
        // Only unsaved work makes the save button stand out (an untouched new lesson has nothing to save).
        view.SetHeaderEmphasis("保存",project.IsDirty);
    }
    // One short line while the name is edited, shown in the saved-state pill right below the field.
    void ShowNameHint(string typed)
    {
        string name=(typed??"").Trim();
        if(name.Length==0) {nameHint="名前を入力";nameHintTone=2;}
        else if(name!=project.CurrentProjectName && project.NameInUse(name)) {nameHint="同じ名前があります";nameHintTone=2;}
        else {nameHint="Enterで確定";nameHintTone=1;}
        nameHintUntil=float.MaxValue/2;
        RefreshSavedPill();
    }
    // Cheap enough for change events only; never call per frame.
    void Revalidate()
    {
        if(validating || graph==null) return;
        validating=true;
        try
        {
            validation=graph.ValidateGraph();
            issues=ScenarioValidationText.BuildEntries(graph,validation,PlacedLookup().ContainsKey);
            validationErrorCount=issues.Count;
            stepsWithIssue.Clear();issuesByStep.Clear();issueByCondition.Clear();
            foreach(var issue in issues)
            {
                if(!string.IsNullOrEmpty(issue.stepId))
                {
                    stepsWithIssue.Add(issue.stepId);
                    if(!issuesByStep.TryGetValue(issue.stepId,out var list)) issuesByStep[issue.stepId]=list=new List<ValidationEntry>();
                    list.Add(issue);
                }
                if(!string.IsNullOrEmpty(issue.conditionId) && !issueByCondition.ContainsKey(issue.conditionId)) issueByCondition[issue.conditionId]=issue;
            }
        }
        finally {validating=false;}
        RecomputeDistanceWarnings();
    }
    Dictionary<string,PlacedObject> PlacedLookup()
    {
        var lookup=new Dictionary<string,PlacedObject>();
        foreach(var p in Placed) if(!string.IsNullOrEmpty(p.Id)) lookup[p.Id]=p;
        return lookup;
    }
    string DistanceWarningFor(ConditionNodeData c,Dictionary<string,PlacedObject> lookup,Dictionary<string,Bounds?> sizes)
    {
        if(!SkillSyncConditionEditing.UsesCenterDistance(c) || !SkillSyncConditionEditing.TargetsValid(c,lookup.ContainsKey)) return null;
        Bounds? Size(string id)
        {
            if(sizes.TryGetValue(id,out var known)) return known;
            Bounds? measured=PlacedObjectGrounding.TryGetRendererBounds(lookup[id].transform,out var bounds)?bounds:(Bounds?)null;
            return sizes[id]=measured;
        }
        var a=Size(c.objectAId);var b=Size(c.objectBId);
        if(a==null || b==null) return null;
        float minimum=SkillSyncConditionEditing.MinimumCenterDistance(a.Value.extents.x,a.Value.extents.y,a.Value.extents.z,b.Value.extents.x,b.Value.extents.y,b.Value.extents.z);
        float limit=ConditionTypeCatalog.GetNumber(c,ConditionTypeCatalog.DistanceKey,.1f);
        return SkillSyncConditionEditing.DistanceLikelyUnreachable(limit,minimum)?SkillSyncConditionEditing.DistanceWarning(limit,minimum):null;
    }
    // Sizes change without graph events, so this is also polled (see RefreshValues). Returns true when the result changed.
    bool RecomputeDistanceWarnings()
    {
        if(graph==null) return false;
        distanceWarnings.Clear();stepsWithWarning.Clear();
        var lookup=PlacedLookup();var sizes=new Dictionary<string,Bounds?>();
        foreach(var step in graph.GetDisplayOrderedSteps())
            foreach(var node in graph.GetConditionNodesForStep(step.nodeId))
            {
                string message=DistanceWarningFor(node.condition,lookup,sizes);
                if(message==null) continue;
                distanceWarnings[node.nodeId]=message;stepsWithWarning.Add(step.nodeId);
                validation?.AddWarning("W-03",message,node.nodeId);
            }
        string signature=string.Join("|",distanceWarnings.Keys.OrderBy(k=>k,StringComparer.Ordinal));
        bool changed=signature!=warningSignature;
        warningSignature=signature;
        return changed;
    }
    // Object deletion and undo change validity without a graph event, so rebuild the step rows only when the result changed.
    void RevalidateAndSync()
    {
        string before=validationErrorCount+"|"+string.Join(",",stepsWithIssue.OrderBy(i=>i,StringComparer.Ordinal))+"|"+warningSignature;
        Revalidate();
        string after=validationErrorCount+"|"+string.Join(",",stepsWithIssue.OrderBy(i=>i,StringComparer.Ordinal))+"|"+warningSignature;
        if(before!=after) RebuildSteps();
    }
    string ValidationSummary() => validationErrorCount>0?"要修正"+validationErrorCount+"件":distanceWarnings.Count>0?"注意"+distanceWarnings.Count+"件":"問題なし";
    // Deleting parts that step conditions use asks first in the central dialog; the real delete runs only after "削除".
    bool ConfirmDeleteObjects(IReadOnlyList<PlacedObject> items)
    {
        if(graph==null) return true;
        var ids=new HashSet<string>(items.Select(p=>p.Id).Where(id=>!string.IsNullOrEmpty(id)));
        var steps=graph.GetDisplayOrderedSteps();var used=new List<int>();
        for(int i=0;i<steps.Count;i++)
            if(graph.GetConditionNodesForStep(steps[i].nodeId).Any(c=>ids.Contains(c.condition.objectAId) || ids.Contains(c.condition.objectBId))) used.Add(i+1);
        if(used.Count==0) return true;
        string subject=items.Count==1?"「"+items[0].GetDisplayName()+"」":"選択中の"+items.Count+"個の部品";
        SkillSyncConfirmDialog.Show("使用中の部品を削除しますか？",
            subject+"は"+string.Join("・",used.Select(n=>"手順"+n))+"の完了条件で使われています。削除すると、その完了条件は成立しなくなります。「元に戻す」で復元できます。",
            "削除",()=>selection.DeleteSelected(true),destructive:true);
        return false;
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
        foreach (var pair in libraryTypes) pair.Key.SetSelected(pair.Value == type || pair.Value == highlightType);
    }
    void ObjectPlaced(PlacedObject _,string __) {RevalidateAndSync();RebuildObjects();RefreshValues();}
    void CameraNotice(string message) => Notify(message,false);
    void HistoryChanged() {RevalidateAndSync();RebuildObjects();RefreshValues();}
    // Button and Cmd/Ctrl+Z both land here, so the key path says what was undone too.
    void HistoryApplied(bool redone,string description) => Notify((redone?"やり直しました：":"元に戻しました：")+description,false);
    void ProjectStatus(string message,bool success) {if(!success && !string.IsNullOrEmpty(message)) Notify(message,true);else status=message;RefreshValues();}
    // The catalog reports severity explicitly; never infer it from the wording.
    void CatalogStatus(string message,bool error)
    {
        if(string.IsNullOrEmpty(message)) return;
        Notify(message,error);
        RefreshValues();
    }
    void EditModeChanged(EditMode m)
    {
        if(m==EditMode.Scale) transformTab="大きさ";
        else if(m==EditMode.Transform && transformTab=="大きさ") {transformTab="移動";TransformToolSettings.SetTool(TransformTool.Move);}
        RefreshValues();
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
        // A card that appeared after "3Dモデルを追加" is shown whatever the search or category, and pointed out below.
        var ids=entries.Where(e=>e.prefab!=null).Select(e=>e.typeId).ToList();
        string added=knownLibraryTypes!=null && importWatch && (catalog==null || !catalog.IsRestoringModels)?ids.FirstOrDefault(id=>!knownLibraryTypes.Contains(id)):null;
        knownLibraryTypes=new HashSet<string>(ids);
        if(added!=null) {importWatch=false;search="";category="すべて";view.Value("search","");}
        SkillSyncDesignRow addedRow=null;string addedTitle=null;
        // Placeholder parts are plain built-in cubes; give each card its own colour so the library is not a row of identical boxes.
        proxyTints.Clear();int proxyIndex=0;
        foreach(var entry in entries)
            if(entry.prefab!=null && !proxyTints.ContainsKey(entry.prefab) && IsPlaceholderCube(entry.prefab)) proxyTints[entry.prefab]=ProxyPalette[proxyIndex++%ProxyPalette.Length];
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
            if(row.thumbnail!=null) row.thumbnail.texture=thumbnails.Get(entry.prefab,proxyTints.TryGetValue(entry.prefab,out var tint)?tint:(Color?)null);
            libraryTypes[row]=entry.typeId;
            var chosen=entry;row.button.onClick.AddListener(()=>BeginGhost(chosen));
            var drag=row.gameObject.AddComponent<SkillSyncCardDrag>();
            drag.begin=()=>CardDragBegin(chosen);drag.moved=point=>CardDragMove(chosen,point);drag.ended=point=>CardDragEnd(chosen,point);
            if(entry.typeId==added) {addedRow=row;addedTitle=title;}
            var remove=row.GetComponent<SkillSyncLibraryRemove>();
            if(remove!=null) remove.remove.onClick.AddListener(()=>SkillSyncConfirmDialog.Show("ライブラリから外しますか？",
                "「"+title+"」をライブラリから外します。すでに配置した部品はそのまま残ります。",
                "外す",()=>{
                    if(ghost!=null && ghost.TypeId==chosen.typeId)CancelGhost();
                    catalog?.RemoveDesignLibraryEntry(chosen.typeId);RebuildLibrary();Refresh();
                },destructive:true));
        }
        libraryEmptyText=libraryRows.Count>0?"":!string.IsNullOrEmpty(search)?"「"+search+"」に一致する部品はありません":category!="すべて"?"このカテゴリには部品がありません":"部品がありません。下のボタンから3Dモデルを追加してください";
        guide?.SelectChip(category);
        if(addedRow!=null)
        {
            highlightType=added;highlightUntil=Time.unscaledTime+12f;RefreshLibrarySelection();
            ScrollIntoView(view.libraryContent,(RectTransform)addedRow.transform);
            Notify("「"+addedTitle+"」を追加しました。カードをクリックまたは3D画面へドラッグして置けます",false);
        }
    }
    void RebuildSteps()
    {
        if(view==null || graph==null) return;
        // Rows under a drag stay alive; the rebuild waits for the drop.
        if(reorder!=null && reorder.Dragging) {stepRebuildPending=true;return;}
        Clear(stepRows);reorder?.Reset();var steps=graph.GetDisplayOrderedSteps();
        for(int i=0;i<steps.Count;i++)
        {
            var step=steps[i];var row=Row(view.stepTemplate,view.stepsContent,stepRows);
            reorder?.Attach((RectTransform)row.transform,i);
            string title=StepTitle(step,i);
            bool hasIssue=stepsWithIssue.Contains(step.nodeId),hasWarning=!hasIssue && stepsWithWarning.Contains(step.nodeId);
            int conditionCount=graph.GetConditionCountForStep(step.nodeId);
            string summary=(conditionCount==0?"条件なし":"条件"+conditionCount+"件")+(string.IsNullOrWhiteSpace(step.step.body)?"":" ｜ "+step.step.body);
            // The reason comes first, so a flagged step says what to fix without opening it.
            string reason=hasIssue?StepIssueReason(step.nodeId):hasWarning?"距離が小さすぎて、成立しない可能性があります":null;
            if(reason!=null) summary="⚠ "+reason+"\n"+summary;
            row.Set(hasIssue?"⚠ "+title+"（要修正）":hasWarning?"⚠ "+title+"（要確認）":title,summary,(i+1).ToString(),step.nodeId==stepId,!hasIssue && !hasWarning);
            row.button.onClick.AddListener(()=>{EndTrial();pickTarget=null;GetComponent<SkillSyncObjectPicker>()?.Close();stepId=step.nodeId;conditionId=null;mode=1;GraphChanged();});
        }
        if(stepId!=scrolledStepId)
        {
            scrolledStepId=stepId;
            int at=steps.FindIndex(s=>s.nodeId==stepId);
            if(at>=0 && at<stepRows.Count) ScrollIntoView(view.stepsContent,(RectTransform)stepRows[at].transform);
        }
    }
    string StepIssueReason(string stepId)
    {
        if(!issuesByStep.TryGetValue(stepId,out var list) || list.Count==0) return "修正が必要です";
        return list[0].Reason+(list.Count>1?"（ほか"+(list.Count-1)+"件）":"");
    }
    // A blank title shows the step's position, which is also what the export names it.
    static string StepTitle(ScenarioNode step,int index) => string.IsNullOrWhiteSpace(step.step.title)?"手順 "+(index+1):step.step.title;
    void RebuildObjects()
    {
        if(view==null) return;
        // Placement roots are listed first; parts inside a model are nested and collapsed until opened.
        // Only visible rows exist, so large models do not instantiate hundreds of rows.
        var allPlaced=Placed;
        var rowsData=SkillSyncPartsTree.Build(allPlaced,expandedParts,collapsedGroups,partsFilter,out int rootCount,out int matchCount);
        groupMembers.Clear();
        foreach(var part in allPlaced)
        {
            if(part==null || string.IsNullOrEmpty(part.editorGroupId) || SkillSyncPartsTree.ParentOf(part)!=null) continue;
            if(!groupMembers.TryGetValue(part.editorGroupId,out var members)) groupMembers[part.editorGroupId]=members=new List<PlacedObject>();
            members.Add(part);
        }
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
                row.button.onClick.AddListener(()=>{if(pickTarget!=null) {if(!rowData.IsGroup) Pick(p.Id);} else {NoticeBlockedRow(rowData);selection.SelectFromList(p,EditInput.ShiftPressed(),CmdOrCtrl(),OrderedPlaced());}});
                var toggles=SkillSyncRowToggles.Add(row,view.objectTemplate.title.font);
                toggles.toggleHidden=()=>ToggleRowState(rowData,false);
                toggles.toggleLocked=()=>ToggleRowState(rowData,true);
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
            var rowTargets=RowTargets(data);
            bool rowHidden=rowTargets.Any(item=>PartState(item,false)),rowLocked=rowTargets.Any(item=>PartState(item,true));
            row.GetComponent<SkillSyncRowToggles>()?.Apply(rowHidden,rowLocked);
            string stateTag=rowHidden?"非表示":rowLocked?"ロック中":"";
            row.Set(data.title,string.IsNullOrEmpty(stateTag)?data.detail:string.IsNullOrEmpty(data.detail)?stateTag:data.detail+" ・ "+stateTag,"",rowSelected);
        }
        expanderBound.RemoveWhere(row=>row==null);
        if(scrollObjectsPending)
        {
            scrollObjectsPending=false;
            if(selection.Current!=null && placedRows.TryGetValue(selection.Current,out var currentRow)) ScrollIntoView(view.objectsContent,(RectTransform)currentRow.transform);
        }
        ShowVisiblePartThumbnails();
    }
    void EditStateChanged(PlacedObjectEditState _) {SkillSyncSurfaceProbe.Invalidate();objectsDirty=true;}
    List<PlacedObject> RowTargets(SkillSyncPartsTree.Row data) =>
        data.IsGroup && groupMembers.TryGetValue(data.groupId,out var members)?members:new List<PlacedObject>{data.part};
    // Hidden / locked as it takes effect on the part, including what it inherits from the model around it.
    static bool PartState(PlacedObject part,bool locked)
    {
        for(var node=part!=null?part.transform:null;node!=null;node=node.parent)
        {
            var state=node.GetComponent<PlacedObjectEditState>();
            if(state!=null && (locked?state.Locked:state.Hidden)) return true;
        }
        return false;
    }
    void NoticeBlockedRow(SkillSyncPartsTree.Row data)
    {
        var targets=RowTargets(data);
        if(targets.Any(item=>PartState(item,false))) Notify(HiddenNotice,false);
        else if(targets.Any(item=>PartState(item,true))) Notify(LockedNotice,false);
    }
    void ToggleRowState(SkillSyncPartsTree.Row data,bool locked)
    {
        var targets=RowTargets(data);
        SetPartState(targets,locked,!targets.Any(item=>PartState(item,locked)));
    }
    // 隠す / 表示する and ロック / ロック解除, for the parts of a list row or of the current selection.
    void SetPartState(IList<PlacedObject> targets,bool locked,bool on)
    {
        if(trial!=null || pickTarget!=null || Modal) return;
        var list=targets.Where(item=>item!=null).ToList();
        if(list.Count==0) return;
        if(!on)
        {
            // Turning it off only works on the part that carries the state, not on a part inside a hidden / locked model.
            foreach(var item in list)
            {
                var own=item.GetComponent<PlacedObjectEditState>();
                if(own!=null && (locked?own.Locked:own.Hidden)) continue;
                for(var node=item.transform.parent;node!=null;node=node.parent)
                {
                    var outer=node.GetComponent<PlacedObjectEditState>();
                    if(outer==null || !(locked?outer.Locked:outer.Hidden)) continue;
                    var holder=outer.GetComponent<PlacedObject>();
                    Notify("「"+(holder!=null?holder.GetDisplayName():"外側の部品")+"」全体が"+(locked?"ロック":"非表示")+"されています。その行の"+(locked?"鍵":"目のアイコン")+"を操作してください",true);
                    return;
                }
            }
        }
        if(on && selection!=null && list.Any(item=>selection.Contains(item) || selection.Selected.Any(sel=>sel.transform.IsChildOf(item.transform)))) selection.Select(null);
        foreach(var item in list)
        {
            var state=item.GetComponent<PlacedObjectEditState>();
            if(state==null) state=item.gameObject.AddComponent<PlacedObjectEditState>();
            if(locked) state.SetLocked(on);else state.SetVisible(!on);
        }
        SkillSyncSurfaceProbe.Invalidate();
        string name=list.Count==1?"「"+list[0].GetDisplayName()+"」":list.Count+"個の部品";
        Notify(locked?on?name+"をロックしました。一覧の鍵を外すと編集できます":name+"のロックを解除しました":on?name+"を隠しました。一覧の目のアイコンで表示に戻せます":name+"を表示しました",false);
        RebuildObjects();
    }
    void ShowBlockedMenu(Vector2 point,IList<PlacedObject> targets)
    {
        var items=new List<SkillSyncContextMenu.Item>();
        var list=targets.ToList();
        if(list.Any(item=>PartState(item,false))) items.Add(new SkillSyncContextMenu.Item{label="表示する",action=()=>SetPartState(list,false,false)});
        if(list.Any(item=>PartState(item,true))) items.Add(new SkillSyncContextMenu.Item{label="ロック解除",action=()=>SetPartState(list,true,false)});
        if(items.Count>0) SkillSyncContextMenu.Show(transform,view.objectTemplate.title.font,point,items);
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
            picker.ConfirmPicking=CommitProvisional;
            picker.SetPicking(pickTarget);
        }
        view.Value("project",project.CurrentProjectName);
        RefreshSavedPill();
        var saveState=project.SaveState;
        string footerStatus = status;
        view.Text("footer",trial!=null?"試行中の操作は教材の初期配置に保存されません":(string.IsNullOrEmpty(footerStatus)?saveState==EditorProjectService.SaveStatus.Saved?"✓ すべての変更を保存しました":"未保存の変更があります":footerStatus)+" ｜ "+ValidationSummary());
        view.Text("objectCount",objectCountText+(ghost!=null?" ＋ 仮配置1件":""));
        view.Text("stepCount","手順 "+graph.GetDisplayOrderedSteps().Count+"件 ｜ "+ValidationSummary());
        var current=selection.Current;
        Transform target=ghost!=null?ghost.Transform:current!=null?current.transform:null;
        selectionInfo=SkillSyncSelectionInfo.Describe(selection);
        // Several parts move together, so their height/angle fields start at 0 and mean "by how much".
        bool relative=ghost==null && current!=null && selectionInfo.multiple;
        view.compactSelectionTitle=ghost==null && current!=null && selectionInfo.compactTitle;
        view.Text("selectionTitle",ghost!=null?"置く場所を選ぶ":current!=null?selectionInfo.title:"部品を選択");
        view.Value("name",ghost!=null?ghost.DisplayName:current!=null?(relative?selectionInfo.title:current.GetDisplayName()):"");
        view.Value("height",relative?"0":target!=null?HeightText(target):"");
        view.Value("angle",relative?"0":target!=null?target.eulerAngles.y.ToString("0.##"):"");
        int index=graph.GetDisplayOrderedSteps().FindIndex(s=>s.nodeId==stepId)+1;
        view.Text("inspectorLabel",mode==1?Step!=null?"選択中の手順："+index:"手順を選択してください":ghost!=null?"仮配置":current!=null?"選択中":"未選択");
        view.UpdateSelectionHeader();
        view.Value("stepTitle",Step?.step.title??"");view.Value("body",Step?.step.body??"");
        view.Value("distance",SkillSyncConditionEditing.Value(Condition?.condition,0));
        view.Value("hold",SkillSyncConditionEditing.Value(Condition?.condition,1));
        bool requiresB=Condition!=null && ConditionTypeCatalog.RequiresObjectB(Condition.condition.type);
        bool distanceCondition=SkillSyncConditionEditing.SupportsPcTrial(Condition?.condition);
        var lookup=PlacedLookup();
        PlacedObject Find(string id)=>!string.IsNullOrEmpty(id) && lookup.TryGetValue(id,out var found)?found:null;
        var a=Find(Condition?.condition.objectAId);var b=requiresB?Find(Condition?.condition.objectBId):null;
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
        view.Text("objectA",a!=null?SkillSyncObjectPicker.DisplayLabel(a):"対象を選択してください");
        view.Text("objectB",b!=null?SkillSyncObjectPicker.DisplayLabel(b):"対象を選択してください");
        bool valid=SkillSyncConditionEditing.TargetsValid(Condition?.condition,lookup.ContainsKey);
        var conditions=Step!=null?graph.GetConditionNodesForStep(stepId):new List<ScenarioNode>();
        bool canTrial=conditions.Count>0 && conditions.All(c=>SkillSyncConditionEditing.TargetsValid(c.condition,lookup.ContainsKey));
        int conditionIndex=conditions.FindIndex(c=>c.nodeId==conditionId);
        int maxConditions=graph.GetMaxConditionsPerStep();
        view.Text("conditionHeading","完了する条件");
        view.Text("conditionCounter",conditions.Count==0?"0件":$"{conditions.Count}件（最大{maxConditions}件）");
        view.SetVisible("DeleteCondition",conditionIndex>=0);
        view.Enable("DeleteCondition",conditionIndex>=0);
        view.Enable("＋ 完了条件を追加",Step!=null && conditions.Count<maxConditions);
        view.Text("conditionAddLabel",conditions.Count<maxConditions?"＋ 完了条件を追加":"条件は最大"+maxConditions+"件です");
        if(deleteConditionLabel!=null) deleteConditionLabel.text=conditions.Count==1?"最後の条件を削除":"この条件を削除";
        view.Enable("DeleteStep",Step!=null && trial==null && ghost==null);
        if(view.conditionList!=null) view.conditionList.SetItems(conditions.Select((c,i)=>ConditionItem(c,i,lookup)).ToList());
        view.Text("conditionSummary",Step==null?"先に手順を追加してください":Condition==null?"完了条件がありません。下の＋で追加してください":
            SkillSyncConditionEditing.Summary(Condition.condition)+(valid?"":"\n\n⚠ "+(issueByCondition.TryGetValue(Condition.nodeId,out var conditionIssue)?conditionIssue.what+"。"+conditionIssue.action:"対象が未設定です")));
        SkillSyncConditionEditorView.Refresh(view,Condition?.condition,canTrial,Condition!=null && distanceWarnings.TryGetValue(Condition.nodeId,out var warning)?warning:null);
        view.SetVisible("IssueChip",issues.Count>0 && !Modal && graph.GetDisplayOrderedSteps().Count>0);
        if(issues.Count>0) view.SetCaption("IssueChip","⚠ 要修正 "+issues.Count+"件");
        view.Enable("▶ この条件を試す",true);
        view.Enable("動作を確認",true);
        view.Enable("↑ 上へ",index>1);view.Enable("↓ 下へ",index>0 && index<graph.GetDisplayOrderedSteps().Count);
        view.Enable("↶ 元に戻す",trial==null && CommandService.I?.Stack.CanUndo==true);view.Enable("↷",trial==null && CommandService.I?.Stack.CanRedo==true);
        view.Enable("最初から通して確認",trial==null && ghost==null && graph.GetDisplayOrderedSteps().Count>0);
        view.Enable("↑ 教材を書き出す",trial==null && ghost==null);
        view.Enable("保存",trial==null && ghost==null);
        view.Enable("新規",!BlocksEditingShortcuts);
        view.Enable("教材を読み込む",!BlocksEditingShortcuts);
        view.Text("workspaceHint",trial!=null?"部品を選び、ギズモで操作 / 中・右ドラッグで視点移動":mode==1?$"手順{index}で使う部品を表示":"部品を選んで、作業面に配置します");
        guide?.SetPlacement(ghost!=null?dragEntry!=null?"3D画面でマウスのボタンを離すと置きます ・ Escでやめる":"クリックで置く ・ Shift+クリックで続けて置く ・ Escでやめる":null);
        guide?.SetLoading(catalog!=null && catalog.IsLoadingModel);
        // First-use guidance in the empty 3D screen: no parts yet (placement) / no steps yet (step editing). Gone once there is one.
        bool noParts=view.State==0 && ghost==null && selection.Current==null && Placed.Length==0;
        bool noSteps=view.State==1 && graph.GetDisplayOrderedSteps().Count==0 && !Modal;
        if(noParts) guide?.SetEmptyHint("部品を置いてみましょう","① 左の「部品を追加」から、置きたい部品をクリック\n② 3D画面の置きたい場所をクリック\n（部品を3D画面へドラッグしても置けます）");
        else if(noSteps) guide?.SetEmptyHint("最初の手順を作りましょう","左下の「＋ 手順を追加」を押すと、手順が1つ増えます。\n手順ごとに、完了の条件を右側で決めます");
        else guide?.SetEmptyHint(null,null);
        guide?.Refresh(view.State==0 || view.State==3,libraryEmptyText);
        view.Text("status",pickTarget!=null?status:ghost!=null?"＋ 仮配置中：クリックで置く / Shift+クリックで続けて置く / Escでやめる":trial!=null?trial.Complete?"✓ 条件が成立しました":"▶ 試行中：ギズモで部品を動かして条件を確かめます":current!=null?(selectionInfo.multiple && selectionInfo.title.EndsWith("選択中",StringComparison.Ordinal)?"● "+selectionInfo.title:"● 選択中："+selectionInfo.title)+" | Escで選択解除":"部品を選択してください");
        if(trial==null) view.viewportDistance.text=valid && distanceCondition?$"現在の距離 {Vector3.Distance(SkillSyncTrialSession.Center(a.transform),SkillSyncTrialSession.Center(b.transform))*100:0} cm":"";
        view.viewportDistance.transform.parent.gameObject.SetActive(valid && distanceCondition && (view.State==1 || trial!=null));
        view.viewportGrid.text=$"グリッド {EditSnapSettings.GridSize*100:0.##} cm";
        view.viewportGrid.color=new Color32(88,103,124,255);
        view.Text("viewportMode",mode==1||trial!=null?valid?distanceCondition?"判定範囲（球）を表示中":"操作条件（PCでは自動判定できません）":"対象を選択してください":EditSnapSettings.ShouldSnap?"作業面に吸着：ON":"作業面に吸着：OFF");
        bool hasPart=ghost!=null || current!=null;
        extras?.Refresh(transformTab,selectionInfo,view.State==0 && ghost==null && trial==null && current!=null,ScaleText(current),view.State==0 && !hasPart);
        // Nothing to edit yet: the inputs and buttons that act on a part/step look unavailable and say what to select (UXE-22).
        view.SetFieldAvailable("name",hasPart && !(ghost==null && selectionInfo.multiple));
        view.SetFieldAvailable("height",hasPart);view.SetFieldAvailable("angle",hasPart);
        view.SetFieldAvailable("stepTitle",Step!=null);view.SetFieldAvailable("body",Step!=null);
        view.Enable("複製",current!=null && ghost==null);view.Enable("削除",current!=null && ghost==null);
        foreach(var tool in new[]{"移動","回転","大きさ"}) view.Enable(tool,hasPart);
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
    SkillSyncItemList.Item ConditionItem(ScenarioNode node,int index,Dictionary<string,PlacedObject> lookup)
    {
        var c=node.condition;
        string Name(string id)=>!string.IsNullOrEmpty(id) && lookup.TryGetValue(id,out var found)?SkillSyncObjectPicker.PartName(found):null;
        string a=Name(c.objectAId),b=Name(c.objectBId);
        string targets=a==null?"対象が未設定":ConditionTypeCatalog.RequiresObjectB(c.type)?a+" → "+(b??"未設定"):a;
        bool valid=SkillSyncConditionEditing.TargetsValid(c,lookup.ContainsKey),warn=true;
        string detail;
        if(!valid) detail="⚠ "+(issueByCondition.TryGetValue(node.nodeId,out var issue)?issue.what:"対象が未設定です");
        else if(distanceWarnings.ContainsKey(node.nodeId)) detail="⚠ 距離が小さすぎて成立しない可能性があります";
        else {warn=false;detail=SkillSyncConditionEditing.SupportsPcTrial(c)?SkillSyncConditionEditing.Detail(c):SkillSyncConditionEditing.PcUnsupportedText;}
        bool selected=node.nodeId==conditionId;
        return new SkillSyncItemList.Item{id=node.nodeId,number=index+1,title=(ConditionTypeCatalog.Find(c.type)?.label??"未対応の条件")+"："+targets,
            detail=detail,warn=warn,selected=selected,tag=selected?"編集中":null};
    }
    PlacedObject FindPlaced(string id) => string.IsNullOrEmpty(id)?null:Placed.FirstOrDefault(p=>p.Id==id);
    RenderTexture ObjectThumbnail(PlacedObject placed)
    {
        if (placed == null) return null;
        // A root's library thumbnail is shared by its instances. Parts retain
        // their own geometry preview instead of showing the entire model.
        var source = placed.gameObject;
        if (placed.modelRoot == null && thumbnailSources.TryGetValue(placed.TypeId, out var prefab) && prefab != null) source = prefab;
        return thumbnails.Get(source,proxyTints.TryGetValue(source,out var tint)?tint:(Color?)null);
    }
    static bool IsPlaceholderCube(GameObject prefab)
    {
        if(prefab.GetComponent<RuntimeModelResources>()!=null) return false;
        var filters=prefab.GetComponentsInChildren<MeshFilter>(true);
        return filters.Length==1 && filters[0].sharedMesh!=null && filters[0].sharedMesh.name=="Cube";
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
        if(conditions.Any(c=>!SkillSyncConditionEditing.TargetsValid(c.condition,id=>FindPlaced(id)!=null)))
        {Notify("手順内のすべての条件に対象を設定してください",true);Refresh();return;}
        // Conditions the PC cannot judge stay in the flow; the gauges follow the first one it can.
        if(!SkillSyncConditionEditing.SupportsPcTrial(Condition.condition))
        {
            var judgeable=conditions.FirstOrDefault(c=>SkillSyncConditionEditing.SupportsPcTrial(c.condition));
            if(judgeable!=null) conditionId=judgeable.nodeId;
        }
        EndTrial();pickTarget=null;
        trialMoveTool = FindFirstObjectByType<MoveTool>();
        PauseTool(trialMoveTool);
        try {trial=new SkillSyncTrialSession(Placed);}
        catch(Exception e) {EndTrial();Notify("試行を開始できません: "+e.Message,true);Debug.LogException(e);Refresh();return;}
        logs.Clear();trialTime=0;trialCarried=false;
        PauseTool(selection);PauseTool(placement);PauseTool(FindFirstObjectByType<RotateTool>());PauseTool(CommandService.I);
        PauseTool(FindFirstObjectByType<SelectionOutline>());
        var first = FindPlaced(Condition.condition.objectAId);
        trialSelectedId = SelectionService.CanEdit(first) ? first.Id : null;
        trialSelected = trialSelectedId != null ? trial.Find(trialSelectedId) : null;
        UpdateTrialGizmo();
        if (trialMoveTool != null) trialMoveTool.enabled = true;
        Log("動作の確認を開始");Refresh();
    }
    void PauseTool(Behaviour tool) {if(tool!=null) {pausedTools[tool]=tool.enabled;tool.enabled=false;}}
    void EndTrial()
    {
        if (trialMoveTool != null) trialMoveTool.EndPreview();
        trialMoveTool=null;trialSelected=null;
        trial?.Dispose();trial=null;
        foreach(var p in pausedTools) if(p.Key!=null) p.Key.enabled=p.Value;
        pausedTools.Clear();
        // The trial rewrote the step rows ("実行中" etc.); restore titles and condition counts.
        if(stepRowsShowTrial) {stepRowsShowTrial=false;RebuildSteps();}
    }
    // Moves on to the next step with the parts where the learner left them; after the last step, says the walkthrough is done.
    void AdvanceTrial()
    {
        var next=ScenarioFlow.Next(graph.curriculum,stepId).Where(n=>n.nodeType==ScenarioNodeType.Step).ToList();
        if(next.Count==1) {ContinueTrial(next[0].nodeId);return;}
        EndTrial();mode=1;
        Notify(next.Count==0?"すべての手順を確認しました。編集画面に戻ります":"分岐先の手順を左の一覧から選んでください",false);
        Refresh();
    }
    void ContinueTrial(string nextStepId)
    {
        var conditions=graph.GetConditionNodesForStep(nextStepId);
        if(trial==null || conditions.Count==0 || conditions.Any(c=>!SkillSyncConditionEditing.TargetsValid(c.condition,id=>FindPlaced(id)!=null)))
        {
            EndTrial();mode=1;stepId=nextStepId;conditionId=null;GraphChanged();
            Notify("次の手順の完了条件と対象を設定すると、続けて確認できます",true);return;
        }
        stepId=nextStepId;
        var lead=conditions.FirstOrDefault(c=>SkillSyncConditionEditing.SupportsPcTrial(c.condition))??conditions[0];
        conditionId=lead.nodeId;
        trial.BeginNextStep();
        trialCarried=true;logs.Clear();trialTime=0;
        var first=FindPlaced(lead.condition.objectAId);
        trialSelectedId=first!=null && SelectionService.CanEdit(first)?first.Id:null;
        trialSelected=trialSelectedId!=null?trial.Find(trialSelectedId):null;
        UpdateTrialGizmo();
        Log("次の手順の確認を開始");
        scrolledStepId=null;RebuildSteps();Refresh();
    }
    // "成立として次へ": the PC cannot judge this condition, so it is counted as met to check the flow.
    void ManualSucceed(string nodeId)
    {
        if(trial==null) return;
        var conditions=graph.GetConditionNodesForStep(stepId);
        bool last=conditions.Where(c=>c.nodeId!=nodeId).All(c=>trial.IsSucceeded(c.nodeId));
        trial.MarkSucceeded(nodeId);
        Log("PCでは判定できない条件を成立として進めました");
        if(last) AdvanceTrial();
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
    // Same rule as editing: the first click takes the whole model, a click inside it takes the part.
    // Clicking empty space keeps the current selection (and its gizmo).
    void SelectTrialObject()
    {
        if(trial.Paused || trial.Complete || !EditInput.LeftPressedThisFrame()) return;
        if(trialMoveTool != null && trialMoveTool.ShouldConsumeSelectionClick()) return;
        var ray=cameraView.ScreenPointToRay(EditInput.MousePosition);
        PlacedObject hit=null;float nearest=float.PositiveInfinity;
        foreach(var p in Placed)
        {
            // Only the innermost parts are hit-tested, since a model's box covers all of its parts.
            if(!SelectionService.CanEdit(p) || p.GetComponentsInChildren<PlacedObject>(true).Length>1) continue;
            var candidate=trial.Find(p.Id);
            if(candidate!=null && PlacedObjectGrounding.TryGetRendererBounds(candidate,out var bounds) && bounds.IntersectRay(ray,out float distance) && distance<nearest)
            {nearest=distance;hit=p;}
        }
        if(hit==null) return;
        var current=FindPlaced(trialSelectedId);
        var root=TrialRoot(hit);
        var chosen=current!=null && TrialRoot(current)==root?hit:root;
        if(!SelectionService.CanEdit(chosen)) chosen=hit;
        trialSelectedId=chosen.Id;trialSelected=trial.Find(chosen.Id);
        UpdateTrialGizmo();
    }
    static PlacedObject TrialRoot(PlacedObject part)
    {
        var root=part;
        for(var t=part.transform.parent;t!=null;t=t.parent) {var found=t.GetComponent<PlacedObject>();if(found!=null) root=found;}
        return root;
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
        view.FollowUnit("distanceValue","distanceLimit");
        view.Text("distanceStatus",trial.InRange?"✓ 距離の条件を満たしています":"距離の条件を確認しています");
        view.Text("holdValue",trial.HeldSeconds.ToString("0.0"));view.Text("holdLimit",$"秒 / {HoldLimit():0.0}秒");
        view.FollowUnit("holdValue","holdLimit");
        view.Text("holdStatus",trial.Complete?"✓ 近づけたままの時間を満たしました":trial.Paused?"一時停止中":"近づけたままの時間を計測中");
        bool holds=Condition?.condition.type==ConditionTypeCatalog.SnapHold;
        view.Text("trialNote",holds?trial.Complete?$"{DistanceLimit()*100:0.##} cm以内に{HoldLimit():0.##}秒間とどまり、":"範囲から出ると、近づけた時間は":
            $"{DistanceLimit()*100:0.##} cm以内に近づけると完了します。");
        view.Text("trialNote2",holds?trial.Complete?"この条件が成立しました。":"0秒から数え直します。":trial.Complete?"この条件が成立しました。":"近づけたままにする時間の条件はありません。");
        string pauseText=trial.Paused?"▶ 再開":"Ⅱ 一時停止";
        if(pauseLabel!=null && pauseLabel.text!=pauseText) pauseLabel.text=pauseText;
        view.Text("trialRule",SkillSyncConditionEditorView.RuleLine+"\n"+(trialCarried?"前の手順で動かした位置を引き継いでいます":"編集中の配置から始まります。次の手順へは位置を引き継ぎます"));
        view.Text("log1",logs.Count>0?logs[0]:"");view.Text("log2",logs.Count>1?logs[1]:"");
        view.holdProgress.rectTransform.sizeDelta=new Vector2(352*(HoldLimit()<=0?trial.Complete?1:0:trial.HeldSeconds/HoldLimit()),8);
        view.holdProgress.color=trial.Complete?new Color32(21,115,74,255):new Color32(8,102,232,255);
        view.viewportDistance.text=$"現在の距離 {trial.Distance*100:0} cm";
        RefreshValues();
        bool gauge=SkillSyncConditionEditing.SupportsPcTrial(Condition?.condition);
        if(!gauge) SkillSyncConditionEditorView.HideTrialGauges(view);
        UpdateTrialList(gauge);
        if(trial.Complete) view.Text("status",$"✓ 手順{index}が完了しました");
        var steps=graph.GetDisplayOrderedSteps();
        stepRowsShowTrial=true;
        for(int i=0;i<stepRows.Count && i<steps.Count;i++)
            stepRows[i].Set(StepTitle(steps[i],i),steps[i].nodeId==stepId?trial.Complete?"完了":"実行中 · 条件を確認":"待機中",(i+1).ToString(),
                trial.Complete?i==index:steps[i].nodeId==stepId,trial.Complete && steps[i].nodeId==stepId);
    }
    void UpdateTrialList(bool gauge)
    {
        var list=view.trialList;
        if(list==null) return;
        var conditions=graph.GetConditionNodesForStep(stepId);
        // With a gauge only the conditions it cannot show are listed; without one, all of them.
        var shown=conditions.Where(c=>!gauge || !SkillSyncConditionEditing.SupportsPcTrial(c.condition)).ToList();
        if(shown.Count==0) {list.gameObject.SetActive(false);return;}
        var lookup=PlacedLookup();
        string Name(string id)=>!string.IsNullOrEmpty(id) && lookup.TryGetValue(id,out var found)?SkillSyncObjectPicker.PartName(found):"未設定";
        var items=new List<SkillSyncItemList.Item>();
        foreach(var node in shown)
        {
            var c=node.condition;
            bool supported=SkillSyncConditionEditing.SupportsPcTrial(c),done=trial.IsSucceeded(node.nodeId);
            bool last=conditions.Where(o=>o.nodeId!=node.nodeId).All(o=>trial.IsSucceeded(o.nodeId));
            string targets=Name(c.objectAId)+(ConditionTypeCatalog.RequiresObjectB(c.type)?" → "+Name(c.objectBId):"");
            items.Add(new SkillSyncItemList.Item{id=node.nodeId,number=conditions.IndexOf(node)+1,done=done,
                title=(ConditionTypeCatalog.Find(c.type)?.label??"未対応の条件")+"："+targets,
                detail=done?"✓ 成立しました":supported?"判定中…":SkillSyncConditionEditing.PcUnsupportedText,
                actionLabel=!done && !supported?(last?"成立として次へ":"成立にする"):null});
        }
        float x=view.wideLayout?SkillSyncDesignLayout.MapX(1184):1184;
        if(gauge) SkillSyncConditionEditorView.HideTrialLog(view);
        float top=gauge?728:320,height=gauge?160:380;
        list.SetBounds(x,top,392,Mathf.Min(height,shown.Count*(SkillSyncItemList.RowHeight+SkillSyncItemList.RowGap)-SkillSyncItemList.RowGap));
        list.SetItems(items);
        list.gameObject.SetActive(true);
    }
    // Name shown on the library card; placed parts use it so card and list agree.
    string LibraryTitle(PrefabEntry entry)
    {
        string title=null;
        if(catalog!=null) catalog.TryGetTypeInfo(entry.typeId,out title,out _);
        return string.IsNullOrWhiteSpace(title) || title==entry.typeId ? entry.prefab.name : title.Trim();
    }
    void BeginGhost(PrefabEntry entry,bool toggle=true)
    {
        if(ghost!=null && ghost.TypeId==entry.typeId) {if(toggle) {CancelGhost();Refresh();}return;}
        highlightType=null;
        EndTrial();CancelGhost();placement.CancelPlacement();
        try {ghost=new SkillSyncPlacementGhost(entry);ghostEntry=entry;ghost.DisplayName=LibraryTitle(entry);}
        catch(Exception e) {Notify("仮配置を開始できません: "+e.Message,true);Debug.LogException(e);Refresh();return;}
        mode=0;
        ghost.Follow(cameraView,new Vector2(cameraView.pixelRect.center.x,cameraView.pixelRect.center.y));
        RefreshLibrarySelection();Refresh();
    }
    // Dragging a library card: the ghost follows the pointer over the 3D screen, and letting go there places the part.
    void CardDragBegin(PrefabEntry entry)
    {
        if(trial!=null || Modal || pickTarget!=null || !initialized) return;
        dragEntry=entry;dragAborted=false;
    }
    bool CardDragMove(PrefabEntry entry,Vector2 point)
    {
        if(dragEntry==null || dragAborted) return false;
        if(RectTransformUtility.RectangleContainsScreenPoint(view.viewport,point))
        {
            if(ghost==null || ghost.TypeId!=entry.typeId) BeginGhost(entry,false);
            return ghost!=null;
        }
        if(ghost!=null) {CancelGhost();Refresh();}
        return false;
    }
    void CardDragEnd(PrefabEntry entry,Vector2 point)
    {
        if(dragEntry==null) return;
        bool aborted=dragAborted;
        dragEntry=null;dragAborted=false;
        if(aborted || ghost==null) {Refresh();return;}
        if(ghost.TypeId==entry.typeId && RectTransformUtility.RectangleContainsScreenPoint(view.viewport,point)) ConfirmGhost(EditInput.ShiftPressed());
        else {CancelGhost();Refresh();}
    }
    // A locked part has no collider, so clicking it selects nothing; say why instead of staying silent.
    void NoticeLockedClick()
    {
        if(cameraView==null || selection==null || Time.unscaledTime<lockedNoticeAt || PlacementController.IsScreenPositionOverBlockingUi(EditInput.MousePosition)) return;
        var ray=cameraView.ScreenPointToRay(EditInput.MousePosition);
        if(!SkillSyncSurfaceProbe.TryFindLocked(ray,out _,out float distance)) return;
        if(PlacedObjectPicker.TryPick(ray,selection.pickMask,out _,out _,out _,out var hitPoint) && Vector3.Distance(ray.origin,hitPoint)<distance) return;
        lockedNoticeAt=Time.unscaledTime+2f;
        Notify(LockedNotice,false);
    }
    const string LockedNotice="ロック中の部品です。一覧の鍵を外すと編集できます", HiddenNotice="隠している部品です。一覧の目のアイコンで表示すると編集できます";
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
    // Steps whose distance condition is smaller than the parts' sizes allow, for the export notice.
    string DistanceCaution()
    {
        RecomputeDistanceWarnings();
        if(distanceWarnings.Count==0) return "";
        var steps=graph.GetDisplayOrderedSteps();
        var numbers=steps.Select((step,i)=>stepsWithWarning.Contains(step.nodeId)?"手順"+(i+1):null).Where(n=>n!=null);
        return "（"+string.Join("・",numbers)+"の距離は小さすぎて、成立しない可能性があります）";
    }
    void Export()
    {
        if(trial!=null || ghost!=null) return;
        // The export saves the lesson first, so a never-saved lesson is named (and a name clash confirmed) before anything is written.
        if(!project.HasSavedFile) {EditorProjectSaveFlow.Run(project,saved=>{if(saved) Export();});return;}
        string caution=DistanceCaution();
        // Only a lesson that can be exported asks where to put it; problems are reported right away.
        string root=null;
        if(graph.ValidateGraph().CanExport)
        {
            root=ExportDestinationPicker.Choose(out bool cancelled);
            if(cancelled) {Notify("書き出しを中止しました。もう一度「教材を書き出す」を押して、書き出し先を選んでください",false);return;}
        }
        string materialName=project.CurrentProjectName;
        var result=TeachingMaterialExportWorkflow.Run(
            (out string message)=>project.Save(project.CurrentProjectName,out message),
            ()=>{validation=graph.ValidateGraph();return validation.CanExport;},
            ()=>TeachingMaterialExportService.Export(graph.BuildScenarioExport(),placement,root,materialName));
        status=result.message;
        if(result.outcome==TeachingMaterialExportWorkflow.Outcome.InvalidGraph)
        {
            Revalidate();
            var first=issues.FirstOrDefault();
            TopCenterNotification.Ensure(transform, view.inspectorTitle)?.Show("教材を書き出せません: "+(first!=null?first.Message:"手順と完了条件を確認してください")+(issues.Count>1?"（ほか"+(issues.Count-1)+"件）":""), true);
            view.Show(5);RefreshValues();RebuildSteps();
            view.Text("errorCount","要修正 "+issues.Count+"件");
            view.Text("errorSummary","教材は保存済みです。直す場所の行を押すと、そこへ移動します");
            foreach(var v in view.visuals) if(v.role=="errorSummary" && v.label!=null) {v.label.textWrappingMode=TextWrappingModes.Normal;v.label.overflowMode=TextOverflowModes.Ellipsis;}
            view.issueList?.SetItems(issues.Select((e,i)=>new SkillSyncItemList.Item{id=i.ToString(),number=e.stepNumber,badge=e.stepNumber>0?null:"!",title=e.Line,detail=e.action,warn=true}).ToList());
            var fix=view.controls.FirstOrDefault(c=>c.action=="該当箇所を修正");
            if(fix!=null) UnityEngine.EventSystems.EventSystem.current?.SetSelectedGameObject(fix.button.gameObject);
            return;
        }
        if(result.outcome==TeachingMaterialExportWorkflow.Outcome.Exported)
        {
            string zip=TeachingMaterialArchive.GetPath(result.exportPath);
            string message="教材を書き出しました。XRアプリには「"+System.IO.Path.GetFileName(zip)+"」を渡してください"+caution;
            string dir=System.IO.Path.GetDirectoryName(result.exportPath);
            float seconds=caution.Length>0?24f:16f;
            if(string.IsNullOrEmpty(dir) || !System.IO.Path.IsPathRooted(dir))
                TopCenterNotification.Ensure(transform, view.inspectorTitle)?.Show(message, false, 8f);
            else
                TopCenterNotification.Ensure(transform, view.inspectorTitle)?.Show(message, false, seconds, ExportDestinationPicker.RevealLabel, ()=>ExportDestinationPicker.Reveal(zip));
        }
        else
        {
            TopCenterNotification.Ensure(transform, view.inspectorTitle)?.Show(status, true);
            if(result.exception!=null) Debug.LogException(result.exception);
        }
        RefreshValues();
    }
    void SelectIssue(string id)
    {
        if(int.TryParse(id,out int index) && index>=0 && index<issues.Count) JumpToIssue(issues[index]);
    }
    // Moves on from the problem last shown (or the first one), wrapping around; fixing one does not skip the next.
    void NextIssue()
    {
        if(issues.Count==0) return;
        int at=lastIssueKey==null?-1:issues.FindIndex(e=>IssueKey(e)==lastIssueKey);
        if(at>=0) at=(at+1)%issues.Count;
        else {at=lastIssueOrder<0?0:issues.FindIndex(e=>e.order>=lastIssueOrder);if(at<0) at=0;}
        JumpToIssue(issues[at]);
    }
    static string IssueKey(ValidationEntry e) => e.stepId+"|"+e.conditionId+"|"+e.Reason;
    // Opens the step (and condition) a problem belongs to in the step screen and says what to do there.
    void JumpToIssue(ValidationEntry issue)
    {
        EndTrial();CancelGhost();pickTarget=null;GetComponent<SkillSyncObjectPicker>()?.Close();
        lastIssueKey=IssueKey(issue);lastIssueOrder=issue.order;
        if(!string.IsNullOrEmpty(issue.stepId) && graph.FindNode(issue.stepId)!=null) {stepId=issue.stepId;conditionId=issue.conditionId;}
        mode=1;view.Show(1);
        GraphChanged();
        int position=issues.FindIndex(e=>IssueKey(e)==lastIssueKey)+1;
        string count=issues.Count>1 && position>0?"（"+position+"/"+issues.Count+"）":"";
        string next=issues.Count>1?"。直したら、上の「要修正」で次の問題へ移動できます":"";
        if((issue.field=="A" || issue.field=="B") && Condition!=null && Condition.nodeId==issue.conditionId)
        {
            BeginPick(issue.field);
            Notify(count+issue.Line+"。3D画面または「配置した部品」の一覧から選んでください"+next,true);
        }
        else
        {
            Notify(count+issue.Message+next,true);
        }
        RefreshValues();
    }
}
