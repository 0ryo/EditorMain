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
    static TransformCoordinateSpace coordinateSpace = TransformCoordinateSpace.World;
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
