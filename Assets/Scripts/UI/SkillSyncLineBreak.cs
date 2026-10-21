using System.Text;
using TMPro;
using UnityEngine;

// TMP may break Japanese between any two characters ("変わりま／せん", "近／づける先"). This keeps each phrase
// (a run of kanji/katakana/latin plus the hiragana after it) on one line and lets lines break only between phrases.
// The text the code sets and reads stays unchanged; only what TMP lays out is rewritten.
public sealed class SkillSyncLineBreak : ITextPreprocessor
{
    readonly TMP_Text owner;
    readonly StringBuilder builder = new StringBuilder();

    SkillSyncLineBreak(TMP_Text owner) { this.owner = owner; }

    // Attaches to wrapping labels under root that have none yet. Text inside input fields is never touched (caret positions).
    public static void Attach(Transform root)
    {
        if (root == null) return;
        foreach (var text in root.GetComponentsInChildren<TMP_Text>(true)) Attach(text);
    }

    public static void Attach(TMP_Text text)
    {
        if (text == null || text.textPreprocessor != null || text.textWrappingMode == TextWrappingModes.NoWrap) return;
        if (text.GetComponentInParent<TMP_InputField>(true) != null) return;
        text.textPreprocessor = new SkillSyncLineBreak(text);
        // <nobr> needs rich text. Text that contains '<' is passed through unchanged, and a label that code
        // later switches back to plain text is left alone, so no tag ever shows as characters.
        text.richText = true;
    }

    public string PreprocessText(string text)
    {
        if (string.IsNullOrEmpty(text) || owner == null || !owner.richText || text.IndexOf('<') >= 0 || !HasJapanese(text)) return text;
        builder.Clear();
        int i = 0;
        while (i < text.Length)
        {
            int start = i;
            while (i < text.Length && Kind(text[i]) == 1) i++;          // kanji, katakana, latin, digits
            while (i < text.Length && Kind(text[i]) == 2) i++;          // the hiragana that follow
            if (i == start)
            {
                // Punctuation, spaces, line breaks. TMP does not break after 。、」 by itself, so a break is offered there too.
                char c = text[i++];
                builder.Append(c);
                if (i < text.Length && Kind(text[i]) != 0 && "「『（［【〈《(".IndexOf(c) < 0 && !char.IsWhiteSpace(c)) builder.Append('\u200B');
                continue;
            }
            if (i - start == 1) builder.Append(text[start]);
            else builder.Append("<nobr>").Append(text, start, i - start).Append("</nobr>");
            // A break is allowed only here, between phrases (TMP's own rules still keep 、。」 off a line start).
            if (i < text.Length && Kind(text[i]) != 0) builder.Append('\u200B');
        }
        return builder.ToString();
    }

    // 1: starts or continues a phrase, 2: hiragana (ends a phrase), 0: anything else.
    static int Kind(char c)
    {
        if (c >= 'ぁ' && c <= 'ゟ') return 2;
        if (c >= '゠' && c <= 'ヿ' || c >= '一' && c <= '鿿' || c == '々') return 1;
        if (c >= 'A' && c <= 'Z' || c >= 'a' && c <= 'z' || c >= '0' && c <= '9' || c >= '０' && c <= 'ｚ') return 1;
        return 0;
    }

    static bool HasJapanese(string text)
    {
        foreach (char c in text) if (c >= 'ぁ' && c <= '鿿') return true;
        return false;
    }
}
