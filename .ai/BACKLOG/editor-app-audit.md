# Editor App Audit Backlog

2026-08-22の監査を起点に現行コード・後続会話と照合した未完了項目を保持します。優先テーマは`../TASKS.md`、恒久的な落とし穴は`../GOTCHAS.md`を正本とします。

## P0 — 原本保護・主要UIの状況認知

- [P0][REC-01] 起動時の同名教材へのRecovery上書きは修正済み。managed回帰と実ファイルI/Oで原本保持・復旧内容・連番・反復・失敗時の保持を確認済み（詳細は [品質確認](../../QUALITY.md)）。UnityのService起動・一覧・復旧教材の独立編集/保存を直接確認する。
- [P0][UIB-04] 3D viewportのworld contextは、床グリッド/軸、編集用照明とreflection、アクセント色の選択輪郭、配置時のgrounding、orientation/scale参照を実装済み。Unityで既定視点からの見え方と配置物の接地・尺度を確認する。
- [P0][UIB-05] 常設status stripにmode表示、選択/配置対象、配置の次操作と取消ボタンを実装済み。配置後のtoast/自動選択と、詳細パネル表示中の設定アクセスもUnityで確認する。
- [P0][UIB-06] Scenario graphの検証一覧・該当nodeへのfocus、見出し/本文の分離、Step本文の空欄案内、action文としてのCondition、通常時に抑え操作時に強調するconnectorを実装済み。Unityで保存失敗からnodeへ移動し、表示とdrag/hoverを確認する。

## UIB — 再現済みUI・操作不具合

- [P1][UIB-01] 詳細パネル表示は修正済み。条件参照分離後の開閉・編集・参照更新を両Sceneで回帰確認する。
- [P1][UIB-02] 過去のX軸dragの飛び出し・Undo不能を現行コードで再確認し、再現時に座標変換と履歴を調査する。
- [P1][UIB-03] 青い形状輪郭とオレンジ候補輪郭を、埋まった部品・複数選択・移動後・再起動後に回帰確認する。

## EDT — 3D編集機能

- [P1][EDT-03] 範囲選択、全選択、一覧Shift範囲選択、選択全体focus、配置物のcopy/pasteは実装済みでUnity確認待ち（`viewport-polish-plan.md`）。平坦なgroup化・解除・保存・複製時のID分離を含めUnity確認待ち。
- [P1][EDT-04] 一覧上部に車両／工具／環境／追加／その他のカテゴリ表示切替を追加。切替は配置物の表示と選択・編集可否へ反映し、個別の非表示・固定状態を変更しない。Unityでカテゴリ別表示、親子部品、選択解除、個別非表示/固定との組合せを確認する。
- [P1][EDT-07] AABBの正の貫通量を使う重なりwarningを配置後とTransform確定時に追加済み。近傍配置物への軸方向surface snapと、クリックした配置物上面への配置を実装済み。重なり候補は近傍Collider queryで探し、視覚boundsベースの可能性通知で物理衝突判定ではない。Unityで接触・入れ子/部品モデル・移動/scale・snap無効化とUndoを確認する。

## SCN — Scenario制作

- [P1][SCN-11] 読込のstaging/rollbackをUnityで失敗注入確認する。ID修復・参照整合性も監査する。入力検証、Recovery所有権、本番切替ヘルパーはmanaged検証済み。主要フローの合格条件・根拠・失敗系の確認手順は [品質確認](../../QUALITY.md) を参照する。
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
- [P1][A11-02] 通常Canvas倍率の1倍下限、runtime/Prefabボタンの44px見かけ操作領域、caption 14pxを実装済み。倍率低下時はボタン寸法をCanvas/親倍率に応じて補正する。解像度別倍率と実シーンButton寸法を検査するPlayMode testを追加。SkillSyncの2560×1440 reference UIだけは全体を画面へfitする独自scaleを保ち、操作領域をscale補正する。WXGA/FHD/QHD、横長・小windowの文字/clip/操作性をUnityで確認する。
- [P1][A11-04] Tab/Shift+Tab巡回、Selectable navigation、Canvas直下のlayout非干渉focus ring、Project modal内への巡回制限を実装済み。両SceneとProject modalで順番、Enter/テンキーEnter/Space/Esc、IME中、EventSystem再生成、focus喪失をUnity確認する。
- [P1][A11-05] 高コントラスト切替とpalette追従、ボタン状態・Outlineへの適用を実装済み。Unityで標準/高コントラストの色差、3D選択輪郭、warning件数・文言を確認する。
- [P1][A11-06] text/status paletteを淡色surface上の文字・badgeで4.5:1以上に調整済み。orientation gizmoは色軸を濃くし、白い縁取りはユーザー指定で除去。Transform/orientation gizmoのScene上の視認性とUnityでの実表示を確認する。
- [P2][A11-07] 既存のUI scale・tooltip・基本shortcut案内を部品選択／ピペット／条件／出力まで拡充し、accessible labelを整備する。
- [P1][A11-09] 1366×768 / 1920×1080 / 2560×1440のCanvas倍率とscreen-space Button寸法のPlayMode checksを追加。日本語長文・英語・長いproject/object名・font fallbackの実画面 screenshot baselineとoverflow比較はUnityで作成・確認する。

## PRF — 性能・応答性

- [P1][PRF-05] GLTF importは追加ボタンのcancel、入力素材256 MB上限、頂点/texture quota、失敗時resource releaseを実装済み。Instantiate前に読込済みmesh/textureの概算メモリをstatusへ表示する。これはpeak memory予測ではない。Unityでcancel後の再試行、上限拒否、資源解放、実表示を確認する。
- [P1][PRF-06] 100 object/100 node/200 edgeの標準規模に、frame time・input latency・GC・保存/読込・一覧表示の暫定budgetを定義済み（`QUALITY.md`）。基準端末のUnity Profilerで実測して妥当性と合否を確認する。

## ARC / REF — 構成と責務分割

- [P1][REF-01] CatalogUIの残るSettingsとUI統括を整理する（CatalogCardCollection、ImportService、Compositionは分離済み）。
- [P1][REF-02] 分離済みNodeFactory／Connection／Viewport等の境界を維持し、残るUI統括・Export UIの分離は必要性を評価する。
- [P1][REF-03] 分離済みObjectConditionReferencePresenterのUnity回帰確認を完了する。
- [P1][REF-04] Step/Condition/project metadata、graph node/edge/bind、線形手順追加/並べ替え、配置物metadataのUI編集をService/Commandへ移行済み。Undo/Redo、失敗rollback、dirty更新をUnityで確認する。
- [P1][REF-05] viewport/outliner/statusのservice参照をcache＋低頻度retryへ変更。カメラ探索もcacheし、SelectionService/MoveToolの欠落参照探索は0.5秒間隔に制限。重なり検知を近傍Collider queryへ変更。残る起動時・ユーザー操作時のone-shot lookupを維持している箇所を監査し、Unityで片側SceneのService欠落/再生成時に確認する。
- [P1][REF-06] pointerごとの階層名比較を廃止し、Raycast可能なuGUI Graphic/Selectableに専用markerを一度付けて入力境界を判定する。`transform.Find`は生成・Prefab互換の一回限り参照に残る。Prefab参照化の対象を絞り、旧Prefabと新Prefabの両方でUnity確認する。
- [P1][REF-07] 未完了。Builderとruntime `Ensure*`が同じUIを生成する箇所の一覧化と、Prefabを単一正本にする移行は未実施。旧/新SceneとPrefabのSerialized参照・fallbackの確認後に統合する。
- [P1][REF-08] hardcoded text、URL、色、pathを設定・localization resourceへ集約する。
- [P2][REF-09] namespaceとasmdefでCore、Runtime、UI、Editor、Testsの境界を作る。
- [P1][REF-10] 保存/読込/新規作成の成功通知と診断ログ保持・明示exportを実装済み。Unityで直近500件上限、出力先、statusとの分離、失敗/例外時の記録を確認する。
- [P1][REF-11] TMP再描画の走査をロード済みSceneの有効・無効コンポーネントへ限定し、private-field reflectionとOS別system font探索・一時FontAsset生成を除去。TMP Settingsから同梱Noto Sans JP FontAssetを利用する実装済み。Unityで日本語fallback、編集時material更新、missing font/TMP warningがないことを確認する。

## TST — Test・CI・運用品質

- [P1][TST-01] 条件判定・Command・保存往復・migration・取込異常系のmanaged回帰runnerを継続使用する。Unity固有の選択group・graph clipboard・安定IDのEditMode検証は未実装。
- [P1][TST-02] PlayMode smoke testを追加済み。操作領域・配置surface/object snap・selection・transform gesture session・graph connectionを検査する。Unity Test Runnerで実行し、両Sceneの実入力gestureを追加確認する。
- [P1][TST-03] 解像度別Canvas倍率と画面内Buttonの44px境界testを追加済み。WXGA/FHD/QHD・日本語/英語・長名のbaseline画像比較と画像生成はUnity実行が必要で未実施。
- [P1][TST-04] 大規模data、巨大・破損GLTF、保存権限なし、容量不足の性能・異常系testを追加する。
- [P1][TST-05] GitHub Actions workflowとUnity Editor APIのScene/Prefab validatorを追加済み。jobはmanaged regression→PlayMode→Build Scene/SampleScene/Prefab参照検査を順に行う。実行にはtrusted Windows self-hosted runner（label `unity-6000.2.6f2`）、ライセンス済みEditor、およびrunner environmentの`UNITY_EDITOR_EXE`が必要。runner登録後の初回成功とbranch protection gate設定を確認する。
- [P1][TST-06] Console warning zeroをrelease gateにし、TMP Importer不整合を解消する。

## 実操作の再確認条件

- `SampleScene`を通常の作業Sceneとしてclean PlayMode確認し、Build用の`EditorMain.unity`でも主要機能が退行しないことを確認する。
- WXGA 1366×768、FHD 1920×1080、QHD 2560×1440を比較する。
- runtime作成物はPlayMode再起動で破棄済み。監査によるproject file変更はない。
