using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public static class UnityPickerAudit
{
    public static async Task<string> Verify()
    {
        Application.runInBackground=true;
        var view=UnityEngine.Object.FindFirstObjectByType<SkillSyncDesignView>();
        var picker=view.GetComponent<SkillSyncObjectPicker>();
        void Click(string action)=>view.controls.First(c=>c.action==action).button.onClick.Invoke();
        Click("手順を編集");await Task.Delay(300);
        foreach(var f in view.fields) if(f.input.richText || f.input.textComponent.richText) throw new Exception("IME markup enabled");
        Click("PickB");await Task.Delay(50);
        var options=picker.content.GetComponentsInChildren<Button>();
        if(!picker.IsOpen || options.Length<3) throw new Exception("Missing placed objects in dropdown");
        options[1].onClick.Invoke();await Task.Delay(100);
        if(picker.IsOpen)throw new Exception("Dropdown did not close after choosing");
        var controller=SkillSyncEditorController.Active;
        var condition=controller.GetType().GetProperty("Condition",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(controller);
        var node=(ScenarioNode)condition;
        if(string.IsNullOrEmpty(node.condition.objectBId))throw new Exception("No object bound to graph");
        Click("PickB");await Task.Delay(50);picker.content.GetComponentsInChildren<Button>()[0].onClick.Invoke();await Task.Delay(50);
        if(!picker.picking.activeSelf || !picker.instruction.text.Contains("近づける先"))throw new Exception("Missing eyedropper cue");
        picker.cancel.onClick.Invoke();await Task.Delay(50);
        if(picker.picking.activeSelf)throw new Exception("Cancel did not exit picking");
        // Test scrolling while the real controller's animation Tick is running, not just OnScroll synchronously.
        var content=view.objectsContent;var scroll=content.GetComponentInParent<ScrollRect>();
        var rows=new List<GameObject>();
        for(int i=0;i<12;i++) {
            var row=new GameObject("ScrollPersistenceAudit",typeof(RectTransform),typeof(LayoutElement));
            row.transform.SetParent(content,false);row.GetComponent<LayoutElement>().preferredHeight=58;rows.Add(row);
        }
        LayoutRebuilder.ForceRebuildLayoutImmediate(content);await Task.Delay(100);
        scroll.verticalNormalizedPosition=1;scroll.StopMovement();Canvas.ForceUpdateCanvases();
        float before=content.anchoredPosition.y;
        scroll.OnScroll(new PointerEventData(EventSystem.current){scrollDelta=new Vector2(0,-1)});
        await Task.Delay(350);
        if(content.anchoredPosition.y-before<47 || content.rect.height<=scroll.viewport.rect.height)throw new Exception("Scroll reset on later frame");
        foreach(var row in rows)UnityEngine.Object.DestroyImmediate(row);
        LayoutRebuilder.ForceRebuildLayoutImmediate(content);scroll.verticalNormalizedPosition=1;
        Click("PickB");
        string result="PASS: plain-text IME configuration; object dropdown binds actual graph; visible picking cue and cancel; object scroll persists across controller frames. OS IME interaction not automated.";
        System.IO.File.WriteAllText("Design/skillsync_codex_handoff/verification/picker-results.txt",result);return result;
    }
    public static string Eyedropper() {
        var picker=UnityEngine.Object.FindFirstObjectByType<SkillSyncObjectPicker>();
        picker.content.GetComponentsInChildren<Button>()[0].onClick.Invoke();return "Picking enabled";
    }
}
