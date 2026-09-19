using System;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;

public static class UnitySplitAudit
{
    public static async Task<string> Verify() {
        Application.runInBackground=true;
        var view=UnityEngine.Object.FindFirstObjectByType<SkillSyncDesignView>();
        var split=view.transform.Find("WorkspaceSplit").GetComponent<SkillSyncVerticalSplit>();
        var list=(RectTransform)view.objectsContent.parent.parent;
        split.SetOffset(0);
        await Task.Delay(100);
        if(Mathf.Abs(-list.anchoredPosition.y+list.rect.height-1000)>.1f)throw new Exception("Bottom gap remains");
        var eventData=new PointerEventData(EventSystem.current){position=((RectTransform)split.transform).position};
        split.OnBeginDrag(eventData);eventData.position+=Vector2.up*144;split.OnDrag(eventData);await Task.Delay(100);
        if(Mathf.Abs(split.Offset+100)>.1f || Mathf.Abs(view.viewport.rect.height-495)>.1f || Mathf.Abs(list.rect.height-282)>.1f)throw new Exception("Drag did not resize both panes");
        if(Mathf.Abs(-list.anchoredPosition.y+list.rect.height-1000)>.1f)throw new Exception("Bottom moved while dragging");
        var catalog=UnityEngine.Object.FindFirstObjectByType<CatalogUI>();
        var entry=catalog.GetDesignLibraryEntries().First(e=>e.typeId=="Verification/Part");
        catalog.TryGetTypeInfo(entry.typeId,out var title,out var detail);
        var row=view.libraryContent.GetComponentsInChildren<SkillSyncLibraryRemove>().First(r=>r.GetComponent<SkillSyncDesignRow>().title.text==title);
        row.OnPointerEnter(eventData);if(!row.remove.gameObject.activeSelf)throw new Exception("Hover cross missing");
        row.OnPointerExit(eventData);if(row.remove.gameObject.activeSelf)throw new Exception("Cross remains after exit");
        int count=UnityEngine.Object.FindObjectsByType<PlacedObject>(FindObjectsSortMode.None).Length;
        row.OnPointerEnter(eventData);row.remove.onClick.Invoke();await Task.Delay(100);
        if(catalog.GetDesignLibraryEntries().Any(e=>e.typeId==entry.typeId))throw new Exception("Card not removed");
        if(UnityEngine.Object.FindObjectsByType<PlacedObject>(FindObjectsSortMode.None).Length!=count)throw new Exception("Placed objects were deleted");
        string result="PASS: list reaches bottom; pointer drag adjusts viewport/list with stable bottom; hover enter/exit; catalog removal preserves placed objects.";
        System.IO.File.WriteAllText("Design/skillsync_codex_handoff/verification/split-results.txt",result);return result;
    }
}
