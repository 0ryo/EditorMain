# Editor App Audit Backlog

2026-08-22の監査を起点に現行コード・後続会話と照合した未完了項目を保持します。優先テーマは`../TASKS.md`、恒久的な落とし穴は`../GOTCHAS.md`を正本とします。

## UIB — 再現済みUI・操作不具合

- [P1][UIB-01] 詳細パネル表示は修正済み。条件参照分離後の開閉・編集・参照更新を両Sceneで回帰確認する。
- [P1][UIB-02] 過去のX軸dragの飛び出し・Undo不能を現行コードで再確認し、再現時に座標変換と履歴を調査する。
- [P1][UIB-03] 青い形状輪郭とオレンジ候補輪郭を、埋まった部品・複数選択・移動後・再起動後に回帰確認する。

## EDT — 3D編集機能

- [P1][EDT-03] 範囲選択、全選択、一覧Shift範囲選択、選択全体focus、配置物のcopy/pasteは実装済みでUnity確認待ち（`viewport-polish-plan.md`）。平坦なgroup化・解除・保存・複製時のID分離を含めUnity確認待ち。
- [P1][EDT-04] layer/category表示制御を追加する。
- [P1][EDT-07] surface/object snap、衝突・重なり警告を追加する。

## SCN — Scenario制作

- [P1][SCN-01] 保存済み教材の複製、テンプレート化／教材作成、削除済みへの移動／復元は実装済みでUnity確認待ち。複製後の編集・保存、条件参照、同名復元、テンプレート原本の保持を確認する。
- [P1][SCN-03] graphの複数選択・集合移動・copy/paste・端揃え・削除とUndo/RedoをUnity確認する。
- [P2][SCN-10] Scenarioのversion履歴と差分表示を追加する。

## ADV — 高度な教材表現

- [P2][ADV-01] loop、parallel、waitを表現できるgraph/modelへ拡張する。
- [P2][ADV-02] 画像、動画、音声等の教材assetと参照管理を追加する。
- [P2][ADV-03] 成功feedback、role/担当者、評価結果をmodelとpreviewへ追加する。
- [P2][ADV-04] user-facing textをlocalization可能な構造へ変更する。

## IMP — Catalog・モデル取込

- [P2][IMP-01] 自動生成thumbnail、filter、sort、favorite、recent、配置数、tagを追加する。thumbnailは手動での画像準備を要求しない。
- [P2][IMP-02] 寸法、polygon数、material、texture等のmodel詳細を表示する。
- [P1][IMP-03] GLB/glTF取込の段階表示・追加ボタン再押下で取消・具体的エラーをUnity確認する。FBXの同期Importは中断できない。
- [P1][IMP-04] 素材込み256 MB、JSON 16 MB、欠損素材・外部参照・GLBヘッダー検証をUnity確認する。展開後500万頂点・texture縦横8192px/計256 MB上限もUnity確認する。展開前のGPUメモリ上限保証はない。
- [P2][IMP-05] import前previewと単位・scale・axis・pivot補正を追加する。
- [P2][IMP-07] catalog項目の編集・削除確認と参照中objectへの影響表示を追加する。
- [P2][IMP-08] Windowsの260文字native dialog依存を解消し、対応platform matrixを定義する。

## A11 — Responsive・Accessibility・視覚設計

- [P1][A11-01] 一覧/詳細/手順の折畳み（F9/F8/F10）と1366px未満の一覧初期折畳みをUnity確認する。
- [P1][A11-02] Canvasの実効倍率1倍下限をUnity確認する。既存の小型ボタンすべての44px化とcaption 14px化は未実装。
- [P1][A11-04] keyboard navigation、論理的Tab順、focus ring、Esc/Enter/Space操作を整備する。
- [P1][A11-05] 色だけに依存しない選択・警告・mode表示と高contrast themeを追加する。
- [P1][A11-06] 薄いgray text、gizmo、statusのcontrastを測定しWCAG AA相当へ改善する。
- [P2][A11-07] 既存のUI scale・tooltip・基本shortcut案内を部品選択／ピペット／条件／出力まで拡充し、accessible labelを整備する。
- [P1][A11-09] 日本語長文、英語、長いproject/object名、font fallbackのlayout testを追加する。

## PRF — 性能・応答性

- [P1][PRF-05] GLTF importへcancel、quota、resource releaseとmemory予測を追加する。
- [P1][PRF-06] 100 object/100 node/200 edge等の基準でFPS、input latency、GC、保存・読込時間budgetを定義する。

## ARC / REF — 構成と責務分割

- [P1][REF-01] CatalogUIの残るSettingsとUI統括を整理する（CatalogCardCollection、ImportService、Compositionは分離済み）。
- [P1][REF-02] 分離済みNodeFactory／Connection／Viewport等の境界を維持し、残るUI統括・Export UIの分離は必要性を評価する。
- [P1][REF-03] 分離済みObjectConditionReferencePresenterのUnity回帰確認を完了する。
- [P1][REF-04] UIがdataを直接変更せずService＋Command経由に統一する。
- [P1][REF-05] `FindFirstObjectByType`/`FindObjectsByType`をserialized参照または明示的compositionへ置換する。
- [P1][REF-06] hierarchy名をAPIとして使うblocking判定と`transform.Find`依存を縮小する。
- [P1][REF-07] Prefab、Editor Builder、runtime `Ensure*`の三重UI生成を一本化する。
- [P1][REF-08] hardcoded text、URL、色、pathを設定・localization resourceへ集約する。
- [P2][REF-09] namespaceとasmdefでCore、Runtime、UI、Editor、Testsの境界を作る。
- [P1][REF-10] user statusとdiagnostic logを分離し、診断情報exportを追加する。
- [P1][REF-11] TMP initializerのreflection/global scanを縮小し、fallback asset生成を決定的にする。

## TST — Test・CI・運用品質

- [P1][TST-01] 条件判定・Command・保存往復・migration・取込異常系のmanaged回帰runnerを継続使用する。Unity固有の選択group・graph clipboard・安定IDのEditMode検証は未実装。
- [P1][TST-02] 配置・選択・gizmo・graph connectionのPlayMode smoke testを追加する。
- [P1][TST-03] WXGA/FHD/QHDと長文localeのscreenshot regression testを追加する。
- [P1][TST-04] 大規模data、巨大・破損GLTF、保存権限なし、容量不足の性能・異常系testを追加する。
- [P1][TST-05] CIでUnity compile、test、Scene/Prefab validationを実行する。
- [P1][TST-06] Console warning zeroをrelease gateにし、TMP Importer不整合を解消する。

## 実操作の再確認条件

- `SampleScene`を通常の作業Sceneとしてclean PlayMode確認し、Build用の`EditorMain.unity`でも主要機能が退行しないことを確認する。
- WXGA 1366×768、FHD 1920×1080、QHD 2560×1440を比較する。
- runtime作成物はPlayMode再起動で破棄済み。監査によるproject file変更はない。
