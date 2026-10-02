using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Unity.Profiling;

public sealed class EditorProjectService : MonoBehaviour
{
    static readonly ProfilerMarker BuildCurrentFingerprintMarker = new("EditorProjectService.BuildCurrentFingerprint");
    static readonly ProfilerMarker SaveRecoveryIfChangedMarker = new("EditorProjectService.SaveRecoveryIfChanged");
    static readonly ProfilerMarker SaveRecoveryNowMarker = new("EditorProjectService.SaveRecoveryNow");

    const float SelectedObjectPollInterval = 0.2f;
    const string AutoSaveIntervalPlayerPrefsKey = "SkillSync.Editor.AutoSaveIntervalSeconds";

    public const float DefaultAutoSaveIntervalSeconds = 5f;
    public const float MinAutoSaveIntervalSeconds = 1f;
    public const float MaxAutoSaveIntervalSeconds = 120f;

    public event Action<string, bool> StatusChanged;
    public event Action<bool> DirtyChanged;
    public event Action RecoveryChanged;

    public string CurrentProjectPath { get; private set; }
    public string CurrentProjectName { get; private set; } = "VRCourseEditor";
    public bool IsDirty { get; private set; }
    public float AutoSaveIntervalSeconds => autoSaveInterval;

    [SerializeField, Min(MinAutoSaveIntervalSeconds)] float autoSaveInterval = DefaultAutoSaveIntervalSeconds;

    CurriculumGraphService graph;
    PlacementController placementController;
    SelectionService selectionService;
    CurriculumGraphService boundGraph;
    PlacementController boundPlacementController;
    CommandStack boundCommandStack;
    PlacedObject monitoredObject;
    string monitoredObjectFingerprint;
    string cleanFingerprint;
    string lastRecoveryFingerprint;
    float nextSelectedObjectPollAt;
    float nextAutoSaveAt;
    bool trackingInitialized;
    bool suppressTracking;
    readonly EditorProjectRecoverySession recoverySession = new EditorProjectRecoverySession();

    public static EditorProjectService Ensure(Transform host)
    {
        var existing = FindFirstObjectByType<EditorProjectService>();
        if (existing != null) return existing;

        var go = new GameObject("EditorProjectService");
        if (host != null) go.transform.SetParent(host, false);
        return go.AddComponent<EditorProjectService>();
    }

    void Awake()
    {
        autoSaveInterval = NormalizeAutoSaveInterval(PlayerPrefs.GetFloat(
            AutoSaveIntervalPlayerPrefsKey,
            DefaultAutoSaveIntervalSeconds));
        if (!EditorProjectStore.TryPreserveRecoveryAtStartup(out _, out var recoveryMigrationError))
        {
            Debug.LogWarning("[EditorProject] 自動保存を別の教材として保護できません: " + recoveryMigrationError);
        }
        ResolveReferences();
        PlacedObject.OnDisplayNameChanged += OnPlacedObjectMetadataChanged;
        PlacedObjectEditState.StateChanged += OnPlacedObjectStateChanged;
    }

    public void SetAutoSaveInterval(float seconds)
    {
        autoSaveInterval = NormalizeAutoSaveInterval(seconds);
        PlayerPrefs.SetFloat(AutoSaveIntervalPlayerPrefsKey, autoSaveInterval);
        PlayerPrefs.Save();
        nextAutoSaveAt = Time.unscaledTime + autoSaveInterval;
    }

    static float NormalizeAutoSaveInterval(float seconds)
    {
        return Mathf.Clamp(
            Mathf.Round(seconds),
            MinAutoSaveIntervalSeconds,
            MaxAutoSaveIntervalSeconds);
    }

    void Start()
    {
        ResolveReferences();
        EstablishCleanBaseline();
        nextAutoSaveAt = Time.unscaledTime + autoSaveInterval;
    }

    void Update()
    {
        ResolveReferences();
        MonitorSelectedObject();

        if (trackingInitialized && IsDirty && Time.unscaledTime >= nextAutoSaveAt)
        {
            SaveRecoveryIfChanged();
            nextAutoSaveAt = Time.unscaledTime + autoSaveInterval;
        }
    }

    void OnDestroy()
    {
        UnbindGraph();
        UnbindPlacementController();
        UnbindCommandStack();
        PlacedObject.OnDisplayNameChanged -= OnPlacedObjectMetadataChanged;
        PlacedObjectEditState.StateChanged -= OnPlacedObjectStateChanged;
    }

    public bool Save(string projectName, out string message)
    {
        ResolveReferences();
        if (graph == null)
        {
            return Fail("シナリオデータが見つからないため保存できません。", out message);
        }

        try
        {
            suppressTracking = true;
            var project = EditorProjectSnapshotBuilder.Capture(graph, projectName);
            bool sameName = string.Equals(project.projectName, CurrentProjectName, StringComparison.Ordinal);
            CurrentProjectPath = sameName && !string.IsNullOrEmpty(CurrentProjectPath)
                ? EditorProjectStore.SaveExisting(project, CurrentProjectPath)
                : EditorProjectStore.Save(project, project.projectName);
            CurrentProjectName = project.projectName;
            if (!string.Equals(graph.curriculum.projectName, project.projectName, StringComparison.Ordinal))
            {
                graph.RestoreCommandSnapshot(JsonUtility.ToJson(project.curriculum));
            }
            recoverySession.DeleteOwned(out _);
            EstablishCleanBaseline();
            message = $"保存しました: {CurrentProjectName}";
            NotifyStatus(message, true);
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            return Fail("保存できません: " + ex.Message, out message);
        }
        finally
        {
            suppressTracking = false;
        }
    }

    public bool Load(string path, out string message)
    {
        return LoadInternal(path, false, out message);
    }

    public bool DuplicateSaved(string path, bool asTemplate, out string message)
    {
        try
        {
            string created = EditorProjectStore.Duplicate(path, asTemplate);
            message = (asTemplate ? "保存済み内容をテンプレート化しました: " : "保存済み内容を複製しました: ") + System.IO.Path.GetFileName(created);
            NotifyStatus(message, true);
            return true;
        }
        catch (Exception ex) { return Fail("複製できません: " + ex.Message, out message); }
    }

    public bool ArchiveSaved(string path, out string message)
    {
        try
        {
            if (!string.IsNullOrEmpty(CurrentProjectPath) && string.Equals(System.IO.Path.GetFullPath(path),
                System.IO.Path.GetFullPath(CurrentProjectPath), StringComparison.OrdinalIgnoreCase))
                return Fail("編集中の教材は削除できません。別の教材を開いてから削除してください。", out message);
            EditorProjectStore.Archive(path);
            message = "削除済みへ移しました。削除済み一覧から復元できます。";
            NotifyStatus(message, true);
            return true;
        }
        catch (Exception ex) { return Fail("削除できません: " + ex.Message, out message); }
    }

    public bool RestoreSaved(string path, out string message)
    {
        try
        {
            EditorProjectStore.RestoreArchived(path);
            message = "復元しました。同名の教材がある場合は番号付きで復元します。";
            NotifyStatus(message, true);
            return true;
        }
        catch (Exception ex) { return Fail("復元できません: " + ex.Message, out message); }
    }

    public bool LoadRecovery(out string message)
    {
        return LoadInternal(EditorProjectStore.RecoveryPath, true, out message);
    }

    bool LoadInternal(string path, bool isRecovery, out string message)
    {
        var catalog = FindFirstObjectByType<CatalogUI>();
        if (catalog != null && catalog.IsRestoringModels)
        {
            message = "保存済みモデルを復元中です。完了後にプロジェクトを開いてください。";
            return false;
        }

        ResolveReferences();
        if (graph == null || placementController == null)
        {
            return Fail("編集サービスが見つからないため読み込めません。", out message);
        }

        if (!EditorProjectStore.TryLoad(path, out var project, out var readError))
        {
            return Fail(readError, out message);
        }

        int repairedObjectIdCount = EditorProjectLoadPreparation.RepairObjectIds(project);
        if (repairedObjectIdCount > 0)
        {
            Debug.LogWarning($"[EditorProject] 読み込み時に配置オブジェクトIDを{repairedObjectIdCount}件修復しました。");
        }

        if (!EditorProjectLoadPreparation.Validate(project, placementController, out var validationError))
        {
            return Fail(validationError, out message);
        }

        var staged = new List<PlacedObject>();
        GameObject stagingRoot = null;
        bool committed = false;
        try
        {
            suppressTracking = true;
            stagingRoot = new GameObject("ProjectLoadStaging");
            stagingRoot.SetActive(false);
            foreach (var item in project.objects)
            {
                staged.Add(CreateStagedObject(item, stagingRoot.transform));
            }

            ReplaceCurrentProject(project, staged);
            committed = true;
            CurrentProjectPath = isRecovery ? null : System.IO.Path.GetFullPath(path);
            CurrentProjectName = project.projectName;
            if (isRecovery)
            {
                EstablishDirtyBaseline(true);
                message = $"自動保存から復元しました: {CurrentProjectName}";
            }
            else
            {
                recoverySession.DeleteOwned(out _);
                EstablishCleanBaseline();
                message = $"読み込みました: {CurrentProjectName}";
            }
            NotifyStatus(message, true);
            return true;
        }
        catch (Exception ex)
        {
            if (!committed) DestroyStaged(staged);
            Debug.LogException(ex);
            return Fail("読み込めません: " + ex.Message, out message);
        }
        finally
        {
            if (stagingRoot != null) Destroy(stagingRoot);
            suppressTracking = false;
        }
    }

    public bool NewProject(string projectName, out string message)
    {
        ResolveReferences();
        if (graph == null)
        {
            return Fail("シナリオデータが見つからないため新規作成できません。", out message);
        }

        string name = string.IsNullOrWhiteSpace(projectName) ? "VRCourseEditor" : projectName.Trim();
        var project = new EditorProjectFile
        {
            projectName = name,
            curriculum = new Curriculum { projectName = name },
            objects = new List<EditorProjectObject>()
        };

        try
        {
            suppressTracking = true;
            ReplaceCurrentProject(project, new List<PlacedObject>());
            CurrentProjectPath = null;
            CurrentProjectName = name;
            recoverySession.DeleteOwned(out _);
            EstablishDirtyBaseline(false);
            message = $"新規プロジェクトを作成しました: {name}";
            NotifyStatus(message, true);
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            return Fail("新規作成できません: " + ex.Message, out message);
        }
        finally
        {
            suppressTracking = false;
        }
    }

    PlacedObject CreateStagedObject(EditorProjectObject item, Transform stagingRoot)
    {
        if (!placementController.TryGetPrefab(item.typeId, out var prefab) || prefab == null)
        {
            throw new InvalidOperationException("Prefabが見つかりません: " + item.typeId);
        }

        var source = ImportedModelParts.Resolve(prefab.transform, item.sourceNodePath);
        var instance = Instantiate(source.gameObject, stagingRoot, false);
        try
        {
            instance.SetActive(false);
            instance.transform.SetPositionAndRotation(item.position, item.rotation);
            instance.transform.localScale = item.scale;

            var placed = instance.GetComponent<PlacedObject>();
            if (placed == null) placed = instance.AddComponent<PlacedObject>();
            placed.id = item.id;
            placed.typeId = item.typeId;
            placed.editorGroupId = item.editorGroupId;
            placed.displayName = item.displayName ?? string.Empty;
            placed.description = item.description ?? string.Empty;
            placed.hasDescriptionOverride = item.hasDescriptionOverride;
            placed.modelRoot = null;
            placed.sourceNodePath = item.sourceNodePath;
            placed.sourceSignature = item.sourceSignature;
            ImportedModelParts.Restore(placed, item.parts);
            return placed;
        }
        catch { instance.SetActive(false); Destroy(instance); throw; }
    }

    void ReplaceCurrentProject(EditorProjectFile project, List<PlacedObject> staged)
    {
        var stagedSet = new HashSet<PlacedObject>(staged);
        var current = FindObjectsByType<PlacedObject>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Where(placed => placed != null && placed.modelRoot == null && !stagedSet.Contains(placed)).ToArray();
        var activeStates = current.Select(placed => placed.gameObject.activeSelf).ToArray();
        var previousSelection = selectionService != null ? selectionService.Selected.ToArray() : Array.Empty<PlacedObject>();
        var previousCurriculum = graph.curriculum;
        var graphUi = FindFirstObjectByType<ScenarioGraphUI>();
        string nextGraph = JsonUtility.ToJson(project.curriculum);
        var objectDataById = project.objects
            .Where(item => item != null)
            .ToDictionary(item => item.id, StringComparer.Ordinal);
        // Prepare all fallible object state before hiding the current lesson.
        foreach (var placed in staged)
        {
            var item = objectDataById[placed.id];
            PlacedObjectPickability.EnsurePickable(placed, true);
            var state = placed.GetComponent<PlacedObjectEditState>();
            if (state == null) state = placed.gameObject.AddComponent<PlacedObjectEditState>();
            state.SetLocked(item.locked);
            state.SetVisible(!item.hidden);
            PlacedObject.ReserveExistingId(placed.id);
            foreach (var part in placed.GetComponentsInChildren<PlacedObject>(true))
                PlacedObject.ReserveExistingId(part.id);
        }

        EditorProjectReplacement.Run(
            () =>
            {
                selectionService?.Select(null);
                foreach (var placed in current) placed.gameObject.SetActive(false);
                foreach (var placed in staged)
                {
                    placed.transform.SetParent(null, true);
                    placed.gameObject.SetActive(true);
                }
                if (!graph.RestoreCommandSnapshot(nextGraph))
                    throw new InvalidOperationException("シナリオデータを復元できませんでした。");
                graphUi?.RebuildFromExternalChange();
            },
            () =>
            {
                // The new lesson is now accepted. Cleanup/listener errors cannot
                // roll back already disposed objects or discard the new lesson.
                Notify(() => CommandService.I?.Stack?.Clear());
                foreach (var placed in current) if (placed != null) Notify(() => Destroy(placed.gameObject));
            },
            () => { foreach (var placed in staged) if (placed != null) placed.gameObject.SetActive(false); },
            () => { for (int i = 0; i < current.Length; i++) if (current[i] != null) current[i].gameObject.SetActive(activeStates[i]); },
            () => { graph.curriculum = previousCurriculum; graph.EnsureGraphInitialized(); graph.NotifyGraphChanged(); },
            () => selectionService?.SelectMany(previousSelection),
            () => graphUi?.RebuildFromExternalChange());
    }

    public bool HasEditableContent()
    {
        ResolveReferences();
        if (FindObjectsByType<PlacedObject>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length > 0)
        {
            return true;
        }

        return graph?.curriculum?.nodes != null && graph.curriculum.nodes.Any(node =>
            node != null && node.nodeType != ScenarioNodeType.Start && node.nodeType != ScenarioNodeType.End);
    }

    void ResolveReferences()
    {
        var nextGraph = graph != null ? graph : FindFirstObjectByType<CurriculumGraphService>();
        if (nextGraph != boundGraph)
        {
            UnbindGraph();
            graph = nextGraph;
            boundGraph = nextGraph;
            if (boundGraph != null) boundGraph.GraphChanged += OnProjectContentChanged;
        }

        var nextPlacement = placementController != null
            ? placementController
            : FindFirstObjectByType<PlacementController>();
        if (nextPlacement != boundPlacementController)
        {
            UnbindPlacementController();
            placementController = nextPlacement;
            boundPlacementController = nextPlacement;
            if (boundPlacementController != null) boundPlacementController.ObjectPlaced += OnObjectPlaced;
        }

        if (selectionService == null) selectionService = FindFirstObjectByType<SelectionService>();

        var nextStack = CommandService.I != null ? CommandService.I.Stack : null;
        if (nextStack != boundCommandStack)
        {
            UnbindCommandStack();
            boundCommandStack = nextStack;
            if (boundCommandStack != null) boundCommandStack.HistoryChanged += OnProjectContentChanged;
        }
    }

    void UnbindGraph()
    {
        if (boundGraph != null) boundGraph.GraphChanged -= OnProjectContentChanged;
        boundGraph = null;
    }

    void UnbindPlacementController()
    {
        if (boundPlacementController != null) boundPlacementController.ObjectPlaced -= OnObjectPlaced;
        boundPlacementController = null;
    }

    void UnbindCommandStack()
    {
        if (boundCommandStack != null) boundCommandStack.HistoryChanged -= OnProjectContentChanged;
        boundCommandStack = null;
    }

    void OnObjectPlaced(PlacedObject _, string __)
    {
        OnProjectContentChanged();
    }

    void OnPlacedObjectMetadataChanged(PlacedObject _)
    {
        OnProjectContentChanged();
    }

    void OnPlacedObjectStateChanged(PlacedObjectEditState _)
    {
        OnProjectContentChanged();
    }

    void OnProjectContentChanged()
    {
        RecalculateDirtyState();
    }

    void MonitorSelectedObject()
    {
        if (!trackingInitialized || suppressTracking || Time.unscaledTime < nextSelectedObjectPollAt) return;
        nextSelectedObjectPollAt = Time.unscaledTime + SelectedObjectPollInterval;

        var selected = selectionService != null ? selectionService.Current : null;
        string fingerprint = EditorProjectFingerprint.BuildSelectedObject(selected);
        if (selected == monitoredObject)
        {
            if (!string.Equals(monitoredObjectFingerprint, fingerprint, StringComparison.Ordinal))
            {
                monitoredObjectFingerprint = fingerprint;
                RecalculateDirtyState();
            }
            return;
        }

        monitoredObject = selected;
        monitoredObjectFingerprint = fingerprint;
    }

    void EstablishCleanBaseline()
    {
        cleanFingerprint = BuildCurrentFingerprint();
        lastRecoveryFingerprint = null;
        trackingInitialized = !string.IsNullOrEmpty(cleanFingerprint);
        SetDirty(false);
        ResetSelectedObjectMonitor();
    }

    void EstablishDirtyBaseline(bool recoveryAlreadyExists)
    {
        string current = BuildCurrentFingerprint();
        cleanFingerprint = string.Empty;
        lastRecoveryFingerprint = recoveryAlreadyExists ? current : null;
        trackingInitialized = !string.IsNullOrEmpty(current);
        SetDirty(trackingInitialized);
        ResetSelectedObjectMonitor();
        nextAutoSaveAt = Time.unscaledTime + autoSaveInterval;
        Notify(() => RecoveryChanged?.Invoke());
    }

    void ResetSelectedObjectMonitor()
    {
        monitoredObject = selectionService != null ? selectionService.Current : null;
        monitoredObjectFingerprint = EditorProjectFingerprint.BuildSelectedObject(monitoredObject);
        nextSelectedObjectPollAt = Time.unscaledTime + SelectedObjectPollInterval;
    }

    void RecalculateDirtyState()
    {
        if (!trackingInitialized || suppressTracking || graph == null) return;
        string current = BuildCurrentFingerprint();
        if (string.IsNullOrEmpty(current)) return;
        SetDirty(!string.Equals(cleanFingerprint, current, StringComparison.Ordinal));
    }

    string BuildCurrentFingerprint()
    {
        using (BuildCurrentFingerprintMarker.Auto())
        {
            if (graph == null) return null;
            try
            {
                return JsonUtility.ToJson(EditorProjectSnapshotBuilder.Capture(graph, graph.curriculum.projectName));
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[EditorProject] 編集状態を確認できません: " + ex.Message);
                return null;
            }
        }
    }

    void SetDirty(bool value)
    {
        if (IsDirty == value) return;
        IsDirty = value;
        Notify(() => DirtyChanged?.Invoke(IsDirty));
        if (IsDirty) nextAutoSaveAt = Time.unscaledTime + autoSaveInterval;
    }

    void SaveRecoveryIfChanged()
    {
        using (SaveRecoveryIfChangedMarker.Auto())
        {
            SaveRecoveryIfChangedCore();
        }
    }

    void SaveRecoveryIfChangedCore()
    {
        string fingerprint = BuildCurrentFingerprint();
        if (string.IsNullOrEmpty(fingerprint) ||
            string.Equals(lastRecoveryFingerprint, fingerprint, StringComparison.Ordinal))
        {
            return;
        }

        SaveRecoveryNow(out _);
    }

    public bool SaveRecoveryNow(out string message)
    {
        using (SaveRecoveryNowMarker.Auto())
        {
            return SaveRecoveryNowCore(out message);
        }
    }

    bool SaveRecoveryNowCore(out string message)
    {
        message = null;
        if (!trackingInitialized || !IsDirty || graph == null)
        {
            message = "自動保存する未保存の変更はありません。";
            return false;
        }

        try
        {
            string fingerprint = BuildCurrentFingerprint();
            if (!string.IsNullOrWhiteSpace(CurrentProjectPath))
            {
                CurrentProjectPath = EditorProjectStore.SaveAutomatic(
                    EditorProjectSnapshotBuilder.Capture(graph, CurrentProjectName),
                    CurrentProjectPath);
                recoverySession.DeleteOwned(out _);
                EstablishCleanBaseline();
                message = "自動保存しました。";
                Notify(() => RecoveryChanged?.Invoke());
                Debug.Log("[EditorProject] " + message);
                return true;
            }

            recoverySession.Save(EditorProjectSnapshotBuilder.Capture(graph, graph.curriculum.projectName));
            lastRecoveryFingerprint = fingerprint;
            message = "復旧用の自動保存を更新しました。";
            Notify(() => RecoveryChanged?.Invoke());
            Debug.Log("[EditorProject] " + message);
            return true;
        }
        catch (Exception ex)
        {
            message = "自動保存できません: " + ex.Message;
            NotifyStatus(message, false);
            Debug.LogWarning("[EditorProject] " + message);
            return false;
        }
    }

    public bool DeleteRecovery(out string message)
    {
        if (!EditorProjectStore.DeleteRecovery(out var error))
        {
            return Fail("自動保存データを破棄できません: " + error, out message);
        }

        lastRecoveryFingerprint = null;
        message = "自動保存データを破棄しました。";
        Notify(() => RecoveryChanged?.Invoke());
        NotifyStatus(message, true);
        return true;
    }

    void NotifyStatus(string message, bool succeeded) => Notify(() => StatusChanged?.Invoke(message, succeeded));

    static void Notify(Action notification)
    {
        // UI listener failures must not turn a committed load/save into a failure.
        try { notification(); }
        catch (Exception ex) { Debug.LogException(ex); }
    }

    bool Fail(string error, out string message)
    {
        message = string.IsNullOrWhiteSpace(error) ? "操作に失敗しました。" : error;
        NotifyStatus(message, false);
        Debug.LogWarning("[EditorProject] " + message);
        return false;
    }

    static void DestroyStaged(IEnumerable<PlacedObject> staged)
    {
        foreach (var placed in staged)
        {
            if (placed != null) { placed.gameObject.SetActive(false); Destroy(placed.gameObject); }
        }
    }
}
