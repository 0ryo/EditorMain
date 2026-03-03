using UnityEngine;

/// <summary>
/// design_rule.md 準拠のデザイントークン定数クラス。
/// すべてのUIスクリプトはこのクラスの値を参照し、ハードコード色を排除する。
/// </summary>
public static class DesignTokens
{
    // ── 2. カラーシステム ──────────────────────────────

    // 背景
    public static readonly Color BgPrimary       = new Color(0.969f, 0.969f, 0.973f, 1f); // #F7F7F8
    public static readonly Color BgSecondary     = new Color(0.929f, 0.929f, 0.941f, 1f); // #EDEDF0
    public static readonly Color BgTertiary      = new Color(0.886f, 0.886f, 0.906f, 1f); // #E2E2E7

    // サーフェス
    public static readonly Color Surface         = new Color(1f, 1f, 1f, 1f);             // #FFFFFF (例外的にPure White可)

    // テキスト
    public static readonly Color TextPrimary     = new Color(0.114f, 0.114f, 0.122f, 1f); // #1D1D1F
    public static readonly Color TextSecondary   = new Color(0.431f, 0.431f, 0.451f, 1f); // #6E6E73
    public static readonly Color TextTertiary    = new Color(0.682f, 0.682f, 0.698f, 1f); // #AEAEB2

    // アクセント
    public static readonly Color Accent          = new Color(0.039f, 0.518f, 1f, 1f);     // #0A84FF
    public static readonly Color AccentHover     = new Color(0f, 0.439f, 0.878f, 1f);     // #0070E0

    // セマンティック
    public static readonly Color Success         = new Color(0.188f, 0.820f, 0.345f, 1f); // #30D158
    public static readonly Color Warning         = new Color(1f, 0.624f, 0.039f, 1f);     // #FF9F0A
    public static readonly Color Error           = new Color(1f, 0.271f, 0.227f, 1f);     // #FF453A

    // 区切り線
    public static readonly Color Divider         = new Color(0.820f, 0.820f, 0.839f, 1f); // #D1D1D6

    // ターミナルノード
    public static readonly Color NodeStart       = new Color(0.537f, 0.765f, 1.000f, 1f); // #89C3FF
    public static readonly Color NodeEnd         = new Color(1.000f, 0.537f, 0.545f, 1f); // #FF898B

    // ボタン テキスト (Primary / Danger 用)
    public static readonly Color ButtonTextLight = new Color(1f, 1f, 1f, 1f);             // White on accent

    // Danger ボタン hover / press
    public static readonly Color DangerHover     = new Color(0.878f, 0.243f, 0.208f, 1f); // #E03E35
    public static readonly Color DangerPress     = new Color(0.769f, 0.208f, 0.188f, 1f); // #C43530

    // Accent press
    public static readonly Color AccentPress     = new Color(0f, 0.369f, 0.769f, 1f);     // #005EC4

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
    public const int FontSizeCaption    = 12;
    public const int FontSizeMicro      = 10;

    // ── 7. 共通値 ─────────────────────────────────────

    public const float CornerRadius       = 6f;
    public const float MinTouchTarget     = 44f;
    public const float TransitionDuration = 0.15f; // 150ms

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
