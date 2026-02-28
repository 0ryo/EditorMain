using UnityEngine;

public class MoveObjectCommand : IEditorCommand {
    GameObject target; Vector3 from, to;
    public string Label => "Move";
    public MoveObjectCommand(GameObject t, Vector3 from, Vector3 to){ target=t; this.from=from; this.to=to; }
    public void Do()  { if(target) target.transform.position = to; }
    public void Undo(){ if(target) target.transform.position = from; }
}
public class RotateObjectCommand : IEditorCommand {
    GameObject target; float fromY, toY;
    public string Label => "Rotate";
    public RotateObjectCommand(GameObject t, float fromY, float toY){ target=t; this.fromY=fromY; this.toY=toY; }
    public void Do()  { if(target){ var e=target.transform.eulerAngles; e.y=toY; target.transform.eulerAngles=e; } }
    public void Undo(){ if(target){ var e=target.transform.eulerAngles; e.y=fromY; target.transform.eulerAngles=e; } }
}

public class RotateObjectQuaternionCommand : IEditorCommand {
    GameObject target; Quaternion from, to;
    public string Label => "Rotate";
    public RotateObjectQuaternionCommand(GameObject t, Quaternion from, Quaternion to){ target=t; this.from=from; this.to=to; }
    public void Do()  { if(target) target.transform.rotation = to; }
    public void Undo(){ if(target) target.transform.rotation = from; }
}

public class ScaleObjectCommand : IEditorCommand {
    GameObject target; Vector3 from, to;
    public string Label => "Scale";
    public ScaleObjectCommand(GameObject t, Vector3 from, Vector3 to){ target=t; this.from=from; this.to=to; }
    public void Do()  { if(target) target.transform.localScale = to; }
    public void Undo(){ if(target) target.transform.localScale = from; }
}
