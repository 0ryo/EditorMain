using TMPro;
using UnityEngine;

public static class SkillSyncObjectPickerBuilder
{
    public static void Apply(SkillSyncDesignView view) {
        if(view.GetComponent<SkillSyncObjectPicker>() is SkillSyncObjectPicker existing) {Frame(existing);return;}
        var picker=view.gameObject.AddComponent<SkillSyncObjectPicker>();
        RectTransform Rect(string name,Transform parent,float x,float y,float w,float h) {
            var r=(RectTransform)new GameObject(name,typeof(RectTransform)).transform;r.SetParent(parent,false);
            r.anchorMin=r.anchorMax=r.pivot=new Vector2(0,1);r.anchoredPosition=new Vector2(x,-y);r.sizeDelta=new Vector2(w,h);return r;
        }
        TMP_Text Text(RectTransform r,string value) {
            var t=r.gameObject.AddComponent<TextMeshProUGUI>();t.font=view.partALabel.font;t.fontSize=14;t.text=value;
            t.color=new Color32(23,34,54,255);t.alignment=TextAlignmentOptions.MidlineLeft;
            t.richText=false;t.raycastTarget=false;t.textWrappingMode=TextWrappingModes.NoWrap;t.overflowMode=TextOverflowModes.Ellipsis;return t;
        }
        UnityEngine.UI.Button Button(RectTransform r,string value) {
            var bg=r.gameObject.AddComponent<SkillSyncRoundedGraphic>();bg.color=Color.white;bg.borderColor=new Color32(205,213,224,255);bg.radius=6;
            var b=r.gameObject.AddComponent<UnityEngine.UI.Button>();b.targetGraphic=bg;
            var text=Text(Rect("Label",r,12,0,r.rect.width-24,r.rect.height),value);
            return b;
        }
        var menu=Rect("ConditionObjectDropdown",view.transform,0,0,1777.778f,1000);picker.menu=menu.gameObject;
        var backdrop=Rect("Dismiss",menu,0,0,1777.778f,1000);
        var image=backdrop.gameObject.AddComponent<UnityEngine.UI.Image>();image.color=Color.clear;
        picker.dismiss=backdrop.gameObject.AddComponent<UnityEngine.UI.Button>();picker.dismiss.targetGraphic=image;
        var panel=Rect("Options",menu,1377.778f,491,360,300);picker.panel=panel;
        var background=panel.gameObject.AddComponent<SkillSyncRoundedGraphic>();background.color=Color.white;background.radius=8;
        var scroll=panel.gameObject.AddComponent<UnityEngine.UI.ScrollRect>();scroll.horizontal=false;scroll.scrollSensitivity=48;scroll.movementType=UnityEngine.UI.ScrollRect.MovementType.Clamped;
        var viewport=Rect("Viewport",panel,6,6,348,288);viewport.anchorMin=Vector2.zero;viewport.anchorMax=Vector2.one;viewport.offsetMin=new Vector2(6,6);viewport.offsetMax=new Vector2(-6,-6);
        viewport.gameObject.AddComponent<UnityEngine.UI.RectMask2D>();
        var content=Rect("Content",viewport,0,0,348,0);picker.content=content;
        var layout=content.gameObject.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();layout.childControlWidth=layout.childControlHeight=true;layout.childForceExpandHeight=false;layout.spacing=0;
        content.gameObject.AddComponent<UnityEngine.UI.ContentSizeFitter>().verticalFit=UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
        scroll.viewport=viewport;scroll.content=content;
        picker.template=Button(Rect("OptionTemplate",content,0,0,348,42),"");picker.template.gameObject.AddComponent<UnityEngine.UI.LayoutElement>().preferredHeight=42;picker.template.gameObject.SetActive(false);
        var picking=Rect("ViewportObjectPicking",view.viewport,0,0,view.viewport.rect.width,view.viewport.rect.height);
        picking.anchorMin=Vector2.zero;picking.anchorMax=Vector2.one;picking.offsetMin=picking.offsetMax=Vector2.zero;
        picker.picking=picking.gameObject;
        var outline=picking.gameObject.AddComponent<SkillSyncRoundedGraphic>();outline.color=new Color(0,0,0,0);outline.borderColor=new Color32(8,102,232,255);outline.borderWidth=3;outline.radius=0;outline.raycastTarget=false;
        var banner=Rect("Instruction",picking,16,16,640,44);
        var surface=banner.gameObject.AddComponent<SkillSyncRoundedGraphic>();surface.color=new Color32(234,242,255,255);surface.raycastTarget=false;surface.radius=8;
        picker.instruction=Text(Rect("Text",banner,12,0,490,44),"");
        picker.cancel=Button(Rect("Cancel",banner,510,5,118,34),"取消 / Esc");
        menu.gameObject.SetActive(false);picking.gameObject.SetActive(false);
        Frame(picker);
    }
    static void Frame(SkillSyncObjectPicker picker) {
        var old=picker.picking.GetComponent<SkillSyncRoundedGraphic>();
        if(old!=null)Object.DestroyImmediate(old);
        if(picker.picking.transform.Find("FrameTop")==null) {
            for(int i=0;i<4;i++) {
                var rect=(RectTransform)new GameObject(new[]{"FrameTop","FrameBottom","FrameLeft","FrameRight"}[i],typeof(RectTransform)).transform;
                rect.SetParent(picker.picking.transform,false);
                if(i<2) {rect.anchorMin=new Vector2(0,i==0?1:0);rect.anchorMax=new Vector2(1,i==0?1:0);rect.pivot=new Vector2(.5f,i==0?1:0);rect.sizeDelta=new Vector2(0,3);}
                else {rect.anchorMin=new Vector2(i==2?0:1,0);rect.anchorMax=new Vector2(i==2?0:1,1);rect.pivot=new Vector2(i==2?0:1,.5f);rect.sizeDelta=new Vector2(3,0);}
                rect.anchoredPosition=Vector2.zero;
                var line=rect.gameObject.AddComponent<UnityEngine.UI.Image>();line.color=new Color32(8,102,232,255);line.raycastTarget=false;
            }
        }
        picker.cancel.GetComponentInChildren<TMP_Text>(true).alignment=TextAlignmentOptions.Midline;
    }
}
