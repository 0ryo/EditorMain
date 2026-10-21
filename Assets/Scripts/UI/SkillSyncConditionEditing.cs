using System;
using System.Globalization;
using System.Linq;

// Fixed UI policy; mutations are called inside CurriculumGraphService commands.
public static class SkillSyncConditionEditing
{
    public const string TypeActionPrefix = "ConditionType:";

    public static bool ChangeType(ConditionNodeData condition, string type, RuleSet rules)
    {
        if (condition == null || condition.type == type || !ConditionTypeCatalog.Definitions.Any(d => d.id == type)) return false;
        condition.type = type;
        // Retain inactive values and B so switching back and Undo do not lose work.
        ConditionTypeCatalog.Normalize(condition, rules);
        return true;
    }

    public static ConditionTypeCatalog.ParameterDefinition Parameter(ConditionNodeData condition, int slot)
    {
        var parameters = ConditionTypeCatalog.Find(condition?.type)?.parameters;
        return parameters != null && slot >= 0 && slot < parameters.Count ? parameters[slot] : null;
    }

    public static string Value(ConditionNodeData condition, int slot)
    {
        var parameter = Parameter(condition, slot);
        if (parameter == null) return "";
        float value = ConditionTypeCatalog.GetNumber(condition, parameter.key, parameter.defaultValue);
        return (parameter.key == ConditionTypeCatalog.DistanceKey ? value * 100 : value).ToString("0.##", CultureInfo.CurrentCulture);
    }

    public static bool TrySetValue(ConditionNodeData condition, int slot, string input) => TrySetValue(condition, slot, input, out _);

    // Numeric input is clamped to the parameter range; non-numeric input is rejected without mutation.
    public static bool TrySetValue(ConditionNodeData condition, int slot, string input, out bool clamped)
    {
        clamped = false;
        var parameter = Parameter(condition, slot);
        if (parameter == null || !float.TryParse(input, NumberStyles.Float, CultureInfo.CurrentCulture, out float value) ||
            float.IsNaN(value) || float.IsInfinity(value)) return false;
        if (parameter.key == ConditionTypeCatalog.DistanceKey) value /= 100;
        float bounded = Math.Max(parameter.minValue, Math.Min(parameter.maxValue, value));
        clamped = bounded != value;
        ConditionTypeCatalog.SetNumber(condition, parameter.key, bounded);
        return true;
    }

    public static string RangeHint(ConditionNodeData condition, int slot)
    {
        var parameter = Parameter(condition, slot);
        if (parameter == null) return "";
        bool distance = parameter.key == ConditionTypeCatalog.DistanceKey;
        float scale = distance ? 100 : 1;
        string unit = distance ? "cm" : parameter.key == ConditionTypeCatalog.AngleKey ? "度" : "秒";
        return (parameter.minValue * scale).ToString("0.##", CultureInfo.CurrentCulture) + "〜" +
            (parameter.maxValue * scale).ToString("0.##", CultureInfo.CurrentCulture) + " " + unit;
    }

    public static bool TargetsValid(ConditionNodeData condition, Func<string, bool> exists)
    {
        if (condition == null || ConditionTypeCatalog.Find(condition.type) == null ||
            string.IsNullOrEmpty(condition.objectAId) || !exists(condition.objectAId)) return false;
        return !ConditionTypeCatalog.RequiresObjectB(condition.type) ||
            (!string.IsNullOrEmpty(condition.objectBId) && condition.objectAId != condition.objectBId && exists(condition.objectBId));
    }

    public static bool SupportsPcTrial(ConditionNodeData condition) =>
        condition != null && (condition.type == ConditionTypeCatalog.Proximity || condition.type == ConditionTypeCatalog.SnapHold);

    public const string PcUnsupportedText = "PCでは自動判定できません";

    // Distances are measured between the bounding-box centres of the two parts.
    public static bool UsesCenterDistance(ConditionNodeData condition) =>
        condition != null && (condition.type == ConditionTypeCatalog.Proximity || condition.type == ConditionTypeCatalog.SnapHold);

    // Two solid parts cannot be closer than their sizes allow. The smallest axis-wise sum of half sizes
    // is the nearest they can stand side by side; far below it the condition cannot be met in practice.
    public static float MinimumCenterDistance(float ax, float ay, float az, float bx, float by, float bz) =>
        Math.Min(ax + bx, Math.Min(ay + by, az + bz));

    public static bool DistanceLikelyUnreachable(float limitMeters, float minimumMeters) =>
        minimumMeters > 0 && limitMeters < minimumMeters * .5f;

    public static string DistanceWarning(float limitMeters, float minimumMeters, string markerHint)
    {
        if (!DistanceLikelyUnreachable(limitMeters, minimumMeters)) return null;
        return $"距離{limitMeters * 100:0.##} cmは小さすぎて、成立しない可能性があります（中心どうしは約{minimumMeters * 100:0} cm以上離れます）。" +
            "場所を指定するには、" + (string.IsNullOrEmpty(markerHint) ? "目印の部品" : markerHint) + "をその場所に置き、そこへ近づけてください。";
    }

    // One-line description shown under the condition title in the step's condition list.
    public static string Detail(ConditionNodeData condition)
    {
        string distance = Value(condition, 0), hold = Value(condition, 1);
        return condition?.type switch
        {
            ConditionTypeCatalog.Proximity => $"{distance} cm以内に近づけると完了",
            ConditionTypeCatalog.SnapHold => $"{distance} cm以内に{hold}秒とどまると完了",
            ConditionTypeCatalog.Turn => $"{distance}度以上回すと完了",
            _ => ""
        };
    }

    public static string Summary(ConditionNodeData condition)
    {
        string distance = Value(condition, 0), hold = Value(condition, 1);
        return condition?.type switch
        {
            ConditionTypeCatalog.Proximity => $"近づける：{distance} cm以内で完了",
            ConditionTypeCatalog.SnapHold => $"近づけて保持：{distance} cm以内で{hold}秒保持",
            ConditionTypeCatalog.Push => "押す：対象への押す操作で完了",
            ConditionTypeCatalog.Pull => "引く：対象への引く操作で完了",
            ConditionTypeCatalog.Grabbed => "もつ：対象をつかむと完了",
            ConditionTypeCatalog.Turn => $"回す：対象を{distance}度以上回すと完了",
            ConditionTypeCatalog.Separation => $"離す：{distance} cm以上で完了",
            ConditionTypeCatalog.RotationMatch => $"向きをそろえる：{distance}度以内で{hold}秒保持",
            ConditionTypeCatalog.Released => "つかんで手放す：この手順中につかんだ対象を手放す",
            _ => "未対応の条件：" + condition?.type
        };
    }
}
