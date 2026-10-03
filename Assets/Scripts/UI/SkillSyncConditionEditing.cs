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

    public static bool TrySetValue(ConditionNodeData condition, int slot, string input)
    {
        var parameter = Parameter(condition, slot);
        if (parameter == null || !float.TryParse(input, NumberStyles.Float, CultureInfo.CurrentCulture, out float value) ||
            float.IsNaN(value) || float.IsInfinity(value)) return false;
        if (parameter.key == ConditionTypeCatalog.DistanceKey) value /= 100;
        if (value < parameter.minValue || value > parameter.maxValue) return false;
        ConditionTypeCatalog.SetNumber(condition, parameter.key, value);
        return true;
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
