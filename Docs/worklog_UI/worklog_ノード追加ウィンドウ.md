# ノード追加ウィンドウ仕様

## 1. 対象
- 名称: `Panel_ScenarioGraph`
- 実装方式: uGUI（Canvas/Prefab）
- 正となるアセット: `Assets/UI/Prefabs/UIRoot.prefab`

## 2. 目的
- シナリオのノード（Start/End/Step/Condition）編集、接続、保存を行う。
- 画面下部常設パネルとして、オブジェクト一覧ウィンドウの右隣に密着配置される。

## 3. レイアウト
- アンカー:
  - `anchorMin = (0, 0)`
  - `anchorMax = (1, 0)`
- オフセット:
  - `offsetMin = (288, 0)`（左端はカタログ幅に追従）
  - `offsetMax = (0, 300)`（初期高さ 300）
- 画面下端に接地し、右端まで伸長する。
- 左端は `UiPanelDockSync` により `Panel_Catalog` の右端へ常時追従（隙間 0）。

## 4. UI階層（Prefab）
- `Panel_ScenarioGraph`
- `TopBar`
- `TopBar/Input_ProjectName`
- `TopBar/Button_AddStep`
- `TopBar/Button_AddCondition`
- `TopBar/Button_SaveCurriculum`
- `TopBar/Text_Status`
- `NodeArea`
- `NodeArea/LineLayer`
- `NodeArea/LineLayer/LineTemplate`（非表示テンプレート）
- `NodeArea/StepNodeTemplate`（非表示テンプレート）
- `NodeArea/ConditionNodeTemplate`（非表示テンプレートまたはランタイム生成）
- `NodeArea/StartNodeTemplate`（非表示テンプレートまたはランタイム生成）
- `NodeArea/EndNodeTemplate`（非表示テンプレートまたはランタイム生成）
- `ResizeHandle`（縦リサイズ）

## 5. ノードテンプレート仕様
- `StepNodeTemplate`
- 上段: `Text_StepId`
- 中段: `Input_Title`（表示専用）
- 補助表示: `Text_ConditionSummary`
- 接続コネクタ:
  - 左右に丸コネクタ（ノード外側に配置）
- ドラッグ:
  - `DragHandle` 領域でノード移動
- `ConditionNodeTemplate`
  - 表示: `nodeId` + 条件行（`DropdownA`/`DropdownB`）
  - 接続コネクタ: 出力のみ
- `StartNodeTemplate` / `EndNodeTemplate`
  - 表示: 固定ラベル（`START` / `END`）
  - 接続コネクタ: Startは出力のみ、Endは入力のみ

## 6. 見た目
- パネル: 薄いグレー基調
- ノード: 薄い黄色基調
- 条件エリア: 薄い黄色系の濃淡
- テキスト: 黒

## 7. 挙動
- `ScenarioGraphUI` がUI参照（SerializeField）経由で制御する。
- `+ Step`:
  - 新規Stepを追加
  - 既存ノード位置は保持
  - 未配置ノードは自動整列配置
- `+ Condition`:
  - 新規Conditionを追加
- 接続:
  - 出力コネクタクリック後、入力コネクタクリックでEdge追加
  - 接続不可時は理由コードをステータス表示
- 保存:
  - `Assets/Exports/<ProjectName>-curriculum.json`
  - 保存前に E-01〜E-11 を検証
  - エラー時はSave不可（ボタン無効）
  - 有効時は `version=2` / `requiredActions[].conditions[]` を含むJSONを出力

## 8. リサイズ
- スクリプト: `PanelVerticalResizeHandle`
- 操作: 上端 `ResizeHandle` を上下ドラッグ
- 制約:
  - `minHeight = 180`
  - `maxHeight = 720`

## 9. 依存スクリプト
- `ScenarioGraphUI`
- `StepNodeUI`
- `ConditionNodeUI`
- `TerminalNodeUI`
- `ConditionRowUI`
- `ConnectionLineGraphic`
- `NodeDragHandler`
- `PanelVerticalResizeHandle`
- `UiPanelDockSync`

## 10. 変更ルール
- 見た目と階層は `UIRoot.prefab` で調整する。
- データ保存・接続ロジックは `ScenarioGraphUI`/`CurriculumGraphService` 側で調整する。

## 11. 2026-02-22 UI bugfix
- Clamp node positions to `NodeArea` when `Panel_ScenarioGraph` is resized.
- Clamp node dragging inside `NodeArea` bounds.
- Purpose: prevent nodes from protruding outside the node area after resize.

## 12. 2026-02-22 NodeArea pan/zoom
- Added `NodeArea/GraphContent` as the zoom/pan target container.
- Mouse wheel: zoom in/out around cursor position.
- Middle mouse drag: pan horizontally and vertically.
- Node dragging is bounded by `GraphContent` area.

## 13. 2026-02-22 line clipping fix
- `ConnectionLineGraphic` changed from `Graphic` to `MaskableGraphic`.
- Connection paths are now clipped by `NodeArea` (`RectMask2D`) during pan/zoom.

## 14. 2026-02-22 terminal label z-order fix
- `TerminalNodeUI` now forces label text to front via `SetAsLastSibling()`.
- `labelText.raycastTarget = false` to avoid blocking drag/connector interactions.

## 15. 2026-02-22 global rounded UI
- Added `UiRoundedTheme` and applied it to full hierarchy in `CatalogUI` and `ScenarioGraphUI`.
- Corner radius is configurable via `cornerRadius` (default: 14).
- Covers object cards, search inputs, buttons, node cards, dropdowns, and panel backgrounds.

## 16. 2026-02-22 node/path delete interactions
- Added delete `X` button at top-right for `Step` and `Condition` nodes.
- `Start`/`End` nodes do not show delete button.
- Clicking a path now removes the corresponding edge.
- Hovering a path shows `�폜` text on the path center.

## 17. 2026-02-22 delete button and path hover behavior tuning
- Delete button visual updated: gray background and centered on the yellow node header bar.
- Fixed path hit testing to react only near the visible line segment.
- Restored node dragging while connections already exist.
- Path delete hint now appears only during hover and is displayed above the path.