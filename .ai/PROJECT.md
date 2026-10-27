# Project Knowledge

## 目的

`EditorMain`（README上の名称は `SkillSync Editor`）は、Unity上で3Dオブジェクトを配置・選択・変形し、そのオブジェクトを参照する手順/条件グラフを作成してJSONへ出力するエディタアプリです。Unity Editor拡張そのものではなく、メインSceneを実行して利用するランタイム型編集UIと、Prefab/Sceneを生成するEditor自動化を同居させています。

## 研究コンセプト

- 3D開発の専門知識がない教員が、XR（Meta Quest）向けの技能実習教材を自分で作成・修正できるPCオーサリングツール。外部開発者への依頼で生じる、教員の意図（手順・成功基準・注意点）の翻訳と再依頼の手間をなくす。
- 教員はGUIだけで「実習空間（3D配置）」「作業手順」「各手順の成功条件」を定義し、JSON教材データとしてXR実行アプリへ渡す。教材データと実行機能を分離し、教材更新と実行アプリ改良を独立させる。
- 本リポジトリは①教材作成ツール。②教材データ（JSON v6＋モデル素材ZIP）、③XR実行アプリ（Quest、別担当・連携未検証）、④成績管理Webアプリ（別担当）と組み合わせる。初期題材は自動車のタイヤ交換（`TeachingMaterials/TireChange/`、14手順）。
- 評価は教員による新規作成・修正（詳細は `BACKLOG/evaluation-method.md`）。最重要の品質目標は「初見の教員が説明少なく迷わず完走できるUI」。
- 中間発表（2026-09-09）のノードを線で結ぶUIは現在のアプリにはない。2026-09-19以降は線形の手順リスト型の固定SkillSync UI。

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
| Test package | Unity Test Framework `1.6.0`（専用Unity test assemblyは未確認）。別途 `Tools/RegressionChecks` にmanaged回帰runnerあり |

依存の正本は `Packages/manifest.json` と `Packages/packages-lock.json` です。

## 起動・ビルド

- 人間がUnity `6000.2.6f2` でプロジェクトを開き、`Assets/EditorMain.unity` を実行する。
- Build Settingsで有効なSceneは `Assets/EditorMain.unity`。`Assets/Scenes/SampleScene.unity` は登録済みだが無効。
- Windows Build Profileは `Assets/Settings/Build Profiles/windows.asset` にある。
- AIはGUIを使わず、Unity CLIのbatch compile・`-executeMethod`の検査・PlayModeテスト・Playerビルドまで行う（AGENTS.md）。GUI操作と実機確認はユーザー担当。
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
- `Design/skillsync_codex_handoff/`: 固定SkillSync UIのデザイン原本（Figma・PDF・`DESIGN_SPEC.md`）。

配置・シナリオJSONの出力先は、Editorではプロジェクト直下の`Exports/`、PlayerではDocuments内の`SkillSync/Exports/`です（Documentsが取得できない場合はpersistentDataPath配下）。「教材を書き出す」は保存先を選び（macOS。他は既定の場所）、毎回`<教材名>-yyyyMMdd-HHmm`の別フォルダーに、同名のJSON・ZIPとモデル素材・EditorのPrefabパッケージをまとめます。同じ分の出力は末尾`-2`以降で区別し、以前の出力を保持します。

## 用語

- `typeId`: 配置Prefabの技術ID。例 `Vehicle/Car_Proxy`。
- `PlacedObject`: Scene上に配置されたオブジェクトへ `id`、`typeId`、表示名、説明を付与するComponent。
- `PrefabRegistry`: `typeId` とPrefabを対応付けるScriptableObject。
- `Curriculum`: schema version 5の編集用グラフデータ。
- `StepFlow`: Start → Step群 → Endの順序接続。
- `ConditionBind`: Condition → Stepの所属接続。
- `workspace plane`: 配置座標の正本となる数学的な `y=0` 平面。表示Floor Colliderとは別。
- `UIRoot`: uGUI全体のPrefabルート。
