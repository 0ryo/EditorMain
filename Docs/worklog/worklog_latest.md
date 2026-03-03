# worklog_latest

## 0. 対象範囲
- ブランチ: `improve/objectlist`
- 作業テーマ: オブジェクト一覧カードの削除導線追加（右上 `x`）
- 最終更新: 2026-03-02
- 参照コミット:
  - `eb3fc47c` rulesを更新

## 1. Phase A 現状把握
### 1.1 Unityバージョン
- `ProjectSettings/ProjectVersion.txt`: `6000.2.6f2`

### 1.2 UI方式
- `.uxml/.uss` は未検出。
- `UnityEngine.UI` ベースの `uGUI` 構成を使用。

### 1.3 UI構造メモ（主要Scene/Prefab/入口）
- 主要Scene: `Assets/EditorMain.unity`
- UIルート: `Assets/UI/Prefabs/UIRoot.prefab`
- 主要UI入口（暫定）:
  - `CatalogUI`
  - `UiPanelDockSync`
  - `EditModeService`

## 2. 実装サマリ（このブランチ）
- オブジェクト一覧カードに小型 `×` ボタン（`Button_RemoveCard`）を追加。
- `×` はカードホバー時のみ表示されるように変更。
- `×` はカード右上角の外側にはみ出し、丸の中心がカード角に重なる配置へ変更。
- `×` 背景を完全な丸形に固定。
- `UiRoundedTheme` の角丸一括適用から `Button_RemoveCard` を除外し、ノード接続点と同じ丸形維持ルートへ統一。
- カードクリックで配置待ち状態になった `typeId` のカードを、`DesignTokens.Accent`（青）の枠線で強調表示するように変更。
- `PlacementController` の `Instantiate` 呼び出しを `UnityEngine.Object.Instantiate` に明示し、`CS0104`（`Object` の曖昧参照）を解消。
- `×` 押下時に対象カードを一覧から除去する挙動を `CatalogUI` に追加。
- 検索変更や `RebuildCards()` 実行後も、同一セッション内では除去済みカードが復活しないように調整。
- 既存Prefab互換のため、`CatalogUI` は削除ボタン未配置カードにもランタイム補完で `x` を生成。
- `BuildUiPrefabs` の `Card_Template` にも `Button_RemoveCard` を追加し、Prefab自動生成経路を同期。
- UI仕様ドキュメント（オブジェクト一覧 / 全体UI仕様）へ今回挙動を追記。

## 3. 変更ファイル
- `Assets/Scripts/CatalogUI.cs`
- `Assets/Editor/Automation/BuildUiPrefabs.cs`
- `Docs/worklog/worklog_UI/worklog_オブジェクト一覧ウィンドウ.md`
- `Docs/worklog/worklog_UI/全体UI仕様.md`
- `Docs/worklog/worklog_latest.md`

## 4. 操作仕様（現行）
- オブジェクト一覧の各カードに、ホバー時のみ `×` ボタンを表示。
- `×` はカード右上角の外側にはみ出し、丸の中心がカード角に重なる。
- カードクリック後、ワールドクリック待ちの配置モード中は該当カードを青枠で強調表示する。
- `×` 押下で該当カードをオブジェクト一覧から除去。
- この除去は一覧表示のみで、ワールド内の既存オブジェクトには影響しない。

## 5. 検証状況
- AGENTS.md の Local Execution Policy に従い、Unity Editor 起動・CLIコンパイルは未実施。
- 静的確認として、以下を実施:
  - ブランチ名・Unityバージョン・UI方式を確認
  - 関連スクリプト/Prefab生成コードの参照整合を確認
  - ドキュメント更新差分を確認

## 6. 人間確認チェックリスト
- [ ] オブジェクト一覧カードにホバーしたときのみ小型 `×` が表示される。
- [ ] `×` がカード右上角に対して半分はみ出し、丸の中心が角に重なっている。
- [ ] カードクリック直後、配置待ち中のカードだけ青枠で強調表示される。
- [ ] ワールド配置完了後（または配置モード解除後）に青枠が消える。
- [ ] `×` 押下で該当カードが一覧から消える。
- [ ] 検索ワード変更後も除去したカードが再表示されない。
- [ ] 設定画面や他UI操作に副作用がない（カードクリック配置・ドラッグ配置が従来どおり動く）。

## 7. アーカイブ
- 旧 `Docs/worklog/worklog_latest.md`（`view/settings` 内容）は以下へ archive 済み:
  - `Docs/worklog/worklog_2026-03-02_archive_view_settings.md`

## 8. セッション進捗メモ（2026-03-02）
- シナリオノードUI作業について、このセッションは **タスク2の調整完了** まで到達。
- タスク2で完了した範囲:
  - Conditionノード見た目調整（余白・ドロップダウン配置・白背景＋細線アウトライン）
  - 不要な内側枠の除去
  - Conditionノードのドラッグ移動対応
  - CS0136ホットフィックス
  - 条件設定領域の黄色背景解消
- タスク3（内包状態の階層構造と動的サイズ変更）は未着手。

## 9. セッション進捗メモ（2026-03-03）— Task3

### 実装サマリ
- `ConditionNodeUI` に `EnterEmbeddedMode(int sequentialIndex)` を追加。
  - 出力コネクタを非表示化（埋め込み時は不要）。
  - ヘッダーラベルを nodeId ベースの番号から **ステップ内連番**（手順 1, 手順 2 …）に切り替え。
- `StepNodeUI` の埋め込み条件ループを改修。
  - `validConditions` で null/未設定を除外し、連番 `displayIdx + 1` を `EnterEmbeddedMode` に渡す。
  - 各カードの後（最後を除く）に `AddEmbeddedDivider` を呼び、`DesignTokens.Divider` 色の 1px 水平線を `conditionListRoot` に追加。
  - `EmbeddedSpacing` を 16 → 8 に変更（区切り線の前後 8px ずつで計 17px の視覚的なギャップ）。
- `ResizeForEmbeddedCount` のリサイズ計算式を更新。
  - VLG 内の配置: `[C, D, C, D, ..., C]` (Condition + Divider 交互)
  - `embeddedHeight = N*condH + (N-1)*DivH + (2*(N-1))*spacing`
- `UiRoundedTheme.ShouldApply` に `"Divider"` 除外を追加（1px 画像への角丸スプライト誤適用を防止）。

### 変更ファイル
- `Assets/Scripts/UI/ConditionNodeUI.cs`
- `Assets/Scripts/UI/StepNodeUI.cs`
- `Assets/Scripts/UI/UiRoundedTheme.cs`

### 人間確認チェックリスト（Task3）
- [ ] Condition ノードを Step ノード付近にドラッグ＆ドロップすると自動バインド・埋め込み表示になる。
- [ ] 埋め込み条件カードのヘッダーが「手順 1」「手順 2」… と連番で表示される（nodeId の番号ではない）。
- [ ] 埋め込み条件カードに出力コネクタ（黄丸）が表示されない。
- [ ] 埋め込み条件が 2 枚以上あるとき、カード間に 1px のグレー区切り線が表示される。
- [ ] Step カードが埋め込み条件数に合わせて縦に拡張される（1 枚: ~284px / 2 枚: ~481px / 3 枚: ~678px）。
- [ ] 埋め込み条件内のドロップダウン（A/B）が選択・編集できる。
- [ ] 埋め込み条件カードの削除ボタン（X）が機能し、条件ノードをグラフから除去できる。
- [ ] 接続線を埋め込みドロップダウン上でクリックしても、ドロップダウンが優先して反応する。

## 10. セッション進捗メモ（2026-03-03）— ターミナルノード色・ドラッグ修正

### 実装サマリ
- `DesignTokens` に `NodeStart` (#89C3FF) / `NodeEnd` (#FF898B) を追加。
- `DesignTokenApplier.ApplyNodeColors` のターミナルノードループを改修。
  - `GetTerminalColor()` で labelText を見て START/END を判別し、単色を返す。
  - `HideTerminalDragHandle()` で DragHandle の `Image.enabled = false` / `Outline.enabled = false`。
    - 以前の `ApplyTerminalDragHandleColor()` が呼んでいた `EnsureThinOutline` が境界線の原因だったため削除。
- `ScenarioGraphUI.ConfigureNodeDragCallbacks()` に Start/End 専用ブランチを追加。
  - ルートに `NodeDragHandler` を AddComponent し、ノード全体をドラッグ可能にした。
  - 以前のコードは DragHandle 子から `NodeDragHandler` を取得しようとして常に null → ドラッグ不能だった。

### 変更ファイル
- `Assets/Scripts/UI/DesignTokens.cs`
- `Assets/Scripts/UI/DesignTokenApplier.cs`
- `Assets/Scripts/UI/ScenarioGraphUI.cs`

### 人間確認チェックリスト（ターミナルノード）
- [ ] START / END ノードが指定の単色（青 / 赤）で表示される。
- [ ] 2色の境界線（以前の DragHandle アウトライン）が消えている。
- [ ] START / END ノードのどこを掴んでもドラッグで移動できる。

## 11. セッション進捗メモ（2026-03-03）— オブジェクト詳細パネル

### Task 1 完了: SelectionService 選択変更イベント追加
- `SelectionService` に `public event System.Action<PlacedObject> OnSelectionChanged` を追加。
- `Select(PlacedObject po)` 内で `Current` 更新・アウトライン更新の直後に `OnSelectionChanged?.Invoke(po)` を発火。
- `null`（選択解除）もそのまま渡すため、購読側で表示/非表示を一元管理できる。

### 変更ファイル（Task 1）
- `Assets/Scripts/SelectionService.cs`

### Task 2 完了: CatalogUI メタデータ公開 API 追加
- `CatalogUI` に `public bool TryGetTypeInfo(string typeId, out string label, out string description)` を追加。
- `cards` リストを OrdinalIgnoreCase で線形検索し、一致すれば `displayLabel` / `displayDescription` を返す。
- 見つからない場合は `label = typeId`、`description = empty` にフォールバックして `false` を返す。

### 変更ファイル（Task 2）
- `Assets/Scripts/CatalogUI.cs`

### Task 3 完了: ObjectDetailPanel スクリプト作成
- `Assets/Scripts/UI/ObjectDetailPanel.cs` を新規作成。
- `Start()` で `SelectionService` / `CatalogUI` を `FindFirstObjectByType` で自動取得。
- `SelectionService.OnSelectionChanged` を購読し、`OnDestroy()` で購読解除。
- 選択解除 (`null`) → `gameObject.SetActive(false)`。
- 選択時 → `Populate()` でテキスト設定後に `SetActive(true)`。
  - `textPrefabLabel`: `CatalogUI.TryGetTypeInfo` の label
  - `textObjectName`: `po.gameObject.name`
  - `textDescription` / `rowDescription`: description が空なら行ごと非表示
- `DesignTokenApplier.ApplyDetailPanel` の空スタブを `DesignTokenApplier.cs` に追加（Task 5 で実装）。

### 変更ファイル（Task 3）
- `Assets/Scripts/UI/ObjectDetailPanel.cs`（新規）
- `Assets/Scripts/UI/DesignTokenApplier.cs`（ApplyDetailPanel スタブ追加）

### Task 4 完了: BuildUiPrefabs 詳細パネル生成処理追加
- `Build()` に `BuildDetailPanel(root.transform)` 呼び出しを追加。
- `BuildDetailPanel()`: `Panel_Detail` を右アンカー（anchorMin.x=1）・幅288px・全高さで生成。
  - ヘッダー（"オブジェクト詳細"、Surface 背景）
  - `Scroll_Detail`（ScrollRect）→ Viewport → Content（VerticalLayoutGroup + ContentSizeFitter）
  - `Row_PrefabLabel` / Divider / `Row_ObjectName` / Divider / `Row_Description` を Content に追加
  - `ObjectDetailPanel` を AddComponent し SerializedObject で各 Text・rowDescription を配線
  - 初期 `SetActive(false)`
- `BuildDetailRow()`: 見出し Label（caption/TextSecondary）+ 値テキスト（body/TextPrimary、WordWrap）の行を生成。ContentSizeFitter で高さ自動拡張。
- `BuildDetailDivider()`: 1px Divider Image を LayoutElement で管理。

### 変更ファイル（Task 4）
- `Assets/Editor/Automation/BuildUiPrefabs.cs`

### Task 5 完了: DesignTokenApplier.ApplyDetailPanel 実装
- スタブを完全実装に置き換え。
- 適用順序:
  1. `ApplyCanvasResolution` — QHD 強制
  2. パネル背景 → `BgPrimary`
  3. `Header` → `Surface`、内包 Title テキスト → `TextPrimary`
  4. `Viewport` → `Surface`
  5. `Content` 直下を走査:
     - `Row_*` → `Surface`、`Label` 子 → `TextSecondary`、`Text_*` 子 → `TextPrimary`
     - `Divider` → `DesignTokens.Divider`

### 変更ファイル（Task 5）
- `Assets/Scripts/UI/DesignTokenApplier.cs`

### 人間確認チェックリスト（オブジェクト詳細パネル）
- [ ] `Tools > Automation > Build UI Prefabs` を実行すると `UIRoot.prefab` に `Panel_Detail` が追加される。
- [ ] ワールド上のオブジェクトをクリックすると画面右側にパネルが表示される。
- [ ] パネルに「プレファブ名」「オブジェクト名」が正しく表示される。
- [ ] 説明文があるオブジェクトでは「説明」行も表示される。
- [ ] 説明文がないオブジェクト（レジストリ由来）では「説明」行が非表示になる。
- [ ] オブジェクトの選択を解除するとパネルが非表示になる。
- [ ] パネルの色がデザイントークン準拠（背景 BgPrimary、ヘッダー Surface、見出し TextSecondary、値 TextPrimary）になっている。
