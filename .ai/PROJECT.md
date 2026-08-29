# Project Knowledge

## 目的

`EditorMain`（README上の名称は `SkillSync Editor`）は、Unity上で3Dオブジェクトを配置・選択・変形し、そのオブジェクトを参照する手順/条件グラフを作成してJSONへ出力するエディタアプリです。Unity Editor拡張そのものではなく、メインSceneを実行して利用するランタイム型編集UIと、Prefab/Sceneを生成するEditor自動化を同居させています。

## 主な機能

- 既定Prefabまたは追加モデルのカタログ表示、検索、配置
- 配置オブジェクトの選択、移動、回転、スケール、削除、複製
- Command patternによる配置・移動・回転・スケール・削除のUndo/Redo
- Start / End / Step / Conditionノードの作成、接続、検証
- Conditionから配置オブジェクトA/Bへの参照
- 配置JSONとカリキュラムJSONの出力
- FBX取込（Unity Editor）とglTF/GLB取込（EditorおよびWindows Playerのランタイム経路）
- uGUIのPrefab生成・Scene適用自動化

## 技術スタック

| 項目 | 確認値 |
|---|---|
| Unity | `6000.2.6f2` |
| 言語 | C#、Unityの既定Assembly構成（独自asmdefなし） |
| UI | uGUI `2.0.0` + TextMeshPro。UXML/USSは未確認 |
| Rendering | URP `17.2.0`。Quality設定からURP assetを参照 |
| Input | Input System `1.14.2`、`activeInputHandler: 2`（Both） |
| 3D model | glTFast `6.16.1`、Editor AssetDatabaseによるFBX取込 |
| Test package | Unity Test Framework `1.6.0`（プロジェクト固有テストは未確認） |

依存の正本は `Packages/manifest.json` と `Packages/packages-lock.json` です。

## 起動・ビルド

- 人間がUnity `6000.2.6f2` でプロジェクトを開き、`Assets/EditorMain.unity` を実行する。
- Build Settingsで有効なSceneは `Assets/EditorMain.unity`。`Assets/Scenes/SampleScene.unity` は登録済みだが無効。
- Windows Build Profileは `Assets/Settings/Build Profiles/windows.asset` にある。
- CodexはこのリポジトリではUnity Editor/Unity CLIを起動しない。コンパイル、PlayMode、ビルド、実機確認はユーザー担当。
- shellで信頼できる最小確認は `git diff --check`。既存 `.csproj` は現在の全ソースを含まず、単独の `dotnet build` は検証ゲートにならない。

## 全体構成

- `Assets/EditorMain.unity`: メインScene。`Systems`、Main Camera、Floor、SelectionOutline、EventSystemと `UIRoot.prefab` instanceを持つ。
- `Assets/Scripts/`: 配置、入力、カメラ、選択、コマンド、シナリオ、UI。
- `Assets/Scripts/Core/`: JSON化するシナリオ/出力モデル。
- `Assets/Scripts/Services/`: グラフ操作・検証、ID、配置オブジェクト選択肢。
- `Assets/Scripts/UI/`: シナリオノード、詳細パネル、デザイントークン、状態表示。
- `Assets/UI/Prefabs/UIRoot.prefab`: Catalog、Scenario Graph、Detail、Settings等を含むUIルート。
- `Assets/Editor/Automation/`: `UIRoot.prefab` 生成、Scene適用、検証エントリポイント。
- `Assets/Data/DefaultRegistry.asset`: 既定4種の `typeId` とPrefab参照。
- `Assets/Exports/`: 現行コードのJSON出力先。
- `Docs/`: 詳細仕様、デザイン監査、旧作業履歴。

## 用語

- `typeId`: 配置Prefabの技術ID。例 `Vehicle/Car_Proxy`。
- `PlacedObject`: Scene上に配置されたオブジェクトへ `id`、`typeId`、表示名、説明を付与するComponent。
- `PrefabRegistry`: `typeId` とPrefabを対応付けるScriptableObject。
- `Curriculum`: schema version 2の編集用グラフデータ。
- `StepFlow`: Start → Step群 → Endの順序接続。
- `ConditionBind`: Condition → Stepの所属接続。
- `workspace plane`: 配置座標の正本となる数学的な `y=0` 平面。表示Floor Colliderとは別。
- `UIRoot`: uGUI全体のPrefabルート。
