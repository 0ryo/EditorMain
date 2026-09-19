"""Create GUIDs once for new owned assets; never replace existing meta files."""
from pathlib import Path
from uuid import uuid4
root=Path(__file__).resolve().parents[2]
paths=[]
for directory in ['Assets/UI/SkillSyncDesign','Assets/Editor/SkillSyncDesign']:
    p=root/directory;paths.extend([p,*p.rglob('*')])
paths.extend((root/'Assets/Scripts/UI').glob('SkillSync*.cs'))
paths.extend((root/'Assets/Scripts/Services').glob('SkillSync*.cs'))
paths.append(root/'Assets/Editor/Automation/ApplySkillSyncDesign.cs')
for p in paths:
    if p.suffix=='.meta': continue
    meta=Path(str(p)+'.meta')
    if meta.exists(): continue
    body=f'fileFormatVersion: 2\nguid: {uuid4().hex}\n'
    if p.is_dir(): body+='folderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n'
    elif p.suffix=='.cs': body+='MonoImporter:\n  externalObjects: {}\n  serializedVersion: 2\n  defaultReferences: []\n  executionOrder: 0\n  icon: {instanceID: 0}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n'
    meta.write_text(body,encoding='utf-8')
print('New asset GUIDs prepared; existing metadata preserved.')
