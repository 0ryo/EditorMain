using UnityEngine;

public class PlaceObjectCommand : IEditorCommand, IDiscardableEditorCommand {
    string typeId; Vector3 pos; Quaternion rot;
    GameObject instance;
    System.Func<string, GameObject> factory; // typeId→Instantiateする関数
    public string Label => "Place " + typeId;

    public PlaceObjectCommand(string typeId, Vector3 pos, Quaternion rot, System.Func<string,GameObject> factory){
        this.typeId=typeId; this.pos=pos; this.rot=rot; this.factory=factory;
    }
    public bool Do()  {
        if (instance == null)
        {
            instance = factory != null ? factory(typeId) : null;
        }

        if (instance == null)
        {
            Debug.LogWarning($"[PlaceObjectCommand] Factory returned null for typeId={typeId}");
            return false;
        }

        instance.SetActive(true);
        instance.transform.SetPositionAndRotation(pos, rot);
        return true;
    }
    public bool Undo(){ if (instance==null) return false; instance.SetActive(false); return true; }
    public void Discard(){ if (instance!=null && !instance.activeSelf) GameObject.Destroy(instance); }
}

public class DuplicateObjectCommand : IEditorCommand, IDiscardableEditorCommand {
    readonly GameObject source;
    readonly Vector3 pos;
    readonly Quaternion rot;
    readonly string sourceName;
    GameObject instance;
    PlacedObject placed;

    public string Label => "Duplicate " + sourceName;
    public PlacedObject Result => placed;

    public DuplicateObjectCommand(GameObject source, Vector3 offset){
        this.source=source;
        sourceName=source!=null ? source.name : "obj";
        if (source!=null){
            pos=source.transform.position+offset;
            rot=source.transform.rotation;
        }
    }

    public bool Do(){
        if (instance==null){
            if (source==null){
                Debug.LogWarning("[DuplicateObjectCommand] Source object is missing.");
                return false;
            }

            instance=GameObject.Instantiate(source, pos, rot);
            placed=instance.GetComponent<PlacedObject>();
            if (placed==null) placed=instance.AddComponent<PlacedObject>();

            var sourcePlaced=source.GetComponent<PlacedObject>();
            if (string.IsNullOrEmpty(placed.typeId) && sourcePlaced!=null){
                placed.typeId=sourcePlaced.typeId;
            }

            placed.ForceNewId();
            PlacedObjectPickability.EnsurePickable(placed, true);
        }

        instance.SetActive(true);
        instance.transform.SetPositionAndRotation(pos, rot);
        return true;
    }

    public bool Undo(){ if (instance==null) return false; instance.SetActive(false); return true; }
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
    public bool Do()  { if (target==null) return false; target.SetActive(false); return true; }
    public bool Undo(){
        if (target==null)
        {
            target = factory != null ? factory(typeId) : null;
        }

        if (target == null)
        {
            Debug.LogWarning($"[DeleteObjectCommand] Factory returned null for typeId={typeId}");
            return false;
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
        return true;
    }
    public void Discard(){ if (target!=null && !target.activeSelf) GameObject.Destroy(target); }
}
