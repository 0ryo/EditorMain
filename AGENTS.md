# AGENTS.md — EditorMain / SkillSync Editor

## 入口

このリポジトリは、Unity上で3Dオブジェクト配置・編集とシナリオグラフ作成を行うエディタアプリです。

- 作業開始時は最初に `.ai/INDEX.md` を読む。
- `.ai/` と `Docs/` は、INDEXが案内するタスク関連資料だけを読む。一括読込は禁止。
- 長期知識は `.ai/`、詳細なUI仕様や旧履歴は `Docs/` に置く。ソースコードと設定を最終的な事実確認先とする。

## 重要ディレクトリ

- `Assets/Scripts/`: ランタイムの編集・配置・シナリオ・UIロジック
- `Assets/Editor/Automation/`: Prefab/SceneをUnity Editor APIで生成・更新する処理
- `Assets/UI/Prefabs/UIRoot.prefab`: uGUIのUIルート
- `Assets/EditorMain.unity`: メインScene
- `Assets/Data/DefaultRegistry.asset`: 既定の配置Prefab一覧
- `Packages/`, `ProjectSettings/`: Unity依存とプロジェクト設定
- `Docs/`: 詳細仕様、監査、旧Worklog（必要時のみ参照）

## 基本確認

```powershell
git status --short
git diff --check
```

- Unity: `6000.2.6f2`。正確な確認が必要な場合は `ProjectSettings/ProjectVersion.txt` を読む。
- リポジトリ内にプロジェクト固有のTest assembly、Lint設定、CI設定は確認できていない。
- `Assembly-CSharp.csproj` はUnity生成後に更新されておらず、現在の全ソースを含まないため `dotnet build` を合否判定に使わない。
- Unityのコンパイル・PlayMode・ビルド検証はユーザーが行う。CodexはUnity Editor/Unity CLIを起動しない。

## 絶対制約

- GUI操作をしない。Scene/PrefabのYAML直接編集は最終手段とし、原則 `Assets/Editor/Automation/` のEditor API経路で更新する。
- 既存アーキテクチャを大きく崩すリネーム・一括移動・大改修を避け、差分を小さく読みやすく保つ。
- 新規Package、外部DLL、その他の依存は無断追加しない。必要性・代替案・運用コストを先に示す。
- ユーザーの未コミット変更を保持し、関連のないファイルを変更しない。
- UIはuGUI + TextMeshPro + Prefabが基準。データ/状態と表示ロジックを分離する。
- コミットする場合はタスク単位の小さなコミットにする。

## 終了時

- 自動で可能な静的確認を行う。
- 完了報告は「完了したこと」「追加機能」「ユーザーが確認すべき事項」だけに絞り、変更箇所やcommit/pushの記載は省く。
- 将来の別タスクでも再利用価値がある新事実だけを `.ai/` に反映する。対象は、アーキテクチャ上の事実、恒久的判断、反復する規約、再発しやすい原因、有効/失敗した解決法、人間が明示した恒久ルール。
- 一時的作業、会話ログ、ソースから容易に分かる細部、重複、推測は保存しない。
- 追加前に既存知識を検索し、追記より更新・統合を優先する。古い情報やコードとの矛盾は修正し、知識ベースを肥大化させない。
