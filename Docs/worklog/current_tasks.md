# オブジェクト詳細パネル — タスク一覧

ワールド上のオブジェクトを選択すると画面右側に自動表示され、選択解除で自動非表示になる詳細パネルを実装する。

---

## ✅ Task 1 — SelectionService: 選択変更イベント追加（完了）
## ✅ Task 2 — CatalogUI: メタデータ公開 API 追加（完了）
## ✅ Task 3 — ObjectDetailPanel.cs: スクリプト作成（完了）
## ✅ Task 4 — BuildUiPrefabs.cs: 詳細パネル生成処理追加（完了）
## ✅ Task 5 — DesignTokenApplier.cs: ApplyDetailPanel 実装（完了）

---

## 残作業（人間操作が必要）

- `Tools > Automation > Build UI Prefabs` を実行して UIRoot.prefab を再生成する。

---

## 人間確認チェックリスト
- [ ] `Tools > Automation > Build UI Prefabs` を実行すると `UIRoot.prefab` に `Panel_Detail` が追加される。
- [ ] ワールド上のオブジェクトをクリックすると画面右側にパネルが表示される。
- [ ] パネルに「プレファブ名」「オブジェクト名」が正しく表示される。
- [ ] 説明文があるオブジェクトでは「説明」行も表示される。
- [ ] 説明文がないオブジェクト（レジストリ由来）では「説明」行が非表示になる。
- [ ] オブジェクトの選択を解除するとパネルが非表示になる。
- [ ] パネルの色がデザイントークン準拠（背景 BgPrimary、ヘッダー Surface、見出し TextSecondary、値 TextPrimary）になっている。
