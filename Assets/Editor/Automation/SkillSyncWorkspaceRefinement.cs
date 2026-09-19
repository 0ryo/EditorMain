using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

public static class SkillSyncWorkspaceRefinement
{
    public static void Apply(SkillSyncDesignView view)
    {
        if(view.GetComponent<SkillSyncWorkspaceExpansion>()!=null) {Finish(view);return;}
        var root=(RectTransform)view.transform;
        var chrome=(RectTransform)root.Find("Chrome");
        float extra=SkillSyncDesignLayout.Extra;
        void Place(RectTransform r,float x,float y,float w,float h) {
            r.anchorMin=r.anchorMax=r.pivot=new Vector2(0,1);
            r.anchoredPosition=new Vector2(x,-y);r.sizeDelta=new Vector2(w,h);
        }
        // Move button islands and hit areas together; captions already stretch to their buttons.
        foreach(var c in view.controls) {
            var r=(RectTransform)c.button.transform;
            float x=r.anchoredPosition.x,y=-r.anchoredPosition.y;
            Vector2 next=r.anchoredPosition;
            if(c.action=="配置を編集"||c.action=="手順を編集"||c.action=="動作を確認")
                next=new Vector2(425+(c.action=="配置を編集"?0:c.action=="手順を編集"?142:284),-23);
            if(c.action=="↶ 元に戻す") next=new Vector2(876,-23);
            if(c.action=="↷") next=new Vector2(928,-23);
            if(c.action=="全体を見る") next=new Vector2(1136+extra-2*106.78f-8,-94);
            if(c.action=="上から見る") next=new Vector2(1136+extra-106.78f,-94);
            if(next==r.anchoredPosition) continue;
            bool tab=c.action=="配置を編集"||c.action=="手順を編集"||c.action=="動作を確認";
            foreach(var v in view.visuals.Where(v=>v.label==null && v.target.transform.parent==chrome)) {
                var island=(RectTransform)v.target.transform;
                if(Vector2.Distance(island.anchoredPosition,new Vector2(x,-y))<.1f && Mathf.Abs(island.rect.width-r.rect.width)<.1f) {
                    island.anchoredPosition=next;if(tab)island.sizeDelta=new Vector2(138,40);
                }
            }
            r.anchoredPosition=next;if(tab)r.sizeDelta=new Vector2(138,40);
        }
        foreach(var v in view.visuals) {
            var r=(RectTransform)v.target.transform;
            if(r.parent!=chrome) continue;
            float x=r.anchoredPosition.x,y=-r.anchoredPosition.y;
            if(v.label==null && Mathf.Abs(y-90)<.1f && Mathf.Abs(r.rect.width-356)<.1f) Place(r,419,17,434,52);
            // Remove the entire upper-left viewport badge, including placement-preview variants.
            if(x>=308 && x<350 && y>=150 && y<195) {v.mask=0;v.target.SetActive(false);}
            if(v.label!=null && v.role=="status") {Place(r,304,732,580,34);v.label.alignment=TextAlignmentOptions.MidlineLeft;}
            if(x>=308 && x<345 && y>=660 && y<710) {
                Place(r,846+extra,734,158,30);
                if(v.label!=null) {v.label.alignment=TextAlignmentOptions.Midline;v.label.margin=Vector4.zero;}
            }
        }
        Place(view.viewportGrid.rectTransform,1016+extra,732,120,34);
        view.viewportGrid.alignment=TextAlignmentOptions.Midline;view.viewportGrid.margin=Vector4.zero;
        var expansion=view.gameObject.AddComponent<SkillSyncWorkspaceExpansion>();
        var panel=new GameObject("InspectorSlide",typeof(RectTransform),typeof(CanvasGroup));
        var panelRect=(RectTransform)panel.transform;panelRect.SetParent(chrome,false);
        Place(panelRect,0,0,root.rect.width,1000);
        expansion.inspector=panel.GetComponent<CanvasGroup>();
        var items=new List<SkillSyncWorkspaceExpansion.Item>();
        void Track(RectTransform rect,float move,float grow) {
            if(items.Any(i=>i.rect==rect))return;
            items.Add(new SkillSyncWorkspaceExpansion.Item{rect=rect,position=rect.anchoredPosition,size=rect.sizeDelta,move=move,grow=grow});
        }
        foreach(RectTransform r in chrome.Cast<Transform>().ToArray()) {
            if(r==panelRect)continue;
            float x=r.anchoredPosition.x,y=-r.anchoredPosition.y;
            if(x>=1160+extra-1 && y>=87) {r.SetParent(panelRect,false);continue;}
            if(y>=88 && x>=264 && x<1136+extra) {
                if(r.rect.width>=800) Track(r,0,1);
                else if(x>=846+extra) Track(r,1,0);
            }
        }
        Track(view.viewport,0,1);Track(view.conditionOverlay.rectTransform,0,1);
        Track(view.viewportGrid.rectTransform,1,0);
        Track((RectTransform)root.Find("RightOfViewport"),1,-1);
        var objects=(RectTransform)view.objectsContent.parent.parent;
        Track(objects,0,1);
        Track((RectTransform)view.objectsContent.parent,0,1);
        Track(view.objectsContent,0,1);
        // Runtime rows size through the layout; pin their status labels to the moving right edge.
        var status=view.objectTemplate.status.rectTransform;
        float statusRight=status.anchoredPosition.x-((RectTransform)view.objectTemplate.transform).rect.width;
        status.anchorMin=status.anchorMax=new Vector2(1,1);status.anchoredPosition=new Vector2(statusRight,status.anchoredPosition.y);
        expansion.objectsContent=view.objectsContent;expansion.items=items.ToArray();
        Finish(view);
    }
    static void Finish(SkillSyncDesignView view) {
        foreach(var field in view.fields) {field.input.richText=false;field.input.textComponent.richText=false;}
        SkillSyncObjectPickerBuilder.Apply(view);
        foreach(var content in new[]{view.libraryContent,view.objectsContent,view.stepsContent})
            content.GetComponentInParent<UnityEngine.UI.ScrollRect>(true).scrollSensitivity=48;
        var steps=(RectTransform)view.stepsContent.parent.parent;
        steps.sizeDelta=new Vector2(steps.sizeDelta.x,606);
        var stepsViewport=(RectTransform)view.stepsContent.parent;
        stepsViewport.sizeDelta=new Vector2(stepsViewport.sizeDelta.x,606);
        foreach(var c in view.controls.Where(c=>c.action=="＋ 手順を追加"||c.action=="↑ 上へ"||c.action=="↓ 下へ")) {
            var button=(RectTransform)c.button.transform;
            var old=button.anchoredPosition;
            var next=new Vector2(old.x,c.action=="＋ 手順を追加"?-832:-888);
            foreach(var v in view.visuals.Where(v=>v.label==null && (v.mask & c.mask)!=0)) {
                var island=(RectTransform)v.target.transform;
                if(Vector2.Distance(island.anchoredPosition,old)<.1f && Mathf.Abs(island.rect.width-button.rect.width)<.1f)
                    island.anchoredPosition=next;
            }
            button.anchoredPosition=next;
        }
        foreach(var v in view.visuals.Where(v=>v.label!=null && v.label.text=="順番は後から変更できます")) {
            var r=v.label.rectTransform;r.anchoredPosition=new Vector2(r.anchoredPosition.x,-948);
        }
        var expansion=view.GetComponent<SkillSyncWorkspaceExpansion>();
        var chrome=view.transform.Find("Chrome");
        // Source strokes extend half a pixel beyond their nominal boundary.
        foreach(RectTransform r in chrome.Cast<Transform>().ToArray())
            if(r!=expansion.inspector.transform && r.anchoredPosition.x>=1160+SkillSyncDesignLayout.Extra-1 && -r.anchoredPosition.y>=87)
                r.SetParent(expansion.inspector.transform,false);
        foreach(var v in view.visuals) {
            var r=(RectTransform)v.target.transform;
            if(v.role=="viewportMode" || (Mathf.Abs(-r.anchoredPosition.y-734)<.1f && Mathf.Abs(r.rect.width-158)<.1f))
                v.orders=Enumerable.Repeat(v.label!=null?1001:1000,6).ToArray();
        }
        view.viewportGrid.color=new Color32(88,103,124,255);
        SkillSyncSplitBuilder.Apply(view);
    }
}
