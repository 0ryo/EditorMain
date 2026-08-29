# Editor App Audit Backlog

2026-08-22のコード監査とUnity実操作で確認した未完了項目だけを保持します。優先テーマは`../TASKS.md`、恒久的な落とし穴は`../GOTCHAS.md`を正本とします。

## UIB — 再現済みUI・操作不具合

- [P0][UIB-01] 正規`EditorMain` SceneでObjectDetailPanelが選択時に表示されない問題を再現し、service binding/lifecycleを修正する。
- [P0][UIB-02] X軸gizmo drag後にobjectが画面外へ飛びUndoできない現象を正規Sceneで計測し、座標変換とcommand記録を修正する。

## EDT — 3D編集機能

- [P1][EDT-03] 複数選択、範囲選択、全選択、group化、copy/pasteを追加する。
- [P1][EDT-04] 整列、等間隔配置、layer/category表示制御を追加する。
- [P1][EDT-07] surface/object snap、衝突・重なり警告を追加する。

## SCN — Scenario制作

- [P1][SCN-01] Scenario一覧、読込、複製、削除、template作成を追加する。
- [P1][SCN-03] graph編集のUndo/Redo、copy/paste、複数選択、整列を追加する。
- [P2][SCN-10] Scenarioのversion履歴と差分表示を追加する。

## ADV — 高度な教材表現

- [P2][ADV-01] 分岐、選択肢、loop、parallel、wait、timeoutを表現できるgraph/modelへ拡張する。
- [P2][ADV-02] 画像、動画、音声等の教材assetと参照管理を追加する。
- [P2][ADV-03] 成功・失敗feedback、role/担当者、評価結果をmodelとpreviewへ追加する。
- [P2][ADV-04] user-facing textをlocalization可能な構造へ変更する。

## IMP — Catalog・モデル取込

- [P2][IMP-01] thumbnail preview、filter、sort、favorite、recent、配置数、tagを追加する。
- [P2][IMP-02] 寸法、polygon数、material、texture等のmodel詳細を表示する。
- [P1][IMP-03] import進捗、cancel、具体的な失敗理由を表示する。
- [P1][IMP-04] file size、頂点数、texture容量、破損file、外部参照に制限と警告を設ける。
- [P2][IMP-05] import前previewと単位・scale・axis・pivot補正を追加する。
- [P1][IMP-06] runtime importしたmodel/cardをprojectへ永続化し、元file移動後も再読込できるようにする。
- [P2][IMP-07] catalog項目の編集・削除確認と参照中objectへの影響表示を追加する。
- [P2][IMP-08] Windowsの260文字native dialog依存を解消し、対応platform matrixを定義する。

## A11 — Responsive・Accessibility・視覚設計

- [P1][A11-01] WXGA等のbreakpointを設け、catalog/detail/graphを折畳み可能にする。
- [P1][A11-02] Canvas一様縮小でも本文と操作領域が実効14px/44px未満にならないscale方針へ変更する。
- [P1][A11-04] keyboard navigation、論理的Tab順、focus ring、Esc/Enter/Space操作を整備する。
- [P1][A11-05] 色だけに依存しない選択・警告・mode表示と高contrast themeを追加する。
- [P1][A11-06] 薄いgray text、gizmo、statusのcontrastを測定しWCAG AA相当へ改善する。
- [P2][A11-07] UI scale設定、tooltip、accessible label、shortcut一覧を追加する。
- [P1][A11-09] 日本語長文、英語、長いproject/object名、font fallbackのlayout testを追加する。

## PRF — 性能・応答性

- [P1][PRF-04] detail usageのpollingを差分event駆動へ変更する（graph validation、reference、Step/Condition node同期、pickabilityは対応済み）。
- [P1][PRF-05] GLTF importへcancel、quota、resource releaseとmemory予測を追加する。
- [P1][PRF-06] 100 object/100 node/200 edge等の基準でFPS、input latency、GC、保存・読込時間budgetを定義する。

## ARC / REF — 構成と責務分割

- [P1][REF-01] 3,000行超のCatalogUIをCatalog view/controller、ImportService、Settings、Compositionへ分割する。
- [P1][REF-02] ScenarioGraphUIをGraphView、NodeFactory、Connection、Viewport、Exportへ分割する。
- [P1][REF-03] ObjectDetailPanelからcondition node複製とgraph走査を分離する。
- [P1][REF-04] UIがdataを直接変更せずService＋Command経由に統一する。
- [P1][REF-05] `FindFirstObjectByType`/`FindObjectsByType`をserialized参照または明示的compositionへ置換する。
- [P1][REF-06] hierarchy名をAPIとして使うblocking判定と`transform.Find`依存を縮小する。
- [P1][REF-07] Prefab、Editor Builder、runtime `Ensure*`の三重UI生成を一本化する。
- [P1][REF-08] hardcoded text、URL、色、pathを設定・localization resourceへ集約する。
- [P2][REF-09] namespaceとasmdefでCore、Runtime、UI、Editor、Testsの境界を作る。
- [P1][REF-10] user statusとdiagnostic logを分離し、診断情報exportを追加する。
- [P1][REF-11] TMP initializerのreflection/global scanを縮小し、fallback asset生成を決定的にする。

## TST — Test・CI・運用品質

- [P1][TST-01] CurriculumGraphService、Command、安定ID、保存往復、migrationのEditMode testを追加する。
- [P1][TST-02] 配置・選択・gizmo・graph connectionのPlayMode smoke testを追加する。
- [P1][TST-03] WXGA/FHD/QHDと長文localeのscreenshot regression testを追加する。
- [P1][TST-04] 大規模data、巨大・破損GLTF、保存権限なし、容量不足の性能・異常系testを追加する。
- [P1][TST-05] CIでUnity compile、test、Scene/Prefab validationを実行する。
- [P1][TST-06] Console warning zeroをrelease gateにし、TMP Importer不整合を解消する。

## 実操作の再確認条件

- `SampleScene`を通常の作業Sceneとしてclean PlayMode確認し、Build用の`EditorMain.unity`でも主要機能が退行しないことを確認する。
- WXGA 1366×768、FHD 1920×1080、QHD 2560×1440を比較する。
- runtime作成物はPlayMode再起動で破棄済み。監査によるproject file変更はない。
