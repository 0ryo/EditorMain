using UnityEngine;

public static class ScenarioGraphBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void EnsureScenarioGraphUi()
    {
        if (Object.FindObjectOfType<ScenarioGraphUI>() != null) return;

        var go = new GameObject("ScenarioGraphUI");
        go.AddComponent<ScenarioGraphUI>();
    }
}
