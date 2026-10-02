# 全体UI仕様

## 現行SkillSync UIの教材読込導線

- 上部は「ヘルプ」→「教材を読み込む」→「↑ 教材を書き出す」の順に配置する。
- 「教材を読み込む」は隣接する「ヘルプ」と同じ白地・角丸10・枠線1の通常ボタンとし、文字・中央位置補正・操作時の設定はヘルプから引き継ぐ。幅だけは教材読込の文字数に合わせる。既に追加済みのボタンにも再適用する。
- 「教材を読み込む」は既存のプロジェクト一覧を保存済み教材タブで開く。Ctrl/Cmd+Oも同じ入口。配布用JSONのインポートではなく、保存済み`.skillsync.json`の再編集用読込である。
- 試行中・仮配置中・対象選択中・検証モーダル中は教材読込を無効にする。
- SkillSync UIでは旧右上「ヒント」を表示・再生成しない。操作説明は上部「ヘルプ」から開く。
- Prefabへの反映は`Tools/Automation/SkillSync/Apply Viewport Refinements`。既存Prefabの実行時互換補完も同じボタン生成処理を使う。Unity上でのレイアウト・操作確認は別途必要。

## 現行SkillSync UIの完了条件操作

- 手順編集の完了条件見出しに、選択中の条件番号（`1/2`）と左右の移動ボタン（`<` / `>`）を表示する。条件が2件以上あるときだけ移動ボタンを表示し、先頭では`<`、末尾では`>`を無効にする。
- 「この条件を削除」ボタンは選択中の完了条件だけを削除し、既存のグラフコマンド履歴からUndoできる。削除後は近い残存条件を選択し、最後の条件を削除した場合は`0件`と追加案内を表示する。
- Prefabへの反映は`Tools/Automation/SkillSync/Apply Viewport Refinements`。Unity上で前後移動、削除、Undo、条件0件時の表示を確認する。

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
  - `EditWorkspace.TryScreenToGround` でカメラから y=0 の作業平面へ `ScreenPointToRay` / `Plane.Raycast` する
  - 床Colliderは配置入力の必須条件にしない。`WorkspaceFloorGrid` は視覚補助として扱う
- 3Dビュー補助表示
  - `WorkspaceFloorGrid` が実行時に広い半透明の床面と、同じ太さの半透明格子線/X/Z中心線を補完する
  - `ViewportStatusStrip` が `閲覧中` / `配置中` / `移動中` / `スケール調整` と対象情報を表示する
  - デバッグ時は `ViewportStatusStrip` にカメラ座標、ズーム値、直近の配置ログを表示する
  - Main Camera は起動時に `(0, 6, -10)` から原点を見る既定ビューへ補正する
  - 3Dビュー操作は中ドラッグで回転、Shift+中ドラッグで平行移動、ホイールでズームする
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
  - Startの出力は最大1。Stepは複数接続を許可し、全成功条件の達成後に進路を選ぶ。Step/Endへの合流を許可する。
  - StepFlowは循環禁止
  - Conditionは1つのStepにのみ接続
  - StepのCondition受け取り上限は既定8（設定範囲1〜32）
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

## 38. 2026-08-22 Remaining UI Polish
- 空の手順ノードは `条件を追加してください` を表示する。
- Catalogの横リサイズ境界とScenario graphの縦リサイズ境界は、細いグリップ線と方向カーソルで操作可能範囲を示す。
- Scenario graphは編集中に `要確認: n件` の短い状態だけを表示し、保存操作時に詳細な問題一覧を開く。
- 問題一覧の関連nodeId付き項目を選ぶと、該当ノードまたはConditionを格納するStepへ表示を移す。
- Object detailは表示名と技術IDをheaderに表示し、`基本情報` / `説明` / `使用中の条件` のsectionに分ける。
- Object detailの未使用時は `このオブジェクトはまだ手順で使われていません` と表示し、200msのslide＋fadeで開閉する。
- Object detailは画面右端の上端から下端まで表示し、開閉時はglobalの `設定` と `ヒント` をパネル幅に合わせて水平移動する。
- Global操作として `ヒント` を追加し、内容差し替え可能なplaceholder panelを開く。
- Catalog card右側にneutral colorのcategory fallback blockを表示する。

## 教材作成機能の操作

- 一覧と3D空間はShift/Ctrl+クリックで追加選択・解除する。複数選択した全対象に形状に沿う青い輪郭を表示し、最後に選択した対象をギズモと数値Transformの基準にする。輪郭は遮蔽物越しにも表示し、他の選択対象に埋まった物も個別に確認できる。選択中の対象を3D空間で通常クリックすると、選択集合を保ったまま基準を切り替える。
- 通常の選択枠は矩形を使わない。拡縮モードでは従来の角ドラッグ位置を小さなハンドルで示す。候補確認のオレンジ輪郭と通常選択の青輪郭は別状態とし、同じ位置ではオレンジを上に表示する。
- 移動・回転・拡大縮小・複製・削除は選択集合に適用し、一度のUndoで戻せる。固定・非表示の対象は選択集合に含めない。
- 一覧の整列操作は初期状態で閉じた`> 整列メニュー`から開き、`v 整列メニュー`で閉じる。閉じた分だけ一覧領域を広げる。常設の複数選択・整列説明は表示しない。X/Y/Z整列は最後に選択した対象の原点にそろえる。等間隔は各軸の両端を維持し、3個以上の選択を均等に配置する。操作可能な個数に満たないボタンは無効化する。
- 新規条件は「近づける」「近づけて保持」「押す」「引く」「もつ」「回す」。回すには角度（度）の数値欄を表示する。旧条件は既存データの互換用に保持する。種類に必要な対象と数値欄だけ表示する。失敗設定は追加しない。
- 対象ドロップダウンは高速スクロールと名前の折り返しに対応する。候補へのホバーで全文と遮蔽物越しのオレンジ輪郭を表示し、選択済み欄へのホバーでも確認できる。ホバーは編集選択を変えない。
- 対象リスト上部で名前の部分一致検索（英字の大文字小文字を区別しない）ができる。「画面上から選択」は対象欄を挟んでリストの反対側へ表示する。ピペットで3D上の対象をクリックすると、操作を始めたA/B欄へ設定する。Esc・右クリックで取り消す。入力待ち中は通常の配置・変形・編集選択を抑止する。
- 「画面上から選択」は元の対象欄と左右端・幅をそろえ、8pxの間隔を置く。アクセント色の背景に中央揃えの明るい文字を使い、44pxの高さを確保する。検索欄は固定ヘッダーとしてリストのスクロール領域から分離する。
- Bを使わない条件（押す・引く・もつ・回すなど）は「対象を 動作」の1行に詰める。回すの角度欄も繰り上げる。近づける・近づけて保持などへ戻したらB欄を含む2行に戻す。親の手順カードも各条件の実際の高さに合わせる。
- 手順の出力から複数の手順へ接続すると分岐になる。手順カードには「成功後 n択」を表示する。
- プレビューでは「成功を模擬」を押して次への操作を有効化し、複数接続の場合は手順名付きの進路ボタンを表示する。「前の手順」は実際に選んだ経路を戻り、成功の模擬状態をリセットする。
- 追加モデルは名前・説明とともに保存され、再起動後にカタログへ復元する。復元中に教材を開こうとした場合は、復元完了後に開くよう案内する。
- 追加した静的モデルを新しく配置すると、内部のMeshとその親グループを「一覧」に親子順で表示する。親の行で車全体、子の行または3D上の形状で個々の部品を選択する。検索は部品名・配置ID・元モデルの表示名に対応する。スキニングまたはLODを含むモデルは全体のまま扱う。
- 親と子を同時選択して変形を二重適用しないよう、親を選択集合に含めた場合はその子を除外する。親の移動・回転・拡縮・非表示・固定は子に継承し、親を再表示しても子自身の非表示状態を維持する。部品だけの複製は独立配置になる。
- 部品は名前、local Transform、表示/固定、削除状態、条件参照IDを保存・再読込できる。旧保存形式の配置は全体のまま読み込むため、部品編集にはカタログから新しく配置する。
- UI更新は `Tools/Automation/Apply Authoring Features`。Scene/Prefab YAMLを直接編集しない。

## ビューポートの表示

- 床はグリッド線と軸線だけを表示し、下面からの視界を遮る面を生成しない。
- 移動ギズモは約2pxの線、小さな円錐先端、滑らかな四分円と円形ハンドルを使う。拡縮の角ハンドルも円形とし、描画幅と選択幅を分離する。
- 編集カメラ専用のUniversal Rendererと視点追従する主光・補助光を使う。元のモデル材質は維持し、UIに照明効果を加えない。
- 初期導入は `Tools > Automation > Apply Viewport Improvements`。実行時互換補完とPrefab生成は同じ一覧更新経路を使う。

- カメラの初期表示と視点リセットは透視投影。ホイール1段で距離を約8%変更し、近距離ほど細かく調整する。入力は整数段に丸めて即時反映し、半段未満の微小入力は蓄積せず捨てる。平行投影への切替は維持する。

## 教材ライブラリ

- プロジェクト画面の一覧を「保存済み」「テンプレート」「削除済み」の3タブで切り替える。行は92px、名称・更新日時と操作ボタンを2段で配置する。
- 保存済み教材は読込・複製・テンプレート化・削除を提供する。複製／テンプレート化はディスク上の保存済み内容を対象にし、現在の編集中データは置き換えない。
- テンプレートの「教材を作成」は保存済み一覧へ独立したコピーを作る。利用者はそのコピーを読み込んで編集する。部品ID・条件参照は教材内部で保持し、素材ライブラリは共用する。
- 削除は確認後に削除済みへ移し、同タブの復元操作で元の分類へ戻す。現在編集中の教材は「編集中」と表示して削除を無効化する。完全削除機能は提供しない。
- 複製・復元先に同名がある場合は番号を付け、既存教材を上書きしない。復元後の通常保存は現在のファイルへ書き込む。
- Unity確認: 複製→読込→編集→保存、テンプレート→教材作成→編集して原本が変わらないこと、削除→同名を作成→復元して両方が残ること、入力・スクロール・確認キャンセルを確認する。
## UI遷移と動きの設定

- プロジェクト／確認、設定、追加モデル設定、ヒント、カメラメニュー、シナリオのプレビュー／問題一覧を、表示0.14秒・非表示0.10秒のフェードで切り替える。配置／一覧タブと教材ライブラリのタブにもフェードを適用する。
- 開いた時点から操作可能とし、閉じ始めたらパネル内の操作を止める。モーダルは非表示になるまで背後へのクリックを遮断する。連続開閉は現在の透明度から反転し、遅延コールバックで後の操作を上書きしない。
- ヒント画面の「アニメーション: ON/OFF」で切り替える。OFFは即時表示とし、詳細パネルのスライドも省略する。設定は次回起動へ引き継ぐ。
- 教材ライブラリの各タブに教材名の部分一致検索を設ける。検索入力中は一覧キャッシュを絞り込み、文字入力ごとのファイル再読込は行わない。タブ変更・複製・復元・画面再表示時に一覧を更新する。復旧用自動保存行は検索にかかわらず表示する。
- Unity確認: 各画面の連続開閉、切替途中の別タブ選択、検索該当なし／解除、確認パネルを開いたまま親画面を閉じて再表示、アニメーションOFFと再起動後の保持を確認する。

## 詳細パネルの開閉

- オブジェクト選択時は右端から約0.28秒でスライドし、選択解除時は右へ閉じる。開閉は同じ処理を使用し、途中の反転では現在位置と透明度を引き継ぐ。
- 最初の位置を一度描画してから時間を進め、重いフレームでも一気に終端へ飛ばさない。閉じる途中はパネルの背後へのクリックを遮断する。


## 集合編集と小画面操作

- 固定配置のSkillSync画面は2560×1440基準で画面全体に収める。以下の従来UIの倍率下限・折畳み規則は、この固定画面には適用しない。倍率は`UiScaleController`に集約し、固定位置のボタンを一律に拡大して隣の操作領域を侵さない。
- 教材一覧・設定・モデル追加のダイアログを開いている間は背面への編集キーを遮断し、Tab移動もダイアログ内に限定する。入力欄の編集中以外はEscでダイアログを閉じる。

- 配置オブジェクトはCtrl/Cmd+Gで平坦な選択グループ化、Ctrl/Cmd+Shift+Gで解除する。Transform階層と部品IDは変えず、保存・復元とUndo/Redoの対象とする。コピー先は別のグループIDを持つ。
- グラフはノード背景をクリックして選択し、Shift+クリックで追加/解除する。複数選択したノードのドラッグは集合移動となる。グラフ上でCtrl/Cmd+A/C/V、Delete、Escが利用でき、Ctrl/Cmd+Shift+L/Tで左端/上端を揃える。Stepコピーには埋込条件と選択内の接続を含める。入力欄の編集中と手前の別画面表示中はグラフshortcutを抑止する。
- 一覧・詳細・手順は作業領域のボタンとF9/F8/F10で折り畳む。詳細を手動で畳んだ後は選択を変えても開かず、手動で再表示する。1366px未満では未操作の一覧を畳み、本文が縮みすぎないようCanvasの実効倍率を1倍以上にする。
- GLB/glTF取込中は段階名を表示し、追加ボタンの再押下で中止する。読込済み割合ではないためパーセントは表示しない。FBXはUnityの同期Importであり中断対象外。


## 欠損テクスチャの検索・復元

- 選択オブジェクトの詳細パネルに「欠損テクスチャを探して復元」を置く。EditorではバイナリFBXの元画像と材質の接続を読み、テクスチャ欄が空になった材質へ再接続する。元のパスにある画像を優先し、見つからない場合は指定フォルダー以下の同じファイル名（拡張子を含む）を探す。同名複数・検索上限・元接続情報なしは理由を表示し、推測で割り当てない。
- 詳細パネルには復元ボタンのみを表示する。押すたびにフォルダー選択ダイアログを開き、選択確定後に復元する。キャンセル時は何も変更しない。パス入力欄、別のフォルダー選択ボタン、結果欄、コピー欄は置かない。
- 処理中はボタンを無効化し、選択変更時は古い検索を中止する。完了・失敗は画面上部中央の通知に表示する（通常6秒、失敗8秒、フェード付き、入力を妨げない）。詳しい復元記録はConsoleに残す。他形式やPlayerでの再接続は未対応として通知する。
- 画像と復元材質はAssets/RecoveredMaterialsへ保存し、FBXの外部材質割当も保存する。同一モデルの配置済みオブジェクトにも反映する。ベースカラー・法線・発光に対応し、Blender FBXの金属度／粗さと透明度はURP用画像に変換する。Blenderが出力した画像を使わない材質のベースカラー・金属度・粗さの定数も復元する。部品を選んでも所属モデル全体を対象とする。再import後にクリック選択用MeshColliderを再構築する。画像は1枚64 MB以下、取込解像度は最大4096。
- 検索は最大20,000ファイル／4,000フォルダー、各候補100件まで。リンク先は追跡せず、上限・読取不可は結果に明示する。検索・FBX参照読取はworkerで行い、画像取込・材質への反映はmain threadで行う。
