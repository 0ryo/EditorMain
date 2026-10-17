using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;

// Audit patch A: header/chrome fixes applied to the existing UIRoot prefab through the Editor API.
// Every value is assigned absolutely and objects are found by name first, so repeated runs leave no diff.
public static class SkillSyncAuditChromePatch
{
    const string PrefabPath="Assets/UI/Prefabs/UIRoot.prefab";
    const string BackgroundName="FullScreenBackground";
    const string SavedPillName="SavedPill";
    const string UndoIslandName="UndoIsland";
    static readonly string[] Tabs={"配置を編集","手順を編集","動作を確認"};
    static readonly string[] TabLabels={"① 配置を編集","② 手順を編集","③ 動作を確認"};

    [MenuItem("Tools/Automation/SkillSync/Apply Audit Chrome Patch")]
    public static void ApplyToPrefab()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
        var root=PrefabUtility.LoadPrefabContents(PrefabPath);
        try {
            var view=root.GetComponentInChildren<SkillSyncDesignView>(true);
            if(view==null) throw new InvalidOperationException("Apply SkillSync Figma Design first.");
            Apply(view);
            view.Show(0);
            PrefabUtility.SaveAsPrefabAsset(root,PrefabPath,out bool saved);
            if(!saved) throw new InvalidOperationException("UIRoot save failed");
        } finally {PrefabUtility.UnloadPrefabContents(root);}
        AssetDatabase.SaveAssets();
    }

    public static void Apply(SkillSyncDesignView view)
    {
        var chrome=(RectTransform)view.transform.Find("Chrome");
        if(chrome==null) throw new InvalidOperationException("Chrome is missing.");
        FullScreenBackground(view);
        TabLabelsAndSize(view);
        UndoIsland(view,chrome);
        SaveButton(view,chrome);
        ModalScrim(view);
        LogRects(view);
    }

    static void Place(RectTransform r,float x,float y,float w,float h)
    {
        r.anchorMin=r.anchorMax=r.pivot=new Vector2(0,1);
        r.anchoredPosition=new Vector2(x,-y);r.sizeDelta=new Vector2(w,h);
    }
    static SkillSyncDesignView.Control Control(SkillSyncDesignView view,string action)
        => view.controls.FirstOrDefault(c=>c.action==action && c.button!=null);

    // L-01: fills the letterbox band outside the 1600x1000 design area on non-16:10 screens.
    static void FullScreenBackground(SkillSyncDesignView view)
    {
        var uiRoot=view.transform.parent;
        if(uiRoot==null) throw new InvalidOperationException("SkillSyncDesign has no parent.");
        var found=uiRoot.Find(BackgroundName);
        var rect=found!=null ? (RectTransform)found : new GameObject(BackgroundName,typeof(RectTransform),typeof(UnityEngine.UI.Image)).GetComponent<RectTransform>();
        if(found==null) rect.SetParent(uiRoot,false);
        rect.gameObject.SetActive(true);
        rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.pivot=new Vector2(.5f,.5f);
        rect.offsetMin=Vector2.zero;rect.offsetMax=Vector2.zero;
        var image=rect.GetComponent<UnityEngine.UI.Image>();
        image.sprite=null;image.color=HeaderColor(view);image.raycastTarget=false;
        rect.SetAsFirstSibling();
    }
    static Color HeaderColor(SkillSyncDesignView view)
    {
        var fallback=new Color32(245,247,250,255);
        var above=view.transform.Find("AboveViewport");
        var aboveImage=above!=null ? above.GetComponent<UnityEngine.UI.Image>() : null;
        if(aboveImage!=null) fallback=aboveImage.color;
        // Header art is a sprite: sample it when the texture is readable, otherwise use the band surface.
        foreach(var v in view.visuals) {
            if(v.label!=null || v.target==null) continue;
            var r=(RectTransform)v.target.transform;
            var image=v.target.GetComponent<UnityEngine.UI.Image>();
            if(image==null || image.sprite==null || r.parent==null || r.parent.name!="Chrome") continue;
            if(Mathf.Abs(r.anchoredPosition.y)>.1f || r.sizeDelta.x<1500 || r.sizeDelta.y<60 || r.sizeDelta.y>100) continue;
            try {
                var tex=image.sprite.texture;
                if(tex!=null && tex.isReadable) {var c=tex.GetPixel(tex.width/2,tex.height/2);c.a=1;return c;}
            } catch(Exception) {}
        }
        fallback.a=1;return fallback;
    }

    // L-02 and tab numbering: labels only, the Control.action strings stay as they are.
    static void TabLabelsAndSize(SkillSyncDesignView view)
    {
        for(int i=0;i<Tabs.Length;i++) {
            var control=Control(view,Tabs[i]);
            if(control==null) {Debug.LogWarning("[SkillSync] Tab control missing: "+Tabs[i]);continue;}
            foreach(var label in control.button.GetComponentsInChildren<TMP_Text>(true)) {
                if(label.font!=null) {
                    if(!label.font.TryAddCharacters("①②③",out string missing)) Debug.LogWarning("[SkillSync] Missing glyphs: "+missing);
                    EditorUtility.SetDirty(label.font);
                }
                label.text=TabLabels[i];
                label.enableAutoSizing=false;label.fontSize=18;
                label.textWrappingMode=TextWrappingModes.NoWrap;
                label.alignment=TextAlignmentOptions.Midline;label.margin=Vector4.zero;
                EditorUtility.SetDirty(label);
            }
        }
    }

    // L-04: replace the clipped stroke of the undo island by a rounded graphic of the button's size.
    static void UndoIsland(SkillSyncDesignView view,RectTransform chrome)
    {
        var control=Control(view,"↶ 元に戻す");
        if(control==null) {Debug.LogWarning("[SkillSync] Undo control missing.");return;}
        var button=(RectTransform)control.button.transform;
        var pos=button.anchoredPosition;var size=button.sizeDelta;
        SkillSyncDesignView.Visual island=null;
        foreach(var v in view.visuals) {
            if(v.label!=null || v.target==null) continue;
            var r=(RectTransform)v.target.transform;
            if(r.parent!=chrome) continue;
            bool thin=r.sizeDelta.x<=2.5f && Mathf.Abs(r.sizeDelta.y-size.y)<=4 && Mathf.Abs(r.anchoredPosition.x-pos.x)<=2 && Mathf.Abs(r.anchoredPosition.y-pos.y)<=4;
            if(thin) {v.mask=0;v.target.SetActive(false);continue;}
            if(island==null && Vector2.Distance(r.anchoredPosition,pos)<1f && r.sizeDelta.x>3 &&
               (v.target.GetComponent<UnityEngine.UI.Image>()!=null || v.target.GetComponent<SkillSyncRoundedGraphic>()!=null)) island=v;
        }
        var visuals=view.visuals.ToList();
        if(island==null) {
            var created=chrome.Find(UndoIslandName);
            var go=created!=null ? created.gameObject : new GameObject(UndoIslandName,typeof(RectTransform));
            if(created==null) go.transform.SetParent(chrome,false);
            island=visuals.FirstOrDefault(v=>v.target==go);
            if(island==null) {
                island=new SkillSyncDesignView.Visual{target=go,role="undoIsland",mask=control.mask};
                visuals.Add(island);
            }
        }
        var rect=(RectTransform)island.target.transform;
        var oldImage=island.target.GetComponent<UnityEngine.UI.Image>();
        if(oldImage!=null) UnityEngine.Object.DestroyImmediate(oldImage);
        var graphic=island.target.GetComponent<SkillSyncRoundedGraphic>();
        if(graphic==null) graphic=island.target.AddComponent<SkillSyncRoundedGraphic>();
        graphic.radius=10;graphic.borderWidth=1;graphic.color=Color.white;
        graphic.borderColor=new Color32(205,213,224,255);graphic.raycastTarget=false;
        graphic.SetVerticesDirty();
        Place(rect,pos.x,-pos.y,size.x,size.y);
        rect.SetSiblingIndex(Mathf.Max(0,button.GetSiblingIndex()-1));
        view.visuals=visuals.ToArray();
    }

    // Save: a hit target that ends left of the mode tabs (x=425) plus a visible pill for the "saved" label.
    static void SaveButton(SkillSyncDesignView view,RectTransform chrome)
    {
        var save=Control(view,"Save");
        if(save==null) {Debug.LogWarning("[SkillSync] Save control missing.");return;}
        const float x=276,y=46,w=140,h=28;
        Place((RectTransform)save.button.transform,x,y,w,h);
        var savedLabels=view.visuals.Where(v=>v.role=="saved" && v.label!=null).ToList();
        if(savedLabels.Count==0) {Debug.LogWarning("[SkillSync] 'saved' label missing.");return;}
        int mask=0;foreach(var v in savedLabels) mask|=v.mask;
        var visuals=view.visuals.ToList();
        var existing=chrome.Find(SavedPillName);
        var pill=existing!=null ? existing.gameObject : new GameObject(SavedPillName,typeof(RectTransform));
        if(existing==null) pill.transform.SetParent(chrome,false);
        var entry=visuals.FirstOrDefault(v=>v.target==pill);
        var first=savedLabels[0];
        int[] orders=first.orders==null ? null : first.orders.Select(o=>o-1).ToArray();
        if(entry==null) {
            entry=new SkillSyncDesignView.Visual{target=pill,role="savedPill"};
            visuals.Insert(visuals.IndexOf(first),entry);
        }
        entry.mask=mask;entry.orders=orders;
        Place((RectTransform)pill.transform,x,y,w,h);
        var oldImage=pill.GetComponent<UnityEngine.UI.Image>();
        if(oldImage!=null) UnityEngine.Object.DestroyImmediate(oldImage);
        var graphic=pill.GetComponent<SkillSyncRoundedGraphic>();
        if(graphic==null) graphic=pill.AddComponent<SkillSyncRoundedGraphic>();
        graphic.radius=14;graphic.borderWidth=1;graphic.color=Color.white;
        graphic.borderColor=new Color32(205,213,224,255);graphic.raycastTarget=false;
        graphic.SetVerticesDirty();
        pill.transform.SetSiblingIndex(first.target.transform.GetSiblingIndex());
        foreach(var v in savedLabels) {
            Place(v.label.rectTransform,x,y,w,h);
            v.label.alignment=TextAlignmentOptions.Midline;v.label.margin=Vector4.zero;
            v.label.textWrappingMode=TextWrappingModes.NoWrap;v.label.overflowMode=TextOverflowModes.Ellipsis;
            if(v.target!=v.label.gameObject) Place((RectTransform)v.target.transform,x,y,w,h);
        }
        view.visuals=visuals.ToArray();
    }

    // L-28: the export-modal scrim also dims the header (y 0..1000).
    static void ModalScrim(SkillSyncDesignView view)
    {
        if(view.modal==null) return;
        var modalRect=(RectTransform)view.modal.transform;
        foreach(var v in view.visuals) {
            if(v.label!=null || v.target==null || v.target.transform.parent!=modalRect) continue;
            var image=v.target.GetComponent<UnityEngine.UI.Image>();
            if(image==null) continue;
            var r=(RectTransform)v.target.transform;
            bool scrim=(image.material!=null && image.material.name=="ScrimLinear") || (Mathf.Abs(r.anchoredPosition.x)<.1f && (Mathf.Abs(r.anchoredPosition.y+88)<.1f || Mathf.Abs(r.anchoredPosition.y)<.1f && r.sizeDelta.y>=900));
            if(!scrim) continue;
            Place(r,0,0,Mathf.Max(r.sizeDelta.x,modalRect.rect.width),1000);
        }
    }

    static Rect RectOf(RectTransform r) => new Rect(r.anchoredPosition.x,-r.anchoredPosition.y,r.sizeDelta.x,r.sizeDelta.y);

    // Debug step: Save and the three tabs share the Chrome coordinate space and must not intersect.
    static void LogRects(SkillSyncDesignView view)
    {
        var rects=new List<(string name,Rect rect)>();
        var save=Control(view,"Save");
        if(save!=null) rects.Add(("Save",RectOf((RectTransform)save.button.transform)));
        foreach(var tab in Tabs) {
            var c=Control(view,tab);
            if(c!=null) rects.Add((tab,RectOf((RectTransform)c.button.transform)));
        }
        foreach(var r in rects) Debug.Log($"[SkillSync] Chrome rect {r.name}: x={r.rect.x:0.##} y={r.rect.y:0.##} w={r.rect.width:0.##} h={r.rect.height:0.##}");
        for(int i=0;i<rects.Count;i++)
            for(int j=i+1;j<rects.Count;j++)
                if(rects[i].rect.Overlaps(rects[j].rect))
                    throw new InvalidOperationException($"Chrome rects overlap: {rects[i].name} and {rects[j].name}");
    }
}
