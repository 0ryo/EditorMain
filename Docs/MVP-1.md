# フレームワーク設計

kickoff: 2025年7月8日
outline: Unityエディタ拡張の基盤構築、カスタムウィンドウの作成
status: 未着手

[既存ファイルの場所](https://www.notion.so/283168239bba80baa3e3cc8ba65b458e?pvs=21)

## フェーズA：ひな形 & カメラ・床・グリッド

### [フェーズのゴール]

空のシーンに「床」と「見やすいカメラ操作（回転・移動・拡大縮小）」と「簡易グリッド表示」を用意して、作業の土台を作る。

- [x]  **Unityプロジェクトを作る**
    - [x]  「**Unity Hub**」を開く → 左上「**New project**」。
    - [x]  テンプレートは「**3D (URP)**」を選択（URP＝軽量描画で動作が軽い）。
    - [x]  プロジェクト名を入力（例：`VRCourseEditor`）→ 保存先を選ぶ → **Create project**。
    - [x]  数十秒〜数分待つとUnityエディタが起動します。
- [x]  **シーンを作る・保存する**
    - [x]  上部メニュー「**File > Save As...**」→ 名前を `EditorMain` にして保存（`Assets/Scenes` を作って保存するのがオススメ）。
    - [x]  以後はこのシーンで作業します。
- [x]  **床（Floor）を作る**
    - [x]  上部メニュー「**GameObject > 3D Object > Cube**」でキューブを作成。
    - [x]  左側「**Hierarchy（階層パネル）**」で `Cube` を選択。
    - [x]  右側「**Inspector（検査パネル）**」の **Transform** で `Scale` を `X=100, Y=1, Z=100` に変更（広い床にする）。
    - [x]  名前を `Floor` に変更（Hierarchyで `Cube` をダブルクリックして名前変更）。
    - [x]  右クリック → 「**Rename**」でもOK。
- [x]  **床に専用レイヤーを付ける（後のレイキャスト用）**
    - [x]  上部メニュー「**Edit > Project Settings... > Tags and Layers**」。
    - [x]  **Layers** の空欄に `Floor` という名前で新規レイヤーを1つ作成（User Layer 8など）。
    - [x]  `Floor` オブジェクトを選択 → Inspector最上部の **Layer** ドロップダウンを `Floor` に設定 → **Yes, change children** を選択（子がいれば）。
- [x]  **簡易グリッドの見た目を用意する（最初は“床＋色分け”でOK）**
    - 初期は見た目のグリッドがなくても作業できます。
    - 可能なら床のマテリアルを作成して薄い**チェック柄**にするか、後述の「Grid線描画」を入れる。
    - [x]  「**Project（下部）**」で右クリック → **Create > Material** → 名前 `Mat_Floor`。
    - [x]  Inspectorの **Shader** を **URP > Unlit** に変更、**Base Map** の色を薄いグレーに、**Secondary Map** は使わない。
    - [x]  `Floor` にドラッグ＆ドロップで適用。
- [x]  **カメラ操作スクリプトを仕込む（オービット・パン・ズーム）**
    - [x]  「Project」右クリック → **Create > C# Script** → `EditorCameraController`。
    - [x]  `Main Camera` を選択 → Inspector下の **Add Component** → 検索で `EditorCameraController` を追加。
    - [x]  `EditorCameraController.cs` を開き、下記の最小実装を貼り付けて保存：
        
        ```
        using UnityEngine;
        
        public class EditorCameraController : MonoBehaviour {
            public float orbitSpeed = 3f;   // 右ドラッグ
            public float panSpeed   = 0.01f;// 中ドラッグ
            public float zoomSpeed  = 10f;  // ホイール
            public Transform pivot;         // 回転の中心
            private void Start() {
                if (pivot == null) {
                    var go = new GameObject("CameraPivot");
                    go.transform.position = [Vector3.zero](http://Vector3.zero);
                    pivot = go.transform;
                }
                // カメラをpivotの子にして距離をとる
                transform.parent = pivot;
                transform.localPosition = new Vector3(0, 5, -10);
                transform.LookAt(pivot.position);
            }
            void Update() {
                // ズーム（ホイール）
                float scroll = Input.GetAxis("Mouse ScrollWheel");
                transform.localPosition += transform.forward * (scroll * zoomSpeed);
        
                // オービット（右ドラッグ）
                if (Input.GetMouseButton(1)) {
                    float dx = Input.GetAxis("Mouse X") * orbitSpeed;
                    float dy = -Input.GetAxis("Mouse Y") * orbitSpeed;
                    pivot.Rotate(Vector3.up, dx, [Space.World](http://Space.World));
                    pivot.Rotate(Vector3.right, dy, Space.Self);
                }
                // パン（中ドラッグ）
                if (Input.GetMouseButton(2)) {
                    float dx = -Input.GetAxis("Mouse X");
                    float dy = -Input.GetAxis("Mouse Y");
                    Vector3 right = pivot.right;
                    Vector3 up = Vector3.up; // 水平パン
                    pivot.position += (right * dx + up * dy) * (panSpeed * Vector3.Distance(transform.position, pivot.position) * 10f);
                }
            }
        }
        ```
        
    - [x]  **Gameビュー**で実行（三角再生ボタン）。右ドラッグ＝回転、中ドラッグ＝平行移動、ホイール＝ズーム。
- [ ]  **（任意）ラインでグリッドを描く**
    - 後で良いですが、見やすさ向上のため `GridRenderer` を作って原点中心に線を描く方法もあります。
    - 実装が長くなるため、MVP-1では**床のみ**でも十分です。

---

## フェーズB：Prefabレジストリ & カタログ

### [フェーズのゴール]

「タイプ文字列ID → Prefab」を対応づける”表”を作り、UIのカタログ一覧から配置モードに入れるようにする。

- [x]  **Prefabの用意（最低4つ）**
    - [x]  「Hierarchy」で `Floor` 以外は空にしておく。
    - [x]  上部メニュー「**GameObject > 3D Object > Cube**」を使って、次を作成：
        - `Vehicle/Car_Proxy`（車体ダミー）
        - `Toolbox/Basic_Proxy`（工具箱ダミー）
        - `Tire/Replacement_Proxy`（交換タイヤ）
        - `Env/Wall_Min`（薄い壁）
    - [x]  各オブジェクトは**Inspector > Add Component > Box Collider** を付ける（後の選択用）。
    - [x]  「Project」ウィンドウで `Assets/Prefabs` フォルダを作る。
    - [x]  Hierarchyの各オブジェクトを `Assets/Prefabs` に**ドラッグ＆ドロップ**して**Prefab化**。
    - [x]  Hierarchy上の元オブジェクトは一旦削除（Sceneを軽く保つ）。
- [x]  **Prefabレジストリ（ScriptableObject）を作る**
    - [x]  「Project」右クリック → **Create > C# Script** → `PrefabRegistry`。
    - [x]  ダブルクリックで開き、以下を貼り付け：
        
        ```
        using System;
        using System.Collections.Generic;
        using UnityEngine;
        
        [Serializable]
        public class PrefabEntry {
            public string typeId;      // 例: "Vehicle/Car_Proxy"
            public GameObject prefab;  // Prefab参照
        }
        
        [CreateAssetMenu(menuName = "CourseEditor/PrefabRegistry")]
        public class PrefabRegistry : ScriptableObject {
            public List<PrefabEntry> entries = new();
        }
        ```
        
    - [x]  保存後、「Project」で右クリック → **Create > CourseEditor > PrefabRegistry** を選び、`Assets/Data/PrefabRegistry` フォルダに `DefaultRegistry.asset` を作る。
    - [x]  `DefaultRegistry.asset` を選択 → Inspectorの **Entries** に要素を4つ追加：
        - `typeId="Vehicle/Car_Proxy"`、`prefab` に該当Prefab
        - 他も同様。**typeIdは重複禁止**。
- [x]  **カタログUIを作る（uGUI）**
    - [x]  上部メニュー「**GameObject > UI > Canvas**」でキャンバスを作る。
    - [x]  Canvasを選択 → Inspectorの **Canvas** で **Render Mode=Screen Space - Overlay**（既定のまま）。
    - [x]  同階層で「**UI > Panel**」を追加、名前を `Panel_Catalog`。**左側**に固定：
        - RectTransformの **Anchor Presets** で左上固定、`Left=0, Right= - (画面幅-300)` 目安（幅300px）。
        - `Top=0, Bottom=0`（上下いっぱい）。
    - [x]  `Panel_Catalog` の子として「**UI > Scroll View**」を追加 → 名前 `Scroll_Catalog`。
    - [x]  「Project」右クリック → **Create > C# Script** → `CatalogUI`。
    - [x]  `CatalogUI.cs` を以下で実装：
        
        ```
        using UnityEngine;
        using UnityEngine.UI;
        
        public class CatalogUI : MonoBehaviour {
            public PrefabRegistry registry;
            public RectTransform content; // ScrollViewのContent
            public Button buttonTemplate; // 1行のボタンプレハブ
        
            public System.Action<string> onSelectType; // 選択時コールバック
        
            void Start() {
                foreach (Transform c in content) Destroy(c.gameObject);
                foreach (var e in registry.entries) {
                    var btn = Instantiate(buttonTemplate, content);
                    btn.gameObject.SetActive(true);
                    btn.GetComponentInChildren<Text>().text = e.typeId;
                    string id = e.typeId;
                    btn.onClick.AddListener(()=> onSelectType?.Invoke(id));
                }
            }
        }
        ```
        
    - [x]  ボタン雛形を作る：`Scroll_Catalog/Viewport/Content` を選択 → 子に「**UI > Button - TextMeshPro** でも OK（Textでも可）」。名前 `Btn_Template`。
        - `Btn_Template` を **Prefab化** し、**元は非表示**（Inspectorのチェックを外す）。
    - [x]  `CatalogUI` を `Panel_Catalog` に付ける → `registry` に `DefaultRegistry.asset`、`content` に `Scroll_Catalog/Viewport/Content`、`buttonTemplate` に `Btn_Template` を割り当て。
- [x]  **”配置モード”につなぐ受け口を用意**
    - 次フェーズ（C）の `PlacementController` で `onSelectType` を受けて配置モードに入ります。ここでは**イベントが飛ぶ**ことだけ確認。

---

## フェーズC：配置（Raycast＋スナップ）

### [フェーズのゴール]

カタログで選んだタイプを**床クリック位置へスナップ**して**Instantiate（生成）**できるようにする。

- [x]  **PlacementController を作る**
    - [x]  「Project」→ **Create > C# Script** → `PlacementController`。
    - [x]  `PlacementController.cs` を開いて以下を貼る：
        
        ```
        using System.Collections.Generic;
        using UnityEngine;
        using UnityEngine.EventSystems;
        
        public class PlacementController : MonoBehaviour {
            public PrefabRegistry registry;
            public Camera cam;
            public float gridSize = 0.1f;   // 10cm
            public LayerMask floorMask;     // Floorレイヤー
        
            string currentTypeId = null;
        
            Dictionary<string, GameObject> map;
            void Awake() {
                map = new Dictionary<string, GameObject>();
                foreach (var e in registry.entries) {
                    if (!map.ContainsKey(e.typeId) && e.prefab != null)
                        map.Add(e.typeId, e.prefab);
                }
            }
        
            public void EnterPlacement(string typeId) {
                currentTypeId = map.ContainsKey(typeId) ? typeId : null;
            }
        
            void Update() {
                if (string.IsNullOrEmpty(currentTypeId)) return;
                if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;
        
                if (Input.GetMouseButtonDown(0)) {
                    Ray ray = cam.ScreenPointToRay(Input.mousePosition);
                    if (Physics.Raycast(ray, out var hit, 1000f, floorMask)) {
                        Vector3 p = hit.point;
                        p.x = Mathf.Round(p.x / gridSize) * gridSize;
                        p.y = hit.point.y; // 床の高さ
                        p.z = Mathf.Round(p.z / gridSize) * gridSize;
        
                        var prefab = map[currentTypeId];
                        var go = Instantiate(prefab, p, Quaternion.identity);
                        go.AddComponent<PlacedObject>().Init(currentTypeId);
                    }
                }
            }
        }
        
        public class PlacedObject : MonoBehaviour {
            public string id;
            public string typeId;
            static int seq = 0;
            public void Init(string t) { typeId = t; id = "obj-" + (++seq).ToString("D4"); }
        }
        ```
        
    - [x]  シーンに空の `GameObject` を作成（Hierarchyで右クリック → **Create Empty**）→ 名前を `Systems`。
    - [x]  `Systems` に `PlacementController` を追加。**cam** は `Main Camera` をドラッグ、**registry** は `DefaultRegistry` を割当。
    - [x]  **floorMask**：`LayerMask` に `Floor` を指定（「Everything」を一旦外し、`Floor` だけにチェック）。
- [x]  **CatalogUI と接続する**
    - [x]  `Panel_Catalog` に付けた `CatalogUI` を選択。
    - [x]  Inspectorの `onSelectType` にイベントを追加（+）。
    - [x]  `Systems` をドラッグ → 関数に `PlacementController.EnterPlacement(string)` を選ぶ。
    - [x]  実行し、左パネルの `Vehicle/Car_Proxy` などをクリック → 床を左クリック → その位置にオブジェクトが**生成**されればOK。
- [ ]  **（任意）ゴーストプレビュー**
    - 追加で「カーソル位置に半透明プレビュー」を出したい場合は、`Update()` で `Instantiate` 前に `prefab` の透明版を表示・移動する処理を追加します（MVP-1では省略可）。

---

## フェーズD：選択・ハイライト・削除・複製

### [フェーズのゴール]

配置済みオブジェクトを**クリックで選択**し、**見た目で分かるハイライト**、**Deleteで削除**、**Ctrl/⌘+Dで複製**できるようにする。

- [x]  **SelectionService を作る**
    - [x]  「Project」→ **Create > C# Script** → `SelectionService`。
    - [x]  コードを貼る：
        
        ```
        using UnityEngine;
        using UnityEngine.EventSystems;
        
        public class SelectionService : MonoBehaviour {
            public Camera cam;
            public LayerMask pickMask = ~0; // すべて
            public PlacedObject Current;
            public SelectionOutline outline; // ハイライト描画
        
            void Update() {
                if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;
        
                if (Input.GetMouseButtonDown(0)) {
                    Ray ray = cam.ScreenPointToRay(Input.mousePosition);
                    if (Physics.Raycast(ray, out var hit, 1000f, pickMask)) {
                        var po = hit.collider.GetComponentInParent<PlacedObject>();
                        if (po != null) Select(po); else Select(null);
                    }
                }
                if (Current != null) {
                    // 削除
                    if (Input.GetKeyDown(KeyCode.Delete)) {
                        Destroy(Current.gameObject);
                        Select(null);
                    }
                    // 複製
                    if ((Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightCommand)) && Input.GetKeyDown(KeyCode.D)) {
                        var dup = Instantiate(Current.gameObject, Current.transform.position + new Vector3(0.2f,0,0.2f), Current.transform.rotation);
                        var po = dup.GetComponent<PlacedObject>(); [po.id](http://po.id) = null; po.Init(po.typeId);
                        Select(po);
                    }
                }
            }
            public void Select(PlacedObject po) {
                Current = po;
                if (outline != null) outline.ShowFor(po ? po.gameObject : null);
            }
        }
        ```
        
    - [x]  `Systems` に `SelectionService` を追加し、`cam` に `Main Camera`、`outline` は後で設定。
- [x]  **SelectionOutline（ハイライト）を作る（最小実装）**
    - **Bounding Boxの12本線**を `LineRenderer` で表示（シンプル＆依存少ない）。
    - [x]  「Project」→ **Create > C# Script** → `SelectionOutline`。
    - [x]  コードを貼る：
        
        ```
        using UnityEngine;
        using System.Collections.Generic;
        
        public class SelectionOutline : MonoBehaviour {
            public Material lineMat; // URP/Unlit Color 推奨
            List<LineRenderer> lines = new();
            GameObject target;
        
            void EnsureLines(int count){
                while(lines.Count < count){
                    var go = new GameObject("OutlineLine");
                    go.transform.SetParent(transform);
                    var lr = go.AddComponent<LineRenderer>();
                    lr.material = lineMat;
                    lr.widthMultiplier = 0.01f;
                    lr.positionCount = 2;
                    lr.useWorldSpace = true;
                    lines.Add(lr);
                }
                for(int i=0;i<lines.Count;i++) lines[i].gameObject.SetActive(i<count);
            }
        
            public void ShowFor(GameObject t){
                target = t;
                if(target==null){ EnsureLines(0); return; }
                var r = new Bounds(target.transform.position, [Vector3.one](http://Vector3.one)*0.5f);
                var renderers = target.GetComponentsInChildren<Renderer>();
                foreach(var rr in renderers) r.Encapsulate(rr.bounds);
        
                // 8頂点
                Vector3[] v = new Vector3[8];
                Vector3 min=r.min, max=r.max;
                v[0]=new Vector3(min.x,min.y,min.z);
                v[1]=new Vector3(max.x,min.y,min.z);
                v[2]=new Vector3(max.x,min.y,max.z);
                v[3]=new Vector3(min.x,min.y,max.z);
                v[4]=new Vector3(min.x,max.y,min.z);
                v[5]=new Vector3(max.x,max.y,min.z);
                v[6]=new Vector3(max.x,max.y,max.z);
                v[7]=new Vector3(min.x,max.y,max.z);
        
                // 12辺（下4,上4,縦4）
                Vector3[,] edges = {
                  {v[0],v[1]},{v[1],v[2]},{v[2],v[3]},{v[3],v[0]},
                  {v[4],v[5]},{v[5],v[6]},{v[6],v[7]},{v[7],v[4]},
                  {v[0],v[4]},{v[1],v[5]},{v[2],v[6]},{v[3],v[7]}
                };
                EnsureLines(12);
                for(int i=0;i<12;i++){
                    lines[i].SetPosition(0, edges[i,0]);
                    lines[i].SetPosition(1, edges[i,1]);
                }
            }
        }
        ```
        
    - [x]  シーンに空の `GameObject` を作り `SelectionOutline` を付ける → `Systems` の `SelectionService.outline` に割り当て。
    - [x]  `lineMat` 用に **Material** を作成（URP/Unlit、色＝シアンなど見やすい色）。
- [x]  **動作確認**
    - 再生 → 何かを配置（Cフェーズ）→ 左クリックで選択 → **枠線が出る**。
    - **Delete** で消える、**Ctrl/⌘+D** で複製される（わずかにオフセット）。

---

## フェーズE：移動・回転（ホットキー＋スナップ）

### [フェーズのゴール]

選択中のオブジェクトを**移動（0.1m刻み）**・**回転（15°刻み）できる。操作は分かりやすくW＝移動 / E＝回転**。

- [x]  **編集モード管理を追加（非常に重要）**
    - [x]  「Project」→ **Create > C# Script** → `EditModeService` を作成：
        
        ```
        using UnityEngine;
        public enum EditMode { None, Place, Move, Rotate }
        public class EditModeService : MonoBehaviour {
            public static EditModeService I;
            public EditMode Mode = EditMode.None;
            void Awake(){ I=this; }
            void Update(){
                if (Input.GetKeyDown(KeyCode.W)) Mode = EditMode.Move;
                if (Input.GetKeyDown(KeyCode.E)) Mode = EditMode.Rotate;
            }
        }
        ```
        
    - [x]  `Systems` に `EditModeService` を付ける。
    - [x]  すでにある `PlacementController` の `EnterPlacement` で `EditModeService.I.Mode = [EditMode.Place](http://EditMode.Place);` を呼ぶように軽く改修（C#1行追加）。
- [x]  **Move（床に沿ってドラッグで移動）**
    - [x]  「Project」→ **Create > C# Script** → `MoveTool`。
    - [x]  以下を記述：
        
        ```
        using UnityEngine;
        using UnityEngine.EventSystems;
        
        public class MoveTool : MonoBehaviour {
            public Camera cam;
            public SelectionService sel;
            public float gridSize = 0.1f;
            public LayerMask floorMask;
        
            void Update() {
                if (EditModeService.I==null || EditModeService.I.Mode != EditMode.Move) return;
                if (sel.Current == null) return;
                if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;
        
                // マウス位置の床ヒット
                if (Input.GetMouseButton(0)) {
                    Ray ray = cam.ScreenPointToRay(Input.mousePosition);
                    if (Physics.Raycast(ray, out var hit, 1000f, floorMask)) {
                        Vector3 p = hit.point;
                        p.x = Mathf.Round(p.x / gridSize) * gridSize;
                        p.z = Mathf.Round(p.z / gridSize) * gridSize;
                        p.y = sel.Current.transform.position.y; // 高さは維持
                        sel.Current.transform.position = p;
                    }
                }
                // 矢印キーで微調整（±1グリッド）
                Vector3 nudge = [Vector3.zero](http://Vector3.zero);
                if (Input.GetKeyDown(KeyCode.UpArrow)) nudge += new Vector3(0,0,gridSize);
                if (Input.GetKeyDown(KeyCode.DownArrow)) nudge += new Vector3(0,0,-gridSize);
                if (Input.GetKeyDown(KeyCode.LeftArrow)) nudge += new Vector3(-gridSize,0,0);
                if (Input.GetKeyDown(KeyCode.RightArrow)) nudge += new Vector3(gridSize,0,0);
                if (nudge != [Vector3.zero](http://Vector3.zero)) sel.Current.transform.position += nudge;
            }
        }
        ```
        
    - [x]  `Systems` に `MoveTool` を付け、`cam=Main Camera`、`sel=SelectionService`、`floorMask=Floor` を設定。
- [x]  **Rotate（キーで15°刻み、またはドラッグ）**
    - [x]  「Project」→ **Create > C# Script** → `RotateTool`。
    - [x]  以下を記述（まずはキー操作を優先＝確実で簡単）：
        
        ```
        using UnityEngine;
        
        public class RotateTool : MonoBehaviour {
            public SelectionService sel;
            public int stepDeg = 15;
        
            void Update() {
                if (EditModeService.I==null || EditModeService.I.Mode != EditMode.Rotate) return;
                if (sel.Current == null) return;
        
                if (Input.GetKeyDown(KeyCode.Q))  Add(stepDeg);
                if (Input.GetKeyDown(KeyCode.E))  Add(-stepDeg); // Eで逆回転でも可（好みで）
            }
            void Add(int d) {
                var t = sel.Current.transform;
                var e = t.eulerAngles;
                e.y = Mathf.Round((e.y + d) / stepDeg) * stepDeg;
                t.eulerAngles = e;
            }
        }
        ```
        
    - [x]  `Systems` に `RotateTool` を付け、`sel=SelectionService` に割り当て。
    - [x]  これで **W** でMove、**E** でRotate → **Q/Eキー**で回転が15°刻みに変わります。
- [x]  **動作確認**
    - 何かを配置 → クリックで選択 → **W** → マウス左ドラッグで床上を移動（0.1m刻み）。
    - **E** → **Q/E**で回転（15°刻み）。
    - ずれやすければ、回転は**Q/Eのみ**に絞る方が安定します。

---

## フェーズF：Undo/Redo（コマンド方式）

### [フェーズのゴール]

**配置／削除／移動／回転**の操作を**Ctrl/⌘+Zで取り消し**、**Ctrl/⌘+Y（またはShift+Z）でやり直し**できるようにする。

- [ ]  **コマンドの基底I/Fとスタックを用意**
    - [x]  「Project」→ **Create > C# Script** → `CommandStack`。
    - [x]  以下を記述：
        
        ```
        using System.Collections.Generic;
        
        public interface IEditorCommand {
            void Do();
            void Undo();
            string Label { get; }
        }
        
        public class CommandStack {
            Stack<IEditorCommand> undo = new();
            Stack<IEditorCommand> redo = new();
            public void Execute(IEditorCommand cmd) { [cmd.Do](http://cmd.Do)(); undo.Push(cmd); redo.Clear(); }
            public void Undo() { if (undo.Count>0){ var c=undo.Pop(); c.Undo(); redo.Push(c);} }
            public void Redo() { if (redo.Count>0){ var c=redo.Pop(); [c.Do](http://c.Do)(); undo.Push(c);} }
        }
        ```
        
    - [x]  「Project」→ **Create > C# Script** → `CommandService`。
        
        ```
        using UnityEngine;
        
        public class CommandService : MonoBehaviour {
            public static CommandService I;
            public CommandStack Stack = new();
            void Awake(){ I=this; }
            void Update(){
                bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightCommand);
                if (ctrl && Input.GetKeyDown(KeyCode.Z)) Stack.Undo();
                if (ctrl && (Input.GetKeyDown(KeyCode.Y) || Input.GetKeyDown(KeyCode.Z) && Input.GetKey(KeyCode.LeftShift))) Stack.Redo();
            }
        }
        ```
        
    - [x]  `Systems` に `CommandService` を追加。
- [ ]  **各操作をコマンド化する**
    - **配置**：`PlaceObjectCommand`
    - **削除**：`DeleteObjectCommand`
    - **移動**：`MoveObjectCommand`（ドラッグ開始時の位置→終了時の位置で1コマンド）
    - **回転**：`RotateObjectCommand`（開始角→終了角）
    - [x]  「Project」→ **Create > C# Script** → `PlaceDeleteCommands`。
        
        ```
        using UnityEngine;
        
        public class PlaceObjectCommand : IEditorCommand {
            string typeId; Vector3 pos; Quaternion rot;
            GameObject instance;
            System.Func<string, GameObject> factory; // typeId→Instantiateする関数
            public string Label => "Place " + typeId;
        
            public PlaceObjectCommand(string typeId, Vector3 pos, Quaternion rot, System.Func<string,GameObject> factory){
                this.typeId=typeId; this.pos=pos; this.rot=rot; this.factory=factory;
            }
            public void Do()  { instance = factory(typeId); instance.transform.SetPositionAndRotation(pos, rot); }
            public void Undo(){ if (instance!=null) GameObject.Destroy(instance); }
        }
        
        public class DeleteObjectCommand : IEditorCommand {
            GameObject target;
            Vector3 pos; Quaternion rot; string typeId;
            System.Func<string, GameObject> factory;
            public string Label => "Delete " + (target? [target.name](http://target.name) : "obj");
            public DeleteObjectCommand(GameObject target, string typeId, System.Func<string,GameObject> factory){
                [this.target](http://this.target)=target; this.typeId=typeId; this.factory=factory;
                if (target!=null){ pos=target.transform.position; rot=target.transform.rotation; }
            }
            public void Do()  { if (target!=null) GameObject.Destroy(target); }
            public void Undo(){ var go = factory(typeId); go.transform.SetPositionAndRotation(pos, rot); }
        }
        ```
        
    - [x]  「Project」→ **Create > C# Script** → `MoveRotateCommands`。
        
        ```
        using UnityEngine;
        
        public class MoveObjectCommand : IEditorCommand {
            GameObject target; Vector3 from, to;
            public string Label => "Move";
            public MoveObjectCommand(GameObject t, Vector3 from, Vector3 to){ target=t; this.from=from; [this.to](http://this.to)=to; }
            public void Do()  { if(target) target.transform.position = to; }
            public void Undo(){ if(target) target.transform.position = from; }
        }
        public class RotateObjectCommand : IEditorCommand {
            GameObject target; float fromY, toY;
            public string Label => "Rotate";
            public RotateObjectCommand(GameObject t, float fromY, float toY){ target=t; this.fromY=fromY; this.toY=toY; }
            public void Do()  { if(target){ var e=target.transform.eulerAngles; e.y=toY; target.transform.eulerAngles=e; } }
            public void Undo(){ if(target){ var e=target.transform.eulerAngles; e.y=fromY; target.transform.eulerAngles=e; } }
        }
        ```
        
- [ ]  **既存処理からコマンドを呼ぶように改修**
    - **配置**（Cフェーズの `PlacementController`）
        - 直接 `Instantiate` していた箇所を **`CommandService.I.Stack.Execute(new PlaceObjectCommand(...))`** に差し替える。
        - `factory` 関数は `typeId`→`Instantiate(prefab)` を返すラムダを渡す。
    - **削除**（Dフェーズの `SelectionService`）
        - `Destroy(Current.gameObject)` の代わりに **Deleteコマンド**を積む。
    - **移動**（Eフェーズの `MoveTool`）
        - ドラッグ開始時に `startPos` を記録、終了時（MouseUp）で `MoveObjectCommand` を実行。
    - **回転**（Eフェーズの `RotateTool`）
        - 回転前の角度 `fromY` を保持、回転操作完了時に `RotateObjectCommand` を実行。
    - ポイント：**操作1回＝コマンド1個**（ドラッグ中の連続適用はNG）。**MouseDownで開始／MouseUpで確定**のイメージ。
- [ ]  **ショートカット確認**
    - **Ctrl/⌘+Z** で直前操作が取り消され、**Ctrl/⌘+Y**（または **Shift+Z**）でやり直せること。
    - 連続して **配置→移動→回転→削除** と試し、完全に戻るか確認。
- [ ]  **注意（よくあるつまずき）**
    - **Null参照**：ターゲットが削除済みのときにMove/Rotateを呼ばないよう、**選択が有効な時だけ**受け付ける。
    - **連打でスタックが壊れる**：コマンド中に例外が出るとUndo/Redoが不整合になるので、`try/catch`を要所で入れると堅牢。

---

### 補足：各フェーズ共通の「画面での呼び名」

- **Hierarchy（左）**：シーン内のオブジェクト一覧。
- **Sceneビュー（中央上）**：編集用の3D空間。
- **Gameビュー（Scene横のタブ）**：実行時の見た目。
- **Inspector（右）**：選択オブジェクトの詳細。
- **Project（下）**：アセット（Prefab、スクリプト、素材）一覧。
- **Console（下のタブ）**：ログ／エラー表示。赤いエラーが出たらここを見る。

---

この手順の通りに進めれば、**Unityほぼ未経験でも**MVP-1の「置く・選ぶ・動かす・回す・消す・複製・Undo/Redo・Prefabレジストリ・カタログ」の基礎が動きます。

詰まった箇所があれば、画面のスクショやエラーメッセージ（Consoleの赤ログ）を送ってください。どのステップで止まったかに合わせて、ピンポイントで直します。