using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public static class EditInput
{
    public static Vector2 MousePosition
    {
        get
        {
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null) return Mouse.current.position.ReadValue();
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.mousePosition;
#else
            return Vector2.zero;
#endif
        }
    }

    public static Vector2 MouseDelta
    {
        get
        {
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null) return Mouse.current.delta.ReadValue();
#endif
            return Vector2.zero;
        }
    }

    public static float ScrollY
    {
        get
        {
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null) return Mouse.current.scroll.ReadValue().y;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.mouseScrollDelta.y;
#else
            return 0f;
#endif
        }
    }

    public static bool LeftPressedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) return true;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetMouseButtonDown(0);
#else
        return false;
#endif
    }

    public static bool MiddlePressed()
    {
#if ENABLE_INPUT_SYSTEM
        if (Mouse.current != null && Mouse.current.middleButton.isPressed) return true;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetMouseButton(2);
#else
        return false;
#endif
    }

    public static bool MiddlePressedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        if (Mouse.current != null && Mouse.current.middleButton.wasPressedThisFrame) return true;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetMouseButtonDown(2);
#else
        return false;
#endif
    }

    public static bool ShiftPressed()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null &&
            (Keyboard.current.leftShiftKey.isPressed || Keyboard.current.rightShiftKey.isPressed))
        {
            return true;
        }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
#else
        return false;
#endif
    }
}
