using UnityEngine;
public enum EditMode { None, Place, Move, Rotate }
public class EditModeService : MonoBehaviour {
    public static EditModeService I;
    public EditMode Mode = EditMode.None;
    void Awake(){ I=this; }
    void Update(){
        if (Input.GetKeyDown(KeyCode.W)) Mode = EditMode.Move;
        if (Input.GetKeyDown(KeyCode.E)) Mode = EditMode.Rotate;
    }
}