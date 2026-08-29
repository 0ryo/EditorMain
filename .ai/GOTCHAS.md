# Gotchas

## 検証環境

- CodexはUnity Editor/CLIを起動しない。compile、PlayMode、build、Prefab/Scene再保存は未検証のまま人間へ渡す。
- 独自test/asmdef/Lint/CIは未確認。Unity Test Framework packageがあることと、project testがあることは別。
- `Assembly-CSharp.csproj` は調査時点でruntime C# 44本中31本の現行sourceが欠落していた。`dotnet build` の失敗/成功をUnity compileの代用にしない。

## 文書と実装の既知差異

- 編集projectは `Application.persistentDataPath/Projects`、配布用Scenario/Placement JSONは `Application.persistentDataPath/Exports` に分離されている。`Docs/rules/scenario_rules.md` の `scenarios` 表記や旧UI仕様の `Assets/Exports` は古い。
- `Docs/worklog/worklog_UI/全体UI仕様.md` は日付順の追記文書で、古い「カード名のみ」仕様が後の3段カード仕様に置き換わっている。後半の新しい項目と現行codeを優先して突合する。
- 同UI仕様の一部（2026-03-03付近）と `BuildUiPrefabs.cs` の一部commentには文字化けがある。文字化け箇所だけを根拠に仕様を断定しない。

## UI / Prefab

- `SampleScene`はBuild Settingsで無効でもユーザーの通常作業・確認Scene。Main CameraとUIRootから`CatalogUI`が不足serviceとworkspace gridを補完するため、scene pathだけでruntime補完を止めない。
- `CatalogUI.cs` は約3,000行あり、Catalogだけでなくservice補完、edit mode、settings、new object dialog、importまで担うhotspot。小変更でもStart/wiring/runtime補完/importの影響を検索する。
- Prefab正本方針でもruntime `Ensure*` が多数ある。Hierarchy名を変えると `transform.Find`、name比較、blocking UI判定、builder、Prefabが同時に壊れる。
- `BuildUiPrefabs.Build()` は既存Prefabの局所patchではなく、新しい `UIRoot` GameObjectを構築して `SaveAsPrefabAsset` する。実行前にbuilderが全必要要素を生成するか確認する。
- Scene/Prefab YAMLには古いserialized fieldが残ることがある。例として現行classから削除済みの `floorMask` がScene YAMLに残っている。raw YAMLのfield存在だけでruntime依存を判断しない。
- EventSystemはSceneで1個が前提。`CatalogUI` は欠落時に `StandaloneInputModule` 付きで生成し、重複を無効化する一方、現行SceneはInput System UI moduleを持つ。入力module変更は両経路を確認する。

## 配置・選択・入力

- 表示Floorと配置面は別物。配置は`y=0` planeでXZをgrid snapした後、`PlacedObjectGrounding`がrenderer bounds下端を接地する。`placementYOffset`は旧serialized互換のためfieldだけ残り、配置Yには使わない。
- 配置objectにusable Colliderがないと選択できないため、`PlacedObjectPickability` がrenderer boundsからBoxColliderを追加する。rendererもないmodelは自動修復できない。
- `PrefabRegistry.LoadDefault()` は `#if UNITY_EDITOR` 内だけでAssetDatabaseから読む。Playerではserialized registry参照が正しく設定されているかが重要。
- `CommandService.I` がない配置は直接配置fallbackを持つが、他の操作にはsingleton前提箇所がある。service欠落を単純な入力bugと誤認しない。
- Both input settingでも `EditInput` のpreprocessor順でlegacy Inputが優先される。Input System action assetが存在しても編集操作がそのaction mapを直接利用するとは限らない。

## Scenario

- 保存可能条件は厳しい。Start/End各1、全Stepが単一のStart→…→End鎖、各Stepに1件以上かつ`RuleSet.maxConditionsPerStep`以下のCondition、各Conditionはちょうど1 Stepへbind、A/Bは別々の存在する `PlacedObject.id` が必要。上限既定値は8、許容設定範囲は1～32。
- 削除済みPlacedObject IDや不整合edgeは検証前に自動clearしない。欠損参照はE-10/E-11として残し、Conditionの差替え・削除または配置削除のUndoで解消する。Scenario検証の再評価はGraphChanged/CommandStack.HistoryChangedに連動する。
- 編集model (`Curriculum`) と出力model (`ScenarioExport`) は別。node title等を追加してもexportへ自動で出るとは限らない。
- 現行 `ConditionNodeData.DefaultTitle` は `手順1` で、UI上の `条件 n` 表記と一致しない。変更する場合はmigration/表示依存を確認する。

## モデル取込・platform

- Unity EditorではFBXをAssetDatabaseへimportできる。PlayerではFBX経路はなく、Windows native dialog + `.glb/.gltf` のみ。
- runtime imported modelはmemory上のPrefab map/card stateに登録されるだけで、`DefaultRegistry.asset` へ永続化されない。
- runtime project/exportは`Application.persistentDataPath`配下を使う。Editor限定FBX importのfile選択はOSのDocumentsを初期位置にし、project内path判定には`FileUtil.GetProjectRelativePath`を使う。Windows以外の保存実機確認はない。

## TMP

- 日本語fallbackは `TmpFontInitializer` がEditor/runtimeでTMP内部cacheやsubmeshを更新する。reflectionとTMP内部stateに依存するため、font修正はFallback assetだけでなくinitializerとTMP Settingsも確認する。
- 過去にTMP material cache/submesh更新中の例外が入力系調査を阻害した。現在はwarningへ落として継続する箇所があるため、warningを無関係として一括無視しない。
