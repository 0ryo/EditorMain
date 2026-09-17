using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public struct ScenarioObjectState
{
    public Vector3 position;
    public Quaternion rotation;
    public bool held;
}

// The VR adapter supplies world-space poses (metres) and grab state, without XR package dependencies.
public interface IScenarioObjectStateSource
{
    bool TryGetState(string objectId, out ScenarioObjectState state);
}

// Optional adapter contract for deliberate operations, not inferred proximity.
// Counters increase once per push/pull. Rotation is signed, continuous, unwrapped
// degrees around the object's operation axis, excluding motion of its parent.
public struct ScenarioInteractionState
{
    public long pushCount;
    public long pullCount;
    public double rotationDegrees;
    public int epoch; // Change whenever the adapter resets its counters.
}

public interface IScenarioInteractionStateSource : IScenarioObjectStateSource
{
    bool TryGetInteractionState(string objectId, out ScenarioInteractionState state);
}

public sealed class ScenarioConditionEvaluator
{
    float heldSeconds;
    bool wasGrabbed;
    bool interactionStarted;
    ScenarioInteractionState interactionStart;
    public bool Succeeded { get; private set; }

    public void Begin(ConditionExport condition, IScenarioObjectStateSource source)
    {
        interactionStarted = condition != null &&
            (condition.type == ConditionTypeCatalog.Push || condition.type == ConditionTypeCatalog.Pull || condition.type == ConditionTypeCatalog.Turn) &&
            source is IScenarioInteractionStateSource operations && source.TryGetState(condition.aObjectId, out _) &&
            operations.TryGetInteractionState(condition.aObjectId, out interactionStart) &&
            !double.IsNaN(interactionStart.rotationDegrees) && !double.IsInfinity(interactionStart.rotationDegrees);
    }

    public bool Tick(ConditionExport condition, IScenarioObjectStateSource source, float deltaSeconds)
    {
        if (Succeeded) return true;
        if (condition == null || source == null || !source.TryGetState(condition.aObjectId, out var a))
        { heldSeconds = 0f; interactionStarted = false; return false; }
        bool matches;
        switch (condition.type)
        {
            case ConditionTypeCatalog.Push:
            case ConditionTypeCatalog.Pull:
            case ConditionTypeCatalog.Turn:
                if (!(source is IScenarioInteractionStateSource operations) ||
                    !operations.TryGetInteractionState(condition.aObjectId, out var operation))
                { interactionStarted = false; return false; }
                if (double.IsNaN(operation.rotationDegrees) || double.IsInfinity(operation.rotationDegrees))
                { interactionStarted = false; return false; }
                if (!interactionStarted || operation.epoch != interactionStart.epoch ||
                    operation.pushCount < interactionStart.pushCount || operation.pullCount < interactionStart.pullCount)
                { interactionStart = operation; interactionStarted = true; return false; }
                matches = condition.type == ConditionTypeCatalog.Push ? operation.pushCount > interactionStart.pushCount :
                    condition.type == ConditionTypeCatalog.Pull ? operation.pullCount > interactionStart.pullCount :
                    Math.Abs(operation.rotationDegrees - interactionStart.rotationDegrees) >= Number(condition, ConditionTypeCatalog.AngleKey, 90f);
                break;
            case ConditionTypeCatalog.Grabbed:
                matches = a.held;
                break;
            case ConditionTypeCatalog.Released:
                wasGrabbed |= a.held;
                matches = wasGrabbed && !a.held;
                break;
            case ConditionTypeCatalog.Proximity:
            case ConditionTypeCatalog.SnapHold:
            case ConditionTypeCatalog.Separation:
            case ConditionTypeCatalog.RotationMatch:
                if (!source.TryGetState(condition.bObjectId, out var b)) { heldSeconds = 0f; return false; }
                float distance = Vector3.Distance(a.position, b.position);
                matches = condition.type == ConditionTypeCatalog.RotationMatch
                    ? Quaternion.Angle(a.rotation, b.rotation) <= Number(condition, ConditionTypeCatalog.AngleKey, 10f)
                    : condition.type == ConditionTypeCatalog.Separation
                        ? distance >= Number(condition, ConditionTypeCatalog.DistanceKey, condition.distanceMeters)
                        : distance <= Number(condition, ConditionTypeCatalog.DistanceKey, condition.distanceMeters);
                break;
            default: return false;
        }
        if (!matches) { heldSeconds = 0f; return false; }
        float hold = condition.type == ConditionTypeCatalog.SnapHold || condition.type == ConditionTypeCatalog.RotationMatch
            ? Number(condition, ConditionTypeCatalog.HoldSecondsKey, condition.holdSeconds) : 0f;
        if (!float.IsNaN(deltaSeconds) && !float.IsInfinity(deltaSeconds)) heldSeconds += Mathf.Max(0f, deltaSeconds);
        Succeeded = heldSeconds >= hold;
        return Succeeded;
    }

    static float Number(ConditionExport condition, string key, float fallback)
    {
        float value = condition.parameters?.FirstOrDefault(p => p != null && p.key == key)?.numberValue ?? fallback;
        return float.IsNaN(value) || float.IsInfinity(value) ? fallback : Mathf.Max(0f, value);
    }
}

// No failure transition: an unfinished action remains current until every condition succeeds.
public sealed class ScenarioPlaybackSession
{
    readonly Dictionary<string, RequiredActionExport> actions;
    readonly List<ScenarioConditionEvaluator> evaluators = new();
    IScenarioObjectStateSource latestSource;
    public RequiredActionExport Current { get; private set; }
    public bool Finished { get; private set; }
    public bool CanAdvance => !Finished && evaluators.Count > 0 && evaluators.All(e => e.Succeeded);
    public IReadOnlyList<ScenarioConditionEvaluator> Conditions => evaluators;

    public ScenarioPlaybackSession(ScenarioExport scenario, IScenarioObjectStateSource initialSource = null)
    {
        if (scenario == null || (scenario.version != 5 && scenario.version != 6) || scenario.progression != "AllConditionsThenChooseNext")
            throw new ArgumentException("対応していない教材形式です。version 5 / 6 の教材を使用してください。");
        actions = scenario.requiredActions.ToDictionary(action => action.id);
        if (!actions.ContainsKey(scenario.startActionId ?? "") || actions.ContainsKey(ScenarioExport.EndActionId))
            throw new ArgumentException("教材の開始手順が不正です。");
        foreach (var action in actions.Values)
        {
            if (action.conditions == null || action.conditions.Count == 0 || action.conditions.Any(c =>
                    c == null || ConditionTypeCatalog.Find(c.type) == null || string.IsNullOrWhiteSpace(c.aObjectId) ||
                    (ConditionTypeCatalog.RequiresObjectB(c.type) && (string.IsNullOrWhiteSpace(c.bObjectId) || c.aObjectId == c.bObjectId))) ||
                action.nextActionIds == null || action.nextActionIds.Count == 0 ||
                action.nextActionIds.Any(id => id != ScenarioExport.EndActionId && !actions.ContainsKey(id ?? "")))
                throw new ArgumentException("教材の条件または接続先が不正です。");
        }
        latestSource = initialSource;
        Enter(actions[scenario.startActionId]);
    }

    public void Tick(IScenarioObjectStateSource source, float deltaSeconds)
    {
        if (Finished) return;
        latestSource = source;
        for (int i = 0; i < evaluators.Count; i++) evaluators[i].Tick(Current.conditions[i], source, deltaSeconds);
    }

    public bool TryAdvance(string nextActionId = null)
    {
        if (!CanAdvance) return false;
        if (nextActionId == null && Current.nextActionIds.Count == 1) nextActionId = Current.nextActionIds[0];
        if (nextActionId == null || !Current.nextActionIds.Contains(nextActionId)) return false;
        if (nextActionId == ScenarioExport.EndActionId) { Finished = true; return true; }
        Enter(actions[nextActionId]);
        return true;
    }

    void Enter(RequiredActionExport action)
    {
        Current = action;
        evaluators.Clear();
        for (int i = 0; i < action.conditions.Count; i++)
        {
            var evaluator = new ScenarioConditionEvaluator();
            evaluator.Begin(action.conditions[i], latestSource);
            evaluators.Add(evaluator);
        }
    }
}
