using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class EditorProjectFile
{
    public const int CurrentSchemaVersion = 6;

    public int schemaVersion = CurrentSchemaVersion;
    public string projectName = "VRCourseEditor";
    public string savedAtUtc;
    public bool lastSaveWasAutomatic;
    public Curriculum curriculum = new Curriculum();
    public List<EditorProjectObject> objects = new List<EditorProjectObject>();
}

[Serializable]
public sealed class EditorProjectObject
{
    public string sourceNodePath;
    public string sourceSignature;
    public List<ModelPartState> parts = new List<ModelPartState>();
    public string id;
    public string typeId;
    public string displayName;
    public string description;
    public string editorGroupId;
    public bool hasDescriptionOverride;
    public Vector3 position;
    public Quaternion rotation = Quaternion.identity;
    public Vector3 scale = Vector3.one;
    public bool hidden;
    public bool locked;
}

// Keeps irreversible disposal outside the rollback-capable apply phase.
public static class EditorProjectReplacement
{
    public static void Run(Action apply, Action commit, params Action[] rollback)
    {
        try { apply(); }
        catch (Exception failure)
        {
            var errors = new List<Exception> { failure };
            foreach (var restore in rollback)
            {
                try { restore(); }
                catch (Exception error) { errors.Add(error); }
            }
            if (errors.Count > 1) throw new AggregateException("教材の切替と復元処理でエラーが発生しました。", errors);
            throw;
        }
        commit();
    }
}

public static class EditorProjectMigration
{
    [Serializable]
    sealed class SchemaEnvelope
    {
        public int schemaVersion;
        // No initializers: distinguish missing collections from intentionally empty ones.
        public CurriculumEnvelope curriculum;
        public List<EditorProjectObject> objects;
    }

    [Serializable]
    sealed class CurriculumEnvelope
    {
        public List<ScenarioNode> nodes;
        public List<ScenarioEdge> edges;
    }

    public static bool TryRead(string json, out EditorProjectFile project, out string error)
    {
        project = null;
        error = null;

        if (string.IsNullOrWhiteSpace(json))
        {
            error = "プロジェクトファイルが空です。";
            return false;
        }

        try
        {
            var envelope = JsonUtility.FromJson<SchemaEnvelope>(json);
            if (envelope != null && envelope.schemaVersion >= 5 &&
                envelope.schemaVersion <= EditorProjectFile.CurrentSchemaVersion &&
                (envelope.objects == null || envelope.curriculum == null ||
                 envelope.curriculum.nodes == null || envelope.curriculum.edges == null))
            {
                error = "教材の配置またはシナリオデータが欠損しています。現在の編集内容は変更しません。";
                return false;
            }
            project = JsonUtility.FromJson<EditorProjectFile>(json);
            if (project != null && (envelope == null || envelope.schemaVersion <= 0))
            {
                project.schemaVersion = 1;
            }
        }
        catch (Exception ex)
        {
            project = null;
            error = "JSONを読み取れません: " + ex.Message;
            return false;
        }

        if (project == null)
        {
            error = "プロジェクトデータを読み取れません。";
            return false;
        }

        // schemaVersion が無い初期試作ファイルは v1 として扱う。
        if (project.schemaVersion <= 0) project.schemaVersion = 1;
        if (project.schemaVersion > EditorProjectFile.CurrentSchemaVersion)
        {
            error = $"このプロジェクトは新しい形式です (v{project.schemaVersion})。";
            project = null;
            return false;
        }

        if (project.curriculum != null && project.curriculum.schemaVersion > 5)
        {
            error = $"この教材は新しい形式です (v{project.curriculum.schemaVersion})。";
            project = null;
            return false;
        }

        if (!ValidateReadData(project, out error))
        {
            project = null;
            return false;
        }

        if (project.schemaVersion == 1)
        {
            MigrateV1ToV2(project);
        }

        if (project.schemaVersion == 2)
        {
            MigrateV2ToV3(project);
        }

        if (project.schemaVersion == 3)
        {
            MigrateV3ToV4(project);
        }

        Normalize(project);
        project.schemaVersion = EditorProjectFile.CurrentSchemaVersion;
        return true;
    }

    static bool ValidateReadData(EditorProjectFile project, out string error)
    {
        error = null;
        foreach (var item in project.objects ?? new List<EditorProjectObject>())
        {
            if (item == null || !Finite(item.position) || !Finite(item.rotation) || !Finite(item.scale))
            {
                error = "配置データが欠損しているか、位置・回転・大きさに無効な数値があります。";
                return false;
            }
            foreach (var part in item.parts ?? new List<ModelPartState>())
            {
                if (part == null || !Finite(part.localPosition) || !Finite(part.localRotation) || !Finite(part.localScale))
                {
                    error = "部品データが欠損しているか、位置・回転・大きさに無効な数値があります。";
                    return false;
                }
            }
        }
        var curriculum = project.curriculum;
        if (curriculum == null) return true; // Legacy migration may supply this.
        if (curriculum.rules != null && (!Finite(curriculum.rules.proximityDistance) || !Finite(curriculum.rules.holdSeconds)))
        {
            error = "教材の距離または保持時間に無効な数値があります。";
            return false;
        }
        foreach (var node in curriculum.nodes ?? new List<ScenarioNode>())
        {
            if (node == null || !Enum.IsDefined(typeof(ScenarioNodeType), node.nodeType))
            {
                error = "シナリオノードが欠損しているか、未対応の種類です。";
                return false;
            }
            foreach (var parameter in node.condition?.parameters ?? new List<ConditionParameterData>())
            {
                if (parameter == null || !Finite(parameter.numberValue))
                {
                    error = "条件パラメーターが欠損しているか、無効な数値があります。";
                    return false;
                }
            }
        }
        foreach (var edge in curriculum.edges ?? new List<ScenarioEdge>())
        {
            if (edge == null || !Enum.IsDefined(typeof(ScenarioEdgeType), edge.edgeType))
            {
                error = "シナリオ接続が欠損しているか、未対応の種類です。";
                return false;
            }
        }

        // Current graph IDs are identity keys. A duplicate would make FindNode
        // resolve edges to an arbitrary node even when the draft is incomplete.
        if (project.schemaVersion >= 5 && !ValidateCurrentGraphReferences(project, out error))
        {
            return false;
        }

        // An unfinished graph (including unresolved references) remains editable.
        // Export validation, not project loading, decides whether it can be played.
        return true;
    }

    static bool ValidateCurrentGraphReferences(EditorProjectFile project, out string error)
    {
        error = null;
        var nodeIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var node in project.curriculum.nodes)
        {
            if (string.IsNullOrWhiteSpace(node.nodeId) || !nodeIds.Add(node.nodeId))
            {
                error = "シナリオノードIDが欠損または重複しています。";
                return false;
            }
        }

        var objectIdCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var item in project.objects)
        {
            AddIdCount(objectIdCounts, item.id?.Trim());
            foreach (var part in item.parts ?? new List<ModelPartState>())
            {
                AddIdCount(objectIdCounts, part.id);
            }
        }

        foreach (var node in project.curriculum.nodes)
        {
            if (node.nodeType != ScenarioNodeType.Condition || node.condition == null) continue;
            if (IsAmbiguousReference(node.condition.objectAId, objectIdCounts) ||
                IsAmbiguousReference(node.condition.objectBId, objectIdCounts))
            {
                error = "条件が重複した配置IDを参照しているため読み込めません。重複IDを解消してから再試行してください。";
                return false;
            }
        }

        return true;
    }

    static void AddIdCount(Dictionary<string, int> counts, string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return;
        counts.TryGetValue(id, out int count);
        counts[id] = count + 1;
    }

    static bool IsAmbiguousReference(string id, Dictionary<string, int> counts)
    {
        return !string.IsNullOrWhiteSpace(id) && counts.TryGetValue(id, out int count) && count > 1;
    }

    static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    static bool Finite(Vector3 value) => Finite(value.x) && Finite(value.y) && Finite(value.z);
    static bool Finite(Quaternion value) => Finite(value.x) && Finite(value.y) && Finite(value.z) && Finite(value.w);

    static void MigrateV1ToV2(EditorProjectFile project)
    {
        // v2 で追加した表示名・説明・表示/ロック状態は、未設定なら既定値のまま維持する。
        project.savedAtUtc ??= string.Empty;
        project.schemaVersion = 2;
    }

    static void MigrateV2ToV3(EditorProjectFile project)
    {
        // v3 adds optional Step metadata; absent values are normalized to empty/zero.
        if (project.curriculum != null) project.curriculum.schemaVersion = 3;
        project.schemaVersion = 3;
    }

    static void MigrateV3ToV4(EditorProjectFile project)
    {
        if (project.curriculum != null) project.curriculum.schemaVersion = 4;
        project.schemaVersion = 4;
    }

    public static void Normalize(EditorProjectFile project)
    {
        if (project == null) return;

        project.projectName = string.IsNullOrWhiteSpace(project.projectName)
            ? "VRCourseEditor"
            : project.projectName.Trim();
        project.curriculum ??= new Curriculum();
        project.curriculum.projectName = project.projectName;
        project.curriculum.rules ??= new RuleSet();
        project.curriculum.nodes ??= new List<ScenarioNode>();
        project.curriculum.edges ??= new List<ScenarioEdge>();
        project.curriculum.schemaVersion = 5;
        project.curriculum.rules.maxConditionsPerStep = Mathf.Clamp(
            project.curriculum.rules.maxConditionsPerStep <= 0 ? 8 : project.curriculum.rules.maxConditionsPerStep,
            1,
            32);
        project.objects ??= new List<EditorProjectObject>();

        foreach (var node in project.curriculum.nodes)
        {
            if (node == null) continue;
            node.step ??= new StepNodeData();
            node.step.title ??= string.Empty;
            node.step.body ??= string.Empty;
            node.step.supplement ??= string.Empty;
            node.step.caution ??= string.Empty;
            node.step.durationMinutes = Math.Max(0, node.step.durationMinutes);
            node.condition ??= new ConditionNodeData();
            if (node.nodeType == ScenarioNodeType.Condition)
            {
                ConditionTypeCatalog.Normalize(node.condition, project.curriculum.rules);
            }
        }

        foreach (var item in project.objects)
        {
            if (item == null) continue;
            item.id = item.id?.Trim();
            item.typeId = item.typeId?.Trim();
            item.displayName ??= string.Empty;
            item.description ??= string.Empty;
            if (item.rotation == default) item.rotation = Quaternion.identity;
        }
    }
}
