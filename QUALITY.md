# 主要作業の品質確認

## 2026-09-28 レビュー後の検証範囲

このタスクではユーザーの一時許可によりUnity 6000.2.6f2を直接検証した。以下の旧記録にある「Unity未実施」は、今回の実施範囲についてはこの節を優先する。

- managed回帰164件、PlayModeテスト16件が成功。従来の空Sceneで全Buttonを列挙するテストはUIなしでも合格するため、実際に対象を生成する表示境界・モーダル・フォーカス・固定操作領域のテストに置き換えた。managed runnerにはProfiler計測を行わないadapterを追加した。
- SampleSceneとEditorMainで、配置・複製・Undo/Redo、手順追加、条件の追加/前後選択/削除/Undo、保存→新規→読込をUnity APIから実行し、配置数と条件数を確認した。検証データの保存先はTemp配下に分離した。
- 1366×768、1920×1080、2560×1440のGame viewを取得し、固定UI全体が画面内に収まることを確認した。固定UIの倍率は全体フィットを優先し、44pxの一律拡大は行わない。小画面での44px操作領域・本文14pxの全面保証ではない。
- UnityコンパイルとScene/Prefab参照検査が成功。追加検証で旧API警告とPlayer限定のasync警告を解消。全種類の外部モデル取込とQuestは未検証。Quest端末・再生プロジェクトはユーザー回答で存在しないため対象外。
- CIはWindowsのUnityプロセス終了を明示的に待ち、PlayMode結果XMLの成功・実行件数を確認する。self-hosted runner上での実行は別途必要。

## 選択・表示・Windows Playerの追加検証（2026-09-28）

- マウス操作ではキーボード用フォーカス表示を出さず、Tab操作では中央を塗りつぶさない四辺の枠を表示する。透明ImageのOutlineが内側まで青く塗る現象を修正。PlayModeのフォーカス検査に内側を塗らないことを追加した。
- 名前ラベルの位置を0.25秒間隔の情報更新からLateUpdateへ分離。背景を半透明グレー、文字を白に変更。条件削除ボタンを濃赤＋白文字にし、下に12 design units以上の間隔を確保した。
- 選択時は既存の行を更新し、非表示の旧Outlinerは表示するまで再生成しない。サイドパネル停止中は毎フレームのRectTransform更新を行わず、行幅は既存LayoutGroupが管理する。ルートモデルのサムネイルはライブラリ原型ごとに共有し、個別の部品は従来の専用プレビューを保持する。
- Unity Editorの実入力でIME未確定「か」、Space変換「化」、Enter確定、未確定「あ」のEsc取消を確認。入力中はtyping gateが有効で、配置数と画面状態が変わらなかった。すべてのIME・入力欄の網羅確認ではない。
- Windows Development Player（Mono）をビルドし、配置、複製、Undo/Redo、手順・条件追加、条件の前後選択・削除・Undo、日本語名を含む保存→新規→読込が成功。通常データに影響しない専用productName/保存先を使用し、ビルド専用検証コードを作業後にAssetsから撤去。
- アプリのC#コンパイル警告は解消。ビルド時にTMP fallbackのimport整合性警告、検証用PipelineがPlayerで無効という通知が残った。CLIの待ち時間超過がBuildReportのerrorに計上されたが、BuildPipelineの結果はSucceededで実行ファイルを起動・検証済み。製品エラーとCLI制御のタイムアウトを区別する。

### 実測値と適用範囲

端末：Core i7-12700 / GeForce RTX 3070、2560×1440、VSync 0、Deep Profile無効。配置物は既定の単純なCubeモデルで、各ルートRenderer 1個。グラフは100ノード（Start/Endを含む）、200辺の非循環グラフ。100個は60秒、500個は20秒のカメラ移動と20回の選択・解除を測定。保存・読込は各5回。選択は同期処理時間、フレーム時間は実フレーム間隔であり、入力から描画までの遅延やCPU Profilerの全区間を表すものではない。

| Playerの配置数 | 選択P95 | フレームP95 / P99 | 保存最大 | 読込最大 | GC P95 | JSON |
|---|---:|---:|---:|---:|---:|---:|
| 100 | 5.32 ms | 1.62 / 6.66 ms | 32.79 ms | 39.89 ms | 40 B/frame | 165,106 B |
| 500 | 3.89 ms | 3.48 / 30.85 ms | 45.67 ms | 137.75 ms | 40 B/frame | 476,506 B |

復元した実教材（配置ルート2個＋部品195個、Renderer 180個）でも20回の選択・解除を確認し、Editorの選択P95は2.27ms、最大2.88ms。実モデルでカメラを動かした20サンプルのラベル投影誤差は0 design pixel。退避前後の教材JSONの一致も確認した。

同じEditor内の100個・20回の比較では選択P95が89.96→1.51ms。Playerのサムネイル共有前後では500個の読込最大が3929.24→137.75ms。EditorとPlayerの時間は直接比較しない。複雑な実モデル、実際の最大教材、Questの性能へ外挿しない。保存一覧の大量ファイル検査・操作ごとの総allocationなど、下記P1予算の全項目を測ったものではないため、P0〜P2全体の品質保証完了とは扱わない。

## 対象と事実確認

- 想定ユーザーは、3D配置と手順・成功条件を組み合わせて教材を作る教員。配置から再編集用保存、再起動後の継続、実行アプリへの配布までを対象にする。
- Unity 6000.2.6f2 / C#、uGUI 2.0.0＋TMP、URP 17.2.0、Input System 1.14.2（Both）、glTFast 6.16.1。Windows Build Profileがあり、有効なビルドSceneは `Assets/EditorMain.unity`。通常の手動確認には `Assets/Scenes/SampleScene.unity` も使う。他OSの配布動作は未確認。
- 人がUnityでSceneを開いてPlay、Windows ProfileからBuildする。エージェントはGUI、Unity Editor/CLIを起動しない。Unityコンパイル・PlayMode・Playerの合否は今回未確認。古い `Assembly-CSharp.csproj` は判定に使わない。
- 再編集形式は `persistentDataPath/Projects/*.skillsync.json`（project v6 / Curriculum v5）。配置のID・Transform・部品・表示/固定状態とグラフをまとめる。追加モデルは別の `ImportedModels` ライブラリに依存するため、プロジェクトJSON単独では移植できない。
- 配布用Scenario JSONはv6、Placement JSONはv2、保存先は `persistentDataPath/Exports`。GLB/glTFは素材を同梱、既定Prefab/FBXは受け取り側のtypeId登録が必要。受け取り側・Questは今回実行していない。
- 配置の状態は `PlacedObject`、選択は `SelectionService`、モードは `EditModeService`、グラフは `CurriculumGraphService`、Undoは `CommandStack`、dirtyと保存先は `EditorProjectService` が管理。プレビューは `ScenarioPlaybackSession` による成功模擬であり、VR条件判定とは別。
- 保存は同期処理。保存済み教材は同じパスへ自動保存、未保存教材は共通のRecoveryスロットへ保存する。Storeの書き込みが戻った後にServiceがcleanにする。非同期保存の世代管理は現行経路にはない。
- READMEと`.ai/PROJECT.md`の実行方式・JSON出力先・managed runnerの記述は現行仕様へ整合済み。専用Unity test assembly、実行側アプリとの結合、Quest実機は未確認。
- Main SceneのUIRoot参照はPrefabのGUIDと一致。Main Scene/UIRoot内のscript GUIDをAssetsとPackageCacheで照合する静的検査と、runtimeのService/Panel補完経路を確認した。serialized参照の型・fileID・実行時Missing Referenceまで保証するものではない。
- Console/Editor.log/Player.logとUnity Profilerを人が利用できる。今回はmanaged runnerと一時ディレクトリ内の実ファイルI/Oを実行した。画面・メモリ・フレーム時間は未計測。

## 主要フローと合格条件

以下は合格条件であり、すべて実機確認済みという意味ではない。既存の確認対象解像度は1366×768、1920×1080、2560×1440。性能上限や応答時間の保証値は未定義。初回の手動確認は既定Prefab 2個、Step 2個と条件1個から始め、部品を持つモデル1個でも繰り返す。

| フロー・開始条件 | 操作と期待結果 | 状態表示・中断/失敗/再試行 | 不変条件・確認方法 |
|---|---|---|---|
| 配置して編集を確定：カタログ読込済み | 配置→名前/位置変更→Undo→Redo。選択と表示が対象の実状態に一致 | 選択・モードを表示。ドラッグ取消は元位置、入力中Deleteは文字だけに作用。再操作可能 | 対象以外を変更しない。両Sceneで座標とUndo後の復元を比較。日本語IME変換中Enter、フォーカス喪失も手動確認 |
| **保存して次回再開：編集可能な教材がある（今回の対象）** | 保存→未保存教材の復旧データがある状態で終了→再起動→保存済み/復旧教材を選び再編集・保存 | 保存成功/失敗とdirtyを区別。復旧教材は一覧に「名前 復旧」、重複時は連番。読込取消は現状維持。退避失敗は元ファイルを残しエラーを返す | 起動だけで保存済み教材を変えない。復旧内容も失わない。独立した復旧教材への保存は原本を変更しない。managedテスト＋下記PlayMode手順 |
| 手順教材を検証して渡す：配置対象が存在 | Start→Step→Endを接続、条件指定→検証→プレビュー→出力 | 問題ノードと理由を表示。無効教材は出力不可。プレビュー終了後も編集内容を維持。出力失敗は編集を残し再試行 | ソース上でexport builder、version 6、typeId/parts、モデルbundleとpreview sessionの受入契約を照合済み。実行側アプリ・Questとの結合確認は未実施 |
| モデルを取り込み継続利用：Windows/対応形式の素材がある | GLB/glTF取込→配置→保存→再起動→再読込 | 段階表示、取消、欠損/外部参照エラー。取消後に再試行可能 | 半端な登録/孤立オブジェクトを残さず、typeIdを維持。managed preflight＋実モデルのUnity確認。FBXはEditor限定で同期Importの取消不可 |

モデル入力の現行上限は素材込み256 MB、JSON 16 MB、展開後500万頂点、texture縦横8192/計256 MB（既存監査・実装の制限）。これを快適な動作量やGPUピークメモリの保証と解釈しない。プロジェクト全体の入力サイズ上限・性能目標は未確定。

## 優先問題と対応状況

| 問題 / 影響 | 再現条件・根拠 / 確度 | 期待する挙動 / 修正方針 / 検証 |
|---|---|---|
| P0 起動時に同名教材が復旧データへ置換される / 正常データの巻戻り・別教材による上書き | 旧 `TryPromoteRecoveryForExistingProject` は名前だけから保存先を決め、SaveAutomatic後にRecoveryを削除。修正前テストで原本変更を再現済み | **今回修正**：Recoveryを別名へ上書きなしでMove。日時や名前を同一教材の証拠としない。元JSONを再シリアライズしない。原本不変・復旧の意味・連番・反復・失敗/再試行を実行検証 |
| P1 読込データの欠損を空データへ補完 / 誤った空教材で現在の作業を置換する | 現行schemaVersionだけのJSONが読み込み成功することを修正前の回帰テストで再現 | **部分修正済み・Unity確認待ち**：v5/v6のcurriculum/objects/nodes/edgesの欠損・nullを補完前に拒否。null要素、不明なnode/edge列挙値、Transform/条件の非有限値も拒否。現在形式ではノードIDの欠損/重複と、条件が複数の配置/部品に一致するIDを参照するケースも拒否。参照のない重複配置IDの修復、旧形式移行、編集中の未解決参照は維持 |
| P1 読込の置換途中に例外 / 現在の正常な配置を失う恐れ | 旧処理は旧配置をDestroyしてからgraph/UIを復元。順序はコードで確認。エンジン上の例外は未再現 | **実装修正・Unity確認待ち**：非表示領域で配置を準備し、旧instanceとactive状態・graph参照・選択を保持して切替。graph/UI復元成功後だけ履歴消去と旧配置破棄。失敗時は旧状態を復元し、復元中の例外でも残りを試す。本番切替ヘルパーの失敗注入テストは成功 |
| P1 Recovery退避失敗後の継続操作 / 残した復旧元が後から消える | 従来のSave/Load/NewProjectの後処理は共通Recoveryを無条件削除。退避失敗→後処理で消失する回帰テストを修正前に確認 | **修正済み・Unity確認待ち**：`EditorProjectRecoverySession` が成功した書込みのパスと内容を保持し、自分の復旧データだけを更新/削除。他のデータは退避成功まで上書きせず、通常保存は可能。破損・差し替え・削除失敗・再試行をmanaged実ファイルI/Oで確認 |
| P2 入力・モーダルの境界 / 意図しない3D操作の懸念 | SelectionServiceに入力欄focus/Application focusの抑止、EditWorkspaceにModal捕捉は存在。すべてのUIとIME経路は未検証 | **コード修正済み・Unity確認待ち**：MoveTool/SelectionOutlineの進行中TransformドラッグとScenarioGraphのノードドラッグは、focus喪失またはProject modal表示時に取消して開始位置へ戻す。フォーカス喪失はUpdate停止中にも各ジェスチャーのコールバックで取消す。Catalogドラッグも取消し、配置入力の一時ブロックを解除する。Project modal表示中は両Sceneのポインター操作・編集ショートカットを共通ゲートで止め、非フォーカス中は配置入力を受け付けない。入力中Delete/ショートカット、IME Enter、モーダル背面、非フォーカス中の配置入力、各ドラッグ中focus喪失を両Sceneで実操作確認 |
| P2 大きい教材の自動保存・一覧 / 操作が止まる懸念 | snapshot走査・JSON化・同期I/O、一覧で各ファイルをTryLoadする。重さは未計測 | **計測準備済み・Profiler測定待ち**：自動保存のfingerprint/snapshot/JSON化/ファイル書込と、ライブラリ列挙/各ファイル解析/行UI再生成をProfiler Markerで分けた。同じ教材と端末で下記手順を計測し、最大停止時間・GC・継続時の購読/生成数を比較してから改善。性能向上を今回の成果としない |

## 大きい教材の応答性計測

### P1標準規模の暫定性能予算

以下をP1の初回合格目標とする。計測済み性能や端末保証ではなく、基準端末で実測して妥当性を更新するための暫定値。Play ModeとWindows Development Playerを混ぜず、通常のリリース相当設定・Deep Profile無効で判定する。標準教材は配置ルート100個、Scenario node 100個、edge 200本とし、モデル部品数・Renderer数・JSONサイズは実データ値を併記する。

| 操作 / 指標 | 暫定予算 | 判定方法 |
|---|---:|---|
| 3D viewport・graph操作中のframe time | P95 ≤ 16.7 ms、P99 ≤ 33.3 ms | 60秒操作し、安定後のCPU frame timeを計測。Scene別に報告 |
| 入力から見た目の応答まで | P95 ≤ 100 ms | 選択、移動開始、graph node選択を各20回測る。入力時刻から最初の反映frameまで |
| 標準教材の明示保存・読込による連続停止 | P95 ≤ 250 ms、単発最大 ≤ 500 ms | warm-up後5回。メインスレッド上の操作全体と最大frame timeを併記 |
| 標準件数の保存済み教材一覧を開く | P95 ≤ 250 ms、単発最大 ≤ 500 ms | 保存件数・合計JSONサイズを固定し5回計測 |
| steady-stateの編集操作で発生するGC | P95 ≤ 4 KB/frame、10秒以上継続するGC停止なし | 選択・viewport移動を60秒行い、GC.Alloc/frameとGC pauseを計測 |
| 保存・読込・一覧更新1回のmanaged allocation | ≤ 8 MB/operation | 同一データ・5回のGC.Allocを記録。閾値超過時は最大allocation箇所も記録 |

合格は単一回の最小値ではなく、5回のP95と単発最大値で判定する。目標超過時は呼出元とallocationを特定し、目標を満たすまでP1を閉じない。端末、GPU/CPU、実行形態、解像度、VSync設定、データ規模を記録する。P2の最大教材計測は次節の手順で別途行い、P1標準規模の合格から大規模性能を推定しない。

最適化前の基準値を取得するため、Unity Profilerで同じ端末・同じ実行形態を使う。Play ModeとWindows Development Playerの結果は混ぜず、Deep Profileは無効にする。教材は小規模、通常想定、現在扱う最大規模の3つから選び、配置ルート数・配置JSONのサイズ・グラフのノード/辺数・モデル部品数と、Projects内の保存済み教材数/JSON合計サイズを記録する。このP2計測では新しい容量上限や性能目標を追加設定しない。

1. 各データセットを一度開いてウォームアップし、2回目以降を5回計測する。ProfilerのCPU Usage Timelineを使用し、メインスレッドで発生した最大フレーム時間と各Markerの所要時間、`GC.Alloc`を記録する。
2. 教材内の配置を変更し、dirty更新から自動保存まで待つ。保存済み教材と未保存教材のRecovery保存をそれぞれ測り、`EditorProjectService.BuildCurrentFingerprint`、`SaveRecoveryIfChanged`、`SaveRecoveryNow`、`EditorProjectSnapshotBuilder.*`、`EditorProjectStore.SerializeProject`、`WriteProjectFile`を比較する。
3. 保存済み教材一覧を開き、`EditorProjectStore.ListDirectory`、内側の`TryLoad.ReadFile`/`TryLoad.ParseAndMigrate`、`EditorProjectPanel.RefreshProjectList`を比較する。テンプレート/削除済み一覧も対象にする場合は件数を分けて記録する。
4. Projectパネルを20回開閉してから1フレーム以上待ち、行UIのGameObject数が毎回の開閉後に増え続けないか確認する。Service/Panelのインスタンス数とイベント通知の重複も同じ操作で確認する。
5. シナリオごとに中央値と最大値を報告する。端末、実行形態、教材規模、保存件数、Marker時間、最大フレーム時間、GC.Alloc、反復開閉後のObject数を並べ、最も重いMarkerとその呼出元を見て改善箇所を決める。Profiler Markerは診断用で、まだ最適化や性能保証は行っていない。

記録用（各行はデータセットごとに作成）：

| 端末 / 実行形態 | 教材規模（配置 / 部品 / ノード・辺 / JSON） | 保存件数・合計サイズ | 操作 | 最大フレーム時間 | 主なMarker時間 | GC.Alloc | 反復後のObject / Service / Panel数 |
|---|---|---|---:|---:|---|---:|---|
| | | | | | | | |

## 起動時復旧の保証範囲

`EditorProjectStore.TryPreserveRecoveryAtStartup` は、読み取れるRecoveryをProjects直下の衝突しない「名前 復旧.skillsync.json」へ移す。未保存教材も対象にする。移動先は元と同じProjects配下で、`File.Move` の上書きしない挙動を使う。成功後は共通スロットが空になるので再起動による重複を作らず、後続の通常保存が共通スロットを消しても退避済み教材は残る。元のbackupはこの処理では削除しない。

既存の通常保存は同一ディレクトリのtmp作成→File.Replace（bak付き）/File.Move。この方式・schema・UI構造・依存は変更していない。Windows上のmanaged実ファイルI/Oで確認したが、Unityのランタイム差、ネットワークドライブ、電源断に対する永続化や複数アプリの同時編集は保証していない。

退避に失敗しても、Serviceの通常保存・読込・新規作成・保存済み教材の自動保存の後処理は、現在のRecoverySessionが書いた内容と一致する復旧ファイルだけを削除する。別セッションの内容や壊れたJSONは残る。未保存教材の自動保存は、残った他の復旧データの退避を再試行し、失敗時には書き込まず既存のStatusChangedへ理由と通常保存の案内を通知する。cleanへの更新も行わない。現在の教材が所有するスロットは繰返し更新でき、保存ごとに退避教材を増やさない。

削除に失敗した場合も所有権を手放すため、次の教材の自動保存が残存データを直接上書きしない。自動後処理は以前のbackupを削除しない（次回の通常書込みでは従来どおりbakを更新する）。明示的な「破棄」は従来の確認UIから実行できる。内容比較は同時実行ロックではなく、複数アプリからの同時書込みには未対応。起動時警告の画面表示は未改善であり、継続時の自動保存エラーはパネルの既存ステータスを使う。

## 実行検証と人による確認

自動確認：リポジトリルートで `dotnet run --project Tools/RegressionChecks/RegressionChecks.csproj`、`git diff --check`。

- 修正前の既存テスト：FBX 24、authoring 52、persistence/history 23件が成功。
- 原本保持の回帰テストを先に追加し、旧実装で `Startup recovery must not overwrite a same-name saved lesson` が失敗することを確認した。
- 初回修正後は計109件が成功。継続操作の保護を追加後はFBX 24、authoring 52、persistence/history 48件（計124件）が成功し、`git diff --check` も成功。Main Sceneの15種、UIRootの47種のscript GUIDは初回の静的確認でAssetsまたはPackageCacheに解決できた。
- 継続操作の修正前には `Save/load/new cleanup must retain recovery from a previous session` の失敗を確認。修正後は退避不能時の削除防止・上書き防止・通常保存・再試行・連続autosave・破損保持・明示破棄・外部差し替え・新セッション・実ファイルロック失敗後の所有権解放を確認した。Serviceの各後処理がこの本番sessionへ委譲することはソースで照合し、Service自体のUnity実行は未検証。
- 修正後の回帰テストは、同名原本保持、JSON無変更での退避、ID/座標の復元、退避後の編集保存、連番、再起動反復、退避失敗時の保持、再試行、破損JSONの保持を検査する。座標の許容誤差はテスト値に対し1e-5。期待は原本不変・データ保持という要件から決めた。
- runnerは本番Store/Writer/Migrationを直接リンクするが、数学/JSONはmanaged adapter。ServiceのAwake/dirty/UI、Unity JsonUtility、Prefabの実行結果を検証したとは扱わない。ロールバック試験の `Execute failed: rollback` は意図した負例のログ。

読込入力の追加検証では、修正前に `Incomplete current project must not become an empty lesson` が失敗することを確認。修正後はFBX 24、authoring 52、persistence/history 77件（計153件）と `git diff --check` が成功。必須集合の欠損/null、null要素、不明な列挙値、float範囲超過、Store経由の拒否と元ファイル不変を検査した。v1〜v6の生成fixtureはID・座標（誤差1e-5）・日本語の手順名を保持し、明示した空集合と未完成の手順・条件も読み込める。過去の全実ファイルを網羅する互換性試験ではない。

必須集合の検査は初期値のない読み取り用Envelopeを使い、既存のJsonUtilityとmigration境界内で行う。v1〜v4/バージョンなしの省略補完は互換目的で維持するため、旧形式を装った欠損まで拒否する保証はない。エラー時はprojectをnullにして既存のLoadInternalの早期returnへ渡し、置換処理へ進ませない。

置換処理はrollbackを実装した。stagingの生成・状態準備に失敗すれば旧配置には触れず、切替後のgraph/UI復元に失敗すれば新配置を非表示にして旧instanceのactive状態、元Curriculum、選択を戻す。保存先・dirtyの確定とRecovery後処理は成功後に行う。新規教材への切替も同じ経路を使う。旧配置の破棄とUndo消去はcommit後だけ行い、完了通知の例外はログへ分離する。commit後の片付け失敗では新教材を破棄しないが、非表示の旧オブジェクトが残る可能性がありログ確認が必要。

切替ヘルパーのrollbackなし実装で回帰テストの失敗を確認し、修正後は切替失敗、履歴のUndo実行、復元中例外後の残りの復元、成功時の一度だけのcommitと履歴消去を確認した。FBX 24、authoring 52、persistence/history 82件（計158件）が成功。これは本番ヘルパーとCommandStackを使ったmanagedモデル上の検証で、Unity上の旧不具合を実操作再現したものではない。PrefabのAwakeによる任意の副作用、エンジンのactivation、graph UIの内部選択・表示位置まで復元できるかは未検証。

現在形式の参照曖昧性監査を追加し、ノードID欠損/重複、重複した配置/部品IDへの条件参照を拒否する回帰確認を追加した。未解決の辺・条件参照は編集中データとして維持し、条件から参照されない重複配置IDは既存の修復経路へ渡す。再実行でFBX 24、authoring 52、persistence/history 88件（計164件）が成功。Unity/Player上の読込・移行確認は未実施。

入力経路の静的確認では、SelectionServiceはfocus喪失で止まる一方、MoveToolの進行中ギズモドラッグとSelectionOutlineのスケール操作は同じfocus/UI gateを通らなかった。またProject modal表示中も、入力欄以外にfocusがある場合の編集ショートカットが通り、ScenarioGraphのノードドラッグは継続できた。MoveToolとSelectionOutlineは中断時にTransformを開始状態へ戻し、NodeDragHandlerはノード/複数選択を開始位置へ戻して未確定Undoを残さない。Catalogカードのドラッグもfocus喪失でキャンセルし、配置入力の一時ブロックを解除する。フォーカス喪失時はUpdateの実行継続に頼らずコールバックで進行中ジェスチャーを取消す。開いているProject modalをEditWorkspaceの共通gateから参照し、ポインター・キーボード操作とSkillSync固有操作を抑止するようにした。Unity非依存のmanaged回帰runnerはこのMonoBehaviour経路を含まないため、両Sceneで入力中Delete/ショートカット、IME Enter、モーダル背面、各ドラッグ中focus喪失とProject modal表示中のキー操作を実操作確認する。

PlayMode/Windows Playerで人が確認する手順（いずれも未実施）：

1. 実データのコピーを取り、検証専用保存領域を使う。Editorでは `EditorProjectStore.EditorVerificationProjectsDirectory` をServiceの生成前にテスト用ディレクトリへ設定できる。Playerにはこのoverrideはないのでテスト用ユーザーを使う。
2. 教材Aに既定Prefabを2個置き、名前/座標とStepを保存。AのJSONを退避。別内容で同名の未保存教材を作り、自動保存後、通常保存せず終了する。
3. 再起動して一覧のAと「A 復旧」を別々に開く。Aは元の配置/手順、復旧教材は終了前の配置/手順であることを確認。復旧教材を編集・保存し、AのJSONが不変であることも確認する。
4. 同名Recoveryで再実行すると連番になること、Recoveryなしで再起動すると増えないことを確認。読込取消は現在の選択・編集を維持することを確認する。
5. テスト領域だけで復旧の移動先に同名ディレクトリを作り起動する（他の通常保存先は書込可能にする）。退避失敗後、通常保存・別教材読込・新規作成を行っても元Recoveryの内容が変わらないことを確認。未保存教材を編集し、自動保存のエラーと通常保存の案内が表示され、未保存表示が残ることを確認する。同名ディレクトリを取り除いた後の自動保存で、以前の内容が別教材に保護され、現在の内容がRecoveryに保存されることを確認する。壊れたRecoveryでも同様に上書き・自動削除されず、明示的な破棄後に再保存できることを確認する。
6. 両SceneとWindows build、上記3解像度で一覧の識別・結果表示・Missing Reference/例外を確認する。イベント購読はServiceのOnDestroy解除があるが、反復Play/パネル開閉で通知多重化がないかも確認する。
7. テスト用に保存したv6教材を複製し、objectsをnull、curriculumのnodesを削除、または配置のposition.xを1e39にしたものを用意する。編集中にそれぞれを読み込み、失敗表示の後も配置・手順・選択・Undo履歴・保存先・dirtyが変わらず、元ファイルも不変であることを確認する。正常なコピーは続けて読めること、未接続の編集中教材を誤って拒否しないことも確認する。Unity JsonUtilityでの欠損フィールドとnullの解釈はこの手順で確認する。
8. 隔離したプロジェクトコピーのPlayModeで、配置2個を編集・複数選択しUndoを残す。`ReplaceCurrentProject`のapply内、graph復元直後に一時的な例外を注入して別教材を読む。旧instance・active状態・graph・選択・保存先・dirtyが戻り、Undoが実行でき、新配置とProjectLoadStagingが次フレームに残らないことを確認する。例外を除去して同じ教材を再読込でき、成功時だけ履歴が消えることを確認する。新規作成への切替、非表示/固定状態、部品モデルでも繰り返す。UI再構築での例外と、完了StatusChanged購読者の例外もそれぞれ試し、後者では読込成功と新教材を維持することを確認する。
9. テスト用v5/v6教材で、nodeId欠損/重複と、同じIDを持つ配置または部品をConditionが参照するデータを作り、読込が拒否されること、現在の教材・選択・Undo・dirty・元ファイルが変わらないことを確認する。条件が参照しない重複配置IDは読込時に一意化されること、未解決の辺/条件参照は編集用下書きとして読み込めることも確認する。
10. 両Sceneで配置を選びMove/Scale handleのドラッグ中にアプリfocusを外す、入力欄へfocusを移す、Project modalを開く操作を試す。ドラッグ開始時のTransformへ戻り、Undo履歴に取消済み操作が残らず、戻った後は通常操作を続けられることを確認する。非フォーカス中に配置モードでクリックしても配置されないことも確認する。シナリオノードの単体/複数選択ドラッグも同様に中断し、開始位置へ戻ることを確認する。カタログカードのドラッグ中にfocusを外した場合は配置が発生せず、以後の配置操作がブロックされないことも確認する。モーダル背景のクリックが背後の3D選択・変形を起こさないことも確認する。

自動確認できたのはStore境界の改善。主要フロー全体の合格はUnity/Playerでの上記確認後に判定する。
