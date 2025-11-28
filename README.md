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
- **`SelectionOutline.cs`**: 選択されたオブジェクトの輪郭線描画。

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
├── Scripts/            # C#スクリプト
├── Prefabs/            # 配置用プレハブ
├── Data/               # 設定データ (ScriptableObjects)
└── Scenes/             # サンプルシーン
```

## ライセンス
[License Information Here]
