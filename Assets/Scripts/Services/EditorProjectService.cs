using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public sealed class EditorProjectService : MonoBehaviour
{
    public event Action<string, bool> StatusChanged;

    public string CurrentProjectPath { get; private set; }
    public string CurrentProjectName { get; private set; } = "VRCourseEditor";

    CurriculumGraphService graph;
    PlacementController placementController;
    SelectionService selectionService;

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
        ResolveReferences();
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
            var project = Capture(projectName);
            CurrentProjectPath = EditorProjectStore.Save(project, project.projectName);
            CurrentProjectName = project.projectName;
            if (!string.Equals(graph.curriculum.projectName, project.projectName, StringComparison.Ordinal))
            {
                graph.RestoreCommandSnapshot(JsonUtility.ToJson(project.curriculum));
            }
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
    }

    public bool Load(string path, out string message)
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
            foreach (var item in project.objects)
            {
                staged.Add(CreateStagedObject(item));
            }

            ReplaceCurrentProject(project, staged);
            CurrentProjectPath = System.IO.Path.GetFullPath(path);
            CurrentProjectName = project.projectName;
            message = $"読み込みました: {CurrentProjectName}";
            StatusChanged?.Invoke(message, true);
            Debug.Log($"[EditorProject] {message} ({CurrentProjectPath})");
            return true;
        }
        catch (Exception ex)
        {
            DestroyStaged(staged);
            Debug.LogException(ex);
            return Fail("読み込めません: " + ex.Message, out message);
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
            ReplaceCurrentProject(project, new List<PlacedObject>());
            CurrentProjectPath = null;
            CurrentProjectName = name;
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
        if (graph == null) graph = FindFirstObjectByType<CurriculumGraphService>();
        if (placementController == null) placementController = FindFirstObjectByType<PlacementController>();
        if (selectionService == null) selectionService = FindFirstObjectByType<SelectionService>();
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
