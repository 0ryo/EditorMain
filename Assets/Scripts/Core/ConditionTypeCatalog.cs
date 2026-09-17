using System;
using System.Collections.Generic;
using System.Linq;

public static class ConditionTypeCatalog
{
    public const string SnapHold = "SnapHold";
    public const string Proximity = "Proximity";
    public const string Separation = "Separation";
    public const string RotationMatch = "RotationMatch";
    public const string Grabbed = "Grabbed";
    public const string Released = "Released";
    public const string Push = "Push";
    public const string Pull = "Pull";
    public const string Turn = "Turn";
    public const string AngleKey = "angleDegrees";
    public const string DistanceKey = "distanceMeters";
    public const string HoldSecondsKey = "holdSeconds";

    public sealed class ParameterDefinition
    {
        public string key;
        public string label;
        public float defaultValue;
        public float minValue;
        public float maxValue;
    }

    public sealed class Definition
    {
        public string id;
        public string label;
        public bool requiresObjectB = true;
        public IReadOnlyList<ParameterDefinition> parameters;
    }

    static readonly IReadOnlyList<Definition> definitions = new[]
    {
        new Definition
        {
            id = SnapHold,
            label = "近づけて保持",
            parameters = new[]
            {
                Parameter(DistanceKey, "距離 (m)", 0.1f, 0.001f, 10f),
                Parameter(HoldSecondsKey, "保持 (秒)", 1f, 0f, 600f)
            }
        },
        new Definition
        {
            id = Proximity,
            label = "近づける",
            parameters = new[]
            {
                Parameter(DistanceKey, "距離 (m)", 0.1f, 0.001f, 10f)
            }
        },
        new Definition
        {
            id = Separation, label = "離す",
            parameters = new[] { Parameter(DistanceKey, "最小距離 (m)", 0.3f, 0.001f, 100f) }
        },
        new Definition
        {
            id = RotationMatch, label = "向きをそろえる",
            parameters = new[] { Parameter(AngleKey, "角度の許容差 (度)", 10f, 0.1f, 180f),
                Parameter(HoldSecondsKey, "保持 (秒)", 1f, 0f, 600f) }
        },
        new Definition { id = Grabbed, label = "もつ", requiresObjectB = false, parameters = Array.Empty<ParameterDefinition>() },
        new Definition { id = Push, label = "押す", requiresObjectB = false, parameters = Array.Empty<ParameterDefinition>() },
        new Definition { id = Pull, label = "引く", requiresObjectB = false, parameters = Array.Empty<ParameterDefinition>() },
        new Definition { id = Turn, label = "回す", requiresObjectB = false,
            parameters = new[] { Parameter(AngleKey, "回す角度 (度)", 90f, 0.1f, 36000f) } },
        new Definition { id = Released, label = "つかんで手放す", requiresObjectB = false, parameters = Array.Empty<ParameterDefinition>() }
    };

    public static bool RequiresObjectB(string type) => Find(type)?.requiresObjectB != false;

    public static IReadOnlyList<Definition> Definitions => new[]
    { Find(Proximity), Find(SnapHold), Find(Push), Find(Pull), Find(Grabbed), Find(Turn) };

    public static Definition Find(string type)
    {
        return definitions.FirstOrDefault(item => string.Equals(item.id, type, StringComparison.Ordinal));
    }

    public static void Normalize(ConditionNodeData condition, RuleSet rules = null)
    {
        if (condition == null) return;
        if (string.IsNullOrWhiteSpace(condition.type))
        {
            condition.type = SnapHold;
        }

        condition.parameters ??= new List<ConditionParameterData>();
        var seenKeys = new HashSet<string>();
        condition.parameters.RemoveAll(item =>
            item == null ||
            string.IsNullOrWhiteSpace(item.key) ||
            !seenKeys.Add(item.key));

        var definition = Find(condition.type);
        if (definition == null) return;
        foreach (var parameter in definition.parameters)
        {
            if (condition.parameters.Any(item => item.key == parameter.key)) continue;
            float defaultValue = parameter.defaultValue;
            if (parameter.key == DistanceKey && rules != null && (condition.type == SnapHold || condition.type == Proximity))
                defaultValue = rules.proximityDistance;
            if (parameter.key == HoldSecondsKey && rules != null) defaultValue = rules.holdSeconds;
            condition.parameters.Add(new ConditionParameterData
            {
                key = parameter.key,
                numberValue = Clamp(parameter, defaultValue)
            });
        }

        foreach (var value in condition.parameters)
        {
            var parameter = definition.parameters.FirstOrDefault(item => item.key == value.key);
            if (parameter != null) value.numberValue = Clamp(parameter, value.numberValue);
            value.textValue ??= string.Empty;
        }
    }

    public static float GetNumber(ConditionNodeData condition, string key, float fallback = 0f)
    {
        var value = condition?.parameters?.FirstOrDefault(item => item != null && item.key == key);
        return value != null ? value.numberValue : fallback;
    }

    public static void SetNumber(ConditionNodeData condition, string key, float value)
    {
        if (condition == null || string.IsNullOrWhiteSpace(key)) return;
        condition.parameters ??= new List<ConditionParameterData>();
        var target = condition.parameters.FirstOrDefault(item => item != null && item.key == key);
        if (target == null)
        {
            target = new ConditionParameterData { key = key };
            condition.parameters.Add(target);
        }

        var definition = Find(condition.type);
        var parameter = definition?.parameters?.FirstOrDefault(item => item.key == key);
        target.numberValue = parameter != null ? Clamp(parameter, value) : value;
    }

    public static string BuildParameterSignature(ConditionNodeData condition)
    {
        if (condition?.parameters == null) return string.Empty;
        var definition = Find(condition.type);
        var activeKeys = definition?.parameters?.Select(item => item.key).ToHashSet();
        return string.Join(";", condition.parameters
            .Where(item => item != null && !string.IsNullOrWhiteSpace(item.key))
            .Where(item => activeKeys == null || activeKeys.Contains(item.key))
            .OrderBy(item => item.key)
            .Select(item => $"{item.key}:{item.numberValue}:{item.textValue}:{item.boolValue}"));
    }

    static ParameterDefinition Parameter(string key, string label, float defaultValue, float minValue, float maxValue)
    {
        return new ParameterDefinition
        {
            key = key,
            label = label,
            defaultValue = defaultValue,
            minValue = minValue,
            maxValue = maxValue
        };
    }

    static float Clamp(ParameterDefinition definition, float value)
    {
        if (float.IsNaN(value) || float.IsInfinity(value)) value = definition.defaultValue;
        return Math.Max(definition.minValue, Math.Min(definition.maxValue, value));
    }
}
