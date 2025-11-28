using UnityEngine;

public class RotateTool : MonoBehaviour {
    public SelectionService sel;
    public int stepDeg = 15;

    void Update() {
        if (EditModeService.I==null || EditModeService.I.Mode != EditMode.Rotate) return;
        if (sel.Current == null) return;

        if (Input.GetKeyDown(KeyCode.Q))  Add(stepDeg);
        if (Input.GetKeyDown(KeyCode.E))  Add(-stepDeg); // Eで逆回転でも可（好みで）
    }
    void Add(int d) {
        var t = sel.Current.transform;
        var e = t.eulerAngles;
        e.y = Mathf.Round((e.y + d) / stepDeg) * stepDeg;
        t.eulerAngles = e;
    }
}