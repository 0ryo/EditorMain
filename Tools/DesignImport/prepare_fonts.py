"""Build three static weights from the locally installed Noto variable font.
fontTools is build tooling only; no Python or fontTools dependency is added to Unity.
"""
import sys
from pathlib import Path
sys.path.insert(0, sys.argv[1])
from fontTools.ttLib import TTFont
from fontTools.varLib.instancer import instantiateVariableFont
root = Path(__file__).resolve().parents[2]
dest = root / 'Assets/UI/SkillSyncDesign/Fonts'
dest.mkdir(exist_ok=True)
font = TTFont('C:/Windows/Fonts/NotoSansJP-VF.ttf')
license_text = '\n\n'.join(sorted({r.toUnicode() for r in font['name'].names if r.nameID in (0, 13, 14)}))
(dest/'LICENSE.txt').write_text(license_text, encoding='utf-8')
for weight, style in [(400,'Regular'), (500,'Medium'), (700,'Bold')]:
    instance = instantiateVariableFont(font, {'wght':weight}, inplace=False)
    for record in instance['name'].names:
        if record.nameID in (2,17): record.string = style.encode(record.getEncoding())
        elif record.nameID in (4,6): record.string = ('NotoSansJP-'+style).encode(record.getEncoding())
    instance.save(dest/f'NotoSansJP-{style}.ttf')
    print(style)
