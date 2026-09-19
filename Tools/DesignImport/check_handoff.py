"""Static source/measurement checks. This does NOT validate Unity rendering."""
import json
from pathlib import Path
root=Path(__file__).resolve().parents[2]
source=root/'Design/skillsync_codex_handoff'
nodes={n['id']:n for n in json.loads((source/'data/nodes.json').read_text(encoding='utf-8'))['nodes']}
doc=json.loads((root/'Assets/UI/SkillSyncDesign/handoff.json').read_text(encoding='utf-8'))
fixtures=json.loads((root/'Assets/Editor/SkillSyncDesign/reference-fixtures.json').read_text(encoding='utf-8'))
count=0
for v in doc['visuals']+fixtures['visuals']:
    assert 0<v['mask']<64
    for ident in v['sources']:
        n=nodes[ident]
        assert abs(v['x']-n['screenPosition']['x'])<.0001
        assert abs(v['y']-n['screenPosition']['y'])<.0001
        if v['kind']=='TEXT':
            assert v['size']==n['text']['effectiveFontSize'],ident
            assert v['baseline']==n['text']['baselines'][0]['position']['y'],ident
        elif v['kind']=='LINE':
            assert v['strokeWidth']==n['strokeWeight'],ident
        else:
            assert (root/'Assets/UI/SkillSyncDesign/Sprites'/v['sprite']).is_file(),ident
        count+=1
for state in range(6):
    assert any(v['mask']&(1<<state) for v in doc['visuals'])
    assert any(f['state']==state for f in fixtures['fields'])
for f in doc['fields']:
    for ident in f['sources']: assert f['size']==nodes[ident]['text']['effectiveFontSize']
assert any(v['size']==21 for v in doc['visuals'])
assert any(v['size']==25 for v in doc['visuals'])
print(f'Static handoff: {count} source-node mappings, 6 state masks, effective font sizes and baselines passed.')
