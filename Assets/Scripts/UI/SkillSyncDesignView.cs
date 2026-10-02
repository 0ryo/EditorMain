using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

// Serialized, editable uGUI elements. Source node IDs stay beside every measurement.
public sealed class SkillSyncDesignView : MonoBehaviour
{
    [Serializable] public sealed class Visual
    {
        public GameObject target;
        public TMP_Text label;
        public string role;
        public string[] sourceNodeIds;
        public int mask;
        public int[] orders;
    }
    [Serializable] public sealed class Control
    {
        public UnityEngine.UI.Button button;
        public string action;
        public int mask;
        public string[] sourceNodeIds;
    }
    [Serializable] public sealed class Field
    {
        public TMP_InputField input;
        public string role;
        public int mask;
        public string[] sourceNodeIds;
    }
    public Visual[] visuals;
    public Control[] controls;
    public Field[] fields;
    public RectTransform viewport;
    public GameObject modal;
    public GameObject modalBlocker;
    public RectTransform libraryContent, stepsContent, objectsContent;
    public SkillSyncDesignRow libraryTemplate, stepTemplate, objectTemplate;
    public UnityEngine.UI.Image holdProgress;
    public TMP_Text viewportDistance;
    public TMP_Text viewportGrid;
    public SkillSyncViewportOverlay conditionOverlay;
    public UnityEngine.UI.RawImage objectAThumbnail, objectBThumbnail;
    public TMP_Text partALabel, partBLabel;
    public bool wideLayout;
    public int pdfRevision;
    public TMP_Text inspectorTitle,inspectorSelection;
    public SkillSyncOrientationGizmo orientation;
    public int State { get; private set; }

    // Shared by the prefab updater and compatibility path for existing UI assets.
    public void EnsureProjectLoadControl()
    {
        if (controls == null) return;
        var help = controls.FirstOrDefault(c => c.action == "ヘルプ");
        var export = controls.FirstOrDefault(c => c.action == "↑ 教材を書き出す");
        if (help?.button == null || export?.button == null) return;
        var existing = controls.FirstOrDefault(c => c.action == "教材を読み込む");
        if (existing?.button != null)
        {
            ApplyProjectLoadStyle(existing.button, help.button);
            return;
        }
        var helpRect = (RectTransform)help.button.transform;
        var exportRect = (RectTransform)export.button.transform;
        const float width = 152f, gap = 12f;
        var oldPosition = helpRect.anchoredPosition;
        float loadX = exportRect.anchoredPosition.x - width - gap;
        var helpPosition = new Vector2(loadX - helpRect.sizeDelta.x - gap, oldPosition.y);
        // PDF button backgrounds are separate visuals under Chrome.
        foreach (var visual in visuals)
        {
            if (visual.label != null || visual.target == null) continue;
            var rect = visual.target.transform as RectTransform;
            if (rect != null && Vector2.Distance(rect.anchoredPosition, oldPosition) < .1f &&
                Vector2.Distance(rect.sizeDelta, helpRect.sizeDelta) < .1f)
                rect.anchoredPosition += helpPosition - oldPosition;
        }
        helpRect.anchoredPosition = helpPosition;
        var go = new GameObject("Button_LoadLesson", typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.Button));
        var loadRect = (RectTransform)go.transform;
        loadRect.SetParent(helpRect.parent, false);
        loadRect.anchorMin = helpRect.anchorMin; loadRect.anchorMax = helpRect.anchorMax; loadRect.pivot = helpRect.pivot;
        loadRect.anchoredPosition = new Vector2(loadX, oldPosition.y);
        loadRect.sizeDelta = new Vector2(width, helpRect.sizeDelta.y);
        var surface = go.GetComponent<UnityEngine.UI.Image>();
        var button = go.GetComponent<UnityEngine.UI.Button>();
        button.targetGraphic = surface;
        var label = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
        label.transform.SetParent(loadRect, false);
        label.text = "教材を読み込む";
        label.rectTransform.anchorMin = Vector2.zero; label.rectTransform.anchorMax = Vector2.one;
        label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;
        ApplyProjectLoadStyle(button, help.button);
        controls = controls.Concat(new[] { new Control { button = button, action = "教材を読み込む", mask = help.mask } }).ToArray();
    }

    // Kept in the Prefab update path and as a compatibility path for older UIRoot assets.
    public void EnsureViewportLabels()
    {
        foreach (var label in new[] { partALabel, partBLabel })
        {
            if (label == null) continue;
            var background = label.transform.parent.GetComponent<SkillSyncRoundedGraphic>();
            if (background != null) {
                background.color = new Color32(60, 65, 72, 175);
                background.borderColor = Color.clear;
                background.borderWidth = 0;
                background.SetVerticesDirty();
            }
            label.color = Color.white;
        }
    }

    public void EnsureConditionControls()
    {
        if (visuals == null || controls == null) return;
        var heading = visuals.FirstOrDefault(v => v.role == "conditionHeading" && v.label != null);
        if (heading == null || heading.target == null) return;

        heading.label.raycastTarget = false;
        heading.label.rectTransform.sizeDelta = new Vector2(124f, heading.label.rectTransform.sizeDelta.y);
        var parent = heading.target.transform.parent;
        int mask = heading.mask;
        float X(float sourceX) => wideLayout ? SkillSyncDesignLayout.MapX(sourceX) : sourceX;

        var counter = visuals.FirstOrDefault(v => v.role == "conditionCounter");
        if (counter == null || counter.label == null)
        {
            var counterRect = new GameObject("ConditionCounter", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<RectTransform>();
            counterRect.SetParent(parent, false);
            counterRect.anchorMin = counterRect.anchorMax = counterRect.pivot = new Vector2(0, 1);
            counterRect.anchoredPosition = new Vector2(X(1364), -354);
            counterRect.sizeDelta = new Vector2(32, 24);
            var counterLabel = counterRect.GetComponent<TextMeshProUGUI>();
            counterLabel.font = heading.label.font;
            counterLabel.fontSharedMaterial = heading.label.fontSharedMaterial;
            counterLabel.fontSize = 14;
            counterLabel.fontWeight = FontWeight.Medium;
            counterLabel.color = new Color32(88, 103, 124, 255);
            counterLabel.alignment = TextAlignmentOptions.Midline;
            counterLabel.richText = false;
            counterLabel.textWrappingMode = TextWrappingModes.NoWrap;
            counterLabel.raycastTarget = false;
            if (counter == null)
            {
                counter = new Visual { role = "conditionCounter", sourceNodeIds = heading.sourceNodeIds };
                visuals = visuals.Concat(new[] { counter }).ToArray();
            }
            counter.target = counterRect.gameObject;
            counter.label = counterLabel;
        }
        var counterTransform = (RectTransform)counter.target.transform;
        counterTransform.anchorMin = counterTransform.anchorMax = counterTransform.pivot = new Vector2(0, 1);
        counterTransform.anchoredPosition = new Vector2(X(1364), -354);
        counterTransform.sizeDelta = new Vector2(32, 24);
        counter.mask = mask;
        counter.orders = heading.orders;

        EnsureConditionButton("PreviousCondition", "<", 1314, 32, new Color32(255, 255, 255, 255), new Color32(205, 213, 224, 255), new Color32(23, 34, 54, 255), 17, parent, heading, mask, X);
        EnsureConditionButton("NextCondition", ">", 1400, DesignTokens.MinTouchTarget, new Color32(255, 255, 255, 255), new Color32(205, 213, 224, 255), new Color32(23, 34, 54, 255), 17, parent, heading, mask, X);
        EnsureConditionButton("DeleteCondition", "この条件を削除", 1452, 124, new Color32(168, 44, 38, 255), new Color32(168, 44, 38, 255), Color.white, 12, parent, heading, mask, X);
    }

    void EnsureConditionButton(string action, string caption, float sourceX, float width, Color surfaceColor, Color borderColor, Color textColor, float fontSize,
        Transform parent, Visual heading, int mask, Func<float, float> mapX)
    {
        var control = controls.FirstOrDefault(c => c.action == action);
        UnityEngine.UI.Button button = control?.button;
        if (button == null)
        {
            var go = new GameObject("Button_" + action, typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.Button), typeof(EditorUiInputBlocker));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            button = go.GetComponent<UnityEngine.UI.Button>();
            control = new Control { button = button, action = action };
            controls = controls.Concat(new[] { control }).ToArray();
        }

        control.action = action;
        control.mask = mask;
        var buttonRect = (RectTransform)button.transform;
        buttonRect.anchorMin = buttonRect.anchorMax = buttonRect.pivot = new Vector2(0, 1);
        buttonRect.anchoredPosition = new Vector2(mapX(sourceX), -337);
        buttonRect.sizeDelta = new Vector2(Mathf.Max(width, DesignTokens.MinTouchTarget), DesignTokens.MinTouchTarget);
        var hitArea = button.GetComponent<UnityEngine.UI.Image>();
        if (hitArea == null) hitArea = button.gameObject.AddComponent<UnityEngine.UI.Image>();
        hitArea.color = Color.clear;
        hitArea.raycastTarget = true;

        var surfaceTransform = button.transform.Find("ConditionSurface");
        if (surfaceTransform == null)
        {
            var surfaceObject = new GameObject("ConditionSurface", typeof(RectTransform), typeof(SkillSyncRoundedGraphic));
            surfaceTransform = surfaceObject.transform;
            surfaceTransform.SetParent(button.transform, false);
        }
        var surfaceRect = (RectTransform)surfaceTransform;
        surfaceRect.anchorMin = Vector2.zero; surfaceRect.anchorMax = Vector2.one;
        surfaceRect.offsetMin = surfaceRect.offsetMax = Vector2.zero;
        surfaceTransform.SetAsFirstSibling();
        var rounded = surfaceTransform.GetComponent<SkillSyncRoundedGraphic>();
        rounded.color = surfaceColor; rounded.radius = 8; rounded.borderWidth = 1; rounded.borderColor = borderColor; rounded.raycastTarget = false;
        button.targetGraphic = rounded;
        button.transition = UnityEngine.UI.Selectable.Transition.ColorTint;
        var colors = UnityEngine.UI.ColorBlock.defaultColorBlock;
        colors.normalColor = Color.white; colors.highlightedColor = Color.white;
        colors.pressedColor = new Color(.85f, .85f, .85f, 1f); colors.selectedColor = Color.white;
        button.colors = colors;
        if (button.GetComponent<EditorUiInputBlocker>() == null) button.gameObject.AddComponent<EditorUiInputBlocker>();

        var label = button.GetComponentInChildren<TMP_Text>(true);
        if (label == null)
        {
            var labelObject = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            labelObject.transform.SetParent(button.transform, false);
            label = labelObject.GetComponent<TextMeshProUGUI>();
        }
        label.font = heading.label.font;
        label.fontSharedMaterial = heading.label.fontSharedMaterial;
        label.text = caption;
        label.fontSize = fontSize;
        label.fontWeight = action == "DeleteCondition" ? FontWeight.Medium : FontWeight.Bold;
        label.color = textColor;
        label.alignment = TextAlignmentOptions.Midline;
        label.richText = false;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.raycastTarget = false;
        var labelRect = label.rectTransform;
        labelRect.anchorMin = Vector2.zero; labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = labelRect.offsetMax = Vector2.zero;
        UiAccessibilityMetrics.EnsureButtonTarget(button);
    }

    static void ApplyProjectLoadStyle(UnityEngine.UI.Button button, UnityEngine.UI.Button reference)
    {
        // The adjacent Help island is a white, 10px-radius, 1px-outline shape
        // (vector_9542d9fde8e97a73.png at 82x40). Use the same geometry without
        // stretching its raster corners when the Japanese caption needs more width.
        var background = button.transform.Find("Surface");
        if (background == null)
        {
            background = new GameObject("Surface", typeof(RectTransform), typeof(SkillSyncRoundedGraphic)).transform;
            background.SetParent(button.transform, false);
        }
        var backgroundRect = (RectTransform)background;
        backgroundRect.anchorMin = Vector2.zero; backgroundRect.anchorMax = Vector2.one;
        backgroundRect.offsetMin = backgroundRect.offsetMax = Vector2.zero;
        background.SetAsFirstSibling();
        var rounded = background.GetComponent<SkillSyncRoundedGraphic>();
        rounded.color = Color.white;
        rounded.radius = 10f; rounded.borderWidth = 1f;
        rounded.borderColor = new Color32(205, 213, 224, 255);
        rounded.raycastTarget = false;
        var hitArea = button.GetComponent<UnityEngine.UI.Image>();
        hitArea.color = reference.targetGraphic.color;
        button.targetGraphic = hitArea;
        button.transition = reference.transition;
        button.colors = reference.colors;
        button.spriteState = reference.spriteState;
        button.animationTriggers = reference.animationTriggers;
        if (button.GetComponent<EditorUiInputBlocker>() == null) button.gameObject.AddComponent<EditorUiInputBlocker>();
        UiAccessibilityMetrics.EnsureButtonTarget(button);

        var label = button.GetComponentInChildren<TMP_Text>(true);
        var source = reference.GetComponentInChildren<TMP_Text>(true);
        if (label == null || source == null) return;
        label.font = source.font;
        label.fontSharedMaterial = source.fontSharedMaterial;
        label.fontSize = source.fontSize;
        label.fontStyle = source.fontStyle;
        label.fontWeight = source.fontWeight;
        label.color = source.color;
        label.alignment = source.alignment;
        label.margin = source.margin;
        label.characterSpacing = source.characterSpacing;
        label.wordSpacing = source.wordSpacing;
        label.enableAutoSizing = source.enableAutoSizing;
        label.fontSizeMin = source.fontSizeMin; label.fontSizeMax = source.fontSizeMax;
        label.textWrappingMode = source.textWrappingMode;
        label.raycastTarget = false;
        label.rectTransform.anchorMin = source.rectTransform.anchorMin;
        label.rectTransform.anchorMax = source.rectTransform.anchorMax;
        label.rectTransform.pivot = source.rectTransform.pivot;
        label.rectTransform.offsetMin = source.rectTransform.offsetMin;
        label.rectTransform.offsetMax = source.rectTransform.offsetMax;
    }

    public void Show(int state)
    {
        State = state;
        int bit = 1 << state;
        foreach (var v in visuals) v.target.SetActive((v.mask & bit) != 0);
        foreach (var v in visuals.Where(v=>(v.mask & bit)!=0).OrderBy(v=>v.orders!=null && v.orders.Length>state?v.orders[state]:0))
            v.target.transform.SetAsLastSibling();
        foreach (var c in controls)
        {
            c.button.gameObject.SetActive((c.mask & bit) != 0);
            c.button.interactable=state!=5 || c.action=="編集を続ける" || c.action=="該当箇所を修正";
            c.button.transform.SetAsLastSibling();
        }
        foreach (var f in fields)
        {
            f.input.gameObject.SetActive((f.mask & bit) != 0);
            f.input.interactable=state!=5 && state!=2 && state!=4;
            f.input.transform.SetAsLastSibling();
            if(f.input.placeholder!=null) f.input.placeholder.gameObject.SetActive((f.mask & bit)!=0 && string.IsNullOrEmpty(f.input.text));
        }
        modal.SetActive(state == 5);
        modalBlocker.SetActive(state == 5);
        libraryContent.parent.parent.gameObject.SetActive(state == 0 || state == 3);
        stepsContent.parent.parent.gameObject.SetActive(state != 0 && state != 3);
        objectsContent.parent.parent.gameObject.SetActive(state != 2 && state != 4);
        holdProgress.gameObject.SetActive(state == 2 || state == 4);
        viewportDistance.transform.parent.gameObject.SetActive(state == 1 || state == 2 || state == 4 || state == 5);
        holdProgress.transform.SetAsLastSibling();
        libraryContent.parent.parent.SetAsLastSibling();
        stepsContent.parent.parent.SetAsLastSibling();
        objectsContent.parent.parent.SetAsLastSibling();
        if(objectAThumbnail!=null) {objectAThumbnail.gameObject.SetActive(state==1||state==5);objectAThumbnail.transform.SetAsLastSibling();}
        if(objectBThumbnail!=null) {objectBThumbnail.gameObject.SetActive(state==1);objectBThumbnail.transform.SetAsLastSibling();}
        UpdateSelectionHeader();
    }
    public void UpdateSelectionHeader()
    {
        if(pdfRevision==0 || State!=0 && State!=3) return;
        var title=visuals.FirstOrDefault(v=>v.role=="selectionTitle" && (v.mask & (1<<State))!=0)?.label;
        var status=visuals.FirstOrDefault(v=>v.role=="inspectorLabel" && (v.mask & (1<<State))!=0)?.label;
        if(title==null || status==null) return;
        float width=Mathf.Min(320,title.GetPreferredValues(title.text).x);
        title.rectTransform.sizeDelta=new Vector2(width,40);title.overflowMode=TextOverflowModes.Ellipsis;
        var position=status.rectTransform.anchoredPosition;
        status.rectTransform.anchoredPosition=new Vector2(title.rectTransform.anchoredPosition.x+width+7,position.y);
    }
    public void Text(string role, string value)
    {
        foreach (var v in visuals) if (v.role == role && v.label != null) v.label.text = value ?? "";
    }
    public void Value(string role, string value)
    {
        foreach (var f in fields) if (f.role == role && !f.input.isFocused) f.input.SetTextWithoutNotify(value ?? "");
    }
    public void Enable(string action, bool enabled)
    {
        foreach (var c in controls) if (c.action == action) c.button.interactable = enabled && State!=5;
    }
    public void SetVisible(string action, bool visible)
    {
        foreach (var c in controls) if (c.action == action) c.button.gameObject.SetActive(visible && (c.mask & (1 << State)) != 0);
    }
    public void Focus(string role)
    {
        foreach (var f in fields) if (f.role == role && f.input.gameObject.activeInHierarchy)
        { f.input.Select(); f.input.ActivateInputField(); break; }
    }
}
