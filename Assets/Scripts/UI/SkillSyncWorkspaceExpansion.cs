using System;
using UnityEngine;

// Presentation only. Selection and editing state remain in the controller.
public sealed class SkillSyncWorkspaceExpansion : MonoBehaviour
{
    [Serializable] public struct Item {
        public RectTransform rect;
        public Vector2 position, size;
        public float move, grow;
    }
    public CanvasGroup inspector;
    public Item[] items;
    public RectTransform objectsContent;
    public float Amount { get; private set; }
    float target;
    public void SetExpanded(bool expanded) { target=expanded?1:0; }
    public void Tick(float deltaTime)
    {
        Amount=Mathf.MoveTowards(Amount,target,deltaTime/.24f);
        float width=Mathf.SmoothStep(0,1,Amount)*440;
        foreach(var item in items) {
            if(item.rect==objectsContent) {
                // ScrollRect owns Y; ContentSizeFitter owns height. Animation only owns width.
                item.rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,item.size.x+width*item.grow);
                continue;
            }
            item.rect.anchoredPosition=item.position+Vector2.right*(width*item.move);
            item.rect.sizeDelta=item.size+Vector2.right*(width*item.grow);
        }
        inspector.alpha=1-Mathf.SmoothStep(0,1,Amount);
        inspector.interactable=inspector.blocksRaycasts=target==0;
        ((RectTransform)inspector.transform).anchoredPosition=Vector2.right*width;
        foreach(RectTransform row in objectsContent) {
            if(row.GetComponent<SkillSyncDesignRow>()==null) continue;
            row.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,objectsContent.rect.width);
        }
        var split=transform.Find("WorkspaceSplit")?.GetComponent<SkillSyncVerticalSplit>();
        if(split!=null)split.Apply();
    }
}
