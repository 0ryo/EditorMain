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
