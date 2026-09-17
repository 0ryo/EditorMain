using System;
using System.Linq;
using UnityEngine;

internal static class ScenarioExportBuilder
{
    public static ScenarioExport Build(CurriculumGraphService graph)
    {
        var curriculum = graph.curriculum;
        if (!ScenarioFlow.TryOrder(curriculum, out var orderedSteps, out var reason))
        {
            throw new InvalidOperationException("Scenario export failed: " + reason);
        }

        var export = new ScenarioExport
        {
            version = 6,
            projectName = string.IsNullOrWhiteSpace(curriculum.projectName) ? "VRCourseEditor" : curriculum.projectName,
            scenarioSettings = new ScenarioSettingsExport
            {
                holdSeconds = curriculum.rules != null ? curriculum.rules.holdSeconds : 1.0f,
                snapDistance_m = curriculum.rules != null ? curriculum.rules.proximityDistance : 0.1f
            }
        };

        var actionIds = orderedSteps.Select((node, index) => new { node.nodeId, id = $"act-{index + 1:D3}" })
            .ToDictionary(item => item.nodeId, item => item.id);
        actionIds[graph.GetEndNode().nodeId] = ScenarioExport.EndActionId;
        export.startActionId = actionIds[ScenarioFlow.Next(curriculum, graph.GetStartNode().nodeId)[0].nodeId];

        for (int i = 0; i < orderedSteps.Count; i++)
        {
            var step = orderedSteps[i];
            var action = new RequiredActionExport
            {
                id = $"act-{(i + 1).ToString("D3")}",
                sourceNodeId = step.nodeId,
                nextActionIds = ScenarioFlow.Next(curriculum, step.nodeId).Select(node => actionIds[node.nodeId]).ToList(),
                name = string.IsNullOrWhiteSpace(step.step.title) ? $"\u624B\u9806 {i + 1}" : step.step.title.Trim(),
                body = step.step.body ?? string.Empty,
                supplement = step.step.supplement ?? string.Empty,
                caution = step.step.caution ?? string.Empty,
                durationMinutes = Math.Max(0, step.step.durationMinutes)
            };

            var conditions = graph.GetConditionNodesForStep(step.nodeId);
            foreach (var condition in conditions.OrderBy(c => c.nodeId))
            {
                var conditionDefinition = ConditionTypeCatalog.Find(condition.condition.type);
                if (conditionDefinition == null)
                {
                    throw new InvalidOperationException($"Scenario export failed: unsupported condition type '{condition.condition.type}'.");
                }

                action.conditions.Add(new ConditionExport
                {
                    type = condition.condition.type,
                    aObjectId = condition.condition.objectAId,
                    bObjectId = conditionDefinition.requiresObjectB ? condition.condition.objectBId : null,
                    holdSeconds = ConditionTypeCatalog.GetNumber(
                        condition.condition,
                        ConditionTypeCatalog.HoldSecondsKey,
                        export.scenarioSettings.holdSeconds),
                    distanceMeters = ConditionTypeCatalog.GetNumber(
                        condition.condition,
                        ConditionTypeCatalog.DistanceKey,
                        export.scenarioSettings.snapDistance_m),
                    parameters = condition.condition.parameters
                        .Where(parameter => parameter != null)
                        .Where(parameter => conditionDefinition.parameters
                            .Any(definition => definition.key == parameter.key))
                        .Select(parameter => new ConditionParameterExport
                        {
                            key = parameter.key,
                            numberValue = parameter.numberValue,
                            textValue = parameter.textValue,
                            boolValue = parameter.boolValue
                        })
                        .ToList()
                });
            }

            export.requiredActions.Add(action);
        }

        var placed = UnityEngine.Object.FindObjectsByType<PlacedObject>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
            .Where(p => p.modelRoot == null)
            .OrderBy(p => p.id)
            .ToList();
        foreach (var po in placed)
        {
            if (po == null) continue;
            po.EnsureHasId();

            export.objects.Add(new PlacementExportObject
            {
                id = po.id,
                sourceNodePath = po.sourceNodePath,
                sourceSignature = po.sourceSignature,
                parts = ImportedModelParts.Capture(po),
                typeId = po.typeId,
                position = po.transform.position,
                rotation = po.transform.rotation,
                scale = po.transform.localScale
            });
        }

        return export;
    }
}
