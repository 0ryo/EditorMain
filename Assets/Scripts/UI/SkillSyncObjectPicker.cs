using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public sealed class SkillSyncObjectPicker : MonoBehaviour
{
    public GameObject menu, picking;
    public RectTransform panel, content;
    public UnityEngine.UI.Button template, dismiss, cancel;
    public TMP_Text instruction;
    public bool IsOpen=>menu.activeSelf;
    public bool BlocksPointer(Vector2 point)=>IsOpen || (picking.activeSelf && RectTransformUtility.RectangleContainsScreenPoint((RectTransform)instruction.transform.parent,point));
    readonly List<GameObject> rows=new List<GameObject>();
    public Action CancelPicking;
    void Awake() {dismiss.onClick.AddListener(Close);cancel.onClick.AddListener(()=>CancelPicking?.Invoke());}
    public void Close() {menu.SetActive(false);}
    public void Show(PlacedObject[] objects,float y,Action<string> choose,Action eyedropper) {
        foreach(var row in rows) Destroy(row);rows.Clear();
        float height=Mathf.Min(300,(objects.Length+1)*42+12);
        panel.anchoredPosition=new Vector2(1200+SkillSyncDesignLayout.Extra,-Mathf.Min(y,988-height));
        panel.sizeDelta=new Vector2(360,height);
        Add("ビューポートから選択…",eyedropper);
        foreach(var obj in objects) {
            var id=obj.Id;Add(obj.GetDisplayName(),()=>choose(id));
        }
        void Add(string label,Action action) {
            var row=Instantiate(template,content);row.gameObject.SetActive(true);
            row.GetComponentInChildren<TMP_Text>().text=label;
            row.onClick.AddListener(()=>{Close();action();});rows.Add(row.gameObject);
        }
        menu.SetActive(true);menu.transform.SetAsLastSibling();
        content.anchoredPosition=Vector2.zero;
    }
    public void SetPicking(string target) {
        picking.SetActive(target!=null);
        if(target==null)return;
        instruction.text=(target=="A"?"動かすもの":"近づける先")+"をビューポートでクリックしてください";
        picking.transform.SetAsLastSibling();
    }
}
