using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class AuthoringFeatureChecks
{
    static int assertions;

    [MenuItem("Tools/Automation/Check Authoring Logic")]
    public static void Run()
    {
        assertions = 0;
        CheckFlow();
        CheckConditions();
        CheckOperations();
        CheckPlayback();
        Debug.Log($"[Authoring] {assertions} logic checks passed.");
    }

    static void CheckFlow()
    {
        var graph = new Curriculum();
        foreach (string id in new[] { "start", "a", "b", "c", "merge", "end" })
            graph.nodes.Add(new ScenarioNode { nodeId = id, nodeType = id == "start" ? ScenarioNodeType.Start :
                id == "end" ? ScenarioNodeType.End : ScenarioNodeType.Step });
        void Link(string a, string b) => graph.edges.Add(new ScenarioEdge { fromNodeId = a, toNodeId = b });
        Link("start", "a"); Link("a", "b"); Link("a", "c"); Link("b", "merge"); Link("c", "merge"); Link("merge", "end");
        Check(ScenarioFlow.TryOrder(graph, out var ordered, out _) && ordered.Count == 4, "Branch and merge are valid");
        Check(ordered[0].nodeId == "a" && ordered[3].nodeId == "merge", "Merge is ordered after both incoming routes");
        Check(ScenarioFlow.Next(graph, "a").Count == 2, "Both choices are retained");
        Link("merge", "a");
        Check(!ScenarioFlow.TryOrder(graph, out _, out _), "Cycle is rejected");
        graph.edges.RemoveAt(graph.edges.Count - 1);
        graph.edges.RemoveAt(graph.edges.Count - 1);
        Check(!ScenarioFlow.TryOrder(graph, out _, out _), "Dead end is rejected");
        Link("merge", "end");
        graph.nodes.Add(new ScenarioNode { nodeId = "isolated", nodeType = ScenarioNodeType.Step });
        Check(!ScenarioFlow.TryOrder(graph, out _, out _), "Disconnected step is rejected");
        graph.nodes.RemoveAt(graph.nodes.Count - 1);
        Link("a", "b");
        Check(!ScenarioFlow.TryOrder(graph, out _, out _), "Duplicate edge is rejected");
    }

    static void CheckConditions()
    {
        var source = new States();
        source.values["a"] = new ScenarioObjectState { rotation = Quaternion.identity };
        source.values["b"] = new ScenarioObjectState { position = new Vector3(0.05f, 0f, 0f), rotation = Quaternion.identity };
        var hold = Condition(ConditionTypeCatalog.SnapHold);
        var evaluator = new ScenarioConditionEvaluator();
        Check(!evaluator.Tick(hold, source, 0.5f), "Hold does not succeed early");
        source.values["b"] = new ScenarioObjectState { position = new Vector3(2f, 0f, 0f), rotation = Quaternion.identity };
        Check(!evaluator.Tick(hold, source, 2f), "Leaving range resets hold");
        source.values["b"] = new ScenarioObjectState { rotation = Quaternion.identity };
        Check(!evaluator.Tick(hold, source, 0.5f), "Hold restarts from zero");
        Check(evaluator.Tick(hold, source, 0.5f), "Continuous hold succeeds");
        Check(new ScenarioConditionEvaluator().Tick(Condition(ConditionTypeCatalog.Proximity), source, 0f), "Proximity succeeds immediately");
        Check(!new ScenarioConditionEvaluator().Tick(Condition(ConditionTypeCatalog.Separation), source, 1f), "Separation rejects close objects");
        source.values["b"] = new ScenarioObjectState { position = Vector3.one, rotation = Quaternion.identity };
        Check(new ScenarioConditionEvaluator().Tick(Condition(ConditionTypeCatalog.Separation), source, 0f), "Separation succeeds beyond distance");
        Check(new ScenarioConditionEvaluator().Tick(Condition(ConditionTypeCatalog.RotationMatch), source, 1f), "Matching rotation succeeds");
        source.values["b"] = new ScenarioObjectState { rotation = Quaternion.Euler(0f, 90f, 0f) };
        Check(!new ScenarioConditionEvaluator().Tick(Condition(ConditionTypeCatalog.RotationMatch), source, 2f), "Wrong rotation waits");
        var released = new ScenarioConditionEvaluator();
        Check(!released.Tick(Condition(ConditionTypeCatalog.Released), source, 0f), "Initially unheld is not a release");
        source.values["a"] = new ScenarioObjectState { rotation = Quaternion.identity, held = true };
        Check(!released.Tick(Condition(ConditionTypeCatalog.Released), source, 0f), "Grab precedes release");
        Check(new ScenarioConditionEvaluator().Tick(Condition(ConditionTypeCatalog.Grabbed), source, 0f), "Grab uses one object");
        source.values["a"] = new ScenarioObjectState { rotation = Quaternion.identity };
        Check(released.Tick(Condition(ConditionTypeCatalog.Released), source, 0f), "Release after grab succeeds");
        Check(!ConditionTypeCatalog.RequiresObjectB(ConditionTypeCatalog.Grabbed), "Grab hides unused B");
        Check(ConditionTypeCatalog.RequiresObjectB(ConditionTypeCatalog.RotationMatch), "Rotation requires reference B");
        source.values.Remove("b");
        Check(!new ScenarioConditionEvaluator().Tick(hold, source, 5f), "Missing target cannot succeed");
        Check(!new ScenarioConditionEvaluator().Tick(Condition("Unknown"), source, 5f), "Unknown type cannot succeed");
    }

    static void CheckOperations()
    {
        var types = ConditionTypeCatalog.Definitions;
        Check(types.Count == 6 && types[2].id == ConditionTypeCatalog.Push && types[3].id == ConditionTypeCatalog.Pull &&
            types[4].label == "もつ" && types[5].id == ConditionTypeCatalog.Turn, "Six authoring choices");
        Check(ConditionTypeCatalog.Find(ConditionTypeCatalog.Released) != null, "Legacy conditions remain readable");
        var data = new ConditionNodeData { type = ConditionTypeCatalog.Turn };
        ConditionTypeCatalog.Normalize(data);
        Check(ConditionTypeCatalog.GetNumber(data, ConditionTypeCatalog.AngleKey) == 90, "Turn defaults to 90 degrees");
        ConditionTypeCatalog.SetNumber(data, ConditionTypeCatalog.AngleKey, 450);
        var restored = JsonUtility.FromJson<ConditionNodeData>(JsonUtility.ToJson(data));
        Check(ConditionTypeCatalog.GetNumber(restored, ConditionTypeCatalog.AngleKey) == 450, "Multi-turn angle survives JSON");
        var source = new Operations();
        var push = new ScenarioConditionEvaluator();
        var pull = new ScenarioConditionEvaluator();
        Check(!push.Tick(Condition(ConditionTypeCatalog.Push), source, 0), "Existing push count is only a baseline");
        Check(!pull.Tick(Condition(ConditionTypeCatalog.Pull), source, 0), "Existing pull count is only a baseline");
        source.state.pushCount++;
        Check(push.Tick(Condition(ConditionTypeCatalog.Push), source, 0), "New push succeeds");
        Check(!pull.Tick(Condition(ConditionTypeCatalog.Pull), source, 0), "Push does not satisfy pull");
        source.state.pullCount++;
        Check(pull.Tick(Condition(ConditionTypeCatalog.Pull), source, 0), "New pull succeeds");
        var turn = new ScenarioConditionEvaluator();
        var condition = Condition(ConditionTypeCatalog.Turn);
        turn.Begin(condition, source);
        source.state.rotationDegrees = 89;
        Check(!turn.Tick(condition, source, 0), "Turn waits below threshold");
        source.state.rotationDegrees = -20;
        Check(!turn.Tick(condition, source, 0), "Reversing does not add absolute travel");
        source.state.rotationDegrees = -90;
        Check(turn.Tick(condition, source, 0), "Negative turn reaches requested magnitude");
        turn = new ScenarioConditionEvaluator();
        turn.Begin(condition, source);
        source.state.epoch++;
        source.state.rotationDegrees = 0;
        Check(!turn.Tick(condition, source, 0), "Adapter reset does not complete a turn");
        source.available = false;
        Check(!turn.Tick(condition, source, 0), "Missing object cannot complete");
        source.available = true;
        source.state.rotationDegrees = 180;
        Check(!turn.Tick(condition, source, 0), "Recovered object gets a fresh baseline");
        var export = new ScenarioExport { startActionId = "first" };
        export.requiredActions.Add(new RequiredActionExport { id = "first", nextActionIds = new List<string> { "second" },
            conditions = new List<ConditionExport> { Condition(ConditionTypeCatalog.Push) } });
        export.requiredActions.Add(new RequiredActionExport { id = "second", nextActionIds = new List<string> { ScenarioExport.EndActionId },
            conditions = new List<ConditionExport> { Condition(ConditionTypeCatalog.Push) } });
        var session = new ScenarioPlaybackSession(export, source);
        source.state.pushCount++;
        session.Tick(source, 0);
        Check(session.TryAdvance(), "First operation after session creation is counted");
        session.Tick(source, 0);
        Check(!session.CanAdvance, "Previous step's operation is not reused");
        source.state.pushCount++;
        session.Tick(source, 0);
        Check(session.CanAdvance, "Next step counts its first new operation");
    }

    sealed class Operations : IScenarioInteractionStateSource
    {
        public ScenarioInteractionState state;
        public bool available = true;
        public bool TryGetState(string id, out ScenarioObjectState value) { value = default; return available && id == "a"; }
        public bool TryGetInteractionState(string id, out ScenarioInteractionState value) { value = state; return available && id == "a"; }
    }

    static void CheckPlayback()
    {
        var export = new ScenarioExport { startActionId = "a" };
        export.requiredActions.Add(new RequiredActionExport { id = "a", nextActionIds = new List<string> { "b", "c" },
            conditions = new List<ConditionExport> { Condition(ConditionTypeCatalog.Grabbed) } });
        foreach (string id in new[] { "b", "c" })
            export.requiredActions.Add(new RequiredActionExport { id = id, nextActionIds = new List<string> { ScenarioExport.EndActionId },
                conditions = new List<ConditionExport> { Condition(ConditionTypeCatalog.Grabbed) } });
        var session = new ScenarioPlaybackSession(export);
        Check(!session.TryAdvance("b"), "Incomplete step cannot advance");
        var source = new States();
        source.values["a"] = new ScenarioObjectState { held = true, rotation = Quaternion.identity };
        session.Tick(source, 0f);
        Check(session.CanAdvance, "All conditions unlock progression");
        Check(!session.TryAdvance(), "Branch requires an explicit choice");
        Check(!session.TryAdvance("missing"), "Unconnected choice is rejected");
        Check(session.TryAdvance("c") && session.Current.id == "c", "Chosen branch is entered");
        Check(!session.CanAdvance && !session.TryAdvance(), "New step resets success");
        session.Tick(source, 0f);
        Check(session.TryAdvance() && session.Finished, "Only chosen route is needed to finish");
        Check(!session.TryAdvance(), "Completed session cannot advance again");
    }

    static ConditionExport Condition(string type) => new ConditionExport
    { type = type, aObjectId = "a", bObjectId = ConditionTypeCatalog.RequiresObjectB(type) ? "b" : null, distanceMeters = 0.1f, holdSeconds = 1f };

    static void Check(bool result, string label)
    {
        if (!result) throw new InvalidOperationException("[Authoring check] " + label);
        assertions++;
    }

    sealed class States : IScenarioObjectStateSource
    {
        public readonly Dictionary<string, ScenarioObjectState> values = new();
        public bool TryGetState(string id, out ScenarioObjectState state) => values.TryGetValue(id ?? "", out state);
    }
}
