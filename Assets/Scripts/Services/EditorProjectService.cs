using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

public sealed class EditorProjectService : MonoBehaviour
{
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
            var project = Capture(projectName);
            CurrentProjectPath = EditorProjectStore.Save(project, project.projectName);
            CurrentProjectName = project.projectName;
            if (!string.Equals(graph.curriculum.projectName, project.projectName, StringComparison.Ordinal))
            {
                graph.RestoreCommandSnapshot(JsonUtility.ToJson(project.curriculum));
            }
            EditorProjectStore.DeleteRecovery(out _);
            EstablishCleanBaseline();
            message = $"保存しました: {CurrentProjectName}";
            StatusChanged?.Invoke(message, true);
            Debug.Log($"[EditorProject] {message} ({CurrentProjectPath})");
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

    public bool LoadRecovery(out string message)
    {
        return LoadInternal(EditorProjectStore.RecoveryPath, true, out message);
    }

    bool LoadInternal(string path, bool isRecovery, out string message)
    {
        ResolveReferences();
        if (graph == null || placementController == null)
        {
            return Fail("編集サービスが見つからないため読み込めません。", out message);
        }

        if (!EditorProjectStore.TryLoad(path, out var project, out var readError))
        {
            return Fail(readError, out message);
        }

        if (!ValidateProject(project, out var validationError))
        {
            return Fail(validationError, out message);
        }

        var staged = new List<PlacedObject>();
        try
        {
            suppressTracking = true;
            foreach (var item in project.objects)
            {
                staged.Add(CreateStagedObject(item));
            }

            ReplaceCurrentProject(project, staged);
            CurrentProjectPath = isRecovery ? null : System.IO.Path.GetFullPath(path);
            CurrentProjectName = project.projectName;
            if (isRecovery)
            {
                EstablishDirtyBaseline(true);
                message = $"自動保存から復元しました: {CurrentProjectName}";
            }
            else
            {
                EditorProjectStore.DeleteRecovery(out _);
                EstablishCleanBaseline();
                message = $"読み込みました: {CurrentProjectName}";
            }
            StatusChanged?.Invoke(message, true);
            Debug.Log($"[EditorProject] {message} ({path})");
            return true;
        }
        catch (Exception ex)
        {
            DestroyStaged(staged);
            Debug.LogException(ex);
            return Fail("読み込めません: " + ex.Message, out message);
        }
        finally
        {
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
            EditorProjectStore.DeleteRecovery(out _);
            EstablishDirtyBaseline(false);
            message = $"新規プロジェクトを作成しました: {name}";
            StatusChanged?.Invoke(message, true);
            Debug.Log("[EditorProject] " + message);
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

    EditorProjectFile Capture(string requestedName)
    {
        graph.EnsureGraphInitialized();
        string name = string.IsNullOrWhiteSpace(requestedName)
            ? graph.curriculum.projectName
            : requestedName.Trim();
        if (string.IsNullOrWhiteSpace(name)) name = "VRCourseEditor";

        var project = new EditorProjectFile
        {
            projectName = name,
            curriculum = JsonUtility.FromJson<Curriculum>(JsonUtility.ToJson(graph.curriculum)),
            objects = new List<EditorProjectObject>()
        };
        project.curriculum.projectName = name;

        var placedObjects = FindObjectsByType<PlacedObject>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
            .Where(item => item != null)
            .OrderBy(item => item.id)
            .ToList();
        foreach (var placed in placedObjects)
        {
            placed.EnsureHasId();
            var editState = placed.GetComponent<PlacedObjectEditState>();
            project.objects.Add(new EditorProjectObject
            {
                id = placed.id,
                typeId = placed.typeId,
                displayName = placed.displayName,
                description = placed.description,
                hasDescriptionOverride = placed.hasDescriptionOverride,
                position = placed.transform.position,
                rotation = placed.transform.rotation,
                scale = placed.transform.localScale,
                hidden = editState != null && editState.Hidden,
                locked = editState != null && editState.Locked
            });
        }

        return project;
    }

    bool ValidateProject(EditorProjectFile project, out string error)
    {
        error = null;
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in project.objects)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.id) || string.IsNullOrWhiteSpace(item.typeId))
            {
                error = "IDまたは種類がない配置オブジェクトを含んでいます。";
                return false;
            }

            if (!ids.Add(item.id))
            {
                error = $"配置オブジェクトIDが重複しています: {item.id}";
                return false;
            }

            if (!placementController.TryGetPrefab(item.typeId, out _))
            {
                error = $"現在のカタログにない種類を含んでいます: {item.typeId}";
                return false;
            }
        }

        return true;
    }

    PlacedObject CreateStagedObject(EditorProjectObject item)
    {
        if (!placementController.TryGetPrefab(item.typeId, out var prefab) || prefab == null)
        {
            throw new InvalidOperationException("Prefabが見つかりません: " + item.typeId);
        }

        var instance = Instantiate(prefab);
        instance.SetActive(false);
        instance.transform.SetPositionAndRotation(item.position, item.rotation);
        instance.transform.localScale = item.scale;

        var placed = instance.GetComponent<PlacedObject>();
        if (placed == null) placed = instance.AddComponent<PlacedObject>();
        placed.id = item.id;
        placed.typeId = item.typeId;
        placed.displayName = item.displayName ?? string.Empty;
        placed.description = item.description ?? string.Empty;
        placed.hasDescriptionOverride = item.hasDescriptionOverride;
        return placed;
    }

    void ReplaceCurrentProject(EditorProjectFile project, List<PlacedObject> staged)
    {
        selectionService?.Select(null);

        var current = FindObjectsByType<PlacedObject>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (var placed in current)
        {
            if (placed == null || staged.Contains(placed)) continue;
            placed.gameObject.SetActive(false);
            Destroy(placed.gameObject);
        }

        foreach (var placed in staged)
        {
            var item = project.objects.First(entry => entry != null && entry.id == placed.id);
            placed.gameObject.SetActive(true);
            PlacedObjectPickability.EnsurePickable(placed, true);
            var state = placed.GetComponent<PlacedObjectEditState>();
            if (state == null) state = placed.gameObject.AddComponent<PlacedObjectEditState>();
            state.SetLocked(item.locked);
            state.SetVisible(!item.hidden);
            IdGenerator.I?.ReserveExistingObjectId(placed.id);
        }

        if (!graph.RestoreCommandSnapshot(JsonUtility.ToJson(project.curriculum)))
        {
            throw new InvalidOperationException("シナリオデータを復元できませんでした。");
        }

        FindFirstObjectByType<ScenarioGraphUI>()?.RebuildFromExternalChange();
        CommandService.I?.Stack?.Clear();
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
        string fingerprint = BuildSelectedObjectFingerprint(selected);
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

    static string BuildSelectedObjectFingerprint(PlacedObject placed)
    {
        if (placed == null) return string.Empty;
        var transform = placed.transform;
        return string.Join("|",
            placed.id,
            placed.displayName,
            placed.description,
            placed.hasDescriptionOverride,
            FormatVector(transform.position),
            FormatQuaternion(transform.rotation),
            FormatVector(transform.localScale));
    }

    static string FormatVector(Vector3 value)
    {
        return string.Join(",", FormatFloat(value.x), FormatFloat(value.y), FormatFloat(value.z));
    }

    static string FormatQuaternion(Quaternion value)
    {
        return string.Join(",", FormatFloat(value.x), FormatFloat(value.y), FormatFloat(value.z), FormatFloat(value.w));
    }

    static string FormatFloat(float value)
    {
        return value.ToString("R", CultureInfo.InvariantCulture);
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
        RecoveryChanged?.Invoke();
    }

    void ResetSelectedObjectMonitor()
    {
        monitoredObject = selectionService != null ? selectionService.Current : null;
        monitoredObjectFingerprint = BuildSelectedObjectFingerprint(monitoredObject);
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
        if (graph == null) return null;
        try
        {
            return JsonUtility.ToJson(Capture(graph.curriculum.projectName));
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[EditorProject] 編集状態を確認できません: " + ex.Message);
            return null;
        }
    }

    void SetDirty(bool value)
    {
        if (IsDirty == value) return;
        IsDirty = value;
        DirtyChanged?.Invoke(IsDirty);
        if (IsDirty) nextAutoSaveAt = Time.unscaledTime + autoSaveInterval;
    }

    void SaveRecoveryIfChanged()
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
        message = null;
        if (!trackingInitialized || !IsDirty || graph == null)
        {
            message = "自動保存する未保存の変更はありません。";
            return false;
        }

        try
        {
            string fingerprint = BuildCurrentFingerprint();
            EditorProjectStore.SaveRecovery(Capture(graph.curriculum.projectName));
            lastRecoveryFingerprint = fingerprint;
            message = "復旧用の自動保存を更新しました。";
            RecoveryChanged?.Invoke();
            Debug.Log("[EditorProject] " + message);
            return true;
        }
        catch (Exception ex)
        {
            message = "自動保存できません: " + ex.Message;
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
        RecoveryChanged?.Invoke();
        StatusChanged?.Invoke(message, true);
        return true;
    }

    bool Fail(string error, out string message)
    {
        message = string.IsNullOrWhiteSpace(error) ? "操作に失敗しました。" : error;
        StatusChanged?.Invoke(message, false);
        Debug.LogWarning("[EditorProject] " + message);
        return false;
    }

    static void DestroyStaged(IEnumerable<PlacedObject> staged)
    {
        foreach (var placed in staged)
        {
            if (placed != null) Destroy(placed.gameObject);
        }
    }
}
