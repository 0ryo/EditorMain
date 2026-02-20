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
- 検索
  - `typeId` の部分一致
  - 大文字/小文字を区別しない
- クリック配置
  - カードクリックで `PlacementController.EnterPlacement(typeId)`
  - 3Dビュー次クリックで 1 回配置し配置モード解除
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
  - TopBar: プロジェクト名、`+ Step`、`Save`、ステータス
  - NodeArea: ノード配置領域
  - LineLayer: 接続線描画レイヤー
  - 上端に縦リサイズハンドル
- `+ Step`
  - `CurriculumGraphService.AddStep()` を実行
  - 既存ノード位置は保持
  - 新規ノードは NodeArea 中心付近（既定 `anchoredPosition = 0,0`）
- ノード移動
  - `NodeDragHandler` でドラッグ移動可能
- 保存
  - `Assets/Exports/<ProjectName>-curriculum.json` に保存
  - 未設定条件数をステータスに表示

## 9. ノードカード仕様
- `StepNodeUI` をテンプレートから複製して使用
- 構成
  - 手順 ID 表示
  - タイトル入力
  - 入力/出力コネクタ（左右）
  - 条件行（1行固定）
- 条件行
  - `DropdownA` + 文言 `を`
  - `DropdownB` + 文言 `に近づけたら`
  - `+条件` ボタンは廃止

## 10. 条件ドロップダウン仕様
- 対象: `ConditionRowUI`, `PlacedObjectOptionProvider`
- 選択肢
  - 先頭は常に `未設定`
  - 以降は現在ワールドにある `PlacedObject.id`（`obj-xxxx`）を表示
- 更新
  - `StepNodeUI` が 0.2 秒間隔で選択肢差分を監視し再バインド
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
- 接続線
  - `ConnectionLineGraphic` で描画
  - 色は明るい黄色、太さ `8`
  - `LineLayer` 上に描画
  - `CanvasRenderer` 欠落時はランタイム補完
- 接続データ
  - `CurriculumGraphService.AddEdge(from, to)` を呼ぶ
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

## 14. 運用ルール（今後）
- UIを変更する場合は、先に `UIRoot.prefab` と対応スクリプトの責務分離を維持する。
- レイアウト変更は Prefab 側を優先し、ロジック変更は `CatalogUI` / `ScenarioGraphUI` / `StepNodeUI` に閉じ込める。
- 本仕様と差異が出た場合は、同一PR/同一コミット系列で `Docs/worklog_UI/` を更新する。
