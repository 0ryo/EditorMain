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
#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.mousePosition;
#elif ENABLE_INPUT_SYSTEM
            if (Mouse.current != null) return Mouse.current.position.ReadValue();
            return Vector2.zero;
#else
            return Vector2.zero;
#endif
        }
    }

    public static Vector2 MouseDelta
    {
        get
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            return new Vector2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y"));
#elif ENABLE_INPUT_SYSTEM
            if (Mouse.current != null) return Mouse.current.delta.ReadValue();
            return Vector2.zero;
#else
            return Vector2.zero;
#endif
        }
    }

    public static float ScrollY
    {
        get
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.mouseScrollDelta.y;
#elif ENABLE_INPUT_SYSTEM
            if (Mouse.current != null) return Mouse.current.scroll.ReadValue().y;
            return 0f;
#else
            return 0f;
#endif
        }
    }

    public static bool LeftPressedThisFrame()
    {
#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetMouseButtonDown(0);
#elif ENABLE_INPUT_SYSTEM
        return Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
#else
        return false;
#endif
    }

    public static bool LeftPressed()
    {
#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetMouseButton(0);
#elif ENABLE_INPUT_SYSTEM
        return Mouse.current != null && Mouse.current.leftButton.isPressed;
#else
        return false;
#endif
    }

    public static bool LeftReleasedThisFrame()
    {
#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetMouseButtonUp(0);
#elif ENABLE_INPUT_SYSTEM
        return Mouse.current != null && Mouse.current.leftButton.wasReleasedThisFrame;
#else
        return false;
#endif
    }

    public static bool MiddlePressed()
    {
#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetMouseButton(2);
#elif ENABLE_INPUT_SYSTEM
        return Mouse.current != null && Mouse.current.middleButton.isPressed;
#else
        return false;
#endif
    }

    public static bool MiddlePressedThisFrame()
    {
#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetMouseButtonDown(2);
#elif ENABLE_INPUT_SYSTEM
        return Mouse.current != null && Mouse.current.middleButton.wasPressedThisFrame;
#else
        return false;
#endif
    }

    public static bool RightPressed()
    {
#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetMouseButton(1);
#elif ENABLE_INPUT_SYSTEM
        return Mouse.current != null && Mouse.current.rightButton.isPressed;
#else
        return false;
#endif
    }

    public static bool RightPressedThisFrame()
    {
#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetMouseButtonDown(1);
#elif ENABLE_INPUT_SYSTEM
        return Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame;
#else
        return false;
#endif
    }

    public static bool ShiftPressed()
    {
#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
#elif ENABLE_INPUT_SYSTEM
        return Keyboard.current != null &&
            (Keyboard.current.leftShiftKey.isPressed || Keyboard.current.rightShiftKey.isPressed);
#else
        return false;
#endif
    }

    public static bool AltPressed()
    {
#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
#elif ENABLE_INPUT_SYSTEM
        return Keyboard.current != null &&
            (Keyboard.current.leftAltKey.isPressed || Keyboard.current.rightAltKey.isPressed);
#else
        return false;
#endif
    }
}
