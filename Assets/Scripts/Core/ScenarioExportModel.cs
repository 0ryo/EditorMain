using System;
using System.Collections.Generic;

[Serializable]
public class ScenarioExport
{
    public int version = 2;
    public string projectName = "VRCourseEditor";
    public ScenarioSettingsExport scenarioSettings = new ScenarioSettingsExport();
    public List<RequiredActionExport> requiredActions = new List<RequiredActionExport>();
    public List<PlacementExportObject> objects = new List<PlacementExportObject>();
}

[Serializable]
public class ScenarioSettingsExport
{
    public float holdSeconds = 1.0f;
    public float snapDistance_m = 0.1f;
}

[Serializable]
public class RequiredActionExport
{
    public string id;
    public string name;
    public List<ConditionExport> conditions = new List<ConditionExport>();
}

[Serializable]
public class ConditionExport
{
    public string type = "SnapHold";
    public string aObjectId;
    public string bObjectId;
    public float holdSeconds = 1.0f;
}
