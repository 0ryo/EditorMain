using UnityEngine;

// Projects the real condition radius and centre-to-centre line into the measured viewport.
[RequireComponent(typeof(CanvasRenderer))]
public sealed class SkillSyncViewportOverlay : UnityEngine.UI.MaskableGraphic
{
    Camera sourceCamera;
    Transform first,second;
    float radius;
    bool satisfied;
    public void Configure(Camera camera,Transform a,Transform b,float metres,bool inRange)
    {sourceCamera=camera;first=a;second=b;radius=metres;satisfied=inRange;SetVerticesDirty();}
    protected override void OnPopulateMesh(UnityEngine.UI.VertexHelper vh)
    {
        vh.Clear();if(sourceCamera==null || first==null || second==null || radius<=0) return;
        var a=SkillSyncTrialSession.Center(first);var b=SkillSyncTrialSession.Center(second);
        Color line=satisfied?new Color32(21,115,74,255):new Color32(8,102,232,255);
        for(int i=0;i<96;i++)
        {
            float start=i*2*Mathf.PI/96,end=(i+1)*2*Mathf.PI/96;
            Vector3 p=b+new Vector3(Mathf.Cos(start),0,Mathf.Sin(start))*radius;
            Vector3 q=b+new Vector3(Mathf.Cos(end),0,Mathf.Sin(end))*radius;
            if(Project(b,out var center) && Project(p,out var left) && Project(q,out var right))
            {
                int index=vh.currentVertCount;var fill=line;fill.a=.08f;
                vh.AddVert(center,fill,Vector2.zero);vh.AddVert(left,fill,Vector2.zero);vh.AddVert(right,fill,Vector2.zero);
                vh.AddTriangle(index,index+1,index+2);
                if(i%2==0) Segment(vh,left,right,line,1.4f);
            }
        }
        if(Project(a,out var pa) && Project(b,out var pb))
        {
            int count=Mathf.Max(1,Mathf.CeilToInt(Vector2.Distance(pa,pb)/10));
            for(int i=0;i<count;i+=2) Segment(vh,Vector2.Lerp(pa,pb,(float)i/count),Vector2.Lerp(pa,pb,(float)(i+1)/count),line,1.5f);
        }
    }
    bool Project(Vector3 world,out Vector2 local)
    {
        var point=sourceCamera.WorldToScreenPoint(world);local=default;
        return point.z>0 && RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform,point,null,out local);
    }
    static void Segment(UnityEngine.UI.VertexHelper vh,Vector2 a,Vector2 b,Color color,float width)
    {
        var delta=b-a;if(delta.sqrMagnitude<.0001f) return;
        var side=new Vector2(-delta.y,delta.x).normalized*width*.5f;int i=vh.currentVertCount;
        vh.AddVert(a-side,color,Vector2.zero);vh.AddVert(a+side,color,Vector2.zero);
        vh.AddVert(b+side,color,Vector2.zero);vh.AddVert(b-side,color,Vector2.zero);
        vh.AddTriangle(i,i+1,i+2);vh.AddTriangle(i,i+2,i+3);
    }
    void LateUpdate() {if(first!=null && second!=null) SetVerticesDirty();}
}
