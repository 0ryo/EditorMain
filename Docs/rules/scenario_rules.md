# Scenario Creation Feature: Rules

## 1. Design and Permissions 🔐

### 1.1 The first implementation must work with zero additional permissions (top priority)
The default save location for scenario data is:
- Under `Application.persistentDataPath`
- Format: JSON (recommended) or ScriptableObject (depending on use case)
- "Let the user choose a custom save location" is deferred (= increases permission and OS-specific differences)

> `persistentDataPath` is the standard path Unity provides for "data that should persist between sessions."  
> By starting within this path, most cases will not require additional OS permissions.  

### 1.2 Patterns where permissions increase in future extensions (only when needed)
Adding the following features may require platform-specific configuration:
- Using the device's **camera / microphone / location** (iOS requires Usage Descriptions in Info.plist)
- Saving directly to the device's **external storage** (Android has particularly strict restrictions)
- **Cloud sync** (network access, authentication / OAuth, ATS / certificate configuration, etc.)

The AI agent must proceed as follows:
1) Explain "why this is necessary" (alternative: is `persistentDataPath` insufficient?)
2) List the target platforms (Standalone / Android / iOS / WebGL, etc.)
3) Specify the Unity-side configuration points before implementing

### 1.3 iOS Notes
- Accessing sensitive information or device features on iOS requires permission descriptions (Usage Descriptions).
- Proceed on the assumption that adding Usage Descriptions in Unity's Player Settings will be reflected in Info.plist.

### 1.4 Android Notes
- On Android, permissions may need to be added to the Manifest.
- If adding permissions, handle `Assets/Plugins/Android/AndroidManifest.xml` (or Unity's generated / custom procedure).
- However, the initial MVP follows the "save to persistentDataPath" approach and must not trigger Manifest changes.

---

## 2. 教材の進行とデータ形式

- 失敗時の処理や失敗分岐は設けない。教員の設定負担を抑え、成功条件を満たすまで現在の手順で待機する。
- 各手順の条件は、その手順へ入ってからそれぞれ一度成功すると達成済みになる。全条件の達成後に次へ進める。
- Startの出力は1本、Stepの出力は1本以上。複数の接続先は成功後に学習者が選ぶ進路になる。自動的な条件分岐や並列実行ではない。
- StepとEndは合流を受け付ける。選ばなかった経路の完了を待たない。循環、孤立した手順、終了に達しない経路、重複接続を禁止する。

### 編集プロジェクト

- `.skillsync.json` は `persistentDataPath/Projects` に保存する。projectのschemaVersionは6、Curriculumは5。
- v1〜v4の既存プロジェクトを読み込める。既存の一本道の接続・条件を保持する。将来のバージョンは拒否する。
- 編集途中のプロジェクト保存と、検証済み教材のJSON出力を区別する。

### 配布用JSON version 6

| 項目 | 意味 |
|---|---|
| `version` | 6。対応バージョンを必ず確認する。v6は配置ルート内の部品状態を持つ |
| `progression` | `AllConditionsThenChooseNext` |
| `startActionId` | 最初に実行する手順ID |
| `requiredActions[].id` | 出力内で一意の手順ID |
| `requiredActions[].sourceNodeId` | 編集元の手順ノードID |
| `requiredActions[].nextActionIds` | 次の手順ID。1件ならその進路、複数なら選択。`$end` は終了 |
| `requiredActions[].conditions` | その手順で達成する全条件 |
| `objects[]` | 配置ルートID、typeId、位置、回転、スケール、sourceNodePath、sourceSignature、parts |
| `models[]` | 使用するtypeIdと素材の受渡し方法 |

`requiredActions` の配列順は表示用の順序であり、実行順の正本は接続IDである。

`objects[]` は生成する配置ルートのみ。`sourceNodePath`がある場合は、モデル内の指定部分木を1回だけ生成する。`parts[]`はその中に既に存在する子ノードへの対応であり、typeIdのPrefabを部品ごとに生成してはいけない。`nodePath`は各階層の子番号とエスケープした元名を持ち、名前と構造を照合してからlocal Transform・active状態と永続IDを復元する。元モデル変更は`sourceSignature`で検出する（構造・Transform・Mesh概要の署名であり、素材ファイル全体のハッシュではない）。`ImportedModelParts.Resolve`、`ValidateSource`、`Restore`が共通処理。条件のA/BはルートIDに加えて`parts[].id`を参照できる。編集用のhidden/lockedと、削除を表すactive=falseは区別する。実行アプリはこのv6生成契約への対応が必要。

### 成功条件

| type | 対象 | 判定・パラメーター |
|---|---|---|
| `Proximity` | A/B | ワールド原点間距離が `distanceMeters` 以下 |
| `SnapHold` | A/B | 上記の状態を `holdSeconds` 秒連続して保持。範囲外で保持時間をリセット |
| `Push` | Aのみ | 手順開始後に対象への「押す」操作カウンターが増加 |
| `Pull` | Aのみ | 手順開始後に対象への「引く」操作カウンターが増加 |
| `Turn` | Aのみ | 操作軸まわりの開始時からの符号付き回転量の絶対値が `angleDegrees` 以上。既定90度、入力範囲0.1〜36000度 |
| `Separation` | A/B | ワールド原点間距離が `distanceMeters` 以上 |
| `RotationMatch` | A/B | ワールド回転のQuaternion角度差が `angleDegrees` 以下の状態を `holdSeconds` 秒保持 |
| `Grabbed` | Aのみ | Aがつかまれている |
| `Released` | Aのみ | この手順中につかまれたAが手放された。初期状態が未把持なだけでは達成しない |

距離はメートル、角度は度。必要な数値は `parameters` に保存する。二つ目の対象が不要な条件はBを表示・検証・出力の対象にしない。向きをそろえる条件だけでは取り付け位置を判定しないため、位置も必要なときは近接条件を併用する。

新規の選択肢は「近づける」「近づけて保持」「押す」「引く」「もつ」（Grabbed）「回す」の6種類。Separation / RotationMatch / Releasedは既存データの編集・判定互換のため保持し、新規の候補には表示しない。旧条件を新条件へ自動変換しない。

対象候補は幅を広げ、全文を折り返し、スクロール感度を240画面ピクセル／入力単位に設定する（実際の1刻みの量は入力デバイスによる）。候補および選択済み欄へのホバーでは全文とオレンジの形状輪郭を一時表示する。輪郭は対象のMeshのみを深度なしのマスクに描き、遮蔽物越しに表示する。編集選択・元マテリアル・保存状態は変更しない。

### モデルの保存と同梱

- 追加モデルは `persistentDataPath/ImportedModels/<ID>/model.json` と `payload/` に保存する。登録したtypeIdを再起動後も維持する。
- GLB/glTFの外部buffer・画像は、モデルと同じディレクトリ配下の参照ファイルだけをコピーする。外部URLや親ディレクトリの参照は、理由を表示して保存を中止する。GLBへまとめるか素材をモデル配下に配置する。
- 配布用JSONは `persistentDataPath/Exports`。GLB/glTFはJSONと同じ場所に作る専用assetsフォルダーへ同梱し、`models[].uri` でJSONからの相対パスを示す。JSONとassetsフォルダーを一緒に渡す。
- 既定モデルとEditorで取り込んだFBXは `requiresPreinstalledPrefab=true`。実行アプリ側でtypeIdに対応するPrefabを用意する。FBXをPC Playerで直接取り込む機能はない。
- カタログから非表示にした追加モデルも、既存教材の読込に必要なため保存データとtypeId登録を保持する。
- 出力済みassetsフォルダーはバックアップJSONからも参照されるため自動上書き・削除しない。

### 実行アプリとの接続

`ScenarioPlaybackSession` と `ScenarioConditionEvaluator` は判定と進行の共通処理。実行アプリのアダプターが `IScenarioObjectStateSource` に配置IDごとのワールド位置・回転・把持状態を供給し、毎フレーム `Tick(source, deltaSeconds)` を呼ぶ。`CanAdvance` がtrueになったら、接続先が1件なら `TryAdvance()`、複数なら選ばれたIDで `TryAdvance(id)` を呼ぶ。

VR入力やモデルのインスタンス生成は実行アプリ側の担当。Quest上の動作と既存VRアプリへの組込みは別途確認する。エディタのプレビューは成功を模擬して接続を確認する機能であり、実機の条件判定試験を代替しない。

押す・引く・回すには追加の `IScenarioInteractionStateSource` を実装する。pushCount/pullCountは対象IDごとの意図的な操作1回につき1増加、rotationDegreesは操作軸まわりの符号付き連続角（360度で折り返さず、親の移動回転を除く）。往復回転は相殺し、時計回り・反時計回りのどちらも受け付ける。カウンターの初期化時にはepochを変更する。物理的な操作認識・軸の決定は実行側の責務であり、単なる近接を押す・引くと見なさない。未接続・対象欠落では成功しない。

操作開始前の状態を `new ScenarioPlaybackSession(scenario, source)` に渡す。省略時は最初のTickを基準サンプルとするため、操作前に呼ぶ。後続手順は遷移時に最新のsourceから基準を採取する。参照欠落やepoch変更時は再基準化し、それ自体を成功扱いしない。新しいtypeを使うv6教材には、この契約に対応した実行アプリが必要。

### 確認方法

Unityの `Tools/Automation/Apply Authoring Features` は既存UIRoot Prefabへ必要なUIを追加する。全体の再生成では `Build UI Prefabs` も同じ追加処理を使う。`Tools/Automation/Check Authoring Logic` は分岐と成功条件のロジックを確認する。Unityの起動・実行はユーザーが行う。
