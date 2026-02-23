# AGENTS.md — Codex Local Rules for Unity Project
 
## 0. この文書の目的 🎯
Codex（CLI / App）に対して、このUnityプロジェクトでの **作業方針・禁止事項・検証手順** を固定する。
実装後の確認は人間が行う前提で、Codexは **「実装を速く・壊さず・差分を読みやすく」** 進める。

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
- UI調整/各機能ともに、後から仕様変更しやすい構造（データとUIロジックの分離）

---

## 2. 作業の基本フロー（Codexが必ず従う）🧭
### Phase A: 現状把握（最初に必ず行う）
1) `ProjectSettings/ProjectVersion.txt` を読んでUnityバージョンを把握  
2) UI方式を特定：
   - UI Toolkit（`.uxml/.uss` がある、または `UnityEngine.UIElements` を使用）
   - uGUI（Canvas/Prefab中心、`UnityEngine.UI` を使用）
3) 現状のUI構造（主要Scene/Prefab、UIルート、画面遷移の入口）を短くメモ（作業ログに残す）
4) **新規チャットで作業開始時は、必ず最初に** `Docs/worklog/worklog_latest.md` を参照する
5) UI実装・UI仕様確認が必要な場合は、`Docs/worklog_UI/` 配下を参照する  
   参照順は `全体UI仕様.md` → `worklog_*.md`（個別ウィンドウ）とする

### Phase B: 変更計画（軽量でよいが必須）
- 何を編集するかを「ファイル単位」で列挙し、依存関係（参照/Prefab/ScriptableObject）を明示する。
- Scene/Prefabに手を入れる必要がある場合は **必ずEditorスクリプトでの更新ルート**を先に設計する。

### Phase C: 実装
- 各機能のルールファイル（セクション3参照）に従って実装する。
- UI調整は「壊さない最小差分」で先に完了させる（大改修しない）。

### Phase D: 検証（自動でできる範囲は自動で）
- コンパイル確認（少なくともスクリプトのコンパイルが通るところまで）
- 可能ならUnityをCLI起動して `-executeMethod` で自動処理（後述）
- 変更サマリ（何をどこに追加/変更したか）を最後に出す

---

## 3. 機能別ルール（外部参照）📎

各機能の詳細ルールは専用ファイルを参照すること。

| 作業内容 | 参照先 |
|---|---|
| UI編集（レイアウト/コンポーネント/Prefab） | → `Docs/rules/ui_editing_rules.md` |
| デザイン変更（色/フォント/スペーシング/コンポーネント仕様） | → `Docs/rules/design_rule.md` |
| シナリオ作成機能（データモデル/保存/権限） | → `Docs/rules/scenario_rules.md` |
| Worklog運用（作業ログの読み書き） | → `Docs/rules/worklog_rules.md` |

> **新しい機能ルールを追加する場合：**  
> `Docs/rules/` にファイルを作成し、上の表に1行足すだけでよい。

---

## 4. Unity CLI自動化（Codexが使う前提）🧪
### 4.1 原則
- CodexはGUIを使わない代わりに、必要ならUnityをCLI起動して自動処理を行う。
- Scene/Prefab更新が必要な場合は `-executeMethod` を使う。
- `Assets/Editor/Automation/` にエントリポイントを用意する（static method）。

### 4.2 自動化メソッドの規約
- `Assets/Editor/Automation/AutomationEntry.cs`
  - `static void ApplyUiEdits()`：UI反映
  - `static void MigrateScenarioData()`：データ移行が必要なら
  - `static void ValidateProject()`：参照整合性/簡易チェック

各メソッドは：
- 何を更新したかを `Debug.Log` で出す
- 失敗時は例外を投げる（CI/自動実行で失敗が検知できるように）

### 4.3 可能ならテストもCLI実行（任意）
- EditModeテストがある場合、CLIで実行して結果ファイルを出す。
- ただし、テストが未整備なら「コンパイルが通る」ことを最優先にする。

---

## 5. 仕上げ（Codexが必ず出すアウトプット）🧾
作業の最後に、以下を必ず提示すること：
1) 変更サマリ（何を、なぜ、どう変えた）
2) 触ったファイル一覧（追加/変更/削除）
3) 追加した自動化（-executeMethod等）があるなら、実行方法
4) 人間が確認すべきチェックリスト
   - UI：主要画面の表示崩れ、入力/遷移、VR操作
   - シナリオ：作成→保存→再起動→復元、プレビュー再生、破損時挙動

---

## 6. MCP/外部連携について（方針）🧰
- Unity MCP は必須ではない。まずは「ファイル編集 + Unity CLI（-executeMethod）」で完結させる。
- MCP等の導入が必要になった場合のみ、導入理由・運用コスト・代替案を先に提示すること。
