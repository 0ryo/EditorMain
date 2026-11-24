using System;
using System.Collections.Generic;
using UnityEngine;

// The step editor shows the parts as the earlier completion conditions leave them (a jack already in place, a nut
// already turned): the parts are moved for the view only. The lesson's layout stays the saved truth: every capture
// (save, recovery, dirty check, export) runs with the layout put back (WithLayout), and leaving the step editor
// restores it. A part moved by anything else meanwhile (an Undo) keeps that new place.
public static class SkillSyncStepPose
{
    struct Entry
    {
        public Vector3 layoutPosition, posedPosition;
        public Quaternion layoutRotation, posedRotation;
    }

    static readonly Dictionary<Transform, Entry> Posed = new();

    public static bool Active => Posed.Count > 0;

    // A posed part was destroyed (a lesson was opened or the part deleted): the pose has to be made again.
    public static bool Stale
    {
        get
        {
            foreach (var t in Posed.Keys) if (t == null) return true;
            return false;
        }
    }

    // Moves the parts through the given conditions in order, starting from the lesson's layout.
    public static void Apply(IEnumerable<(ConditionNodeData condition, IReadOnlyList<ScenarioNode> step)> earlier)
    {
        Clear();
        var scene = new SkillSyncConditionMotion.Scene { find = FindPlaced, rootOf = t => t.root };
        var touched = new List<Transform>();
        void Touch(Transform t)
        {
            if (t == null || Posed.ContainsKey(t)) return;
            Posed[t] = new Entry { layoutPosition = t.localPosition, layoutRotation = t.localRotation };
            touched.Add(t);
        }
        try
        {
            foreach (var (condition, step) in earlier)
                SkillSyncConditionMotion.ApplyAtOnce(SkillSyncConditionMotion.Make(condition, step, scene), Touch);
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            Clear();
            return;
        }
        foreach (var t in touched)
        {
            var entry = Posed[t];
            entry.posedPosition = t.localPosition; entry.posedRotation = t.localRotation;
            Posed[t] = entry;
        }
        Physics.SyncTransforms();
    }

    // Puts the lesson's layout back.
    public static void Clear()
    {
        if (Posed.Count == 0) return;
        foreach (var pair in Posed) PutBack(pair.Key, pair.Value);
        Posed.Clear();
        Physics.SyncTransforms();
    }

    // Runs a capture of the scene (save, export) on the lesson's layout, then shows the posed parts again.
    public static T WithLayout<T>(Func<T> capture)
    {
        if (Posed.Count == 0) return capture();
        var back = new List<KeyValuePair<Transform, Entry>>();
        foreach (var pair in Posed) if (PutBack(pair.Key, pair.Value)) back.Add(pair);
        try { return capture(); }
        finally
        {
            foreach (var pair in back) if (pair.Key != null) pair.Key.SetLocalPositionAndRotation(pair.Value.posedPosition, pair.Value.posedRotation);
        }
    }

    static bool PutBack(Transform t, Entry entry)
    {
        if (t == null) return false;
        // Moved by something else since it was posed: that move is the new layout.
        if ((t.localPosition - entry.posedPosition).sqrMagnitude > 1e-10f || Quaternion.Angle(t.localRotation, entry.posedRotation) > .01f) return false;
        t.SetLocalPositionAndRotation(entry.layoutPosition, entry.layoutRotation);
        return true;
    }

    static readonly Dictionary<string, Transform> Lookup = new(StringComparer.Ordinal);

    static Transform FindPlaced(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        if (Lookup.TryGetValue(id, out var cached) && cached != null) return cached;
        Lookup.Clear();
        foreach (var placed in UnityEngine.Object.FindObjectsByType<PlacedObject>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            if (placed != null && !string.IsNullOrEmpty(placed.id)) Lookup[placed.id] = placed.transform;
        return Lookup.TryGetValue(id, out var found) ? found : null;
    }
}
