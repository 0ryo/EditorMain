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
- `ImportedModelParts`: 追加モデルの配置時に静的Meshノードとその祖先を既存Transform上の`PlacedObject`として登録する。部品の`modelRoot`は構成上の配置ルート、`partNodePath`は名前検証付きの元ノード位置、条件参照は永続化した個別ID。保存・出力はルートだけを生成対象とし、`parts`にlocal Transform・個別ID・表示/固定・削除状態を保存して子を再利用する。部品の単体複製は`sourceNodePath`で元Prefabの部分木を特定する。モデル構造の変更は署名とノード照合で読込前に拒否する。
- `SelectionService`: Raycastと一覧での複数選択、整列、削除、複製、Outline同期。`SelectionTransformSession`が集合のTransformを一つのUndoへ記録し、`SelectionHighlightSet`が基準以外の対象を表示する。Colliderがない配置物は配置・復元・読込の生成境界で `PlacedObjectPickability` がBoxColliderを補完し、選択中の全配置走査は行わない。
- `ViewportOutliner` / `PlacedObjectEditState`: `Panel_Catalog` の `配置` / `一覧` タブを切り替え、配置instanceの検索・選択、Rendererを使った表示切替、Colliderを使った編集固定を管理する。
- `MoveTool` / `SelectionOutline` / `RotateTool`: Transform/Scaleの入力と視覚的handle。
- `ObjectTransformPanel` / `TransformToolSettings`: 詳細パネルの数値Transform入力とworld/local座標系、pivot/center基準を共有し、ギズモ操作にも反映する。
- `EditCameraController`: 中/右ドラッグorbit、Shift+中/右ドラッグpan、wheel zoom、選択focus、定型view、投影切替、resetを担当する。設定button下のcamera iconから開閉する`ViewportCameraToolbar`とshortcutから操作し、入力は`EditInput`経由で読む。`ViewportCameraToolbar`は3D viewport上部buttonの共通hover guideも提供する。
- `CommandService` / `CommandStack` / command classes: 編集操作のDo/Undo/Redo。

### シナリオ

- `Core/CurriculumModel.cs`: 編集モデル。Start/End/Step/ConditionとStepFlow/ConditionBind、Step詳細、拡張可能なCondition parameterを明示するschema version 5。Condition種別定義と既定parameterは`ConditionTypeCatalog`へ集約する。
- `CurriculumGraphService`: node/edge操作、接続制約、欠損参照を保持した検証、分岐・合流を含むStep経路の検証と表示順生成、export model変換。UI非依存の中心サービス。
- `ScenarioGraphUI`: nodeの配置・接続操作、status表示、保存処理を統括する。4種のnode生成と再構築時の破棄は`ScenarioNodeViewFactory`、旧Prefabの不足template補完は`ScenarioNodeTemplateFactory`へ委譲し、位置・展開状態と編集操作のcallbackはUI側で保持する。検証結果を各nodeの文字badgeと枠へ常時反映し、`ScenarioValidationPanel`の前後navigationから問題nodeへfocusする。検証済みgraphは`ScenarioPreviewPanel`で成功を模擬し、接続に沿って進路を選んで確認できる。
- `ScenarioGraphViewport`: GraphContentと操作buttonの補完、minimapの生成・更新を担当する。node参照はUI側の`ScenarioNodeViewBinding`を共有し、nodeの配置位置やUndoは所有しない。マウス入力とpan/zoomの制限は`NodeAreaPanZoomController`が担当する。
- `StepNodeUI` / `ConditionNodeUI` / `ConditionRowUI` / `TerminalNodeUI`: node単位の表示と編集。Step/Conditionの再同期はgraph/command/placement eventで行い、定期pollingしない。
- `ObjectDetailPanel`: 選択中 `PlacedObject` の名前/説明と、詳細パネルの開閉を担当する。参照しているCondition nodeの探索、差分判定、互換UI生成、編集後のgraph再同期は`ObjectConditionReferencePresenter`へ委譲する。
- `Core/ScenarioExportModel.cs`: JSON出力用のversion 6モデル。編集用graphをそのまま保存せず、Step詳細、Condition parameter、接続先IDを含む `requiredActions` へ変換する。実行順は配列順ではなく接続先IDが正本。配置ルート内の`parts`で部品IDと編集状態を渡す。
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
2. `EditorProjectStore` がschema version 6として`persistentDataPath/Projects`へ保存する。旧形式の部品情報がない配置はルートのみとして読み込む。
3. 読込時はmigration、配置ID修復、登録済みtypeId検証を先に行い、成功後に既存の配置とgraphを完全置換してUndo履歴をclearする。
4. 未保存変更はUIへ表示し、保存済みprojectは元fileへ一定間隔でautosaveして一覧の同じ行へ日時を表示する。まだ保存先のないprojectだけ`Projects/Recovery`へ保存し、次回起動時に復元または破棄できる。

### 配布用シナリオ出力

1. `ScenarioGraphUI` が `CurriculumGraphService` を通じてnode/edgeを変更。
2. ServiceがStart/End数、分岐・合流を含むStepFlow、Condition数/参照等を検証する。欠損参照は自動変更せず、エラーとしてUIへ返す。
3. `BuildScenarioExport()` がStep順の `requiredActions` と配置objectsを生成。
4. UIが `persistentDataPath/Exports/<project>-curriculum.json` へatomicに近いtemp置換で保存。

## 境界と依存方向

- Core modelはUnity serialization用のplain classだが、UI classへ依存しない。
- Graphの規則は `CurriculumGraphService` に集約し、node UIはservice経由で変更する。
- 条件の新規選択肢と旧形式の互換判定は `ConditionTypeCatalog.Definitions` / `Find` で区別する。操作条件の実行側入力契約は `IScenarioInteractionStateSource`、詳細は `Docs/rules/scenario_rules.md`。
- 条件候補の検索・ホバーは `ObjectDropdownBrowser` → `ObjectCandidatePreview`。深度なしのMeshマスクから輪郭だけをUIへ重ね、編集選択・元素材・保存データには触れない。Resourcesの専用Shaderへカメラ描画開始時のview/projectionと各Rendererの現在のlocalToWorldを合成して渡し、SRPの暗黙の描画行列に依存しない。`ObjectScreenPicker`は対象欄の既存変更リスナーへIDを渡し、完了フレームも通常の配置・変形入力を抑止する。
- 通常選択は `SelectionHighlightSet` から同じ形状描画を青色で利用する。対象ごとにマスクを作り輪郭を合成するため、選択対象同士の包含でも内側を見失わない。全選択で2枚のRenderTextureを共有する。`SelectionOutline`は拡縮の角位置計算・操作ハンドルを担当し、矩形の選択枠は描かない。
- Catalog metadataは `PrefabRegistry` とruntime card stateに分かれる。追加モデルは`ImportedModelStore`のライブラリへ保存し、再起動時に同じtypeIdで登録する。GLB/glTFの配布素材は`ScenarioModelBundle`が同梱する。
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
