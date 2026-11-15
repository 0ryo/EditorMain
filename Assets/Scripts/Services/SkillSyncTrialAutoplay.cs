using System;
using System.Collections.Generic;
using UnityEngine;

// Plays the ideal motion of each completion condition on the trial copies, one step at a time, so the
// author can watch the lesson instead of moving parts by hand. Only the rendering-only copies move:
// the lesson data, editing selection, and Undo history are never touched.
// Moving a part by hand (drag or gizmo) hands the rest of that step over to the author.
public sealed class SkillSyncTrialAutoplay
{
    const float StartDelay = .6f, ConditionGap = .3f, StepDwell = 1.2f, JudgeGrace = 2f, Lift = .05f, Nudge = .03f, PullMin = .4f, PullMax = 1.2f, GroundSlack = .005f;

    sealed class Segment
    {
        public Transform target;
        public Vector3 move, axis;
        public float angle, duration, elapsed;
        public bool started, hideAtEnd, fixedPivot, onGround;
        public Vector3 pivot, p0;
        public Quaternion r0;
    }

    readonly Camera camera;
    readonly Action<string> log, notify;
    Action<Transform> hide;
    readonly Queue<Segment> segments = new();
    string stepId, conditionId;
    int index;
    float wait, judgeTimeout;
    bool judged, finishing;
    Transform watched;
    Vector3 watchedPosition;
    Quaternion watchedRotation;

    // True once the author took over this step (or a motion did not meet its condition); the next step plays again.
    public bool Manual { get; private set; }
    public string Note { get; private set; } = "";
    public bool Playing => !Manual && stepId != null;

    public SkillSyncTrialAutoplay(Camera camera, Action<string> log, Action<string> notify)
    {
        this.camera = camera; this.log = log; this.notify = notify;
    }

    // "この手順をやり直す" and starting from a step card play the step again from the beginning.
    public void Restart() => stepId = null;

    // Returns true once when the step's conditions are all met and the pause after it has passed: the caller moves on.
    public bool Tick(SkillSyncTrialSession trial, string currentStepId, IReadOnlyList<ScenarioNode> conditions, Transform selected, float delta)
    {
        if (trial == null) return false;
        hide = t => trial.Hide(t);
        if (stepId != currentStepId) Begin(currentStepId);
        if (Manual) return false;
        if (MovedByHand(selected))
        {
            Manual = true; segments.Clear(); Note = "";
            log("部品が動かされたため、この手順は手動で確認します");
            notify("この手順は手動で確認します。次の手順から自動再生に戻ります");
            return false;
        }
        bool advance = delta > 0 && Advance(trial, conditions, delta);
        Remember(selected);
        return advance;
    }

    void Begin(string id)
    {
        stepId = id; conditionId = null; index = -1; Manual = false; finishing = false;
        segments.Clear(); wait = StartDelay; Note = ""; watched = null;
    }

    bool MovedByHand(Transform selected)
    {
        if (selected == null || selected != watched) return false;
        return (selected.position - watchedPosition).sqrMagnitude > 1e-10f || Quaternion.Angle(selected.rotation, watchedRotation) > .01f;
    }

    void Remember(Transform selected)
    {
        watched = selected;
        if (selected != null) { watchedPosition = selected.position; watchedRotation = selected.rotation; }
    }

    bool Advance(SkillSyncTrialSession trial, IReadOnlyList<ScenarioNode> conditions, float delta)
    {
        if (wait > 0) { wait -= delta; if (wait > 0 || !finishing) return false; return true; }
        if (segments.Count > 0) { Play(segments.Peek(), delta); return false; }
        if (conditionId != null)
        {
            if (!judged) { trial.MarkSucceeded(conditionId); log("PCでは判定しないため、成立として進めました"); }
            else if (!trial.IsSucceeded(conditionId))
            {
                if ((judgeTimeout -= delta) > 0) return false;
                Manual = true; Note = "";
                log("自動再生では条件が成立しませんでした");
                notify("自動再生では完了条件が成立しませんでした。距離や対象を見直すか、部品を動かして確かめてください");
                return false;
            }
            conditionId = null; wait = ConditionGap;
            return false;
        }
        if (trial.Complete || index + 1 >= conditions.Count)
        {
            if (!trial.Complete) return false;
            finishing = true; wait = StepDwell; Note = "";
            return false;
        }
        var node = conditions[++index];
        if (node == null || trial.IsSucceeded(node.nodeId)) return false;
        conditionId = node.nodeId;
        Plan(trial, node.condition, conditions);
        return false;
    }

    void Plan(SkillSyncTrialSession trial, ConditionNodeData c, IReadOnlyList<ScenarioNode> stepConditions)
    {
        var a = trial.Find(c.objectAId);
        var b = trial.Find(c.objectBId);
        string label = ConditionTypeCatalog.Find(c.type)?.label ?? c.type;
        float distance = ConditionTypeCatalog.GetNumber(c, ConditionTypeCatalog.DistanceKey, .1f);
        float hold = ConditionTypeCatalog.GetNumber(c, ConditionTypeCatalog.HoldSecondsKey, 0);
        float angle = ConditionTypeCatalog.GetNumber(c, ConditionTypeCatalog.AngleKey, 90);
        judged = false; judgeTimeout = 0;
        bool pair = a != null && b != null && !b.IsChildOf(a);
        switch (c.type)
        {
            case ConditionTypeCatalog.Proximity:
            case ConditionTypeCatalog.SnapHold:
            case ConditionTypeCatalog.Separation:
                if (!pair) { Unplayable(label); return; }
                {
                    bool apart = c.type == ConditionTypeCatalog.Separation;
                    Vector3 from = SkillSyncTrialSession.Center(a), to = SkillSyncTrialSession.Center(b), offset = from - to;
                    Vector3 direction = offset.sqrMagnitude > 1e-8f ? offset.normalized : Horizontal(-camera.transform.right);
                    // Half the range inside, so the gauge visibly passes the line; Separation goes a little past it.
                    bool already = apart ? offset.magnitude >= distance : offset.magnitude <= distance * .5f;
                    var target = already ? from : to + direction * distance * (apart ? 1.2f : .5f);
                    if (!already) target = AboveGround(a, from, target, to, apart ? 0 : distance * .5f);
                    Move(a, target - from);
                }
                judged = true; judgeTimeout = hold + JudgeGrace;
                Note = c.type == ConditionTypeCatalog.SnapHold ? $"{label}：近づける先まで動かし、{hold:0.##}秒とどめます" :
                    c.type == ConditionTypeCatalog.Separation ? $"{label}：{distance * 100:0.##} cm以上離します" : $"{label}：近づける先の範囲まで動かします";
                break;
            case ConditionTypeCatalog.RotationMatch:
                if (!pair) { Unplayable(label); return; }
                (b.rotation * Quaternion.Inverse(a.rotation)).ToAngleAxis(out float match, out var matchAxis);
                if (match > 180) match -= 360;
                if (Mathf.Abs(match) > .01f) Turn(a, matchAxis, match);
                judged = true; judgeTimeout = hold + JudgeGrace;
                Note = $"{label}：向きの基準にそろえ、{hold:0.##}秒とどめます";
                break;
            case ConditionTypeCatalog.Turn:
                if (a == null) { Unplayable(label); return; }
                {
                    bool counter = (int)ConditionTypeCatalog.GetNumber(c, ConditionTypeCatalog.TurnDirectionKey, 0) == ConditionTypeCatalog.TurnCounterclockwise;
                    // A tool brought to a part in the same step (a wrench to a nut) turns about that part's axis,
                    // whatever axis the tool's model was made with.
                    var partner = trial.Find(TurnPartner(c.objectAId, stepConditions));
                    if (partner != null && (partner == a || partner.IsChildOf(a) || a.IsChildOf(partner))) partner = null;
                    var axis = TurnAxis(partner != null ? partner : a);
                    // Seen from the camera along the axis, a positive angle turns counterclockwise.
                    Turn(a, axis, counter ? angle : -angle, partner);
                    Note = $"{label}：{angle:0.##}度、{(counter ? "反時計回り" : "時計回り")}に回します";
                }
                break;
            case ConditionTypeCatalog.Grabbed:
                if (a == null) { Unplayable(label); return; }
                Move(a, Vector3.up * Lift, .35f); Hold(a, .3f);
                Note = $"{label}：持ち上げる動きで示します";
                break;
            case ConditionTypeCatalog.Released:
                if (a == null) { Unplayable(label); return; }
                Move(a, Vector3.up * Lift, .35f); Hold(a, .3f); Move(a, Vector3.down * Lift, .35f);
                Note = $"{label}：持ち上げて置く動きで示します";
                break;
            case ConditionTypeCatalog.Push:
                if (a == null) { Unplayable(label); return; }
                {
                    var away = Horizontal(camera.transform.forward) * Nudge;
                    Move(a, away, .3f); Move(a, -away, .3f);
                    Note = $"{label}：奥へ押す動きで示します";
                }
                break;
            case ConditionTypeCatalog.Pull:
                if (a == null) { Unplayable(label); return; }
                {
                    // Pulled well clear toward the viewer, then gone: the part is off for the following steps.
                    var toward = Horizontal(-camera.transform.forward);
                    float size = PlacedObjectGrounding.TryGetRendererBounds(a, out var bounds) ? bounds.size.magnitude : .3f;
                    Move(a, toward * Mathf.Clamp(size * 1.5f, PullMin, PullMax), 1.2f);
                    Hold(a, .2f);
                    segments.Enqueue(new Segment { target = a, axis = Vector3.up, duration = 0, hideAtEnd = true });
                    Note = $"{label}：手前へ引いて取り外す動きで示します";
                }
                break;
            default:
                Unplayable(label);
                return;
        }
        log("自動再生：" + Note);
    }

    void Unplayable(string label)
    {
        Note = $"{label}：PCでは再現できない動きです";
        log(Note);
    }

    // A part standing on the floor is never planned into it (a jack brought to a marker on the floor):
    // its center is raised until its bottom is on the ground, still within range of the target if that is possible.
    static Vector3 AboveGround(Transform part, Vector3 from, Vector3 target, Vector3 to, float range)
    {
        if (!PlacedObjectGrounding.TryGetRendererBounds(part, out var bounds) || bounds.min.y < EditWorkspace.GroundY - GroundSlack) return target;
        float lowest = EditWorkspace.GroundY + from.y - bounds.min.y;
        if (target.y >= lowest) return target;
        var flat = target - to; flat.y = 0;
        float rise = lowest - to.y, room = range * range - rise * rise;
        if (range > 0) flat = room > 0 && flat.sqrMagnitude > 1e-8f ? flat.normalized * Mathf.Min(flat.magnitude, Mathf.Sqrt(room)) : Vector3.zero;
        return new Vector3(to.x + flat.x, lowest, to.z + flat.z);
    }

    static Vector3 Horizontal(Vector3 v)
    {
        v.y = 0;
        return v.sqrMagnitude > 1e-6f ? v.normalized : Vector3.forward;
    }

    // The other part of a "近づける" condition on the same object in this step (the nut a wrench is put on).
    static string TurnPartner(string objectId, IReadOnlyList<ScenarioNode> stepConditions)
    {
        if (string.IsNullOrEmpty(objectId) || stepConditions == null) return null;
        foreach (var node in stepConditions)
        {
            var other = node?.condition;
            if (other == null || (other.type != ConditionTypeCatalog.Proximity && other.type != ConditionTypeCatalog.SnapHold)) continue;
            if (other.objectAId == objectId && !string.IsNullOrEmpty(other.objectBId)) return other.objectBId;
            if (other.objectBId == objectId && !string.IsNullOrEmpty(other.objectAId)) return other.objectAId;
        }
        return null;
    }

    // A turning part is round about its axis: of its three sides (in its own orientation), two are about equal and
    // the axis is the remaining one (a nut, a wheel, a bolt). Otherwise the thinnest world side, preferring vertical.
    Vector3 TurnAxis(Transform part)
    {
        if (!TrySymmetryAxis(part, out var axis))
        {
            var size = PlacedObjectGrounding.TryGetRendererBounds(part, out var bounds) ? bounds.size : Vector3.one;
            axis = Vector3.up;
            float thinnest = size.y * .9f;
            if (size.x < thinnest) { axis = Vector3.right; thinnest = size.x; }
            if (size.z < thinnest) axis = Vector3.forward;
        }
        if (Vector3.Dot(axis, camera.transform.position - SkillSyncTrialSession.Center(part)) < 0) axis = -axis;
        return axis;
    }

    static bool TrySymmetryAxis(Transform part, out Vector3 axis)
    {
        axis = Vector3.up;
        bool any = false;
        var local = new Bounds();
        foreach (var filter in part.GetComponentsInChildren<MeshFilter>())
        {
            var mesh = filter.sharedMesh;
            if (mesh == null) continue;
            var mb = mesh.bounds;
            for (int i = 0; i < 8; i++)
            {
                var corner = mb.center + Vector3.Scale(mb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                var p = part.InverseTransformPoint(filter.transform.TransformPoint(corner));
                if (!any) { local = new Bounds(p, Vector3.zero); any = true; }
                else local.Encapsulate(p);
            }
        }
        if (!any) return false;
        var size = Vector3.Scale(local.size, part.lossyScale);
        float[] s = { Mathf.Abs(size.x), Mathf.Abs(size.y), Mathf.Abs(size.z) };
        int best = -1;
        float bestPair = 0;
        for (int i = 0; i < 3; i++)
        {
            float j = s[(i + 1) % 3], k = s[(i + 2) % 3];
            if (j <= 1e-6f || k <= 1e-6f || s[i] <= 1e-6f) continue;
            float pair = Mathf.Min(j, k) / Mathf.Max(j, k);
            float odd = s[i] / ((j + k) * .5f);
            if (pair < .8f || (odd > .87f && odd < 1.15f)) continue;
            if (pair > bestPair) { bestPair = pair; best = i; }
        }
        if (best < 0) return false;
        axis = part.TransformDirection(best == 0 ? Vector3.right : best == 1 ? Vector3.up : Vector3.forward).normalized;
        return true;
    }

    void Move(Transform target, Vector3 move, float duration = -1)
    {
        if (duration < 0) duration = Mathf.Clamp(.5f + move.magnitude, .5f, 1.5f);
        segments.Enqueue(new Segment { target = target, move = move, axis = Vector3.up, duration = duration });
    }

    // With a center, the target swings about that part (a wrench about the nut) instead of about its own middle.
    void Turn(Transform target, Vector3 axis, float angle, Transform center = null) =>
        segments.Enqueue(new Segment { target = target, axis = axis, angle = angle, duration = Mathf.Clamp(Mathf.Abs(angle) / 180f, .6f, 3f),
            fixedPivot = center != null, pivot = center != null ? SkillSyncTrialSession.Center(center) : Vector3.zero });

    void Hold(Transform target, float duration) => segments.Enqueue(new Segment { target = target, axis = Vector3.up, duration = duration });

    void Play(Segment s, float delta)
    {
        if (s.target == null) { segments.Dequeue(); return; }
        if (s.hideAtEnd) { segments.Dequeue(); hide?.Invoke(s.target); return; }
        if (!s.started)
        {
            s.started = true; s.p0 = s.target.position; s.r0 = s.target.rotation;
            if (!s.fixedPivot) s.pivot = SkillSyncTrialSession.Center(s.target);
            s.onGround = PlacedObjectGrounding.TryGetRendererBounds(s.target, out var start) && start.min.y >= EditWorkspace.GroundY - GroundSlack;
        }
        s.elapsed += delta;
        float t = Mathf.Clamp01(s.elapsed / s.duration);
        t = t * t * (3 - 2 * t);
        var turn = Quaternion.AngleAxis(s.angle * t, s.axis);
        s.target.SetPositionAndRotation(s.pivot + s.move * t + turn * (s.p0 - s.pivot), turn * s.r0);
        // Whatever the motion (a swing, a set-down), a part that started above the floor does not sink into it.
        if (s.onGround && PlacedObjectGrounding.TryGetRendererBounds(s.target, out var now) && now.min.y < EditWorkspace.GroundY)
            s.target.position += Vector3.up * (EditWorkspace.GroundY - now.min.y);
        if (s.elapsed >= s.duration) segments.Dequeue();
    }
}
