# worklog_latest

## 0. 対象範囲
- ブランチ: `codex/ui`
- 作業テーマ: MVP-4「シナリオ作成機能」
- 参照仕様: `Docs/MVP-4.md`
- 最終更新: 2026-02-22

## 1. 実装サマリ（今回）
- シナリオ内部モデルを `Step単体` から `Start/End/Step/Condition` のグラフ構造へ拡張。
- 接続ルール（StepFlow/ConditionBind）と、MVP-4仕様のバリデーション（E-01〜E-11）を `CurriculumGraphService` に実装。
- Graphから `requiredActions[].conditions[]` を持つ `version=2` JSONへ変換するエクスポート処理を追加。
- ノードUIをMVP-4向けに更新し、`+ Condition` 導線、保存可否制御、エラー/警告表示を追加。
- Editor自動化エントリ `AutomationEntry` を追加（UI適用・移行・検証メソッド）。
- UI仕様文書をMVP-4実装内容に合わせて更新。

## 2. 機能別の詳細

### 2.1 データモデル拡張
- `CurriculumModel` を更新し、`nodes[]` / `edges[]` を追加。
- `ScenarioNodeType`（Start/End/Step/Condition）と `ScenarioEdgeType`（StepFlow/ConditionBind）を定義。
- Conditionノード用データ（A/B objectId）を保持可能に変更。

### 2.2 グラフサービス（中核）
- `CurriculumGraphService` を再設計。
  - Start/Endノード自動保証
  - Step/Condition追加
  - 接続制約チェック（重複・自己接続・分岐・循環・Condition上限）
  - 参照切れ補正（削除オブジェクト参照をnull化）
  - E-01〜E-11検証、W-01/W-02警告
  - 線形Step列確定（Start→...→End）
  - Graph→`ScenarioExport(version=2)` 変換

### 2.3 エクスポート形式
- `ScenarioExportModel` を新規追加。
- 出力JSONに以下を含める構成へ更新。
  - `version: 2`
  - `scenarioSettings`（`holdSeconds`, `snapDistance_m`）
  - `requiredActions[].conditions[]`（`type=SnapHold`, `aObjectId`, `bObjectId`, `holdSeconds`）
  - `objects[]`

### 2.4 UI（ノードエディタ）
- `ScenarioGraphUI` をMVP-4仕様に合わせて更新。
  - `+ Step` / `+ Condition`
  - Start/End/Step/Conditionノード描画
  - 接続失敗理由の表示
  - バリデーション結果によるSave可否制御
  - 保存時に `ScenarioExport` を原子的書き込み
- `StepNodeUI` をStep表示専用に整理（`STEP n` + 条件数サマリ）。
- `ConditionNodeUI` を新規追加（A/Bドロップダウン編集）。
- `TerminalNodeUI` を新規追加（START/END）。
- `ConnectorDragHandler` をStep前提からノードID前提へ変更。

### 2.5 Editor自動化
- `BuildUiPrefabs` を更新。
  - TopBarに `Button_AddCondition` を追加
  - `ScenarioGraphUI` 新規フィールドの自動割当
  - Stepテンプレートに `Text_ConditionSummary` を追加
- `AutomationEntry` を新規追加。
  - `ApplyUiEdits()`
  - `MigrateScenarioData()`
  - `ValidateProject()`

### 2.6 ドキュメント更新
- `Docs/worklog_UI/全体UI仕様.md` をMVP-4仕様へ更新。
- `Docs/worklog_UI/worklog_ノード追加ウィンドウ.md` をMVP-4仕様へ更新。

## 3. 変更ファイル
- 変更: `Assets/Scripts/Core/CurriculumModel.cs`
- 追加: `Assets/Scripts/Core/ScenarioExportModel.cs`
- 変更: `Assets/Scripts/Services/CurriculumGraphService.cs`
- 変更: `Assets/Scripts/UI/ScenarioGraphUI.cs`
- 変更: `Assets/Scripts/UI/StepNodeUI.cs`
- 変更: `Assets/Scripts/UI/ConnectorDragHandler.cs`
- 追加: `Assets/Scripts/UI/ConditionNodeUI.cs`
- 追加: `Assets/Scripts/UI/TerminalNodeUI.cs`
- 変更: `Assets/Editor/Automation/BuildUiPrefabs.cs`
- 追加: `Assets/Editor/Automation/AutomationEntry.cs`
- 変更: `Docs/worklog_UI/全体UI仕様.md`
- 変更: `Docs/worklog_UI/worklog_ノード追加ウィンドウ.md`

## 4. 検証状況
- この実行環境では `dotnet` / `msbuild` / `csc` / `Unity` 実行バイナリが見つからず、CLIコンパイル・Unityバッチ実行は未実施。
- そのため、最終確認はUnity Editor上でのコンパイル・実機動作確認が必要。

## 5. 引き継ぎ注意点
- 次の作業では、まず `AutomationEntry.ApplyUiEdits` 実行でPrefab参照を最新化する。
- その後、MVP-4バリデーション（E-01〜E-11）のUI表示とExport結果をEditor上で確認する。
- 追加でQuest側ランタイム（SnapHold評価）の実装が必要な場合は `Docs/MVP-4.md` の8章契約に合わせる。
