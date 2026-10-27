using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Hover captions for the fixed UI's buttons whose label is only a symbol or whose effect is not obvious from the name
// (the undo/redo arrows, the view buttons, pause). Built at runtime so UIRoot.prefab stays untouched.
public static class SkillSyncButtonTips
{
    public static void Apply(SkillSyncDesignView view, TMP_FontAsset font)
    {
        if (view?.controls == null) return;
        string m = SkillSyncDesignView.ShortcutModifierName;
        string alt = m == "Cmd" ? "Option" : "Alt";
        var tips = new Dictionary<string, string>
        {
            { "↶ 元に戻す", "直前の操作を元に戻します（" + m + "+Z）" },
            { "↷", "元に戻した操作をやり直します（" + m + "+Shift+Z）" },
            { "全体を見る", "置いた部品がすべて見えるように視点を動かします（Home）" },
            { "上から見る", "真上から見ます（7）" },
            { "Ⅱ 一時停止", "動作確認を一時停止／再開します（Esc）" },
            { "初期配置に戻す", "部品をこの手順の始めの位置に戻して、この手順だけもう一度確認します" },
            { "最初から試す", "部品を編集中の配置に戻して、手順1から最後まで通して確認します" },
            { "詳細設定", "視点の感度や、吸着の間隔・回転の刻みを変えます" },
            { "Snap", "吸着のオン／オフを切り替えます（" + alt + "を押している間は一時的にオフ）" },
            { "ヘルプ", "教材を作る流れと、操作のしかたを表示します" },
            { "通知履歴", "これまでのお知らせを表示します" },
            { "↑ 上へ", "選んだ手順を1つ上へ動かします" },
            { "↓ 下へ", "選んだ手順を1つ下へ動かします" },
            { "複製", "選んだ部品を複製します（" + m + "+D）" },
            { "削除", "選んだ部品を削除します（Delete。元に戻せます）" },
        };
        foreach (var control in view.controls)
        {
            if (control?.button == null || control.action == null || !tips.TryGetValue(control.action, out var text)) continue;
            Attach(control.button.gameObject, text, font);
        }
    }

    // Leaves a caption that something else already set (for example a reason a button is unavailable).
    public static void Attach(GameObject target, string text, TMP_FontAsset font)
    {
        if (target == null) return;
        var tip = target.GetComponent<SkillSyncHoverTip>();
        if (tip == null) tip = target.AddComponent<SkillSyncHoverTip>();
        else if (!string.IsNullOrEmpty(tip.text)) return;
        if (target.GetComponent<EditorUiInputBlocker>() == null) target.AddComponent<EditorUiInputBlocker>();
        tip.font = font;
        tip.text = text;
    }
}
