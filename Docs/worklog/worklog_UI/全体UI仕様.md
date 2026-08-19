# 全体UI仕様

## 1. 目的
- 本書は、`codex/ui` ブランチで確定した UI 実装ルールを、次回以降の開発で再利用するための基準書とする。
- 対象は uGUI ベースの編集 UI 全体（オブジェクト一覧ウィンドウ、ノード追加ウィンドウ、ノードカード、接続線、リサイズ、配置操作）とする。

## 2. 実装方式（必須ルール）
- UI は **Canvas/Prefab（アセット）を正** とする。
- スクリプトでの新規 UI 階層生成は原則禁止。見た目調整は Prefab 側で行う。
- ルート Prefab は `Assets/UI/Prefabs/UIRoot.prefab`。
- Scene 反映は `Assets/Editor/Automation/ApplyUiPrefab.cs` を利用し、Unity Editor API 経由で適用する。
- Prefab 生成・更新は `Assets/Editor/Automation/BuildUiPrefabs.cs` を基準にする。

## 3. UIルート構成
- `UIRoot`（Canvas）
  - `Panel_Catalog`（左固定、オブジェクト一覧）
  - `Panel_ScenarioGraph`（下部固定、ノード追加）
  - `ViewportStatusStrip`（3Dビュー上部、現在モード/配置/選択状態）
  - `UiPanelDockSync`（2パネル密着同期）
- Canvas 設定
  - Render Mode: `Screen Space - Overlay`
  - CanvasScaler: `Scale With Screen Size`（Reference 1920x1080）
  - GraphicRaycaster 有効
- EventSystem
  - Scene 内は常に 1 つ。重複時は `CatalogUI` が重複を無効化。

## 4. 共通デザインルール
- 全体トーンは白基調。
- 透明度は原則 1.0（不透明）。例外は線レイヤーの透明背景のみ。
- 主要色（実装値）
  - パネル背景: `DesignTokens.BgPrimary`
  - ノード/カード背景: `DesignTokens.Surface` または `DesignTokens.BgSecondary`
  - アクセント/選択/接続線: `DesignTokens.Accent`（`#2563EB`）
  - ドロップダウン背景: `DesignTokens.Surface`
- フォント
  - ランタイムで参照する built-in font は `LegacyRuntime.ttf` を使用。
- 視認性
  - ドロップダウン文字色は黒で固定。
  - 罫線やアウトラインに依存せず、背景色差で識別する。

## 5. パネル配置・リサイズルール
- `Panel_Catalog`
  - 画面左側固定、上下端まで表示。
  - 横幅可変（`PanelHorizontalResizeHandle`）
  - 幅制限: `min=240`, `max=420`
- `Panel_ScenarioGraph`
  - 画面下部固定、右側に展開。
  - 高さ可変（`PanelVerticalResizeHandle`）
  - 高さ制限: `min=220`, `max=720`
- 2パネルの隙間
  - `UiPanelDockSync.gap = 0` を維持し、常に密着。
  - カタログ幅変更時も隙間を作らない。

## 6. オブジェクト一覧ウィンドウ仕様
- 対象: `Panel_Catalog` / `CatalogUI`
- UI構成
  - ヘッダー（タイトル、`＋` ボタン）
  - 検索入力（`InputField`）
  - スクロール一覧（カード縦積み）
  - 右端に横リサイズハンドル
- カード表示
  - カテゴリバッジ、表示名、技術ID（`typeId`）を表示
  - カード高は `96` 固定
  - `typeId` からカテゴリと表示名を推定する
  - 上詰め配置（`VerticalLayoutGroup.childAlignment = UpperLeft`）
  - 小型 `×` ボタンはカードホバー時のみ表示
  - `×` ボタンはカード右上角の外側にはみ出し、丸の中心がカード角に重なる
  - `×` 押下で一覧から当該カードを除去
- 検索
  - `typeId` の部分一致
  - 大文字/小文字を区別しない
- クリック配置
  - カードクリックで `PlacementController.EnterPlacement(typeId)`
  - 3Dビュー次クリックで 1 回配置し配置モード解除
  - 配置待ち中の `typeId` に対応するカードへ `DesignTokens.Accent`（青）の枠線強調を表示
- ドラッグ&ドロップ配置
  - カードドラッグ終了時、UI外ドロップなら即時 1 回配置
  - 配置モードは残さない
  - ドラッグ中は `PlacementController.SetUiDragInProgress(true)` で 3Dクリック干渉抑制
- `＋` ボタン
  - UIのみ。押下時は「未実装」ステータス表示。

## 7. 配置処理ルール（PlacementController）
- 配置先判定
  - カメラから床へ Raycast（`floorMask`）
  - 床Colliderに当たらない場合は y=0 平面へフォールバックして配置点を解決する
- 3Dビュー補助表示
  - `WorkspaceFloorGrid` が実行時に床グリッドを補完する
  - `ViewportStatusStrip` が `閲覧中` / `配置中` / `移動中` / `スケール調整` と対象情報を表示する
- 座標
  - `x,z` は `0.1m` グリッドスナップ
  - `y = hit.y + 0.5`
- 生成
  - `PlacedObject` を保証
  - `InitType(typeId)` 実行
  - `ForceNewId()` で `obj-0001` 形式 ID を保証
  - 回転は `Quaternion.identity`
- 配置後は自動選択
  - 配置成功時は `ViewportStatusStrip` に短時間 `配置しました: obj-xxxx` を表示する

## 8. ノード追加ウィンドウ仕様
- 対象: `Panel_ScenarioGraph` / `ScenarioGraphUI`
- UI構成
  - TopBar: プロジェクト名、`+ 手順`、`+ 条件`、`保存`、ステータス
  - NodeArea: ノード配置領域
  - LineLayer: 接続線描画レイヤー
  - 上端に縦リサイズハンドル
- ノード種別
  - `Start` / `End`（各1つ）
  - `Step`（複数）
  - `Condition`（複数）
- `+ 手順`
  - `CurriculumGraphService.AddStep()` を実行
  - 既存ノード位置は保持
  - 未保存位置がないノードは自動整列位置を適用
- `+ 条件`
  - `CurriculumGraphService.AddCondition()` を実行
  - 新規Conditionノード（表示名は条件）を追加
- ノード移動
  - `NodeDragHandler` でドラッグ移動可能
- 保存
  - `Assets/Exports/<ProjectName>-curriculum.json` に保存
  - 保存前に E-01〜E-11 を検証し、エラー時は `Save` を無効化
  - 出力形式は `version=2` + `scenarioSettings` + `requiredActions[].conditions[]`

## 9. ノードカード仕様
- `StepNodeUI`
  - 表示: `手順 n`、条件数サマリ、警告アイコン
  - 接続: 入力1 / 出力1（StepFlow）
- `ConditionNodeUI`
  - 表示: `条件 n`、`DropdownA` + `DropdownB`
  - 接続: 出力1（ConditionBind）
- `TerminalNodeUI`
  - `開始`: 出力のみ
  - `終了`: 入力のみ

## 10. 条件ドロップダウン仕様
- 対象: `ConditionRowUI`, `PlacedObjectOptionProvider`
- 選択肢
  - 先頭は常に `未設定`
  - 以降は現在ワールドにある `PlacedObject.id`（`obj-xxxx`）を表示
- 更新
  - `ConditionNodeUI` が 0.2 秒間隔で選択肢差分を監視し再バインド
- 見た目
  - ボタン背景: グレー
  - テンプレート背景: 薄グレー
  - 文字色: 黒
  - 枠線（Outline）は除去
  - ボタン直下に密着表示（隙間なし）
- 参照補完
  - `captionText`, `itemText`, `template` が未設定でも `EnsureDropdownReferences` で補完
  - 開閉時崩れ対策として `DropdownOpenFixer` で表示後補正

## 11. ノード接続仕様
- 接続方法
  - クリック接続: 出力クリック → 入力クリック
  - ドラッグ接続: 出力から入力へドラッグ&ドロップ
- 接続種別
  - `StepFlow`: `Start/Step -> Step/End`
  - `ConditionBind`: `Condition -> Step`
- 接続制約
  - StepFlowは分岐禁止（各ノード出力は最大1）
  - StepFlowは循環禁止
  - Conditionは1つのStepにのみ接続
  - StepのCondition受け取り上限は3
- 接続線
  - `ConnectionLineGraphic` で描画
  - 色は明るい黄色、太さ `8`
  - `LineLayer` 上に描画
  - `CanvasRenderer` 欠落時はランタイム補完
- 接続データ
  - `CurriculumGraphService.TryAddEdge(from, to, out reason)` を呼ぶ
  - 自己接続・重複接続は拒否

## 12. ログ運用（調査用）
- 接続系ログ
  - `[ConnectorDrag] Begin/Complete/Cancel ...`
  - `[ScenarioGraphUI] Drag connect ...`
  - `[ScenarioGraphUI] RefreshLines expectedEdges=... created=... removed=...`
- 配置系ログ
  - `[Placement] EnterPlacement ...`
  - `[Placement] Placed ...`
- ドロップダウン系ログ
  - `[PlacedObjectOptionProvider] options=... ids=...`
  - `[ConditionRowUI] dropdown refs ...`

## 13. 変更時の必須チェック
- コンパイルエラー 0
- EventSystem が 1 つであること
- 2ウィンドウ間に隙間がないこと
- カタログ横リサイズとノード縦リサイズが機能すること
- カード検索・クリック配置・ドラッグ配置が機能すること
- ノード接続線（明るい黄色）が表示されること
- 条件ドロップダウンに `obj-xxxx` が表示されること
- 無効なグラフで `Save` が無効化されること
- 保存JSONに `requiredActions[].conditions[]` が出力されること

## 14. 運用ルール（今後）
- UIを変更する場合は、先に `UIRoot.prefab` と対応スクリプトの責務分離を維持する。
- レイアウト変更は Prefab 側を優先し、ロジック変更は `CatalogUI` / `ScenarioGraphUI` / `StepNodeUI` に閉じ込める。
- 本仕様と差異が出た場合は、同一PR/同一コミット系列で `Docs/worklog_UI/` を更新する。

## 15. 2026-02-22 NodeArea Pan/Zoom
- Scenario graph viewport now supports wheel zoom and middle-drag pan.
- Added `GraphContent` under `NodeArea` as a large canvas for navigation.
- Related scripts: `NodeAreaPanZoomController`, `ScenarioGraphUI`, `NodeDragHandler`.

## 16. 2026-02-22 Connection Path Clipping
- Updated `ConnectionLineGraphic` to `MaskableGraphic` so paths obey `RectMask2D` clipping in `NodeArea`.
- This fixes paths visually escaping the viewport while zooming/panning.

## 17. 2026-02-22 Start/End Label Visibility
- `TerminalNodeUI` brings label text to front so START/END captions remain visible above overlay children.
- Disabled label raycast to keep input handling unchanged.

## 18. 2026-02-22 Global Corner Radius
- Introduced `UiRoundedTheme` to apply rounded sprites to `Image` components across UI hierarchies.
- Applied from both `CatalogUI` and `ScenarioGraphUI` so existing prefabs also receive rounded corners at runtime.
- Added serialized `cornerRadius` fields (default 14) for easy tuning.

## 19. 2026-02-22 Node/Path Deletion UX
- `StepNodeUI` and `ConditionNodeUI` now include a top-right `X` delete button.
- Path (`ConnectionLineGraphic`) supports hover/click events; hover shows `�폜`, click deletes that edge.
- `Start` and `End` terminal nodes keep delete disabled.

## 20. 2026-02-22 Path Hover Hit-Test + Delete Button Visual Tuning
- Delete X button background changed to gray and aligned to the center of the yellow header bar (DragHandle).
- ConnectionLineGraphic now uses segment-distance raycast hit testing instead of full-rect hit testing.
- Node drag now works while edges are connected.
- Path delete hint text appears only on hover and is shown above the path stroke.
## 21. 2026-02-22 Condition Embed Into Step By Proximity
- Drag a Condition node near a Step node and release to auto-bind and embed it into that Step.
- Bound Condition nodes are rendered inside the Step card as editable condition rows.
- Step card height auto-expands to fit the embedded condition rows.
- Embedded condition rows include a delete X button to remove the underlying Condition node.
## 22. 2026-02-22 Embedded Condition Editing Stability
- Embedded condition rows keep A/B dropdown editing enabled after embed.
- StepNodeUI now rebinds embedded dropdown options when placed-object option list changes.
- Connection path raycast is blocked while pointer is over node cards, preventing path hit from stealing dropdown input.
## 23. 2026-02-22 Embedded Condition Card Visual Update
- Embedded conditions are shown as titled cards (Condition1, Condition2, ...).
- Vertical spacing between embedded condition cards was increased for readability.
- Divider line is shown under each embedded condition card (between cards when multiple).
- Step auto-resize logic now uses embedded card height/spacing.
## 24. 2026-02-28 編集モードUI + ランタイムギズモ
- `EditModeService` は `Browse / Place / Transform / Scale` を持ち、`ModeChanged` でUI同期する。
- `CatalogUI` 上部に編集モード行（`閲覧` / `移動` / `スケール`）を配置し、現在モードを色で明示する。
- `Tab` キーは `Transform` モードへのショートカットとして機能する（InputField入力中は無効）。
- `Transform` モードでは `MoveTool` が軸移動ハンドルと回転ハンドルを表示し、オブジェクト変形をギズモ経由で行う。
- `Scale` モードでは `SelectionOutline` がコーナードラッグによる等比スケールを提供する。
- `UiPanelDockSync` は編集モード行も含めてカタログ幅に追従させ、UIの重なりや隙間を防ぐ。
## 25. 2026-03-02 Catalog Card Remove Button
- `CatalogUI` の各オブジェクトカードに右上 `×` ボタン（`Button_RemoveCard`）を追加。
- `×` はカードホバー時のみ表示する。
- `×` ボタンは真円で、丸の中心をカード右上角に一致させる。
- `×` 押下で、その `typeId` カードをオブジェクト一覧から除去する。
- この除去は一覧表示のみを対象とし、既にワールドにあるオブジェクトには影響しない。
- 除去状態は同一セッション内で維持され、検索変更やカード再構築でも除去済みカードは再表示しない。

## 26. 2026-03-02 Placement Waiting Card Highlight
- カードクリック後、ワールドクリック待ちの配置モード中は該当 `typeId` カードに青枠（`DesignTokens.Accent`）を表示する。
- 配置完了または配置モード終了で青枠を解除する。

## 27. 2026-03-03 Catalog Card Name-Only Layout
- オブジェクト一覧カードの表示を「オブジェクト名のみ」に統一した。
- `Thumbnail` と `Button_RemoveCard` はカード上で非表示とし、余計な四角領域を出さない。
- `LabelMain` はカード全幅で `MiddleCenter` 表示にし、文字を中央揃えにした。
- 既存Prefabにも反映されるよう `CatalogUI` / `DesignTokenApplier` でランタイム補正を入れた。

## 28. 2026-03-03 Object Detail Description Editing
- オブジェクト詳細パネルの `説明` 行は、説明文が空でも常時表示する。
- `説明` の値表示は `InputField`（マルチライン）とし、空欄からの新規入力を可能にした。
- 既存の説明文がある場合も、同じ `InputField` をクリックして編集できる。
- 説明文は `PlacedObject` 側でオブジェクト単位に保持し、同一オブジェクト再選択時に編集内容を再表示する。
- 既存Prefab互換のため、`ObjectDetailPanel` で説明入力欄が未配置でもランタイム補完生成する。

## 29. 2026-03-03 Object Detail Condition Usage
- オブジェクト詳細パネルに `使用Condition` 行を追加し、選択中オブジェクトを参照している Condition ノードを表示する。
- 参照判定は `ConditionNodeData.objectAId/objectBId` と `PlacedObject.id` の一致で行う。
- 表示形式は `cond-xxxx [A]` / `cond-xxxx [B]` / `cond-xxxx [A/B]`。Stepバインドがある場合は `-> step-xxxx` を付与する。
- 該当なしの場合は `未使用` を表示する。
- 既存Prefab互換のため、`ObjectDetailPanel` は `Row_ConditionUsage` 未配置時にランタイム補完生成する。

## 30. 2026-03-03 Object Detail In-Use Nodes (Latest)
- �I�u�W�F�N�g�ڍ׃p�l���̕\�L�� `�g�p���m�[�h` �ɕύX�B
- �I�� `PlacedObject.id` �� `ConditionNodeData.objectAId/objectBId` �Əƍ����A�g�p���� Condition �m�[�h�𒊏o�B
- �g�p���m�[�h�͌����������l�p���u���b�N��c�z�u�ŕ\���B
- �e�u���b�N�{���� 2 �s�� `objectA����` / `objectB���ɋ߂Â���` �̌`���B
- �u���b�N�͘g���iOutline�j�t���B�Y���m�[�h�Ȃ��̂Ƃ��� `���g�p` ��\���B
- ����Prefab�݊��̂��߁A`ObjectDetailPanel` �� `Row_ConditionUsage` / `UsageNodeList` / `UsageNodeBlock_Template` ���Ȃ��ꍇ�Ƀ����^�C���⊮��������B

## 31. 2026-03-03 Object Detail Uses Real ConditionNode UI (Latest)
- `�g�p���m�[�h` �͊ȈՃe�L�X�g�ł͂Ȃ� `ConditionNodeUI` ���̂𕡐����ĕ\������B
- �e�m�[�h�� `EnterEmbeddedMode(index)` ��K�p���A�w�b�_�[�� `�菇 n` �\���ɓ��ꂷ��B
- A/B �� Dropdown �͕ҏW�\�ŁA�ύX�� `ConditionNodeData.objectAId/objectBId` �ɔ��f�����B
- �ڍב��̕ҏW�E�폜��� `ScenarioGraphUI.RebuildFromExternalChange()` �ŃO���t�\����ē�������B
- �ڍא�p�̃f�U�C���ύX�|�C���g�Ƃ��� `ObjectDetailConditionNodeStyler` �𓱓����A�ڍ׃E�B���h�E�������̌����ڒ�����\�ɂ���B

## 32. 2026-03-23 Rendering Quality / TMP Migration
- UI 基盤は引き続き `uGUI` だが、テキスト・入力欄・ドロップダウンは `TextMeshPro` 系 (`TMP_Text`, `TMP_InputField`, `TMP_Dropdown`) へ移行する。
- 対象は `CatalogUI`, `ScenarioGraphUI`, `ConditionNodeUI`, `ConditionRowUI`, `StepNodeUI`, `TerminalNodeUI`, `ObjectDetailPanel`, `BuildUiPrefabs`。
- 既存 Prefab との差分が残る期間でも、`DesignTokenApplier` 側で TMP 前提の色・アウトライン補正を行う。
- `TmpFontInitializer` により、Windows では `Yu Gothic UI` / `Meiryo UI` 等を候補に TMP の日本語フォールバックフォントをランタイム登録する。
- `UiRoundedTheme` の角丸スプライト生成解像度を引き上げ、丸ボタンや角丸パネルのジャギーを低減する。
- `ConnectionLineGraphic` はアンチエイリアス幅を広げ、接続線のエッジを滑らかにする。
- `DesignTokenApplier` / `BuildUiPrefabs` / 各 UI スクリプトの `Outline.effectDistance` は `1, -1` 基準に寄せ、細線の見え方を揃える。
- `QualitySettings` は高品質寄りのプリセットを既定にし、UI と線描画の視認性改善を優先する。
- 日本語フォールバックは `TmpFontInitializer` が Editor / Runtime の両方で登録する。`TMP_Settings.fallbackFontAssets` を空のままにしない。
- フォールバック候補フォントは Windows の `Yu Gothic UI` / `Meiryo UI` などを優先し、`あ / ア / 漢 / （ / ）` を描画できるものだけを採用する。

## 33. 2026-06-29 Canvas Reference Resolution
- CanvasScaler の Reference Resolution は `1920x1080` に統一する。
- `DesignTokens.ReferenceResolution` を正とし、`BuildUiPrefabs` と `DesignTokenApplier` は同じ値を参照する。
- 既存仕様の `Reference 1920x1080` を維持し、実装側に残っていた `2560x1440` 固定値は使わない。

## 34. 2026-06-29 Foundation Color / Labels / Layout
- アクセント色を `#2563EB`、hover を `#1D4ED8`、press を `#1E40AF` に更新した。
- Start/End ノードは強い青/赤塗りをやめ、`Surface` 背景 + `Divider` アウトラインの静かな表示にする。
- 設定ボタンは Unicode 歯車単独ではなく `設定` の日本語ラベルで表示する。
- Scenario graph の主要操作ラベルは `+ 手順` / `+ 条件` / `保存` とする。
- ラップトップ対応として Catalog 幅を `min=240`, `default=312`, `max=420`、Scenario graph 高さを `min=220`, `default=320`, `max=720` に寄せる。

## 35. 2026-06-29 Viewport State Feedback
- `ViewportStatusStrip` を追加し、3Dビュー上部に現在モード、配置対象、選択中オブジェクト、配置成功メッセージを表示する。
- `PlacementController.ObjectPlaced` を追加し、配置成功をUIへ通知する。
- `WorkspaceFloorGrid` を追加し、実行時に床グリッドを補完して灰色の無地感を減らす。
- `SelectionOutline` のライン色を `DesignTokens.Accent` に寄せる。

## 36. 2026-06-29 Catalog Card Information Density
- カタログカードをカテゴリバッジ、表示名、技術IDの3段構成に更新する。
- `CatalogUI` は既存Prefabでも `Badge_Category` / `LabelCategory` / `LabelTechnicalId` をランタイム補完する。
- `BuildUiPrefabs` の `Card_Template` も同じ3段構成で生成する。
- `DesignTokenApplier` は旧中央寄せ補正をやめ、新カードレイアウトを維持する。

## 37. 2026-06-29 Scenario Graph Japanese Terminology
- `StepNodeUI` の見出しを `STEP n` から `手順 n` に変更する。
- `ConditionNodeUI` の見出しを `条件 n` に統一する。
- `BuildUiPrefabs` の Step node template も `手順 1` で生成する。
- `CurriculumGraphService` の新規Stepタイトルと保存JSONの required action 名も `手順 n` とする。
