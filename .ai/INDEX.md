# External Knowledge Index

## 読み方

1. 毎タスク、まずこのファイルだけを読む。
2. 下表からタスクに必要な資料だけを選ぶ。
3. 実装直前に、対象ソース・設定・Prefab/Sceneを確認して知識の鮮度を検証する。
4. `.ai/`、`Docs/`、旧Worklogの一括読込はしない。

## `.ai/` のルーティング

| ファイル | 内容 | 読むタスク |
|---|---|---|
| `PROJECT.md` | 目的、機能、技術スタック、起動、用語 | 初参加、全体像、環境・依存変更 |
| `ARCHITECTURE.md` | コンポーネント責務、データフロー、境界、影響範囲 | 機能追加、バグ調査、複数層をまたぐ変更 |
| `CONVENTIONS.md` | 実コードで確認した命名、構成、実装・エラー・検証パターン | 実装、レビュー、リファクタリング |
| `DECISIONS.md` | 今後も守る設計判断と根拠 | 方針変更、代替案比較、既存判断に触れる変更 |
| `GOTCHAS.md` | 環境制約、文書差異、壊れやすい箇所、既知の落とし穴 | 不具合調査、ビルド、UI、保存、入力、モデル取込 |
| `TASKS.md` | 未完了テーマだけの短い作業入口。詳細はリンク先の書庫に置く | 次タスク選定、優先順位確認、完了項目の削除 |
| `WORKLOG/README.md` | 短い再利用可能Worklogの運用規則 | 作業終了時に恒久知識が残る場合のみ |
| `WORKLOG/2026-08-20-knowledge-bootstrap.md` | この知識環境を作成した時点と調査範囲 | 知識の鮮度や調査根拠を監査するとき |

## 既存 `Docs/` のルーティング

既存文書は詳細仕様または履歴であり、通常は `.ai/` から必要箇所を特定してから読む。

| タスク | 読む順序 |
|---|---|
| uGUIレイアウト、Prefab、UI構造 | `Docs/rules/ui_editing_rules.md` → `Docs/worklog/worklog_UI/全体UI仕様.md` → 対象ウィンドウの仕様 |
| 色、フォント、余白、コンポーネント外観 | `Docs/rules/design_rule.md`。実装値は `Assets/Scripts/UI/DesignTokens.cs` と突合する |
| オブジェクト一覧 | 上記に加えて `Docs/worklog/worklog_UI/worklog_オブジェクト一覧ウィンドウ.md` |
| シナリオ作成・保存・権限 | `Docs/rules/scenario_rules.md` → `Assets/Scripts/Core/` と `Assets/Scripts/Services/CurriculumGraphService.cs`。既知差異は `GOTCHAS.md` を先に確認 |
| 2026-06-29 UI監査の実装 | `Docs/design_audit/ui_design_implementation_policy_2026-06-29.md`。指摘根拠が必要な場合だけ監査本文と画像を見る |
| 直近の配置・視点操作の経緯 | `Docs/worklog/worklog_latest.md` の関連Taskだけ読む |
| 過去の失敗・旧仕様の追跡 | `Docs/worklog/` または `Docs/trash/worklog/` をファイル名・見出しで絞り込む |

`Docs/trash/` と過去Worklogは常用知識ではない。全文を入口コンテキストへ入れない。
