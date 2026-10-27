# Gotchas

## 検証環境

- CodexはGUI操作をせず、Unity CLIとEditor APIでcompile、Prefab更新、Player buildと出力物を確認する。GUI操作・PlayModeの実機確認はユーザーが行う。
- ユーザー依頼でcomputer useによる画面点検をする場合: Playerは全画面の別Spaceで動き、app_*の背景操作ではクリックもドラッグも届かないため、画面全体の操作が必要（サブエージェントからの全画面操作は開始直後に中断されたため、メインセッションで行う）。2026-10の初見UX改善以降、定期autosaveは復旧dataだけへ書く（教材fileは明示保存でのみ変わる）。それ以前のビルドでは現在の教材へ自動保存されるため、テスト編集は元に戻すで取り消す。3Dモデル読込のOSファイル選択パネルはcomputer useで操作できず、ユーザーに閉じてもらう。`Builds/`には複数の旧Playerが残り、bundle IDも同じなので、点検や再現の前に`~/Library/Logs/DefaultCompany/Editor/Player.log`先頭のMono pathか`ps`で起動中の`.app`を確認する。
- 変形モード「大きさ」では、選択を変えたクリックと同じフレームで`SelectionOutline`のスケールドラッグが新しい対象に対して始まり、細い部品が数倍に伸びて自動保存される。部品の勝手な拡大は、まずこれを疑う。
- `Tools/RegressionChecks` に本番ロジックをリンクする.NET 8回帰runnerがある。Unity数学/JSONはadapterのためPlayMode/Unity serializationの代用にはならない。独自asmdef/Lint/CIは未確認。
  - macOSでは`dotnet run -- --skip-fbx --keep-going`で回す。FBXの検査はWindowsのpathを前提にしている。Material exportの「Concurrent exports」は、変更前のコードでも失敗する（2026-10-25確認）。
- `Assembly-CSharp.csproj` は調査時点でruntime C# 44本中31本の現行sourceが欠落していた。`dotnet build` の失敗/成功をUnity compileの代用にしない。
- 新規`.meta`のGUIDは32桁の16進数を生成して使う。不正なGUIDではUnityが対応する`.cs`自体を無視し、呼出元にCS0103が出る。クラスがディスク上に存在しても、Editor.logのinvalid GUID警告と`.meta`を確認する。

- 固定UIの全画面描画は、一時PlayModeテストで`EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/EditorMain.unity")`→起動画面を閉じる→root CanvasをScreen Space - CameraにしてRenderTexture（2880×1800）へ`cam.Render()`しPNG保存、で取れる（`-nographics`なし、`LogAssert.ignoreFailingMessages = true`、runtime型はreflection）。拡大確認はUnity内で切り出したPNGを保存する（ローカルに画像処理ツールなし）。確認後にテストを消す。
  - 注意点：
    - root Canvasはscene読込直後（レイアウト計算前）に切り替える。後から切り替えると、batchの小さいScreen基準のまま配置が崩れる。
    - 描画用カメラは3D sceneから遠ざける（例：y=-5000）。近いと3D物体が灰色の帯として写る。
    - batchではアプリが常に非フォーカスなので、`SkillSyncEditorController.Update`が何もしない。`RefreshTrial`、`SkillSyncWorkspaceExpansion.Tick`、`LateUpdate`をテストから呼ぶ。
    - 見本のGLBはEditor PlayModeで素材エラーになり開けない。教材は既定Prefabを`PlaceForDesignUi`で置き、手順と条件をreflectionで組む。
- `new GameObject(name, typeof(自作Graphic))`では`CanvasRenderer`が付かず、何も描かれない（配置一覧の目・鍵が見えなかった原因）。`typeof(CanvasRenderer)`を明示する。`SkillSyncRoundedGraphic`は`borderWidth=0`でも`borderColor`の面を下に描くので、塗りを透明にする時は`borderColor`も合わせる。
- 生成UIのTMPは左上pivotが多い。空のTMP_InputFieldはキャレットを文字の原点（y=0）に描くため、左上pivotだと欄の上端に出る。入力欄のテキストはpivotを中央にする。生成時に作られた入力欄にはplaceholderも枠もない（手順の題名欄）ので、実行時に足す。
- Unity CLIで固定UI全体の画面を確認するには、`-nographics`なしのPlayModeテストで画面解像度を設定し、UI CanvasをScreen Space - CameraにしてRenderTextureへ描く。`-nographics`ではUIも3Dも描かれない。3D画面内の部品は描かれない場合があるため、重なりは位置計算でも確かめる。
- 並行作業でUnity compileを同時に回すには、`git worktree add`後に`cp -c -R Library <worktree>/Library`（APFS clone、数秒）でLibraryを複製すると、再importなしに約30秒でbatch compileできる。同じproject pathでUnityを2つ起動しない。batch実行は`NotoSansJP-* TMP.asset`のdynamic atlasを書き換えるので、コミット前に戻す。

## 文書と実装の既知差異

- 教材切替では旧配置を先にDestroyしない。非表示stagingを準備し、旧instance・active状態・Curriculum参照・選択を保持して切替し、graph/UI復元成功後だけ旧配置とUndoを破棄する。失敗時は元instanceへ戻す必要があり、snapshotからの新規生成だけではUndo内の参照を守れない。commit後の通知例外で新配置を破棄しない。
- project読込の欠損検査はNormalize前に行う。編集modelのfield initializerだけでは省略と正当な空集合を区別できないため、v5/v6は初期値なしEnvelopeで必須集合を確認する。旧形式の省略補完と未完成graphの参照欠損は保持し、出力検証を読込条件に流用しない。
- RecoveryのprojectNameは元教材を一意に識別しない。起動時は同名教材へ上書きせず、元JSONを別名の「名前 復旧」へ移して保護する。Serviceの自動更新/後処理は`EditorProjectRecoverySession`経由で、成功した書込みとパス・内容が一致する復旧データだけを所有する。退避不能の他データは上書きせず、削除失敗後も所有権を手放して次教材による消失を防ぐ。Storeの無条件削除はユーザーの明示破棄専用（`QUALITY.md`）。強制終了→再起動を繰り返すと起動ごとに「復旧 (n)」が増えるため、既存教材と内容（保存時刻を除く）が同じ復旧は退避せず消し、起動画面の「最近使った教材」の補充からは復旧コピーを外す（「教材を開く」には残る）。
- 編集projectは `Application.persistentDataPath/Projects`、配布用Scenario/Placement JSONはEditorのproject直下`Exports`、PlayerのDocuments内`SkillSync/Exports`へ分離されている。
- `BuildUiPrefabs.cs` の一部commentには文字化けがある。文字化け箇所だけを根拠に仕様を断定しない。

## UI / Prefab

- `SkillSyncDesignView.Show` はstate別 `Visual.orders` で毎回兄弟順を並べ直す。後から追加した要素にordersがないと既存の背景より下に入り、activeでも見えない。文字の中央揃えはTMP alignmentだけでなく実際の親・背景の座標を確認する。共有refinementがボタン文字/数値単位を子にしているため、Inspectorの固定座標をそのまま子へ渡すと二重に移動する。

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
- ライブUIは固定SkillSync UI（`UIRoot/SkillSyncDesign`、`ApplySkillSyncDesign`が付与）。旧`Panel_ScenarioGraph`/`Panel_Detail`は`SetActive(false)`で休眠し、`CatalogUI`はalpha 0のバックエンド（取込・設定・ライブラリ状態）。`BuildUiPrefabs`/`ApplyUiPrefab`は`SkillSync`を一切生成しないため、`AutomationEntry.ApplyUiEdits`単独実行は固定UIを失う恐れがある。旧UI上の`FindFirstObjectByType<ScenarioGraphUI>()`は常にnull。
- Scene/Prefab YAMLには古いserialized fieldが残ることがある。例として現行classから削除済みの `floorMask` がScene YAMLに残っている。raw YAMLのfield存在だけでruntime依存を判断しない。
- マウスで押したButtonはEventSystemの選択に残り、3D画面で使う矢印キーやEnterのUIナビゲーションで近くのTMP_InputField（教材名欄など）へ選択が移って編集状態になる。`KeyboardAccessibilityController`がクリック時のButton選択を外し、教材名欄はクリックでだけ編集にする（`shouldActivateOnSelect=false`、Navigation None）。
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

- glTFastの実行時読込用URP shaderはSceneの依存に現れず、Editorでは正常でもPlayerで欠損material／ピンク表示になる。`RuntimeGltfShaderBuildSetup`がビルド前にPBR・SpecularGlossiness・Unlit・Clearcoatの4 shaderをAlways Includedへ登録する。未知の追加モデルにも対応するため材質variantを保持し、初回ビルドのshader compileには時間がかかる。GLB内の画像・材質とUnity側のshaderは別物で、XR受信アプリ側にも対応shaderの同梱が必要。読み込み成功時にもglTFastのErrorとRendererのmaterial/shaderを検証し、不完全な登録を拒否する。
- GLBにも外部画像URIを持つものがある。`ScenarioModelBundle`と`TeachingMaterialArchive`は配布前に参照素材を検証する。内蔵材質・画像は元GLBをバイト単位で保持し、外部素材は相対フォルダーごと同梱する。単体GLBで配れるのは参照素材が内蔵されている場合のみ。
- 固定UIでは旧CatalogのCanvasGroupが透明のため、旧`statusText`だけへ取込エラーを表示するとユーザーに届かない。`CatalogUI.StatusChanged`を現在の画面へ接続する。glTFastの`GltfImport`はlogger省略時に詳細エラーを記録しないため、`CollectingLogger`を渡し、失敗時に詳細をConsoleにも残す。
- GLBの圧縮入力サイズと展開後テクスチャメモリは別物。展開済みモデルへ入力の256 MB上限を適用すると、小さな正常GLBも拒否する。展開後256 MB超は実測値付きの警告とし、入力・参照素材の合計上限とは分離する。展開後の拒否では読込中の最大メモリも制限できない。
- Unity EditorではFBXをAssetDatabaseへimportできる。PlayerではFBX経路はなく、Windows/macOSのfile dialogから`.glb/.gltf`を読み込む。macOSの`osascript`出力は`ProcessStartInfo.StandardOutputEncoding`をUTF-8に明示しないと日本語pathが`?`に化ける。native dialogはUnityの現在frameを長時間止めるため、通知の表示時計は次のfocus中frameで開始する。
- runtime imported modelは `persistentDataPath/ImportedModels` に素材とmanifestを保存して復元する。`DefaultRegistry.asset`は変更しない。FBXはEditorのasset参照なので、Editorの教材書き出しが作る`.unitypackage`をXR projectへ取り込み、JSONの`prefabAssetPath`と`typeId`で事前登録する。PlayerではFBX素材のパッケージ生成はできず、事前登録またはGLB/glTF化が必要。
- 編集projectと素材ライブラリは`Application.persistentDataPath`配下を使う。配布先は`RuntimeExportPathUtility`が分離する。Editor限定FBX importのfile選択はOSのDocumentsを初期位置にし、project内path判定には`FileUtil.GetProjectRelativePath`を使う。Windows以外の保存実機確認はない。

## TMP

- 日本語fallbackはTMP Settingsから同梱のNoto Sans JP FontAssetを選ぶ。`TmpFontInitializer`はprivate-field reflectionやOS system font探索を使わず、公開material/font APIとScene内TMP componentのdirty通知で表示を更新する。旧DynamicOS fallback assetはWindows絶対pathを保持するため新規fallbackとして選ばない。
- TMP material cache/submesh更新中の例外はwarningへ落として継続する箇所がある。warningを無関係として一括無視しない。
- 同梱Noto Sans JPには`▸▾↶↷`の字形がなく□になる。折りたたみは`▶▼`、元に戻す/やり直すは文字で書く。UI文字列に記号を足すときはフォントのcmapで有無を確かめる。
- TMPは日本語を任意の2文字間で改行し（「変わりま／せん」）、`。、`の後では改行しない。`SkillSyncLineBreak`（`ITextPreprocessor`）が文節（漢字・カタカナ・英数字の連なり＋後続ひらがな）を`<nobr>`で包み、文節間と句読点の後にU+200Bを入れる。richTextが要るため、後でコードがrichText=falseに戻したラベルと`<`を含む文字列は素通しし、入力欄の中は対象外（キャレット位置）。表示用だけを書き換え、`text`は変えない。
