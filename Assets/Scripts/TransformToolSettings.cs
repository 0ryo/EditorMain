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

public static class TransformToolSettings
{
    // Align manipulation axes with the selected object's orientation by default.
    static TransformCoordinateSpace coordinateSpace = TransformCoordinateSpace.Local;
    static TransformPivotMode pivotMode = TransformPivotMode.Center;
    static int revision;

    public static TransformCoordinateSpace CoordinateSpace => coordinateSpace;
    public static TransformPivotMode PivotMode => pivotMode;
    public static int Revision => revision;

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
