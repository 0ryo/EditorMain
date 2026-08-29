# Architecture

## 実行時の大枠

メインSceneの `Systems` が編集サービスを持ち、`UIRoot.prefab` が操作UIを提供します。参照切れや旧Prefabに耐えるため `CatalogUI` が必要なサービス/UIを探索・再有効化・補完する経路もあります。

```text
PrefabRegistry / imported model
        ↓
CatalogUI → PlacementController → workspace plane + snap
        ↓                    ↓
   type selection      CommandStack → PlacedObject
                                      ↓
                          SelectionService / MoveTool
                                      ↓
                    ObjectDetailPanel / ViewportStatusStrip

PlacedObject IDs → CurriculumGraphService ← ScenarioGraphUI
        ↓                 ↓ validate/export
EditorProjectService   persistentDataPath/Exports/*.json
        ↓
persistentDataPath/Projects/*.skillsync.json
```

## コンポーネントと責務

### Scene・起動境界

- `Assets/EditorMain.unity`: Build Settingsで有効なComposition Root。編集系MonoBehaviour、Main Camera、EventSystem、Floor、UIRoot instanceを配置。
- `Assets/Scenes/SampleScene.unity`: ユーザーが日常的にPlayMode確認へ使う作業Scene。Main CameraとUIRootを持ち、不足する編集serviceとworkspace gridは`CatalogUI`のruntime補完で構成する。Build Settingsで無効でもscene pathを理由に起動を制限しない。
- `Assets/UI/Prefabs/UIRoot.prefab`: `Panel_Catalog`、`Panel_ScenarioGraph`、`Panel_Detail`、設定モーダル、ノードTemplate等の正本。
- `CatalogUI.Start()`: 実行時の二次的なComposition Root。古いserialized参照や欠落サービスを補完する。

### 配置・編集

- `PrefabRegistry`: 既定 `typeId → prefab` のデータ境界。
- `CatalogUI`: カード、検索、モード、設定、モデル追加を担当。配置は `PlacementController` へ委譲する。
- `EditWorkspace`: Camera解決、`y=0` 平面へのScreen座標変換、grid snap、入力欄/UIブロック判定を共有する。`EditSnapSettings`がgrid/rotation幅、全体ON/OFF、Alt一時解除を保持する。
- `PlacementController`: registry map、配置モード、配置座標、生成、配置イベントを担当。
- `PlacedObject`: 配置instanceの識別情報と表示メタデータ。
- `SelectionService`: Raycast選択、削除、複製、Outline同期。Colliderがない配置物は配置・復元・読込の生成境界で `PlacedObjectPickability` がBoxColliderを補完し、選択中の全配置走査は行わない。
- `ViewportOutliner` / `PlacedObjectEditState`: `Panel_Catalog` の `配置` / `一覧` タブを切り替え、配置instanceの検索・選択、Rendererを使った表示切替、Colliderを使った編集固定を管理する。
- `MoveTool` / `SelectionOutline` / `RotateTool`: Transform/Scaleの入力と視覚的handle。
- `ObjectTransformPanel` / `TransformToolSettings`: 詳細パネルの数値Transform入力とworld/local座標系、pivot/center基準を共有し、ギズモ操作にも反映する。
- `EditCameraController`: 中/右ドラッグorbit、Shift+中/右ドラッグpan、wheel zoom、選択focus、定型view、投影切替、resetを担当する。設定button下のcamera iconから開閉する`ViewportCameraToolbar`とshortcutから操作し、入力は`EditInput`経由で読む。`ViewportCameraToolbar`は3D viewport上部buttonの共通hover guideも提供する。
- `CommandService` / `CommandStack` / command classes: 編集操作のDo/Undo/Redo。

### シナリオ

- `Core/CurriculumModel.cs`: 編集モデル。Start/End/Step/ConditionとStepFlow/ConditionBind、Step詳細、拡張可能なCondition parameterを明示するschema version 4。Condition種別定義と既定parameterは`ConditionTypeCatalog`へ集約する。
- `CurriculumGraphService`: node/edge操作、接続制約、欠損参照を保持した検証、線形Step列の生成、export model変換。UI非依存の中心サービス。
- `ScenarioGraphUI`: node prefabの生成・配置・接続操作、status表示、保存処理。検証結果を各nodeの文字badgeと枠へ常時反映し、`ScenarioValidationPanel`の前後navigationから問題nodeへfocusする。検証済みgraphは`ScenarioPreviewPanel`で実行順に手動または自動送りできる。
- `StepNodeUI` / `ConditionNodeUI` / `ConditionRowUI` / `TerminalNodeUI`: node単位の表示と編集。Step/Conditionの再同期はgraph/command/placement eventで行い、定期pollingしない。
- `ObjectDetailPanel`: 選択中 `PlacedObject` の名前/説明と、参照しているCondition nodeの編集表示。
- `Core/ScenarioExportModel.cs`: JSON出力用のversion 4モデル。編集用graphをそのまま保存せず、Step詳細とCondition parameterを含む線形 `requiredActions` へ変換する。
- `Core/EditorProjectModel.cs` / `EditorProjectService`: 配置Transform・表示情報・lock/hidden状態と`Curriculum`を一体で保存する。読込時は配置IDの欠損・重複を一意IDへ修復し、typeId解決を検証してから現在内容を完全置換する。編集eventと選択中objectの差分からdirtyを判定し、保存済みprojectは元fileへ、未保存projectは復旧dataへautosaveする。
- `EditorProjectStore`: `persistentDataPath/Projects` の `.skillsync.json` をtemp file + backupで保存し、schema migration、保存済み一覧、`Projects/Recovery`の復旧dataを提供する。

### Import / Export

- `RuntimeModelLoader`: glTFastによる `.glb/.gltf` 読込。Windows Playerだけnative file dialogを持つ。
- `CatalogUI` のEditor限定経路: `.fbx` を `Assets/ImportedFbx/` へコピー/Importし、Prefabとして登録。
- `PlacementExportService`: Scene上の `PlacedObject` を `persistentDataPath/Exports` のplacement JSONへ出力。
- `ScenarioGraphUI.SaveScenarioExport()`: Graph検証後、`persistentDataPath/Exports` へcurriculum JSONをtemp file + replace/moveで出力。

### UI生成・適用

- `DesignTokens`: 色、文字、間隔、解像度、Panel制約の実装値。`UiScaleController`が基準解像度へ表示倍率を適用する。
- `DesignTokenApplier` / `UiRoundedTheme`: 既存Hierarchyへの見た目補正。
- `BuildUiPrefabs.Build()`: codeから `UIRoot.prefab` 全体を生成して保存。
- `ApplyUiPrefab.Apply()`: enabled build scenesと `EditorMain.unity` を開き、UIRoot追加、Catalog参照、Panel端を更新して保存。
- `AutomationEntry`: `ApplyUiEdits`、`MigrateScenarioData`、`ValidateProject` の `-executeMethod` 向け入口。ただしCodexは実行しない。

## データフロー

### オブジェクト配置

1. `CatalogUI` がregistryまたはruntime imported entryからカードを生成。
2. Card clickが `PlacementController.EnterPlacement(typeId)` を呼ぶ。
3. `EditInput` のクリック座標を `EditWorkspace.TryScreenToGround()` が `y=0` 平面へ変換。
4. `PlacementController` がgrid snapしたXZ座標を`PlaceObjectCommand`へ渡し、生成後に`PlacedObjectGrounding`がrenderer bounds下端を`y=0`へ合わせる。
5. `PlacedObject` のID/Colliderを保証し、Selectionと `ObjectPlaced` eventを更新。

### 編集プロジェクト保存・読込

1. `EditorProjectService` が配置objectと`Curriculum`を編集用modelへ取り込む。
2. `EditorProjectStore` がschema version 2として`persistentDataPath/Projects`へ保存する。
3. 読込時はmigration、配置ID修復、登録済みtypeId検証を先に行い、成功後に既存の配置とgraphを完全置換してUndo履歴をclearする。
4. 未保存変更はUIへ表示し、保存済みprojectは元fileへ一定間隔でautosaveして一覧の同じ行へ日時を表示する。まだ保存先のないprojectだけ`Projects/Recovery`へ保存し、次回起動時に復元または破棄できる。

### 配布用シナリオ出力

1. `ScenarioGraphUI` が `CurriculumGraphService` を通じてnode/edgeを変更。
2. ServiceがStart/End数、線形StepFlow、Condition数/参照等を検証する。欠損参照は自動変更せず、エラーとしてUIへ返す。
3. `BuildScenarioExport()` がStep順の `requiredActions` と配置objectsを生成。
4. UIが `persistentDataPath/Exports/<project>-curriculum.json` へatomicに近いtemp置換で保存。

## 境界と依存方向

- Core modelはUnity serialization用のplain classだが、UI classへ依存しない。
- Graphの規則は `CurriculumGraphService` に集約し、node UIはservice経由で変更する。
- Catalog metadataは `PrefabRegistry` とruntime card stateに分かれる。runtime importはasset registryへ永続化されない。
- Editor-only AssetDatabase処理は `Assets/Editor/Automation/` または `#if UNITY_EDITOR` 内に閉じる。
- Prefab/Sceneのserialized参照と実行時の `Ensure*` 補完が併存する。

## 変更時の主要な影響範囲

| 変更 | 一緒に確認する箇所 |
|---|---|
| Catalog/UI hierarchy名 | `CatalogUI`、`BuildUiPrefabs`、`DesignTokenApplier`、`UIRoot.prefab`、UI仕様 |
| 配置/入力/Camera | `EditInput`、`EditWorkspace`、`PlacementController`、`SelectionService`、`MoveTool`、`ViewportStatusStrip` |
| `PlacedObject` ID/metadata | Selection、Detail、Condition dropdown、graph validation、両export |
| Scenario node/edge | Core model、`CurriculumGraphService`、各NodeUI、`ScenarioGraphUI`、export model |
| デザイントークン | `DesignTokens`、applier、builder、runtime補完、`Docs/rules/design_rule.md` |
| TMP/font | `TmpFontInitializer`、TMP Settings、Fallback asset、全UI text/input/dropdown |
| UI Prefab更新 | builderとruntime補完の両経路、scene apply、ユーザーのUnity検証 |

`CatalogUI.cs`、`BuildUiPrefabs.cs`、`ScenarioGraphUI.cs`、`MoveTool.cs` は特に大きなhotspotです。局所変更でも、正本Prefab経路とruntime互換経路の両方を確認します。
