# External Knowledge Bootstrap

## Outcome

- 小さい `AGENTS.md`、選択読込用INDEX、事実/architecture/conventions/decisions/gotchasを分離した。
- 旧 `Docs/worklog/worklog_latest.md` の常時読込をやめ、必要taskだけ参照するrouteへ変更した。

## Why / Evidence

- 2026-08-20時点のcurrent branchで、tracked file 316、C# 47本・約15,018行、`Docs` Markdown 20本・約3,075行を目録化した。
- README、Unity/Package/Build/Input/Quality設定、主要Scene/Prefab、全C#宣言と主要hotspot、既存文書見出し/現行rules、130 commitsの履歴を確認した。
- Unity versionは `6000.2.6f2`。project固有test/Lint/CIは未確認。

## Failed approaches

- なし。確認不能な事項は推測で埋めず、未確認または既知差異として分離した。

## Reusable follow-up

- architectureや依存が変わったら該当docを更新し、このentryへ逐次logを足さない。
- 保存先・corner radius・runtime UI補完などの差異は `GOTCHAS.md` で追跡し、ユーザー判断またはcode変更で解消した時に統合する。
