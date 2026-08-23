using UnityEngine;

public class MoveObjectCommand : IEditorCommand {
    GameObject target; Vector3 from, to;
    public string Label => "Move";
    public MoveObjectCommand(GameObject t, Vector3 from, Vector3 to){ target=t; this.from=from; this.to=to; }
    public bool Do()  { if(!target) return false; target.transform.position = to; return true; }
    public bool Undo(){ if(!target) return false; target.transform.position = from; return true; }
}
public class RotateObjectCommand : IEditorCommand {
    GameObject target; float fromY, toY;
    public string Label => "Rotate";
    public RotateObjectCommand(GameObject t, float fromY, float toY){ target=t; this.fromY=fromY; this.toY=toY; }
    public bool Do()  { if(!target) return false; var e=target.transform.eulerAngles; e.y=toY; target.transform.eulerAngles=e; return true; }
    public bool Undo(){ if(!target) return false; var e=target.transform.eulerAngles; e.y=fromY; target.transform.eulerAngles=e; return true; }
}

public class RotateObjectQuaternionCommand : IEditorCommand {
    GameObject target; Quaternion from, to;
    public string Label => "Rotate";
    public RotateObjectQuaternionCommand(GameObject t, Quaternion from, Quaternion to){ target=t; this.from=from; this.to=to; }
    public bool Do()  { if(!target) return false; target.transform.rotation = to; return true; }
    public bool Undo(){ if(!target) return false; target.transform.rotation = from; return true; }
}

public class ScaleObjectCommand : IEditorCommand {
    GameObject target; Vector3 from, to;
    public string Label => "Scale";
    public ScaleObjectCommand(GameObject t, Vector3 from, Vector3 to){ target=t; this.from=from; this.to=to; }
    public bool Do()  { if(!target) return false; target.transform.localScale = to; return true; }
    public bool Undo(){ if(!target) return false; target.transform.localScale = from; return true; }
}
