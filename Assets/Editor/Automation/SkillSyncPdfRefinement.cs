using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEngine;

// UI修正案.pdf, page 1 (1600x1000 points), adapted to the current 16:9 canvas.
public static class SkillSyncPdfRefinement
{
    [Serializable] class Document {public Visual[] visuals;public Button[] buttons;}
    [Serializable] class Visual {public float x,y,w,h,size,baseline;public int mask;public string text,kind;public string[] sources;}
    [Serializable] class Button {public float x,y,w,h;public int mask;public string action;public string[] sources;}
    static float X(float x)=>SkillSyncDesignLayout.MapX(x);
    static void Bounds(RectTransform rect,float x,float y,float w,float h,bool modal=false) {
        rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(0,1);
        rect.anchoredPosition=new Vector2(modal?x+SkillSyncDesignLayout.Extra/2:X(x),-y);
        rect.sizeDelta=new Vector2(w>=800?w+SkillSyncDesignLayout.Extra:w,h);
    }
    static void Text(TMP_Text text,float x,float top,float width,float size) {
        Bounds(text.rectTransform,x,top+size-20,width,40);text.fontSize=size;
        text.alignment=TextAlignmentOptions.BaselineLeft;text.margin=Vector4.zero;
    }
    public static void Apply(SkillSyncDesignView view)
    {
        if(view.pdfRevision>=7) return;
        var doc=JsonUtility.FromJson<Document>(File.ReadAllText("Assets/UI/SkillSyncDesign/handoff.json"));
        var source=doc.visuals.SelectMany(v=>v.sources.Select(id=>(id,v))).ToDictionary(p=>p.id,p=>p.v);
        var buttons=doc.buttons.SelectMany(v=>v.sources.Select(id=>(id,v))).ToDictionary(p=>p.id,p=>p.v);
        foreach(var v in view.visuals) if(v.sourceNodeIds!=null && source.TryGetValue(v.sourceNodeIds[0],out var original)) v.mask=original.mask;
        var buttonLabels=new HashSet<TMP_Text>();
        foreach(var control in view.controls) {
            if(control.sourceNodeIds==null || !control.sourceNodeIds.Any(buttons.ContainsKey)) continue;
            var b=buttons[control.sourceNodeIds.First(buttons.ContainsKey)];
            control.mask=b.mask;
            var box=RevisedButton(b);
            var rect=(RectTransform)control.button.transform;
            Bounds(rect,box.x,box.y,box.width,box.height,b.action=="編集を続ける" || b.action=="該当箇所を修正");
            bool modeTab=b.action=="配置を編集" || b.action=="手順を編集" || b.action=="動作を確認";
            if(modeTab) rect.anchoredPosition=new Vector2(X(458)+box.x-458,-box.y);
            if(b.action=="選択を表示" || new[]{"←","↑","↓","→"}.Contains(b.action)) control.mask=0;
            foreach(var v in view.visuals) {
                if(v.sourceNodeIds==null || !source.TryGetValue(v.sourceNodeIds[0],out var s)) continue;
                if((s.mask & b.mask)==0) continue;
                if(s.x<b.x-.1f || s.y<b.y-.1f || s.x+s.w>b.x+b.w+.2f || s.y+s.h>b.y+b.h+.2f) continue;
                if(control.mask==0) {v.mask=0;continue;}
                if(v.label!=null) {
                    buttonLabels.Add(v.label);v.label.transform.SetParent(rect,false);
                    var labelRect=v.label.rectTransform;
                    labelRect.anchorMin=Vector2.zero;labelRect.anchorMax=Vector2.one;labelRect.pivot=new Vector2(.5f,.5f);
                    labelRect.offsetMin=labelRect.offsetMax=Vector2.zero;
                    v.label.alignment=TextAlignmentOptions.Midline;v.label.margin=Vector4.zero;
                    if(b.action=="配置を編集" || b.action=="手順を編集" || b.action=="動作を確認") v.label.fontSize=12;
                    if(b.action=="↶ 元に戻す" || b.action=="↷") {
                        v.label.text="";
                        foreach(RectTransform glyph in labelRect) {glyph.anchorMin=glyph.anchorMax=glyph.pivot=new Vector2(.5f,.5f);glyph.anchoredPosition=Vector2.zero;}
                    }
                } else {
                    // Button islands use the same bounds as their hit area and caption.
                    Bounds((RectTransform)v.target.transform,box.x,box.y,box.width,box.height,b.action=="編集を続ける"||b.action=="該当箇所を修正");
                    if(modeTab) ((RectTransform)v.target.transform).anchoredPosition=rect.anchoredPosition;
                }
            }
        }
        foreach(var v in view.visuals) {
            if(v.sourceNodeIds==null || !source.TryGetValue(v.sourceNodeIds[0],out var s) || buttonLabels.Contains(v.label)) continue;
            var rect=(RectTransform)v.target.transform;
            if(s.y>=964 || (s.x<264 && s.y>=880 && v.label!=null) || v.role=="workspaceHint" || (s.x>=1184 && s.y>=610 && s.y<740 && (s.mask & 9)!=0) || (s.x>=1060 && s.x<=1114 && s.y>=185 && s.y<250)) {v.mask=0;continue;}
            if(s.x==544 && s.y==17 && s.w==440) Bounds(rect,458,90,356,42);
            if(v.label!=null && s.x==288 && s.y==114) {v.label.text="3Dビューポート";Text(v.label,288,93,170,21);}
            if(v.label!=null && s.x==24 && s.y>=110 && s.y<140) Text(v.label,24,94.25f,220,21);
            if(s.x==16 && s.y==165) Bounds(rect,16,129.64f,232,45.81f);
            if(s.x==30 && s.y==178 && v.label!=null) {
                Bounds(rect,30,129.64f,202,45.81f);v.label.alignment=TextAlignmentOptions.MidlineLeft;v.label.fontSize=14;
                foreach(RectTransform glyph in rect) {glyph.anchorMin=glyph.anchorMax=glyph.pivot=new Vector2(0,.5f);glyph.anchoredPosition=Vector2.zero;}
            }
            if(s.x<264 && s.y>=225 && s.y<255) {
                if(v.label==null) Bounds(rect,s.x,184.82f,s.w,29.15f);
            }
            if(s.x==308 && s.y==190) Bounds(rect,308,158.11f,s.w,35.88f);
            if(s.x==320 && s.y>=195 && s.y<215 && v.label!=null) Text(v.label,320,165.49f,s.w,13);
            if(v.role=="objectCount" && v.label!=null) Text(v.label,414,786,260,12);
            if(s.x>=1184 && (v.mask & 9)!=0) {
                if(v.role=="selectionTitle") {Text(v.label,1184,96.33f,320,25);view.inspectorTitle=v.label;}
                else if(v.role=="inspectorLabel") {Text(v.label,1257,110.9f,120,13);v.label.text="選択中";view.inspectorSelection=v.label;}
                else if(v.label!=null && s.text=="名前") Text(v.label,1184,135.89f,200,14);
                else if(s.x==1184 && s.y==237) Bounds(rect,1184,167.12f,392,47.89f);
                else if(v.label!=null && s.text=="位置と向き") {v.label.text="編集";Text(v.label,1184,237.92f,200,19);}
                else if(s.x==1184 && s.y==421) Bounds(rect,1184,334.74f,392,66.63f);
                else if(v.label!=null && s.y>=430 && s.y<480) Text(v.label,s.x,s.text.Contains("上下の位置")?374.3f:347.23f,s.w,s.size);
                else if(v.label!=null && (s.text=="高さ"||s.text=="向き")) Text(v.label,s.x,427.4f,160,14);
                else if(s.y==541) Bounds(rect,s.x,458.63f,s.w,47.89f);
                else if(v.label!=null && (s.text=="cm"||s.text=="°") && s.y>500 && s.y<600) Text(v.label,s.x,472.16f,s.w,13);
                else if(s.kind=="LINE" && s.y==753) Bounds(rect,s.x,780,s.w,1);
                else if(v.label!=null && s.text=="配置の補助") Text(v.label,1184,806.36f,220,15);
                else if(v.label!=null && s.y>=810 && s.y<846) Text(v.label,s.x,848,s.w,s.size);
                else if(v.label!=null && s.y>=846 && s.y<880) Text(v.label,s.x,883.4f,s.w,13);
            }
        }
        foreach(var field in view.fields) {
            var rect=(RectTransform)field.input.transform;
            if(field.role=="name") Field(field.input,1184,167.12f,392,47.89f,17);
            if(field.role=="height") Field(field.input,1184,458.63f,184,47.89f,17);
            if(field.role=="angle") Field(field.input,1392,458.63f,184,47.89f,17);
            if(field.role=="distance") Field(field.input,1200,642,170,46,17);
            if(field.role=="hold") Field(field.input,1390,642,170,46,17);
            if(field.role=="search") Field(field.input,30,129.64f,202,45.81f,14);
            if(field.role=="project") {
                Field(field.input,276,12,141,32,17);
                field.input.textComponent.rectTransform.offsetMin=field.input.textComponent.rectTransform.offsetMax=Vector2.zero;
                field.input.textComponent.overflowMode=TextOverflowModes.Ellipsis;
            }
            if(new[]{"height","angle","distance","hold"}.Contains(field.role)) foreach(var v in view.visuals.Where(v=>v.label!=null && (v.mask & field.mask)!=0 && v.label.text==(field.role=="angle"?"°":field.role=="hold"?"秒":"cm"))) {
                v.label.transform.SetParent(rect,false);var unit=v.label.rectTransform;
                unit.anchorMin=unit.anchorMax=unit.pivot=new Vector2(0,1);unit.anchoredPosition=new Vector2(rect.rect.width-42,0);unit.sizeDelta=new Vector2(32,rect.rect.height);
                v.label.alignment=TextAlignmentOptions.MidlineLeft;
            }
        }
        foreach(var c in view.controls) {
            var r=(RectTransform)c.button.transform;
            if(c.action=="すべて" || c.action=="部品" || c.action=="環境") {
                Bounds(r,c.action=="すべて"?16:c.action=="部品"?98:172,184.82f,c.action=="すべて"?74:66,29.15f);
                foreach(var v in view.visuals.Where(v=>v.label!=null && source.TryGetValue(v.sourceNodeIds[0],out var s) && s.text==c.action && s.x<264 && s.y>=225 && s.y<255)) {
                    v.label.transform.SetParent(r,false);var t=v.label.rectTransform;t.anchorMin=Vector2.zero;t.anchorMax=Vector2.one;t.offsetMin=t.offsetMax=Vector2.zero;
                    v.label.alignment=TextAlignmentOptions.Midline;v.label.margin=Vector4.zero;
                }
            }
            if(c.action=="Snap") Bounds(r,1184,844,392,34);
            if(c.action=="詳細設定") Bounds(r,1184,879,392,32);
            if(c.action=="Guide") c.mask=0;
        }
        // The enlarged library remains bound to real entries; the PDF's repeated table is not sample data to insert.
        var library=(RectTransform)view.libraryContent.parent.parent;
        Bounds(library,16,223.34f,232,638.19f);
        ((RectTransform)view.libraryContent.parent).sizeDelta=library.sizeDelta;
        view.libraryContent.GetComponent<UnityEngine.UI.VerticalLayoutGroup>().spacing=16.66f;
        view.libraryTemplate.pdfLayout=true;
        Bounds(view.viewport,288,137,848,595);
        view.conditionOverlay.rectTransform.sizeDelta=view.viewport.sizeDelta;
        var root=(RectTransform)view.transform;
        ((RectTransform)root.Find("AboveViewport")).sizeDelta=new Vector2(root.rect.width,137);
        Bounds((RectTransform)root.Find("LeftOfViewport"),0,137,288,595);
        Bounds((RectTransform)root.Find("RightOfViewport"),1136,137,464,595);
        Bounds((RectTransform)root.Find("BelowViewport"),0,732,1600,268);
        // Keep side panels white through the removed footer.
        foreach(var v in view.visuals) if(v.sourceNodeIds!=null && source.TryGetValue(v.sourceNodeIds[0],out var s) && s.y==88 && s.h==876) {
            var r=(RectTransform)v.target.transform;r.sizeDelta=new Vector2(r.sizeDelta.x,912);
        }
        int originalState=view.State;
        for(int state=0;state<6;state++) {
            view.Show(state);Canvas.ForceUpdateCanvases();
            foreach(var c in view.controls.Where(c=>c.button.gameObject.activeInHierarchy))
            foreach(var label in c.button.GetComponentsInChildren<TMP_Text>()) {
                if(string.IsNullOrEmpty(label.text)) continue;
                label.margin=Vector4.zero;label.ForceMeshUpdate(true,true);
                // Measure each state while active: inactive TMP labels can retain stale glyph bounds.
                float offset=label.textBounds.center.y-label.rectTransform.rect.center.y;
                label.margin=new Vector4(0,offset*2,0,0);
            }
        }
        view.pdfRevision=7;view.Show(originalState);
    }
    static void Field(TMP_InputField input,float x,float y,float w,float h,float size) {
        Bounds((RectTransform)input.transform,x,y,w,h);
        input.textViewport.sizeDelta=new Vector2(w,h);
        var text=input.textComponent;var rect=text.rectTransform;
        rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.pivot=new Vector2(.5f,.5f);
        rect.offsetMin=new Vector2(input.name=="Search"?0:14,0);rect.offsetMax=new Vector2(input.name=="Search"?0:-40,0);
        text.fontSize=size;text.alignment=TextAlignmentOptions.MidlineLeft;text.margin=Vector4.zero;
    }
    static Rect RevisedButton(Button b) {
        switch(b.action) {
            case "配置を編集":return new Rect(463.25f,95.25f,112.35f,31.5f);
            case "手順を編集":return new Rect(579.8f,95.25f,112.35f,31.5f);
            case "動作を確認":return new Rect(696.35f,95.25f,112.35f,31.5f);
            case "↶ 元に戻す":return new Rect(433,23,44,40);
            case "↷":return new Rect(485,23,44,40);
            case "ヘルプ":return new Rect(1273,23,82,40);
            case "全体を見る":return new Rect(913,94,106.78f,33.9f);
            case "上から見る":return new Rect(1029.25f,94,106.78f,33.9f);
            case "移動":return new Rect(1184,279.56f,124,45.81f);
            case "回転":return new Rect(1318,279.56f,124,45.81f);
            case "大きさ":return new Rect(1452,279.56f,124,45.81f);
            case "複製":case "キャンセル":return new Rect(1184,936.49f,184,45.81f);
            case "削除":case "ここに配置":return new Rect(1392,936.49f,184,45.81f);
            case "＋ 3Dモデルを読み込む":return new Rect(16,936.49f,232,45.81f);
            default:return new Rect(b.x,b.y,b.w,b.h);
        }
    }
}
