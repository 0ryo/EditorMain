using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

// On macOS, Unity places the IME candidate window (日本語の変換候補) from Input.compositionCursorPos measured from the
// bottom of the screen, while TMP_InputField passes it measured from the top (Windows convention). The candidates then
// open at the mirrored height, at the screen bottom or outside the window, and cannot be seen while typing
// (Unity issue "IME window is placed at the wrong position when using Input.compositionCursorPos on macOS").
// This input override hands the position over flipped back; everything else is the default BaseInput.
public sealed class MacImeCandidateInput : BaseInput
{
    public override Vector2 compositionCursorPos
    {
        get => base.compositionCursorPos;
        set => base.compositionCursorPos = new Vector2(value.x, Screen.height - value.y);
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
