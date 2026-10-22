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
    // Placed-parts rows in the narrow left panel (SkillSyncLeftTabs): one column of name and detail beside a small thumbnail.
    public bool compact;
    // "Objects" rows nest parts below their model: a fixed column for the fold arrow plus one step per level.
    public const float TreeColumn = 30, TreeStep = 24;
    float indent;
    UnityEngine.UI.Button expander;
    TMP_Text expanderLabel;
    string headingValue, detailValue, indexValue;
    bool hasValue, selectedValue, completeValue;
    float numberSize, titleSize;
    public void SetSelected(bool selected) => Set(headingValue, detailValue, indexValue, selected, completeValue);
    public void Set(string heading, string detail, string index, bool selected, bool complete = false)
    {
        if (hasValue && headingValue == heading && detailValue == detail && indexValue == index && selectedValue == selected && completeValue == complete) return;
        hasValue = true; headingValue = heading; detailValue = detail; indexValue = index; selectedValue = selected; completeValue = complete;
        title.text = heading ?? ""; description.text = detail ?? "";
        // A finished step keeps just its number on the green badge: "✓3" read as one cramped mark.
        if (numberSize == 0) numberSize = number.fontSize;
        number.text = complete && string.IsNullOrEmpty(index) ? "✓" : index;
        number.textWrappingMode = TextWrappingModes.NoWrap;
        number.enableAutoSizing = false;
        number.fontSize = numberSize;
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
            float shift = TreeColumn + indent;
            title.rectTransform.anchoredPosition=new Vector2(94+shift,-9);
            title.rectTransform.sizeDelta=new Vector2(588-shift,single?38.4f:24);
            if(compact) LayoutCompactObject(detailValue);
        }
        if(layout=="Steps") LayoutStep();
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
    // Step card grid: the badge in its own column, title (up to two lines) and detail (up to two lines) in the text
    // column beside it, the same padding on every side, and the card as tall as its text.
    const float StepPadding = 14, StepBadge = 32, StepColumnGap = 12, StepTextGap = 6;
    void LayoutStep()
    {
        var row = (RectTransform)transform;
        float width = row.rect.width > 0 ? row.rect.width : ((RectTransform)row.parent).rect.width;
        float left = StepPadding + StepBadge + StepColumnGap, textWidth = Mathf.Max(40, width - left - StepPadding);
        if (titleSize == 0) titleSize = title.fontSize;
        title.enableAutoSizing = false; title.fontSize = titleSize;
        foreach (var text in new[] { title, description })
        {
            text.alignment = TextAlignmentOptions.TopLeft; text.margin = Vector4.zero;
            text.textWrappingMode = TextWrappingModes.Normal; text.overflowMode = TextOverflowModes.Ellipsis; text.maxVisibleLines = 2;
            SkillSyncLineBreak.Attach(text); // wrapping is on only from here, so the phrase-aware breaks attach now
        }
        float TwoLines(TMP_Text text) => text.GetPreferredValues("あ\nあ", textWidth, 0).y;
        float Height(TMP_Text text) => string.IsNullOrEmpty(text.text) ? 0 : Mathf.Min(text.GetPreferredValues(text.text, textWidth, 0).y, TwoLines(text));
        float titleHeight = Mathf.Ceil(Height(title)), detailHeight = Mathf.Ceil(Height(description));
        Place(title.rectTransform, left, StepPadding, StepPadding, titleHeight);
        float detailTop = StepPadding + titleHeight + (detailHeight > 0 ? StepTextGap : 0);
        Place(description.rectTransform, left, StepPadding, detailTop, detailHeight);
        description.gameObject.SetActive(detailHeight > 0);
        if (numberBackground != null)
        {
            var badge = numberBackground.rectTransform;
            badge.anchorMin = badge.anchorMax = badge.pivot = new Vector2(0, 1);
            badge.anchoredPosition = new Vector2(StepPadding, -StepPadding); badge.sizeDelta = new Vector2(StepBadge, StepBadge);
            numberBackground.radius = StepBadge / 2;
            var label = number.rectTransform;
            label.anchorMin = label.anchorMax = label.pivot = new Vector2(0, 1);
            label.anchoredPosition = badge.anchoredPosition; label.sizeDelta = badge.sizeDelta;
            number.alignment = TextAlignmentOptions.Center; number.margin = Vector4.zero;
        }
        float height = Mathf.Max(StepPadding + StepBadge, detailTop + detailHeight) + StepPadding;
        var element = GetComponent<UnityEngine.UI.LayoutElement>();
        if (element != null) element.preferredHeight = height;
        row.sizeDelta = new Vector2(row.sizeDelta.x, height);
    }
    public UnityEngine.UI.Button Expander => expander;
    // Indents the row by tree depth and shows the ▶/▼ fold arrow when the row has parts below it (▸/▾ are not in the Japanese font).
    public void SetTree(int depth, bool hasChildren, bool open)
    {
        treeDepth = depth;
        indent = depth * TreeStep;
        float shift = TreeColumn + indent;
        if(thumbnail!=null) thumbnail.rectTransform.anchoredPosition=new Vector2(9.28f+shift,thumbnail.rectTransform.anchoredPosition.y);
        title.rectTransform.anchoredPosition=new Vector2(94+shift,title.rectTransform.anchoredPosition.y);
        title.rectTransform.sizeDelta=new Vector2(588-shift,title.rectTransform.sizeDelta.y);
        description.rectTransform.anchoredPosition=new Vector2(94+shift,description.rectTransform.anchoredPosition.y);
        if(expander==null && hasChildren)
        {
            var go=new GameObject("Expander",typeof(RectTransform),typeof(UnityEngine.UI.Image),typeof(UnityEngine.UI.Button),typeof(EditorUiInputBlocker));
            var rect=(RectTransform)go.transform;rect.SetParent(transform,false);
            rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(0,1);rect.sizeDelta=new Vector2(TreeColumn-2,40);
            var image=go.GetComponent<UnityEngine.UI.Image>();image.color=Color.clear;
            expander=go.GetComponent<UnityEngine.UI.Button>();expander.targetGraphic=image;expander.transition=UnityEngine.UI.Selectable.Transition.None;
            var labelObject=new GameObject("Label",typeof(RectTransform),typeof(TextMeshProUGUI));
            expanderLabel=labelObject.GetComponent<TextMeshProUGUI>();
            var labelRect=expanderLabel.rectTransform;labelRect.SetParent(rect,false);
            labelRect.anchorMin=Vector2.zero;labelRect.anchorMax=Vector2.one;labelRect.offsetMin=labelRect.offsetMax=Vector2.zero;
            expanderLabel.font=title.font;expanderLabel.fontSize=12;expanderLabel.alignment=TextAlignmentOptions.Center;
            expanderLabel.color=Parse("#58677C");expanderLabel.raycastTarget=false;
        }
        if(expander!=null)
        {
            expander.gameObject.SetActive(hasChildren);
            ((RectTransform)expander.transform).anchoredPosition=new Vector2(4+indent,-9);
            expanderLabel.text=open?"▼":"▶";
        }
        if(compact && hasValue) LayoutCompactObject(detailValue);
    }

    // Compact placed-parts row: 8 px inset, fold arrow column, thumbnail, then the name (one line) over its detail
    // (one line), both clear of the eye / padlock buttons on the right. A cut-off name can be read on hover.
    public const float CompactHeight = 48;
    const float CompactPad = 8, CompactFold = 18, CompactStep = 12, CompactThumbW = 32, CompactThumbH = 24, CompactGap = 8;
    int treeDepth;
    SkillSyncHoverTip nameTip;
    void LayoutCompactObject(string detail)
    {
        float x = CompactPad + Mathf.Min(treeDepth, 5) * CompactStep;
        float textX = x + CompactFold + CompactThumbW + CompactGap, right = SkillSyncRowToggles.CompactReserved;
        var row = (RectTransform)transform;
        row.sizeDelta = new Vector2(row.sizeDelta.x, CompactHeight);
        var element = GetComponent<UnityEngine.UI.LayoutElement>();
        if (element != null) element.preferredHeight = CompactHeight;
        if (expander != null)
        {
            var fold = (RectTransform)expander.transform;
            fold.anchoredPosition = new Vector2(x - 2, 0); fold.sizeDelta = new Vector2(CompactFold + 4, CompactHeight);
            expanderLabel.fontSize = 10;
        }
        if (thumbnail != null)
        {
            var thumb = thumbnail.rectTransform;
            thumb.anchorMin = thumb.anchorMax = thumb.pivot = new Vector2(0, 1);
            thumb.anchoredPosition = new Vector2(x + CompactFold, -(CompactHeight - CompactThumbH) / 2); thumb.sizeDelta = new Vector2(CompactThumbW, CompactThumbH);
        }
        description.gameObject.SetActive(false);
        bool twoLines = !string.IsNullOrEmpty(detail);
        title.fontSize = 14; title.margin = Vector4.zero; title.alignment = TextAlignmentOptions.MidlineLeft;
        Place(title.rectTransform, textX, right, twoLines ? 6 : 0, twoLines ? 20 : CompactHeight);
        if (status != null)
        {
            status.text = detail ?? "";
            status.gameObject.SetActive(twoLines);
            status.fontSize = 11; status.margin = Vector4.zero; status.alignment = TextAlignmentOptions.MidlineLeft;
            status.textWrappingMode = TextWrappingModes.NoWrap; status.overflowMode = TextOverflowModes.Ellipsis;
            status.color = Parse("#58677C");
            Place(status.rectTransform, textX, right, 26, 16);
        }
        float width = Mathf.Max(1, (row.rect.width > 0 ? row.rect.width : ((RectTransform)row.parent).rect.width) - textX - right);
        bool cut = title.GetPreferredValues(title.text).x > width;
        if (nameTip == null && cut) { nameTip = gameObject.AddComponent<SkillSyncHoverTip>(); nameTip.font = regularFont; }
        if (nameTip != null) nameTip.text = cut ? title.text : "";
    }
    // Top-anchored, horizontally stretched placement with pixel insets.
    static void Place(RectTransform rect,float left,float right,float top,float height)
    {
        rect.anchorMin=new Vector2(0,1);rect.anchorMax=new Vector2(1,1);
        rect.offsetMin=new Vector2(left,-top-height);rect.offsetMax=new Vector2(-right,-top);
    }
    static Color Parse(string hex) { ColorUtility.TryParseHtmlString(hex, out var c); return c; }
}
