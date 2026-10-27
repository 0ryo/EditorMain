using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// Target chooser for a condition: a searchable, collapsible part tree, plus the on-screen banner shown while
// a target is picked in the 3D view (the first click only marks the whole group as provisional).
public sealed class SkillSyncObjectPicker : MonoBehaviour
{
    const float RowHeight = 34, Indent = 18, PanelWidth = 360, SearchHeight = 36;
    static readonly Color32 Ink = new Color32(23, 34, 54, 255), Muted = new Color32(88, 103, 124, 255),
        Blue = new Color32(8, 102, 232, 255), BlueSoft = new Color32(234, 242, 255, 255), Line = new Color32(205, 213, 224, 255);

    public GameObject menu, picking;
    public RectTransform panel, content;
    public UnityEngine.UI.Button template, dismiss, cancel;
    public TMP_Text instruction;
    public bool IsOpen=>menu.activeSelf;
    public bool BlocksPointer(Vector2 point)=>IsOpen || (picking.activeSelf && RectTransformUtility.RectangleContainsScreenPoint((RectTransform)instruction.transform.parent,point));
    readonly List<GameObject> rows=new List<GameObject>();
    ObjectCandidatePreview preview, provisionalPreview;
    public Action CancelPicking, ConfirmPicking;
    public PlacedObject Provisional { get; private set; }
    UnityEngine.UI.Button confirm;
    TMP_InputField search;
    // Tree state of the open menu.
    PlacedObject[] all=new PlacedObject[0];
    readonly Dictionary<PlacedObject,List<PlacedObject>> children=new Dictionary<PlacedObject,List<PlacedObject>>();
    readonly List<PlacedObject> roots=new List<PlacedObject>();
    readonly HashSet<PlacedObject> expanded=new HashSet<PlacedObject>(), forcedClosed=new HashSet<PlacedObject>();
    readonly Dictionary<PlacedObject,string> labels=new Dictionary<PlacedObject,string>();
    Action<string> choose;
    Action eyedropper;
    string currentId, excludedId, suggestedId;

    void Awake() {dismiss.onClick.AddListener(Close);cancel.onClick.AddListener(()=>CancelPicking?.Invoke());}
    public void Close() {menu.SetActive(false);ClearPreview();}

    public static bool ConfirmPressedThisFrame()
    {
#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter);
#elif ENABLE_INPUT_SYSTEM
        var keyboard=Keyboard.current;
        return keyboard!=null && (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame);
#else
        return false;
#endif
    }

    // ---- Names shown to the user: no internal IDs, the part name first so a narrow field never cuts it. ----
    public static string PartName(PlacedObject part)
    {
        string name=part.GetDisplayName();
        if(part.modelRoot)
        {
            int count=0,ordinal=0;
            foreach(var other in part.modelRoot.GetComponentsInChildren<PlacedObject>(true))
            {
                if(other==part.modelRoot || other.modelRoot!=part.modelRoot || other.GetDisplayName()!=name) continue;
                count++;
                if(other==part) ordinal=count;
            }
            if(count>1) name+=" ("+ordinal+")";
        }
        return name;
    }
    static string NodeLabel(PlacedObject part)=>PartName(part)+(ImportedModelParts.IsGroup(part)?"（グループ全体）":"");
    // Short form for fields: "Front_Nat1_Left — 自動車".
    public static string DisplayLabel(PlacedObject part)
    {
        if(!part) return "";
        string name=NodeLabel(part);
        return part.modelRoot?name+" — "+part.modelRoot.GetDisplayName():name;
    }
    // Full form for hover captions: "自動車 › Wheel › Front_Nat1_Left".
    public static string PathLabel(PlacedObject part)
    {
        if(!part) return "";
        var names=new List<string>();
        for(var node=part.transform;node!=null;node=node.parent)
        {
            var placed=node.GetComponent<PlacedObject>();
            if(placed) names.Insert(0,placed==part?NodeLabel(placed):placed.GetDisplayName());
        }
        return string.Join(" › ",names);
    }
    static int Natural(string a,string b)
    {
        int i=0,j=0;
        while(i<a.Length && j<b.Length)
        {
            if(char.IsDigit(a[i]) && char.IsDigit(b[j]))
            {
                int si=i,sj=j;
                while(i<a.Length && char.IsDigit(a[i])) i++;
                while(j<b.Length && char.IsDigit(b[j])) j++;
                string na=a.Substring(si,i-si).TrimStart('0'),nb=b.Substring(sj,j-sj).TrimStart('0');
                if(na.Length!=nb.Length) return na.Length.CompareTo(nb.Length);
                int number=string.CompareOrdinal(na,nb);
                if(number!=0) return number;
            }
            else
            {
                int letter=char.ToLowerInvariant(a[i]).CompareTo(char.ToLowerInvariant(b[j]));
                if(letter!=0) return letter;
                i++;j++;
            }
        }
        return (a.Length-i).CompareTo(b.Length-j);
    }

    // suggestedId: the part selected on the placement screen, offered once at the top while the field is empty (never filled in by itself).
    public void Show(PlacedObject[] objects,RectTransform anchor,Action<string> choose,Action eyedropper,string currentId=null,string excludedId=null,string suggestedId=null) {
        ClearPreview();
        this.choose=choose;this.eyedropper=eyedropper;this.currentId=currentId;this.excludedId=excludedId;this.suggestedId=suggestedId;
        all=objects;
        BuildTree();
        expanded.Clear();forcedClosed.Clear();
        // Open the path to the present target so it is visible without searching.
        var current=all.FirstOrDefault(o=>o.Id==currentId);
        for(var node=current?current.transform.parent:null;node!=null;node=node.parent)
        {
            var placed=node.GetComponent<PlacedObject>();
            if(placed && children.ContainsKey(placed)) expanded.Add(placed);
        }
        EnsureSearch();
        search.SetTextWithoutNotify("");
        menu.SetActive(true);menu.transform.SetAsLastSibling();
        Rebuild(true);
        // Directly under the field that was pressed (above it when there is more room there), only as tall as its rows,
        // so the label above the field stays visible and each field's list opens at its own place.
        float wanted=Mathf.Min(480,SearchHeight+30+rows.Count*RowHeight);
        if(all.Length+1>rows.Count) wanted=Mathf.Max(wanted,Mathf.Min(480,SearchHeight+30+6*RowHeight));
        var corners = new Vector3[4];
        anchor.GetWorldCorners(corners);
        var menuRect = (RectTransform)menu.transform;
        var bottomLeft = menuRect.InverseTransformPoint(corners[0]);
        var topLeft = menuRect.InverseTransformPoint(corners[1]);
        float under = menuRect.rect.yMax - bottomLeft.y, over = menuRect.rect.yMax - topLeft.y;
        float below = menuRect.rect.height - 12 - under, above = over - 12;
        bool openBelow = below >= wanted || below >= above;
        float height = Mathf.Max(SearchHeight + 30 + 2 * RowHeight, Mathf.Min(wanted, openBelow ? below : above));
        float top = openBelow ? under : Mathf.Max(12, over - height);
        panel.anchoredPosition = new Vector2(bottomLeft.x - menuRect.rect.xMin, -top);
        panel.sizeDelta=new Vector2(PanelWidth,height);
        search.Select();search.ActivateInputField();
    }

    void BuildTree()
    {
        children.Clear();roots.Clear();labels.Clear();
        var set=new HashSet<PlacedObject>(all);
        // Same-name parts of one model are numbered once per model, not once per row.
        var ordinals=new Dictionary<PlacedObject,Dictionary<PlacedObject,string>>();
        foreach(var o in all)
        {
            string name=o.GetDisplayName();
            if(o.modelRoot)
            {
                if(!ordinals.TryGetValue(o.modelRoot,out var map))
                {
                    map=new Dictionary<PlacedObject,string>();
                    var seen=new Dictionary<string,int>();
                    var parts=o.modelRoot.GetComponentsInChildren<PlacedObject>(true).Where(p=>p!=o.modelRoot && p.modelRoot==o.modelRoot).ToList();
                    var counts=parts.GroupBy(p=>p.GetDisplayName()).ToDictionary(g=>g.Key,g=>g.Count());
                    foreach(var p in parts)
                    {
                        string n=p.GetDisplayName();
                        seen[n]=seen.TryGetValue(n,out int c)?c+1:1;
                        map[p]=counts[n]>1?n+" ("+seen[n]+")":n;
                    }
                    ordinals[o.modelRoot]=map;
                }
                if(map.TryGetValue(o,out var numbered)) name=numbered;
            }
            labels[o]=name+(ImportedModelParts.IsGroup(o)?"（グループ全体）":"");
        }
        foreach(var o in all)
        {
            PlacedObject parent=null;
            for(var node=o.transform.parent;node!=null && parent==null;node=node.parent)
            {
                var placed=node.GetComponent<PlacedObject>();
                if(placed && set.Contains(placed)) parent=placed;
            }
            if(parent==null) {roots.Add(o);continue;}
            if(!children.TryGetValue(parent,out var list)) children[parent]=list=new List<PlacedObject>();
            list.Add(o);
        }
        // A group whose only content is another group (a model's wrapper node) lists that group's parts directly,
        // so the same thing is not offered twice.
        foreach(var parent in children.Keys.ToList())
        {
            if(!children.TryGetValue(parent,out var list)) continue;
            while(list.Count==1 && children.TryGetValue(list[0],out var inner) && inner.Count>0) {children.Remove(list[0]);list=inner;}
            children[parent]=list;
        }
        roots.Sort((a,b)=>Natural(labels[a],labels[b]));
        foreach(var list in children.Values) list.Sort((a,b)=>Natural(labels[a],labels[b]));
    }

    void EnsureSearch()
    {
        var viewport=(RectTransform)content.parent;
        viewport.anchorMin=Vector2.zero;viewport.anchorMax=Vector2.one;
        viewport.offsetMin=new Vector2(6,6);viewport.offsetMax=new Vector2(-6,-(SearchHeight+14));
        if(search) return;
        var go=new GameObject("Search",typeof(RectTransform),typeof(SkillSyncRoundedGraphic));
        go.SetActive(false);
        go.transform.SetParent(panel,false);
        var rect=(RectTransform)go.transform;
        rect.anchorMin=new Vector2(0,1);rect.anchorMax=new Vector2(1,1);rect.pivot=new Vector2(.5f,1);
        rect.offsetMin=new Vector2(8,-(SearchHeight+8));rect.offsetMax=new Vector2(-8,-8);
        var background=go.GetComponent<SkillSyncRoundedGraphic>();
        background.color=Color.white;background.borderColor=Line;background.radius=8;
        var area=new GameObject("TextArea",typeof(RectTransform),typeof(UnityEngine.UI.RectMask2D));
        area.transform.SetParent(rect,false);
        var areaRect=(RectTransform)area.transform;
        areaRect.anchorMin=Vector2.zero;areaRect.anchorMax=Vector2.one;
        areaRect.offsetMin=new Vector2(12,0);areaRect.offsetMax=new Vector2(-12,0);
        TMP_Text Text(string name,string value,Color color,FontStyles style)
        {
            var text=new GameObject(name,typeof(RectTransform),typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
            text.transform.SetParent(areaRect,false);
            text.rectTransform.anchorMin=Vector2.zero;text.rectTransform.anchorMax=Vector2.one;
            text.rectTransform.offsetMin=text.rectTransform.offsetMax=Vector2.zero;
            text.font=instruction.font;text.fontSharedMaterial=instruction.fontSharedMaterial;
            text.fontSize=14;text.color=color;text.fontStyle=style;text.text=value;
            text.alignment=TextAlignmentOptions.MidlineLeft;text.richText=false;text.raycastTarget=false;
            text.textWrappingMode=TextWrappingModes.NoWrap;
            return text;
        }
        var placeholder=Text("Placeholder","名前で探す（例：ナット）",new Color32(128,142,160,255),FontStyles.Normal);
        var input=Text("Text","",Ink,FontStyles.Normal);
        search=go.AddComponent<TMP_InputField>();
        search.textViewport=areaRect;search.textComponent=input;search.placeholder=placeholder;
        search.targetGraphic=background;search.lineType=TMP_InputField.LineType.SingleLine;
        search.onFocusSelectAll=false;
        search.onValueChanged.AddListener(_=>{forcedClosed.Clear();Rebuild(false);});
        go.SetActive(true);
    }

    bool Matches(PlacedObject o,string term)=>labels[o].IndexOf(term,StringComparison.CurrentCultureIgnoreCase)>=0;

    void Rebuild(bool resetScroll)
    {
        float scrollY=content.anchoredPosition.y;
        foreach(var row in rows) {row.SetActive(false);Destroy(row);}
        rows.Clear();
        string term=search?search.text.Trim():"";
        var keep=new HashSet<PlacedObject>();
        var searchOpen=new HashSet<PlacedObject>();
        if(term.Length>0)
            foreach(var o in all)
            {
                if(!Matches(o,term)) continue;
                keep.Add(o);
                // A hit stays visible together with the path above it.
                for(var node=o.transform.parent;node!=null;node=node.parent)
                {
                    var placed=node.GetComponent<PlacedObject>();
                    if(placed && children.ContainsKey(placed) && labels.ContainsKey(placed)) {keep.Add(placed);searchOpen.Add(placed);}
                }
            }
        AddRow("3D画面で選択…",0,null,false,false,false,eyedropper,null,null,true);
        var suggested=term.Length==0 && string.IsNullOrEmpty(currentId) && !string.IsNullOrEmpty(suggestedId) && suggestedId!=excludedId?all.FirstOrDefault(o=>o.Id==suggestedId):null;
        if(suggested && labels.TryGetValue(suggested,out var suggestedLabel))
        {
            string id=suggested.Id;
            AddRow("選択中の部品：「"+suggestedLabel+"」",0,null,false,false,false,()=>choose(id),suggested,null,true);
        }
        int shown=0;
        void Emit(PlacedObject o,int depth)
        {
            if(term.Length>0 && !keep.Contains(o)) return;
            bool has=children.TryGetValue(o,out var kids) && kids.Count>0;
            bool open=has && (expanded.Contains(o) || (term.Length>0 && searchOpen.Contains(o))) && !forcedClosed.Contains(o);
            bool excluded=o.Id==excludedId && !string.IsNullOrEmpty(excludedId);
            var id=o.Id;
            AddRow(labels[o]+(excluded?"（もう一方の対象）":""),depth,has?(open?"▼":"▶"):null,o.Id==currentId,excluded,true,
                excluded?null:(Action)(()=>choose(id)),o,has?(Action)(()=>Toggle(o,open)):null);
            shown++;
            if(open) foreach(var child in kids) Emit(child,depth+1);
        }
        foreach(var root in roots) Emit(root,0);
        if(shown==0) AddRow("「"+term+"」に一致する部品はありません",0,null,false,true,false,null,null);
        UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(content);
        var viewport=(RectTransform)content.parent;
        content.anchoredPosition=new Vector2(0,resetScroll?0:Mathf.Clamp(scrollY,0,Mathf.Max(0,content.rect.height-viewport.rect.height)));
    }

    void Toggle(PlacedObject o,bool wasOpen)
    {
        if(wasOpen) {expanded.Remove(o);forcedClosed.Add(o);}
        else {forcedClosed.Remove(o);expanded.Add(o);}
        Rebuild(false);
    }

    void AddRow(string label,int depth,string arrow,bool current,bool disabled,bool indentLeaf,Action action,PlacedObject candidate,Action toggle=null,bool accent=false)
    {
        var row=Instantiate(template,content);row.gameObject.SetActive(true);
        rows.Add(row.gameObject);
        var layout=row.GetComponent<UnityEngine.UI.LayoutElement>();
        if(layout) layout.preferredHeight=RowHeight;
        float left=indentLeaf?8+depth*Indent+24:12;
        var text=row.GetComponentInChildren<TMP_Text>();
        text.text=label;
        text.color=disabled?Muted:accent?Blue:Ink;
        text.rectTransform.anchoredPosition=new Vector2(left,0);
        text.rectTransform.sizeDelta=new Vector2(PanelWidth-12-left-8,RowHeight);
        text.fontStyle=!disabled && depth==0 && indentLeaf?FontStyles.Bold:FontStyles.Normal;
        var background=row.GetComponent<SkillSyncRoundedGraphic>();
        if(background)
        {
            background.color=current?BlueSoft:Color.white;
            background.borderColor=current?Blue:Color.clear;
            background.borderWidth=current?1:0;
            background.radius=6;
        }
        row.interactable=!disabled && action!=null;
        if(action!=null) row.onClick.AddListener(()=>{Close();action();});
        if(candidate) row.gameObject.AddComponent<SkillSyncObjectCandidateItem>().Bind(this,candidate);
        if(arrow==null || toggle==null) return;
        var go=new GameObject("Toggle",typeof(RectTransform),typeof(UnityEngine.UI.Image),typeof(UnityEngine.UI.Button));
        go.transform.SetParent(row.transform,false);
        var rect=(RectTransform)go.transform;
        rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(0,1);
        rect.anchoredPosition=new Vector2(4+depth*Indent,0);rect.sizeDelta=new Vector2(28,RowHeight);
        var hit=go.GetComponent<UnityEngine.UI.Image>();hit.color=Color.clear;
        go.GetComponent<UnityEngine.UI.Button>().targetGraphic=hit;
        go.GetComponent<UnityEngine.UI.Button>().onClick.AddListener(()=>toggle());
        var glyph=new GameObject("Glyph",typeof(RectTransform),typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
        glyph.transform.SetParent(rect,false);
        glyph.rectTransform.anchorMin=Vector2.zero;glyph.rectTransform.anchorMax=Vector2.one;
        glyph.rectTransform.offsetMin=glyph.rectTransform.offsetMax=Vector2.zero;
        glyph.font=instruction.font;glyph.fontSharedMaterial=instruction.fontSharedMaterial;
        glyph.fontSize=11;glyph.color=Muted;glyph.text=arrow;glyph.alignment=TextAlignmentOptions.Midline;
        glyph.richText=false;glyph.raycastTarget=false;
    }

    // ---- 3D view picking banner ----
    void LayoutBanner()
    {
        var banner=(RectTransform)instruction.transform.parent;
        banner.sizeDelta=new Vector2(800,64);
        instruction.rectTransform.anchoredPosition=new Vector2(14,0);
        instruction.rectTransform.sizeDelta=new Vector2(500,64);
        instruction.textWrappingMode=TextWrappingModes.Normal;
        instruction.overflowMode=TextOverflowModes.Ellipsis;
        if(!confirm)
        {
            confirm=Instantiate(cancel,cancel.transform.parent);
            confirm.name="Confirm";
            confirm.onClick.RemoveAllListeners();
            confirm.onClick.AddListener(()=>ConfirmPicking?.Invoke());
            var surface=confirm.GetComponent<SkillSyncRoundedGraphic>();
            if(surface) {surface.color=Blue;surface.borderColor=Blue;}
            var caption=confirm.GetComponentInChildren<TMP_Text>(true);
            caption.text="この対象に決定";caption.color=Color.white;caption.fontStyle=FontStyles.Bold;
        }
        void Place(UnityEngine.UI.Button button,float x,float width)
        {
            var rect=(RectTransform)button.transform;
            rect.anchoredPosition=new Vector2(x,-15);rect.sizeDelta=new Vector2(width,34);
            var caption=button.GetComponentInChildren<TMP_Text>(true).rectTransform;
            caption.anchorMin=Vector2.zero;caption.anchorMax=Vector2.one;
            caption.offsetMin=new Vector2(6,0);caption.offsetMax=new Vector2(-6,0);
        }
        Place(confirm,524,150);
        Place(cancel,684,104);
        cancel.GetComponentInChildren<TMP_Text>(true).text="取消（Esc）";
    }

    void UpdateBanner()
    {
        LayoutBanner();
        if(Provisional)
        {
            bool whole=ImportedModelParts.IsGroup(Provisional);
            instruction.text="仮選択中：「"+DisplayLabel(Provisional)+"」\n"+(whole?
                "このまま「決定」するか、もう一度クリックして中の部品を選びます。":"「決定」（Enter）で確定します。別の部品はクリックで選び直せます。");
        }
        else instruction.text="対象をクリックしてください。\n最初のクリックではグループ全体が仮選択され、もう一度クリックすると中の部品を選べます。";
        confirm.interactable=Provisional;
    }

    public void SetPicking(string target) {
        if(picking.activeSelf!=(target!=null)) ClearPreview();
        picking.SetActive(target!=null);
        if(target==null) {SetProvisional(null);return;}
        UpdateBanner();
        picking.transform.SetAsLastSibling();
    }
    // The provisional pick keeps its own (blue) outline while the pointer previews other candidates (orange).
    public void SetProvisional(PlacedObject candidate)
    {
        if(Provisional==candidate && (candidate==null || provisionalPreview)) {if(picking.activeSelf) UpdateBanner();return;}
        Provisional=candidate;
        if(!candidate) {if(provisionalPreview) provisionalPreview.Clear();}
        else
        {
            if(!provisionalPreview) provisionalPreview=gameObject.AddComponent<ObjectCandidatePreview>();
            provisionalPreview.ShowSelection(new[]{candidate});
        }
        if(picking.activeSelf) UpdateBanner();
    }
    public void ShowCandidate(PlacedObject candidate)
    {
        if(!candidate) {ClearPreview();return;}
        if(!preview) preview=gameObject.AddComponent<ObjectCandidatePreview>();
        preview.Show(candidate,PathLabel(candidate),instruction.font);
    }
    public void ClearPreview() {if(preview) preview.Clear();}
    void OnDisable() {ClearPreview();if(provisionalPreview) provisionalPreview.Clear();Provisional=null;}
}

public sealed class SkillSyncObjectCandidateItem : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    SkillSyncObjectPicker picker;
    PlacedObject candidate;
    public void Bind(SkillSyncObjectPicker owner,PlacedObject target) {picker=owner;candidate=target;}
    public void OnPointerEnter(PointerEventData data) {if(picker) picker.ShowCandidate(candidate);}
    public void OnPointerExit(PointerEventData data) {if(picker) picker.ClearPreview();}
    void OnDisable() {if(picker) picker.ClearPreview();}
}
