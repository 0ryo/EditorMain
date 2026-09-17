# Decisions

重要判断だけを更新・統合します。理由が履歴から断定できない事項は `GOTCHAS.md` の未解決差異として扱います。

## D-001 — 小さい入口から必要資料だけ読む

- **Decision:** `AGENTS.md` は短い常時入口とし、毎タスク `.ai/INDEX.md` から関連資料だけ選ぶ。
- **Reason:** 同じ長文・旧Worklogの反復読込を避け、過去の判断を再利用しつつコンテキストを抑えるため。
- **Alternatives:** 全知識を `AGENTS.md` へ集約する、全Worklogを毎回読む、会話だけに保持する。
- **Consequences:** INDEXのルーティングと各資料の重複排除・鮮度維持が必要。容易にソースから分かる情報は原則保存しない。
- **Evidence:** 2026-08-20のユーザー指示、本外部知識環境。

## D-002 — CodexはUnityを起動しない

- **Decision:** CodexはUnity Editor/Unity CLIを起動せず、file変更とshellの静的確認だけを行う。
- **Reason:** このプロジェクトのLocal Execution Policy。compile/runtimeは人間が確認する運用。
- **Alternatives:** `-batchmode` / `-executeMethod` / Test RunnerをCodexが実行する。
- **Consequences:** 完了報告には未実施のUnity確認と人間向けchecklistを含める。報告されたcompile/runtime errorはlogに基づき修正する。
- **Evidence:** 旧 `AGENTS.md` Section 7（2026-02-28）、現行 `AGENTS.md`。

## D-003 — uGUI Prefabを正本にし、Editor APIで更新する

- **Decision:** UIはuGUI + TextMeshPro、正本は `Assets/UI/Prefabs/UIRoot.prefab`。Scene/Prefab変更はEditor API automation経路を使う。
- **Reason:** serialized参照をUnityに正しく書かせ、Scene直置きと手編集YAMLの破損riskを抑えるため。
- **Alternatives:** UI Toolkitへ移行、Sceneへ直接配置、YAML直接編集、runtime hierarchyだけで構築。
- **Consequences:** UI構造変更は `BuildUiPrefabs`、必要に応じ `ApplyUiPrefab`、runtime互換補完、仕様書を一緒に確認する。Codex自身はautomationを実行せず、ユーザーに実行方法を渡す。
- **Evidence:** `Docs/rules/ui_editing_rules.md`、`BuildUiPrefabs.cs`、`ApplyUiPrefab.cs`、`UIRoot.prefab`。

## D-004 — 既存Prefab・作業Scene互換のruntime補完を限定的に維持する

- **Decision:** Prefab正本を維持しつつ、既存serialized参照が古い期間と`SampleScene`での作業は `CatalogUI` 等の `Ensure*` runtime補完で支える。Build Settingsやscene pathだけを理由に補完を停止しない。
- **Reason:** 実際に参照切れ・inactive service・旧Prefabで配置/UIが無反応になる問題があり、現行実装が再有効化/生成fallbackで回復する。ユーザーの通常確認Sceneは`SampleScene`で、補完を止めるとworkspace gridも生成されないため。
- **Alternatives:** runtime動的生成のみ、Prefabが完全移行するまで機能停止、毎回Scene/Prefabを手動修正。
- **Consequences:** UI変更がbuilderとruntime補完の二重実装になりやすい。新規機能で補完経路を無制限に増やさず、Prefab反映後に整理可能か検討する。
- **Evidence:** `CatalogUI.EnsureRuntimeBindings()` / `EnsureRuntimeEditServices()`、`EditWorkspace.EnsureWorkspaceVisuals()`、2026-08-19の配置復旧commits、2026-08-23のユーザー確認。

## D-005 — 配置座標は `y=0` の数学的作業平面を使う

- **Decision:** 配置の主経路はFloor Collider raycastではなく `EditWorkspace` の `Plane.Raycast`。
- **Reason:** SceneのFloor Collider範囲と表示grid範囲が一致せず、広いviewportで配置clickが失敗したため。
- **Alternatives:** Floor Colliderを巨大化、LayerMask raycastだけに依存、Camera前方固定距離へ配置。
- **Consequences:** Floor mesh/colliderは視覚・補助であり配置座標の正本ではない。GroundY変更はplacement、grid、camera、selectionへの影響を確認する。
- **Evidence:** `EditWorkspace.cs`、`PlacementController.cs`、commit `43ca515d`、現行Worklog Task 14。

## D-006 — 編集マウス入力は `EditInput` に集約する

- **Decision:** Placement、Selection、Move、Cameraの入力は可能な範囲で `EditInput` を経由し、Both設定ではlegacy Inputを優先する。
- **Reason:** EventSystemはInput Systemを使う一方、編集処理でInput APIが混在してGame view外座標や無反応の診断が難しかったため。
- **Alternatives:** 全処理を旧Inputへ固定、全処理をInput Systemへ即時移行、各componentが個別に分岐。
- **Consequences:** 新しいpointer操作も共通入口を使う。input backend変更時は `ProjectSettings.asset`、EventSystem、`EditInput` を一緒に確認する。
- **Evidence:** `EditInput.cs`、`ProjectSettings/ProjectSettings.asset` (`activeInputHandler: 2`)、2026-08-19 commits。

## D-007 — Scenario規則をServiceへ集約し、schema version 5を使う

- **Decision:** Start/End/Step/Condition nodeとStepFlow/ConditionBind edgeを編集modelに持ち、接続・検証・export変換は `CurriculumGraphService` に集約する。
- **Reason:** UI表示とgraph整合性を分離し、分岐・合流を含むStep経路、Condition所属、配置object参照を保存前に検証するため。
- **Alternatives:** 旧 `StepNode` のlinear listだけを直接編集、各NodeUIがedge/listを直接変更、graph全体をそのままexport。
- **Consequences:** model変更はservice、UI、validation、export、migration互換を同時に更新する。legacy `StepNode` / `ProximityPair` はmigration/fallback目的で残る。
- **Evidence:** `Core/CurriculumModel.cs`、`CurriculumGraphService.cs`、`ScenarioGraphUI.cs`、`ScenarioExportModel.cs`。

## D-008 — 編集用projectと配布用JSONを分離する

- **Decision:** 再編集用の配置・Scenario統合dataはschema version付き `.skillsync.json` として `persistentDataPath/Projects` に保存し、配布用Scenario/Placement JSONは `persistentDataPath/Exports` に出力する。
- **Reason:** 編集状態を欠落なく復元しつつ、runtime buildでも追加permissionや書込可能なAssets directoryへ依存しないため。
- **Alternatives:** graphと配置を別fileで管理する、配布JSONを再編集dataとして兼用する、`Application.dataPath/Exports`を維持する。
- **Consequences:** project loadはmigrationとtypeId/ID検証後に一括置換する。保存先があるprojectは同じfileへautosaveし、未保存projectは `Projects/Recovery` へ置く。追加モデルはprojectとは別の `ImportedModels` ライブラリでtypeIdを永続化する。
- **Evidence:** `Core/EditorProjectModel.cs`、`EditorProjectStore.cs`、`EditorProjectService.cs`、`RuntimeExportPathUtility.cs`。

## D-009 — 成功するまで待機し、失敗設定を増やさない

- **Decision:** 手順は全成功条件の達成まで待機する。失敗時の処理・失敗分岐は実装対象にしない。分岐は成功後に接続先から進路を選ぶ方式とし、未選択経路を合流時に待たない。
- **Reason:** 教員の設定項目と操作負担を増やさず、成功しないと先に進めない制約でカバーするというユーザーの明示方針。
- **Consequences:** 出力JSON version 5は接続先IDを保持する。配列順の実行を禁止し、実行側はバージョンを検査する。エディタプレビューの成功模擬とVR実機での条件判定を区別する。
- **Evidence:** 2026-09-08のユーザー指示、`ScenarioFlow`、`ScenarioPlaybackSession`、`Docs/rules/scenario_rules.md`。
