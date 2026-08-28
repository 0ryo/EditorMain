using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class EditorProjectFile
{
    public const int CurrentSchemaVersion = 2;

    public int schemaVersion = CurrentSchemaVersion;
    public string projectName = "VRCourseEditor";
    public string savedAtUtc;
    public Curriculum curriculum = new Curriculum();
    public List<EditorProjectObject> objects = new List<EditorProjectObject>();
}

[Serializable]
public sealed class EditorProjectObject
{
    public string id;
    public string typeId;
    public string displayName;
    public string description;
    public bool hasDescriptionOverride;
    public Vector3 position;
    public Quaternion rotation = Quaternion.identity;
    public Vector3 scale = Vector3.one;
    public bool hidden;
    public bool locked;
}

public static class EditorProjectMigration
{
    [Serializable]
    sealed class SchemaEnvelope
    {
        public int schemaVersion;
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
            project = JsonUtility.FromJson<EditorProjectFile>(json);
            if (envelope == null || envelope.schemaVersion <= 0)
            {
                project.schemaVersion = 1;
            }
        }
        catch (Exception ex)
        {
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

        if (project.schemaVersion == 1)
        {
            MigrateV1ToV2(project);
        }

        Normalize(project);
        project.schemaVersion = EditorProjectFile.CurrentSchemaVersion;
        return true;
    }

    static void MigrateV1ToV2(EditorProjectFile project)
    {
        // v2 で追加した表示名・説明・表示/ロック状態は、未設定なら既定値のまま維持する。
        project.savedAtUtc ??= string.Empty;
        project.schemaVersion = 2;
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
        project.objects ??= new List<EditorProjectObject>();

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
