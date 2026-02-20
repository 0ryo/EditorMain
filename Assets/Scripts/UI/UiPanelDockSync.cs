using UnityEngine;

public class UiPanelDockSync : MonoBehaviour
{
    public RectTransform catalogPanel;
    public RectTransform scenarioPanel;
    public float gap;

    void LateUpdate()
    {
        if (catalogPanel == null || scenarioPanel == null) return;

        float left = catalogPanel.offsetMax.x + gap;
        if (!Mathf.Approximately(scenarioPanel.offsetMin.x, left))
        {
            scenarioPanel.offsetMin = new Vector2(left, scenarioPanel.offsetMin.y);
        }
    }
}
