# Reusable Worklog Rules

`WORKLOG/` は会話履歴や逐次作業日誌の置き場ではありません。将来の別タスクが同じ調査・失敗を繰り返さないための短い引継ぎだけを保存します。

## 作成条件

次のいずれかがあり、`PROJECT` / `ARCHITECTURE` / `CONVENTIONS` / `DECISIONS` / `GOTCHAS` へ直接統合する前の監査記録が有用な場合だけ作成します。

- 複数案を試し、失敗理由が再利用できる
- root causeと有効な解決方法を証拠付きで残す価値がある
- 大きなmigrationの到達点や未完了条件を次taskへ渡す必要がある

一時的な進捗、command出力、会話、全変更file一覧、容易にGit/sourceから分かる内容は保存しません。

## 形式

- file名: `YYYY-MM-DD-short-topic.md`
- 目安: 40行以内。長くなる場合は恒久知識へ統合する。
- 最低限の見出し:

```markdown
# Topic

## Outcome
## Why / Evidence
## Failed approaches
## Reusable follow-up
```

- 同じtopicのentryを増やし続けず、既存entryを更新・統合する。
- 結論が安定したら適切な恒久docへ移し、Worklog側はリンクと最小監査情報だけ残す。
