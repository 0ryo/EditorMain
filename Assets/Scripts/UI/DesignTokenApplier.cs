using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// ランタイムで UI 階層全体に DesignTokens のカラーを強制適用するユーティリティ。
/// Prefab に保存された旧い色を上書きするために使用する。
/// </summary>
public static class DesignTokenApplier
{
    // Canvas 基準解像度（QHD）
    static readonly Vector2 ReferenceResolution = new Vector2(2560f, 1440f);
    /// <summary>
    /// カタログパネル配下の全要素に DesignTokens カラーを適用する。
    /// </summary>
    public static void ApplyCatalogPanel(Transform panelRoot)
    {
        if (panelRoot == null) return;

        // Canvas 解像度を QHD に強制
        ApplyCanvasResolution(panelRoot);

        // パネル背景
        SetImageColor(panelRoot, DesignTokens.BgPrimary);

        // ヘッダー
        var header = panelRoot.Find("Header");
        SetImageColor(header, DesignTokens.Surface);

        // ＋ボタン
        var addButton = FindDeep(panelRoot, "Button_AddObject");
        SetImageColor(addButton, DesignTokens.BgSecondary);

        // 検索行
        var searchRow = FindDeep(panelRoot, "SearchRow");
        if (searchRow == null) searchRow = FindDeep(panelRoot, "SearchRow_Runtime");
        SetImageColor(searchRow, DesignTokens.BgPrimary);

        // 入力フィールド
        ApplyInputFieldColors(searchRow);

        // スクロール領域
        var scroll = FindDeep(panelRoot, "Scroll_Catalog");
        SetImageColor(scroll, DesignTokens.Surface);
        if (scroll != null)
        {
            var viewport = scroll.Find("Viewport");
            SetImageColor(viewport, DesignTokens.Surface);
        }

        // リサイズハンドル
        var resizeHandle = FindDeep(panelRoot, "ResizeHandleX");
        SetImageColor(resizeHandle, DesignTokens.BgPrimary);

        // カードテンプレート & 全カード
        ApplyCardColors(panelRoot);

        // ステータステキスト
        var statusText = FindDeep(panelRoot, "Text_Status");
        if (statusText != null)
        {
            var text = statusText.GetComponent<Text>();
            if (text != null) text.color = DesignTokens.TextSecondary;
        }

        // 全テキストを text-primary に（status 以外）
        ApplyTextColors(panelRoot);

        // カードの横幅縮小（余白追加）
        ApplyCardLayoutPadding(panelRoot);

        // カードテキスト中央配置
        ApplyCardTextCentering(panelRoot);

        // 検索窓アウトライン
        ApplySearchInputOutline(panelRoot);
    }

    /// <summary>
    /// シナリオグラフパネル配下の全要素に DesignTokens カラーを適用する。
    /// </summary>
    public static void ApplyScenarioPanel(Transform panelRoot)
    {
        if (panelRoot == null) return;

        // Canvas 解像度を QHD に強制
        ApplyCanvasResolution(panelRoot);

        // パネル背景
        SetImageColor(panelRoot, DesignTokens.BgPrimary);

        // トップバー
        var topBar = FindDeep(panelRoot, "TopBar");
        SetImageColor(topBar, DesignTokens.BgPrimary);

        // トップバー内ボタン全て
        if (topBar != null)
        {
            ApplyButtonColors(topBar);
            ApplyInputFieldColors(topBar);
        }

        // ノードエリア
        var nodeArea = FindDeep(panelRoot, "NodeArea");
        SetImageColor(nodeArea, DesignTokens.Surface);

        // リサイズハンドル
        var resizeHandle = FindDeep(panelRoot, "ResizeHandle");
        SetImageColor(resizeHandle, DesignTokens.BgPrimary);

        // ノードテンプレート & 全ノード
        ApplyNodeColors(panelRoot);

        // 全テキスト
        ApplyTextColors(panelRoot);
    }

    /// <summary>
    /// ノード系 UI 要素（Step / Condition / Terminal）のカラー適用。
    /// </summary>
    public static void ApplyNodeColors(Transform root)
    {
        if (root == null) return;

        // StepNodeTemplate と全インスタンス
        foreach (var step in root.GetComponentsInChildren<StepNodeUI>(true))
        {
            ApplySingleNodeColors(step.transform);
            ApplyCirclesToNode(step.transform);
        }

        // ConditionNodeUI
        foreach (var cond in root.GetComponentsInChildren<ConditionNodeUI>(true))
        {
            ApplySingleNodeColors(cond.transform);
            ApplyCirclesToNode(cond.transform);
        }

        // TerminalNodeUI
        foreach (var term in root.GetComponentsInChildren<TerminalNodeUI>(true))
        {
            SetImageColor(term.transform, DesignTokens.BgSecondary);
            ApplyNodeDragHandle(term.transform);
            ApplyNodeConnectors(term.transform);
            ApplyCirclesToNode(term.transform);
            ApplyTerminalNodeLayout(term);
        }
    }

    static void ApplySingleNodeColors(Transform nodeRoot)
    {
        if (nodeRoot == null) return;

        // ノード本体背景
        SetImageColor(nodeRoot, DesignTokens.Surface);

        // アウトライン追加
        EnsureNodeOutline(nodeRoot);

        // DragHandle
        ApplyNodeDragHandle(nodeRoot);

        // コネクタ
        ApplyNodeConnectors(nodeRoot);

        // 削除ボタン
        var deleteBtn = FindDeep(nodeRoot, "Button_Delete");
        if (deleteBtn == null) deleteBtn = FindDeep(nodeRoot, "Button_Delete_Runtime");
        if (deleteBtn != null)
        {
            SetImageColor(deleteBtn, DesignTokens.Surface);
            EnsureThinOutline(deleteBtn, DesignTokens.Divider);
            var label = deleteBtn.GetComponentInChildren<Text>(true);
            if (label != null) label.color = DesignTokens.TextPrimary;
        }

        // 警告アイコン
        var warning = FindDeep(nodeRoot, "Warning");
        if (warning != null)
        {
            var text = warning.GetComponent<Text>();
            if (text != null) text.color = DesignTokens.Warning;
        }

        // conditionSummary テキスト色
        var condSummary = FindDeep(nodeRoot, "Text_ConditionSummary");
        if (condSummary != null)
        {
            var text = condSummary.GetComponent<Text>();
            if (text != null) text.color = DesignTokens.TextSecondary;
        }

        // ConditionRow 背景
        foreach (var row in nodeRoot.GetComponentsInChildren<ConditionRowUI>(true))
        {
            ApplyDropdownColors(row.transform);
        }
    }

    static void ApplyNodeDragHandle(Transform nodeRoot)
    {
        var dragHandle = nodeRoot.Find("DragHandle");
        if (dragHandle != null)
        {
            SetImageColor(dragHandle, DesignTokens.Surface);
            EnsureThinOutline(dragHandle, DesignTokens.Divider);
        }
    }

    static void ApplyNodeConnectors(Transform nodeRoot)
    {
        var input = FindDeep(nodeRoot, "InputConnector");
        SetImageColor(input, DesignTokens.Accent);

        var output = FindDeep(nodeRoot, "OutputConnector");
        SetImageColor(output, DesignTokens.Accent);
    }

    static void ApplyCardColors(Transform root)
    {
        if (root == null) return;

        // Card_Template とその複製を検索
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            if (child == null) continue;
            if (!child.name.StartsWith("Card_") && child.name != "Card_Template") continue;

            var image = child.GetComponent<Image>();
            var button = child.GetComponent<Button>();
            if (image != null && button != null)
            {
                image.color = DesignTokens.BgSecondary;

                // サムネイル
                var thumb = child.Find("Thumbnail");
                SetImageColor(thumb, DesignTokens.BgSecondary);
            }
        }
    }

    static void ApplyButtonColors(Transform root)
    {
        if (root == null) return;

        foreach (var button in root.GetComponentsInChildren<Button>(true))
        {
            if (button == null) continue;
            // InputField 内のものは除外
            if (button.GetComponent<InputField>() != null) continue;
            if (button.GetComponentInParent<InputField>() != null) continue;
            // Dropdown 内のものは除外
            if (button.GetComponent<Dropdown>() != null) continue;
            if (button.GetComponentInParent<Dropdown>() != null) continue;

            var image = button.GetComponent<Image>();
            if (image != null)
            {
                image.color = DesignTokens.BgSecondary;
            }
        }
    }

    static void ApplyInputFieldColors(Transform root)
    {
        if (root == null) return;

        foreach (var input in root.GetComponentsInChildren<InputField>(true))
        {
            if (input == null) continue;

            var bg = input.GetComponent<Image>();
            if (bg != null) bg.color = DesignTokens.BgPrimary;

            if (input.textComponent != null)
                input.textComponent.color = DesignTokens.TextPrimary;

            var placeholder = input.placeholder as Text;
            if (placeholder != null)
                placeholder.color = DesignTokens.TextTertiary;
        }
    }

    static void ApplyDropdownColors(Transform root)
    {
        if (root == null) return;

        foreach (var dropdown in root.GetComponentsInChildren<Dropdown>(true))
        {
            if (dropdown == null) continue;

            var bg = dropdown.GetComponent<Image>();
            if (bg != null) bg.color = DesignTokens.Surface;
            EnsureThinOutline(dropdown.transform, DesignTokens.Divider);

            if (dropdown.captionText != null)
                dropdown.captionText.color = DesignTokens.TextPrimary;

            if (dropdown.template != null)
            {
                var templateImage = dropdown.template.GetComponent<Image>();
                if (templateImage != null) templateImage.color = DesignTokens.Surface;
                EnsureThinOutline(dropdown.template, DesignTokens.Divider);

                var viewport = dropdown.template.Find("Viewport");
                if (viewport != null)
                {
                    SetImageColor(viewport, DesignTokens.Surface);
                    EnsureThinOutline(viewport, DesignTokens.Divider);
                }

                var item = dropdown.template.Find("Viewport/Content/Item");
                if (item != null)
                {
                    SetImageColor(item, DesignTokens.Surface);
                    EnsureThinOutline(item, DesignTokens.Divider);
                }
            }
        }
    }

    static void ApplyTextColors(Transform root)
    {
        if (root == null) return;

        foreach (var text in root.GetComponentsInChildren<Text>(true))
        {
            if (text == null) continue;
            // 特殊テキストは除外（ステータス、警告、削除ヒント等は個別設定済み）
            if (text.name == "Text_Status" || text.name == "Warning" ||
                text.name == "Text_DeleteHint") continue;

            // Color.black (#000000) を text-primary に置換
            if (text.color == Color.black ||
                (Mathf.Approximately(text.color.r, 0f) &&
                 Mathf.Approximately(text.color.g, 0f) &&
                 Mathf.Approximately(text.color.b, 0f)))
            {
                text.color = DesignTokens.TextPrimary;
            }
        }
    }

    // ── 円形要素 ──

    static void ApplyCirclesToNode(Transform nodeRoot)
    {
        if (nodeRoot == null) return;

        // コネクタを円形に
        ApplyCircleByName(nodeRoot, "InputConnector");
        ApplyCircleByName(nodeRoot, "OutputConnector");

        // 削除ボタンを円形に
        ApplyCircleByName(nodeRoot, "Button_Delete");
        ApplyCircleByName(nodeRoot, "Button_Delete_Runtime");
    }

    static void ApplyCircleByName(Transform root, string name)
    {
        var target = FindDeep(root, name);
        if (target == null) return;
        var image = target.GetComponent<Image>();
        if (image != null) UiRoundedTheme.ApplyCircleToElement(image);
    }

    // ── Canvas 解像度 ──

    static void ApplyCanvasResolution(Transform uiElement)
    {
        if (uiElement == null) return;

        var canvas = uiElement.GetComponentInParent<Canvas>();
        if (canvas == null) return;

        var scaler = canvas.GetComponent<CanvasScaler>();
        if (scaler == null) return;

        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = ReferenceResolution;
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
    }

    // ── ノードアウトライン ──

    static void EnsureNodeOutline(Transform nodeRoot)
    {
        EnsureThinOutline(nodeRoot, DesignTokens.Divider);
    }

    static void EnsureThinOutline(Transform target, Color color)
    {
        if (target == null) return;
        if (target.GetComponent<Graphic>() == null) return;

        var outline = target.GetComponent<Outline>();
        if (outline == null) outline = target.gameObject.AddComponent<Outline>();
        outline.effectColor = color;
        outline.effectDistance = new Vector2(0.5f, -0.5f);
        outline.useGraphicAlpha = false;
    }

    // ── ターミナルノードレイアウト ──

    static void ApplyTerminalNodeLayout(TerminalNodeUI terminal)
    {
        if (terminal == null) return;

        // サイズ縮小
        var rt = terminal.transform as RectTransform;
        if (rt != null)
        {
            rt.sizeDelta = new Vector2(160f, 64f);
        }

        // ラベルテキストを中央配置
        if (terminal.labelText != null)
        {
            terminal.labelText.alignment = TextAnchor.MiddleCenter;
            var labelRt = terminal.labelText.rectTransform;
            labelRt.anchorMin = Vector2.zero;
            labelRt.anchorMax = Vector2.one;
            labelRt.offsetMin = new Vector2(4f, 4f);
            labelRt.offsetMax = new Vector2(-4f, -4f);
            terminal.labelText.fontSize = DesignTokens.FontSizeSubheading;
        }
    }

    // ── カタログカードレイアウト ──

    static void ApplyCardLayoutPadding(Transform panelRoot)
    {
        if (panelRoot == null) return;

        // Content の VerticalLayoutGroup のパディングを増やす
        var content = FindDeep(panelRoot, "Content");
        if (content == null) return;

        var layout = content.GetComponent<VerticalLayoutGroup>();
        if (layout == null) return;

        layout.padding = new RectOffset(
            (int)DesignTokens.SpaceMd,  // 左 16
            (int)DesignTokens.SpaceMd,  // 右 16
            (int)DesignTokens.SpaceSm,  // 上 8
            (int)DesignTokens.SpaceSm   // 下 8
        );
        layout.spacing = DesignTokens.SpaceSm;
    }

    static void ApplyCardTextCentering(Transform panelRoot)
    {
        if (panelRoot == null) return;

        foreach (Transform child in panelRoot.GetComponentsInChildren<Transform>(true))
        {
            if (child == null) continue;
            if (!child.name.StartsWith("Card_") && child.name != "Card_Template") continue;
            if (child.GetComponent<Button>() == null) continue;

            // LabelMain を中央配置
            var labelMain = child.Find("LabelMain");
            if (labelMain != null)
            {
                var text = labelMain.GetComponent<Text>();
                if (text != null)
                {
                    text.alignment = TextAnchor.MiddleCenter;
                }
            }

            // フォールバック: 最初の Text を中央配置
            if (labelMain == null)
            {
                var text = child.GetComponentInChildren<Text>(true);
                if (text != null)
                {
                    text.alignment = TextAnchor.MiddleCenter;
                }
            }
        }
    }

    // ── 検索窓アウトライン ──

    static void ApplySearchInputOutline(Transform panelRoot)
    {
        if (panelRoot == null) return;

        foreach (var input in panelRoot.GetComponentsInChildren<InputField>(true))
        {
            if (input == null) continue;
            if (!input.gameObject.name.Contains("Search")) continue;

            var outline = input.GetComponent<Outline>();
            if (outline == null) outline = input.gameObject.AddComponent<Outline>();
            outline.effectColor = DesignTokens.Divider;
            outline.effectDistance = new Vector2(1f, -1f);
            outline.useGraphicAlpha = false;
        }
    }

    // ── ヘルパー ──

    static void SetImageColor(Transform target, Color color)
    {
        if (target == null) return;
        var image = target.GetComponent<Image>();
        if (image != null) image.color = color;
    }

    static Transform FindDeep(Transform parent, string name)
    {
        if (parent == null) return null;

        // 直接の子を先に検索
        var direct = parent.Find(name);
        if (direct != null) return direct;

        // 再帰検索
        foreach (Transform child in parent)
        {
            var found = FindDeep(child, name);
            if (found != null) return found;
        }

        return null;
    }
}
