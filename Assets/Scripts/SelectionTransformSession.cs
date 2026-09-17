using System.Collections.Generic;
using UnityEngine;

// A gesture snapshots the selection once; every member is restored by the same undo entry.
public sealed class SelectionTransformSession
{
    readonly List<PlacedObject> objects = new();
    readonly List<TransformObjectCommand.State> before = new();
    readonly List<Vector3> positions = new();
    readonly List<Quaternion> rotations = new();
    readonly PlacedObject primary;
    readonly Vector3 origin;
    readonly Quaternion orientation;
    readonly Vector3 scale;

    public SelectionTransformSession(SelectionService selection)
    {
        primary = selection.Current;
        origin = primary.transform.position;
        orientation = primary.transform.rotation;
        scale = primary.transform.localScale;
        foreach (var item in selection.Selected)
        {
            if (!SelectionService.CanEdit(item)) continue;
            objects.Add(item);
            before.Add(TransformObjectCommand.State.Capture(item.transform));
            positions.Add(item.transform.position);
            rotations.Add(item.transform.rotation);
        }
    }

    public void Apply()
    {
        if (primary == null) return;
        Quaternion rotation = primary.transform.rotation * Quaternion.Inverse(orientation);
        Vector3 ratio = new Vector3(Divide(primary.transform.localScale.x, scale.x),
            Divide(primary.transform.localScale.y, scale.y), Divide(primary.transform.localScale.z, scale.z));
        for (int i = 0; i < objects.Count; i++)
        {
            var item = objects[i];
            if (item == null || item == primary) continue;
            Vector3 offset = Quaternion.Inverse(orientation) * (positions[i] - origin);
            item.transform.position = primary.transform.position + rotation * (orientation * Vector3.Scale(offset, ratio));
            item.transform.rotation = rotation * rotations[i];
            item.transform.localScale = Vector3.Scale(before[i].localScale, ratio);
        }
    }

    public void Commit(string label)
    {
        Apply();
        var commands = new List<IEditorCommand>();
        for (int i = 0; i < objects.Count; i++)
        {
            if (objects[i] == null) continue;
            var after = TransformObjectCommand.State.Capture(objects[i].transform);
            if ((after.localPosition - before[i].localPosition).sqrMagnitude < 0.00000001f &&
                Quaternion.Angle(after.localRotation, before[i].localRotation) < 0.0001f &&
                (after.localScale - before[i].localScale).sqrMagnitude < 0.00000001f) continue;
            commands.Add(new TransformObjectCommand(objects[i].gameObject, before[i], after, label));
        }
        if (commands.Count > 0 && CommandService.I != null)
            CommandService.I.Stack.RecordApplied(new CompositeEditorCommand(label, commands));
    }

    public void Cancel()
    {
        for (int i = 0; i < objects.Count; i++)
            if (objects[i] != null)
                new TransformObjectCommand(objects[i].gameObject, before[i], before[i], "Cancel transform").Undo();
    }

    static float Divide(float value, float divisor) => Mathf.Abs(divisor) < 0.000001f ? 1f : value / divisor;
}
