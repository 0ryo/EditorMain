# SkillSync シナリオ作成機能 仕様書

## 0. スコープ

### 0.1 目的

* プログラミング知識のない指導者が、PCエディタ上で **手順（Step）** と **成功条件（Condition）** をノードで定義し、Quest側で **自動進行**できる教材を作れるようにする。
* 対象題材は **タイヤ交換**（研究スコープ）。

### 0.2 MVPでやること（In Scope）

* 線形手順（分岐なし）を **Start→Step…→End** で構築
* Stepごとに **1〜3個の条件**（AND）を持たせる
* 条件は **「AをBに近づける」**（選択式）で表現
* 条件達成：**スナップ吸着状態が1.0秒維持**
* Step完了：条件が全て達成されたら **自動で次Stepへ**
* Step完了は確定（巻き戻しなし）
* 参照切れ（配置オブジェクト削除等）は **未設定に戻す**＋**エクスポート禁止**
* JSON出力：既存 `requiredActions` を **拡張**して条件を持たせる
* ノード座標は保存せず、読み込み時に **自動整列**

### 0.3 MVPでやらないこと（Out of Scope）

* 分岐（IF/ELSE）、OR/NOT
* 条件タイプ追加（掴む/回す/使う 等）
* 教師による文言編集（自由文）
* スコア、ログ詳細、誤操作判定、難易度分岐
* SnapPointの任意配置UI（教師操作）

---

## 1. 用語定義

* **Step（手順）**：教材進行の単位。順序を持ち、完了すると次へ進む。
* **Condition（条件）**：Stepを完了するために達成すべき要素。MVPでは「SnapHold」のみ。
* **Stepノード**：複数Conditionを束ねる“器”。Step順序のチェーンを構成する。
* **Conditionノード**：1ノード＝1条件。「AをBに近づける」のみ。
* **スナップ吸着状態**：オブジェクトAが、ターゲットBに吸着している状態。MVPでは **Aの currentSnapTargetId == B.id** を吸着と定義（1対1）。
* **SnapPoint**：吸着判定/吸着位置の基準点。MVPでは **Transform pivot（local origin）**。

---

## 2. ユーザー（指導者）体験フロー

1. 左のオブジェクト一覧から選択してワールドに配置（既存機能）
2. シナリオ画面でノード作成（既存機能：ノード追加・接続）
3. Conditionノードで A/B をドロップダウンから選択（既存機能）
4. Stepノードに複数Conditionノードを接続してAND構成
5. Stepノード同士を線形に接続し、Start→…→Endを完成
6. バリデーションOKならエクスポート（JSON）

---

## 3. UI仕様（ノードエディタ）

### 3.1 ノード種別

#### (1) Startノード（必須）

* 入力：なし
* 出力：Stepへ1本のみ
* 役割：開始点

#### (2) Endノード（必須）

* 入力：Stepから1本のみ
* 出力：なし
* 役割：終了点

#### (3) Stepノード（必須・複数）

* 表示：

  * `STEP {index}`（indexはStartからの順序で自動）
  * 任意で「条件達成数/総数」（例：`0/2`）※表示有無は任意（ロジックは必要）
* 接続：

  * **Step順序接続**：前Step→次Step（各Stepは次へ1本のみ）
  * **Condition束ね接続**：Condition→Step（最大3本）

#### (4) Conditionノード（複数）

* 表示（固定テンプレ）：

  * `"{A}" を "{B}" に近づける`
  * A/Bが未設定なら `"{未設定}" を "{未設定}" に近づける`
* 入力UI：

  * Dropdown A：配置インスタンスID一覧
  * Dropdown B：配置インスタンスID一覧
* 接続：

  * 出力：Stepへ接続（**必ず1つのStepに紐づく**）
  * Start/Endへ接続は禁止

---

### 3.2 接続ルール（編集段階で制限する）

#### Step順序（Start/Step/End）

* Startの出力：**1本のみ**（最初のStepへ）
* Stepの出力（次Step）：**0または1本**

  * 中間Step：1本必須
  * 最終Step：Endへ1本必須（または出力先がEnd）
* Endの入力：**1本のみ**
* Cycle（循環）禁止
* 分岐禁止（Stepが次を2本以上持つのは禁止）

#### Condition束ね（AND）

* Conditionは **必ず1つのStepに接続**
* Stepが受け取れるConditionは **最大3**
* StepのCondition数は **最小1（0は禁止）**
* 同一Step内で同一条件（同じA,B,type）が重複した場合はエラー（推奨）

---

### 3.3 表示テキスト（Quest側表示）

* 各Stepの表示は **条件文を全て表示（複数行）**

  * Stepに2条件なら2行
  * Stepに3条件なら3行
* 文言は固定テンプレで生成し、教師編集は不可（MVP）

---

## 4. バリデーション仕様（エクスポート可否）

### 4.1 エラー（Export禁止）

**E-01** Startノードが存在しない / 複数ある
**E-02** Endノードが存在しない / 複数ある
**E-03** Start→最初のStepが未接続
**E-04** Step順序が線形でない（分岐・循環・途中欠落）
**E-05** Endが最終Stepから未接続
**E-06** StepのCondition数が 0 または 4以上
**E-07** ConditionがStepに接続されていない / 複数Stepに接続されている
**E-08** ConditionのAまたはBが未設定（参照切れ含む）
**E-09** Conditionで A == B（自己参照）※禁止にする場合（推奨）
**E-10** 参照オブジェクトがシーンに存在しない（削除済み）
**E-11** JSON生成に必要なID（object.id等）が欠落

> 参照切れの挙動は仕様固定：削除されたら Conditionの該当フィールドを **未設定** に戻し、E-08でエクスポート不可にする。

### 4.2 警告（Exportは可能だが注意）

※MVPでは警告は最小でも良い。例：

* **W-01** あるStepのConditionが3個（複雑）
* **W-02** 同じAが複数Stepで頻繁に要求される（進行が詰まりやすい）

---

## 5. データモデル（内部表現）

### 5.1 Graph（メモリ上）

最低限、以下が取れればOK（すでにノード/接続は実装済みとのことなので、それに合わせて）

* `Node`

  * `nodeId: string`（GUID推奨）
  * `nodeType: Start | End | Step | Condition`
* `Edge`

  * `fromNodeId: string`
  * `toNodeId: string`
  * `edgeType: StepFlow | ConditionBind`

    * StepFlow：Start/Step→Step/End
    * ConditionBind：Condition→Step
* `ConditionNodeData`

  * `objectAId: string|null`
  * `objectBId: string|null`
* `StepNodeData`

  * （座標は保存しないが、作業中はUI用に持って良い）
  * 条件はEdgeから集計する（単一ソース化）

---

## 6. JSON仕様（requiredActions拡張）

### 6.1 方針

* 既存スキーマ `requiredActions: [{id,name}]` を拡張し、各手順に `conditions` を追加する。
* **後方互換**：

  * 旧データ：`conditions` が存在しない場合は “シナリオ未定義” として扱う（インポート時にエラー or 編集促進）
* `version` は、実装方針として次のどちらかに統一する（推奨はB）：

  * A) `version`は据え置き（1のまま）で optional field として追加
  * B) `version`を **2** に上げる（Questローダーが厳格型ならこちらが安全）

この仕様書では **B（version=2）** を推奨案として記述します（必要ならAに変更可能）。

### 6.2 スキーマ（提案：version 2）

```json
{
  "version": 2,
  "meta": { "...": "既存のまま" },

  "scenarioSettings": {
    "holdSeconds": 1.0,
    "snapDistance_m": 0.1
  },

  "requiredActions": [
    {
      "id": "act-001",
      "name": "STEP 1",
      "conditions": [
        {
          "type": "SnapHold",
          "aObjectId": "obj-200",
          "bObjectId": "obj-100",
          "holdSeconds": 1.0
        },
        {
          "type": "SnapHold",
          "aObjectId": "obj-201",
          "bObjectId": "obj-101",
          "holdSeconds": 1.0
        }
      ]
    }
  ],

  "objects": [ "...": "既存のまま" ]
}
```

#### フィールド定義

* `scenarioSettings.holdSeconds`：MVP固定 1.0
* `scenarioSettings.snapDistance_m`：仮置き 0.1（スナップ検出に距離を使う場合の共有値）
* `requiredActions[i].id`：安定ID（自動採番 or GUID）
* `requiredActions[i].name`：表示名（MVPでは `STEP n` で自動付与）
* `requiredActions[i].conditions`：Step内のAND条件リスト（順不同）

#### Condition（MVPでは SnapHold のみ）

* `type`：`"SnapHold"`
* `aObjectId`：動作側（ツール等）
* `bObjectId`：ターゲット側（ナット等）
* `holdSeconds`：固定 1.0（将来拡張を見越して残す）

---

## 7. エクスポート生成仕様（Graph → JSON）

### 7.1 手順順序の確定

1. Startノードを取得（1つであること）
2. StepFlowエッジを辿って線形列を構築

   * `Start -> Step1 -> Step2 -> ... -> End`
3. Step列のindexを決定（1始まり）

### 7.2 Stepごとの conditions 生成

各Stepについて：

1. `ConditionBind` の入力エッジで接続されたConditionノードを取得
2. 各Conditionノードについて：

   * `objectAId`, `objectBId` を読む
   * 未設定/nullはエラー（E-08）
3. `conditions` 配列を生成（並び順は **任意**。ただし安定化のため `nodeId` 昇順などでソート推奨）
4. Stepの `name` は `STEP {index}` に自動設定

### 7.3 参照切れ処理

* オブジェクト削除イベントを受けたら：

  * 該当Conditionノードの `objectAId` / `objectBId` を null にする
  * UI表示を未設定に更新
* エクスポート時に null があれば禁止

---

## 8. Quest側ランタイム契約（評価ロジック）

### 8.1 スナップ状態モデル（MVP固定）

* 各配置オブジェクトはランタイムで以下を持つ：

  * `objectId: string`
  * `currentSnapTargetId: string|null`（1対1）
* スナップイベント：

  * `OnSnapEnter(aId, bId)`：aがbに吸着開始 → `a.currentSnapTargetId = bId`
  * `OnSnapExit(aId, bId)`：吸着解除 → `a.currentSnapTargetId = null`（bId一致時）

### 8.2 条件達成（SnapHold）

* 条件 `c` が「成立している」とは：

  * `a.currentSnapTargetId == bObjectId`
* 条件達成は：

  * 成立状態が **holdSeconds（1.0s）** 継続したら `c.satisfied = true`
  * 継続途中で崩れたらタイマーリセット（未達のまま）

### 8.3 Step進行（自動）

* `currentStepIndex` を持つ（0始まり）
* Step開始時に、そのStep内の `c.satisfied=false` を初期化
* 全条件 `satisfied=true` になった瞬間に Step完了 → `currentStepIndex++`
* 完了したStepは確定（巻き戻しなし）

### 8.4 表示（MVP）

* 現在Stepの条件文を **全行表示**
* 文は固定テンプレ：

  * `"{aObjectId} を {bObjectId} に近づける"`
* 表示更新タイミング：

  * Stepが進んだ時

### 8.5 擬似コード（参考）

```csharp
void Update() {
  var step = requiredActions[currentStepIndex];
  foreach (var cond in step.conditions) {
    if (cond.satisfied) continue;

    bool isHeld = (snapState[cond.aObjectId].currentTarget == cond.bObjectId);

    if (isHeld) cond.holdTimer += Time.deltaTime;
    else cond.holdTimer = 0f;

    if (cond.holdTimer >= cond.holdSeconds) cond.satisfied = true;
  }

  if (step.conditions.All(c => c.satisfied)) {
    currentStepIndex++;
    EnterStep(currentStepIndex);
  }
}
```

---

## 9. SnapPoint仕様（Editor/Quest共通ルール）

* SnapPointは **Transform pivot（local origin）**
* 教師操作は不要：

  * オブジェクトを追加した時点でSnapPointは「そのオブジェクトのpivot」として成立する
* 重要制約（制作ルール）：

  * タイヤ交換で使用するPrefabは、**pivotが意味のある位置**（スナップしたい基準点）に置かれていること
  * ここが崩れるとシナリオが成立しないので、**アセット制作要件**として明記する

---

## 10. 既存実装との結合点（あなたの現状に合わせた「残タスク」）

実装状況（ノード追加/接続/ドロップダウン表示まで実装済み）から、残りの主作業は次の4つに集約されます✅

1. **Stepノード導入**

   * Stepノード追加UI
   * StepFlow接続の制限（分岐禁止、End必須等）
2. **Graphバリデーション実装**

   * E-01〜E-11の検出
   * エクスポートボタンのDisable + エラー一覧表示
3. **Graph→requiredActions 変換**

   * Startから線形走査してStep列化
   * ConditionBindを集計してconditions生成
4. **JSONスキーマ拡張**

   * `scenarioSettings` 追加
   * `requiredActions[].conditions[]` 追加
   * version運用（推奨：2）

---

## 11. テスト観点（最低限）

* 正常系

  * Step 1条件 / 2条件 / 3条件でエクスポートできる
  * 読み込み→自動整列→再エクスポートで同等JSONになる（論理等価）
* 異常系（エクスポート禁止）

  * Startなし / Endなし
  * Step分岐（次が2本）
  * Step条件0個 / 4個
  * ConditionがStep未接続
  * A/B未設定
  * 参照オブジェクト削除→未設定化→禁止
* ランタイム契約

  * SnapEnter後、1.0s未満で解除→未達
  * 1.0s維持→達成
  * 全条件達成→Step自動進行
