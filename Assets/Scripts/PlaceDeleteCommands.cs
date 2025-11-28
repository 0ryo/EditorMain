using UnityEngine;

public class PlaceObjectCommand : IEditorCommand {
    string typeId; Vector3 pos; Quaternion rot;
    GameObject instance;
    System.Func<string, GameObject> factory; // typeId→Instantiateする関数
    public string Label => "Place " + typeId;

    public PlaceObjectCommand(string typeId, Vector3 pos, Quaternion rot, System.Func<string,GameObject> factory){
        this.typeId=typeId; this.pos=pos; this.rot=rot; this.factory=factory;
    }
    public void Do()  { instance = factory(typeId); instance.transform.SetPositionAndRotation(pos, rot); }
    public void Undo(){ if (instance!=null) GameObject.Destroy(instance); }
}

public class DeleteObjectCommand : IEditorCommand {
    GameObject target;
    Vector3 pos; Quaternion rot; string typeId;
    System.Func<string, GameObject> factory;
    public string Label => "Delete " + (target? target.name : "obj");
    public DeleteObjectCommand(GameObject target, string typeId, System.Func<string,GameObject> factory){
        this.target=target; this.typeId=typeId; this.factory=factory;
        if (target!=null){ pos=target.transform.position; rot=target.transform.rotation; }
    }
    public void Do()  { if (target!=null) GameObject.Destroy(target); }
    public void Undo(){ var go = factory(typeId); go.transform.SetPositionAndRotation(pos, rot); }
}