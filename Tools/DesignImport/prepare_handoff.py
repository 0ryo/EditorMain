"""Reduce the local Figma handoff without rounding measurements. No Unity dependencies.

Run with Python; rasterize the returned SVG list with rasterize.mjs afterwards.
The original 3D illustration is deliberately excluded from the application UI.
"""
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / 'Design/skillsync_codex_handoff'
OUT = ROOT / 'Assets/UI/SkillSyncDesign'
OUT.mkdir(parents=True, exist_ok=True)
nodes = json.loads((SOURCE / 'data/nodes.json').read_text(encoding='utf-8'))['nodes']
by_id = {n['id']: n for n in nodes}
assets = {n['nodeId']: n for n in json.loads((SOURCE / 'assets/manifest.json').read_text(encoding='utf-8'))['assets']}
keys = ['01_layout', '02_steps', '03_preview', '04_placement_ghost', '05_completed', '06_export_validation']
children = {}
for n in nodes:
    children.setdefault(n.get('parentId'), []).append(n)
for group in children.values():
    group.sort(key=lambda n: n.get('childOrder', ''))

def traverse(n):
    yield n
    for child in children.get(n['id'], []):
        yield from traverse(child)

def ancestors(n):
    while n.get('parentId') in by_id:
        n = by_id[n['parentId']]
        yield n

def field_role(x, y, state):
    if (x, y) == (276, 18): return 'project'
    if state in (0, 3):
        if (x, y) == (1198, 247): return 'name'
        if (x, y) == (1198, 551): return 'height'
        if (x, y) == (1406, 551): return 'angle'
    if state in (1, 5):
        if (x, y) == (1184, 146): return 'stepTitle'
        if (x, y) == (1200, 249): return 'body'
        if (x, y) == (1214, 652): return 'distance'
        if (x, y) == (1404, 652): return 'hold'
    return ''

def role(x, y, text, state):
    if (x,y)==(320,673): return 'viewportMode'
    if (x, y) == (276, 48): return 'saved'
    if (x, y) == (24, 974): return 'footer'
    if (x, y) == (302, 740): return 'status'
    if (x, y) == (414, 786): return 'objectCount'
    if (x, y) == (24, 156): return 'stepCount'
    if (x, y) == (288, 145): return 'workspaceHint'
    if (x, y) == (1184, 118): return 'inspectorLabel'
    if state in (0, 3) and (x, y) == (1184, 148): return 'selectionTitle'
    if state in (1, 5):
        if (x, y) == (1184, 354): return 'conditionHeading'
        if (x, y) == (1276, 454): return 'objectA'
        if (x, y) in ((1276, 552), (1214, 553)): return 'objectB'
        if (x, y) == (1200, 788): return 'conditionSummary'
    if state in (2, 4):
        if (x, y) == (1196, 120): return 'trialStep'
        if (x, y) == (1184, 217): return 'trialBody'
        if (x, y) == (1184, 245): return 'trialBody2'
        if (x, y) == (1204, 331): return 'distanceStatus'
        if (x, y) == (1204, 369): return 'distanceValue'
        if (x, y) == (1262, 390): return 'distanceLimit'
        if (x, y) == (1204, 477): return 'holdStatus'
        if (x, y) == (1204, 514): return 'holdValue'
        if (x, y) == (1275, 534): return 'holdLimit'
        if (x, y) == (1184, 643): return 'trialNote'
        if (x, y) == (1184, 667): return 'trialNote2'
        if (x, y) == (1184, 785): return 'log1'
        if (x, y) == (1184, 817): return 'log2'
        if (x, y) == (308, 838): return 'learner'
    if state == 5:
        return {(534,307): 'errorCount', (522,401): 'errorSummary', (522,429): 'draftStatus',
                (540,501): 'errorTitle', (540,535): 'errorDetail'}.get((x,y), '')
    return ''

merged, buttons, fields = {}, {}, {}
fixture_merged, fixture_fields = {}, []
used = {}
for state, key in enumerate(keys):
    board = next(n for n in nodes if n.get('boardKey') == key and n['id'] == n.get('boardId'))
    for order, n in enumerate(traverse(board)):
        if n.get('visible') is False or any(a.get('visible') is False for a in ancestors(n)): continue
        p, size = n.get('screenPosition', {}), n.get('size', {})
        x,y,w,h = p.get('x',0),p.get('y',0),size.get('x',0),size.get('y',0)
        chain = list(ancestors(n))
        viewport = any(a['name'].startswith('3D viewport') for a in chain)
        if viewport and not ((190<=y<246 and x>=308 and w<=270 and h<=54) or (669<=y<706 and x<500 and w<=200)): continue
        if state in (2,4) and x==1204 and y==578 and n['type']=='VECTOR':
            fills=[f for f in n.get('fills',[]) if f.get('visible',True)]
            if fills and fills[0].get('color',{}).get('hex') in ('#0866E8','#15734A'): continue
        # Fixed sample lists are isolated in Editor-only reference fixtures.
        fixture_only = x < 264 and ((state in (0,3) and 275 <= y < 735) or (state not in (0,3) and 202 <= y < 536))
        fixture_only |= 288 <= x < 1136 and 818 <= y < 944 and state not in (2,4)
        fixture_only |= any(a['name'] == 'Group' for a in chain) and 1200 <= x < 1270 and 440 <= y < 590
        if n['type'] == 'FRAME' and n['name'].startswith('Button / '):
            b = dict(action=n['name'][9:], x=x,y=y,w=w,h=h)
            sig=json.dumps(b,sort_keys=True)
            if sig not in buttons: buttons[sig]=dict(b,mask=0,sources=[])
            buttons[sig]['mask'] |= 1 << state
            buttons[sig]['sources'].append(n['id'])
        if n['type'] not in ('TEXT','VECTOR','ROUNDED_RECTANGLE'): continue
        if x==0 and y==0 and w==1600 and h==1000: continue
        if state in (1,5) and (x,y)==(1200,275): continue # one editable multiline field
        fr = field_role(x,y,state) if n['type']=='TEXT' else ''
        if fr:
            f=dict(role=fr,x=x,y=y,w=w,h=h,size=n['text']['effectiveFontSize'],baseline=n['text']['baselines'][0]['position']['y'],weight={'Regular':400,'Medium':500,'Bold':700}.get(n['text']['fontName']['style'],400))
            fixture_fields.append(dict(state=state,role=fr,value=n['text']['characters']))
            sig=json.dumps(f,sort_keys=True)
            if sig not in fields: fields[sig]=dict(f,mask=0,sources=[])
            fields[sig]['mask'] |= 1<<state
            fields[sig]['sources'].append(n['id'])
            continue
        opacity=n.get('opacity',1)
        for a in chain:
            opacity *= a.get('opacity',1)
        v=dict(x=x,y=y,w=w,h=h,alpha=opacity,kind=n['type'],text='',size=0,weight=400,baseline=0,lineHeight=0,color='#FFFFFF',sprite='',role='',region='Chrome')
        if any('Overlay' in a['name'] or '書き出し前' in a['name'] and a['id']!=board['id'] for a in chain): v['region']='Modal'
        if state==5 and 490<=x<=1110 and 272<=y<=714: v['region']='Modal'
        if state==5 and x==0 and y==88 and w==1600: v['region']='Modal'
        if n['type']=='TEXT':
            t=n['text']; v.update(text=t['characters'],size=t['effectiveFontSize'],role=role(x,y,t['characters'],state))
            v['weight']={'Regular':400,'Medium':500,'Bold':700}.get(t['fontName']['style'],400)
            if t.get('baselines'):
                v['baseline']=t['baselines'][0]['position']['y']; v['lineHeight']=t['baselines'][0]['lineHeight']
            fills=[f for f in n.get('fills',[]) if f.get('visible',True)]
            if fills: v['color']=fills[0]['color']['hex']
        elif (w==0 or h==0) and n.get('strokes'):
            stroke=next(s for s in n['strokes'] if s.get('visible',True))
            v.update(kind='LINE',strokeWidth=n['strokeWeight'],color=stroke['color']['hex'],alpha=opacity*stroke.get('opacity',1))
        else:
            asset=assets.get(n['id'])
            if not asset: continue
            v['sprite']=Path(asset['file']).stem+'.png'
            used[v['sprite']]=asset['file']
        sig=json.dumps(v,sort_keys=True,ensure_ascii=False)
        target=fixture_merged if fixture_only else merged
        if sig not in target: target[sig]=dict(v,mask=0,sources=[],order=order,orders=[-1]*6)
        target[sig]['orders'][state]=order
        target[sig]['mask'] |= 1<<state
        target[sig]['sources'].append(n['id'])
document=dict(width=1600,height=1000,visuals=list(merged.values()),buttons=list(buttons.values()),fields=list(fields.values()))
(OUT/'handoff.json').write_text(json.dumps(document,ensure_ascii=False,indent=2),encoding='utf-8')
(OUT/'source-map.json').write_text(json.dumps(dict(source='skillsync_ui.fig',pageId='25:2',rasters=used),ensure_ascii=False,indent=2),encoding='utf-8')
fixture_out=ROOT/'Assets/Editor/SkillSyncDesign'
fixture_out.mkdir(parents=True,exist_ok=True)
(fixture_out/'reference-fixtures.json').write_text(json.dumps(dict(visuals=list(fixture_merged.values()),fields=fixture_fields),ensure_ascii=False,indent=2),encoding='utf-8')
print(f'{len(merged)} shared/state visuals, {len(buttons)} buttons, {len(fields)} fields, {len(used)} original SVGs')
