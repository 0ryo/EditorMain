using System.Collections.Generic;
using System.Linq;

// Plain-language names for the few captions the design file bakes in with older wording (see "用語" in .ai).
// Action keys stay as they are because the button wiring depends on them; only what the user reads changes.
public static class SkillSyncTerms
{
    static readonly Dictionary<string, string> Labels = new()
    {
        { "試行中の手順", "動作確認中の手順" },
        { "試行モード", "動作確認中" },
        { "動作のプレビュー", "3D画面（動作確認）" },
        { "3D空間または一覧から対象を選びます。", "3D画面または一覧から対象を選びます。" },
        { "下書きは保存されています。", "教材は保存されています。" },
    };

    public static void Apply(SkillSyncDesignView view)
    {
        if (view == null) return;
        view.SetCaption("＋ 3Dモデルを読み込む", "＋ 3Dモデルを追加");
        if (view.visuals == null) return;
        foreach (var visual in view.visuals.Where(v => v?.label != null))
            if (Labels.TryGetValue(visual.label.text, out var replacement)) visual.label.text = replacement;
    }
}
