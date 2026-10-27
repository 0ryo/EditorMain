using System;
using System.Collections.Generic;
using UnityEngine;

// Plays the ideal motion of each completion condition on the trial copies, one step at a time, so the
// author can watch the lesson instead of moving parts by hand. Only the rendering-only copies move:
// the lesson data, editing selection, and Undo history are never touched.
// Moving a part by hand (drag or gizmo) hands the rest of that step over to the author.
public sealed class SkillSyncTrialAutoplay
{
    const float StartDelay = .6f, ConditionGap = .3f, StepDwell = 1.2f, JudgeGrace = 2f, Lift = .05f, Nudge = .03f;

    sealed class Segment
    {
        public Transform target;
        public Vector3 move, axis;
        public float angle, duration, elapsed;
        public bool started;
        public Vector3 pivot, p0;
        public Quaternion r0;
    }

    readonly Camera camera;
    readonly Action<string> log, notify;
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
        Plan(trial, node.condition);
        return false;
    }

    void Plan(SkillSyncTrialSession trial, ConditionNodeData c)
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
                    var axis = TurnAxis(a);
                    // Seen from the camera along the axis, a positive angle turns counterclockwise.
                    Turn(a, axis, counter ? angle : -angle);
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
            case ConditionTypeCatalog.Pull:
                if (a == null) { Unplayable(label); return; }
                {
                    var away = Horizontal(camera.transform.forward) * (c.type == ConditionTypeCatalog.Push ? Nudge : -Nudge);
                    Move(a, away, .3f); Move(a, -away, .3f);
                    Note = c.type == ConditionTypeCatalog.Push ? $"{label}：奥へ押す動きで示します" : $"{label}：手前へ引く動きで示します";
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

    static Vector3 Horizontal(Vector3 v)
    {
        v.y = 0;
        return v.sqrMagnitude > 1e-6f ? v.normalized : Vector3.forward;
    }

    // The thinnest side of the part is its turning axis (a wheel or a nut); ties prefer the vertical axis.
    Vector3 TurnAxis(Transform part)
    {
        var size = PlacedObjectGrounding.TryGetRendererBounds(part, out var bounds) ? bounds.size : Vector3.one;
        var axis = Vector3.up;
        float thinnest = size.y * .9f;
        if (size.x < thinnest) { axis = Vector3.right; thinnest = size.x; }
        if (size.z < thinnest) axis = Vector3.forward;
        if (Vector3.Dot(axis, camera.transform.position - SkillSyncTrialSession.Center(part)) < 0) axis = -axis;
        return axis;
    }

    void Move(Transform target, Vector3 move, float duration = -1)
    {
        if (duration < 0) duration = Mathf.Clamp(.5f + move.magnitude, .5f, 1.5f);
        segments.Enqueue(new Segment { target = target, move = move, axis = Vector3.up, duration = duration });
    }

    void Turn(Transform target, Vector3 axis, float angle) =>
        segments.Enqueue(new Segment { target = target, axis = axis, angle = angle, duration = Mathf.Clamp(Mathf.Abs(angle) / 180f, .6f, 3f) });

    void Hold(Transform target, float duration) => segments.Enqueue(new Segment { target = target, axis = Vector3.up, duration = duration });

    void Play(Segment s, float delta)
    {
        if (s.target == null) { segments.Dequeue(); return; }
        if (!s.started)
        {
            s.started = true; s.p0 = s.target.position; s.r0 = s.target.rotation; s.pivot = SkillSyncTrialSession.Center(s.target);
        }
        s.elapsed += delta;
        float t = Mathf.Clamp01(s.elapsed / s.duration);
        t = t * t * (3 - 2 * t);
        var turn = Quaternion.AngleAxis(s.angle * t, s.axis);
        s.target.SetPositionAndRotation(s.pivot + s.move * t + turn * (s.p0 - s.pivot), turn * s.r0);
        if (s.elapsed >= s.duration) segments.Dequeue();
    }
}
