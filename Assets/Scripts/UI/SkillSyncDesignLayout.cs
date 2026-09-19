using UnityEngine;

// Keep the measured typography isotropic; the extra 16:9 width belongs to the workspace.
public static class SkillSyncDesignLayout
{
    public const float Width=2560, Height=1440, Scale=1.44f;
    public const float Extra=Width/Scale-1600;
    public static float MapX(float x)=>x+Mathf.Clamp01((x-288)/848)*Extra;
    public static void AdaptReferenceHost(RectTransform host) {
        host.sizeDelta+=Vector2.right*Extra;
        foreach(RectTransform child in host) Adapt(child);
    }
    public static void Apply(SkillSyncDesignView view)
    {
        if(view.wideLayout) return;
        var root=(RectTransform)view.transform;
        foreach(RectTransform child in root) {
            if(child.name=="Chrome") {foreach(RectTransform item in child) Adapt(item);child.sizeDelta+=Vector2.right*Extra;}
            else if(child.name=="ExportValidation") {
                foreach(RectTransform item in child) {
                    if(item.rect.width>=1600) item.sizeDelta+=Vector2.right*Extra;
                    else item.anchoredPosition+=Vector2.right*(Extra/2);
                }
                child.sizeDelta+=Vector2.right*Extra;
            }
            else Adapt(child);
        }
        foreach(var v in view.visuals) {
            var rect=(RectTransform)v.target.transform;
            // The original orientation tile is replaced by an actual camera-relative gizmo.
            if(rect.anchoredPosition.x>=MapX(1060) && rect.anchoredPosition.x<=MapX(1114) && -rect.anchoredPosition.y>=185 && -rect.anchoredPosition.y<250) {
                v.mask=0;v.target.SetActive(false);
            }
            if(v.label!=null && (Mathf.Abs(rect.anchoredPosition.x-24)<1 || Mathf.Abs(rect.anchoredPosition.x-288)<1 || Mathf.Abs(rect.anchoredPosition.x-(1184+Extra))<1) && -rect.anchoredPosition.y>100 && -rect.anchoredPosition.y<180)
                rect.anchoredPosition+=Vector2.up*10;
        }
        var objects=view.objectsContent.parent.parent as RectTransform;
        foreach(var rect in objects.GetComponentsInChildren<RectTransform>(true))
            if(rect!=objects && rect.rect.width>=800) rect.sizeDelta+=Vector2.right*Extra;
        if(view.objectTemplate.status!=null) view.objectTemplate.status.rectTransform.anchoredPosition+=Vector2.right*Extra;
        view.conditionOverlay.rectTransform.sizeDelta=view.viewport.sizeDelta;
        root.sizeDelta=new Vector2(Width/Scale,1000);root.localScale=Vector3.one*Scale;
        var canvas=root.GetComponentInParent<Canvas>();
        var scaler=canvas.GetComponent<UnityEngine.UI.CanvasScaler>();
        scaler.referenceResolution=new Vector2(Width,Height);
        scaler.screenMatchMode=UnityEngine.UI.CanvasScaler.ScreenMatchMode.Expand;
        var go=new GameObject("ViewOrientation",typeof(RectTransform));
        var gizmoRect=(RectTransform)go.transform;gizmoRect.SetParent(view.viewport,false);
        gizmoRect.anchorMin=gizmoRect.anchorMax=gizmoRect.pivot=Vector2.one;
        gizmoRect.anchoredPosition=new Vector2(-14,-14);gizmoRect.sizeDelta=new Vector2(72,72);
        view.orientation=go.AddComponent<SkillSyncOrientationGizmo>();view.orientation.raycastTarget=false;
        view.wideLayout=true;
    }
    static void Adapt(RectTransform rect)
    {
        float x=rect.anchoredPosition.x;
        bool header=-rect.anchoredPosition.y<88;
        rect.anchoredPosition=new Vector2(header&&x>=540&&x<1160?x+Extra/2:MapX(x),rect.anchoredPosition.y);
        if(rect.rect.width>=800) rect.sizeDelta+=Vector2.right*Extra;
    }
}
