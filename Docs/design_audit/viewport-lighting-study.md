# 視点に追従するビューポート照明の調査と導入設計

調査日：2026-09-10。対象：Unity 6000.2.6f2／URP 17.2.0、EditorMainの編集用3Dビューポート。実装前の設計であり、描画結果と性能は未検証。

## 1. Blenderで起きていること

BlenderのSolid表示はWorkbenchによる編集向けの簡略化した描画で、シーンに配置したLightを使う最終レンダリングとは別である。Studio照明は視点に追従でき、World Space Lightingを有効にするとワールド側に固定される。したがって、シーンにLightを配置していなくても面に明暗が出る。[Blender Workbench](https://docs.blender.org/manual/en/latest/render/workbench/index.html)、[Blender Viewport Shading](https://docs.blender.org/manual/en/4.2/editors/3dview/display/shading.html)

添付画像だけではStudioプリセットやShadow／Cavityの有効状態は特定できない。ユーザーが求める「視点に合わせて面の見え方が変わり、形を把握しやすい表示」を再現対象とし、Blender内部のレンダラーをそのまま移植する必要はない。

| 表現 | 役割 | 今回の扱い |
|---|---|---|
| 面の明暗 | 面の向きに応じた明るさで立方体の面などを区別 | 最初に改善する。投影影だけを追加して済ませない |
| 投影される影 | 物体が光を遮って他の面や自身へ作る影 | 必要に応じて主光に追加する |
| Cavity | 稜線や溝を強調する編集用表現 | 面の明暗が整った後の補助候補 |
| MatCap | 画像に記録した材質・照明を視点基準で参照する方式 | 灰色の形状確認モードの代案。元の材質表現との両立に追加設計が必要 |

WorkbenchのShadowには独立した方向設定があり、Cavityは稜線と谷を強調する。Studio追従とすべての投影影の追従を同じ仕組みと断定しない。[Blender Options](https://docs.blender.org/manual/en/latest/render/workbench/options.html)。MatCapの概要は[Blender Lighting](https://docs.blender.org/manual/en/3.6/render/workbench/lighting.html)を参照。

## 2. 現行プロジェクトで確認した事実

| 調査先 | 確認内容と意味 |
|---|---|
| `Packages/manifest.json` | URP 17.2.0が導入済み。新たな描画Packageは不要 |
| `ProjectSettings/QualitySettings.asset` | 各品質設定がGUID `681886c5eb7344803b6206f758bf0b1c` のURP assetを指定 |
| `Assets/Settings/UniversalRP.asset` | 上記GUIDのasset。既定Renderer indexは0で、Renderer2DのGUID `424799608f7334c24bf367e4bbfa7f9a` を参照 |
| `Assets/EditorMain.unity`、`Assets/Scenes/SampleScene.unity` | CameraのRenderer indexは-1、つまり既定参照。静的設定では編集Cameraも2D Rendererを使用する構成 |
| `Assets/New Universal Render Pipeline Asset_Renderer.asset` | UniversalRendererDataが別に存在し、Renderer Featuresは空。この存在だけで使用中とは判断できない |
| `Assets/Settings/UniversalRP.asset` | 主光の影は許可、Soft Shadowsは無効。これはasset側の許可であり、実際に影が描かれることの保証ではない |
| `WorkspaceFloorGrid.cs` | `Floor_Surface`として色付きQuadを生成。線と軸は別生成。下面の視界問題は照明だけでは解決しない |
| `MoveTool`等 | ギズモは専用の非照明材質を使用。照明改善から分離して見た目を維持する必要がある |

「3D物体が平坦に見える原因がライトだけ」という前提では進めない。実行時のCamera Renderer、各materialのshader、法線、ambient、Lightを確認する。特に2D Rendererから3D向けUniversal Rendererへの切替を先に検証する。実行時の上書きと取込素材ごとのshaderは未確認なので、ここでは唯一の原因とは断定しない。

## 3. 採用する初期案

既存URPのUniversal Rendererと、編集Cameraの向きに追従する主Directional Light＋弱い補助光を使う。モデルの色・テクスチャを保持しつつ、斜めからの明暗で面を区別する。真っ正面からの光だけでは正面の面が一様になりやすいので避ける。

提案する責務は `ViewportLightingController`（新設予定）。編集Cameraの解決は既存`EditWorkspace`の入口を利用し、Cameraの回転更新後に追従させる。概念式は `light.rotation = camera.rotation * studioLocalRotation`。Directional Lightのforwardは光線の進行方向なので、実装時に立方体で左右・上下の符号を確認する。カメラ移動と照明更新の実行順を指定し、1フレーム遅れを防ぐ。

初期試作では主光を視点の斜め上30〜45度程度、補助光を主光の15〜30%程度の強さから調整する。数値は本システム向けの提案でありBlender既定値ではない。補助光の影は無効、主光の影は段階的に有効化する。真下から見ても裏側全体が黒つぶれしないよう、低い環境光も比較する。

照明は編集支援用Prefab／サービスとして管理し、教材のPlacedObject・条件候補・保存・モデル同梱出力へ混入させない。モデルassetや共有materialを直接書き換えず、必要な補正は編集表示用の複製・一時設定に限定して解除時に復元する。初期化を繰り返してもLightを重複生成しない。既存のLightとの二重照明も検査する。

## 4. 導入手順

### L0：描画経路を確定する

Editor APIの診断で実行CameraのURP Renderer、モデルのshader名、Rendererの影設定、meshのnormal有無、シーンのLight・ambientを記録する。SampleSceneとEditorMainの両方が対象。

専用UniversalRendererDataを既存URP assetへ追加し、編集Cameraに明示指定する案を優先する。2D Rendererの既存indexを壊さず、UI Cameraとのstacking互換を確認する。共用が難しければ専用URP assetへの切替を比較し、全品質設定への影響を記録する。変更はEditor APIによる冪等なAutomationで行い、元の参照へ戻せるようにする。

### L1：面の明暗を整える

Lit対応materialで立方体→Suzanne相当→車→ナットを確認する。Unlit材質はLightを置いても同じ反応にはならない。既存の色・テクスチャ・透過・metallic／smoothness／normal mapを失う一律shader置換は避ける。flat／smooth normalの違いも確認し、元モデルのhard edgeを無断で再計算しない。[Unity URP Lit](https://docs.unity3d.com/6000.2/Documentation/Manual/urp/lit-shader.html)

### L2：自己影と近接部の陰影を追加する

主光のshadow、Rendererのcast／receive、距離・bias・解像度を確認してからSoft Shadowsを試す。ナットのような小物でshadow acneや影が浮く現象を確認する。

必要ならUniversal RendererへSSAO Renderer Featureを追加する。SSAOは溝・穴・近接面を暗くする機能で、Volume追加だけで導入するものではない。面の方向による明暗やBlenderの稜線強調全体の代わりにはならない。[Unity SSAO概要](https://docs.unity3d.com/6000.2/Documentation/Manual/urp/post-processing-ssao.html)

SSAOの法線／深度入力と品質・半解像度等の候補を比較し、GPU時間を計測する。透過窓・細い部品の縁・画面端のハローを確認する。独自Cavity shaderは標準機能で足りない場合のみ次段階とし、URP 17のRender Graph対応と保守コストを先に見積もる。

### L3：透明なXZ面と統合する

グリッドの線以外の面描画をなくし、深度にも不透明な床を残さない。通常の不透明床への落ち影はこの状態では表示できないため、形状把握は面の明暗・自己影・部品間の遮蔽を中心とする。影を受けるために不透明床を再導入しない。影だけの床が必要になった場合は別提案とし、下面からの可視性と両立させる。

## 5. 受入確認

- 同じ立方体を周回して、隣接する面の明るさが識別でき、画面の斜め上を基準とする照明方向が保たれる。
- 車の下面、ホイール、ナット、重なった部品が黒一色にならず、床面にも遮られない。
- 明るい／暗いmaterial、透過窓、flat／smooth normal、透視／平行投影、近距離／遠距離を比較する。
- マウスorbit、定型視点、カメラreset後に照明の遅れや急な反転がない。
- グリッド、ギズモ、uGUI/TMP、選択枠の色・視認性とクリック判定が維持される。
- 同じ解像度・視点・品質設定でCPU/GPU時間を導入前後比較する。対象PCで許容できる値を試作後に定める。
- 保存・再読込・複製・Undo・配布JSONに表示専用Lightや一時materialが混入しない。

Unityのコンパイル・PlayMode・Playerと画像比較はユーザーが行う。既存モデルの見え方が不安定な状態でSSAO等を重ねず、各段階を個別に切り戻せる形で進める。
