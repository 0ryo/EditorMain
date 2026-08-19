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
  - `offsetMax = (312, 0)`（初期幅 312）
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
- 背景色: `DesignTokens.BgPrimary`
- カード背景: `DesignTokens.Surface` + `DesignTokens.Divider` の細線
- カード内表示:
  - カテゴリバッジ（例: `車両`, `工具`, `環境`, `追加`, `その他`）
  - 表示名（例: `車両`, `工具箱`, `タイヤ交換`）
  - 技術ID（例: `Vehicle/Car_Proxy`）
- 文字色: `DesignTokens.TextPrimary` / `DesignTokens.TextSecondary`

## 6. 挙動
- `CatalogUI` が `PrefabRegistry.entries` を列挙し、`Btn_Template` を `Content` に生成。
- 各カードはカテゴリ、表示名、技術IDを表示し、左揃えで配置する。
- ボタン押下で `onSelectType(string typeId)` を発火。
- カード押下後、ワールドクリック待ちの配置モード中は該当カードをテーマ青（`DesignTokens.Accent`）の枠線で強調表示する。
- 実行時の機能接続:
  - `PlacementController` が見つかる場合、`CatalogUI` は `registry` を自動バインド。
  - 永続イベント未設定時は `PlacementController.EnterPlacement` をランタイム接続。

## 7. リサイズ
- スクリプト: `PanelHorizontalResizeHandle`
- 操作: `ResizeHandleX` を左右ドラッグ
- 制約:
  - `minWidth = 240`
  - `maxWidth = 420`
- 幅変更時、`UiPanelDockSync` によりノード追加ウィンドウの左端が追従し、隙間は常に `0`。

## 8. 依存スクリプト
- `CatalogUI`
- `PanelHorizontalResizeHandle`
- `UiPanelDockSync`

## 9. 変更ルール
- 見た目調整は `UIRoot.prefab` 側で行う。
- 機能変更（イベント配線/データソース）は `CatalogUI` 側で行う。

## 10. 追記（2026-02-25 / FBXオブジェクト追加）
- パネル最下部に `Button_AddObjectBottom` を追加し、FBX追加導線を実装。
- ボタン押下で `.fbx` を選択し、成功時に一覧最下部へ `New Object` カードを追加。
- `New Object` カード押下で配置モードへ入り、ワールドクリックで選択済みFBXを配置。
- 外部パスのFBXは `Assets/ImportedFbx/` に取り込み、配置可能なランタイム型として登録。

## 11. 追記（2026-02-25 / オブジェクト設定画面）
- FBX選択直後に即カード追加せず、`オブジェクト設定` モーダルを開くフローへ変更。
- 設定画面に以下を追加:
  - `オブジェクト名` 入力欄（カード名に反映）
  - `説明` 入力欄（ランタイム保持・検索対象）
  - `追加` / `キャンセル` ボタン
  - 選択済みFBXパス表示
- `BuildUiPrefabs` で `Panel_NewObjectSettings` をPrefab自動生成し、`CatalogUI` の参照へ自動バインド。
- 既存Prefabにも追従できるよう、`CatalogUI` にランタイム補完生成を実装。
- 設定画面は `UIRoot` 基準の画面中央モーダル表示に変更し、タイトルは `オブジェクト設定` に統一（`(new)` を削除）。
- デザインは `Docs/rules/design_rule.md` に合わせ、余白・ボタン高さ・オーバーレイ色を調整。

## 12. 追記（2026-02-28 / 編集モード行の追加）
- `Panel_Catalog` 上端付近に `EditModeRow`（`閲覧` / `移動` / `スケール`）を追加。
- ボタン押下で `EditModeService.SetMode(...)` を呼び、`Browse` / `Transform` / `Scale` を切り替える。
- 選択中モードのボタンは `DesignTokens.Accent` で強調表示し、非選択は `DesignTokens.BgSecondary` とする。
- 既存Prefab互換のため、`CatalogUI` は `EditModeRow` と各ボタンが未配置でもランタイム補完生成する。
- `UiPanelDockSync` の `editModePanel` と連携し、カタログ幅変更時も編集モード行の位置を追従させる。

## 13. 追記（2026-03-02 / カード削除ボタン）
- 各オブジェクトカードに小型の `×` ボタン（`Button_RemoveCard`）を追加。
- `×` はカードホバー時のみ表示する。
- `×` はカード右上角の外側にはみ出す配置とし、丸の中心がカード角に重なる。
- `×` ボタン背景は完全な丸形とする。
- `×` 押下時、該当 `typeId` のカードをオブジェクト一覧から除去する。
- 本挙動は「一覧からの除去」のみを対象とし、ワールド内に既に配置済みのオブジェクトには影響しない。
- 除去済みカードは、検索フィルタ変更や `RebuildCards()` 実行後も同一セッション内では復活しない。
- `BuildUiPrefabs` の `Card_Template` に同ボタンを追加し、既存Prefab向けには `CatalogUI` がランタイム補完する。

## 14. 追記（2026-03-03 / カード表示の簡素化）
- オブジェクトカードから `Thumbnail`（四角領域）を非表示化し、一覧表示をテキスト中心に統一。
- オブジェクトカードから `Button_RemoveCard` を非表示化し、カード表示要素をオブジェクト名のみに整理。
- `LabelMain` はカード全幅にストレッチし、`MiddleCenter` で中央表示する。
- 既存Prefabとの互換維持のため、`CatalogUI` と `DesignTokenApplier` でランタイム補正を実装。

## 15. 追記（2026-03-23 / TMP化と表示品質改善）
- カタログ内の検索欄、ステータス、各ボタンラベル、カードラベルは `TextMeshPro` 系コンポーネントへ移行する。
- `BuildUiPrefabs` の生成物も `TMP_Text` / `TMP_InputField` 前提に揃え、Prefab 生成経路とランタイム補完経路の差を減らす。
- 既存Prefabとの差分期間でも、`CatalogUI` / `DesignTokenApplier` が TMP 前提でカード中央寄せと配色を補正する。
- 角丸・アウトラインの解像感を上げるため、丸角スプライト解像度と細線アウトライン設定を見直す。
- 目的は「オブジェクト名のみ中央表示」のレイアウトを保ったまま、高解像度環境での文字ぼけ・輪郭荒れを抑えること。
## 16. 追記（2026-04-14 / オブジェクト追加の形式拡張）
- 追加ボタンの文言を `FBXを追加` から `オブジェクトを追加` に変更。
- Editor 上の追加導線で `.fbx / .glb / .gltf` を選択可能にした。
- `.fbx` は従来どおり `Assets/ImportedFbx/` に取り込み、`.glb / .gltf` は `RuntimeModelLoader` で読み込んで新規オブジェクト設定へ進む。

## 17. 追記（2026-06-29 / カード情報密度の改善）
- カード高を `96` に変更し、カテゴリバッジ、表示名、技術IDの3段構成にした。
- `CatalogUI` は `typeId` からカテゴリと表示名を推定し、既存Prefabでもランタイム補完で同じ構造を生成する。
- `BuildUiPrefabs` の `Card_Template` も同じ3段構成で生成する。
- `DesignTokenApplier` は旧カード中央寄せ補正をやめ、新カードレイアウトを維持する。
