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
    // Turn direction as seen by the operator: 0 either, 1 clockwise, 2 counterclockwise.
    public const string TurnDirectionKey = "turnDirection";
    public const int TurnEither = 0, TurnClockwise = 1, TurnCounterclockwise = 2;

    public sealed class ParameterDefinition
    {
        public string key;
        public string label;
        public float defaultValue;
        public float minValue;
        public float maxValue;
        // Non-null for a choice parameter: the stored number is an index into these labels (no numeric field).
        public string[] choices;
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
            // Shown as one type with Proximity in the editor: holding for more than 0 seconds is stored as SnapHold.
            label = "近づける",
            parameters = new[]
            {
                // 0–100 cm: the XR side handles any distance in this range (even 0), so the editor never warns about it.
                Parameter(DistanceKey, "距離 (m)", 0.1f, 0f, 1f),
                Parameter(HoldSecondsKey, "近づけたまま (秒)", 1f, 0f, 600f)
            }
        },
        new Definition
        {
            id = Proximity,
            label = "近づける",
            parameters = new[]
            {
                Parameter(DistanceKey, "距離 (m)", 0.1f, 0f, 1f)
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
                Parameter(HoldSecondsKey, "そろえたまま (秒)", 1f, 0f, 600f) }
        },
        new Definition { id = Grabbed, label = "もつ", requiresObjectB = false, parameters = Array.Empty<ParameterDefinition>() },
        new Definition { id = Push, label = "押す", requiresObjectB = false, parameters = Array.Empty<ParameterDefinition>() },
        new Definition { id = Pull, label = "引く", requiresObjectB = false, parameters = Array.Empty<ParameterDefinition>() },
        new Definition { id = Turn, label = "回す", requiresObjectB = false,
            parameters = new[] { Parameter(AngleKey, "回す角度 (度)", 90f, 0.1f, 3600f),
                Choice(TurnDirectionKey, "回す向き", "どちらでも", "時計回り", "反時計回り") } },
        new Definition { id = Released, label = "手放す", requiresObjectB = false, parameters = Array.Empty<ParameterDefinition>() }
    };

    public static bool RequiresObjectB(string type) => Find(type)?.requiresObjectB != false;

    public static IReadOnlyList<Definition> Definitions => new[]
    { Find(Proximity), Find(SnapHold), Find(Push), Find(Pull), Find(Grabbed), Find(Released), Find(Turn) };

    // The dropdown's choices: "近づける" covers both Proximity and SnapHold (its hold time decides which is stored),
    // so the XR side keeps receiving the same two types.
    public static IReadOnlyList<Definition> EditorChoices => Definitions.Where(d => d.id != SnapHold).ToArray();
    public static string EditorChoiceOf(string type) => type == SnapHold ? Proximity : type;

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

    static ParameterDefinition Choice(string key, string label, params string[] choices)
    {
        var definition = Parameter(key, label, 0f, 0f, choices.Length - 1);
        definition.choices = choices;
        return definition;
    }

    static float Clamp(ParameterDefinition definition, float value)
    {
        if (float.IsNaN(value) || float.IsInfinity(value)) value = definition.defaultValue;
        value = Math.Max(definition.minValue, Math.Min(definition.maxValue, value));
        return value;
    }
}
