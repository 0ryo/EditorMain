using System;
using System.Collections.Generic;
using UnityEngine;

// Plays the ideal motion of each completion condition on the trial copies, one step at a time, so the
// author can watch the lesson instead of moving parts by hand. Only the rendering-only copies move:
// the lesson data, editing selection, and Undo history are never touched.
// The motion itself is planned by SkillSyncConditionMotion, which the step editor also uses to show the parts
// as earlier conditions leave them.
// Moving a part by hand (drag or gizmo) hands the rest of that step over to the author.
public sealed class SkillSyncTrialAutoplay
{
    const float StartDelay = .6f, ConditionGap = .3f, StepDwell = 1.2f, JudgeGrace = 2f;

    sealed class Segment
    {
        public SkillSyncConditionMotion.Piece piece;
        public SkillSyncConditionMotion.Start start;
        public float elapsed;
        public bool started;
    }

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

    public SkillSyncTrialAutoplay(Action<string> log, Action<string> notify)
    {
        this.log = log; this.notify = notify;
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
        var plan = SkillSyncConditionMotion.Make(node.condition, conditions, trial.Scene);
        judged = plan.judged; judgeTimeout = plan.hold + JudgeGrace; Note = plan.note;
        foreach (var piece in plan.pieces) segments.Enqueue(new Segment { piece = piece });
        log((plan.playable ? "自動再生：" : "") + Note);
        return false;
    }

    void Play(Segment s, float delta)
    {
        var piece = s.piece;
        if (piece.target == null) { segments.Dequeue(); return; }
        if (piece.kind == SkillSyncConditionMotion.Kind.Hide) { segments.Dequeue(); hide?.Invoke(piece.target); return; }
        if (!s.started) { s.started = true; if (piece.kind == SkillSyncConditionMotion.Kind.Move) s.start = SkillSyncConditionMotion.Begin(piece); }
        s.elapsed += delta;
        if (piece.kind == SkillSyncConditionMotion.Kind.Move)
        {
            float t = piece.duration > 0 ? Mathf.Clamp01(s.elapsed / piece.duration) : 1;
            SkillSyncConditionMotion.Step(piece, s.start, t * t * (3 - 2 * t));
        }
        if (s.elapsed >= piece.duration) segments.Dequeue();
    }
}
