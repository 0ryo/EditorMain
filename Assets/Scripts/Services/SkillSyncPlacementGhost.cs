using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public sealed class SkillSyncPlacementGhost : IDisposable
{
    public string TypeId { get; }
    public string DisplayName { get; set; }
    public Transform Transform { get; }
    readonly List<Material> materials = new();
    readonly List<Mesh> meshes = new();
    Vector2 lastPointer;
    bool hasPointer;
    readonly bool translucent;
    float? fixedHeight;
    public SkillSyncPlacementGhost(PrefabEntry entry,bool translucent=true)
    {
        this.translucent=translucent;
        TypeId=entry.typeId;DisplayName=entry.prefab.name;
        Transform=new GameObject("PlacementGhost_RenderingOnly").transform;
        Transform.gameObject.hideFlags=HideFlags.DontSave;
        Transform.localScale=entry.prefab.transform.localScale;
        try {Copy(entry.prefab.transform,Transform,true);}
        catch {Dispose();throw;}
    }
    void Copy(Transform source,Transform parent,bool root=false)
    {
        var target=root?parent:new GameObject(source.name).transform;
        if(!root) {target.SetParent(parent,false);target.localPosition=source.localPosition;target.localRotation=source.localRotation;target.localScale=source.localScale;}
        var renderer=source.GetComponent<Renderer>();var filter=source.GetComponent<MeshFilter>();
        Mesh mesh=filter!=null?filter.sharedMesh:null;
        if(renderer is SkinnedMeshRenderer skin) {mesh=new Mesh();skin.BakeMesh(mesh);meshes.Add(mesh);}
        if(renderer!=null && mesh!=null)
        {
            target.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;
            var output=target.gameObject.AddComponent<MeshRenderer>();var array=new Material[renderer.sharedMaterials.Length];
            for(int i=0;i<array.Length;i++)
            {
                if(renderer.sharedMaterials[i]==null) continue;
                if(!translucent) {array[i]=renderer.sharedMaterials[i];continue;}
                var material=new Material(renderer.sharedMaterials[i]);materials.Add(material);array[i]=material;
                if(material.HasProperty("_BaseColor")) {var c=material.GetColor("_BaseColor");c.a=.35f;material.SetColor("_BaseColor",c);}
                if(material.HasProperty("_Color")) {var c=material.color;c.a=.35f;material.color=c;}
                material.SetFloat("_Surface",1);material.SetFloat("_ZWrite",0);
                material.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);material.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha);
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");material.renderQueue=(int)RenderQueue.Transparent;
            }
            output.sharedMaterials=array;output.shadowCastingMode=ShadowCastingMode.Off;
        }
        foreach(Transform child in source) Copy(child,target);
    }
    public void Follow(Camera camera,Vector2 pointer)
    {
        if (hasPointer && (pointer-lastPointer).sqrMagnitude < .01f) return;
        lastPointer=pointer;hasPointer=true;
        if(!EditWorkspace.TryScreenToGround(camera,pointer,out var ground,out _)) return;
        Transform.position=EditWorkspace.SnapPlacementPoint(ground,EditSnapSettings.GridSize,0);
        PlacedObjectGrounding.AlignRendererBoundsToGround(Transform.gameObject,EditWorkspace.GroundY,out _);
        if(fixedHeight.HasValue) Transform.position=new Vector3(Transform.position.x,fixedHeight.Value,Transform.position.z);
    }
    public void SetHeight(float value) {fixedHeight=value;Transform.position=new Vector3(Transform.position.x,value,Transform.position.z);}
    public void Dispose()
    {
        if(Transform!=null) {Transform.gameObject.SetActive(false);UnityEngine.Object.Destroy(Transform.gameObject);}
        foreach(var material in materials) UnityEngine.Object.Destroy(material);
        foreach(var mesh in meshes) UnityEngine.Object.Destroy(mesh);
    }
}
