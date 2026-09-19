# PROJECT_CONTEXT — 調査結果

確認日: 2026-09-19。このブランチでのUnity操作をユーザーが許可したため、Prefab適用、Play Mode、6状態の実撮影と比較、隔離した操作検証まで実施。Playerビルドは未実施。

## Unity環境

| 項目 | 確認結果 | 根拠 |
|---|---|---|
| Unity | 6000.2.6f2 | ProjectSettings/ProjectVersion.txt |
| Render Pipeline | URP 17.2.0 | Packages/manifest.json |
| UI | uGUI 2.0.0 + TextMeshPro | Packages、UIRoot.prefab、既存UIコード |
| Input | Both（activeInputHandler: 2）、Input System 1.14.2。編集入力はEditInput | ProjectSettings/ProjectSettings.asset、EditInput.cs |
| 既存Canvas | Scale With Screen Size、1920×1080 | UIRoot.prefab |
| 新UI | ユーザーの追加指定で2560×1440へ変更。文字とアイコンは等方スケール、中央幅を拡張 | SkillSyncDesignLayout、ApplySkillSyncDesign、SkillSyncEditorController。適用・6状態の実撮影済み |
| 対象プラットフォーム | Windows固有のモデル取込経路あり。今回の最終ビルド対象は未確認 | CatalogUI、既存取込コード |

## UI・適用経路

- 最新の配置・文言はユーザー添付 UI修正案.pdf に基づく SkillSyncPdfRefinement を適用。原資料JSONは保持し、生成時・既存Prefabの調整時の両経路で反映する。最新撮影は verification/pdf-captures/。

- 正本Prefab: Assets/UI/Prefabs/UIRoot.prefab。
- 対象Scene: Assets/EditorMain.unity。Assets/Scenes/SampleScene.unityもUIRootを使用する作業Scene。
- 既存起点: CatalogUI。既存サービスの補完・取込済みモデル復元を維持する。
- 新UIの適用入口: Tools/Automation/Apply SkillSync Figma Design。
- 新UI: UIRoot/SkillSyncDesign。共通のChrome、Viewport、リスト用Prefabテンプレート、ExportValidation、透過ModalInputBlocker。
- 旧UIの参照を残したまま表示を抑制する。CatalogUIは新UIの存在時にサービスと取込の初期化を行い、旧レイアウト全体の補完を省略する。
- 構築スクリプトが新UI所有下の階層を再生成する。UIRoot全体の再生成やScene YAMLの書換えは行わない。
- 各Visual/Control/Fieldは出典ノードIDを持つ。Assets/UI/SkillSyncDesign/handoff.jsonが実測値の縮約資料。

## 資産

| 項目 | 採用経路 |
|---|---|
| Button | 測定された元ベクターのImage、同寸法の透明Button、TMPラベル |
| Input | TMP_InputField。元の値の文字サイズ・ウェイトを使用。Auto Size不使用 |
| ScrollView | ScrollRect → Viewport（Image+Mask）→ Content（VerticalLayoutGroup+ContentSizeFitter）→ 行テンプレート |
| リスト背景 | Inspectorで角丸・枠幅を変更できるSkillSyncRoundedGraphic |
| アイコン | 元SVGの個別PNG化。4つのフォント欠損記号だけ参照PNGから抽出 |
| フォント | ローカルのC:/Windows/Fonts/NotoSansJP-VF.ttfから400/500/700を静的ウェイト化。Assets/UI/SkillSyncDesign/Fonts。OFL同梱 |
| TMP Font Asset | 適用時に3ウェイトのDynamic Atlasを生成。Windowsの絶対フォントパスに依存しない |
| 9-slice | 新規の9-slice依存は追加せず、元図形Spriteまたは幾何描画を使用 |

Noto Sans JPにない ↶・↷・⌄・⌕ は別フォントに置換しない。verification/symbol-sources.jsonに切出し元ノード・PNG・範囲を記録。TMP内では元のadvanceだけを確保し、記号部分をImageで描画する。文字入力と本文は画像化しない。実機のグリフ・ベースライン表示は未検証。

## 3D・配置

- Camera: EditWorkspace.ResolveCameraで既存Main Cameraを取得。RenderTextureで画面を偽装せず、Camera.rectをViewportの実スクリーン座標へ合わせる。
- 選択: SelectionService。Transform操作: MoveTool/RotateTool、SelectionTransformSession、CommandService。
- スナップ: EditSnapSettings。既存配置は数学的y=0面とPlacedObjectGroundingを使用。
- 仮配置: SkillSyncPlacementGhostはメッシュ表示だけを複製し、PlacedObject、ユーザースクリプト、永続IDを生成しない。確定時だけPlacementController→PlaceObjectCommandへ渡す。位置・角度・サイズを1回の配置Undoに含める。
- Raycast: 編集は既存Collider、ScreenPointToRay。新UI外・モーダル・対象選択・仮配置・試行の入力遮断はEditWorkspaceに接続。
- 寸法単位: 既存ScenarioObjectStateの仕様はワールド座標メートル。新UIでm↔cm変換する。
- 試行: SkillSyncTrialSessionは描画Transformだけを複製。元Renderer.forceRenderingOffを一時変更して復帰する。教材Transform、配置ID、コマンド履歴、保存用モデルを変更しない。
- 試行判定: 既存ScenarioConditionEvaluatorを再利用し、HeldSecondsを表示へ公開。ステップ内の全距離・保持条件の成立を待つ。
- 他の条件種別: データを保持する。この新しい距離・保持画面で意味が変わる判定を無理に実行せず、試行不可の理由を表示する。
- ライブラリ・配置一覧のサムネイル: 実モデルの描画用コピーをURP render requestで撮影。実機未検証。

## 教材・手順データ

- 編集モデル: Curriculum/ScenarioNode/StepNodeData/ConditionNodeData。
- 条件パラメータ: ConditionTypeCatalog.distanceMeters/holdSeconds。
- 手順編集: CurriculumGraphService.ExecuteCommand経由。並べ替えは既存の線形経路確認を通った場合だけ行い、分岐を平坦化しない。
- 下書き: EditorProjectService.Saveと既存autosave。保存先・形式を変更しない。
- 読込: 既存EditorProjectPanelをCtrl/Cmd+Oから開く。Ctrl/Cmd+Sまたは保存表示で既存保存サービスを呼ぶ。
- 書き出し: ValidateGraph、BuildScenarioExport、ScenarioModelBundle、ExportFileWriterを使用。先に下書き保存を試行し、保存失敗時は「保存済み」と表示しない。
- エラー誘導: 検証issueのnodeIdからStep/Conditionを選択し、対象の再指定または手順名入力へ誘導。

## 検証状況

- 現在の全Assets/ScriptsとAssets/Editorを、インストール済みUnityの参照DLLとRoslynで静的コンパイルした。古いAssembly-CSharp.csprojは使用しない。
- 回帰: 24 FBX + 52ロジック + 23保存/履歴 = 99件成功。
- 730ノードの出典・座標・実効文字サイズ・ベースライン照合成功。
- Unityのインポート、Prefab適用、Play Mode、6状態の実撮影と比較、実サービスの操作検証を実施。別解像度とPlayerビルドは未検証。詳細はimplementation-report.md。
- 参照SceneはAssets/Editor/SkillSyncDesign/ReferencePreview.unity。本番教材から独立し、固定データはEditor用JSONとUNITY_EDITOR限定コンポーネントへ隔離。操作検証は保存先をverification/test-projectsへ切り替えるEditor限定フックを使用した。
