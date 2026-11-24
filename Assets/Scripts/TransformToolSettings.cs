public enum TransformCoordinateSpace
{
    World,
    Local
}

public enum TransformPivotMode
{
    Pivot,
    Center
}

// Which handles the transform tab shows: arrows move the selection, rings rotate it.
public enum TransformTool
{
    Move,
    Rotate
}

public static class TransformToolSettings
{
    // Models made to be handled at their origin: the instructional socket wrench has its origin at the mouth that
    // goes on the nut, so its handles sit there instead of at the middle of the shape.
    static readonly System.Collections.Generic.HashSet<string> OriginPivotTypeIds = new(System.StringComparer.Ordinal)
        { "Imported/TeacherSocketWrench" };
    // Rendering-only copies (the trial) of such models.
    static readonly System.Collections.Generic.HashSet<UnityEngine.Transform> OriginPivotCopies = new();

    public static bool IsOriginPivotModel(PlacedObject placed) =>
        placed != null && placed.modelRoot == null && OriginPivotTypeIds.Contains(placed.typeId ?? "");

    public static void MarkOriginPivotCopy(UnityEngine.Transform copy, bool on)
    {
        if (copy == null) return;
        if (on) OriginPivotCopies.Add(copy); else OriginPivotCopies.Remove(copy);
    }

    // Handles at the origin: in pivot mode, or for a model made to be handled at its origin.
    public static bool PivotsAtOrigin(UnityEngine.Transform target)
    {
        if (pivotMode == TransformPivotMode.Pivot) return true;
        if (target == null) return false;
        return OriginPivotCopies.Contains(target) || IsOriginPivotModel(target.GetComponent<PlacedObject>());
    }

    // Align manipulation axes with the selected object's orientation by default.
    static TransformCoordinateSpace coordinateSpace = TransformCoordinateSpace.Local;
    static TransformPivotMode pivotMode = TransformPivotMode.Center;
    static TransformTool tool = TransformTool.Move;
    static int revision;

    public static TransformCoordinateSpace CoordinateSpace => coordinateSpace;
    public static TransformPivotMode PivotMode => pivotMode;
    public static TransformTool Tool => tool;
    public static int Revision => revision;

    public static void SetTool(TransformTool value)
    {
        if (tool == value) return;
        tool = value;
        revision++;
    }

    public static void SetCoordinateSpace(TransformCoordinateSpace value)
    {
        if (coordinateSpace == value) return;
        coordinateSpace = value;
        revision++;
    }

    public static void SetPivotMode(TransformPivotMode value)
    {
        if (pivotMode == value) return;
        pivotMode = value;
        revision++;
    }
}
