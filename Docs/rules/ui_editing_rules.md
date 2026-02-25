# UI編集ルール

## 1. UI共通の設計規約
- UIは「見た目」と「状態/ロジック」を分離する。
  - View（表示）: `UI/*` 名前空間 or `UI/Views/*`
  - State/Logic: `UI/State/*` or `UI/Controllers/*`
- "UIから直接データを書き換える"は禁止。必ずService/UseCase層（またはそれに相当）を経由する。
- UI調整は「既存のPrefab/Scene構造を維持」し、必要最小限の追加に留める。

## 2. UI Toolkitの場合（UXML/USS）
- 追加/変更は原則として：
  - レイアウト：`.uxml`
  - 見た目：`.uss`
  - つなぎ込み：`.cs`（Viewの初期化、イベント購読、バインド）
- USSは「局所化」する。グローバルに効くセレクタは極力避ける。
- 既存のクラス命名規約があるならそれに合わせる（例：BEM風、kebab-case等）。

## 3. uGUIの場合（Canvas/Prefab）
- **新規UI作成はCanvas（uGUI）で実装すること**（コードでのUI階層動的生成を正としない）。
- "Scene直置き"を増やさない。可能ならUIはPrefab化して参照で差し込む。
- RectTransform/アンカー/スケールは既存規約を維持。
- ボタン/入力/リストなどは共通コンポーネント化を優先（同じ見た目・同じ挙動を量産しない）。
- UIを実装/調整する前に `Docs/worklog_UI/` を確認し、実装後に仕様差分が出た場合は同ディレクトリの文書を更新する。

## 4. Scene/Prefabを更新する必要がある場合（重要）
- 直接YAML編集は禁止（最終手段）。
- 代わりに `Assets/Editor/Automation/` に **Editorスクリプト**を作り、
  - 対象Scene/Prefabを `AssetDatabase` 経由でロード
  - 必要なGameObject/Componentを追加・参照をセット
  - `PrefabUtility.SaveAsPrefabAsset` もしくはScene保存
  - 変更点をログ出力
  という形で "Unityに正しい参照を書かせる"。
