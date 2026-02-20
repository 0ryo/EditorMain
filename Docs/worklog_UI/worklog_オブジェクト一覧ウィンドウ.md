# オブジェクト一覧ウィンドウ仕様

## 1. 対象
- 名称: `Panel_Catalog`
- 実装方式: uGUI（Canvas/Prefab）
- 正となるアセット: `Assets/UI/Prefabs/UIRoot.prefab`

## 2. 目的
- 配置可能オブジェクト一覧を表示し、選択した `typeId` を配置モードへ渡す。
- 画面左側の常設操作パネルとして、ノード追加ウィンドウと隙間なく接続する。

## 3. レイアウト
- アンカー:
  - `anchorMin = (0, 0)`
  - `anchorMax = (0, 1)`
- オフセット:
  - `offsetMin = (0, 0)`
  - `offsetMax = (288, 0)`（初期幅 288）
- 画面に対して上端・下端まで伸長する。
- 右端に横リサイズハンドル `ResizeHandleX` を持つ。

## 4. UI階層（Prefab）
- `Panel_Catalog`
- `Scroll_Catalog`
- `Scroll_Catalog/Viewport`
- `Scroll_Catalog/Viewport/Content`
- `Btn_Template`（非表示テンプレート）
- `ResizeHandleX`

## 5. 見た目
- 背景色: 薄いグレー基調（シナリオ側パネルと同系色）
- ボタンテンプレート: 薄い黄色基調
- 文字色: 黒

## 6. 挙動
- `CatalogUI` が `PrefabRegistry.entries` を列挙し、`Btn_Template` を `Content` に生成。
- ボタン押下で `onSelectType(string typeId)` を発火。
- 実行時の機能接続:
  - `PlacementController` が見つかる場合、`CatalogUI` は `registry` を自動バインド。
  - 永続イベント未設定時は `PlacementController.EnterPlacement` をランタイム接続。

## 7. リサイズ
- スクリプト: `PanelHorizontalResizeHandle`
- 操作: `ResizeHandleX` を左右ドラッグ
- 制約:
  - `minWidth = 220`
  - `maxWidth = 720`
- 幅変更時、`UiPanelDockSync` によりノード追加ウィンドウの左端が追従し、隙間は常に `0`。

## 8. 依存スクリプト
- `CatalogUI`
- `PanelHorizontalResizeHandle`
- `UiPanelDockSync`

## 9. 変更ルール
- 見た目調整は `UIRoot.prefab` 側で行う。
- 機能変更（イベント配線/データソース）は `CatalogUI` 側で行う。
