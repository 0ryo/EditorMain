using System;
using System.Collections.Generic;
using UnityEngine;

// Copies rendering transforms only: no PlacedObject IDs, scripts, commands, or saved data.
public sealed class SkillSyncTrialSession : IDisposable, IScenarioObjectStateSource, IScenarioDistanceSource
{
    readonly GameObject root;
    readonly Dictionary<Transform, Transform> copies = new();
    readonly Dictionary<Renderer, bool> rendering = new();
    readonly List<Mesh> bakedMeshes = new();
    readonly Dictionary<string, Transform> objects = new();
    readonly Dictionary<Transform, TransformObjectCommand.State> initial = new();
    readonly Dictionary<string, ScenarioConditionEvaluator> evaluators = new();
    readonly Dictionary<string, ConditionExport> conditionsById = new();
    // Conditions that put a tool on a part (measured at the tool's tip), by their exported form.
    readonly HashSet<ConditionExport> toolConditions = new();
    // Conditions the PC cannot judge (push/pull/grab/turn) are marked met by hand, only to walk through the flow.
    readonly HashSet<string> manual = new();
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
        copies[original] = copy; initial[copy] = TransformObjectCommand.State.Capture(copy);
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
    public static Vector3 Center(Transform t) => SkillSyncConditionMotion.Center(t);
    // The copied model a part belongs to: the copies hang under one rendering-only root, not the scene root.
    public Transform RootOf(Transform t)
    {
        if (t == null || root == null) return t;
        while (t.parent != null && t.parent != root.transform) t = t.parent;
        return t;
    }
    SkillSyncConditionMotion.Scene scene;
    public SkillSyncConditionMotion.Scene Scene => scene ??= new SkillSyncConditionMotion.Scene { find = Find, rootOf = RootOf };
    public bool TryGetDistance(ConditionExport condition, out float distance)
    {
        distance = SkillSyncConditionMotion.JudgeDistance(Find(condition.aObjectId), Find(condition.bObjectId), toolConditions.Contains(condition));
        return !float.IsInfinity(distance);
    }
    // Starts the trial where the earlier steps leave the parts (a jack already in place, a wrench already on the nut).
    public void ApplyEarlier(IEnumerable<(ConditionNodeData condition, IReadOnlyList<ScenarioNode> step)> earlier)
    {
        foreach (var (condition, step) in earlier) SkillSyncConditionMotion.ApplyAtOnce(SkillSyncConditionMotion.Make(condition, step, Scene), null);
        foreach (var key in new List<Transform>(initial.Keys)) if (key != null) initial[key] = TransformObjectCommand.State.Capture(key);
    }
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
            bool tool = SkillSyncConditionMotion.IsToolApproach(c, conditions);
            float distance = SkillSyncConditionMotion.JudgeDistance(first, second, tool);
            if (!evaluators.TryGetValue(node.nodeId, out var evaluator))
            {
                var export=new ConditionExport {type=c.type,aObjectId=c.objectAId,bObjectId=c.objectBId,
                    distanceMeters=ConditionTypeCatalog.GetNumber(c,ConditionTypeCatalog.DistanceKey,.1f),
                    holdSeconds=ConditionTypeCatalog.GetNumber(c,ConditionTypeCatalog.HoldSecondsKey,0)};
                foreach(var p in c.parameters) if(p!=null) export.parameters.Add(new ConditionParameterExport
                    {key=p.key,numberValue=p.numberValue,textValue=p.textValue,boolValue=p.boolValue});
                conditionsById[node.nodeId]=export;
                if(tool) toolConditions.Add(export);
                evaluators[node.nodeId]=evaluator=new ScenarioConditionEvaluator();evaluator.Begin(export,this);
            }
            if(!Paused) evaluator.Tick(conditionsById[node.nodeId],this,delta);
            all &= evaluator.Succeeded || manual.Contains(node.nodeId);
            if (node.nodeId == selectedId)
            {
                Distance=distance;InRange=distance<=conditionsById[node.nodeId].distanceMeters;
                HeldSeconds=Mathf.Min(conditionsById[node.nodeId].holdSeconds,evaluator.HeldSeconds);
            }
        }
        Complete = all;
    }
    // Pulled-off parts disappear. Parts hidden during the current step come back on "やり直す" (Reset);
    // once the next step begins they stay hidden.
    readonly List<Renderer> hiddenThisStep = new();
    public void Hide(Transform t, bool forThisStep = true)
    {
        if (t == null) return;
        foreach (var r in t.GetComponentsInChildren<Renderer>())
        {
            if (!r.enabled) continue;
            r.enabled = false;
            if (forThisStep) hiddenThisStep.Add(r);
        }
    }
    public void MarkSucceeded(string nodeId) { if (!string.IsNullOrEmpty(nodeId)) manual.Add(nodeId); }
    public bool IsSucceeded(string nodeId) => manual.Contains(nodeId) || (evaluators.TryGetValue(nodeId, out var e) && e.Succeeded);
    // Starts the next step from where the parts now are: Reset() returns to this state, not to the lesson's first layout.
    public void BeginNextStep()
    {
        foreach (var key in new List<Transform>(initial.Keys)) if (key != null) initial[key] = TransformObjectCommand.State.Capture(key);
        hiddenThisStep.Clear();
        Reset(false);
    }

    public void Reset(bool restorePositions = true)
    {
        if (restorePositions) foreach (var p in initial) if (p.Key != null)
        {
            p.Key.localPosition = p.Value.localPosition;
            p.Key.localRotation = p.Value.localRotation;
            p.Key.localScale = p.Value.localScale;
        }
        if (restorePositions) foreach (var r in hiddenThisStep) if (r != null) r.enabled = true;
        hiddenThisStep.Clear();
        HeldSeconds = 0; Complete = false; InRange = false; Paused = false;
        evaluators.Clear();conditionsById.Clear();toolConditions.Clear();manual.Clear();
    }
    public void Dispose()
    {
        foreach (var p in rendering) if (p.Key != null) p.Key.forceRenderingOff = p.Value;
        if (root != null) { root.SetActive(false); UnityEngine.Object.Destroy(root); }
        foreach (var mesh in bakedMeshes) UnityEngine.Object.Destroy(mesh);
    }
}
