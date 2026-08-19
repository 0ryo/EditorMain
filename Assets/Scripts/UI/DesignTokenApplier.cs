using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// ランタイムで UI 階層全体に DesignTokens のカラーを強制適用するユーティリティ。
/// Prefab に保存された旧い色を上書きするために使用する。
/// </summary>
public static class DesignTokenApplier
{
    // Canvas 基準解像度は design_rule.md と実装方針に合わせて 1920x1080 に統一する。
    static readonly Vector2 ReferenceResolution = DesignTokens.ReferenceResolution;
    /// <summary>
    /// カタログパネル配下の全要素に DesignTokens カラーを適用する。
    /// </summary>
    public static void ApplyCatalogPanel(Transform panelRoot)
    {
        if (panelRoot == null) return;

        // Canvas 解像度をデザイン基準へ補正
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
            var text = statusText.GetComponent<TMP_Text>();
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

        // Canvas 解像度をデザイン基準へ補正
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
            Color termColor = GetTerminalColor(term);
            SetImageColor(term.transform, termColor);
            HideTerminalDragHandle(term.transform);
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
            var label = deleteBtn.GetComponentInChildren<TMP_Text>(true);
            if (label != null) label.color = DesignTokens.TextPrimary;
        }

        // 警告アイコン
        var warning = FindDeep(nodeRoot, "Warning");
        if (warning != null)
        {
            var text = warning.GetComponent<TMP_Text>();
            if (text != null) text.color = DesignTokens.Warning;
        }

        // conditionSummary テキスト色
        var condSummary = FindDeep(nodeRoot, "Text_ConditionSummary");
        if (condSummary != null)
        {
            var text = condSummary.GetComponent<TMP_Text>();
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
                image.color = DesignTokens.Surface;
                EnsureThinOutline(child, DesignTokens.Divider);

                var thumb = child.Find("Thumbnail");
                if (thumb != null) thumb.gameObject.SetActive(false);

                var removeButton = child.Find("Button_RemoveCard");
                if (removeButton != null) removeButton.gameObject.SetActive(false);
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
            if (button.GetComponent<TMP_InputField>() != null) continue;
            if (button.GetComponentInParent<TMP_InputField>() != null) continue;
            // Dropdown 内のものは除外
            if (button.GetComponent<TMP_Dropdown>() != null) continue;
            if (button.GetComponentInParent<TMP_Dropdown>() != null) continue;

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

        foreach (var input in root.GetComponentsInChildren<TMP_InputField>(true))
        {
            if (input == null) continue;

            var bg = input.GetComponent<Image>();
            if (bg != null) bg.color = DesignTokens.BgPrimary;

            if (input.textComponent != null)
                input.textComponent.color = DesignTokens.TextPrimary;

            var placeholder = input.placeholder as TMP_Text;
            if (placeholder != null)
                placeholder.color = DesignTokens.TextTertiary;
        }
    }

    static void ApplyDropdownColors(Transform root)
    {
        if (root == null) return;

        foreach (var dropdown in root.GetComponentsInChildren<TMP_Dropdown>(true))
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

        foreach (var text in root.GetComponentsInChildren<TMP_Text>(true))
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
        canvas.pixelPerfect = true;
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
        outline.effectDistance = new Vector2(1f, -1f);
        outline.useGraphicAlpha = false;
    }

    // ── ターミナルノード色 ──

    static Color GetTerminalColor(TerminalNodeUI term)
    {
        if (term != null && term.labelText != null)
        {
            string label = term.labelText.text;
            if (label == "START" || label == "開始") return DesignTokens.NodeStart;
            if (label == "END" || label == "終了") return DesignTokens.NodeEnd;
        }
        return DesignTokens.BgSecondary;
    }

    /// <summary>ターミナルノードの DragHandle の Image / Outline を無効化して境界線を消す。</summary>
    static void HideTerminalDragHandle(Transform nodeRoot)
    {
        var dragHandle = nodeRoot.Find("DragHandle");
        if (dragHandle == null) return;
        var image = dragHandle.GetComponent<Image>();
        if (image != null) image.enabled = false;
        var outline = dragHandle.GetComponent<Outline>();
        if (outline != null) outline.enabled = false;
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
            terminal.labelText.alignment = TextAlignmentOptions.Center;
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

            ApplyCatalogCardTextLayout(child);

            var thumb = child.Find("Thumbnail");
            if (thumb != null) thumb.gameObject.SetActive(false);

            var removeButton = child.Find("Button_RemoveCard");
            if (removeButton != null) removeButton.gameObject.SetActive(false);
        }
    }

    static void ApplyCatalogCardTextLayout(Transform child)
    {
        if (child == null) return;

        var categoryBadge = child.Find("Badge_Category") as RectTransform;
        if (categoryBadge != null)
        {
            categoryBadge.anchorMin = new Vector2(0f, 1f);
            categoryBadge.anchorMax = new Vector2(0f, 1f);
            categoryBadge.pivot = new Vector2(0f, 1f);
            categoryBadge.offsetMin = new Vector2(16f, -34f);
            categoryBadge.offsetMax = new Vector2(84f, -12f);

            var image = categoryBadge.GetComponent<Image>();
            if (image != null) image.color = DesignTokens.BadgeBg(DesignTokens.Accent);

            var categoryText = categoryBadge.Find("LabelCategory")?.GetComponent<TMP_Text>();
            if (categoryText != null)
            {
                categoryText.fontSize = DesignTokens.FontSizeCaption;
                categoryText.color = DesignTokens.Accent;
                categoryText.alignment = TextAlignmentOptions.Center;
            }
        }

        var technical = child.Find("LabelTechnicalId")?.GetComponent<TMP_Text>();
        if (technical != null)
        {
            technical.fontSize = DesignTokens.FontSizeCaption;
            technical.color = DesignTokens.TextSecondary;
            technical.alignment = TextAlignmentOptions.MidlineLeft;
            var technicalRect = technical.rectTransform;
            technicalRect.anchorMin = new Vector2(0f, 1f);
            technicalRect.anchorMax = new Vector2(1f, 1f);
            technicalRect.pivot = new Vector2(0f, 1f);
            technicalRect.offsetMin = new Vector2(16f, -84f);
            technicalRect.offsetMax = new Vector2(-16f, -64f);
        }

        // LabelMain をカードタイトルとして左寄せ配置
        {
            var labelMain = child.Find("LabelMain");
            if (labelMain != null)
            {
                var text = labelMain.GetComponent<TMP_Text>();
                if (text != null)
                {
                    text.fontSize = DesignTokens.FontSizeBody;
                    text.color = DesignTokens.TextPrimary;
                    text.alignment = TextAlignmentOptions.MidlineLeft;
                }

                var labelRect = labelMain as RectTransform;
                if (labelRect != null)
                {
                    labelRect.anchorMin = new Vector2(0f, 1f);
                    labelRect.anchorMax = new Vector2(1f, 1f);
                    labelRect.pivot = new Vector2(0f, 1f);
                    labelRect.offsetMin = new Vector2(16f, -64f);
                    labelRect.offsetMax = new Vector2(-16f, -38f);
                }
            }

            // フォールバック: 最初の TMP_Text をカードタイトルとして扱う
            if (labelMain == null)
            {
                var text = child.GetComponentInChildren<TMP_Text>(true);
                if (text != null)
                {
                    text.alignment = TextAlignmentOptions.MidlineLeft;
                    var textRect = text.rectTransform;
                    if (textRect != null && textRect.parent == child)
                    {
                        textRect.anchorMin = new Vector2(0f, 1f);
                        textRect.anchorMax = new Vector2(1f, 1f);
                        textRect.pivot = new Vector2(0f, 1f);
                        textRect.offsetMin = new Vector2(16f, -64f);
                        textRect.offsetMax = new Vector2(-16f, -38f);
                    }
                }
            }
        }
    }

    // ── 検索窓アウトライン ──

    static void ApplySearchInputOutline(Transform panelRoot)
    {
        if (panelRoot == null) return;

        foreach (var input in panelRoot.GetComponentsInChildren<TMP_InputField>(true))
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

    /// <summary>
    /// オブジェクト詳細パネル配下の全要素に DesignTokens カラーを適用する。
    /// </summary>
    public static void ApplyDetailPanel(Transform panelRoot)
    {
        if (panelRoot == null) return;

        // Canvas 解像度を QHD に強制
        ApplyCanvasResolution(panelRoot);

        // パネル背景
        SetImageColor(panelRoot, DesignTokens.BgPrimary);

        // ヘッダー背景 + タイトルテキスト
        var header = panelRoot.Find("Header");
        SetImageColor(header, DesignTokens.Surface);
        if (header != null)
        {
            var titleText = header.GetComponentInChildren<TMP_Text>(true);
            if (titleText != null) titleText.color = DesignTokens.TextPrimary;
        }

        // Viewport 背景
        var viewport = FindDeep(panelRoot, "Viewport");
        SetImageColor(viewport, DesignTokens.Surface);

        // InputField（オブジェクト名入力欄）のスタイル
        ApplyInputFieldColors(panelRoot);

        // Content 直下の Row / Divider を個別に処理
        var content = FindDeep(panelRoot, "Content");
        if (content == null) return;

        foreach (Transform child in content)
        {
            if (child == null) continue;

            if (child.name.StartsWith("Row_"))
            {
                SetImageColor(child, DesignTokens.Surface);

                // 見出しラベル → TextSecondary
                var labelTf = child.Find("Label");
                if (labelTf != null)
                {
                    var t = labelTf.GetComponent<TMP_Text>();
                    if (t != null) t.color = DesignTokens.TextSecondary;
                }

                // 値テキスト (Text_PrefabLabel / Text_ObjectName / Text_Description) → TextPrimary
                foreach (Transform grandchild in child)
                {
                    if (grandchild == null) continue;
                    if (!grandchild.name.StartsWith("Text_")) continue;
                    var t = grandchild.GetComponent<TMP_Text>();
                    if (t != null) t.color = DesignTokens.TextPrimary;
                }
            }
            else if (child.name == "Divider")
            {
                SetImageColor(child, DesignTokens.Divider);
            }
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
