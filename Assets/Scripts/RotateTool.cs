using UnityEngine;

public class RotateTool : MonoBehaviour {
    public SelectionService sel;
    public int stepDeg = 15;

    void Update() {
        if (EditModeService.I==null || EditModeService.I.Mode != EditMode.Transform) return;
        if (sel.Current == null) return;

        if (Input.GetKeyDown(KeyCode.Q))  Add(stepDeg);
        if (Input.GetKeyDown(KeyCode.E))  Add(-stepDeg); // Eで逆回転でも可（好みで）
    }
    void Add(int d) {
        var t = sel.Current.transform;
        float fromY = t.eulerAngles.y;
        float toY = Mathf.Round((fromY + d) / stepDeg) * stepDeg;

        var cmd = new RotateObjectCommand(sel.Current.gameObject, fromY, toY);
        CommandService.I.Stack.Execute(cmd);
    }
}
