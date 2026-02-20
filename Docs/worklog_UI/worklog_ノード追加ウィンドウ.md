# ノード追加ウィンドウ仕様

## 1. 対象
- 名称: `Panel_ScenarioGraph`
- 実装方式: uGUI（Canvas/Prefab）
- 正となるアセット: `Assets/UI/Prefabs/UIRoot.prefab`

## 2. 目的
- シナリオのノード（Step）編集、接続、保存を行う。
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
- `TopBar/Button_SaveCurriculum`
- `TopBar/Text_Status`
- `NodeArea`
- `NodeArea/LineLayer`
- `NodeArea/LineLayer/LineTemplate`（非表示テンプレート）
- `NodeArea/StepNodeTemplate`（非表示テンプレート）
- `ResizeHandle`（縦リサイズ）

## 5. ノードテンプレート仕様
- `StepNodeTemplate`
- 上段: `Text_StepId`
- 中段: `Input_Title`
- 条件ブロック:
  - 1段目: `DropdownA` + テキスト `を`
  - 2段目: `DropdownB` + テキスト `に近づけたら`
- 接続コネクタ:
  - 左右に丸コネクタ（ノード外側に配置）
- ドラッグ:
  - `DragHandle` 領域でノード移動

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
  - 新規ノードは中央配置
- 接続:
  - 出力コネクタクリック後、入力コネクタクリックでEdge追加
- 保存:
  - `Assets/Exports/<ProjectName>-curriculum.json`
  - 未設定条件があっても保存し、警告件数を表示

## 8. リサイズ
- スクリプト: `PanelVerticalResizeHandle`
- 操作: 上端 `ResizeHandle` を上下ドラッグ
- 制約:
  - `minHeight = 180`
  - `maxHeight = 720`

## 9. 依存スクリプト
- `ScenarioGraphUI`
- `StepNodeUI`
- `ConditionRowUI`
- `ConnectionLineGraphic`
- `NodeDragHandler`
- `PanelVerticalResizeHandle`
- `UiPanelDockSync`

## 10. 変更ルール
- 見た目と階層は `UIRoot.prefab` で調整する。
- データ保存・接続ロジックは `ScenarioGraphUI`/`CurriculumGraphService` 側で調整する。
