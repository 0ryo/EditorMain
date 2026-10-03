# Gotchas

## 検証環境

- CodexはUnity Editor/CLIを起動しない。compile、PlayMode、build、Prefab/Scene再保存は未検証のまま人間へ渡す。
- `Tools/RegressionChecks` に本番ロジックをリンクする.NET 8回帰runnerがある。Unity数学/JSONはadapterのためPlayMode/Unity serializationの代用にはならない。独自asmdef/Lint/CIは未確認。
- `Assembly-CSharp.csproj` は調査時点でruntime C# 44本中31本の現行sourceが欠落していた。`dotnet build` の失敗/成功をUnity compileの代用にしない。
- 新規`.meta`のGUIDは32桁の16進数を生成して使う。不正なGUIDではUnityが対応する`.cs`自体を無視し、呼出元にCS0103が出る。クラスがディスク上に存在しても、Editor.logのinvalid GUID警告と`.meta`を確認する。

## 文書と実装の既知差異

- 教材切替では旧配置を先にDestroyしない。非表示stagingを準備し、旧instance・active状態・Curriculum参照・選択を保持して切替し、graph/UI復元成功後だけ旧配置とUndoを破棄する。失敗時は元instanceへ戻す必要があり、snapshotからの新規生成だけではUndo内の参照を守れない。commit後の通知例外で新配置を破棄しない。
- project読込の欠損検査はNormalize前に行う。編集modelのfield initializerだけでは省略と正当な空集合を区別できないため、v5/v6は初期値なしEnvelopeで必須集合を確認する。旧形式の省略補完と未完成graphの参照欠損は保持し、出力検証を読込条件に流用しない。
- RecoveryのprojectNameは元教材を一意に識別しない。起動時は同名教材へ上書きせず、元JSONを別名の「名前 復旧」へ移して保護する。Serviceの自動更新/後処理は`EditorProjectRecoverySession`経由で、成功した書込みとパス・内容が一致する復旧データだけを所有する。退避不能の他データは上書きせず、削除失敗後も所有権を手放して次教材による消失を防ぐ。Storeの無条件削除はユーザーの明示破棄専用（`QUALITY.md`）。
- 編集projectは `Application.persistentDataPath/Projects`、配布用Scenario/Placement JSONはEditorのproject直下`Exports`、PlayerのDocuments内`SkillSync/Exports`へ分離されている。`Docs/rules/scenario_rules.md` の `scenarios` 表記や旧UI仕様の `Assets/Exports` は古い。
- `Docs/worklog/worklog_UI/全体UI仕様.md` は日付順の追記文書で、古い「カード名のみ」仕様が後の3段カード仕様に置き換わっている。後半の新しい項目と現行codeを優先して突合する。
- 同UI仕様の一部（2026-03-03付近）と `BuildUiPrefabs.cs` の一部commentには文字化けがある。文字化け箇所だけを根拠に仕様を断定しない。

## UI / Prefab

- 固定配置の`SkillSyncDesignView`は2560×1440を全体フィットする。`UiScaleController`を倍率の共通窓口とし、旧パネルの`DesignTokenApplier`から1920×1080へ戻さない。Camera rectは入力停止中もLateUpdateで追従する。固定配置ボタンの一括拡大は、隣接する操作領域や条件番号との重なりを起こすため適用しない。
- 非表示の旧Outlinerもイベント購読は生きる。選択通知から非表示UIを全件再生成しない。固定UIの行は選択状態だけ更新し、同じ原型のサムネイルを配置個数分描画しない。ラベルの投影位置は文字情報の低頻度更新と分け、カメラ更新後のLateUpdateで反映する。
- 透明Imageに`Outline.useGraphicAlpha=false`を付けると内側までeffectColorで塗られる。キーボードのフォーカス枠には中空の四辺を使い、マウスクリック時は表示しない。
- モーダル表示中の編集遮断は`EditWorkspace.HasOpenModal`（教材一覧・設定・モデル追加）へ集約する。モーダル自身のEsc処理で`IsTypingIntoInputField`を使うと、自分自身の遮断条件により閉じられない。入力欄の実フォーカスを直接確認する。

- TextureImporterの`textureType = Sprite`だけでは`textureShape`は保証されない。SkillSync画像で`textureShape: 2`（Cubemap）が残り、再import成功後もSpriteが生成されなかった。UI画像はEditor APIで`TextureImporterShape.Texture2D`を明示し、設定済み判定にもshapeを含める。
- Figma取得済みSVGには幅／高さ0のVECTOR（区切り線）が含まれない場合がある。nodes.jsonのstrokeWeight・色・座標から別途描画する。Sprite対応だけの照合では欠落を検出できない。
- Unityの背景実行による撮影では、Editorの更新継続だけでなくPlay側の`Application.runInBackground`も確認する。Game viewの古いRenderTextureを取得できてしまうため、解像度だけでなく状態変化とフレーム進行も確認する。

- `StartCoroutine`は最初のyieldまで同期実行する。選択直後の詳細パネルでは、UI生成直後に`unscaledDeltaTime`を加算すると重いフレームでスライド終端へ飛び得る。現在位置を適用して一度yieldし、1フレームの進行を上限1/30秒に抑える。開閉は同じroutineで現在位置から反転する。

- `MaterialPropertyBlock`などUnityネイティブ資源をMonoBehaviourのfield initializerで生成しない。`Awake`または明示的な初期化メソッドで生成する。候補輪郭でconstructor例外の後に描画callbackのNullReferenceが毎フレーム続き、スクロールまで重くなった。構文・型チェックでは検出できない。
- `ScrollRect.AutoHideAndExpandViewport`はlayout時にviewportの余白を書き換える。検索欄などの固定ヘッダーをリスト上部に置く場合はviewport自動拡張を切り、検索欄・scrollbarの余白を共通設定する。

- `SampleScene`はBuild Settingsで無効でもユーザーの通常作業・確認Scene。Main CameraとUIRootから`CatalogUI`が不足serviceとworkspace gridを補完するため、scene pathだけでruntime補完を止めない。
- `CatalogUI.cs` は約3,000行あり、Catalogだけでなくservice補完、edit mode、settings、new object dialog、importまで担うhotspot。小変更でもStart/wiring/runtime補完/importの影響を検索する。
- Prefab正本方針でもruntime `Ensure*` が多数ある。Hierarchy名を変えると `transform.Find`、name比較、blocking UI判定、builder、Prefabが同時に壊れる。
- `BuildUiPrefabs.Build()` は既存Prefabの局所patchではなく、新しい `UIRoot` GameObjectを構築して `SaveAsPrefabAsset` する。実行前にbuilderが全必要要素を生成するか確認する。
- Scene/Prefab YAMLには古いserialized fieldが残ることがある。例として現行classから削除済みの `floorMask` がScene YAMLに残っている。raw YAMLのfield存在だけでruntime依存を判断しない。
- EventSystemはSceneで1個が前提。`CatalogUI` は欠落時に `StandaloneInputModule` 付きで生成し、重複を無効化する一方、現行SceneはInput System UI moduleを持つ。入力module変更は両経路を確認する。

## 配置・選択・入力

- Unityの`GetComponent<T>()`が返す欠損ComponentはEditorでCLRのnullとは異なる場合がある。`GetComponent<MeshFilter>()?.sharedMesh`は安全な欠損確認にならず、MeshFilterのないモデル親で`MissingComponentException`になった。`GetComponent<T>() ?? AddComponent<T>()`も追加をスキップするため使用しない。Unityの`== null`／`!= null`／truthinessで確認する。配置factoryは例外時に生成物を非アクティブ化して破棄し、接地・Collider設定前の孤立モデルが残らないようにする。

- 表示Floorと配置面は別物。配置は`y=0` planeでXZをgrid snapした後、`PlacedObjectGrounding`がrenderer bounds下端を接地する。`placementYOffset`は旧serialized互換のためfieldだけ残り、配置Yには使わない。
- 配置objectにusable Colliderがないと選択できないため、`PlacedObjectPickability` がrenderer boundsからBoxColliderを追加する。rendererもないmodelは自動修復できない。
- `PrefabRegistry.LoadDefault()` は `#if UNITY_EDITOR` 内だけでAssetDatabaseから読む。Playerではserialized registry参照が正しく設定されているかが重要。
- `CommandService.I` がない配置は直接配置fallbackを持つが、他の操作にはsingleton前提箇所がある。service欠落を単純な入力bugと誤認しない。
- Both input settingでも `EditInput` のpreprocessor順でlegacy Inputが優先される。Input System action assetが存在しても編集操作がそのaction mapを直接利用するとは限らない。

## Scenario

- 保存可能条件は厳しい。Start/End各1、全StepがStartから到達できEndへ達する非循環経路、各Stepに1件以上かつ`RuleSet.maxConditionsPerStep`以下のCondition、各Conditionはちょうど1 Stepへbind、近接・保持・距離・回転条件ではA/Bは別々の存在する `PlacedObject.id` が必要。把持・手放しはAだけを使う。上限既定値は8、許容設定範囲は1～32。
- 削除済みPlacedObject IDや不整合edgeは検証前に自動clearしない。欠損参照はE-10/E-11として残し、Conditionの差替え・削除または配置削除のUndoで解消する。Scenario検証の再評価はGraphChanged/CommandStack.HistoryChangedに連動する。
- 編集model (`Curriculum`) と出力model (`ScenarioExport`) は別。node title等を追加してもexportへ自動で出るとは限らない。
- 現行 `ConditionNodeData.DefaultTitle` は `手順1` で、UI上の `条件 n` 表記と一致しない。変更する場合はmigration/表示依存を確認する。

## モデル取込・platform

- 固定UIでは旧CatalogのCanvasGroupが透明のため、旧`statusText`だけへ取込エラーを表示するとユーザーに届かない。`CatalogUI.StatusChanged`を現在の画面へ接続する。glTFastの`GltfImport`はlogger省略時に詳細エラーを記録しないため、`CollectingLogger`を渡し、失敗時に詳細をConsoleにも残す。
- GLBの圧縮入力サイズと展開後テクスチャメモリは別物。展開済みモデルへ入力の256 MB上限を適用すると、小さな正常GLBも拒否する。展開後256 MB超は実測値付きの警告とし、入力・参照素材の合計上限とは分離する。展開後の拒否では読込中の最大メモリも制限できない。
- Unity EditorではFBXをAssetDatabaseへimportできる。PlayerではFBX経路はなく、Windows native dialog + `.glb/.gltf` のみ。
- runtime imported modelは `persistentDataPath/ImportedModels` に素材とmanifestを保存して復元する。`DefaultRegistry.asset`は変更しない。FBXはEditorのasset参照なので、Editorの教材書き出しが作る`.unitypackage`をXR projectへ取り込み、JSONの`prefabAssetPath`と`typeId`で事前登録する。PlayerではFBX素材のパッケージ生成はできず、事前登録またはGLB/glTF化が必要。
- 編集projectと素材ライブラリは`Application.persistentDataPath`配下を使う。配布先は`RuntimeExportPathUtility`が分離する。Editor限定FBX importのfile選択はOSのDocumentsを初期位置にし、project内path判定には`FileUtil.GetProjectRelativePath`を使う。Windows以外の保存実機確認はない。

## TMP

- 日本語fallbackはTMP Settingsから同梱のNoto Sans JP FontAssetを選ぶ。`TmpFontInitializer`はprivate-field reflectionやOS system font探索を使わず、公開material/font APIとScene内TMP componentのdirty通知で表示を更新する。旧DynamicOS fallback assetはWindows絶対pathを保持するため新規fallbackとして選ばない。
- TMP material cache/submesh更新中の例外はwarningへ落として継続する箇所がある。warningを無関係として一括無視しない。
