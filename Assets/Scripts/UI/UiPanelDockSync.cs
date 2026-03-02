using UnityEngine;

public class UiPanelDockSync : MonoBehaviour
{
    public RectTransform catalogPanel;
    public RectTransform scenarioPanel;
    public RectTransform editModePanel;
    public RectTransform settingsButtonPanel;
    public float gap;
    public float editModePanelLeftMargin = 12f;
    public float editModePanelTop = -12f;
    public float editModePanelWidth = 236f;
    public float editModePanelHeight = 40f;
    public float settingsButtonRightMargin = 12f;
    public float settingsButtonWidth = 70f;

    void LateUpdate()
    {
        if (catalogPanel == null || scenarioPanel == null) return;

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

        if (settingsButtonPanel == null) return;

        float settingsTop = editModePanelTop;
        float settingsBottom = settingsTop - editModePanelHeight;
        float settingsRight = -settingsButtonRightMargin;
        float settingsLeft = settingsRight - settingsButtonWidth;

        settingsButtonPanel.anchorMin = new Vector2(1f, 1f);
        settingsButtonPanel.anchorMax = new Vector2(1f, 1f);
        settingsButtonPanel.pivot = new Vector2(1f, 1f);
        settingsButtonPanel.offsetMin = new Vector2(settingsLeft, settingsBottom);
        settingsButtonPanel.offsetMax = new Vector2(settingsRight, settingsTop);
    }
}
