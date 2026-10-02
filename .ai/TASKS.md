# Active Tasks

未完了テーマだけの入口。詳細はリンク先の該当節を読む。実装済みとUnity確認待ちを混同せず、確認完了後は一覧から削除する。

## 運用

- 関連機能を一回のセッションでまとめて実装し、静的確認を挟んで自律的に進める。小項目ごとに確認を求めない。
- 描画・操作などUnity確認が必要なまとまり、外部担当との仕様が必要な境界で確認事項をまとめる。
- 監査結果は既存項目へ統合する。完了履歴や会話ログは残さない。
- 失敗分岐は対象外。成功条件がそろうまで現在手順で待機する。

## 優先キュー

| 優先度 | テーマ | 次に達成する状態 | 詳細 |
|---|---|---|---|
| P0 | 教材復旧の原本保護・主要UIの状況認知 | managed確認済みの復旧保護とUI実装をUnityで直接確認する | [監査](BACKLOG/editor-app-audit.md) `REC-01/UIB-04..06`、[品質確認](../QUALITY.md) |
| P1 | タイヤ交換の操作表現 | 既存6条件での教材表現を確認する。工具回転・着脱の専用条件は対象外 | [教材計画](BACKLOG/tire-change-authoring-plan.md) |
| P1 | XR／Web連携・教員評価 | Web側完成と再開依頼を待つ。外部連携の実装はユーザー指定で保留 | [連携・評価](BACKLOG/research-integration-plan.md) |
| P1 | 操作回帰確認 | 詳細パネル、青い輪郭、X軸ギズモを両Sceneで確認する | [監査](BACKLOG/editor-app-audit.md) `UIB-*` |
| P1 | 3D編集・教材再利用 | グループ・graph複数選択/コピー/整列、教材再利用をUnity確認する | [操作確認](BACKLOG/viewport-polish-plan.md)、[監査](BACKLOG/editor-app-audit.md) `EDT/SCN-*` |
| P1 | 小画面・操作案内 | ノートPCで文字と操作領域を保ち、新機能の案内を整える | [監査](BACKLOG/editor-app-audit.md) `A11-*` |
| P1 | 性能・責務分割・自動検証 | 設定・UI生成を整理し、性能測定と退行検出を継続可能にする | [作業計画](BACKLOG/function-preserving-refactor-plan.md)、[監査](BACKLOG/editor-app-audit.md) `PRF/REF/TST-*` |
| P2 | カタログ・モデル取込 | 取消・段階表示・入力制限をUnity確認し、自動サムネイルと素材補正へ進む | [監査](BACKLOG/editor-app-audit.md) `IMP-*` |
| P2 | 高度な教材表現 | メディア、待機・反復、成功フィードバック等を必要性に応じて拡張する | [監査](BACKLOG/editor-app-audit.md) `ADV-*` |
