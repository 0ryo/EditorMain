using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

[RequireComponent(typeof(CanvasRenderer))]
public sealed class SkillSyncOrientationGizmo : UnityEngine.UI.MaskableGraphic, IPointerClickHandler
{
    public Camera sourceCamera;
    /// <summary>クリックした軸の向き（ワールド座標。+Y なら上の軸）。その側から見る視点にするために使う。</summary>
    public System.Action<Vector3> axisClicked;
    Quaternion lastRotation;
    readonly Vector3[] axes={Vector3.right,Vector3.up,Vector3.forward,-Vector3.right,-Vector3.up,-Vector3.forward};
    readonly Color32[] colors={new Color32(159,29,23,255),new Color32(25,97,47,255),new Color32(29,78,216,255)};
    readonly int[] order={0,1,2,3,4,5};
    readonly TMP_Text[] labels=new TMP_Text[3];
    protected override void OnEnable() {
        base.OnEnable();
        string[] names={"X","Y","Z"};
        for(int i=0;i<3;i++) {
            var child=transform.Find("AxisLabel"+names[i]);
            if(!child) {
                var rect=new GameObject("AxisLabel"+names[i],typeof(RectTransform)).GetComponent<RectTransform>();
                rect.SetParent(transform,false);rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(.5f,.5f);rect.sizeDelta=new Vector2(14,14);
                var text=rect.gameObject.AddComponent<TextMeshProUGUI>();
                text.text=names[i];text.fontSize=10;text.fontStyle=FontStyles.Bold;text.alignment=TextAlignmentOptions.Center;
                text.textWrappingMode=TextWrappingModes.NoWrap;text.raycastTarget=false;text.color=colors[i];
                child=rect;
            }
            labels[i]=child.GetComponent<TMP_Text>();
        }
        UpdateLabels();
    }
    public void OnPointerClick(PointerEventData eventData) {
        if(sourceCamera==null || axisClicked==null) return;
        if(!RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform,eventData.position,eventData.pressEventCamera,out var local)) return;
        var rotation=Quaternion.Inverse(sourceCamera.transform.rotation);
        var center=rectTransform.rect.center;
        int best=-1;float bestDepth=float.MaxValue;
        for(int i=0;i<axes.Length;i++) {
            var direction=rotation*axes[i];
            bool hit=Vector2.Distance(local,center+(Vector2)direction*26)<=13f || (i<3 && Vector2.Distance(local,center+(Vector2)direction*36)<=10f);
            // Among overlapping tips the one nearest the viewer (smallest depth) wins.
            if(hit && direction.z<bestDepth) {bestDepth=direction.z;best=i;}
        }
        if(best>=0) axisClicked(axes[best]);
    }
    void UpdateLabels() {
        if(sourceCamera==null) return;
        var rotation=Quaternion.Inverse(sourceCamera.transform.rotation);
        for(int i=0;i<3;i++) {
            if(labels[i]==null) continue;
            var direction=rotation*axes[i];
            labels[i].rectTransform.anchoredPosition=(Vector2)direction*36;
        }
    }
    void LateUpdate() {
        if(sourceCamera!=null && lastRotation!=sourceCamera.transform.rotation) {lastRotation=sourceCamera.transform.rotation;SetVerticesDirty();UpdateLabels();}
    }
    protected override void OnPopulateMesh(UnityEngine.UI.VertexHelper vh)
    {
        vh.Clear();if(sourceCamera==null) return;
        var rotation=Quaternion.Inverse(sourceCamera.transform.rotation);
        System.Array.Sort(order,(a,b)=>(rotation*axes[b]).z.CompareTo((rotation*axes[a]).z));
        var center=rectTransform.rect.center;
        foreach(int index in order) {
            var direction=rotation*axes[index];var tip=center+(Vector2)direction*26;
            Color tint=colors[index%3];if(index>=3) tint=Color.Lerp(tint,new Color(.6f,.64f,.7f),.45f);
            float width=index<3?3:2;float radius=index<3?5:3;
            Segment(vh,center,tip,tint,width);Circle(vh,tip,radius,tint);
        }
        Circle(vh,center,2.5f,DesignTokens.TextPrimary);
    }
    static void Circle(UnityEngine.UI.VertexHelper vh,Vector2 center,float radius,Color color) {
        int start=vh.currentVertCount;vh.AddVert(center,color,Vector2.zero);
        for(int i=0;i<=20;i++) {float angle=i*Mathf.PI/10;vh.AddVert(center+new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*radius,color,Vector2.zero);if(i>0) vh.AddTriangle(start,start+i,start+i+1);}
    }
    static void Segment(UnityEngine.UI.VertexHelper vh,Vector2 a,Vector2 b,Color color,float width) {
        var delta=b-a;if(delta.sqrMagnitude<.001f) return;
        var side=new Vector2(-delta.y,delta.x).normalized*width/2;int start=vh.currentVertCount;
        vh.AddVert(a-side,color,Vector2.zero);vh.AddVert(a+side,color,Vector2.zero);vh.AddVert(b+side,color,Vector2.zero);vh.AddVert(b-side,color,Vector2.zero);
        vh.AddTriangle(start,start+1,start+2);vh.AddTriangle(start,start+2,start+3);
    }
}
