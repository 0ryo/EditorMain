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
  - パネル背景: `0.96, 0.96, 0.96, 1`
  - ノード/主要ボタン: 薄い黄系（例: `1, 0.98, 0.86, 1`）
  - 接続線: 明るい黄（`1, 0.92, 0.2, 1`）
  - ドロップダウン背景: グレー系（`0.92`〜`0.97`）
- フォント
  - ランタイムで参照する built-in font は `LegacyRuntime.ttf` を使用。
- 視認性
  - ドロップダウン文字色は黒で固定。
  - 罫線やアウトラインに依存せず、背景色差で識別する。

## 5. パネル配置・リサイズルール
- `Panel_Catalog`
  - 画面左側固定、上下端まで表示。
  - 横幅可変（`PanelHorizontalResizeHandle`）
  - 幅制限: `min=220`, `max=720`
- `Panel_ScenarioGraph`
  - 画面下部固定、右側に展開。
  - 高さ可変（`PanelVerticalResizeHandle`）
  - 高さ制限: `min=180`, `max=720`
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
  - `typeId` を表示
  - カード高は `84` 固定
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
- 座標
  - `x,z` は `0.1m` グリッドスナップ
  - `y = hit.y + 0.5`
- 生成
  - `PlacedObject` を保証
  - `InitType(typeId)` 実行
  - `ForceNewId()` で `obj-0001` 形式 ID を保証
  - 回転は `Quaternion.identity`
  - 配置後は自動選択

## 8. ノード追加ウィンドウ仕様
- 対象: `Panel_ScenarioGraph` / `ScenarioGraphUI`
- UI構成
  - TopBar: プロジェクト名、`+ Step`、`+ Condition`、`Save`、ステータス
  - NodeArea: ノード配置領域
  - LineLayer: 接続線描画レイヤー
  - 上端に縦リサイズハンドル
- ノード種別
  - `Start` / `End`（各1つ）
  - `Step`（複数）
  - `Condition`（複数）
- `+ Step`
  - `CurriculumGraphService.AddStep()` を実行
  - 既存ノード位置は保持
  - 未保存位置がないノードは自動整列位置を適用
- `+ Condition`
  - `CurriculumGraphService.AddCondition()` を実行
  - 新規Conditionノードを追加
- ノード移動
  - `NodeDragHandler` でドラッグ移動可能
- 保存
  - `Assets/Exports/<ProjectName>-curriculum.json` に保存
  - 保存前に E-01〜E-11 を検証し、エラー時は `Save` を無効化
  - 出力形式は `version=2` + `scenarioSettings` + `requiredActions[].conditions[]`

## 9. ノードカード仕様
- `StepNodeUI`
  - 表示: `STEP n`、条件数サマリ、警告アイコン
  - 接続: 入力1 / 出力1（StepFlow）
- `ConditionNodeUI`
  - 表示: `Condition nodeId`、`DropdownA` + `DropdownB`
  - 接続: 出力1（ConditionBind）
- `TerminalNodeUI`
  - `START`: 出力のみ
  - `END`: 入力のみ

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
