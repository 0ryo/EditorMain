"""Extract ONLY four absent font glyphs from supplied reference PNGs.
All editable words remain TMP text. Coordinates/advances come from cached Figma glyphs.
"""
import json,math
from pathlib import Path
from PIL import Image
root=Path(__file__).resolve().parents[2];source=root/'Design/skillsync_codex_handoff';out=root/'Assets/UI/SkillSyncDesign'
nodes=json.loads((source/'data/nodes.json').read_text(encoding='utf-8'))['nodes']
raw={f"{n['guid']['sessionID']}:{n['guid']['localID']}":n for n in json.loads((source/'data/raw_nodes.json').read_text(encoding='utf-8'))['nodes']}
missing='↶↷⌄⌕';glyphs={};sources={}
for character in missing:
    n=next(n for n in nodes if n.get('boardKey') in ('01_layout','02_steps') and n.get('text',{}).get('characters','').startswith(character))
    board=n['boardKey'];png='01_layout.png' if board=='01_layout' else '02 手順を編集 — UI.png'
    g=raw[n['id']]['derivedTextData']['glyphs'][0]
    advance=g['advance']*g['fontSize'];x=n['screenPosition']['x'];y=n['screenPosition']['y'];h=n['size']['y']
    box=(math.floor(x),math.floor(y),math.ceil(x+advance),math.ceil(y+h))
    image=Image.open(source/png).convert('RGB').crop(box)
    bg=image.getpixel((0,0));ink=n['fills'][0]['color'];fg=tuple(round(ink[c]*255) for c in ('r','g','b'))
    channel=max(range(3),key=lambda i:abs(bg[i]-fg[i]))
    pixels=[]
    for p in image.getdata():
        alpha=max(0,min(255,round((bg[channel]-p[channel])*255/(bg[channel]-fg[channel]))))
        pixels.append((255,255,255,alpha))
    result=Image.new('RGBA',image.size);result.putdata(pixels)
    file=f'symbol_{ord(character):04x}.png';result.save(out/'Sprites'/file)
    glyphs[character]=dict(sprite=file,advance=advance,width=image.width,height=image.height,offsetX=box[0]-x,offsetY=box[1]-y)
    sources[character]=dict(nodeId=n['id'],reference=png,crop=box,advance=advance)
(out/'symbols.json').write_text(json.dumps(dict(glyphs=[dict(character=c,**v) for c,v in glyphs.items()]),ensure_ascii=False,indent=2),encoding='utf-8')
(source/'verification').mkdir(exist_ok=True)
(source/'verification/symbol-sources.json').write_text(json.dumps(sources,ensure_ascii=False,indent=2),encoding='utf-8')
print('Extracted 4 source glyphs; no words or complete UI regions were rasterized.')
