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
    float numberSize;
    public void SetSelected(bool selected) => Set(headingValue, detailValue, indexValue, selected, completeValue);
    public void Set(string heading, string detail, string index, bool selected, bool complete = false)
    {
        if (hasValue && headingValue == heading && detailValue == detail && indexValue == index && selectedValue == selected && completeValue == complete) return;
        hasValue = true; headingValue = heading; detailValue = detail; indexValue = index; selectedValue = selected; completeValue = complete;
        title.text = heading ?? ""; description.text = detail ?? "";
        // A finished step keeps its number ("✓3"), shrunk to fit the badge.
        if (numberSize == 0) numberSize = number.fontSize;
        number.text = complete && !string.IsNullOrEmpty(index) ? "✓" + index : complete ? "✓" : index;
        number.textWrappingMode = TextWrappingModes.NoWrap;
        number.enableAutoSizing = complete;
        number.fontSizeMin = 9; number.fontSizeMax = numberSize;
        if (!complete) number.fontSize = numberSize;
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
            title.overflowMode=TextOverflowModes.Ellipsis;title.textWrappingMode=TextWrappingModes.NoWrap;
            bool single=string.IsNullOrEmpty(description.text);
            title.alignment=single?TextAlignmentOptions.MidlineLeft:TextAlignmentOptions.TopLeft;
            title.rectTransform.anchoredPosition=new Vector2(94,-9);
            title.rectTransform.sizeDelta=new Vector2(588,single?38.4f:24);
        }
        if(layout=="Steps") {
            Place(title.rectTransform,60,16,16,24);
            description.textWrappingMode=TextWrappingModes.Normal;description.overflowMode=TextOverflowModes.Ellipsis;description.maxVisibleLines=2;
            Place(description.rectTransform,16,16,46,36);
        }
        if(layout=="Library") {
            background.borderWidth=selected?2:1;
            GetComponent<UnityEngine.UI.LayoutElement>().preferredHeight=pdfLayout?(selected?160.33f:142.63f):(selected?154:138);
            title.alignment=TextAlignmentOptions.Top;description.alignment=TextAlignmentOptions.Top;
            Place(title.rectTransform,8,8,pdfLayout?(selected?96.82f:87.45f):(selected?93:79),30);
            Place(description.rectTransform,8,8,pdfLayout?(selected?125.98f:114.52f):(selected?121:107),24);
        }
        if(numberBackground!=null)
        {
            numberBackground.color=Parse(complete?"#15734A":selected?"#0866E8":"#EEF2F7");
            number.color=complete||selected?Color.white:Parse("#172236");
        }
    }
    // Top-anchored, horizontally stretched placement with pixel insets.
    static void Place(RectTransform rect,float left,float right,float top,float height)
    {
        rect.anchorMin=new Vector2(0,1);rect.anchorMax=new Vector2(1,1);
        rect.offsetMin=new Vector2(left,-top-height);rect.offsetMax=new Vector2(-right,-top);
    }
    static Color Parse(string hex) { ColorUtility.TryParseHtmlString(hex, out var c); return c; }
}
