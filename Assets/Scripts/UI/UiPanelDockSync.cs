using UnityEngine;

public class UiPanelDockSync : MonoBehaviour
{
    public RectTransform catalogPanel;
    public RectTransform scenarioPanel;
    public RectTransform editModePanel;
    public float gap;
    public float editModePanelLeftMargin = 12f;
    public float editModePanelTop = -12f;
    public float editModePanelWidth = 236f;
    public float editModePanelHeight = 40f;

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
    }
}
