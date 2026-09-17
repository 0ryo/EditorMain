using UnityEngine;

public class RotateTool : MonoBehaviour {
    public SelectionService sel;
    public int stepDeg = 15;

    void Update() {
        if (ObjectScreenPicker.Capturing) return;
        if (EditModeService.I==null || EditModeService.I.Mode != EditMode.Transform) return;
        if (sel.Current == null) return;
        if (EditWorkspace.IsTypingIntoInputField()) return;

        if (Input.GetKeyDown(KeyCode.Q))  Add(stepDeg);
        if (Input.GetKeyDown(KeyCode.E))  Add(-stepDeg); // Eで逆回転でも可（好みで）
    }
    void Add(int d) {
        var t = sel.Current.transform;
        float fromY = t.eulerAngles.y;
        float toY = Mathf.Round((fromY + d) / stepDeg) * stepDeg;

        var gesture = new SelectionTransformSession(sel);
        var angles = t.eulerAngles;
        angles.y = toY;
        t.eulerAngles = angles;
        gesture.Commit("Rotate selection");
    }
}
