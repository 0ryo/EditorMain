using System;
using System.Collections.Generic;

[Serializable]
public class Curriculum
{
    public int schemaVersion = 2;
    public string projectName = "VRCourseEditor";
    public string mode = "Graph";
    public RuleSet rules = new RuleSet();
    public List<ScenarioNode> nodes = new List<ScenarioNode>();
    public List<ScenarioEdge> edges = new List<ScenarioEdge>();
}

[Serializable]
public class RuleSet
{
    public float proximityDistance = 0.1f;
    public float holdSeconds = 1.0f;
}

[Serializable]
public enum ScenarioNodeType
{
    Start,
    End,
    Step,
    Condition
}

[Serializable]
public enum ScenarioEdgeType
{
    StepFlow,
    ConditionBind
}

[Serializable]
public class ScenarioNode
{
    public string nodeId;
    public ScenarioNodeType nodeType;
    public StepNodeData step = new StepNodeData();
    public ConditionNodeData condition = new ConditionNodeData();
}

[Serializable]
public class ScenarioEdge
{
    public string fromNodeId;
    public string toNodeId;
    public ScenarioEdgeType edgeType;
}

[Serializable]
public class StepNodeData
{
    public string title = "";
}

[Serializable]
public class ConditionNodeData
{
    public string type = "SnapHold";
    public string objectAId;
    public string objectBId;
}

// Legacy model types are retained for migration/fallback parsing only.
[Serializable]
public class StepNode
{
    public string id;
    public string title = "タイトル";
    public string description = "";
    public List<ProximityPair> conditions = new List<ProximityPair>();
    public List<string> nextStepIds = new List<string>();
}

[Serializable]
public class ProximityPair
{
    public string aObjectId;
    public string bObjectId;
}
