# Worklog Operating Rules

このファイルは旧 `Docs/worklog/worklog_latest.md` 運用との互換入口です。現在の正本は `AGENTS.md` と `.ai/WORKLOG/README.md` です。

- 新規chatで旧Worklogを自動的に全文読込しない。最初に `.ai/INDEX.md` を読み、taskに関係する場合だけ対象sectionを読む。
- 作業終了時は、将来の別taskで再利用価値がある情報だけを既存の `.ai/` 文書へ統合する。
- raw会話、command出力、逐次進捗、一時的な変更一覧を保存しない。
- 独立したWorklogが必要な場合だけ `.ai/WORKLOG/YYYY-MM-DD-short-topic.md` を作り、40行以内を目安にOutcome / Evidence / Failed approaches / Reusable follow-upを残す。
- 同じ知識を追記し続けず、既存記述を更新・統合する。古い `Docs/worklog/` は履歴調査時だけ参照する。
