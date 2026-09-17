using TMPro;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

// Shared shape outlines for candidate previews and editor selection. Does not mutate model data.
public sealed class ObjectCandidatePreview : MonoBehaviour
{
    PlacedObject target;
    Renderer[] renderers;
    Canvas outlineCanvas;
    Canvas labelCanvas;
    RawImage outline;
    TMP_Text label;
    RenderTexture mask;
    RenderTexture selectionTexture;
    bool selectionMode;
    readonly List<PlacedObject> selection = new();
    readonly List<Renderer[]> selectionRenderers = new();
    Material maskMaterial;
    Material outlineMaterial;
    CommandBuffer commands;
    readonly Dictionary<SkinnedMeshRenderer, Mesh> bakedMeshes = new();
    MaterialPropertyBlock properties;
    float clearedAt;
    string measuredLabel;
    float measuredWidth = -1;

    void Awake() { properties = new MaterialPropertyBlock(); }

    void OnEnable()
    {
        RenderPipelineManager.beginCameraRendering += BeforeCamera;
        Camera.onPreRender += BeforeBuiltinCamera;
    }

    void BeforeCamera(ScriptableRenderContext context, Camera camera) { RenderOutline(camera); }
    void BeforeBuiltinCamera(Camera camera) { if (GraphicsSettings.currentRenderPipeline == null) RenderOutline(camera); }

    public void Show(PlacedObject candidate, string fullName, TMP_FontAsset font)
    {
        selectionMode = false;
        if (!outlineCanvas) Build(font);
        outline.material = outlineMaterial;
        outlineCanvas.sortingOrder = -30000;
        if (outlineMaterial) outlineMaterial.SetColor("_OutlineColor", new Color(1, 0.48f, 0.04f, 1));
        if (mask) outline.texture = mask;
        if (target != candidate)
        {
            target = candidate;
            renderers = target ? target.GetComponentsInChildren<Renderer>(true) : null;
        }
        label.text = fullName;
        labelCanvas.gameObject.SetActive(true);
        outlineCanvas.gameObject.SetActive(target != null);
    }

    // One shared pair of textures for the entire selection, not one per object.
    public void ShowSelection(IReadOnlyList<PlacedObject> selected)
    {
        if (selected.Count == 0) { Clear(); return; }
        if (!outlineCanvas) Build(TMP_Settings.defaultFontAsset);
        bool changed = !selectionMode || selection.Count != selected.Count;
        for (int i = 0; !changed && i < selected.Count; i++) changed = selection[i] != selected[i];
        selectionMode = true;
        if (changed)
        {
            selection.Clear();
            selectionRenderers.Clear();
            foreach (var item in selected)
            {
                selection.Add(item);
                selectionRenderers.Add(item ? item.GetComponentsInChildren<Renderer>(true) : System.Array.Empty<Renderer>());
            }
        }
        target = selected[0];
        renderers = selectionRenderers[0];
        outlineCanvas.sortingOrder = -30001; // Orange candidate preview stays above the blue selection.
        outlineCanvas.gameObject.SetActive(true);
        labelCanvas.gameObject.SetActive(false);
        outline.material = null;
        if (outlineMaterial) outlineMaterial.SetColor("_OutlineColor", DesignTokens.Accent);
    }

    public void Clear()
    {
        if (target) clearedAt = Time.unscaledTime;
        target = null;
        renderers = null;
        selection.Clear();
        selectionRenderers.Clear();
        if (outlineCanvas) outlineCanvas.gameObject.SetActive(false);
        if (labelCanvas) labelCanvas.gameObject.SetActive(false);
    }

    void Build(TMP_FontAsset font)
    {
        outlineCanvas = MakeCanvas("Candidate outline", -30000);
        outline = new GameObject("Outline", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
        outline.transform.SetParent(outlineCanvas.transform, false);
        outline.raycastTarget = false;
        var maskShader = Resources.Load<Shader>("ObjectHoverMask");
        var borderShader = Resources.Load<Shader>("ObjectHoverOutline");
        if (maskShader && borderShader)
        {
            maskMaterial = new Material(maskShader) { hideFlags = HideFlags.HideAndDontSave };
            outlineMaterial = new Material(borderShader) { hideFlags = HideFlags.HideAndDontSave };
            outline.material = outlineMaterial;
            commands = new CommandBuffer { name = "Object candidate silhouette" };
        }
        labelCanvas = MakeCanvas("Candidate full name", 32000);
        var panel = new GameObject("Full name", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(labelCanvas.transform, false);
        panel.GetComponent<Image>().color = new Color(0.08f, 0.08f, 0.08f, 0.97f);
        panel.GetComponent<Image>().raycastTarget = false;
        var rect = (RectTransform)panel.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0, -12);
        label = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TMP_Text>();
        label.transform.SetParent(panel.transform, false);
        label.font = font;
        label.fontSize = 18;
        label.richText = false;
        label.color = Color.white;
        label.enableWordWrapping = true;
        label.raycastTarget = false;
        label.rectTransform.anchorMin = Vector2.zero;
        label.rectTransform.anchorMax = Vector2.one;
        label.rectTransform.offsetMin = new Vector2(12, 8);
        label.rectTransform.offsetMax = new Vector2(-12, -8);
    }

    static Canvas MakeCanvas(string name, int order)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Canvas));
        go.hideFlags = HideFlags.DontSave;
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = order;
        return canvas;
    }

    void LateUpdate()
    {
        if (labelCanvas && labelCanvas.gameObject.activeSelf)
        {
            float width = Mathf.Min(760, Screen.width - 24);
            if (measuredLabel != label.text || measuredWidth != width)
            {
                measuredLabel = label.text;
                measuredWidth = width;
                ((RectTransform)label.transform.parent).sizeDelta = new Vector2(width,
                    label.GetPreferredValues(label.text, width - 24, 0).y + 16);
            }
        }
        // Reuse the texture while the pointer crosses adjacent rows during scrolling.
        if (!target && Time.unscaledTime - clearedAt > 0.5f) ReleaseMask();
    }

    void RenderOutline(Camera camera)
    {
        if (!target || !target.gameObject.activeInHierarchy || !camera || commands == null || properties == null || renderers == null)
        { if (outline) outline.enabled = false; return; }
        if (camera != EditWorkspace.ResolveCamera()) return;
        outline.enabled = true;
        int widthPixels = Mathf.Max(1, camera.pixelWidth), heightPixels = Mathf.Max(1, camera.pixelHeight);
        if (!mask || mask.width != widthPixels || mask.height != heightPixels)
        {
            if (mask) { mask.Release(); Destroy(mask); }
            mask = new RenderTexture(widthPixels, heightPixels, 0, RenderTextureFormat.ARGB32)
            { name = "Candidate mask", hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp };
            mask.Create();
            outline.texture = mask;
        }
        outline.rectTransform.anchorMin = camera.rect.min;
        outline.rectTransform.anchorMax = camera.rect.max;
        outline.rectTransform.offsetMin = outline.rectTransform.offsetMax = Vector2.zero;
        commands.Clear();
        var viewProjection = GL.GetGPUProjectionMatrix(camera.projectionMatrix, true) * camera.worldToCameraMatrix;
        if (selectionMode)
        {
            if (!selectionTexture || selectionTexture.width != widthPixels || selectionTexture.height != heightPixels)
            {
                if (selectionTexture) { selectionTexture.Release(); Destroy(selectionTexture); }
                selectionTexture = new RenderTexture(widthPixels, heightPixels, 0, RenderTextureFormat.ARGB32)
                { name = "Selection silhouettes", hideFlags = HideFlags.HideAndDontSave, wrapMode = TextureWrapMode.Clamp };
                selectionTexture.Create();
            }
            outline.texture = selectionTexture;
            commands.SetRenderTarget(selectionTexture);
            commands.ClearRenderTarget(false, true, Color.clear);
            for (int i = 0; i < selection.Count; i++)
            {
                if (!SelectionService.CanEdit(selection[i])) continue;
                DrawMask(selectionRenderers[i], viewProjection, widthPixels, heightPixels);
                // Composite each silhouette separately, so another selected object cannot hide it.
                commands.Blit(mask, selectionTexture, outlineMaterial);
            }
        }
        else DrawMask(renderers, viewProjection, widthPixels, heightPixels);
        var previousTarget = RenderTexture.active;
        try { Graphics.ExecuteCommandBuffer(commands); }
        finally { RenderTexture.active = previousTarget; }
    }

    void DrawMask(Renderer[] targets, Matrix4x4 viewProjection, int widthPixels, int heightPixels)
    {
        commands.SetRenderTarget(mask);
        commands.SetViewport(new Rect(0, 0, widthPixels, heightPixels));
        commands.ClearRenderTarget(false, true, Color.clear);
        foreach (var renderer in targets)
        {
            if (!renderer || !renderer.gameObject.activeInHierarchy) continue;
            if (selectionMode && !renderer.enabled) continue;
            Mesh mesh = null;
            if (renderer is SkinnedMeshRenderer skin)
            {
                if (!bakedMeshes.TryGetValue(skin, out mesh))
                {
                    mesh = new Mesh { hideFlags = HideFlags.HideAndDontSave };
                    bakedMeshes.Add(skin, mesh);
                }
                skin.BakeMesh(mesh);
            }
            else if (renderer is MeshRenderer)
            {
                var filter = renderer.GetComponent<MeshFilter>();
                if (filter) mesh = filter.sharedMesh;
            }
            if (!mesh) continue;
            // Explicit matrices avoid SRP per-draw camera/object globals from another draw.
            properties.SetMatrix("_HoverMVP", viewProjection * renderer.localToWorldMatrix);
            for (int submesh = 0; submesh < mesh.subMeshCount; submesh++)
                commands.DrawMesh(mesh, Matrix4x4.identity, maskMaterial, submesh, 0, properties);
        }
    }

    void ReleaseMask()
    {
        if (outline) outline.texture = null;
        if (mask) { mask.Release(); Destroy(mask); mask = null; }
        if (selectionTexture) { selectionTexture.Release(); Destroy(selectionTexture); selectionTexture = null; }
    }

    void OnDisable()
    {
        RenderPipelineManager.beginCameraRendering -= BeforeCamera;
        Camera.onPreRender -= BeforeBuiltinCamera;
        Clear(); ReleaseMask();
    }
    void OnDestroy()
    {
        if (outlineCanvas) Destroy(outlineCanvas.gameObject);
        if (labelCanvas) Destroy(labelCanvas.gameObject);
        ReleaseMask();
        if (maskMaterial) Destroy(maskMaterial);
        if (outlineMaterial) Destroy(outlineMaterial);
        commands?.Release();
        foreach (var mesh in bakedMeshes.Values) if (mesh) Destroy(mesh);
    }
}
