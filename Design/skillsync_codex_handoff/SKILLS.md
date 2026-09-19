# Skills：最小構成と導入

確認日：2026-09-19。以下は公開リポジトリの説明とSkill本文を確認した案です。ユーザーのPCへのインストールは実行していません。既に同等Skill/プラグインが導入済みなら重複導入は不要です。

## 1. uGUIのプロジェクトなら Unity公式 `ui-ugui`

Canvas、RectTransform、Layout Group、Prefab UIの読み取り・編集・生成を扱います。

```bash
npx skills add Unity-Technologies/skills --skill ui-ugui --agent codex
```

公開元： https://github.com/Unity-Technologies/skills
Skill本文： https://raw.githubusercontent.com/Unity-Technologies/skills/main/skills/ui-ugui/SKILL.md

UI方式を調べた結果がUI Toolkitなら `ui-uitk` を使い、uGUIへ無理に移行しません。前回説明のプラグイン名に依存せず、今回は公開が確認できたこの公式Skillsリポジトリから導入できます。

## 2. Unityを直接操作するなら Unity公式 `unity-cli`

```bash
npx skills add Unity-Technologies/skills --skill unity-cli --agent codex
```

Skill本文： https://raw.githubusercontent.com/Unity-Technologies/skills/main/skills/unity-cli/SKILL.md

**SkillとCLI実行プログラムは別です。** ライブEditor操作には対応するUnity CLIとプロジェクト側の `com.unity.pipeline` が必要です。公開SkillはPipelineをUnity 6.0+向けと記載しています。既存環境で `unity --version`、`unity status` を確認し、不足する場合はインストール済みSkillの手順に従います。CLI導入例はbetaチャネルを使うため、導入時に内容を確認してください。古いUnityをこの目的だけで無断更新しません。

既に動作中のUnity MCPがあるなら、その経路を活かしても構いません。CLIと複数MCPを同時に追加することを必須にはしません。手動実行する構築用Editorスクリプトという経路もありますが、未実行なら検証済みにはできません。

## 3. 同梱のプロジェクト専用 `skillsync-ui-reproduction`

今回の抽出資料の読み方、6状態、文字継承、3Dとの区別、検証ルールをまとめた**この資料用に作成した独自Skill**です。Unity公式やOpenAI公式のSkillではありません。

同梱 `.agents/skills/skillsync-ui-reproduction/SKILL.md` をUnityプロジェクトルートの同じ相対パスへコピーします。既存 `.agents` フォルダー全体を上書きせず、このサブフォルダーだけ追加してください。

Codexへの指定例： `$skillsync-ui-reproduction` を使用して、`Design/skillsync_codex_handoff/CODEX_PROMPT.md` の実装依頼を実行してください。

CodexのローカルSkill読み込み先： https://developers.openai.com/codex/skills/

## 4. Figmaへの追加アクセスができるときだけ `figma-implement-design`

```bash
npx skills add openai/skills --skill figma-implement-design --agent codex
```

Skill本文： https://raw.githubusercontent.com/openai/skills/main/skills/.curated/figma-implement-design/SKILL.md

このSkillはFigma MCPからデザインと画像を取得するフローを前提にしています。**今回のローカル抽出資料を使うだけなら必須ではありません。** Figmaの修正を後から追う、公式画像と照合する、追加ノードを取得する場合の候補です。Skillの追加だけでプラン上限は解消しません。React/Tailwindの参照出力をUnityへ読み替え、Reactアプリの作成を中間工程として追加しません。

`--skill` と `--agent codex` の選択構文： https://github.com/vercel-labs/skills

## 導入しないもの

見た目を再設計する汎用frontend/design Skill、Webアプリを自動生成するSkill、画像をそれらしく描き直すSkillは今回の必須構成に含めません。必要なのはこのデザインの数値・状態を維持することです。追加Skillsは内容を読み、実行権限や外部送信の必要性を確認してから使用します。
