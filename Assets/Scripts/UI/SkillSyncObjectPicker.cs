using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

public sealed class SkillSyncObjectPicker : MonoBehaviour
{
    public GameObject menu, picking;
    public RectTransform panel, content;
    public UnityEngine.UI.Button template, dismiss, cancel;
    public TMP_Text instruction;
    public bool IsOpen=>menu.activeSelf;
    public bool BlocksPointer(Vector2 point)=>IsOpen || (picking.activeSelf && RectTransformUtility.RectangleContainsScreenPoint((RectTransform)instruction.transform.parent,point));
    readonly List<GameObject> rows=new List<GameObject>();
    ObjectCandidatePreview preview;
    public Action CancelPicking;
    void Awake() {dismiss.onClick.AddListener(Close);cancel.onClick.AddListener(()=>CancelPicking?.Invoke());}
    public void Close() {menu.SetActive(false);ClearPreview();}
    public void Show(PlacedObject[] objects,RectTransform anchor,Action<string> choose,Action eyedropper) {
        ClearPreview();
        foreach(var row in rows) Destroy(row);rows.Clear();
        float height=Mathf.Min(300,(objects.Length+1)*42+12);
        var corners = new Vector3[4];
        anchor.GetWorldCorners(corners);
        var menuRect = (RectTransform)menu.transform;
        var local = menuRect.InverseTransformPoint(corners[0]);
        float y = menuRect.rect.yMax - local.y;
        panel.anchoredPosition = new Vector2(local.x - menuRect.rect.xMin, -Mathf.Min(y, menuRect.rect.height - 12 - height));
        panel.sizeDelta=new Vector2(360,height);
        Add("ビューポートから選択…",eyedropper);
        Array.Sort(objects,(left,right)=>{
            int depth=ImportedModelParts.Depth(left).CompareTo(ImportedModelParts.Depth(right));
            return depth!=0?depth:string.Compare(ImportedModelParts.Label(left),ImportedModelParts.Label(right),StringComparison.CurrentCulture);
        });
        foreach(var obj in objects) {
            var id=obj.Id;
            var row=Add(ImportedModelParts.Label(obj),()=>choose(id));
            row.gameObject.AddComponent<SkillSyncObjectCandidateItem>().Bind(this,obj);
        }
        UnityEngine.UI.Button Add(string label,Action action) {
            var row=Instantiate(template,content);row.gameObject.SetActive(true);
            row.GetComponentInChildren<TMP_Text>().text=label;
            row.onClick.AddListener(()=>{Close();action();});rows.Add(row.gameObject);
            return row;
        }
        menu.SetActive(true);menu.transform.SetAsLastSibling();
        content.anchoredPosition=Vector2.zero;
    }
    public void SetPicking(string target) {
        if(picking.activeSelf!=(target!=null)) ClearPreview();
        picking.SetActive(target!=null);
        if(target==null)return;
        instruction.text="クリックでグループ全体、もう一度クリックで部品";
        picking.transform.SetAsLastSibling();
    }
    public void ShowCandidate(PlacedObject candidate)
    {
        if(!candidate) {ClearPreview();return;}
        if(!preview) preview=gameObject.AddComponent<ObjectCandidatePreview>();
        preview.Show(candidate,ImportedModelParts.Label(candidate),instruction.font);
    }
    public void ClearPreview() {if(preview) preview.Clear();}
    void OnDisable() {ClearPreview();}
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
