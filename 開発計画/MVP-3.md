# UI改善 フェーズA：画面レイアウト骨格を作る（uGUI）

### [フェーズのゴール]

Gameビューで、UIidea.png と同じ5領域が見える状態にする。

- 上：メニューバー（表示のみ）
- 左：プレハブ一覧（検索・＋・スクロール枠まで）
- 中央：3D表示領域（枠だけ）
- 右：インスペクタ（枠だけ）
- 下：カリキュラムフロー（枠＋右上＋ボタンまで、内容はダミーでOK）
- 最下部：ステータスバー（表示のみ）

- 参照：~/EditorMain/開発計画/UI案.png

---

## A-1：既存UIを退避して、V2用ルートを作る

- [ ]  **Hierarchy** で `Canvas` を選択
- [ ]  `Canvas` を右クリック → **Create Empty**
- [ ]  作成された空オブジェクトを `UIRoot_V2` にリネーム
- [ ]  `UIRoot_V2` を選択 → Inspectorで **RectTransform** を確認
    - [ ]  Anchor Min = (0,0), Anchor Max = (1,1)
    - [ ]  Left/Right/Top/Bottom = 0（全画面フィット）

> 既存のUIがすでにCanvas配下にある場合は、見た目が被るので 既存UIをLegacyUIのような親の下にまとめて非アクティブにしておくのが安全です（このフェーズでは「UIRoot_V2だけが表示される」状態が目標）。
> 

---

## A-2：上部メニューバー（表示だけ）を作る

- [ ]  `UIRoot_V2` を右クリック → **UI > Panel**
- [ ]  Panel を `TopMenuBar` にリネーム
- [ ]  `TopMenuBar` の RectTransform を設定
    - [ ]  Anchor Min = (0,1), Anchor Max = (1,1)
    - [ ]  Pivot = (0.5,1)
    - [ ]  Height（Size Delta Y）を **40** 前後に設定（好みでOK）
    - [ ]  Pos Y = 0
- [ ]  `TopMenuBar` を右クリック → **UI > Text**
- [ ]  Text を `TopMenuText` にリネーム
- [ ]  `TopMenuText` の Inspector（Text）で設定
    - [ ]  Text = `ファイル 編集 表示 ウィンドウ ヘルプ`
    - [ ]  Alignment = Left / Middle
    - [ ]  Font Size は見やすい値に（例：18）
- [ ]  `TopMenuText` の RectTransform
    - [ ]  Anchor Min = (0,0), Anchor Max = (1,1)
    - [ ]  Left=20, Right=20, Top=0, Bottom=0

---

## A-3：左パネル（プレハブ一覧）を作る

- [ ]  `UIRoot_V2` を右クリック → **UI > Panel**
- [ ]  `LeftPanel_PrefabList` にリネーム
- [ ]  RectTransform を設定（左固定幅）
    - [ ]  Anchor Min = (0,0), Anchor Max = (0,1)
    - [ ]  Width（Size Delta X）を **420** 前後（UI案に合わせて）
    - [ ]  Top は `TopMenuBar` 分だけ下げる（例：Top = -40）
        
        ※RectTransformの上側余白として調整
        

### 左パネル内：ヘッダー（タイトル＋＋ボタン）

- [ ]  `LeftPanel_PrefabList` を右クリック → **UI > Text** → `LeftTitle`
    - Text = `プレハブ一覧`
    - Anchor Min=(0,1), Anchor Max=(1,1), Height=40, Left=20
- [ ]  `LeftPanel_PrefabList` を右クリック → **UI > Button** → `Btn_AddPrefab`
    - RectTransform：Anchor Min=(1,1), Anchor Max=(1,1)
    - Pos X = -30, Pos Y = -20, Size = 32x32（目安）
- [ ]  `Btn_AddPrefab` の子 `Text` を選択 → Text = `+`

### 左パネル内：検索欄（見た目だけ）

- [ ]  `LeftPanel_PrefabList` を右クリック → **UI > Input Field** → `Input_SearchPrefab`
    - RectTransform：Anchor Min=(0,1), Anchor Max=(1,1)
    - Pos Y をタイトルの下に（例：Y=-70）、Height=36、Left=20、Right=20
    - Placeholder テキスト = `プレハブを検索...`

### 左パネル内：スクロール一覧（枠だけ）

- [ ]  `LeftPanel_PrefabList` を右クリック → **UI > Scroll View** → `Scroll_PrefabList`
    - RectTransform：Anchor Min=(0,0), Anchor Max=(1,1)
    - Top を検索欄の下に、Bottom=20、Left=20、Right=20 で余白調整
- [ ]  この時点では Content にダミー要素を入れなくてOK（次フェーズで動的生成）

---

## A-4：中央（3D領域の枠）を作る

- [ ]  `UIRoot_V2` を右クリック → **UI > Panel** → `CenterPanel_ViewFrame`
- [ ]  RectTransform を設定（中央は可変）
    - [ ]  Anchor Min = (0,0), Anchor Max = (1,1)
    - [ ]  Left = 左パネル幅分（例：440）
    - [ ]  Right = 右パネル幅分（例：440）
    - [ ]  Top = -40（TopMenu分）
    - [ ]  Bottom = 下フロー＋ステータス分を確保（例：260）

> ここは「3DカメラのGameView領域そのもの」ではなく、**UI案の“枠”**を作るだけです。実際の3Dは背面に見えます。
> 

---

## A-5：右パネル（インスペクタ枠）を作る

- [ ]  `UIRoot_V2` を右クリック → **UI > Panel** → `RightPanel_Inspector`
- [ ]  RectTransform（右固定幅）
    - [ ]  Anchor Min=(1,0), Anchor Max=(1,1)
    - [ ]  Width=420 前後
    - [ ]  Top=-40（TopMenu分）
    - [ ]  Bottom=ステータス分（例：40）
- [ ]  `RightPanel_Inspector` を右クリック → **UI > Text** → `RightTitle`
    - Text=`インスペクタ / プロパティ`
    - Anchor Min=(0,1), Anchor Max=(1,1), Height=40, Left=20

> 中身（id/position/scale + 適用ボタン）は次フェーズで“機能込み”で作ります。ここでは枠だけ。
> 

---

## A-6：下フロー（カリキュラムフロー枠＋＋ボタン）を作る

- [ ]  `UIRoot_V2` を右クリック → **UI > Panel** → `BottomPanel_CurriculumFlow`
- [ ]  RectTransform
    - [ ]  Anchor Min=(0,0), Anchor Max=(1,0)
    - [ ]  Height=220 前後（UI案に合わせて）
    - [ ]  Left=左パネル幅分（例：440）
    - [ ]  Right=右パネル幅分（例：440）
    - [ ]  Bottom=ステータスバー分（例：40）

### タイトル＋右上＋ボタン

- [ ]  `BottomPanel_CurriculumFlow` を右クリック → **UI > Text** → `FlowTitle`
    - Text=`カリキュラムフロー`
    - Anchor Min=(0,1), Anchor Max=(1,1), Height=40, Left=20
- [ ]  `BottomPanel_CurriculumFlow` を右クリック → **UI > Button** → `Btn_AddFlowNode`
    - Anchor Min=(1,1), Anchor Max=(1,1)
    - Pos X=-30, Pos Y=-20, Size=32x32
    - 子Text=`+`

### フロー内の“黒いキャンバス”枠（ダミー）

- [ ]  `BottomPanel_CurriculumFlow` を右クリック → **UI > Panel** → `FlowCanvas_Dummy`
    - Anchor Min=(0,0), Anchor Max=(1,1)
    - Top=-40（タイトル分）、Left=20、Right=20、Bottom=20
- [ ]  色を暗めに（見た目合わせのため。任意）

---

## A-7：最下部ステータスバーを作る（表示だけ）

- [ ]  `UIRoot_V2` を右クリック → **UI > Panel** → `StatusBar`
- [ ]  RectTransform
    - [ ]  Anchor Min=(0,0), Anchor Max=(1,0)
    - [ ]  Height=40
    - [ ]  Left=0, Right=0, Bottom=0
- [ ]  `StatusBar` を右クリック → **UI > Text** → `StatusText`
    - Text=`モード：置く｜選択：なし`
    - Anchor Min=(0,0), Anchor Max=(1,1)
    - Left=20, Right=20

（スナップ表示は入れない：あなたの仕様どおり）

---