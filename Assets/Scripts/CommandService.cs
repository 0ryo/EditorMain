using UnityEngine;

public class CommandService : MonoBehaviour {
    public static CommandService I;
    public CommandStack Stack = new();
    void Awake(){ I=this; }
    void Update(){
        if (EditWorkspace.IsTypingIntoInputField()) return;

        bool primaryModifier =
            Input.GetKey(KeyCode.LeftControl) ||
            Input.GetKey(KeyCode.RightControl) ||
            Input.GetKey(KeyCode.LeftCommand) ||
            Input.GetKey(KeyCode.RightCommand);
        if (!primaryModifier) return;

        bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        bool zPressed = Input.GetKeyDown(KeyCode.Z);
        bool redoPressed = Input.GetKeyDown(KeyCode.Y) || (shift && zPressed);

        if (redoPressed){
            Stack.Redo();
            return;
        }

        if (zPressed) Stack.Undo();
    }
}
