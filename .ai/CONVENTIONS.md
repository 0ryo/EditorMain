# Observed Conventions

ここには現在のコードから確認できたパターンだけを記録します。理想規約ではなく、変更時に既存コードと整合させるための観察結果です。

## C#・ファイル構成

- 独自namespaceとasmdefはなく、runtime scriptはUnity既定の `Assembly-CSharp` に入る。
- 主要class名とファイル名は一致する。小さなcommand/model/helper classは関連ファイルへ同居する場合がある。
- MonoBehaviour service/controller、static helper、`[Serializable]` public-field model、ScriptableObject registryを使い分ける。
- braces/空白は新旧コードで混在する。大規模な整形はせず、編集箇所の周辺styleに合わせる。
- Unity objectは `null` とUnityのtruthiness (`if (!registry)`) の両形式が存在する。

## 参照と初期化

- Inspector参照は `[SerializeField]` privateまたはpublic field。
- 参照切れ対策は `Ensure*` / `Resolve*`、`FindFirstObjectByType`、inactive探索、runtime生成の順で補完する実装が多い。
- `CommandService.I`、`EditModeService.I`、`IdGenerator.I` はstatic singleton。利用前の存在保証が必要。
- 後方互換のserialized fieldをコメント付きで残す例がある（例: `ScenarioGraphUI.nodeTemplate`、`StepNodeUI.conditionRowTemplate`）。削除前にPrefab互換を確認する。

## UI

- UI方式はuGUI + TextMeshPro。新規UIの正本は `UIRoot.prefab` とEditor builder。
- Hierarchy名はコードから検索されるためAPIに近い。主なprefixは `Panel_`、`Button_`、`Input_`、`Text_`、`Row_`、`Scroll_`、`Node_`、suffixは `_Template` / `Template`。
- 共有値は `DesignTokens`、色適用は `DesignTokenApplier`、角丸は `UiRoundedTheme`。
- Canvas基準解像度は `DesignTokens.ReferenceResolution` (`1920x1080`)。
- 表示stateはeventまたはpollingで同期する。例: `ModeChanged`、`PlacementTypeChanged`、`ObjectPlaced`、`OnSelectionChanged`、`PlacedObject.OnDisplayNameChanged`。
- UIからgraphを直接編集せず `CurriculumGraphService` を通す。一方、`PlacedObject` の表示名/説明は詳細UIからComponent APIへ反映する現行実装がある。

## データとID

- Unity `JsonUtility` 用modelはpublic field + `[Serializable]`。
- IDはzero-padded。`obj-0001`、`step-0001`、`cond-0001`、`act-001`。
- `typeId` はslash区切りの技術ID。runtime importは `Imported/<sanitized-name>_<ticks>`。
- schema/version fieldをexportに含める。現行Curriculum schemaとScenario exportはversion 2、placement exportはversion 1。

## 入力・platform分岐

- 編集マウス入力は `EditInput` を共通入口にし、legacy Inputを優先、Input Systemをfallbackにする。
- Editor APIは `#if UNITY_EDITOR`、Windows native dialogは `#if UNITY_STANDALONE_WIN` で囲む。
- text field focus中はcamera/mode shortcut等を抑止する。

## エラー処理とログ

- runtimeではguard clause + `Debug.LogError` / `LogWarning` / prefix付きdiagnostic logを使い、可能な箇所はfallbackを続行する。
- Editor automationは必要asset/referenceがない場合に例外を投げ、batch実行の失敗を検出可能にする。
- Scenario exportは検証errorで保存buttonを無効化し、例外はstatus textと `Debug.LogException` に出す。
- ファイル保存はcurriculum exportで `.tmp` を作成後、replace/moveする。

## テスト・検証

- プロジェクト固有のEditMode/PlayMode test、asmdef、Lint、CIは確認できていない。
- Codexの標準静的確認は `git diff --check`、対象pathの存在確認、`rg` による参照/旧値確認、必要に応じたJSON parse。
- Unity compile/runtime/buildの最終確認はユーザーが行う。失敗logを受けて修正する。

## ドキュメント

- 恒久知識は `.ai/` に統合し、作業日誌を連続追記しない。
- 詳細UI仕様は `Docs/worklog/worklog_UI/`、design sourceは `Docs/rules/design_rule.md`。
- 実装と文書が違う場合は推測で解決せず、差異を明示して正本を確認する。
