using UnityEngine;

[RequireComponent(typeof(CanvasRenderer))]
public sealed class SkillSyncOrientationGizmo : UnityEngine.UI.MaskableGraphic
{
    public Camera sourceCamera;
    Quaternion lastRotation;
    readonly Vector3[] axes={Vector3.right,Vector3.up,Vector3.forward,-Vector3.right,-Vector3.up,-Vector3.forward};
    readonly Color32[] colors={new Color32(226,86,76,255),new Color32(64,170,112,255),new Color32(65,135,235,255)};
    readonly int[] order={0,1,2,3,4,5};
    void LateUpdate() {
        if(sourceCamera!=null && lastRotation!=sourceCamera.transform.rotation) {lastRotation=sourceCamera.transform.rotation;SetVerticesDirty();}
    }
    protected override void OnPopulateMesh(UnityEngine.UI.VertexHelper vh)
    {
        vh.Clear();if(sourceCamera==null) return;
        var rotation=Quaternion.Inverse(sourceCamera.transform.rotation);
        System.Array.Sort(order,(a,b)=>(rotation*axes[b]).z.CompareTo((rotation*axes[a]).z));
        var center=rectTransform.rect.center;
        foreach(int index in order) {
            var direction=rotation*axes[index];var tip=center+(Vector2)direction*26;
            Color tint=colors[index%3];if(index>=3) tint=Color.Lerp(tint,new Color(.6f,.64f,.7f),.7f);
            Segment(vh,center,tip,tint,index<3?3:2);Circle(vh,tip,index<3?5:3,tint);
        }
        Circle(vh,center,4,new Color32(220,227,238,255));
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
