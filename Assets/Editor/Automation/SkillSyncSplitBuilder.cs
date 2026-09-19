using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

public static class SkillSyncSplitBuilder
{
    public static void Apply(SkillSyncDesignView view) {
        if(view.transform.Find("WorkspaceSplit")!=null)return;
        var root=(RectTransform)view.transform;
        var chrome=root.Find("Chrome");
        var items=new List<SkillSyncVerticalSplit.Item>();
        void Track(RectTransform r,float move,float grow,float height=-1) {
            if(items.Any(i=>i.rect==r))return;
            items.Add(new SkillSyncVerticalSplit.Item{rect=r,y=r.anchoredPosition.y,height=height<0?r.rect.height:height,move=move,grow=grow});
        }
        foreach(RectTransform r in chrome) {
            float x=r.anchoredPosition.x,y=-r.anchoredPosition.y;
            if(x>=288 && x<1136+SkillSyncDesignLayout.Extra && y>=732 && y<944) Track(r,1,0);
        }
        Track(view.viewport,0,1);Track(view.conditionOverlay.rectTransform,0,1);
        Track((RectTransform)root.Find("LeftOfViewport"),0,1);Track((RectTransform)root.Find("RightOfViewport"),0,1);
        Track((RectTransform)root.Find("BelowViewport"),1,-1);
        Track(view.viewportGrid.rectTransform,1,0);
        var list=(RectTransform)view.objectsContent.parent.parent;
        items.RemoveAll(i=>i.rect==list);
        Track(list,1,-1,182);Track((RectTransform)view.objectsContent.parent,0,-1,182);
        var handle=(RectTransform)new GameObject("WorkspaceSplit",typeof(RectTransform)).transform;
        handle.SetParent(root,false);handle.anchorMin=handle.anchorMax=handle.pivot=new Vector2(0,1);
        handle.anchoredPosition=new Vector2(288,-766);handle.sizeDelta=new Vector2(view.viewport.rect.width,12);
        var image=handle.gameObject.AddComponent<UnityEngine.UI.Image>();image.color=new Color(0,0,0,0);
        handle.gameObject.AddComponent<EditorUiInputBlocker>();
        var line=(RectTransform)new GameObject("Grip",typeof(RectTransform)).transform;line.SetParent(handle,false);
        line.anchorMin=line.anchorMax=line.pivot=new Vector2(.5f,.5f);line.sizeDelta=new Vector2(48,3);
        var grip=line.gameObject.AddComponent<UnityEngine.UI.Image>();grip.color=new Color32(180,190,205,255);grip.raycastTarget=false;
        var split=handle.gameObject.AddComponent<SkillSyncVerticalSplit>();split.view=view;Track(handle,1,0);split.items=items.ToArray();
        var expansion=view.GetComponent<SkillSyncWorkspaceExpansion>();
        expansion.items=expansion.items.Concat(new[]{new SkillSyncWorkspaceExpansion.Item{rect=handle,position=handle.anchoredPosition,size=handle.sizeDelta,grow=1}}).ToArray();
        split.Apply();
        var row=view.libraryTemplate;
        var buttonRect=(RectTransform)new GameObject("RemoveFromLibrary",typeof(RectTransform)).transform;buttonRect.SetParent(row.transform,false);
        buttonRect.anchorMin=buttonRect.anchorMax=buttonRect.pivot=new Vector2(1,1);buttonRect.anchoredPosition=new Vector2(-6,-6);buttonRect.sizeDelta=new Vector2(28,28);
        var background=buttonRect.gameObject.AddComponent<SkillSyncRoundedGraphic>();background.color=Color.white;background.radius=14;
        var button=buttonRect.gameObject.AddComponent<UnityEngine.UI.Button>();button.targetGraphic=background;
        var textRect=(RectTransform)new GameObject("Cross",typeof(RectTransform)).transform;textRect.SetParent(buttonRect,false);
        textRect.anchorMin=Vector2.zero;textRect.anchorMax=Vector2.one;textRect.offsetMin=textRect.offsetMax=Vector2.zero;
        var text=textRect.gameObject.AddComponent<TextMeshProUGUI>();text.font=row.regularFont;text.text="×";text.fontSize=20;text.color=new Color32(88,103,124,255);text.alignment=TextAlignmentOptions.Midline;text.raycastTarget=false;
        var hover=row.gameObject.AddComponent<SkillSyncLibraryRemove>();hover.remove=button;button.gameObject.SetActive(false);
    }
}
