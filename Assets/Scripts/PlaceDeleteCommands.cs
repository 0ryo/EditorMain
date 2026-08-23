using UnityEngine;

public class PlaceObjectCommand : IEditorCommand, IDiscardableEditorCommand {
    string typeId; Vector3 pos; Quaternion rot;
    GameObject instance;
    System.Func<string, GameObject> factory; // typeId→Instantiateする関数
    public string Label => "Place " + typeId;

    public PlaceObjectCommand(string typeId, Vector3 pos, Quaternion rot, System.Func<string,GameObject> factory){
        this.typeId=typeId; this.pos=pos; this.rot=rot; this.factory=factory;
    }
    public void Do()  {
        if (instance == null)
        {
            instance = factory != null ? factory(typeId) : null;
        }

        if (instance == null)
        {
            Debug.LogWarning($"[PlaceObjectCommand] Factory returned null for typeId={typeId}");
            return;
        }

        instance.SetActive(true);
        instance.transform.SetPositionAndRotation(pos, rot);
    }
    public void Undo(){ if (instance!=null) instance.SetActive(false); }
    public void Discard(){ if (instance!=null && !instance.activeSelf) GameObject.Destroy(instance); }
}

public class DeleteObjectCommand : IEditorCommand, IDiscardableEditorCommand {
    GameObject target;
    Transform parent;
    int siblingIndex;
    Vector3 pos; Quaternion rot; Vector3 scale; string typeId;
    bool wasActiveSelf;
    string id;
    string displayName;
    string description;
    bool hasDescriptionOverride;
    System.Func<string, GameObject> factory;
    readonly string targetName;
    public string Label => "Delete " + targetName;
    public DeleteObjectCommand(GameObject target, string typeId, System.Func<string,GameObject> factory){
        this.target=target; this.typeId=typeId; this.factory=factory;
        targetName = target != null ? target.name : "obj";
        if (target!=null){
            parent=target.transform.parent;
            siblingIndex=target.transform.GetSiblingIndex();
            pos=target.transform.position;
            rot=target.transform.rotation;
            scale=target.transform.localScale;
            wasActiveSelf=target.activeSelf;

            var placed=target.GetComponent<PlacedObject>();
            if (placed!=null){
                id=placed.id;
                displayName=placed.displayName;
                description=placed.description;
                hasDescriptionOverride=placed.hasDescriptionOverride;
            }
        }
    }
    public void Do()  { if (target!=null) target.SetActive(false); }
    public void Undo(){
        if (target==null)
        {
            target = factory != null ? factory(typeId) : null;
        }

        if (target == null)
        {
            Debug.LogWarning($"[DeleteObjectCommand] Factory returned null for typeId={typeId}");
            return;
        }

        target.transform.SetParent(parent, true);
        target.transform.SetSiblingIndex(siblingIndex);
        target.transform.SetPositionAndRotation(pos, rot);
        target.transform.localScale=scale;

        var placed=target.GetComponent<PlacedObject>();
        if (placed!=null){
            placed.id=id;
            placed.typeId=typeId;
            placed.displayName=displayName;
            placed.description=description;
            placed.hasDescriptionOverride=hasDescriptionOverride;
        }

        target.SetActive(wasActiveSelf);
    }
    public void Discard(){ if (target!=null && !target.activeSelf) GameObject.Destroy(target); }
}
