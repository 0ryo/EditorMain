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

## 10. 追記（2026-03-02: Shift+中ボタンドラッグ感度の再調整）
- 対象: `Assets/Scripts/EditCameraController.cs`
- 変更方針をシンプル化し、`Shift` パン用の追加倍率は使わず、既存 `panSpeed` を直接有効化。
- `ApplySensitivityFloor()` で `panSpeed` を `0.04f` 以上へ強制する処理を削除。
- 既定値を `panSpeed = 0.01f` に変更。
- これにより、Sceneに保存されている `panSpeed` がそのまま反映され、過剰な移動量を抑制できるようにした。

## 11. 追記（2026-03-02: 設定ボタンと設定ウィンドウの追加）
- 対象:
  - `Assets/Scripts/CatalogUI.cs`
  - `Assets/Scripts/UI/UiPanelDockSync.cs`
  - `Assets/Editor/Automation/BuildUiPrefabs.cs`
- 右上UIとして、編集モード行と同じ高さ（40px）の歯車ボタン（`Button_Settings`）を追加。
  - 右端から余白を確保するため、`UiPanelDockSync` に `settingsButtonPanel` / `settingsButtonRightMargin` / `settingsButtonWidth` を追加。
  - 実座標は `LateUpdate()` で右上基準に再配置し、編集モード行と高さを揃える。
- 歯車ボタン押下で中央オーバーレイ `Panel_Settings` を表示する処理を `CatalogUI` に追加。
  - 中央に空の `Window` のみを表示（テキストなし）。
- 既存Prefabでも反映できるよう、`CatalogUI` のランタイム補完で以下を自動生成/再バインドするようにした。
  - `Button_Settings`
  - `Panel_Settings`
- `BuildUiPrefabs` も同構成を生成するよう更新し、新規再生成時にも同じUI構造になるよう統一した。

## 12. 追記（2026-03-02: 設定画面タブ構成の実装）
- 対象:
  - `Assets/Scripts/CatalogUI.cs`
  - `Assets/Editor/Automation/BuildUiPrefabs.cs`
- 設定画面の左側に縦タブを追加し、`一般 / 連携 / アカウント` を常時表示する構成へ変更。
  - タブ押下時は右側コンテンツのみ切り替え、左タブ領域は固定表示。
- `一般` タブ:
  - 視点操作感度のスライダー（`0.2x - 2.5x`）を追加。
  - 値表示テキスト（`x` 倍率）を追加。
  - `EditorCameraController` の `orbitSpeed / panSpeed / zoomSpeed / orthographicZoomSpeed` に倍率を反映。
- `連携` タブ:
  - `ここからウェブサイトへ遷移` ボタンを配置し、クリックで `https://unity.com/` を開くようにした。
- `アカウント` タブ:
  - 中央の丸アイコン（`●`）
  - 大きめのユーザー名 (`User Name`)
  - 小さめのメール (`user@example.com`)
  をモックとして追加。
- 既存Prefab・ランタイム補完の両方で同じ階層名/参照が揃うように、`BuildUiPrefabs` と `CatalogUI` の生成・バインドを同期した。

## 13. 追記（2026-03-02: 設定変更時の適用ボタン表示）
- 対象:
  - `Assets/Scripts/CatalogUI.cs`
  - `Assets/Editor/Automation/BuildUiPrefabs.cs`
- 設定ウィンドウ右下に `適用` ボタン（`Button_ApplySettings`）を追加。
  - 位置: ウィンドウ右下固定（右16px / 下16px）
  - 初期状態: 非表示
- 設定値が1つでも変更されたタイミングで `CatalogUI` が dirty 状態を立て、`適用` ボタンを表示するようにした。
  - 現在は `一般` タブの感度スライダー操作を変更トリガーとして実装。
- `適用` ボタン押下で dirty 状態を解除し、ボタンを再び非表示に戻す。

## 14. 追記（2026-03-02: 設定画面クローズ挙動）
- 対象:
  - `Assets/Scripts/CatalogUI.cs`
- 設定画面を閉じる共通処理 `CloseSettingsPanel()` を追加。
- `適用` ボタン押下時は、dirty解除後に設定画面を閉じるよう変更。
- 設定ウィンドウ外側クリックで閉じるため、`SettingsOverlayClickCatcher` を追加。
  - オーバーレイに `IPointerClickHandler` を付与し、クリック位置が `Window` の外側なら `CloseSettingsPanel()` を実行。
  - ウィンドウ内クリック時は閉じない。

## 15. 追記（2026-03-02: 元に戻すボタン + 未適用破棄）
- 対象:
  - `Assets/Scripts/CatalogUI.cs`
  - `Assets/Editor/Automation/BuildUiPrefabs.cs`
- 設定変更（現在は感度スライダー）が1つでもある場合に、右下へ `適用` と `元に戻す` を同時表示するように変更。
  - `元に戻す` は `Button_RevertSettings` として追加し、色はシステムグレー系（`DesignTokens.BgSecondary`）を使用。
  - 配置は `適用` の左隣（同サイズ 136x40）。
- 設定値の状態管理を `committed`（確定値）と `pending`（未適用値）に分離。
  - `適用` 押下時のみ `pending` をカメラ設定へ反映し、`committed` として確定。
  - `元に戻す` 押下時は `pending` を `committed` へ戻す（未適用変更を取り消し）。
- オーバーレイクリックで閉じる場合（`適用` 未押下）は、`pending` を破棄してから閉じる仕様へ変更（未適用変更は反映しない）。
