using System;
using System.Collections.Generic;
using UnityEngine;

// Copies rendering transforms only: no PlacedObject IDs, scripts, commands, or saved data.
public sealed class SkillSyncTrialSession : IDisposable, IScenarioObjectStateSource
{
    readonly GameObject root;
    readonly Dictionary<Transform, Transform> copies = new();
    readonly Dictionary<Renderer, bool> rendering = new();
    readonly List<Mesh> bakedMeshes = new();
    readonly Dictionary<string, Transform> objects = new();
    readonly Dictionary<Transform, Vector3> initial = new();
    readonly Dictionary<string, ScenarioConditionEvaluator> evaluators = new();
    readonly Dictionary<string, ConditionExport> conditionsById = new();
    public float HeldSeconds { get; private set; }
    public float Distance { get; private set; }
    public bool Complete { get; private set; }
    public bool InRange { get; private set; }
    public bool Paused { get; set; }

    public SkillSyncTrialSession(PlacedObject[] placed)
    {
        root = new GameObject("SkillSyncTrial_RenderingOnly");
        root.hideFlags = HideFlags.DontSave;
        try
        {
        foreach (var p in placed)
        {
            if (p == null || HasPlacedAncestor(p.transform)) continue;
            Copy(p.transform, root.transform, true);
        }
        foreach (var p in placed)
            if (p != null && copies.TryGetValue(p.transform, out var copy)) objects[p.Id] = copy;
        }
        catch { Dispose(); throw; }
    }

    static bool HasPlacedAncestor(Transform t)
    {
        for (var p = t.parent; p != null; p = p.parent) if (p.GetComponent<PlacedObject>() != null) return true;
        return false;
    }

    Transform Copy(Transform original, Transform parent, bool world)
    {
        var copy = new GameObject(original.name).transform;
        copy.SetParent(parent, false);
        copy.localPosition = world ? original.position : original.localPosition;
        copy.localRotation = world ? original.rotation : original.localRotation;
        copy.localScale = world ? original.lossyScale : original.localScale;
        copies[original] = copy; initial[copy] = copy.localPosition;
        var renderer = original.GetComponent<Renderer>();
        var filter = original.GetComponent<MeshFilter>();
        Mesh mesh = filter != null ? filter.sharedMesh : null;
        if (renderer is SkinnedMeshRenderer skin)
        {
            mesh = new Mesh(); skin.BakeMesh(mesh); bakedMeshes.Add(mesh);
        }
        if (renderer != null && mesh != null)
        {
            copy.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            var target = copy.gameObject.AddComponent<MeshRenderer>();
            target.sharedMaterials = renderer.sharedMaterials;
            target.enabled = renderer.enabled && original.gameObject.activeInHierarchy;
            rendering[renderer] = renderer.forceRenderingOff;
            target.forceRenderingOff = renderer.forceRenderingOff;
            renderer.forceRenderingOff = true;
        }
        foreach (Transform child in original) Copy(child, copy, false);
        return copy;
    }

    public Transform Find(string id) => !string.IsNullOrEmpty(id) && objects.TryGetValue(id, out var t) ? t : null;
    public static Vector3 Center(Transform t) => PlacedObjectGrounding.TryGetRendererBounds(t, out var b) ? b.center : t.position;
    public bool TryGetState(string objectId, out ScenarioObjectState state)
    {
        var t=Find(objectId);state=default;
        if(t==null) return false;
        state.position=Center(t);state.rotation=t.rotation;return true;
    }

    public void Tick(IReadOnlyList<ScenarioNode> conditions, string selectedId, float delta)
    {
        bool all = conditions.Count > 0;
        foreach (var node in conditions)
        {
            var c = node.condition; var first = Find(c.objectAId); var second = Find(c.objectBId);
            float distance = first != null && second != null ? Vector3.Distance(Center(first), Center(second)) : float.PositiveInfinity;
            if (!evaluators.TryGetValue(node.nodeId, out var evaluator))
            {
                var export=new ConditionExport {type=c.type,aObjectId=c.objectAId,bObjectId=c.objectBId,
                    distanceMeters=ConditionTypeCatalog.GetNumber(c,ConditionTypeCatalog.DistanceKey,.1f),
                    holdSeconds=ConditionTypeCatalog.GetNumber(c,ConditionTypeCatalog.HoldSecondsKey,0)};
                foreach(var p in c.parameters) if(p!=null) export.parameters.Add(new ConditionParameterExport
                    {key=p.key,numberValue=p.numberValue,textValue=p.textValue,boolValue=p.boolValue});
                conditionsById[node.nodeId]=export;
                evaluators[node.nodeId]=evaluator=new ScenarioConditionEvaluator();evaluator.Begin(export,this);
            }
            if(!Paused) evaluator.Tick(conditionsById[node.nodeId],this,delta);
            all &= evaluator.Succeeded;
            if (node.nodeId == selectedId)
            {
                Distance=distance;InRange=distance<=conditionsById[node.nodeId].distanceMeters;
                HeldSeconds=Mathf.Min(conditionsById[node.nodeId].holdSeconds,evaluator.HeldSeconds);
            }
        }
        Complete = all;
    }
    public void Reset(bool restorePositions = true)
    {
        if (restorePositions) foreach (var p in initial) if (p.Key != null) p.Key.localPosition = p.Value;
        HeldSeconds = 0; Complete = false; InRange = false; Paused = false;
        evaluators.Clear();conditionsById.Clear();
    }
    public void Dispose()
    {
        foreach (var p in rendering) if (p.Key != null) p.Key.forceRenderingOff = p.Value;
        if (root != null) { root.SetActive(false); UnityEngine.Object.Destroy(root); }
        foreach (var mesh in bakedMeshes) UnityEngine.Object.Destroy(mesh);
    }
}
