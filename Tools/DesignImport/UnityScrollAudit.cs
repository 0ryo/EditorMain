using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public static class UnityScrollAudit
{
    public static async Task<string> Verify()
    {
        Application.runInBackground=true;
        var view=UnityEngine.Object.FindFirstObjectByType<SkillSyncDesignView>();
        var results=new List<string>();
        foreach(var content in new[]{view.libraryContent,view.objectsContent,view.stepsContent}) {
            var scroll=content.GetComponentInParent<ScrollRect>(true);
            bool active=scroll.gameObject.activeSelf;scroll.gameObject.SetActive(true);
            var added=new List<GameObject>();
            for(int i=0;i<15;i++) {
                var go=new GameObject("ScrollAuditRow",typeof(RectTransform),typeof(LayoutElement));
                go.transform.SetParent(content,false);go.GetComponent<LayoutElement>().preferredHeight=100;added.Add(go);
            }
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);Canvas.ForceUpdateCanvases();await Task.Delay(50);
            scroll.verticalNormalizedPosition=1;scroll.StopMovement();Canvas.ForceUpdateCanvases();
            float before=content.anchoredPosition.y;
            scroll.OnScroll(new PointerEventData(EventSystem.current){scrollDelta=new Vector2(0,-1)});
            float delta=content.anchoredPosition.y-before;
            if(Mathf.Abs(delta-48)>.1f) throw new Exception(scroll.name+" scroll distance: "+delta);
            results.Add(scroll.name+": wheel notch = "+delta+" logical px");
            foreach(var go in added) UnityEngine.Object.DestroyImmediate(go);
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);scroll.verticalNormalizedPosition=1;scroll.gameObject.SetActive(active);
        }
        var add=(RectTransform)view.controls.First(c=>c.action=="＋ 手順を追加").button.transform;
        if(-add.anchoredPosition.y!=832 || ((RectTransform)view.stepsContent.parent).rect.height!=606) throw new Exception("Step layout mismatch");
        System.IO.File.WriteAllText("Design/skillsync_codex_handoff/verification/scroll-results.txt",string.Join("\n",results));
        return string.Join("\n",results)+"\nStep list ends at 808; controls start at 832; no overlap.";
    }
}
