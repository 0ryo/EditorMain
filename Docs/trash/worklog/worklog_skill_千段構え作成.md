# worklog_latest

## 0. 対象範囲
- ブランチ: `codex/ui`
- 集計範囲: `ff2913b9`（`main` から分岐）以降の `codex/ui` 固有コミット
- 最終更新: 2026-02-20

## 1. 実装サマリ
- MVP-3 として、カリキュラムグラフのデータモデル・ノードUI・保存機能を新規実装。
- シナリオ操作UIを、スクリプト生成方式から **Canvas/Prefab 正式運用** へ移行。
- 左オブジェクト一覧と下部ノード追加ウィンドウを統合し、密着レイアウト・双方向リサイズを実装。
- ノード条件UIを2段ドロップダウン化し、ワールド上の `PlacedObject` を `obj-xxxx` で選択可能に改善。
- ノード接続をクリック/ドラッグ両対応にし、明るい黄色の接続線描画とデバッグログを実装。
- 運用文書（AGENTS/README/仕様書）を更新し、ローカル生成物の `.gitignore` 整理まで実施。

## 2. 機能別の詳細

### 2.1 MVP-3基盤（カリキュラム編集）
- `CurriculumModel` / `CurriculumGraphService` を追加。
- `StepNode` の追加、エッジ接続、参照修復、未設定条件検出を実装。
- ノードUI部品（`StepNodeUI`, `ConditionRowUI`, `ConnectionLineGraphic`, `ScenarioGraphUI`）を追加。
- カリキュラム保存を `Assets/Exports/<Project>-curriculum.json` へ出力する処理を追加。

### 2.2 ノード操作性改善
- Stepノードのドラッグ移動を追加（`NodeDragHandler`）。
- `+ Step` 追加時に既存ノード位置を保持し、新規ノードは中央基準で生成するよう修正。
- ノードの見た目を薄い黄色基調へ統一。
- コネクタ位置・余白調整（四角コネクタをノード外側へ、矢印削除）を反映。

### 2.3 条件UI（ドロップダウン）改善
- 条件行を「未設定 / を / 未設定 / に近づけたら」の2段構成へ変更。
- `+条件` ボタンを廃止し、1行固定の条件編集に整理。
- `PlacedObjectOptionProvider` を通じ、現在ワールドに存在するオブジェクトIDを選択肢に反映。
- ドロップダウン文字不可視問題を修正:
  - フォントを `LegacyRuntime.ttf` に統一
  - 文字色黒固定
  - 背景グレー系固定
  - 表示位置・レイアウト補正（`DropdownOpenFixer` 追加）
  - Outline除去（枠なしグレーボタン化）

### 2.4 配置UI（オブジェクト一覧）改善
- 検索バー、ヘッダー、カード一覧を備えたオブジェクト一覧ウィンドウを整備。
- カード高さを大きくし、上揃えレイアウトへ調整。
- クリック配置（1回で終了）を維持。
- カードのドラッグ&ドロップ配置（1回配置で終了）を実装。
- UIドラッグと3Dクリック入力の干渉を抑制するフラグ制御を追加。
- EventSystem 重複時の無効化処理を追加。

### 2.5 接続線・接続操作の改善
- 出力→入力のドラッグ接続を実装（`ConnectorDragHandler`）。
- 接続線色を明るい黄色に統一。
- 接続イベント/線生成のログを追加（接続成否、生成本数、座標距離）。
- 線が描画されない問題を修正:
  - コールバック配線順を修正（`Bind` 前に設定）
  - `CanvasRenderer` 欠落をランタイム補完
  - `ConnectionLineGraphic` に `RequireComponent(CanvasRenderer)` 付与
  - Prefab生成処理でも `LineTemplate` に `CanvasRenderer` を明示付与

### 2.6 レイアウト・デザイン統一
- 全体を白基調＋角丸方針に調整。
- 半透明運用を廃止（UI本体は不透明、必要箇所のみ透明要素）。
- 左右ウィンドウ間の余白を排除して密着。
- 左ウィンドウ（横）・下ウィンドウ（縦）のリサイズ対応。
- ウィンドウが画面上端/下端まで届くようにアンカー・オフセット調整。
- リサイズハンドル色と操作領域色の整合性を調整。

### 2.7 Prefab/Scene運用へ移植（重要）
- `Assets/UI/Prefabs/UIRoot.prefab` を構築し、UI参照を SerializeField 割当中心へ移行。
- `BuildUiPrefabs` で Prefab を生成/更新する仕組みを整備。
- `ApplyUiPrefab` で Scene に自動適用する仕組みを実装。
- `SampleScene` に加え、`EditorMain` へも同様の UI 反映を実施。
- 旧方式（ランタイム階層組み立て）依存を縮小し、見た目修正を Prefab 側に寄せる運用へ変更。

### 2.8 ドキュメント整備
- 操作ウィンドウ仕様を文書化（当初 `開発計画/仕様` 配下）。
- `AGENTS.md` に、仕様確認ルールと Canvas 実装方針を追記。
- `README.md` にディレクトリ構造・機能更新を反映。

### 2.9 リポジトリ運用整備
- `.gitignore` を更新し、ローカル生成物を除外:
  - `Assets/Exports/*-curriculum.json`
  - `Assets/Exports/*-curriculum.json.meta`
  - `開発計画/UI案.png`（現: `Docs/UI案.png`）
- `UserSettings` を Git 追跡対象から除外（ローカル保持）。

## 3. コミット履歴（codex/ui 固有）
- `b2ef5718` feat(mvp3): add curriculum model and graph service
- `ed7996c4` feat(mvp3): ノードUI部品と接続線描画を追加
- `3e0ee3de` feat(mvp3): 下部シナリオグラフUIの統合スクリプトを追加
- `2df3d626` feat(mvp3): curriculum保存処理と警告表示を追加
- `f49254a5` feat(mvp3): シナリオUIの自動起動ブートストラップを追加
- `e6efa875` chore(mvp3): 追加スクリプトのmetaファイルを整備
- `db5dd972` fix(ui): Unity 6の組み込みフォント参照を修正
- `24e2e417` fix(ui): 条件行テンプレートを破棄しないよう修正
- `1137e3e7` feat(ui): Stepノードをドラッグ移動可能にする
- `1f468a3d` style(ui): シナリオUIを角丸の白基調デザインへ調整
- `a139cf6f` fix(ui): 角丸スプライトをランタイム生成に変更
- `c51e7f92` feat(ui): 下部ノードウィンドウの上下リサイズを追加
- `41d842af` style(ui): ノードUIを薄い黄色基調に調整
- `d17c63f5` fix(ui): ノード追加時に既存位置を保持し新規を中央生成
- `159a443d` refactor(ui): 条件入力を2段プルダウンレイアウトへ変更
- `4439ff2d` fix(ui): リサイズ領域の色を上書きしないよう修正
- `1075bcad` style(ui): 条件余白拡張とコネクタ表示を調整
- `9a3653c4` refactor(ui): スクリプト生成UIをPrefab参照前提へ移行
- `7f4d1296` feat(editor): UIプレハブをSceneへ適用する自動化を追加
- `aeeb74b9` chore(ui): UIRootプレハブ生成とScene適用結果を反映
- `95c5bfcc` chore(ui): EditorMainシーンにもUIRootを適用
- `a622e473` refactor(ui): オブジェクト追加ウィンドウを新UIへ統合
- `a0993b71` feat(ui): カタログとノードウィンドウの密着レイアウトと双方向リサイズを追加
- `dc8de0e4` fix(ui): 操作ウィンドウを画面上下端まで拡張
- `3d78f1c0` docs: 操作ウィンドウ仕様を開発計画/仕様に追加
- `74b0d2a5` docs: AGENTSとREADMEを最新UI運用に合わせて更新
- `4826e28c` UI改善の最終反映（接続線表示とドラッグ接続を修正）
- `407bdcba` chore: UserSettingsとローカル生成物をgit管理対象から除外

## 4. 今後の引き継ぎ注意点
- 新規チャット開始時は、まず本ファイルを読み、次に `Docs/worklog_UI/` の個別仕様と全体仕様を確認する。
- UI改修は Prefab 優先、スクリプトはロジック責務に限定する。
- 表示不具合調査時は、接続/ドロップダウン/配置ログを先に確認する。
