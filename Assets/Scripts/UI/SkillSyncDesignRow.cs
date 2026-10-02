using TMPro;
using UnityEngine;

// Instances of the prefab row template are the only dynamically generated list content.
public sealed class SkillSyncDesignRow : MonoBehaviour
{
    public UnityEngine.UI.Button button;
    public TMP_Text title, description, number;
    public UnityEngine.UI.RawImage thumbnail;
    public SkillSyncRoundedGraphic numberBackground;
    public string layout;
    public bool pdfLayout;
    public TMP_Text status;
    public TMP_FontAsset regularFont,boldFont;
    string headingValue, detailValue, indexValue;
    bool hasValue, selectedValue, completeValue;
    public void SetSelected(bool selected) => Set(headingValue, detailValue, indexValue, selected, completeValue);
    public void Set(string heading, string detail, string index, bool selected, bool complete = false)
    {
        if (hasValue && headingValue == heading && detailValue == detail && indexValue == index && selectedValue == selected && completeValue == complete) return;
        hasValue = true; headingValue = heading; detailValue = detail; indexValue = index; selectedValue = selected; completeValue = complete;
        title.text = heading ?? ""; description.text = detail ?? "";
        number.text = complete ? "✓" : index;
        var background = GetComponent<SkillSyncRoundedGraphic>();
        background.color = Parse(complete ? "#EAF7F0" : selected ? "#EAF2FF" : "#FFFFFF");
        background.borderColor = Parse(selected ? "#0866E8" : "#CDD5E0");
        background.SetAllDirty();
        title.color = Parse(selected ? "#0866E8" : "#172236");
        if(layout=="Objects") {
            title.font=selected?boldFont:regularFont;
            status.text=selected?"選択中":detail;
            if(!selected) description.text="";
            status.color=Parse(selected?"#0866E8":"#58677C");
        }
        if(layout=="Library") {
            background.borderWidth=selected?2:1;
            GetComponent<UnityEngine.UI.LayoutElement>().preferredHeight=pdfLayout?(selected?160.33f:142.63f):(selected?154:138);
            title.rectTransform.anchoredPosition=new Vector2(16,pdfLayout?(selected?-96.82f:-87.45f):(selected?-93:-79));
            description.rectTransform.anchoredPosition=new Vector2(16,pdfLayout?(selected?-125.98f:-114.52f):(selected?-121:-107));
        }
        if(numberBackground!=null)
        {
            numberBackground.color=Parse(complete?"#15734A":selected?"#0866E8":"#EEF2F7");
            number.color=complete||selected?Color.white:Parse("#172236");
        }
    }
    static Color Parse(string hex) { ColorUtility.TryParseHtmlString(hex, out var c); return c; }
}
