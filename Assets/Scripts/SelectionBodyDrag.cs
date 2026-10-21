using UnityEngine;

// Left-dragging a part's body slides the selection along the floor as one undo entry.
// A press on something already selected waits for the drag threshold, so a plain click can still
// drill from a group into its part (SelectionService.SelectPicked).
public sealed class SelectionBodyDrag
{
    const float ThresholdSqr = 36f;
    const float MaxDistance = 500f;

    readonly SelectionService selection;
    PlacedObject pressed;
    bool selectOnClick;
    bool dragging;
    Vector2 pressPoint;
    Vector3 grabPoint;
    Vector3 startPosition;
    SelectionTransformSession session;

    public SelectionBodyDrag(SelectionService selection) { this.selection = selection; }

    public bool Active => pressed != null;

    public void Begin(PlacedObject picked, Vector3 hitPoint, Vector2 pointer, bool selectOnClick)
    {
        Cancel();
        pressed = picked;
        grabPoint = hitPoint;
        pressPoint = pointer;
        this.selectOnClick = selectOnClick;
    }

    public void Cancel()
    {
        if (dragging) session?.Cancel();
        pressed = null;
        session = null;
        dragging = false;
    }

    // Returns true while the gesture owns the pointer.
    public bool Update(Camera cam, Vector2 pointer)
    {
        if (pressed == null) return false;
        if (cam == null || EditInput.CancelPressedThisFrame() || EditInput.MiddlePressed() || EditInput.RightPressed())
        {
            Cancel();
            return false;
        }

        if (!dragging && (pointer - pressPoint).sqrMagnitude >= ThresholdSqr)
        {
            if (selection.Current == null) { Cancel(); return false; }
            session = new SelectionTransformSession(selection);
            startPosition = selection.Current.transform.position;
            dragging = true;
        }

        if (dragging && !PlacementController.IsScreenPositionOverBlockingUi(pointer)) Move(cam, pointer);

        if (EditInput.LeftPressed()) return true;
        Finish();
        return false;
    }

    void Move(Camera cam, Vector2 pointer)
    {
        var current = selection.Current;
        if (current == null) return;
        var plane = new Plane(Vector3.up, grabPoint);
        var ray = cam.ScreenPointToRay(pointer);
        if (!plane.Raycast(ray, out float enter) || enter < 0f) return;

        Vector3 delta = ray.GetPoint(enter) - grabPoint;
        delta.y = 0f;
        if (delta.sqrMagnitude > MaxDistance * MaxDistance) delta = delta.normalized * MaxDistance;
        if (EditSnapSettings.ShouldSnap)
        {
            float grid = Mathf.Max(0.0001f, EditSnapSettings.GridSize);
            delta.x = Mathf.Round(delta.x / grid) * grid;
            delta.z = Mathf.Round(delta.z / grid) * grid;
        }
        current.transform.position = startPosition + delta;
        session?.Apply();
    }

    void Finish()
    {
        var target = pressed;
        bool wasDragging = dragging;
        var finishedSession = session;
        pressed = null;
        session = null;
        dragging = false;

        if (!wasDragging)
        {
            if (selectOnClick && target != null) selection.SelectPicked(target, false);
            return;
        }

        var current = selection.Current;
        if (current != null && EditSnapSettings.ShouldSnap && selection.Selected.Count == 1)
        {
            float range = Mathf.Max(EditSnapSettings.GridSize, 0.1f);
            if (PlacementObjectSnapper.TrySnap(current, range, Vector3.right, out var snapped)) current.transform.position = snapped;
            if (PlacementObjectSnapper.TrySnap(current, range, Vector3.forward, out snapped)) current.transform.position = snapped;
            finishedSession?.Apply();
        }
        finishedSession?.Commit("Move selection");
    }
}
