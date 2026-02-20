# EditorMain (SkillSync Editor)

## 概要
Unity上で動作する、オブジェクト配置・編集ツールのエディタ拡張プロジェクトです。
コマンドパターンを採用し、Undo/Redo機能を含む堅牢な編集システムを提供します。

## 機能 (Features)

### 1. オブジェクト操作
- **配置 (Place)**: プレハブを選択してシーンに配置します。
- **移動 (Move)**: 配置されたオブジェクトを移動します。
- **回転 (Rotate)**: オブジェクトを回転させます (Q/Eキーで15度単位)。
- **削除 (Delete)**: 選択したオブジェクトを削除します。
- **複製 (Duplicate)**: 選択したオブジェクトを複製します (Ctrl+D)。

### 2. コマンドシステム (Undo/Redo)
- 全ての操作（配置、移動、回転、削除）はコマンドとして管理されます。
- `Ctrl+Z` で元に戻す (Undo)、`Ctrl+Y` でやり直し (Redo) が可能です。

### 3. UI（Canvas/Prefab運用）
- UIは **uGUI（Canvas） + Prefab** を正として運用します。
- `Assets/UI/Prefabs/UIRoot.prefab` に以下の操作ウィンドウを統合しています。
  - オブジェクト一覧ウィンドウ（`Panel_Catalog`）
  - ノード追加ウィンドウ（`Panel_ScenarioGraph`）
- オブジェクト一覧ウィンドウは以下に対応しています。
  - `typeId` の部分一致検索（大文字小文字無視）
  - スクロールカード表示
  - クリック配置（1回で終了）
  - ドラッグ&ドロップ配置（都度配置）
  - `+` ボタン未実装フィードバック
- 両ウィンドウは隙間なく接続され、以下のリサイズに対応しています。
  - オブジェクト一覧: 左右方向リサイズ
  - ノード追加: 上下方向リサイズ

### 4. シナリオ作成UI（MVP）
- ノード追加・接続・保存（`curriculum.json`）に対応。
- 保存先は `Assets/Exports/<ProjectName>-curriculum.json`。
- ノードUIはCanvas上のPrefab参照で構築し、見た目調整はPrefab側で行います。

## 主要スクリプト (Key Scripts)

### Core Systems
- **`CommandService.cs`**: コマンドの実行と履歴（スタック）を管理するシングルトンサービス。
- **`CommandStack.cs`**: Undo/Redo用のコマンドスタック実装。
- **`EditModeService.cs`**: エディタのモード（Select, Move, Rotate, Placeなど）を管理。
- **`SelectionService.cs`**: オブジェクトの選択状態を管理。
- **`PrefabRegistry.cs`**: 配置可能なプレハブのリストを管理。

### Commands
- **`MoveRotateCommands.cs`**: 移動と回転のロジックをカプセル化したコマンド。
- **`PlaceDeleteCommands.cs`**: 配置と削除のロジックをカプセル化したコマンド。

### Tools & Controllers
- **`MoveTool.cs`**: 移動ギズモの描画と操作ロジック。
- **`RotateTool.cs`**: 回転ギズモの描画と操作ロジック。
- **`PlacementController.cs`**: オブジェクト配置時のレイキャストや位置計算。
- **`EditCameraController.cs`**: エディタ時のカメラ操作（移動、ズームなど）。

### UI & Visuals
- **`CatalogUI.cs`**: 配置するプレハブを選択するUIの制御。
- **`ScenarioGraphUI.cs`**: シナリオノードUI（追加・接続・保存）の制御。
- **`SelectionOutline.cs`**: 選択されたオブジェクトの輪郭線描画。
- **`PanelHorizontalResizeHandle.cs`**: オブジェクト一覧ウィンドウの横リサイズ。
- **`PanelVerticalResizeHandle.cs`**: ノード追加ウィンドウの縦リサイズ。
- **`UiPanelDockSync.cs`**: 2ウィンドウ間の隙間ゼロ維持（追従同期）。

### Editor Automation
- **`BuildUiPrefabs.cs`**: `UIRoot.prefab` を自動生成/更新。
- **`ApplyUiPrefab.cs`**: Sceneへ `UIRoot` を適用し、必要なUI参照・構成を反映。

## 操作方法 (Controls)

| 操作 | キー / アクション |
| --- | --- |
| **回転 (左/右)** | `Q` / `E` (15度刻み) |
| **削除** | `Delete` |
| **複製** | `Ctrl + D` |
| **Undo** | `Ctrl + Z` |
| **Redo** | `Ctrl + Y` |

## ディレクトリ構造
```text
Assets/
├── Scripts/                  # C#スクリプト
│   ├── Core/                 # データモデル
│   ├── Services/             # サービス層
│   └── UI/                   # UI制御/リサイズ/同期
├── UI/
│   └── Prefabs/
│       └── UIRoot.prefab     # 操作UIルート（Canvas）
├── Editor/
│   └── Automation/           # UI Prefab生成/Scene適用自動化
├── Prefabs/                  # 配置用プレハブ
├── Data/                     # 設定データ (ScriptableObjects)
├── Exports/                  # 出力JSON
└── Scenes/                   # サンプルシーン
```

## UI自動化の実行
- `BuildUiPrefabs.Build`:
  - `Assets/UI/Prefabs/UIRoot.prefab` を生成/更新します。
- `ApplyUiPrefab.Apply`:
  - 対象Sceneへ `UIRoot` を配置（未配置時）し、UI構成を適用します。

## ライセンス
[License Information Here]
