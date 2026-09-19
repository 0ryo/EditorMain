using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Display-only lights: never registered as PlacedObject or written to curriculum data.
[DisallowMultipleComponent, DefaultExecutionOrder(1000)]
public sealed class ViewportLightingController : MonoBehaviour
{
    public const string RendererName = "SkillSyncViewportRenderer";
    Camera viewCamera;
    UniversalAdditionalCameraData cameraData;
    UniversalRenderPipelineAsset pipeline;
    int previousRendererIndex;
    bool configured;
    bool warned;
    GameObject lightRoot;
    Light key;
    Light fill;
    Light previousSun;
    AmbientMode previousAmbientMode;
    Color previousSky, previousEquator, previousGround;
    readonly List<Light> suppressedLights = new();
    Cubemap studioReflection;
    Texture previousReflection;
    DefaultReflectionMode previousReflectionMode;
    float previousReflectionIntensity;

    public static void Ensure(Camera camera)
    {
        if (camera == null) return;
        if (camera.GetComponent<ViewportLightingController>() == null)
            camera.gameObject.AddComponent<ViewportLightingController>();
    }

    void OnEnable()
    {
        viewCamera = GetComponent<Camera>();
        Configure();
    }

    void Configure()
    {
        if (configured || viewCamera == null) return;
        pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        if (pipeline == null) return;
        int index = -1;
        for (int i = 0; i < pipeline.rendererDataList.Length; i++)
            if (pipeline.rendererDataList[i] is UniversalRendererData data && data.name == RendererName)
            { index = i; break; }
        if (index < 0)
        {
            if (!warned)
                Debug.LogWarning("[Viewport] 3D照明設定が未適用です。Playを止めて Tools > Automation > Apply Viewport Improvements を実行してください。");
            warned = true;
            return;
        }
        cameraData = viewCamera.GetUniversalAdditionalCameraData();
        // The current project uses a single base camera. Do not silently break a future stack.
        var previousRenderer = cameraData.scriptableRenderer;
        if (cameraData.renderType != CameraRenderType.Base ||
            (previousRenderer != null && previousRenderer.SupportsCameraStackingType(CameraRenderType.Base) &&
             cameraData.cameraStack != null && cameraData.cameraStack.Count > 0))
        {
            if (!warned) Debug.LogWarning("[Viewport] カメラスタックがあるため照明設定を適用していません。");
            warned = true;
            return;
        }
        previousRendererIndex = -1;
        for (int i = 0; i < pipeline.rendererDataList.Length; i++)
            if (pipeline.rendererDataList[i] != null && pipeline.GetRenderer(i) == previousRenderer)
            { previousRendererIndex = i; break; }
        cameraData.SetRenderer(index);

        previousSun = RenderSettings.sun;
        previousAmbientMode = RenderSettings.ambientMode;
        previousSky = RenderSettings.ambientSkyColor;
        previousEquator = RenderSettings.ambientEquatorColor;
        previousGround = RenderSettings.ambientGroundColor;
        foreach (var light in FindObjectsByType<Light>(FindObjectsSortMode.None))
        {
            if (light.gameObject.scene != gameObject.scene || !light.enabled ||
                light.type != LightType.Directional || light.GetComponentInParent<PlacedObject>() != null) continue;
            suppressedLights.Add(light);
            light.enabled = false;
        }
        lightRoot = new GameObject("ViewportStudioLights") { hideFlags = HideFlags.DontSave };
        lightRoot.transform.SetParent(transform, false);
        key = CreateLight("Key", 1.05f, new Color(1f, 0.97f, 0.93f), LightShadows.Hard);
        fill = CreateLight("Fill", 0.23f, new Color(0.86f, 0.92f, 1f), LightShadows.None);
        RenderSettings.sun = key;
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.32f, 0.34f, 0.38f);
        RenderSettings.ambientEquatorColor = new Color(0.22f, 0.23f, 0.25f);
        RenderSettings.ambientGroundColor = new Color(0.14f, 0.15f, 0.18f);
        // Ambient trilight lights diffuse surfaces, but does not provide metallic reflections.
        // Respect scenes which already have a skybox or a custom environment.
        if (RenderSettings.skybox == null && RenderSettings.customReflectionTexture == null)
        {
            previousReflection = RenderSettings.customReflectionTexture;
            previousReflectionMode = RenderSettings.defaultReflectionMode;
            previousReflectionIntensity = RenderSettings.reflectionIntensity;
            studioReflection = ViewportReflectionEnvironment.Create();
            RenderSettings.customReflectionTexture = studioReflection;
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
            RenderSettings.reflectionIntensity = 1;
        }
        configured = true;
        UpdateDirections();
    }

    Light CreateLight(string name, float intensity, Color color, LightShadows shadows)
    {
        var go = new GameObject(name) { hideFlags = HideFlags.DontSave };
        go.transform.SetParent(lightRoot.transform, false);
        var light = go.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = intensity;
        light.color = color;
        light.shadows = shadows;
        light.shadowBias = 0.03f;
        light.shadowNormalBias = 0.15f;
        light.cullingMask = viewCamera.cullingMask & ~(1 << 5); // uGUI is unlit and excluded.
        return light;
    }

    void LateUpdate()
    {
        if (pipeline != GraphicsSettings.currentRenderPipeline)
        {
            Restore();
            Configure();
        }
        if (configured) UpdateDirections();
    }

    void UpdateDirections()
    {
        if (key == null || fill == null || viewCamera == null) return;
        key.transform.rotation = viewCamera.transform.rotation * Quaternion.Euler(35f, -35f, 0f);
        fill.transform.rotation = viewCamera.transform.rotation * Quaternion.Euler(-15f, 50f, 0f);
    }

    void OnDisable() => Restore();

    void Restore()
    {
        if (!configured) return;
        if (cameraData != null)
            cameraData.SetRenderer(pipeline == GraphicsSettings.currentRenderPipeline ? previousRendererIndex : -1);
        foreach (var light in suppressedLights) if (light != null) light.enabled = true;
        suppressedLights.Clear();
        RenderSettings.sun = previousSun;
        RenderSettings.ambientMode = previousAmbientMode;
        RenderSettings.ambientSkyColor = previousSky;
        RenderSettings.ambientEquatorColor = previousEquator;
        RenderSettings.ambientGroundColor = previousGround;
        if (lightRoot != null)
        {
            lightRoot.SetActive(false);
            Destroy(lightRoot);
        }
        if (studioReflection != null)
        {
            if (RenderSettings.customReflectionTexture == studioReflection)
            {
                RenderSettings.customReflectionTexture = previousReflection;
                RenderSettings.defaultReflectionMode = previousReflectionMode;
                RenderSettings.reflectionIntensity = previousReflectionIntensity;
            }
            Destroy(studioReflection); studioReflection = null;
        }
        configured = false;
    }
}
