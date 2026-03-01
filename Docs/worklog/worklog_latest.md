# worklog_latest

## 0. 対象範囲
- ブランチ: `refactor/editmode`
- 作業テーマ: 編集モード刷新（閲覧/変形/スケール）とランタイムギズモ操作の導入
- 最終更新: 2026-03-01
- 参照コミット:
  - `548c39cb` 編集モードを改善
  - `30f7bb9c` 移動モード中にギズモ表示
  - `1b6e04be` ギズモUI調整
  - `95ba3868` マウスドラッグ移動の抑止

## 1. Phase A 現状把握
### 1.1 Unityバージョン
- `ProjectSettings/ProjectVersion.txt`: `6000.2.6f2`

### 1.2 UI方式
- `.uxml/.uss` は未使用。
- `UnityEngine.UI` ベースの `uGUI` 構成を使用。

### 1.3 UI構造メモ（主要Scene/Prefab/入口）
- 主要Scene: `Assets/EditorMain.unity`
- UIルート: `Assets/UI/Prefabs/UIRoot.prefab`
- 主要UI入口:
  - `CatalogUI`（オブジェクト一覧 + 編集モードボタン）
  - `ScenarioGraphUI`（シナリオ編集）
- モード制御入口: `EditModeService`（`CatalogUI` ボタン + `Tab` キー）

## 2. 実装サマリ（今回）
- `EditModeService` を再設計し、`Browse / Place / Transform / Scale` モードへ整理。
- `ModeChanged` イベントを追加し、UI側がモード状態に追従できるようにした。
- `CatalogUI` と `BuildUiPrefabs` に編集モード行（`閲覧 / 移動 / スケール`）を追加。
  - 既存Prefabでも動くよう、ランタイム補完生成と再バインド処理を実装。
  - アクティブモードのボタン色を `DesignTokens` で強調表示。
- `UiPanelDockSync` を拡張し、編集モード行の位置をカタログ幅変更に追従させた。
- `MoveTool` をランタイムギズモ方式へ全面更新。
  - 軸移動ハンドル（X/Y/Z）と回転ハンドルを表示。
  - ドラッグ中の移動スナップ、回転スナップ、Undo/Redo コマンド化を追加。
  - クリック選択との競合を回避するため、`SelectionService` へ入力消費フックを追加。
- `SelectionOutline` を拡張し、`Scale` モードでコーナードラッグによる等比スケールを追加。
  - 最小スケール軸をクランプし、`ScaleObjectCommand` でUndo/Redo対応。
- `MoveRotateCommands` に `RotateObjectQuaternionCommand` と `ScaleObjectCommand` を追加。
- `PlacementController` は `EditModeService.SetMode` 経由に統一。
- `RotateTool` は `Transform` モード判定に合わせて整合。
- 実行方針として `AGENTS.md` に Local Execution Policy（Unity起動禁止）を追記。

## 3. 変更ファイル
- `AGENTS.md`
- `Assets/Editor/Automation/BuildUiPrefabs.cs`
- `Assets/Scripts/CatalogUI.cs`
- `Assets/Scripts/EditModeService.cs`
- `Assets/Scripts/MoveRotateCommands.cs`
- `Assets/Scripts/MoveTool.cs`
- `Assets/Scripts/PlacementController.cs`
- `Assets/Scripts/RotateTool.cs`
- `Assets/Scripts/SelectionOutline.cs`
- `Assets/Scripts/SelectionService.cs`
- `Assets/Scripts/UI/UiPanelDockSync.cs`
- `Assets/_Recovery/0 (1).unity`
- `Assets/_Recovery/0 (1).unity.meta`

## 4. 操作仕様（現行）
- モード切替:
  - UI: `閲覧 / 移動 / スケール` ボタン
  - キー: `Tab` で `Transform` モードへ
- `Transform` モード:
  - 左クリックでギズモ軸ドラッグ移動
  - 回転ハンドルドラッグで軸回転（スナップあり）
  - `WASD` / 矢印キーでグリッド単位微移動
- `Scale` モード:
  - 選択アウトライン角付近をドラッグして等比スケール

## 5. 検証状況
- Local Execution Policy（2026-02-28）に従い、Unity Editor の起動・CLIコンパイルは未実施。
- 静的確認として以下を実施:
  - `git log` / `git diff` による変更範囲確認
  - `rg` による UI方式（uGUI）と主要UI参照確認

## 6. 人間確認チェックリスト
- [ ] カタログ上部に `閲覧 / 移動 / スケール` ボタンが表示される
- [ ] カタログ幅を変更しても編集モード行が追従し、重なりや隙間が出ない
- [ ] `移動` モードで選択物に X/Y/Z 軸ギズモと回転ハンドルが表示される
- [ ] ギズモ操作中、オブジェクト選択が意図せず切り替わらない
- [ ] ギズモ移動/回転後に Undo/Redo が正しく動作する
- [ ] `スケール` モードで角ドラッグにより等比スケールし、極小値で破綻しない
- [ ] `Tab` キーで `Transform` モード遷移し、InputField入力中は誤反応しない

## 7. worklog_UI 反映
- `Docs/worklog/worklog_UI/全体UI仕様.md` に編集モードUIとギズモ運用の追記を追加。
- `Docs/worklog/worklog_UI/worklog_オブジェクト一覧ウィンドウ.md` に編集モード行仕様を追記。

## 8. アーカイブ
- 旧 `Docs/worklog/worklog_latest.md` は `Docs/worklog/worklog_2026-02-25_refactor_orbit.md` として退避。

## 9. 追記（2026-03-01: 回転ハンドルロジック変更）
- 対象: `Assets/Scripts/MoveTool.cs`
- 回転ハンドルUIを「軸先端の点ハンドル」から「1/4アーク（XY / YZ / ZX）」へ変更。
  - `LineRenderer` でアークを描画し、複数 `BoxCollider` で当たり判定を構成。
  - 回転軸マッピングを以下に固定:
    - XYアーク -> Z軸回転
    - YZアーク -> X軸回転
    - ZXアーク -> Y軸回転
- 回転計算は、ドラッグ開始姿勢基準で毎フレーム再計算する方式を維持。
  - `Plane(axisDir, center)` へのレイ投影
  - `Vector3.SignedAngle` による角度差分算出
  - `Quaternion.AngleAxis` で `startRotation` に反映
- Undo/Redo設計を維持。
  - マウス離し時のみ `RotateObjectQuaternionCommand` を積む。
- 移動矢印の描画方式を「1本の折れ線」から「シャフト + 3Dコーン先端」へ変更し、先端破綻を抑制。
  - シャフト: `LineRenderer` 2点（`center` -> `tip - axis * headLength`）
  - 先端: 動的生成したコーンメッシュを `Quaternion.LookRotation(axis)` で配置
- 見た目調整:
  - 矢印/弧の太さを従来比 10% 減
  - 透明度係数を `0.9` に統一
  - 弧色をテーマグレー（通常: `DesignTokens.Divider` / アクティブ: `DesignTokens.TextSecondary`）に変更
