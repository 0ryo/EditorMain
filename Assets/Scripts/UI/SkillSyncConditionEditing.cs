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
        var parameters = ConditionTypeCatalog.Find(condition?.type)?.parameters?.Where(p => p.choices == null).ToList();
        return parameters != null && slot >= 0 && slot < parameters.Count ? parameters[slot] : null;
    }

    // The choice parameter (turn direction) has no numeric field; the editor shows it as buttons.
    public static ConditionTypeCatalog.ParameterDefinition ChoiceParameter(ConditionNodeData condition) =>
        ConditionTypeCatalog.Find(condition?.type)?.parameters?.FirstOrDefault(p => p.choices != null);

    public static int Choice(ConditionNodeData condition)
    {
        var parameter = ChoiceParameter(condition);
        return parameter == null ? 0 : (int)ConditionTypeCatalog.GetNumber(condition, parameter.key, parameter.defaultValue);
    }

    public static string ChoiceLabel(ConditionNodeData condition)
    {
        var parameter = ChoiceParameter(condition);
        return parameter == null ? "" : parameter.choices[Math.Max(0, Math.Min(parameter.choices.Length - 1, Choice(condition)))];
    }

    public static string Value(ConditionNodeData condition, int slot)
    {
        var parameter = Parameter(condition, slot);
        if (parameter == null) return "";
        float value = ConditionTypeCatalog.GetNumber(condition, parameter.key, parameter.defaultValue);
        return (parameter.key == ConditionTypeCatalog.DistanceKey ? value * 100 : value).ToString("0.##", CultureInfo.CurrentCulture);
    }

    public static void SetChoice(ConditionNodeData condition, int index)
    {
        var parameter = ChoiceParameter(condition);
        if (parameter != null) ConditionTypeCatalog.SetNumber(condition, parameter.key, index);
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

    public static string DistanceWarning(float limitMeters, float minimumMeters)
    {
        if (!DistanceLikelyUnreachable(limitMeters, minimumMeters)) return null;
        return $"{limitMeters * 100:0.##} cmは近すぎて成立しない可能性があります。位置を示すには目印の部品を置き、そこへ近づけます";
    }

    // What the player must do for the condition to be met, as the runtime judges it (ScenarioConditionEvaluator).
    public static string Criteria(ConditionNodeData condition)
    {
        string distance = Value(condition, 0), hold = Value(condition, 1);
        return condition?.type switch
        {
            ConditionTypeCatalog.Proximity => $"2つの部品の中心どうしが{distance} cm以内になると完了します（3D画面の球が判定範囲）",
            ConditionTypeCatalog.SnapHold => $"中心どうしが{distance} cm以内の状態を{hold}秒続けると完了します。部品をもつ必要はなく、範囲を出ると数え直します",
            ConditionTypeCatalog.Push => "対象を押す操作が1回あると完了します（何を押す操作とみなすかはXRアプリ側で判定します）",
            ConditionTypeCatalog.Pull => "対象を引く操作が1回あると完了します（何を引く操作とみなすかはXRアプリ側で判定します）",
            ConditionTypeCatalog.Grabbed => "対象をつかんだ時点で完了します。運ぶ動きは判定しないので、運ぶ場合は「近づけて保持」を使います",
            ConditionTypeCatalog.Released => "対象をつかんだあと、手放すと完了します。最初からつかんでいないだけでは完了しません",
            ConditionTypeCatalog.Turn => $"対象を{distance}度以上回すと完了します。" + (Choice(condition) == 0 ? "向きは問いません（XRアプリ側で判定します）" : ChoiceLabel(condition) + "だけが数えられます（XRアプリ側で判定します）"),
            ConditionTypeCatalog.Separation => $"2つの部品の中心どうしが{distance} cm以上離れると完了します",
            ConditionTypeCatalog.RotationMatch => $"2つの部品の向きの差が{distance}度以内の状態を{hold}秒続けると完了します",
            _ => ""
        };
    }

    // One-line description shown under the condition title in the step's condition list.
    public static string Detail(ConditionNodeData condition)
    {
        string distance = Value(condition, 0), hold = Value(condition, 1);
        return condition?.type switch
        {
            ConditionTypeCatalog.Proximity => $"{distance} cm以内に近づけると完了",
            ConditionTypeCatalog.SnapHold => $"{distance} cm以内に{hold}秒とどまると完了（もつ必要なし）",
            ConditionTypeCatalog.Turn => $"{distance}度以上回すと完了（{ChoiceLabel(condition)}）",
            ConditionTypeCatalog.Push => "押す操作で完了",
            ConditionTypeCatalog.Pull => "引く操作で完了",
            ConditionTypeCatalog.Grabbed => "つかむと完了",
            ConditionTypeCatalog.Released => "つかんだあと手放すと完了",
            _ => ""
        };
    }

    public static string Summary(ConditionNodeData condition)
    {
        string distance = Value(condition, 0), hold = Value(condition, 1);
        return condition?.type switch
        {
            ConditionTypeCatalog.Proximity => $"近づける：{distance} cm以内で完了",
            ConditionTypeCatalog.SnapHold => $"近づけて保持：{distance} cm以内に{hold}秒とどまると完了",
            ConditionTypeCatalog.Push => "押す：対象を押す操作で完了",
            ConditionTypeCatalog.Pull => "引く：対象を引く操作で完了",
            ConditionTypeCatalog.Grabbed => "もつ：対象をつかむと完了",
            ConditionTypeCatalog.Turn => $"回す：対象を{distance}度以上回すと完了（{ChoiceLabel(condition)}）",
            ConditionTypeCatalog.Separation => $"離す：{distance} cm以上で完了",
            ConditionTypeCatalog.RotationMatch => $"向きをそろえる：{distance}度以内の状態を{hold}秒続けると完了",
            ConditionTypeCatalog.Released => "手放す：つかんだ対象を手放すと完了",
            _ => "未対応の条件：" + condition?.type
        };
    }
}
