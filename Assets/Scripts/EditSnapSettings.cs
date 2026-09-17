using UnityEngine;

public static class EditSnapSettings
{
    public const float DefaultGridSize = 0.1f;
    public const float DefaultRotationDegrees = 15f;

    public static float GridSize { get; private set; } = DefaultGridSize;
    public static float RotationDegrees { get; private set; } = DefaultRotationDegrees;
    public static bool Enabled { get; private set; } = true;
    public static bool TemporarilyDisabled => Enabled && EditInput.AltPressed();
    public static bool ShouldSnap => Enabled && !TemporarilyDisabled;

    public static void Configure(float gridSize, float rotationDegrees, bool enabled)
    {
        GridSize = Mathf.Clamp(gridSize, 0.01f, 10f);
        RotationDegrees = Mathf.Clamp(rotationDegrees, 1f, 180f);
        Enabled = enabled;
    }
}
