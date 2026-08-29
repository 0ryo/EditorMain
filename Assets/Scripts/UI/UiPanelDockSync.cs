using UnityEngine;

public class UiPanelDockSync : MonoBehaviour
{
    public RectTransform catalogPanel;
    public RectTransform scenarioPanel;
    public RectTransform editModePanel;
    public RectTransform settingsButtonPanel;
    public RectTransform hintButtonPanel;
    public float gap;
    public float editModePanelLeftMargin = 12f;
    public float editModePanelTop = -12f;
    public float editModePanelWidth = 236f;
    public float editModePanelHeight = 40f;
    public float settingsButtonRightMargin = 12f;
    public float settingsButtonWidth = DesignTokens.ButtonMinWidth;
    public float globalButtonGap = 8f;

    float detailPanelVisibleWidth;

    public void SetDetailPanelVisibleWidth(float visibleWidth)
    {
        detailPanelVisibleWidth = Mathf.Max(0f, visibleWidth);
    }

    void Awake()
    {
        ApplyDefaultLayoutValues();
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        ApplyDefaultLayoutValues();
    }
#endif

    void LateUpdate()
    {
        ResolveGlobalButtons();
        if (catalogPanel == null || scenarioPanel == null) return;
        ApplyDefaultLayoutValues();

        float left = catalogPanel.offsetMax.x + gap;
        if (!Mathf.Approximately(scenarioPanel.offsetMin.x, left))
        {
            scenarioPanel.offsetMin = new Vector2(left, scenarioPanel.offsetMin.y);
        }

        if (editModePanel == null) return;

        float panelLeft = left + editModePanelLeftMargin;
        float panelRight = panelLeft + editModePanelWidth;
        float panelBottom = editModePanelTop - editModePanelHeight;

        editModePanel.anchorMin = new Vector2(0f, 1f);
        editModePanel.anchorMax = new Vector2(0f, 1f);
        editModePanel.pivot = new Vector2(0f, 1f);
        editModePanel.offsetMin = new Vector2(panelLeft, panelBottom);
        editModePanel.offsetMax = new Vector2(panelRight, editModePanelTop);

        float settingsTop = editModePanelTop;
        float settingsBottom = settingsTop - editModePanelHeight;
        float settingsRight = -settingsButtonRightMargin - detailPanelVisibleWidth;
        float settingsLeft = settingsRight - settingsButtonWidth;

        if (settingsButtonPanel != null)
        {
            settingsButtonPanel.anchorMin = new Vector2(1f, 1f);
            settingsButtonPanel.anchorMax = new Vector2(1f, 1f);
            settingsButtonPanel.pivot = new Vector2(1f, 1f);
            settingsButtonPanel.offsetMin = new Vector2(settingsLeft, settingsBottom);
            settingsButtonPanel.offsetMax = new Vector2(settingsRight, settingsTop);
        }

        if (hintButtonPanel != null)
        {
            float hintRight = settingsLeft - globalButtonGap;
            float hintLeft = hintRight - settingsButtonWidth;
            hintButtonPanel.anchorMin = new Vector2(1f, 1f);
            hintButtonPanel.anchorMax = new Vector2(1f, 1f);
            hintButtonPanel.pivot = new Vector2(1f, 1f);
            hintButtonPanel.offsetMin = new Vector2(hintLeft, settingsBottom);
            hintButtonPanel.offsetMax = new Vector2(hintRight, settingsTop);
        }
    }

    void ResolveGlobalButtons()
    {
        if (settingsButtonPanel == null)
        {
            settingsButtonPanel = transform.Find("Button_Settings") as RectTransform;
            if (settingsButtonPanel == null)
            {
                settingsButtonPanel = transform.Find("Button_Settings_Runtime") as RectTransform;
            }
        }

        if (hintButtonPanel == null)
        {
            hintButtonPanel = transform.Find("Button_Hints") as RectTransform;
        }
    }

    void ApplyDefaultLayoutValues()
    {
        settingsButtonWidth = DesignTokens.ButtonMinWidth;
    }
}
