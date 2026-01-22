# MVP-2（配置データJSONエクスポート：絶対座標）

## 目的

MVP-1で作成した配置データ（PlacedObject）を、Unityの**ワールド座標（絶対値）**として取得し、`Assets/Exports/` に **JSONとして書き出す機能**を実装する。
本MVPでは **Exportのみ**を対象とし、ImportやValidationは実装しない。

---

## 前提

- シーン上の配置済みオブジェクトには `PlacedObject` コンポーネントが付与されている。
- `PlacedObject` は少なくとも以下を持つ：
    - `string id`（一意識別子）
    - `string typeId`（Prefab/カテゴリ識別子：復元用キー）
- オブジェクト配置はMVP-1時点の仕組みで実装済み。

---

## 出力仕様（暫定・最小）

### 出力ファイル

- 出力先：`Assets/Exports/`
- ファイル名：`<ProjectName>-placement.json`（例：`MyProject-placement.json`）

### JSON構造（MVP-2）

- `version`（int）
- `projectName`（string）
- `objects`（array）
    - `id`（string）
    - `typeId`（string）
    - `position`（Vector3, world）
    - `rotation`（Quaternion, world）
    - `scale`（Vector3, localScale）

> position/rotation は transform.position / transform.rotation
scale は transform.localScale
> 

---

## 実装手順

### 手順1. ID付与の確実化（一意・永続）

### 目的

Export時点で、全 `PlacedObject` が必ず `id` を持つ状態にする。

### 作業

1. `IdGenerator`（連番採番）を用意する。
2. オブジェクト配置時（Instantiate直後）に `PlacedObject.id` が空なら採番して設定する。

---

## フェーズ1-1：作業場所を整える（Scriptsフォルダ作成）

### [フェーズのゴール]

ID関連スクリプトを置く場所を作り、以降の編集対象を明確にする。

- [x]  **Scriptsフォルダを作成**
    - [x]  下部の「**Project**」ウィンドウで `Assets` を右クリック
    - [x]  **Create > Folder** を選択
    - [x]  フォルダ名を `Scripts` にする
- [x]  **サブフォルダを作成（任意だが推奨）**
    - [x]  `Assets/Scripts` を右クリック → **Create > Folder**
    - [x]  `Core` を作る
    - [x]  `Services` を作る
    （配置・選択など既存スクリプトが散らかりにくくなります）

---

## フェーズ1-2：IdGenerator を作る（ID発行サービス）

### [フェーズのゴール]

`obj-0001` のような一意IDを、どこからでも発行できる状態にする。

- [x]  **IdGenerator.cs を作成**
    - [x]  `Assets/Scripts/Services` を右クリック
    - [x]  **Create > C# Script**
    - [x]  名前を `IdGenerator` にする
- [x]  **IdGenerator.cs の中身を以下に置き換え（そのまま貼る）**

```csharp
using UnityEngine;

public class IdGenerator : MonoBehaviour {
    public static IdGenerator I { get; private set; }

    [Header("Runtime sequence (MVP-2)")]
    [SerializeField] private int seq = 0;

    private void Awake() {
        if (I != null && I != this) {
            Destroy(gameObject);
            return;
        }
        I = this;
    }

    public string NewObjectId() {
        seq++;
        return $"obj-{seq:D4}";
    }
}

```

---

## フェーズ1-3：PlacedObject を「正式なコンポーネント」にする

### [フェーズのゴール]

配置済みオブジェクトが「必ず id / typeId を保持する」状態にする（Export/Scenarioで参照可能にする）。

- [x]  **PlacedObject.cs を作成**
    - [x]  `Assets/Scripts/Core` を右クリック
    - [x]  **Create > C# Script**
    - [x]  名前を `PlacedObject` にする
- [x]  **PlacedObject.cs の中身を以下に置き換え（そのまま貼る）**

```csharp
using UnityEngine;

public class PlacedObject : MonoBehaviour {
    [SerializeField] private string id;
    [SerializeField] private string typeId;

    public string Id => id;
    public string TypeId => typeId;

    // PlacementController から必ず呼ぶ
    public void InitType(string t) {
        typeId = t;
    }

    // 「IDが無ければ発行」：通常の配置で使う
    public void EnsureHasId() {
        if (!string.IsNullOrEmpty(id)) return;
        if (IdGenerator.I == null) {
            Debug.LogError("IdGenerator がシーンに存在しません。Hierarchyの Systems に IdGenerator を追加してください。");
            return;
        }
        id = IdGenerator.I.NewObjectId();
    }

    // 「強制で新しいID」：複製で使う（同一ID事故を防ぐ）
    public void ForceNewId() {
        id = null;
        EnsureHasId();
    }
}

```

---

## フェーズ1-4：Hierarchy に IdGenerator を配置する（Systemsに付ける）

### [フェーズのゴール]

シーン上に IdGenerator が常に存在し、配置・複製がいつでもID発行できる状態にする。

- [ ]  **Hierarchy の `Systems` を確認**
    - [ ]  左側の「**Hierarchy**」に `Systems` があるか確認
        - 無ければ：Hierarchyで右クリック → **Create Empty** → `Systems` にリネーム
- [ ]  **Systems に IdGenerator を追加**
    - [ ]  `Systems` を選択
    - [ ]  右側の「**Inspector**」下部の **Add Component**
    - [ ]  検索欄に `IdGenerator` と入力して追加
- [ ]  **（重要）再生してエラーが出ないことを確認**
    - [ ]  上部の **再生ボタン（▶）** を押す
    - [ ]  Console（Window > General > Console）に赤エラーが出ないこと

---

## フェーズ1-5：PlacementController.cs を改修する（埋め込みPlacedObjectを撤去）

### [フェーズのゴール]

オブジェクト生成時に、必ず `PlacedObject` が付き、`typeId` と `id` が設定される。

あなたの `MVP-1.md` の `PlacementController.cs` は、末尾に `PlacedObject` クラスが埋め込まれています。
ここを **削除**して、先ほど作った `PlacedObject.cs` を使うように直します。

- [ ]  **PlacementController.cs を開く**
    - [ ]  Projectウィンドウで `PlacementController` をダブルクリック
- [ ]  **ファイル末尾の `public class PlacedObject : MonoBehaviour { ... }` を削除**
    - [ ]  `PlacementController` のクラス定義の後にある `PlacedObject` を丸ごと消す
    （このままだと同名クラスが重複してコンパイルエラーになります）
- [ ]  **Instantiate直後の行を次のように置き換える**

変更前（MVP-1の状態）：

```csharp
var go = Instantiate(prefab, p, Quaternion.identity);
go.AddComponent<PlacedObject>().Init(currentTypeId);

```

変更後（確定版）：

```csharp
var go = Instantiate(prefab, p, Quaternion.identity);

// Prefab側に付いていてもOK、無ければ付ける
var po = go.GetComponent<PlacedObject>();
if (po == null) po = go.AddComponent<PlacedObject>();

po.InitType(currentTypeId);
po.EnsureHasId();

```

- [ ]  **UnityのConsoleにエラーが無いことを確認**
    - [ ]  上部▶再生
    - [ ]  カタログから配置して、Hierarchyで生成物をクリック
    - [ ]  Inspectorに `PlacedObject` が付いていることを確認
    - [ ]  `id` が `obj-0001` のように埋まっていることを確認
    - [ ]  `typeId` が `Vehicle/Car_Proxy` のように埋まっていることを確認

---

## フェーズ1-6：SelectionService の「複製」を改修する（ID重複防止）

### [フェーズのゴール]

Ctrl/⌘+D 複製で **必ず新しいIDが発行される**。

MVP-1では複製時に `po.id = null; po.Init(po.typeId);` をしています。
これを `ForceNewId()` に置き換えます。

- [ ]  **SelectionService.cs を開く**
- [ ]  **複製ブロックを次のように変更**

変更前（MVP-1の状態）：

```csharp
var dup = Instantiate(Current.gameObject, Current.transform.position + new Vector3(0.2f,0,0.2f), Current.transform.rotation);
var po = dup.GetComponent<PlacedObject>();
po.id = null;
po.Init(po.typeId);
Select(po);

```

変更後（確定版）：

```csharp
var dup = Instantiate(
    Current.gameObject,
    Current.transform.position + new Vector3(0.2f, 0f, 0.2f),
    Current.transform.rotation
);

var po = dup.GetComponent<PlacedObject>();
if (po == null) po = dup.AddComponent<PlacedObject>();

// typeIdは元のまま複製されている想定（SerializeField）
// 念のため未設定なら埋める
if (string.IsNullOrEmpty(po.TypeId)) {
    po.InitType(Current.TypeId);
}

po.ForceNewId();
Select(po);

```

---

## フェーズ1-7：動作確認（IDの安定性テスト）

### [フェーズのゴール]

「配置」「複製」「削除」を繰り返しても、IDが重複しないことを確認する。

- [ ]  **配置テスト**
    - [ ]  3つ配置する（例：車体、タイヤ、工具箱）
    - [ ]  それぞれ `id` が `obj-0001, obj-0002...` と別になっている
- [ ]  **複製テスト**
    - [ ]  どれか選択 → Ctrl/⌘+D
    - [ ]  複製後の `id` が元と違う
- [ ]  **削除テスト**
    - [ ]  Deleteで削除
    - [ ]  残ったオブジェクトの `id` が変化しない（勝手に振り直されない）

---

# 手順2. Export用データモデルの作成（JSONの器）

### 目的

書き出すJSONをC#のSerializableクラスとして定義する。

### フェーズ2-1：スクリプト配置場所を決める

### [フェーズのゴール]

データモデル用スクリプトを置く場所を固定し、後から迷わない状態にする。

- [ ]  **Project（下部）でフォルダを作成**
    - [ ]  `Assets/Scripts` を開く
    - [ ]  右クリック → **Create > Folder**
    - [ ]  フォルダ名を `Export` にする（例：`Assets/Scripts/Export`）
        - 既にフォルダ運用ルールがあるならそれに従ってOK
        - 重要なのは「Exportモデルを置く場所を固定」すること

---

### フェーズ2-2：PlacementExportModel.cs を作成する

### [フェーズのゴール]

JsonUtilityでそのまま吐ける「JSONの器」をC#のSerializableとして定義する。

- [ ]  **PlacementExportModel.cs を作成**
    - [ ]  `Assets/Scripts/Export` を右クリック
    - [ ]  **Create > C# Script**
    - [ ]  名前を `PlacementExportModel` にする
- [ ]  **PlacementExportModel.cs を開いて、全文を下記に置き換え**
    - Unity標準の `JsonUtility` で確実に動くように、**public fieldのみ**で構成します。

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// MVP-2: 配置データJSONのルート
/// JsonUtility前提のため public field のみで構成する。
/// </summary>
[Serializable]
public class PlacementExport
{
    public int version = 1;
    public string projectName = "MyProject";
    public List<PlacementObject> objects = new List<PlacementObject>();
}

/// <summary>
/// MVP-2: 配置オブジェクト1件分
/// position/rotation はワールド、scale は localScale
/// </summary>
[Serializable]
public class PlacementObject
{
    public string id;
    public string typeId;

    // Absolute (world)
    public Vector3 position;
    public Quaternion rotation;

    // Local
    public Vector3 scale;
}

```

---

### フェーズ2-3：コンパイル確認（Unity Consoleでエラー0）

### [フェーズのゴール]

この時点でモデル定義が正しく、Unityが正常コンパイルできる状態にする。

- [ ]  Unityへ戻る（自動でコンパイルが走る）
- [ ]  上部メニュー **Window > General > Console** を開く
- [ ]  **赤いエラーが出ていない**ことを確認
    - もしエラーが出た場合は、まず以下を確認
        - `PlacementExportModel.cs` が `Assets/Scripts/Export` に1つだけ存在するか
        - 同名クラス（PlacementExport / PlacementObject）が別ファイルに存在しないか

---

### フェーズ2-4（任意だが推奨）：JsonUtilityでの出力スモークテスト

### [フェーズのゴール]

データモデルが「本当にJsonUtilityで期待通りにJSONになる」ことを、最短で確認する。

- [ ]  **テスト用スクリプトを作成**
    - [ ]  `Assets/Scripts/Export` を右クリック
    - [ ]  **Create > C# Script**
    - [ ]  名前を `PlacementExportModelSmokeTest` にする
- [ ]  **中身を下記に置き換え**
    - 再生するとConsoleにJSONを出します（ファイル出力はしない）。

```csharp
using UnityEngine;

public class PlacementExportModelSmokeTest : MonoBehaviour
{
    void Start()
    {
        var data = new PlacementExport
        {
            version = 1,
            projectName = "SmokeTest"
        };

        data.objects.Add(new PlacementObject
        {
            id = "obj-0001",
            typeId = "Vehicle_Car_Proxy",
            position = new Vector3(1.2f, 0.5f, -3.4f),
            rotation = Quaternion.identity,
            scale = new Vector3(1f, 1f, 1f)
        });

        string json = JsonUtility.ToJson(data, true);
        Debug.Log(json);
    }
}

```

- [ ]  **Hierarchy（左）で空オブジェクトを作る**
    - [ ]  Hierarchyで右クリック → **Create Empty**
    - [ ]  名前を `ExportSmokeTest` にする
- [ ]  **Inspector（右）でコンポーネント追加**
    - [ ]  `ExportSmokeTest` を選択
    - [ ]  **Add Component** → `PlacementExportModelSmokeTest` を追加
- [ ]  **再生（Play）してConsoleを確認**
    - [ ]  上部の再生ボタン（▶）
    - [ ]  ConsoleにJSONが出て、`objects` 配列が含まれることを確認
    - [ ]  確認できたら `ExportSmokeTest` と `PlacementExportModelSmokeTest.cs` は削除してOK（任意）

---

## フェーズ2完了条件（DoD）

- [ ]  `PlacementExport` / `PlacementObject` が **1か所に定義**されている
- [ ]  Unity Consoleが **エラー0**
- [ ]  （任意）`JsonUtility.ToJson(..., true)` で期待形のJSONが出ることを確認済み

---

### 手順3. Export処理の実装（走査→構築→ファイル出力）

### 目的

シーン内の `PlacedObject` を走査し、Transformを取得してJSON化し、`Assets/Exports/` に保存する。

### 実装（例）

**PlacementExportService.cs**

```csharp
using System.IO;
using UnityEngine;

public class PlacementExportService : MonoBehaviour {
    public string projectName = "MyProject";

    public void ExportToJson() {
        var data = new PlacementExport {
            version = 1,
            projectName = projectName
        };

        foreach (var p in FindObjectsOfType<PlacedObject>()) {
            data.objects.Add(new PlacementObject {
                id = p.id,
                typeId = p.typeId,
                position = p.transform.position,     // world
                rotation = p.transform.rotation,     // world (Quaternion)
                scale = p.transform.localScale       // local
            });
        }

        string dir = Path.Combine(Application.dataPath, "Exports");
        Directory.CreateDirectory(dir);

        string fileName = $"{projectName}-placement.json";
        string path = Path.Combine(dir, fileName);

        string json = JsonUtility.ToJson(data, true);
        File.WriteAllText(path, json);

        Debug.Log($"Exported: Assets/Exports/{fileName}");
    }
}

```

---

### 手順4. UIの追加（Exportボタン）

### 目的

ユーザー操作でExportを実行できるようにする。

### 作業

1. Canvasに `Button(Export)` を追加
2. `OnClick()` に `PlacementExportService.ExportToJson()` を割り当てる
3. 必要なら `InputField(ProjectName)` を追加し、`projectName` に反映する（任意）

---

## 動作確認（チェックリスト）

1. オブジェクトを3個配置し、位置・回転・拡縮を変更する
2. Exportボタンを押す
3. `Assets/Exports/<ProjectName>-placement.json` が生成される
4. `objects.length == 3`
5. 各要素の `id` が一意である
6. `position` がワールド座標として妥当（移動分が反映）
7. `rotation`（x,y,z,w）が変化している（全て同値になっていない）
8. `scale` が拡縮操作を反映している

---

## Definition of Done（完了条件）

- [ ]  全 `PlacedObject` が `id` を保持している
- [ ]  `id/typeId/position(rotation quaternion)/scale` をJSONに書き出せる
- [ ]  `Assets/Exports/` に `<ProjectName>-placement.json` を生成できる
- [ ]  UIボタンからExportを実行できる
- [ ]  チェックリストを満たす

---

## 備考（将来拡張の差し込み点）

- Import（復元）機能はMVP-2対象外
- Validation（必須オブジェクトの有無等）もMVP-2対象外
- 出力先は将来的に `Application.persistentDataPath` へ切替予定（ビルド運用対応）
- 表示名（Rename）やユーザーフレンドリーなメタ情報は後続MVPで追加