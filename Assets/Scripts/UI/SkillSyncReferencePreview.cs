#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEngine;

// Only present in the separately generated reference scene; never attached to UIRoot.
[ExecuteAlways]
public sealed class SkillSyncReferencePreview : MonoBehaviour
{
    [Serializable] public class SampleField { public int state; public string role,value; }
    [Range(0,5)] public int state;
    public SkillSyncDesignView view;
    public SkillSyncDesignView.Visual[] fixtureVisuals;
    public SampleField[] fields;
    public Transform samplePart;
    public Transform sampleMarker;
    public Camera sampleCamera;
    public Renderer partRenderer;
    public Material partMaterial,ghostMaterial;
    public Vector3 initialPartPosition,trialPartPosition;
    int applied=-1;
    public void ApplyState(int value)
    {
        state=Mathf.Clamp(value,0,5);applied=state;
        view.Show(state);
        view.libraryContent.parent.parent.gameObject.SetActive(false);
        view.stepsContent.parent.parent.gameObject.SetActive(false);
        view.objectsContent.parent.parent.gameObject.SetActive(false);
        foreach(var visual in fixtureVisuals) visual.target.SetActive((visual.mask & (1<<state))!=0);
        foreach(var visual in fixtureVisuals.Where(v=>v.target.activeSelf).OrderBy(v=>v.orders[state])) visual.target.transform.SetAsLastSibling();
        if(view.objectAThumbnail!=null) view.objectAThumbnail.gameObject.SetActive(false);
        if(view.objectBThumbnail!=null) view.objectBThumbnail.gameObject.SetActive(false);
        foreach(var field in fields) if(field.state==state) view.Value(field.role,field.value);
        if(state==1 || state==5) view.Value("body","部品Aを目印Bに近づけ、\nそのまま3秒間保ってください。");
        view.viewportDistance.text=state==1||state==5?"現在の距離 65 cm":"現在の距離 32 cm";
        view.viewportGrid.text="グリッド 10 cm";
        view.viewportGrid.color=new Color32(88,103,124,255);
        if(view.partALabel!=null) {view.partALabel.text="部品A";view.partALabel.transform.parent.gameObject.SetActive(state!=5);}
        if(view.partBLabel!=null) {view.partBLabel.text="目印B";view.partBLabel.transform.parent.gameObject.SetActive(state!=5);}
        view.holdProgress.rectTransform.sizeDelta=new Vector2(state==4?352:211,8);
        view.holdProgress.color=state==4?new Color32(21,115,74,255):new Color32(8,102,232,255);
        if(samplePart!=null) samplePart.position=state==2||state==4?trialPartPosition:initialPartPosition;
        if(view.partALabel!=null) ((RectTransform)view.partALabel.transform.parent).anchoredPosition=state==2||state==4?new Vector2(SkillSyncDesignLayout.MapX(732),-397):new Vector2(SkillSyncDesignLayout.MapX(508),-354);
        if(view.viewportDistance!=null) {
            var badge=(RectTransform)view.viewportDistance.transform.parent;
            badge.anchoredPosition=state==2||state==4?new Vector2(SkillSyncDesignLayout.MapX(706),-550):new Vector2(SkillSyncDesignLayout.MapX(628),-448);
            badge.sizeDelta=new Vector2(state==2||state==4?179:178,36);
            view.viewportDistance.color=state==2||state==4?new Color32(21,115,74,255):new Color32(23,34,54,255);
        }
        if(partRenderer!=null) partRenderer.sharedMaterial=state==3?ghostMaterial:partMaterial;
        if(view.conditionOverlay!=null) view.conditionOverlay.Configure(sampleCamera,state==0||state==3?null:samplePart,sampleMarker,.5f,state==2||state==4);
    }
    void Update()
    {
        if(view==null) return;
        if(applied!=state) ApplyState(state);
        if(sampleCamera!=null)
        {
            var corners=new Vector3[4];view.viewport.GetWorldCorners(corners);
            var min=RectTransformUtility.WorldToScreenPoint(null,corners[0]);var max=RectTransformUtility.WorldToScreenPoint(null,corners[2]);
            sampleCamera.rect=new Rect(min.x/Screen.width,min.y/Screen.height,(max.x-min.x)/Screen.width,(max.y-min.y)/Screen.height);
        }
    }
}
#endif
