import json
from pathlib import Path
import pdfplumber

with pdfplumber.open('C:/Users/ryota/Downloads/UI修正案.pdf') as pdf:
    page=pdf.pages[0]
    words=page.extract_words(extra_attrs=['size'])
    for w in words:
        print('{}: {:.2f},{:.2f},{:.2f},{:.2f} size={:.2f}'.format(w['text'],w['x0'],w['top'],w['x1']-w['x0'],w['bottom']-w['top'],w['size']))
    out=Path('tmp/pdfs/revision-geometry.json')
    out.write_text(json.dumps({'words':words,'curves':page.curves,'rects':page.rects},ensure_ascii=False,indent=2),encoding='utf-8')
    print('Large curves:')
    for c in page.curves:
        if c['width']>30 and c['height']>25:
            print(round(c['x0'],2),round(c['top'],2),round(c['width'],2),round(c['height'],2),c.get('non_stroking_color'))
