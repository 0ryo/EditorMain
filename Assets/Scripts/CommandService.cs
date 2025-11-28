using UnityEngine;

public class CommandService : MonoBehaviour {
    public static CommandService I;
    public CommandStack Stack = new();
    void Awake(){ I=this; }
    void Update(){
        bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightCommand);
        if (ctrl && Input.GetKeyDown(KeyCode.Z)) Stack.Undo();
        if (ctrl && (Input.GetKeyDown(KeyCode.Y) || Input.GetKeyDown(KeyCode.Z) && Input.GetKey(KeyCode.LeftShift))) Stack.Redo();
    }
}