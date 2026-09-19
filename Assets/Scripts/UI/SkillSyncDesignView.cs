using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

// Serialized, editable uGUI elements. Source node IDs stay beside every measurement.
public sealed class SkillSyncDesignView : MonoBehaviour
{
    [Serializable] public sealed class Visual
    {
        public GameObject target;
        public TMP_Text label;
        public string role;
        public string[] sourceNodeIds;
        public int mask;
        public int[] orders;
    }
    [Serializable] public sealed class Control
    {
        public UnityEngine.UI.Button button;
        public string action;
        public int mask;
        public string[] sourceNodeIds;
    }
    [Serializable] public sealed class Field
    {
        public TMP_InputField input;
        public string role;
        public int mask;
        public string[] sourceNodeIds;
    }
    public Visual[] visuals;
    public Control[] controls;
    public Field[] fields;
    public RectTransform viewport;
    public GameObject modal;
    public GameObject modalBlocker;
    public RectTransform libraryContent, stepsContent, objectsContent;
    public SkillSyncDesignRow libraryTemplate, stepTemplate, objectTemplate;
    public UnityEngine.UI.Image holdProgress;
    public TMP_Text viewportDistance;
    public TMP_Text viewportGrid;
    public SkillSyncViewportOverlay conditionOverlay;
    public UnityEngine.UI.RawImage objectAThumbnail, objectBThumbnail;
    public TMP_Text partALabel, partBLabel;
    public bool wideLayout;
    public int pdfRevision;
    public TMP_Text inspectorTitle,inspectorSelection;
    public SkillSyncOrientationGizmo orientation;
    public int State { get; private set; }

    public void Show(int state)
    {
        State = state;
        int bit = 1 << state;
        foreach (var v in visuals) v.target.SetActive((v.mask & bit) != 0);
        foreach (var v in visuals.Where(v=>(v.mask & bit)!=0).OrderBy(v=>v.orders!=null && v.orders.Length>state?v.orders[state]:0))
            v.target.transform.SetAsLastSibling();
        foreach (var c in controls)
        {
            c.button.gameObject.SetActive((c.mask & bit) != 0);
            c.button.interactable=state!=5 || c.action=="編集を続ける" || c.action=="該当箇所を修正";
            c.button.transform.SetAsLastSibling();
        }
        foreach (var f in fields)
        {
            f.input.gameObject.SetActive((f.mask & bit) != 0);
            f.input.interactable=state!=5 && state!=2 && state!=4;
            f.input.transform.SetAsLastSibling();
            if(f.input.placeholder!=null) f.input.placeholder.gameObject.SetActive((f.mask & bit)!=0 && string.IsNullOrEmpty(f.input.text));
        }
        modal.SetActive(state == 5);
        modalBlocker.SetActive(state == 5);
        libraryContent.parent.parent.gameObject.SetActive(state == 0 || state == 3);
        stepsContent.parent.parent.gameObject.SetActive(state != 0 && state != 3);
        objectsContent.parent.parent.gameObject.SetActive(state != 2 && state != 4);
        holdProgress.gameObject.SetActive(state == 2 || state == 4);
        viewportDistance.transform.parent.gameObject.SetActive(state == 1 || state == 2 || state == 4 || state == 5);
        holdProgress.transform.SetAsLastSibling();
        libraryContent.parent.parent.SetAsLastSibling();
        stepsContent.parent.parent.SetAsLastSibling();
        objectsContent.parent.parent.SetAsLastSibling();
        if(objectAThumbnail!=null) {objectAThumbnail.gameObject.SetActive(state==1||state==5);objectAThumbnail.transform.SetAsLastSibling();}
        if(objectBThumbnail!=null) {objectBThumbnail.gameObject.SetActive(state==1);objectBThumbnail.transform.SetAsLastSibling();}
        UpdateSelectionHeader();
    }
    public void UpdateSelectionHeader()
    {
        if(pdfRevision==0 || State!=0 && State!=3) return;
        var title=visuals.FirstOrDefault(v=>v.role=="selectionTitle" && (v.mask & (1<<State))!=0)?.label;
        var status=visuals.FirstOrDefault(v=>v.role=="inspectorLabel" && (v.mask & (1<<State))!=0)?.label;
        if(title==null || status==null) return;
        float width=Mathf.Min(320,title.GetPreferredValues(title.text).x);
        title.rectTransform.sizeDelta=new Vector2(width,40);title.overflowMode=TextOverflowModes.Ellipsis;
        var position=status.rectTransform.anchoredPosition;
        status.rectTransform.anchoredPosition=new Vector2(title.rectTransform.anchoredPosition.x+width+7,position.y);
    }
    public void Text(string role, string value)
    {
        foreach (var v in visuals) if (v.role == role && v.label != null) v.label.text = value ?? "";
    }
    public void Value(string role, string value)
    {
        foreach (var f in fields) if (f.role == role && !f.input.isFocused) f.input.SetTextWithoutNotify(value ?? "");
    }
    public void Enable(string action, bool enabled)
    {
        foreach (var c in controls) if (c.action == action) c.button.interactable = enabled && State!=5;
    }
    public void Focus(string role)
    {
        foreach (var f in fields) if (f.role == role && f.input.gameObject.activeInHierarchy)
        { f.input.Select(); f.input.ActivateInputField(); break; }
    }
}
