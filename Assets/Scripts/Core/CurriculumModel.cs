using System;
using System.Collections.Generic;

[Serializable]
public class Curriculum
{
    public int schemaVersion = 1;
    public string projectName = "VRCourseEditor";
    public string mode = "Graph";
    public RuleSet rules = new RuleSet();
    public List<StepNode> steps = new List<StepNode>();
}

[Serializable]
public class RuleSet
{
    public float proximityDistance = 0.5f;
    public float holdSeconds = 1.0f;
}

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
