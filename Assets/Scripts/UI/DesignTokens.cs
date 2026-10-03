using UnityEngine;

/// <summary>
/// design_rule.md 準拠のデザイントークン定数クラス。
/// すべてのUIスクリプトはこのクラスの値を参照し、ハードコード色を排除する。
/// </summary>
public static class DesignTokens
{
    // ── 2. カラーシステム ──────────────────────────────

    enum PaletteColor
    {
        BgPrimary, BgSecondary, BgTertiary, Surface,
        TextPrimary, TextSecondary, TextTertiary,
        Accent, AccentHover, Success, Warning, Error, Divider,
        ButtonTextLight, DangerHover, DangerPress, AccentPress
    }

    static readonly Color[] StandardPalette =
    {
        new Color(0.969f, 0.969f, 0.973f, 1f), new Color(0.929f, 0.929f, 0.941f, 1f), new Color(0.886f, 0.886f, 0.906f, 1f), Color.white,
        new Color(0.114f, 0.114f, 0.122f, 1f), new Color(0.357f, 0.357f, 0.376f, 1f), new Color(0.384f, 0.384f, 0.404f, 1f),
        new Color(0.145f, 0.388f, 0.922f, 1f), new Color(0.114f, 0.306f, 0.847f, 1f), new Color(0.098f, 0.380f, 0.184f, 1f),
        new Color(0.490f, 0.271f, 0f, 1f), new Color(0.624f, 0.114f, 0.090f, 1f), new Color(0.820f, 0.820f, 0.839f, 1f),
        Color.white, new Color(0.561f, 0.106f, 0.078f, 1f), new Color(0.478f, 0.094f, 0.071f, 1f), new Color(0.118f, 0.251f, 0.686f, 1f)
    };

    static readonly Color[] HighContrastPalette =
    {
        new Color(0.98f, 0.98f, 0.98f, 1f), new Color(0.90f, 0.90f, 0.90f, 1f), new Color(0.78f, 0.78f, 0.78f, 1f), Color.white,
        Color.black, new Color(0.06f, 0.06f, 0.06f, 1f), new Color(0.12f, 0.12f, 0.12f, 1f),
        new Color(0f, 0f, 0.45f, 1f), new Color(0f, 0f, 0.30f, 1f), new Color(0f, 0.30f, 0.05f, 1f),
        new Color(0.36f, 0.16f, 0f, 1f), new Color(0.55f, 0f, 0f, 1f), new Color(0.15f, 0.15f, 0.15f, 1f),
        Color.white, new Color(0.38f, 0f, 0f, 1f), new Color(0.25f, 0f, 0f, 1f), new Color(0f, 0f, 0.20f, 1f)
    };

    public static bool HighContrastEnabled { get; private set; }
    public static void SetHighContrastEnabled(bool enabled) => HighContrastEnabled = enabled;

    static Color Get(PaletteColor color) => (HighContrastEnabled ? HighContrastPalette : StandardPalette)[(int)color];

    public static Color MapPaletteColor(Color color, bool toHighContrast)
    {
        var source = toHighContrast ? StandardPalette : HighContrastPalette;
        var target = toHighContrast ? HighContrastPalette : StandardPalette;
        for (int i = 0; i < source.Length; i++)
        {
            var candidate = source[i];
            if (Mathf.Abs(color.r - candidate.r) > 0.003f ||
                Mathf.Abs(color.g - candidate.g) > 0.003f ||
                Mathf.Abs(color.b - candidate.b) > 0.003f) continue;
            var mapped = target[i];
            mapped.a = color.a;
            return mapped;
        }
        return color;
    }

    // 背景
    public static Color BgPrimary       => Get(PaletteColor.BgPrimary); // #F7F7F8
    public static Color BgSecondary     => Get(PaletteColor.BgSecondary); // #EDEDF0
    public static Color BgTertiary      => Get(PaletteColor.BgTertiary); // #E2E2E7

    // サーフェス
    public static Color Surface         => Get(PaletteColor.Surface);             // #FFFFFF (例外的にPure White可)

    // テキスト
    public static Color TextPrimary     => Get(PaletteColor.TextPrimary); // #1D1D1F
    public static Color TextSecondary   => Get(PaletteColor.TextSecondary); // #5B5B60 — AA on light UI surfaces
    public static Color TextTertiary    => Get(PaletteColor.TextTertiary); // #626267 — AA on light UI surfaces

    // アクセント
    public static Color Accent          => Get(PaletteColor.Accent); // #2563EB
    public static Color AccentHover     => Get(PaletteColor.AccentHover); // #1D4ED8

    // セマンティック
    public static Color Success         => Get(PaletteColor.Success); // #19612F — AA on light UI surfaces and badge tints
    public static Color Warning         => Get(PaletteColor.Warning); // #7D4500 — AA on light UI surfaces and badge tints
    public static Color Error           => Get(PaletteColor.Error); // #9F1D17 — AA on light UI surfaces and badge tints
    public static Color NotificationSuccessBackground => new Color32(226, 242, 196, 255); // #E2F2C4
    public static Color NotificationErrorBackground   => new Color32(253, 232, 228, 255); // #FDE8E4

    // 区切り線
    public static Color Divider         => Get(PaletteColor.Divider); // #D1D1D6

    // ターミナルノード
    public static readonly Color NodeStart       = Surface;
    public static readonly Color NodeEnd         = Surface;

    // ボタン テキスト (Primary / Danger 用)
    public static Color ButtonTextLight => Get(PaletteColor.ButtonTextLight);             // White on accent

    // Danger ボタン hover / press
    public static Color DangerHover     => Get(PaletteColor.DangerHover); // #8F1B14
    public static Color DangerPress     => Get(PaletteColor.DangerPress); // #7A1812

    // Accent press
    public static Color AccentPress     => Get(PaletteColor.AccentPress); // #1E40AF

    // ── 4. スペーシングシステム ────────────────────────

    public const float SpaceNone = 0f;
    public const float SpaceXs   = 4f;
    public const float SpaceSm   = 8f;
    public const float SpaceMd   = 16f;
    public const float SpaceLg   = 24f;
    public const float SpaceXl   = 32f;
    public const float Space2Xl  = 48f;

    // ── 3. タイポグラフィ ─────────────────────────────

    public const int FontSizeDisplay    = 32;
    public const int FontSizeHeading    = 20;
    public const int FontSizeSubheading = 16;
    public const int FontSizeBody       = 14;
    public const int FontSizeCaption    = 14;
    public const int FontSizeMicro      = 10;

    // ── 7. 共通値 ─────────────────────────────────────

    public const float CornerRadius       = 6f;
    public const float MinTouchTarget     = 44f;
    public const float TransitionDuration = 0.15f; // 150ms
    public static readonly Vector2 ReferenceResolution = new Vector2(1920f, 1080f);

    // レスポンシブレイアウト
    public const float CatalogDefaultWidth = 312f;
    public const float CatalogMinWidth     = 240f;
    public const float CatalogMaxWidth     = 420f;
    public const float ScenarioDefaultHeight = 320f;
    public const float ScenarioMinHeight     = 220f;
    public const float ScenarioMaxHeight     = 720f;

    // 円形要素（コネクタ・削除ボタン）
    public const float ConnectorSize      = 24f;   // コネクタの幅・高さ
    public const float DeleteButtonSize   = 22f;   // 削除ボタンの幅・高さ

    // ── 6. コンポーネント仕様 ─────────────────────────

    // ボタン共通
    public const float ButtonHeight      = 40f;
    public const float ButtonMinWidth    = 80f;
    public const float ButtonPaddingH    = 20f;
    public const float GhostButtonPadH   = 12f;

    // カード
    public const float CardPadding       = SpaceMd;   // 16
    public const float CardGap           = SpaceSm;   // 8

    // 入力フィールド
    public const float InputHeight       = 40f;
    public const float InputPaddingH     = 12f;

    // ドロップダウン
    public const float DropdownTriggerH  = 40f;
    public const float DropdownItemH     = 36f;

    // ステータスバッジ
    public const float BadgeHeight       = 24f;
    public const float BadgePaddingH     = 8f;
    public const float BadgeCornerRadius = 12f;

    // アイコン
    public const float IconSizeSmall     = 16f;
    public const float IconSizeStandard  = 20f;
    public const float IconSizeLarge     = 24f;

    // 区切り線
    public const float DividerHeight     = 1f;

    // ── ヘルパー ──────────────────────────────────────

    /// <summary>セマンティックバッジ背景 (opacity 0.15)</summary>
    public static Color BadgeBg(Color semantic)
    {
        return new Color(semantic.r, semantic.g, semantic.b, 0.15f);
    }

    /// <summary>Disabled 状態 (opacity 0.4)</summary>
    public static Color Disabled(Color c)
    {
        return new Color(c.r, c.g, c.b, c.a * 0.4f);
    }
}
