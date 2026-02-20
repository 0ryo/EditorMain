# AGENTS.md — Codex Local Rules for Unity Project (UI tuning + Scenario Authoring)

## 0. この文書の目的 🎯
Codex（CLI / App）に対して、このUnityプロジェクトでの **作業方針・禁止事項・検証手順** を固定する。
本プロジェクトの当面のゴールは以下：
- **UIの調整（レイアウト/導線/情報設計）**
- **シナリオ作成機能の追加（データモデル + 編集UI + 保存/読み込み）**
- 実装後の確認は人間が行う前提で、Codexは **「実装を速く・壊さず・差分を読みやすく」** 進める。

---

## 1. 非交渉ルール（必須）🧷
### 1.1 変更の原則
- **GUI操作はしない**（Inspectorでの手動変更を前提にしない）。
- UnityのScene/Prefabの直接YAML編集は最終手段。  
  原則は **Unity Editor API 経由（Editorスクリプト）で生成/更新して保存**する。
- 1タスク = 1コミット（もしくは小さなコミット列）。差分を読みやすく保つ。
- 既存アーキテクチャを大きく崩さない（大規模リネーム/全ファイル移動は禁止）。
- 新規依存（Unity Package / 外部DLL / npm等）を追加する必要が出たら、まず理由と代替案を提示してから進める（勝手に増やさない）。

### 1.2 成功条件
- エディタ起動時/ビルド時の **コンパイルエラーがゼロ**
- 変更内容が「どこを何のために変えたか」説明できる（最終的にCHANGELOG相当を残す）
- UI調整/シナリオ機能ともに、後から仕様変更しやすい構造（データとUIロジックの分離）

---

## 2. 作業の基本フロー（Codexが必ず従う）🧭
### Phase A: 現状把握（最初に必ず行う）
1) `ProjectSettings/ProjectVersion.txt` を読んでUnityバージョンを把握  
2) UI方式を特定：
   - UI Toolkit（`.uxml/.uss` がある、または `UnityEngine.UIElements` を使用）
   - uGUI（Canvas/Prefab中心、`UnityEngine.UI` を使用）
3) シナリオ機能の既存痕跡を検索（`Scenario`, `Story`, `Lesson`, `Step` などの語）
4) 現状のUI構造（主要Scene/Prefab、UIルート、画面遷移の入口）を短くメモ（作業ログに残す）
5) 仕様確認が必要な場合は、まず `開発計画/仕様/` 配下のウィンドウ仕様書（`*.md`）を参照する

### Phase B: 変更計画（軽量でよいが必須）
- 何を編集するかを「ファイル単位」で列挙し、依存関係（参照/Prefab/ScriptableObject）を明示する。
- Scene/Prefabに手を入れる必要がある場合は **必ずEditorスクリプトでの更新ルート**を先に設計する。

### Phase C: 実装（UI → シナリオの順）
- UI調整は「壊さない最小差分」で先に完了させる（大改修しない）。
- 次にシナリオ作成機能を追加。データモデル → 保存/読み込み → 編集UI → 画面統合 の順で積む。

### Phase D: 検証（自動でできる範囲は自動で）
- コンパイル確認（少なくともスクリプトのコンパイルが通るところまで）
- 可能ならUnityをCLI起動して `-executeMethod` で自動処理（後述）
- 変更サマリ（何をどこに追加/変更したか）を最後に出す

---

## 3. UI編集ルール 🧩

### 3.1 UI共通の設計規約
- UIは「見た目」と「状態/ロジック」を分離する。
  - View（表示）: `UI/*` 名前空間 or `UI/Views/*`
  - State/Logic: `UI/State/*` or `UI/Controllers/*`
- “UIから直接データを書き換える”は禁止。必ずService/UseCase層（またはそれに相当）を経由する。
- UI調整は「既存のPrefab/Scene構造を維持」し、必要最小限の追加に留める。

### 3.2 UI Toolkitの場合（UXML/USS）
- 追加/変更は原則として：
  - レイアウト：`.uxml`
  - 見た目：`.uss`
  - つなぎ込み：`.cs`（Viewの初期化、イベント購読、バインド）
- USSは「局所化」する。グローバルに効くセレクタは極力避ける。
- 既存のクラス命名規約があるならそれに合わせる（例：BEM風、kebab-case等）。

### 3.3 uGUIの場合（Canvas/Prefab）
- “Scene直置き”を増やさない。可能ならUIはPrefab化して参照で差し込む。
- RectTransform/アンカー/スケールは既存規約を維持。
- ボタン/入力/リストなどは共通コンポーネント化を優先（同じ見た目・同じ挙動を量産しない）。

### 3.4 Scene/Prefabを更新する必要がある場合（重要）
- 直接YAML編集は禁止（最終手段）。
- 代わりに `Assets/Editor/Automation/` に **Editorスクリプト**を作り、
  - 対象Scene/Prefabを `AssetDatabase` 経由でロード
  - 必要なGameObject/Componentを追加・参照をセット
  - `PrefabUtility.SaveAsPrefabAsset` もしくはScene保存
  - 変更点をログ出力
  という形で “Unityに正しい参照を書かせる”。

---

## 4. シナリオ作成機能：設計と権限（Permissions）🔐

### 4.1 最初の実装は「追加権限ゼロ」で成立させる（最優先）
シナリオデータの保存先は原則：
- `Application.persistentDataPath` 配下
- 形式は JSON（推奨）または ScriptableObject（用途次第）
- “ユーザーが任意の場所を指定して保存” は後回し（＝権限・OS差分が増える）

> persistentDataPath は「実行間で保持したいデータの保存先」としてUnityが提供する標準パス。  
> まずここに閉じれば、多くのケースで追加のOS権限が不要になる。  

### 4.2 将来拡張で権限が増えるパターン（必要になった時だけ）
次の機能を入れる場合は、プラットフォームごとの設定が必要になる可能性がある。
- 端末の **カメラ/マイク/位置情報** を使う（iOSはInfo.plistのUsage Descriptionが必要）
- 端末の **外部ストレージ** に直接保存する（特にAndroidは制約が強い）
- **クラウド同期**（ネットワークアクセス、認証/OAuth、ATS/証明書設定など）

Codexは以下の手順で進めること：
1) 「なぜそれが必要か」を説明（代替案：persistentDataPathで足りないか？）
2) 対象プラットフォームを列挙（Standalone / Android / iOS / WebGL等）
3) Unity側の設定箇所を提示してから実装

### 4.3 iOS系の注意
- 機密情報/デバイス機能にアクセスする場合、iOSは許可文言（Usage Description）が必要になる。
- UnityはPlayer SettingsにUsage Descriptionを追加すると、Info.plistへ反映される想定で進める。

### 4.4 Android系の注意
- AndroidはManifestに権限が入る場合がある。
- 追加する場合は `Assets/Plugins/Android/AndroidManifest.xml`（またはUnityの生成/カスタム手順）を扱う。
- ただし、最初のMVPは「persistentDataPathへ保存」方針でManifest変更を発生させない。

---

## 5. シナリオ作成機能：実装の最低ライン（MVP）🧱

### 5.1 データモデル（必須）
- Scenario（シナリオ）
  - id（安定ID）
  - title
  - description（任意）
  - steps[]（配列）
- Step（手順/ページ）
  - id
  - type（例：Text / Choice / Action / Wait など。最初はTextだけでもよい）
  - payload（本文、選択肢、パラメータ等）
  - next（遷移。最初は線形でもよい）

### 5.2 保存/読み込み（必須）
- 保存：persistentDataPath 配下に `scenarios/*.json`
- 読み込み：起動時 or 画面表示時に一覧をロード
- 破損対策：
  - 書き込みは一時ファイル → 原子的リネーム
  - JSONのスキーマバージョンを持たせる（`schemaVersion`）

### 5.3 編集UI（必須）
- 一覧（作成/複製/削除/検索は後回し可）
- 編集（title + steps編集）
- プレビュー（ランタイム上で再生できる簡易プレビュー）

### 5.4 UI統合（必須）
- 既存UI/画面遷移に「シナリオ作成」導線を追加
- 既存のInput/VR操作規約を崩さない（必要なら既存の操作体系に合わせる）

---

## 6. Unity CLI自動化（Codexが使う前提）🧪
### 6.1 原則
- CodexはGUIを使わない代わりに、必要ならUnityをCLI起動して自動処理を行う。
- Scene/Prefab更新が必要な場合は `-executeMethod` を使う。
- `Assets/Editor/Automation/` にエントリポイントを用意する（static method）。

### 6.2 自動化メソッドの規約
- `Assets/Editor/Automation/AutomationEntry.cs`
  - `static void ApplyUiEdits()`：UI反映
  - `static void MigrateScenarioData()`：データ移行が必要なら
  - `static void ValidateProject()`：参照整合性/簡易チェック

各メソッドは：
- 何を更新したかを `Debug.Log` で出す
- 失敗時は例外を投げる（CI/自動実行で失敗が検知できるように）

### 6.3 可能ならテストもCLI実行（任意）
- EditModeテストがある場合、CLIで実行して結果ファイルを出す。
- ただし、テストが未整備なら「コンパイルが通る」ことを最優先にする。

---

## 7. 仕上げ（Codexが必ず出すアウトプット）🧾
作業の最後に、以下を必ず提示すること：
1) 変更サマリ（何を、なぜ、どう変えた）
2) 触ったファイル一覧（追加/変更/削除）
3) 追加した自動化（-executeMethod等）があるなら、実行方法
4) 人間が確認すべきチェックリスト
   - UI：主要画面の表示崩れ、入力/遷移、VR操作
   - シナリオ：作成→保存→再起動→復元、プレビュー再生、破損時挙動

---

## 8. MCP/外部連携について（方針）🧰
- Unity MCP は必須ではない。まずは「ファイル編集 + Unity CLI（-executeMethod）」で完結させる。
- MCP等の導入が必要になった場合のみ、導入理由・運用コスト・代替案を先に提示すること。
