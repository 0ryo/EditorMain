using System;
using System.Collections.Generic;

[Serializable]
public class ScenarioExport
{
    public int version = 6;
    public const string EndActionId = "$end";
    public string startActionId;
    // Explicit choices after all conditions succeed; never infer execution order from the array.
    public string progression = "AllConditionsThenChooseNext";
    public string projectName = "VRCourseEditor";
    public ScenarioSettingsExport scenarioSettings = new ScenarioSettingsExport();
    public List<RequiredActionExport> requiredActions = new List<RequiredActionExport>();
    public List<PlacementExportObject> objects = new List<PlacementExportObject>();
    public List<ScenarioModelExport> models = new List<ScenarioModelExport>();
}

[Serializable]
public class ScenarioModelExport
{
    public string typeId;
    // Relative to the curriculum JSON. Null means the consuming app must supply this prefab.
    public string uri;
    public bool requiresPreinstalledPrefab;
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
    public string sourceNodeId;
    public List<string> nextActionIds = new List<string>();
    public string id;
    public string name;
    public string body;
    public string supplement;
    public string caution;
    public int durationMinutes;
    public List<ConditionExport> conditions = new List<ConditionExport>();
}

[Serializable]
public class ConditionExport
{
    public string type = "SnapHold";
    public string aObjectId;
    public string bObjectId;
    public float holdSeconds = 1.0f;
    public float distanceMeters = 0.1f;
    public List<ConditionParameterExport> parameters = new List<ConditionParameterExport>();
}

[Serializable]
public class ConditionParameterExport
{
    public string key;
    public float numberValue;
    public string textValue;
    public bool boolValue;
}
