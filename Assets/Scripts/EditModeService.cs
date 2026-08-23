using System;
using UnityEngine;

public enum EditMode
{
    Browse = 0,
    Place = 1,
    Transform = 2,
    LegacyRotate = 3,
    Scale = 4,

    // Backward compatible aliases used by existing tools.
    None = Browse,
    Move = Transform,
    Rotate = Transform
}

public class EditModeService : MonoBehaviour
{
    public static EditModeService I;

    [SerializeField] KeyCode enterTransformModeKey = KeyCode.Tab;
    public EditMode Mode = EditMode.Browse;

    public event Action<EditMode> ModeChanged;

    void Awake()
    {
        I = this;
        if (enterTransformModeKey == KeyCode.None)
        {
            enterTransformModeKey = KeyCode.Tab;
        }

        if (Mode == EditMode.LegacyRotate)
        {
            Mode = EditMode.Transform;
        }
    }

    void Update()
    {
        if (!Input.GetKeyDown(enterTransformModeKey)) return;
        if (EditWorkspace.IsTypingIntoInputField()) return;

        SetMode(EditMode.Transform);
    }

    public void SetMode(EditMode mode)
    {
        if (Mode == mode) return;
        Mode = mode;
        ModeChanged?.Invoke(mode);
    }

}
