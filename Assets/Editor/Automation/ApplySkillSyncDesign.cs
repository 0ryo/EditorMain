using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

// Reads reduced source measurements; updates only the owned child of the existing prefab.
public static class ApplySkillSyncDesign
{
    const string AssetsPath = "Assets/UI/SkillSyncDesign";
    const string PrefabPath = "Assets/UI/Prefabs/UIRoot.prefab";
    const string RootName = "SkillSyncDesign";
    [Serializable] class Document { public V[] visuals; public B[] buttons; public F[] fields; }
    [Serializable] class FixtureDocument { public V[] visuals; public SkillSyncReferencePreview.SampleField[] fields; }
    [Serializable] class SymbolDocument { public Symbol[] glyphs; }
    [Serializable] class Symbol { public string character,sprite; public float advance,width,height,offsetX,offsetY; }
    static Symbol[] symbols;
    [Serializable] class V
    {
        public float x,y,w,h,alpha,size,baseline,lineHeight,strokeWidth;
        public int weight,mask,order;
        public string kind,text,color,sprite,role,region;
        public string[] sources;
        public int[] orders;
    }
    [Serializable] class B { public float x,y,w,h; public int mask; public string action; public string[] sources; }
    [Serializable] class F { public float x,y,w,h,size,baseline; public int mask,weight; public string role; public string[] sources; }
    static readonly Dictionary<int, TMP_FontAsset> fonts = new();

    [MenuItem("Tools/Automation/SkillSync/Apply Viewport Refinements")]
    public static void ApplyViewportRefinements()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
        var root=PrefabUtility.LoadPrefabContents(PrefabPath);
        try {
            var view=root.GetComponentInChildren<SkillSyncDesignView>(true);
            if(view==null) throw new InvalidOperationException("Apply SkillSync Figma Design first.");
            SkillSyncDesignLayout.Apply(view);SkillSyncPdfRefinement.Apply(view);SkillSyncWorkspaceRefinement.Apply(view);
            view.EnsureProjectLoadControl();view.EnsureConditionControls();view.EnsureViewportLabels();
            TopCenterNotification.Ensure(view.transform, view.inspectorTitle);
            var legacyHints = root.transform.Find("Button_Hints");
            if (legacyHints != null) legacyHints.gameObject.SetActive(false);
            view.Show(0);
            PrefabUtility.SaveAsPrefabAsset(root,PrefabPath,out bool saved);
            if(!saved) throw new InvalidOperationException("UIRoot save failed");
        } finally {PrefabUtility.UnloadPrefabContents(root);}
        PlayerSettings.defaultScreenWidth=2560;PlayerSettings.defaultScreenHeight=1440;
        AssetDatabase.SaveAssets();
    }

    [MenuItem("Tools/Automation/SkillSync/Create Isolated Reference Scene")]
    public static void CreateReferenceScene()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
        PrepareFonts(JsonUtility.FromJson<Document>(File.ReadAllText(AssetsPath+"/handoff.json")));
        PrepareSprites();
        var doc=JsonUtility.FromJson<FixtureDocument>(File.ReadAllText("Assets/Editor/SkillSyncDesign/reference-fixtures.json"));
        ValidateSprites(doc.visuals);
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if(prefab==null || prefab.GetComponentInChildren<SkillSyncDesignView>(true)==null)
            throw new InvalidOperationException("Apply SkillSync Figma Design first.");
        var scene=UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.EmptyScene,UnityEditor.SceneManagement.NewSceneMode.Additive);
        var root=(GameObject)PrefabUtility.InstantiatePrefab(prefab,scene);
        PrefabUtility.UnpackPrefabInstance(root,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
        foreach(Transform child in root.transform) if(child.name!=RootName) child.gameObject.SetActive(false);
        var view=root.GetComponentInChildren<SkillSyncDesignView>(true);
        UnityEngine.Object.DestroyImmediate(view.GetComponent<SkillSyncEditorController>());
        var fixture=view.gameObject.AddComponent<SkillSyncReferencePreview>();fixture.view=view;
        fixture.fields=doc.fields;
        var host=Rect("ReferenceLists_EditorOnly",view.transform,0,0,1600,1000);
        var generated=new List<SkillSyncDesignView.Visual>();
        foreach(var v in doc.visuals.OrderBy(v=>v.order))
        {
            // The reference list follows the revised PDF geometry, without inserting duplicate sample assets.
            if((v.mask & 9)!=0 && v.x<264 && v.y>=275 && v.y<736) {
                float origin=v.y<429?275:v.y<583?445:598;
                float destination=origin==275?223.34f:origin==445?400.33f:559.62f;
                float scale=origin==275?160.33f/154:142.63f/138;
                v.y=destination+(v.y-origin)*scale;v.h*=scale;
            }
            var rect=Rect(v.sources[0],host,v.x,v.y,v.w,v.h);TMP_Text label=null;
            if(v.kind=="TEXT")
            {
                label=Label(rect,v.text,v.size,v.weight,ColorOf(v.color));label.alignment=TextAlignmentOptions.BaselineLeft;
                rect.anchoredPosition=new Vector2(v.x,-v.y-v.baseline+v.h/2);
                foreach(RectTransform child in rect) child.anchoredPosition+=Vector2.up*(v.baseline-v.h/2);
            }
            else if(v.kind=="LINE") DrawSourceLine(rect,v);
            else
            {
                var image=rect.gameObject.AddComponent<UnityEngine.UI.Image>();image.sprite=AssetDatabase.LoadAssetAtPath<Sprite>(AssetsPath+"/Sprites/"+v.sprite);
                image.color=new Color(1,1,1,v.alpha);image.raycastTarget=false;
            }
            generated.Add(new SkillSyncDesignView.Visual {target=rect.gameObject,label=label,mask=v.mask,sourceNodeIds=v.sources,orders=v.orders});
        }
        SkillSyncDesignLayout.AdaptReferenceHost(host);
        fixture.fixtureVisuals=generated.ToArray();view.modalBlocker.transform.SetAsLastSibling();view.modal.transform.SetAsLastSibling();
        var stage=new GameObject("Reference3D_Approximation_EditorOnly");UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(stage,scene);
        var cameraObject=new GameObject("Reference Camera",typeof(Camera));cameraObject.transform.SetParent(stage.transform);
        var camera=cameraObject.GetComponent<Camera>();camera.backgroundColor=ColorOf("#EEF2F7");camera.clearFlags=CameraClearFlags.SolidColor;
        camera.transform.position=new Vector3(4,2.953f,-2);camera.transform.LookAt(new Vector3(0,.925f,0));camera.orthographic=true;camera.orthographicSize=.765f;
        var pipeline=UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline as UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset;
        if(pipeline!=null) for(int i=0;i<pipeline.rendererDataList.Length;i++)
            if(pipeline.rendererDataList[i].name==ViewportLightingController.RendererName)
            {cameraObject.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>().SetRenderer(i);break;}
        fixture.sampleCamera=camera;
        if(view.orientation!=null) view.orientation.sourceCamera=camera;
        camera.backgroundColor=EditWorkspace.BackgroundColor;
        var lightObject=new GameObject("Reference Light",typeof(Light));lightObject.transform.SetParent(stage.transform);
        var light=lightObject.GetComponent<Light>();light.type=LightType.Directional;light.intensity=1.3f;light.transform.rotation=Quaternion.Euler(45,-30,0);
        Cube("Worktop",new Vector3(0,.85f,0),new Vector3(2,.1f,1.4f),"#D3DEE9");
        for(int i=-10;i<=10;i++) Cube("GridX",new Vector3(i*.1f,.901f,0),new Vector3(.0015f,.001f,1.4f),"#B9C9DA");
        for(int i=-7;i<=7;i++) Cube("GridZ",new Vector3(0,.901f,i*.1f),new Vector3(2,.001f,.0015f),"#B9C9DA");
        foreach(float x in new[]{-.85f,.85f}) Cube("Leg",new Vector3(x,.6f,Mathf.Sign(x)*.55f),new Vector3(.05f,.5f,.05f),"#A8BBD0");
        fixture.samplePart=Cube("Part A",new Vector3(-.1f,1,0),new Vector3(.3f,.2f,.25f),"#4298F0");
        fixture.partRenderer=fixture.samplePart.GetComponent<Renderer>();fixture.partMaterial=fixture.partRenderer.sharedMaterial;
        const string ghostPath="Assets/Editor/SkillSyncDesign/ReferenceGhost.mat";
        fixture.ghostMaterial=AssetDatabase.LoadAssetAtPath<Material>(ghostPath);
        if(fixture.ghostMaterial==null)
        {
            var material=new Material(fixture.partMaterial);material.SetColor("_BaseColor",new Color(.259f,.596f,.941f,.35f));
            material.SetFloat("_Surface",1);material.SetFloat("_ZWrite",0);material.SetFloat("_SrcBlend",5);material.SetFloat("_DstBlend",10);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");material.renderQueue=3000;
            AssetDatabase.CreateAsset(material,ghostPath);fixture.ghostMaterial=material;
        }
        fixture.sampleMarker=Cube("Marker B",new Vector3(.55f,.9125f,0),new Vector3(.3f,.025f,.24f),"#45BABE");
        camera.aspect=848f/550f;
        Vector3 SourcePoint(float x,float y,float height)
        {
            var ray=camera.ViewportPointToRay(new Vector3((x-288)/848f,1-(y-170)/550f,0));
            new Plane(Vector3.up,new Vector3(0,height,0)).Raycast(ray,out float distance);return ray.GetPoint(distance);
        }
        fixture.initialPartPosition=SourcePoint(558,449,1);
        fixture.trialPartPosition=SourcePoint(783,492,1);
        fixture.sampleMarker.position=SourcePoint(889,509,.9125f);
        Transform Cube(string name,Vector3 position,Vector3 scale,string color)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(stage.transform);go.transform.position=position;go.transform.localScale=scale;
            string path="Assets/Editor/SkillSyncDesign/Reference"+color.TrimStart('#')+".mat";
            var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(material==null) {material=new Material(Shader.Find("Universal Render Pipeline/Lit"));material.SetColor("_BaseColor",ColorOf(color));AssetDatabase.CreateAsset(material,path);}
            go.GetComponent<Renderer>().sharedMaterial=material;return go.transform;
        }
        fixture.ApplyState(0);
        UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene,"Assets/Editor/SkillSyncDesign/ReferencePreview.unity");
        Selection.activeGameObject=fixture.gameObject;
        Debug.Log("[SkillSync] Isolated fixture scene created. Close other scenes before entering Play Mode for capture. No project data was loaded or saved.");
    }

    [MenuItem("Tools/Automation/SkillSync/Capture Six Reference States")]
    public static void CaptureSixStates()
    {
        var fixture=UnityEngine.Object.FindFirstObjectByType<SkillSyncReferencePreview>();
        if(!EditorApplication.isPlaying || fixture==null) throw new InvalidOperationException("Play the isolated reference scene at 2560×1440 first.");
        if(Screen.width!=2560 || Screen.height!=1440) throw new InvalidOperationException("Set Game view to 2560×1440 before capture.");
        fixture.StartCoroutine(Capture(fixture));
    }
    static System.Collections.IEnumerator Capture(SkillSyncReferencePreview fixture)
    {
        Application.runInBackground=true;
        string folder="Design/skillsync_codex_handoff/verification/wide-captures";Directory.CreateDirectory(folder);
        int previous=fixture.state;
        for(int i=0;i<6;i++)
        {
            fixture.ApplyState(i);yield return null;yield return new WaitForEndOfFrame();
            var texture=ScreenCapture.CaptureScreenshotAsTexture();
            File.WriteAllBytes(folder+"/"+(i+1).ToString("00")+"_unity.png",texture.EncodeToPNG());UnityEngine.Object.Destroy(texture);
        }
        fixture.ApplyState(previous);Debug.Log("[SkillSync] Captured six reference UI states. These are fixture captures, not interaction test results.");
    }

    [MenuItem("Tools/Automation/Apply SkillSync Figma Design")]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode before applying the prefab.");
        var document = JsonUtility.FromJson<Document>(File.ReadAllText(AssetsPath + "/handoff.json"));
        if (document?.visuals == null) throw new InvalidDataException("Run Tools/DesignImport/prepare_handoff.py first.");
        PrepareSprites();
        ValidateSprites(document.visuals);
        PrepareFonts(document);
        var root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            RepairLegacyInputBlocker(root);
            root.GetComponent<Canvas>().vertexColorAlwaysGammaSpace=true;
            var scaler = root.GetComponent<UnityEngine.UI.CanvasScaler>();
            if (scaler == null) throw new InvalidOperationException("UIRoot CanvasScaler is missing.");
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600,1000); scaler.matchWidthOrHeight = .5f;
            // Keep old serialized references and service bootstrap. Do not rebuild UIRoot.
            foreach (Transform child in root.transform)
            {
                if (child.name == RootName) continue;
                if (child.GetComponent<CatalogUI>() != null)
                {
                    var group = child.GetComponent<CanvasGroup>();
                    if (group == null) group = child.gameObject.AddComponent<CanvasGroup>();
                    group.alpha = 0; group.interactable = false; group.blocksRaycasts = false;
                }
                else child.gameObject.SetActive(false);
            }
            foreach (var component in root.GetComponents<MonoBehaviour>())
                if (component is UiWorkspacePanels || component is UiScaleController || component is UiPanelDockSync || component is ViewportStatusStrip) component.enabled = false;
            var previous = root.transform.Find(RootName);
            if (previous != null) UnityEngine.Object.DestroyImmediate(previous.gameObject);
            var ui = Rect(RootName, root.transform, 0,0,1600,1000);
            // Source coordinates are retained until the 16:9 refinement below is applied.
            ui.anchorMin = ui.anchorMax = ui.pivot = new Vector2(.5f,.5f); ui.anchoredPosition = Vector2.zero;
            var view = ui.gameObject.AddComponent<SkillSyncDesignView>();
            ui.gameObject.AddComponent<SkillSyncEditorController>();
            Background(ui, "AboveViewport",0,0,1600,170);
            Background(ui, "BelowViewport",0,720,1600,280);
            Background(ui, "LeftOfViewport",0,170,288,550);
            Background(ui, "RightOfViewport",1136,170,464,550);
            var chrome = Rect("Chrome", ui,0,0,1600,1000);
            view.viewport = Rect("Viewport",ui,288,170,848,550);
            view.viewport.gameObject.AddComponent<UnityEngine.UI.RectMask2D>();
            var conditionOverlay=Rect("ConditionRange",view.viewport,0,0,848,550);
            view.conditionOverlay=conditionOverlay.gameObject.AddComponent<SkillSyncViewportOverlay>();view.conditionOverlay.raycastTarget=false;
            var visuals = new List<SkillSyncDesignView.Visual>();
            var controls = new List<SkillSyncDesignView.Control>();
            var fields = new List<SkillSyncDesignView.Field>();
            view.modalBlocker = Rect("ModalInputBlocker",ui,0,0,1600,1000).gameObject;
            var blocker = view.modalBlocker.AddComponent<UnityEngine.UI.Image>(); blocker.color = Color.clear;
            view.modalBlocker.AddComponent<EditorUiInputBlocker>();
            var modal = Rect("ExportValidation",ui,0,0,1600,1000); view.modal = modal.gameObject;
            foreach (var v in document.visuals.OrderBy(v => v.order))
            {
                var parent = v.region == "Modal" ? modal : chrome;
                var rect = Rect(v.sources[0] + " " + (v.text.Length > 0 ? v.text : v.kind),parent,v.x,v.y,v.w,v.h);
                TMP_Text label = null;
                if (v.kind == "TEXT")
                {
                    label = Label(rect, v.text,v.size,v.weight,ColorOf(v.color));
                    // Anchor the first baseline rather than substituting TMP's ascent metric.
                    label.alignment = TextAlignmentOptions.BaselineLeft;
                    rect.pivot = new Vector2(0,1); rect.anchoredPosition = new Vector2(v.x,-v.y-v.baseline+v.h/2);
                    rect.sizeDelta = new Vector2(v.w,v.h);
                    foreach(RectTransform child in rect) child.anchoredPosition+=Vector2.up*(v.baseline-v.h/2);
                    if(!string.IsNullOrEmpty(v.role))
                    {
                        float width=v.w,height=v.h;
                        if(v.role=="trialBody") {width=392;height=72;label.textWrappingMode=TextWrappingModes.Normal;}
                        else if(v.role=="learner") {width=808;height=44;label.textWrappingMode=TextWrappingModes.Normal;}
                        else if(v.role=="footer") width=1230;
                        else if(v.role=="selectionTitle" || v.role=="conditionHeading") width=392;
                        else if(v.role=="conditionSummary") width=360;
                        else if(v.role.StartsWith("error",StringComparison.Ordinal) || v.role=="draftStatus") width=538;
                        else if(v.role=="saved") width=250;
                        else if(v.role=="objectCount") width=200;
                        else if(v.role=="objectA" || v.role=="objectB") width=244;
                        rect.sizeDelta=new Vector2(width,height);rect.anchoredPosition=new Vector2(v.x,-v.y-v.baseline+height/2);
                        label.overflowMode=TextOverflowModes.Overflow;
                    }
                }
                else if(v.kind=="LINE") DrawSourceLine(rect,v);
                else
                {
                    var image = rect.gameObject.AddComponent<UnityEngine.UI.Image>();
                    image.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(AssetsPath + "/Sprites/" + v.sprite);
                    if (image.sprite == null) throw new InvalidDataException(v.sprite);
                    image.color = new Color(1,1,1,v.alpha); image.raycastTarget = false;
                    if(v.region=="Modal" && v.x==0 && v.y==88)
                    {
                        const string materialPath=AssetsPath+"/ScrimLinear.mat";
                        var material=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                        if(material==null) {material=new Material(Shader.Find("UI/Default"));AssetDatabase.CreateAsset(material,materialPath);}
                        // Figma composites its 32% scrim in sRGB; compensate the Linear render target's blend.
                        material.SetColor("_Color",new Color(1,1,1,QualitySettings.activeColorSpace==ColorSpace.Linear?1.72f:1));
                        EditorUtility.SetDirty(material);image.material=material;
                    }
                }
                visuals.Add(new SkillSyncDesignView.Visual { target=rect.gameObject,label=label,role=v.role,mask=v.mask,sourceNodeIds=v.sources,orders=v.orders });
            }
            foreach (var b in document.buttons)
            {
                bool inModal = b.action == "編集を続ける" || b.action == "該当箇所を修正";
                var rect=Rect("Button " + b.action,inModal ? modal : chrome,b.x,b.y,b.w,b.h);
                var button=ClickTarget(rect);
                controls.Add(new SkillSyncDesignView.Control {button=button,action=b.action,mask=b.mask,sourceNodeIds=b.sources});
            }
            foreach (var f in document.fields)
            {
                float w=f.w,h=f.h,x=f.x,y=f.y;
                if (f.role=="project") w=250;
                if (f.role=="name") {w=356;h=34;y=241;}
                if (f.role=="body") {w=360;h=72;y=243;}
                if (f.role=="stepTitle") {w=392;h=40;}
                if (f.role=="height" || f.role=="angle") {w=120;h=34;y=545;}
                if (f.role=="distance" || f.role=="hold") {w=102;h=34;y=646;}
                var input=Input("Field " + f.role,chrome,x,y,w,h,f.size,f.role=="body");
                input.textComponent.font=fonts[f.weight];
                var face=input.textComponent.font.faceInfo;
                float scale=f.size/face.pointSize*face.scale;
                input.textComponent.alignment=TextAlignmentOptions.TopLeft;
                input.textComponent.rectTransform.anchoredPosition=new Vector2(f.x-x,-(f.y-y+f.baseline-face.ascentLine*scale));
                if(f.role=="body") input.textComponent.lineSpacing=26/scale-face.lineHeight;
                fields.Add(new SkillSyncDesignView.Field {input=input,role=f.role,mask=f.mask,sourceNodeIds=f.sources});
            }
            var search=Input("Search",chrome,30,170,202,32,13,false);
            // The measured source already contains the placeholder; only show one copy.
            search.placeholder=visuals.First(v=>v.sourceNodeIds.Contains("26:258")).label;
            fields.Add(new SkillSyncDesignView.Field {input=search,role="search",mask=9,sourceNodeIds=new[]{"26:258","26:1514"}});
            AddTarget("PickA",1200,443,360,48,34); AddTarget("PickB",1200,540,360,48,34);
            AddTarget("NextCondition",1184,346,392,40,34);
            AddTarget("すべて",16,225,74,28,9); AddTarget("部品",94,225,64,28,9); AddTarget("環境",166,225,64,28,9);
            AddTarget("Snap",1184,810,392,34,9); AddTarget("詳細設定",1184,846,392,32,9);
            AddTarget("Save",276,46,240,24,63); AddTarget("Guide",1286,964,290,36,63);
            void AddTarget(string action,float x,float y,float w,float h,int mask)
            { controls.Add(new SkillSyncDesignView.Control {action=action,button=ClickTarget(Rect(action,chrome,x,y,w,h)),mask=mask}); }
            view.libraryContent=List(chrome,"Library",16,275,232,460,16,out view.libraryTemplate,154);
            view.stepsContent=List(chrome,"Steps",16,202,232,334,14,out view.stepTemplate,102);
            view.objectsContent=List(chrome,"Objects",288,818,848,124,8,out view.objectTemplate,58);
            var progress=Rect("HoldProgress",chrome,1204,578,352,8);
            view.holdProgress=progress.gameObject.AddComponent<UnityEngine.UI.Image>();
            view.holdProgress.color=ColorOf("#0866E8");view.holdProgress.raycastTarget=false;
            var badge=Rect("DistanceLabel",ui,628,448,220,36);
            var bg=badge.gameObject.AddComponent<SkillSyncRoundedGraphic>();bg.color=Color.white;bg.borderWidth=0;bg.radius=14;bg.raycastTarget=false;
            view.viewportDistance=Label(Rect("Value",badge,12,8,196,24),"",13,400,ColorOf("#15734A"));
            view.viewportGrid=Label(Rect("Grid",ui,969,677,160,22),"",12,400,ColorOf("#58677C"));
            view.objectAThumbnail=Rect("ObjectAThumbnail",chrome,1212,451,50,34).gameObject.AddComponent<UnityEngine.UI.RawImage>();
            view.objectBThumbnail=Rect("ObjectBThumbnail",chrome,1212,548,50,34).gameObject.AddComponent<UnityEngine.UI.RawImage>();
            view.objectAThumbnail.raycastTarget=view.objectBThumbnail.raycastTarget=false;
            view.partALabel=Badge("PartALabel",508,354,104,"#0866E8");
            view.partBLabel=Badge("PartBLabel",856,433,98,"#008B92");
            TMP_Text Badge(string name,float x,float y,float width,string color)
            {
                var host=Rect(name,ui,x,y,width,34);
                var surface=host.gameObject.AddComponent<SkillSyncRoundedGraphic>();surface.radius=10;surface.borderWidth=0;surface.color=Color.white;surface.raycastTarget=false;
                return Label(Rect("Value",host,14,7,width-28,22),"",14,700,ColorOf(color));
            }
            view.visuals=visuals.ToArray();view.controls=controls.ToArray();view.fields=fields.ToArray();
            SkillSyncDesignLayout.Apply(view);
            SkillSyncPdfRefinement.Apply(view);
            SkillSyncWorkspaceRefinement.Apply(view);
            view.EnsureProjectLoadControl();view.EnsureConditionControls();view.EnsureViewportLabels();
            TopCenterNotification.Ensure(view.transform, view.inspectorTitle);
            view.modalBlocker.transform.SetAsLastSibling();modal.SetAsLastSibling();
            view.Show(0);
            PrefabUtility.SaveAsPrefabAsset(root,PrefabPath,out bool saved);
            if(!saved) throw new InvalidOperationException("[SkillSync] UIRoot prefab save failed. See the preceding Console error.");
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        AssetDatabase.SaveAssets();
        PlayerSettings.defaultScreenWidth=2560;PlayerSettings.defaultScreenHeight=1440;
        Debug.Log("[SkillSync] Applied Figma design with 2560×1440 viewport refinements to UIRoot.");
    }

    static void RepairLegacyInputBlocker(GameObject root)
    {
        var missing=root.GetComponentsInChildren<Transform>(true)
            .Where(t=>GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)>0).ToArray();
        if(missing.Length==0) return;

        // Only migrate the verified, fieldless marker from the legacy Outliner.
        // Unknown missing scripts must retain their serialized data for investigation.
        var blocks=System.Text.RegularExpressions.Regex.Split(File.ReadAllText(PrefabPath),@"(?=--- !u!)");
        var broken=blocks.Where(b=>b.Contains("m_Script: {fileID: 0}")).ToArray();
        bool known=broken.Length==1 && System.Text.RegularExpressions.Regex.IsMatch(broken[0],
            @"m_Script: \{fileID: 0\}\s+m_Name:\s*m_EditorClassIdentifier: Assembly-CSharp::EditorUiInputBlocker\s*$");
        if(known)
        {
            var id=System.Text.RegularExpressions.Regex.Match(broken[0],@"m_GameObject: \{fileID: (\d+)\}").Groups[1].Value;
            known=blocks.Any(b=>(b.StartsWith("--- !u!1 &"+id+"\n",StringComparison.Ordinal)
                || b.StartsWith("--- !u!1 &"+id+"\r\n",StringComparison.Ordinal))
                && System.Text.RegularExpressions.Regex.IsMatch(b,@"(?m)^  m_Name: Panel_Outliner\r?$"));
        }
        if(!known || missing.Length!=1 || missing[0].name!="Panel_Outliner"
            || GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(missing[0].gameObject)!=1)
            throw new InvalidOperationException("[SkillSync] Unrecognized missing scripts in UIRoot: "
                +string.Join(", ",missing.Select(t=>t.name))+". No missing components were removed.");

        var script=AssetDatabase.LoadAssetAtPath<MonoScript>("Assets/Scripts/EditorUiInputBlocker.cs");
        if(script==null || script.GetClass()!=typeof(EditorUiInputBlocker))
            throw new InvalidOperationException("[SkillSync] Wait for EditorUiInputBlocker.cs to finish importing and compiling.");
        var panel=missing[0].gameObject;
        if(panel.GetComponent<EditorUiInputBlocker>()==null) panel.AddComponent<EditorUiInputBlocker>();
        GameObjectUtility.RemoveMonoBehavioursWithMissingScript(panel);
    }

    static void PrepareSprites()
    {
        foreach (var path in Directory.GetFiles(AssetsPath+"/Sprites","*.png"))
        {
            var assetPath=path.Replace('\\','/');
            var importer=AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if(importer==null) throw new InvalidOperationException("Import assets before applying: "+path);
            var settings=new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            bool ready=importer.textureType==TextureImporterType.Sprite && importer.spriteImportMode==SpriteImportMode.Single
                && importer.textureShape==TextureImporterShape.Texture2D
                && settings.spriteMeshType==SpriteMeshType.FullRect && !importer.mipmapEnabled
                && importer.alphaIsTransparency && importer.spritePixelsPerUnit==200
                && importer.textureCompression==TextureImporterCompression.Uncompressed && importer.maxTextureSize==4096;
            if(ready && AssetDatabase.LoadAssetAtPath<Sprite>(assetPath)!=null) continue;
            settings.ApplyTextureType(TextureImporterType.Sprite);
            // Type and shape are independent: a Cubemap importer cannot produce a UI Sprite.
            settings.textureShape=TextureImporterShape.Texture2D;
            settings.spriteMeshType=SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
            importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;
            importer.mipmapEnabled=false;importer.alphaIsTransparency=true;importer.spritePixelsPerUnit=200;
            importer.textureCompression=TextureImporterCompression.Uncompressed;importer.maxTextureSize=4096;
            importer.SaveAndReimport();
            if(AssetDatabase.LoadAssetAtPath<Sprite>(assetPath)==null)
                AssetDatabase.ImportAsset(assetPath,ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            if(AssetDatabase.LoadAssetAtPath<Sprite>(assetPath)==null)
                throw new InvalidDataException("[SkillSync] Sprite import failed: "+assetPath
                    +" (type="+importer.textureType+", shape="+importer.textureShape+", mode="+importer.spriteImportMode
                    +", imported="+string.Join(", ",AssetDatabase.LoadAllAssetsAtPath(assetPath).Select(a=>a.GetType().Name))
                    +"). Check the texture importer errors in Console. UIRoot has not been modified.");
        }
    }
    static void DrawSourceLine(RectTransform rect,V v)
    {
        rect.sizeDelta=new Vector2(v.w==0?v.strokeWidth:v.w,v.h==0?v.strokeWidth:v.h);
        rect.anchoredPosition=new Vector2(v.x-(v.w==0?v.strokeWidth/2:0),-v.y+(v.h==0?v.strokeWidth/2:0));
        var line=rect.gameObject.AddComponent<UnityEngine.UI.Image>();
        var color=ColorOf(v.color);color.a=v.alpha;line.color=color;line.raycastTarget=false;
    }
    static void ValidateSprites(IEnumerable<V> visuals)
    {
        var names=visuals.Where(v=>v.kind!="TEXT" && v.kind!="LINE").Select(v=>v.sprite)
            .Concat(JsonUtility.FromJson<SymbolDocument>(File.ReadAllText(AssetsPath+"/symbols.json")).glyphs.Select(s=>s.sprite));
        var missing=names.Distinct().Where(name=>string.IsNullOrEmpty(name)
            || AssetDatabase.LoadAssetAtPath<Sprite>(AssetsPath+"/Sprites/"+name)==null).ToArray();
        if(missing.Length>0) throw new InvalidDataException("[SkillSync] Missing Sprite assets: "+string.Join(", ",missing));
    }
    static void PrepareFonts(Document document)
    {
        fonts.Clear();
        symbols=JsonUtility.FromJson<SymbolDocument>(File.ReadAllText(AssetsPath+"/symbols.json")).glyphs;
        foreach(var pair in new[]{(400,"Regular"),(500,"Medium"),(700,"Bold")})
        {
            string path=AssetsPath+"/Fonts/NotoSansJP-"+pair.Item2+" TMP.asset";
            var asset=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            if(asset==null)
            {
                var font=AssetDatabase.LoadAssetAtPath<Font>(AssetsPath+"/Fonts/NotoSansJP-"+pair.Item2+".ttf");
                if(font==null) throw new FileNotFoundException("Noto Sans JP "+pair.Item2);
                asset=TMP_FontAsset.CreateFontAsset(font,90,9,GlyphRenderMode.SDFAA,2048,2048,AtlasPopulationMode.Dynamic,true);
                asset.name="NotoSansJP-"+pair.Item2;
                AssetDatabase.CreateAsset(asset,path);
                AssetDatabase.AddObjectToAsset(asset.material,asset);
                foreach(var texture in asset.atlasTextures) AssetDatabase.AddObjectToAsset(texture,asset);
            }
            fonts[pair.Item1]=asset;
            asset.material.SetFloat("_Sharpness",.5f);
            var chars=string.Concat(document.visuals.Where(v=>v.weight==pair.Item1).Select(v=>v.text))+"0123456789✓↶↷⌕▶Ⅱ°←↑↓→";
            foreach(var symbol in symbols) chars=chars.Replace(symbol.character,"");
            if(!asset.TryAddCharacters(chars,out string missing)) Debug.LogWarning("[SkillSync] Missing glyphs: "+missing);
            foreach(var texture in asset.atlasTextures)
            {
                if(!AssetDatabase.Contains(texture)) AssetDatabase.AddObjectToAsset(texture,asset);
                EditorUtility.SetDirty(texture);
            }
            EditorUtility.SetDirty(asset.material);
            EditorUtility.SetDirty(asset);
        }
    }
    static RectTransform Rect(string name,Transform parent,float x,float y,float w,float h)
    {
        var rect=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();rect.SetParent(parent,false);
        rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(0,1);
        rect.anchoredPosition=new Vector2(x,-y);rect.sizeDelta=new Vector2(w,h);return rect;
    }
    static Color ColorOf(string hex) {ColorUtility.TryParseHtmlString(hex,out var c);return c;}
    static TMP_Text Label(RectTransform rect,string value,float size,int weight,Color color)
    {
        var text=rect.gameObject.AddComponent<TextMeshProUGUI>();text.text=value;text.font=fonts[weight];text.fontSize=size;
        text.enableAutoSizing=false;text.richText=false;text.textWrappingMode=TextWrappingModes.NoWrap;text.overflowMode=TextOverflowModes.Overflow;
        text.color=color;text.raycastTarget=false;text.alignment=TextAlignmentOptions.TopLeft;
        foreach(var symbol in symbols ?? Array.Empty<Symbol>())
        {
            if(!value.StartsWith(symbol.character,StringComparison.Ordinal)) continue;
            text.richText=true;text.text="<space="+symbol.advance.ToString(System.Globalization.CultureInfo.InvariantCulture)+">"+value.Substring(symbol.character.Length);
            var glyph=Rect("Source glyph "+symbol.character,rect,symbol.offsetX,symbol.offsetY,symbol.width,symbol.height);
            var image=glyph.gameObject.AddComponent<UnityEngine.UI.Image>();image.sprite=AssetDatabase.LoadAssetAtPath<Sprite>(AssetsPath+"/Sprites/"+symbol.sprite);
            image.color=color;image.raycastTarget=false;
        }
        return text;
    }
    static UnityEngine.UI.Button ClickTarget(RectTransform rect)
    {
        var image=rect.gameObject.AddComponent<UnityEngine.UI.Image>();image.color=Color.clear;
        var button=rect.gameObject.AddComponent<UnityEngine.UI.Button>();button.targetGraphic=image;
        button.transition=UnityEngine.UI.Selectable.Transition.None;rect.gameObject.AddComponent<EditorUiInputBlocker>();return button;
    }
    static TMP_InputField Input(string name,Transform parent,float x,float y,float w,float h,float size,bool multiline)
    {
        var rect=Rect(name,parent,x,y,w,h);var image=rect.gameObject.AddComponent<UnityEngine.UI.Image>();image.color=Color.clear;
        var input=rect.gameObject.AddComponent<TMP_InputField>();rect.gameObject.AddComponent<EditorUiInputBlocker>();
        var area=Rect("Text Area",rect,0,0,w,h);area.gameObject.AddComponent<UnityEngine.UI.RectMask2D>();
        var text=Label(Rect("Text",area,0,0,w,h),"",size,400,ColorOf("#172236"));
        text.alignment=TextAlignmentOptions.MidlineLeft;text.textWrappingMode=multiline?TextWrappingModes.Normal:TextWrappingModes.NoWrap;
        input.textViewport=area;input.textComponent=(TextMeshProUGUI)text;input.targetGraphic=image;
        input.lineType=multiline?TMP_InputField.LineType.MultiLineNewline:TMP_InputField.LineType.SingleLine;
        input.transition=UnityEngine.UI.Selectable.Transition.None;return input;
    }
    static void Background(Transform parent,string name,float x,float y,float w,float h)
    {var i=Rect(name,parent,x,y,w,h).gameObject.AddComponent<UnityEngine.UI.Image>();i.color=ColorOf("#F5F7FA");i.raycastTarget=false;}
    static RectTransform List(Transform parent,string name,float x,float y,float w,float h,float gap,out SkillSyncDesignRow template,float rowHeight)
    {
        var rect=Rect(name,parent,x,y,w,h);var scroll=rect.gameObject.AddComponent<UnityEngine.UI.ScrollRect>();
        var viewport=Rect("Viewport",rect,0,0,w,h);var image=viewport.gameObject.AddComponent<UnityEngine.UI.Image>();image.color=Color.white;
        viewport.gameObject.AddComponent<UnityEngine.UI.Mask>().showMaskGraphic=false;viewport.gameObject.AddComponent<EditorUiInputBlocker>();
        var content=Rect("Content",viewport,0,0,w,h);
        var layout=content.gameObject.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();layout.spacing=gap;
        layout.childControlWidth=true;layout.childControlHeight=true;layout.childForceExpandWidth=true;layout.childForceExpandHeight=false;
        content.gameObject.AddComponent<UnityEngine.UI.ContentSizeFitter>().verticalFit=UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
        scroll.viewport=viewport;scroll.content=content;scroll.horizontal=false;scroll.movementType=UnityEngine.UI.ScrollRect.MovementType.Clamped;
        var row=Rect("RowTemplate",content,0,0,w,rowHeight);template=row.gameObject.AddComponent<SkillSyncDesignRow>();
        template.layout=name;template.regularFont=fonts[400];template.boldFont=fonts[700];
        var rounded=row.gameObject.AddComponent<SkillSyncRoundedGraphic>();rounded.color=Color.white;rounded.radius=name=="Library"?14:12;
        template.button=row.gameObject.AddComponent<UnityEngine.UI.Button>();template.button.targetGraphic=rounded;template.button.transition=UnityEngine.UI.Selectable.Transition.None;
        row.gameObject.AddComponent<UnityEngine.UI.LayoutElement>().preferredHeight=rowHeight;
        template.title=Label(Rect("Title",row,name=="Steps"?60:16, name=="Library"?93:18,w-80,30),"",name=="Library"?16:15,700,ColorOf("#172236"));
        template.description=Label(Rect("Description",row,16,name=="Library"?121:65,w-32,24),"",12,400,ColorOf("#58677C"));
        template.number=Label(Rect("Number",row,16,18,30,28),"",17,700,ColorOf("#172236"));
        template.title.overflowMode=TextOverflowModes.Ellipsis;template.description.overflowMode=TextOverflowModes.Ellipsis;
        if(name=="Steps")
        {
            var circle=Rect("NumberBackground",row,16,15,34,34);circle.SetSiblingIndex(0);
            template.numberBackground=circle.gameObject.AddComponent<SkillSyncRoundedGraphic>();template.numberBackground.radius=17;template.numberBackground.borderWidth=0;template.numberBackground.raycastTarget=false;
            template.number.fontSize=16;template.number.rectTransform.anchoredPosition=new Vector2(27,-22);
        }
        else
        {
            template.number.gameObject.SetActive(false);
            var preview=name=="Library"?Rect("ModelThumbnail",row,62.8f,17,104,64):Rect("ModelThumbnail",row,9.28f,9,62.4f,38.4f);
            template.thumbnail=preview.gameObject.AddComponent<UnityEngine.UI.RawImage>();template.thumbnail.raycastTarget=false;
        }
        if(name=="Objects") {
            template.title.rectTransform.anchoredPosition=new Vector2(94,-9);template.description.rectTransform.anchoredPosition=new Vector2(94,-32);
            template.status=Label(Rect("Status",row,690,18,140,24),"",12,400,ColorOf("#58677C"));
        }
        row.gameObject.SetActive(false);return content;
    }
}
