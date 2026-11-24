using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

// On macOS, Unity places the IME candidate window (日本語の変換候補) from Input.compositionCursorPos measured from the
// bottom of the screen, while TMP_InputField passes it measured from the top (Windows convention). The candidates then
// open at the mirrored height, at the screen bottom or outside the window, and cannot be seen while typing
// (Unity issue "IME window is placed at the wrong position when using Input.compositionCursorPos on macOS").
// It also reads the value as pixels of the real screen (Retina backing pixels; it divides by the backing scale), while
// TMP measures in the Player's render resolution. Full screen, the Player draws 1920×1080 scaled to fit the display
// with black bars (2880×1800 on a 1440×900 pt MacBook: ×1.5 and 90 px bars above and below), so the window landed
// at two thirds of the way and off by the bar (checked on the device, 2026-11).
// This input override hands the position over flipped back and mapped onto the display; everything else is the
// default BaseInput.
public sealed class MacImeCandidateInput : BaseInput
{
    public override Vector2 compositionCursorPos
    {
        get => base.compositionCursorPos;
        set => base.compositionCursorPos = ToDisplay(new Vector2(value.x, Screen.height - value.y));
    }

    // Render pixels (from the bottom) → display pixels (from the bottom), as the fitted full-screen image is laid out.
    static Vector2 ToDisplay(Vector2 point)
    {
        if (!Screen.fullScreen || Screen.width <= 0 || Screen.height <= 0) return point;
        float width = Display.main.systemWidth, height = Display.main.systemHeight;
        if (width <= 0 || height <= 0) return point;
        float scale = Mathf.Min(width / Screen.width, height / Screen.height);
        var bars = new Vector2((width - Screen.width * scale) / 2, (height - Screen.height * scale) / 2);
        return bars + point * scale;
    }

    static bool OnMac => Application.platform == RuntimePlatform.OSXPlayer || Application.platform == RuntimePlatform.OSXEditor;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Install()
    {
        if (!OnMac) return;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        Apply();
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => Apply();

    // Every input module in the scene reads the caret position through this override (TMP asks the current module).
    static void Apply()
    {
        foreach (var module in Object.FindObjectsByType<BaseInputModule>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (module == null || module.inputOverride is MacImeCandidateInput) continue;
            var input = module.GetComponent<MacImeCandidateInput>();
            if (input == null) input = module.gameObject.AddComponent<MacImeCandidateInput>();
            module.inputOverride = input;
        }
    }
}
