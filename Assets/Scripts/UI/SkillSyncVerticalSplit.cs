using System;
using UnityEngine;
using UnityEngine.EventSystems;

public sealed class SkillSyncVerticalSplit : MonoBehaviour, IBeginDragHandler, IDragHandler
{
    [Serializable] public struct Item {public RectTransform rect;public float y,height,move,grow;}
    public SkillSyncDesignView view;
    public Item[] items;
    public float Offset {get;private set;}
    float start, pointer;
    void OnEnable() {
        if(!transform.Find("HitArea")) {
            var hit=NewChild("HitArea",new Vector2(0,.5f),new Vector2(1,.5f),new Vector2(0,12));
            var image=hit.gameObject.AddComponent<UnityEngine.UI.Image>();image.color=new Color(0,0,0,0);image.raycastTarget=true;
        }
        if(!transform.Find("Grip")) {
            var grip=NewChild("Grip",new Vector2(.5f,.5f),new Vector2(.5f,.5f),new Vector2(32,4));
            var image=grip.gameObject.AddComponent<UnityEngine.UI.Image>();image.color=new Color32(154,167,184,255);image.raycastTarget=false;
        }
    }
    RectTransform NewChild(string childName,Vector2 anchorMin,Vector2 anchorMax,Vector2 size) {
        var rect=new GameObject(childName,typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(transform,false);rect.anchorMin=anchorMin;rect.anchorMax=anchorMax;rect.pivot=new Vector2(.5f,.5f);
        rect.anchoredPosition=Vector2.zero;rect.sizeDelta=size;return rect;
    }
    public void OnBeginDrag(PointerEventData e) {start=Offset;pointer=Local(e);}
    public void OnDrag(PointerEventData e) {SetOffset(start+pointer-Local(e));}
    float Local(PointerEventData e) {RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)view.transform,e.position,e.pressEventCamera,out var p);return p.y;}
    public void SetOffset(float value) {Offset=Mathf.Clamp(value,-330,70);Apply();}
    public void Apply() {
        bool active=view.State==0||view.State==1||view.State==3||view.State==5;
        gameObject.SetActive(active);
        float delta=active?Offset:0;
        foreach(var item in items) {
            var p=item.rect.anchoredPosition;p.y=item.y-delta*item.move;item.rect.anchoredPosition=p;
            item.rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,item.height+delta*item.grow);
        }
    }
}
