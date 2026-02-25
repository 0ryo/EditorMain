# シナリオ作成機能：ルール

## 1. 設計と権限（Permissions）🔐

### 1.1 最初の実装は「追加権限ゼロ」で成立させる（最優先）
シナリオデータの保存先は原則：
- `Application.persistentDataPath` 配下
- 形式は JSON（推奨）または ScriptableObject（用途次第）
- "ユーザーが任意の場所を指定して保存" は後回し（＝権限・OS差分が増える）

> persistentDataPath は「実行間で保持したいデータの保存先」としてUnityが提供する標準パス。  
> まずここに閉じれば、多くのケースで追加のOS権限が不要になる。  

### 1.2 将来拡張で権限が増えるパターン（必要になった時だけ）
次の機能を入れる場合は、プラットフォームごとの設定が必要になる可能性がある。
- 端末の **カメラ/マイク/位置情報** を使う（iOSはInfo.plistのUsage Descriptionが必要）
- 端末の **外部ストレージ** に直接保存する（特にAndroidは制約が強い）
- **クラウド同期**（ネットワークアクセス、認証/OAuth、ATS/証明書設定など）

Codexは以下の手順で進めること：
1) 「なぜそれが必要か」を説明（代替案：persistentDataPathで足りないか？）
2) 対象プラットフォームを列挙（Standalone / Android / iOS / WebGL等）
3) Unity側の設定箇所を提示してから実装

### 1.3 iOS系の注意
- 機密情報/デバイス機能にアクセスする場合、iOSは許可文言（Usage Description）が必要になる。
- UnityはPlayer SettingsにUsage Descriptionを追加すると、Info.plistへ反映される想定で進める。

### 1.4 Android系の注意
- AndroidはManifestに権限が入る場合がある。
- 追加する場合は `Assets/Plugins/Android/AndroidManifest.xml`（またはUnityの生成/カスタム手順）を扱う。
- ただし、最初のMVPは「persistentDataPathへ保存」方針でManifest変更を発生させない。

---

## 2. 実装の最低ライン（MVP）🧱

### 2.1 データモデル（必須）
- Scenario（シナリオ）
  - id（安定ID）
  - title
  - description（任意）
  - steps[]（配列）
- Step（手順/ページ）
  - id
  - type（例：Text / Choice / Action / Wait など。最初はTextだけでもよい）
  - payload（本文、選択肢、パラメータ等）
  - next（遷移。最初は線形でもよい）

### 2.2 保存/読み込み（必須）
- 保存：persistentDataPath 配下に `scenarios/*.json`
- 読み込み：起動時 or 画面表示時に一覧をロード
- 破損対策：
  - 書き込みは一時ファイル → 原子的リネーム
  - JSONのスキーマバージョンを持たせる（`schemaVersion`）

### 2.3 編集UI（必須）
- 一覧（作成/複製/削除/検索は後回し可）
- 編集（title + steps編集）
- プレビュー（ランタイム上で再生できる簡易プレビュー）

### 2.4 UI統合（必須）
- 既存UI/画面遷移に「シナリオ作成」導線を追加
- 既存のInput/VR操作規約を崩さない（必要なら既存の操作体系に合わせる）
