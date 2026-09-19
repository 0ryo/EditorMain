"""Compare actual Unity fixture screenshots to the six supplied PNGs.
Requires Pillow as workstation tooling. Does not create substitute Unity captures.
"""
import json
from pathlib import Path
from PIL import Image, ImageChops, ImageDraw, ImageStat
root=Path(__file__).resolve().parents[2]/'Design/skillsync_codex_handoff'
captures=root/'verification/captures';out=root/'verification/comparisons'
refs=['01_layout.png','02 手順を編集 — UI.png','03 動作を確認 — UI.png',
      '04 配置する場所を選択 — UI.png','05 条件成立・手順完了 — UI.png','06 書き出し前の確認 — UI.png']
missing=[str(captures/f'{i+1:02}_unity.png') for i in range(6) if not (captures/f'{i+1:02}_unity.png').exists()]
if missing: raise SystemExit('Unity captures are missing; no comparison claimed.\n'+'\n'.join(missing))
out.mkdir(parents=True,exist_ok=True);results=[]
for i,name in enumerate(refs):
    ref=Image.open(root/name).convert('RGB');actual=Image.open(captures/f'{i+1:02}_unity.png').convert('RGB')
    if ref.size!=(1600,1000) or actual.size!=ref.size: raise SystemExit(f'Resolution mismatch: {name}')
    mask=Image.new('L',ref.size,255);ImageDraw.Draw(mask).rectangle((288,170,1135,719),fill=0)
    diff=ImageChops.difference(ref,actual)
    mean=sum(ImageStat.Stat(diff,mask).mean)/3
    diff.save(out/f'{i+1:02}_difference.png')
    pair=Image.new('RGB',(3200,1000));pair.paste(ref,(0,0));pair.paste(actual,(1600,0));pair.save(out/f'{i+1:02}_comparison.png')
    results.append(dict(state=i+1,uiMeanAbsoluteChannelError=mean,note='3D viewport excluded from metric; visual review and interactions still required.'))
(out/'results.json').write_text(json.dumps(results,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps(results,indent=2))
