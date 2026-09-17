using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

public static class EditorProjectSnapshotBuilder
{
    public static EditorProjectFile Capture(CurriculumGraphService graph, string requestedName)
    {
        if (graph == null) throw new ArgumentNullException(nameof(graph));

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

        var placedObjects = UnityEngine.Object
            .FindObjectsByType<PlacedObject>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
            .Where(item => item != null && item.modelRoot == null)
            .OrderBy(item => item.id)
            .ToList();
        foreach (var placed in placedObjects)
        {
            placed.EnsureHasId();
            var editState = placed.GetComponent<PlacedObjectEditState>();
            project.objects.Add(new EditorProjectObject
            {
                id = placed.id,
                sourceNodePath = placed.sourceNodePath,
                sourceSignature = placed.sourceSignature,
                parts = ImportedModelParts.Capture(placed),
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
}

public static class EditorProjectFingerprint
{
    public static string BuildSelectedObject(PlacedObject placed)
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
}

public static class EditorProjectLoadPreparation
{
    public static bool Validate(EditorProjectFile project, PlacementController placementController, out string error)
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

            if (!placementController.TryGetPrefab(item.typeId, out var prefab))
            {
                error = $"現在のカタログにない種類を含んでいます: {item.typeId}";
                return false;
            }
            if (!ids.Add(item.id)) { error = "配置IDが重複しています。"; return false; }
            try
            {
                var source = ImportedModelParts.Resolve(prefab.transform, item.sourceNodePath);
                ImportedModelParts.ValidateSource(source, item.sourceSignature);
                var paths = new HashSet<string>(StringComparer.Ordinal);
                foreach (var part in item.parts ?? new List<ModelPartState>())
                {
                    if (part == null || string.IsNullOrWhiteSpace(part.id) || !ids.Add(part.id) ||
                        string.IsNullOrEmpty(part.nodePath) || !paths.Add(part.nodePath))
                        throw new InvalidOperationException("部品IDまたは対応情報が重複・欠損しています。");
                    ImportedModelParts.Resolve(source, part.nodePath);
                }
            }
            catch (Exception ex) { error = item.displayName + ": " + ex.Message; return false; }
        }

        return true;
    }

    public static int RepairObjectIds(EditorProjectFile project)
    {
        if (project?.objects == null) return 0;

        var reservedOriginalIds = new HashSet<string>(StringComparer.Ordinal);
        int nextSequence = 1;
        foreach (var item in project.objects)
        {
            string id = item?.id?.Trim();
            if (string.IsNullOrWhiteSpace(id)) continue;
            reservedOriginalIds.Add(id);
            if (id.StartsWith("obj-", StringComparison.Ordinal) &&
                int.TryParse(id.Substring(4), out int sequence))
            {
                nextSequence = Mathf.Max(nextSequence, sequence + 1);
            }
        }

        var usedIds = new HashSet<string>(StringComparer.Ordinal);
        int repairedCount = 0;
        foreach (var item in project.objects)
        {
            if (item == null) continue;

            string originalId = item.id?.Trim();
            if (!string.IsNullOrWhiteSpace(originalId) && usedIds.Add(originalId))
            {
                item.id = originalId;
                continue;
            }

            string replacement;
            do
            {
                replacement = $"obj-{nextSequence++:D4}";
            }
            while (reservedOriginalIds.Contains(replacement) || !usedIds.Add(replacement));

            item.id = replacement;
            repairedCount++;
        }

        return repairedCount;
    }
}
