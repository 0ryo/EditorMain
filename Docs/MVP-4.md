# MVP-3（シナリオ作成機能：ノードベース + curriculum.json）

status: 未着手（MVP-1完了後に着手）
goal: Unity知識がなくても「手順」と「近接条件」と「手順の接続（分岐/合流）」を直感的に編集し、VR側でそのまま進行制御できる curriculum.json を出力する

---

## 0. 用語定義

- シナリオ: 教材の開始から終了までの「全手順」を指す（1教材=1シナリオ）
- 手順（Step）: タイトルを持ち、条件を満たすと完了する単位
- 条件（Condition）: MVP-3では「オブジェクト同士が一定距離以内」が唯一の条件
- 接続（Edge）: Stepの完了後に、次のStepをアクティブ化するための有向リンク
- 合流（Join）: あるStepが複数の親Stepを持つ状態。開始には「全親が完了」が必要

---

## 1. 仕様（MVP-3の確定事項）

### 1-1. 条件
- 条件タイプ: Proximity のみ
- 判定方式: オブジェクト中心距離
- 距離しきい値: 固定（例: 0.5m）
- キープ時間: 固定（1.0秒）
- 複合条件: ANDのみ
  - 例: (A-B近接) AND (C-D近接) を1秒キープで完了

### 1-2. 進行（ゲート概念の実装）
- 手順完了はラッチ（完了後に条件が外れても未完了に戻らない）
- 手順の開始は自動
  - 開始条件: 親Step（incoming edge）の全てが完了
  - 親を持たないStepは開始Stepとして最初からアクティブ
- 分岐: 1つのStepから複数の次Stepへ接続可能
- 合流: 複数の親Stepが1つの子Stepに接続可能（全親完了が必要）

### 1-3. 参照切れ
- 参照しているPlacedObjectが削除された場合:
  - 該当条件の参照を null に戻す（未設定扱い）
  - Step自体は残し、⚠表示する

### 1-4. 出力ファイル
- 配置情報JSONとは別ファイル
- ファイル名: 「プロジェクト名-curriculum.json」
- 出力先: `Assets/Exports/`（存在しない場合は作成）

---

## 2. Unity UI（下部常設）

### 2-1. 画面構成
- 既存: 左にカタログ、中央に3Dビュー
- 追加: 画面下部に「Panel_ScenarioGraph」を常設

### 2-2. ノード（Stepカード）UI
各ノードは以下を持つ:
- 手順番号（表示用） + タイトル（InputField）
- 条件リスト（行の追加/削除）
  - 行: Dropdown(A) + 「を」 + Dropdown(B) + 「に近づけたら」
- 左に入力コネクタ（白丸）
- 右に出力コネクタ（白丸）
- ⚠（参照切れや未設定条件がある場合に表示）

### 2-3. 接続操作（MVP方式）
- 出力コネクタをクリックすると「接続モード」
- 接続モード中に、別ノードの入力コネクタをクリックすると Edge 作成
- 既存Edgeを削除できる（後述の最小仕様）

※ドラッグ接続やベジェ曲線はMVP外（線は直線でOK）

---

## 3. 実装タスク

### 3-1. データモデル（Curriculum）
- Steps（配列）
- 各Step:
  - id（step-0001）
  - title
  - description（オプション、MVPでは入力欄は後回しでも可）
  - conditions（ProximityPair配列）
  - nextStepIds（接続先のStepId配列）

### 3-2. Graph編集（追加/削除/接続）
- Step追加（+）
- Step削除（対象Stepおよび関連Edge削除）
- Edge追加（クリック接続）
- Edge削除（MVPでは「選択中Stepのnext一覧から×で削除」方式でOK）

### 3-3. オブジェクト一覧（Dropdown用）
- シーン内のPlacedObjectを列挙
- 表示: `typeId`（MVPはこれだけ。将来Renameで表示を変える）

### 3-4. curriculum.json出力
- 保存ボタンで `Assets/Exports/<ProjectName>-curriculum.json` を生成
- 参照切れ/未設定がある場合:
  - 保存はできるが警告を表示（または保存ボタンを無効化。MVPでは警告のみでOK）

---

## 4. Definition of Done（完了条件）

- [ ] 下部にノードUIが常設表示される
- [ ] Stepを追加/削除できる
- [ ] Step内でProximity条件を複数（AND）設定できる
- [ ] Dropdownはシーン内PlacedObject(typeId)から選べる
- [ ] ノード間を接続できる（分岐/合流対応）
- [ ] prerequisites（親全完了）が満たされたStepがアクティブになるロジックをcurriculumに持てる
- [ ] プロジェクト名-curriculum.json を Assets/Exports に出力できる
- [ ] 参照切れ時に条件が未設定へ戻り、⚠が表示される
```

---

# Unity 実装（貼って動く最小セット）

> 前提: MVP-1で `PlacedObject { id, typeId }` が存在している状態。

## 1) データモデル `CurriculumModel.cs`

```csharp
using System;
using System.Collections.Generic;

[Serializable]
public class Curriculum {
    public int version = 1;
    public string projectName = "VRCourseEditor";
    public string mode = "Graph"; // 将来 "Set" など拡張余地
    public RuleSet rules = new RuleSet();
    public List<StepNode> steps = new List<StepNode>();
}

[Serializable]
public class RuleSet {
    public float proximityDistance = 0.5f; // 固定
    public float holdSeconds = 1.0f;       // 固定
}

[Serializable]
public class StepNode {
    public string id;            // "step-0001"
    public string title = "タイトル";
    public string description = ""; // MVPでは未使用でも可

    public List<ProximityPair> conditions = new List<ProximityPair>(); // AND
    public List<string> nextStepIds = new List<string>();             // 出力エッジ
}

[Serializable]
public class ProximityPair {
    public string aObjectId; // PlacedObject.id
    public string bObjectId; // PlacedObject.id
}
```

## 2) グラフ操作 `CurriculumGraphService.cs`

```csharp
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class CurriculumGraphService : MonoBehaviour {
    public Curriculum curriculum = new Curriculum();

    int stepSeq = 0;

    public StepNode AddStep() {
        var s = new StepNode {
            id = "step-" + (++stepSeq).ToString("D4"),
            title = "手順 " + stepSeq
        };
        // 最低1条件行を用意（未設定）
        s.conditions.Add(new ProximityPair { aObjectId = null, bObjectId = null });
        curriculum.steps.Add(s);
        return s;
    }

    public void RemoveStep(string stepId) {
        // step本体削除
        curriculum.steps.RemoveAll(s => s.id == stepId);
        // 参照しているnextを除去
        foreach (var s in curriculum.steps) {
            s.nextStepIds.RemoveAll(n => n == stepId);
        }
    }

    public StepNode FindStep(string stepId) {
        return curriculum.steps.FirstOrDefault(s => s.id == stepId);
    }

    public void AddEdge(string fromStepId, string toStepId) {
        if (fromStepId == toStepId) return;
        var from = FindStep(fromStepId);
        var to = FindStep(toStepId);
        if (from == null || to == null) return;
        if (!from.nextStepIds.Contains(toStepId)) from.nextStepIds.Add(toStepId);
    }

    public void RemoveEdge(string fromStepId, string toStepId) {
        var from = FindStep(fromStepId);
        if (from == null) return;
        from.nextStepIds.RemoveAll(n => n == toStepId);
    }

    // 合流のため: incoming(親)一覧を導出
    public List<string> GetParents(string stepId) {
        var parents = new List<string>();
        foreach (var s in curriculum.steps) {
            if (s.nextStepIds.Contains(stepId)) parents.Add(s.id);
        }
        return parents;
    }

    // 参照切れを検出し、MVP仕様どおり「参照を空に戻す」
    public bool RepairBrokenReferences() {
        var placed = FindObjectsOfType<PlacedObject>().Select(p => p.id).ToHashSet();
        bool changed = false;

        foreach (var step in curriculum.steps) {
            foreach (var c in step.conditions) {
                if (!string.IsNullOrEmpty(c.aObjectId) && !placed.Contains(c.aObjectId)) { c.aObjectId = null; changed = true; }
                if (!string.IsNullOrEmpty(c.bObjectId) && !placed.Contains(c.bObjectId)) { c.bObjectId = null; changed = true; }
            }
        }
        return changed;
    }

    public bool HasUnconfiguredConditions(StepNode step) {
        return step.conditions.Any(c => string.IsNullOrEmpty(c.aObjectId) || string.IsNullOrEmpty(c.bObjectId));
    }
}
```

## 3) Dropdown用のPlacedObject列挙 `PlacedObjectOptionProvider.cs`

```csharp
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class PlacedObjectOptionProvider {
    public struct Option {
        public string id;
        public string label; // MVPでは typeId 表示
    }

    public static List<Option> GetOptions() {
        var list = new List<Option>();
        foreach (var p in Object.FindObjectsOfType<PlacedObject>()) {
            // MVP: 表示はtypeIdだけ（将来 rename で差し替え可能）
            list.Add(new Option { id = p.id, label = p.typeId });
        }
        // 安定表示のためソート
        return list.OrderBy(o => o.label).ToList();
    }
}
```

## 4) ノードUI（カード） `StepNodeUI.cs`

> これは「1ノードの見た目とイベント」をまとめる部品です。

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class StepNodeUI : MonoBehaviour {
    [Header("Basic")]
    public Text stepIdText;
    public InputField titleInput;
    public GameObject warningIcon;

    [Header("Connectors")]
    public Button inputConnector;
    public Button outputConnector;

    [Header("Conditions")]
    public RectTransform conditionListRoot;
    public ConditionRowUI conditionRowTemplate;
    public Button addConditionButton;

    // 外から注入
    StepNode step;
    CurriculumGraphService graph;

    public Action<string> onClickInputConnector;
    public Action<string> onClickOutputConnector;
    public Action onChanged;

    readonly List<ConditionRowUI> rows = new List<ConditionRowUI>();

    public void Bind(CurriculumGraphService graphService, StepNode s) {
        graph = graphService;
        step = s;

        stepIdText.text = s.id;
        titleInput.SetTextWithoutNotify(s.title);
        titleInput.onEndEdit.RemoveAllListeners();
        titleInput.onEndEdit.AddListener(v => { step.title = v; onChanged?.Invoke(); });

        inputConnector.onClick.RemoveAllListeners();
        outputConnector.onClick.RemoveAllListeners();
        inputConnector.onClick.AddListener(() => onClickInputConnector?.Invoke(step.id));
        outputConnector.onClick.AddListener(() => onClickOutputConnector?.Invoke(step.id));

        addConditionButton.onClick.RemoveAllListeners();
        addConditionButton.onClick.AddListener(() => {
            step.conditions.Add(new ProximityPair { aObjectId = null, bObjectId = null });
            RebuildConditions();
            onChanged?.Invoke();
        });

        RebuildConditions();
        RefreshWarning();
    }

    public void RefreshWarning() {
        bool warn = graph.HasUnconfiguredConditions(step);
        if (warningIcon != null) warningIcon.SetActive(warn);
    }

    void RebuildConditions() {
        foreach (Transform c in conditionListRoot) Destroy(c.gameObject);
        rows.Clear();

        var options = PlacedObjectOptionProvider.GetOptions();

        for (int i = 0; i < step.conditions.Count; i++) {
            int idx = i;
            var row = Instantiate(conditionRowTemplate, conditionListRoot);
            row.gameObject.SetActive(true);

            row.Bind(
                options,
                step.conditions[idx].aObjectId,
                step.conditions[idx].bObjectId,
                onAChanged: (newId) => { step.conditions[idx].aObjectId = newId; RefreshWarning(); onChanged?.Invoke(); },
                onBChanged: (newId) => { step.conditions[idx].bObjectId = newId; RefreshWarning(); onChanged?.Invoke(); },
                onRemove: () => {
                    // 1行は最低残す
                    if (step.conditions.Count <= 1) {
                        step.conditions[idx].aObjectId = null;
                        step.conditions[idx].bObjectId = null;
                    } else {
                        step.conditions.RemoveAt(idx);
                    }
                    RebuildConditions();
                    RefreshWarning();
                    onChanged?.Invoke();
                }
            );

            rows.Add(row);
        }
    }
}
```

## 5) 条件行UI `ConditionRowUI.cs`

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ConditionRowUI : MonoBehaviour {
    public Dropdown dropdownA;
    public Dropdown dropdownB;
    public Button removeButton;

    public void Bind(
        List<PlacedObjectOptionProvider.Option> options,
        string currentAId,
        string currentBId,
        Action<string> onAChanged,
        Action<string> onBChanged,
        Action onRemove
    ) {
        // 選択肢: 先頭に "未設定"
        var labels = new List<string> { "未設定" };
        foreach (var o in options) labels.Add(o.label);

        dropdownA.ClearOptions();
        dropdownB.ClearOptions();
        dropdownA.AddOptions(labels);
        dropdownB.AddOptions(labels);

        dropdownA.onValueChanged.RemoveAllListeners();
        dropdownB.onValueChanged.RemoveAllListeners();

        dropdownA.value = IdToIndex(options, currentAId);
        dropdownB.value = IdToIndex(options, currentBId);

        dropdownA.onValueChanged.AddListener(v => onAChanged(IndexToId(options, v)));
        dropdownB.onValueChanged.AddListener(v => onBChanged(IndexToId(options, v)));

        removeButton.onClick.RemoveAllListeners();
        removeButton.onClick.AddListener(() => onRemove?.Invoke());
    }

    int IdToIndex(List<PlacedObjectOptionProvider.Option> options, string id) {
        if (string.IsNullOrEmpty(id)) return 0;
        for (int i = 0; i < options.Count; i++) {
            if (options[i].id == id) return i + 1; // 0が未設定
        }
        return 0;
    }

    string IndexToId(List<PlacedObjectOptionProvider.Option> options, int index) {
        if (index <= 0) return null;
        int i = index - 1;
        if (i < 0 || i >= options.Count) return null;
        return options[i].id;
    }
}
```

## 6) 接続線（UI上に直線） `ConnectionLineGraphic.cs`

> 最小の「線を引くGraphic」。ベジェ曲線はMVP外。直線でOK。

```csharp
using UnityEngine;
using UnityEngine.UI;

public class ConnectionLineGraphic : Graphic {
    public RectTransform from;
    public RectTransform to;
    public float thickness = 3f;

    protected override void OnPopulateMesh(VertexHelper vh) {
        vh.Clear();
        if (from == null || to == null) return;

        Vector2 a = WorldToLocalCenter(from);
        Vector2 b = WorldToLocalCenter(to);

        Vector2 dir = (b - a).normalized;
        Vector2 n = new Vector2(-dir.y, dir.x) * (thickness * 0.5f);

        // Quad: a-n, a+n, b+n, b-n
        AddQuad(vh, a - n, a + n, b + n, b - n);
    }

    Vector2 WorldToLocalCenter(RectTransform rt) {
        Vector3 world = rt.TransformPoint(rt.rect.center);
        Vector2 local = rectTransform.InverseTransformPoint(world);
        return local;
    }

    void AddQuad(VertexHelper vh, Vector2 v0, Vector2 v1, Vector2 v2, Vector2 v3) {
        int i = vh.currentVertCount;
        UIVertex vert = UIVertex.simpleVert;
        vert.color = color;

        vert.position = v0; vh.AddVert(vert);
        vert.position = v1; vh.AddVert(vert);
        vert.position = v2; vh.AddVert(vert);
        vert.position = v3; vh.AddVert(vert);

        vh.AddTriangle(i, i + 1, i + 2);
        vh.AddTriangle(i, i + 2, i + 3);
    }

    void Update() {
        // 位置更新のために再描画
        SetVerticesDirty();
    }
}
```

## 7) グラフUI（下部常設・ノード生成・クリック接続） `ScenarioGraphUI.cs`

```csharp
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

public class ScenarioGraphUI : MonoBehaviour {
    [Header("Services")]
    public CurriculumGraphService graph;

    [Header("Top Controls")]
    public InputField projectNameInput;
    public Button addStepButton;
    public Button saveButton;
    public Text statusText;

    [Header("Nodes")]
    public RectTransform nodeArea;       // ノードを置く領域
    public StepNodeUI nodeTemplate;

    [Header("Lines")]
    public RectTransform lineLayer;      // nodeAreaの上に重ねる全画面レイヤ
    public ConnectionLineGraphic lineTemplate;

    // 接続モード
    string linkingFromStepId = null;

    // 生成済みUI参照
    readonly Dictionary<string, StepNodeUI> nodeUIs = new Dictionary<string, StepNodeUI>();
    readonly List<ConnectionLineGraphic> lines = new List<ConnectionLineGraphic>();

    void Start() {
        if (graph.curriculum.steps.Count == 0) graph.AddStep();

        projectNameInput.SetTextWithoutNotify(graph.curriculum.projectName);

        addStepButton.onClick.AddListener(() => {
            graph.AddStep();
            RebuildAll();
        });

        saveButton.onClick.AddListener(() => SaveCurriculum());

        RebuildAll();
    }

    void RebuildAll() {
        // 参照切れ修復
        graph.RepairBrokenReferences();

        // Nodes
        foreach (Transform c in nodeArea) Destroy(c.gameObject);
        nodeUIs.Clear();

        float x = 60f, y = -20f;
        foreach (var s in graph.curriculum.steps) {
            var ui = Instantiate(nodeTemplate, nodeArea);
            ui.gameObject.SetActive(true);

            // 雑に並べる（MVP: 後でドラッグ移動等を追加）
            var rt = ui.GetComponent<RectTransform>();
            rt.anchoredPosition = new Vector2(x, y);
            y -= 150f;
            if (y < -400f) { y = -20f; x += 420f; }

            ui.Bind(graph, s);
            ui.onClickOutputConnector = OnClickOutput;
            ui.onClickInputConnector = OnClickInput;
            ui.onChanged = () => { ui.RefreshWarning(); RefreshLines(); };

            nodeUIs[s.id] = ui;
        }

        RefreshLines();
        statusText.text = linkingFromStepId == null ? "" : "接続先ノードの入力●をクリック";
    }

    void OnClickOutput(string fromStepId) {
        linkingFromStepId = fromStepId;
        statusText.text = "接続先ノードの入力●をクリック";
    }

    void OnClickInput(string toStepId) {
        if (string.IsNullOrEmpty(linkingFromStepId)) return;
        graph.AddEdge(linkingFromStepId, toStepId);
        linkingFromStepId = null;
        statusText.text = "";
        RefreshLines();
    }

    void RefreshLines() {
        // Lines clear
        foreach (var l in lines) Destroy(l.gameObject);
        lines.Clear();

        // Build edges
        foreach (var s in graph.curriculum.steps) {
            if (!nodeUIs.TryGetValue(s.id, out var fromUI)) continue;

            foreach (var toId in s.nextStepIds) {
                if (!nodeUIs.TryGetValue(toId, out var toUI)) continue;

                var line = Instantiate(lineTemplate, lineLayer);
                line.gameObject.SetActive(true);
                line.color = new Color(1f, 1f, 1f, 0.9f);

                // from: 出力コネクタ中心, to: 入力コネクタ中心
                line.from = fromUI.outputConnector.GetComponent<RectTransform>();
                line.to   = toUI.inputConnector.GetComponent<RectTransform>();
                lines.Add(line);
            }
        }
    }

    void SaveCurriculum() {
        graph.curriculum.projectName = string.IsNullOrWhiteSpace(projectNameInput.text)
            ? graph.curriculum.projectName
            : projectNameInput.text;

        // 参照切れ/未設定があっても保存は可能（警告表示）
        int warnCount = 0;
        foreach (var s in graph.curriculum.steps) {
            if (graph.HasUnconfiguredConditions(s)) warnCount++;
        }

        string dir = Path.Combine(Application.dataPath, "Exports");
        Directory.CreateDirectory(dir);

        string file = $"{graph.curriculum.projectName}-curriculum.json";
        string path = Path.Combine(dir, file);

        string json = JsonUtility.ToJson(graph.curriculum, true);
        File.WriteAllText(path, json);

        statusText.text = warnCount > 0
            ? $"保存しました（⚠未設定手順: {warnCount}）: Assets/Exports/{file}"
            : $"保存しました: Assets/Exports/{file}";

        Debug.Log(statusText.text);
    }
}
```

---

# Unity 上での作り方（uGUI配置の最短手順）

1. 既存Canvasに下部パネルを追加

* `Canvas` 右クリック → UI → Panel → `Panel_ScenarioGraph`
* Anchor: 下に固定（Stretch X / Bottom）
* 高さ: 260 くらい

2. `Panel_ScenarioGraph` の中身

* 上段: `InputField(ProjectName)`, `Button(+Step)`, `Button(Save)`, `Text(Status)`
* 中段: `NodeArea`（RectTransform）
* 最前面: `LineLayer`（RectTransform, Stretch Full, Raycast Target off推奨）

3. Nodeテンプレを作る

* `StepNodeUI` をアタッチするPanelを1つ作ってPrefab化して `nodeTemplate` に入れる
* 左に `inputConnector`（丸いボタン）、右に `outputConnector`（丸いボタン）
* `conditionRowTemplate` も同様に1行作ってPrefab化

4. `Systems` に `CurriculumGraphService` を追加
5. `Panel_ScenarioGraph` に `ScenarioGraphUI` を追加し参照を割り当てる

