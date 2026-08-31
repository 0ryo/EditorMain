# Function-Preserving Refactor Plan

## Objective

EditorMainの実装を機能単位で整理し、肥大化したclass、重複処理、不要な探索・polling・allocationを縮小する。機能、操作、表示、文言、余白、保存形式、出力内容は変更しない。

## Working Rules

- 作業branchは `codex/function-preserving-refactor` とする。
- 1機能または安全に確認できる小単位ごとに1commitとする。
- 各commit後に作業を停止し、変更内容、静的確認結果、Unityでの確認項目を報告する。
- ユーザーから次へ進む指示を受けるまで次の機能へ着手しない。
- 退行が確認されたcommitは `git revert` で取り消し、正常状態から再実装する。
- userの未commit変更と未追跡fileを変更しない。
- Prefab/Scene YAMLを直接編集しない。UI変更が必要な場合はEditor API経路を使うが、本refactorでは表示値を変えない。
- serialized field、Hierarchy名、公開API、schema、ID、保存先、JSON内容を原則維持する。
- Unity Editor/CLIは起動しない。compile、PlayMode、buildはユーザーが確認する。

## Verification Per Commit

- `git status --short`
- `git diff --check`
- 対象API、serialized field、Hierarchy名、文言、定数値の参照検索
- 保存・読込・exportを扱う場合はschema、path、JSON field、並び順、migration経路の差分確認
- UIを扱う場合は文字、色、余白、size、anchor、表示条件の差分確認
- ユーザーによるUnity compileと対象機能のPlayMode確認

## Sequence

### 1. Project Save, Load, and JSON Export

- 保存、復元、migration、dirty判定、atomic writeの責務を整理する。
- 配置exportとscenario exportの重複を調査し、安全に共有できる実装だけを共通化する。
- schema、保存先、file名、JSON内容、上書き・backup動作は変更しない。

### 2. Placement, Catalog, and Model Import

- Catalog card表示・検索、配置制御、runtime composition、FBX/glTF取込を分離する。
- `CatalogUI`を一括変更せず、各機能を個別commitにする。
- Settings UIは独立した確認単位とする。

### 3. 3D Editing

- 選択・削除・複製、outliner、移動・回転・scale、camera操作を順に整理する。
- input priority、snap、座標系、gizmo表示と操作感は変更しない。

### 4. Scenario Domain and Validation

- model、接続規則、検証、command処理の責務を整理する。
- schema、ID、検証message、node/edge規則、export順序は変更しない。

### 5. Scenario Graph UI

- node生成、接続、viewport、auto layout、minimap、検証表示、export UIを分割する。
- `ScenarioGraphUI`を複数の確認可能なcommitへ分ける。

### 6. Object Detail and Condition References

- `ObjectDetailPanel`からgraph走査、参照収集、表示生成を分離する。
- 表示内容、編集操作、animationは変更しない。

### 7. UI Settings, Generation, and Compatibility Fallbacks

- Settings、`BuildUiPrefabs`、runtime `Ensure*`、theme適用を整理する。
- PrefabのHierarchy、文字、色、余白、sizeは変更しない。

### 8. Cross-Cutting Performance

- 不要なHierarchy全走査、毎frame polling、一時collection・文字列allocationを調査する。
- 初期化、再生成、inactive object、旧Prefab fallbackを含めて同じ結果になるものだけevent駆動化またはcache化する。

### 9. Documentation Cleanup

- 参照元を検索してから、重複、完了済みtask、統合済み旧Worklog、陳腐化した資料だけを削除または統合する。
- 現行仕様、判断根拠、監査証跡は保持する。
- 文書整理を独立commitにする。

## Progress

| Phase | Status | Commit | Unity verification |
|---|---|---|---|
| 1. Project save/load/export | Complete | `refactor: separate project persistence data processing` | Passed |
| 2. Placement/catalog/import | Complete | `refactor: separate catalog card state and filtering`<br>`refactor: separate placement prefab and object creation`<br>`refactor: extract runtime edit composition`<br>`refactor: separate catalog model import processing` | Passed |
| 3. 3D editing | Complete | `refactor: separate placed object selection processing`<br>`refactor: separate outliner data processing`<br>`refactor: separate transform gizmo geometry`<br>`refactor: separate camera navigation calculations`<br>`fix: restrict camera navigation to viewport`<br>`fix: ignore camera input outside game view` | Passed |
| 4. Scenario domain/validation | Validation and connection rules passed; awaiting traversal verification | `refactor: separate scenario graph validation`<br>`refactor: separate scenario connection rules`<br>`refactor: separate scenario graph traversal` | Traversal pending |
| 5. Scenario graph UI | Pending | - | Pending |
| 6. Object detail/condition references | Pending | - | Pending |
| 7. UI settings/generation/fallbacks | Pending | - | Pending |
| 8. Cross-cutting performance | Pending | - | Pending |
| 9. Documentation cleanup | Pending | - | Pending |
