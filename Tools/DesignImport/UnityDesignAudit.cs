using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

public static class UnityDesignAudit
{
    public static string FinishEditor()
    {
        if(EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode first");
        for(int i=0;i<SceneManager.sceneCount;i++)
            if(SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Unsaved scene: "+SceneManager.GetSceneAt(i).path);
        // These folders contain only the temporary captures created by this verification run.
        AssetDatabase.DeleteAsset("Assets/Design/skillsync_codex_handoff/verification/before.png");
        foreach(var folder in new[]{"Assets/Design/skillsync_codex_handoff/verification","Assets/Design/skillsync_codex_handoff","Assets/Design"})
            if(System.IO.Directory.Exists(folder) && !System.IO.Directory.EnumerateFileSystemEntries(folder).Any()) AssetDatabase.DeleteAsset(folder);
        AssetDatabase.DeleteAsset("Assets/SkillSyncVerification/01.png");
        AssetDatabase.DeleteAsset("Assets/SkillSyncVerification/01_sharp.png");
        if(System.IO.Directory.Exists("Assets/SkillSyncVerification") && !System.IO.Directory.EnumerateFileSystemEntries("Assets/SkillSyncVerification").Any()) AssetDatabase.DeleteAsset("Assets/SkillSyncVerification");
        UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/UI/Prefabs/UIRoot.prefab");
        int missing=prefab.GetComponentsInChildren<Transform>(true).Sum(t=>GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject));
        int views=prefab.GetComponentsInChildren<SkillSyncDesignView>(true).Length;
        if(missing!=0 || views!=1) throw new InvalidOperationException($"Missing scripts={missing}, design roots={views}");
        return $"SampleScene restored in Edit Mode. Prefab: missing scripts={missing}, design roots={views}. Existing scene was not saved or overwritten.";
    }
    public static void CalibrateText()
    {
        var view=UnityEngine.Object.FindFirstObjectByType<SkillSyncDesignView>();
        foreach(var text in view.GetComponentsInChildren<TMP_Text>(true)) {
            text.fontSharedMaterial.SetFloat("_Sharpness",.5f);
        }
    }
    public static void State(int state)
    {
        var fixture=UnityEngine.Object.FindFirstObjectByType<SkillSyncReferencePreview>();
        fixture.StopAllCoroutines();fixture.ApplyState(state);
        Canvas.ForceUpdateCanvases();
    }
    public static async System.Threading.Tasks.Task<string> Capture(int state)
    {
        Application.runInBackground=true;
        if(state>=0) State(state);
        await System.Threading.Tasks.Task.Delay(200);
        Canvas.ForceUpdateCanvases();
        var gameViewType=typeof(Editor).Assembly.GetType("UnityEditor.GameView");
        var gameView=EditorWindow.GetWindow(gameViewType);
        var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Public;
        gameViewType.GetProperty("drawGizmos",flags)?.SetValue(gameView,false);
        gameView.Focus(); gameViewType.GetMethod("RepaintImmediately",flags).Invoke(gameView,null);
        await System.Threading.Tasks.Task.Delay(200);
        var source=(RenderTexture)gameViewType.BaseType.GetField("m_TargetTexture",flags).GetValue(gameView);
        if(source==null || source.width!=2560 || source.height!=1440) throw new InvalidOperationException("Expected a 2560x1440 Game view target");
        var target=RenderTexture.GetTemporary(source.width,source.height,0,RenderTextureFormat.ARGB32);
        var previous=RenderTexture.active;
        var texture=new Texture2D(source.width,source.height,TextureFormat.RGB24,false);
        try {
            Graphics.Blit(source,target,SystemInfo.graphicsUVStartsAtTop?new Vector2(1,-1):Vector2.one,SystemInfo.graphicsUVStartsAtTop?new Vector2(0,1):Vector2.zero);
            RenderTexture.active=target;texture.ReadPixels(new Rect(0,0,target.width,target.height),0,0);texture.Apply();
            var folder="Design/skillsync_codex_handoff/verification/workspace-captures";System.IO.Directory.CreateDirectory(folder);
            var path=folder+"/"+(state>=0?(state+1).ToString("00")+"_unity":"runtime")+".png";
            System.IO.File.WriteAllBytes(path,texture.EncodeToPNG());return path;
        }
        finally {RenderTexture.active=previous;RenderTexture.ReleaseTemporary(target);UnityEngine.Object.DestroyImmediate(texture);}
    }
    public static string PrepareReference()
    {
        if(EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode first.");
        for(int i=0;i<SceneManager.sceneCount;i++)
            if(SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Unsaved scene: "+SceneManager.GetSceneAt(i).path);
        if(SceneManager.GetActiveScene().path.EndsWith("ReferencePreview.unity"))
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
        ApplySkillSyncDesign.ApplyViewportRefinements();
        ApplySkillSyncDesign.CreateReferenceScene();
        for(int i=SceneManager.sceneCount-1;i>=0;i--) {
            var scene=SceneManager.GetSceneAt(i);
            if(!scene.path.EndsWith("ReferencePreview.unity")) UnityEditor.SceneManagement.EditorSceneManager.CloseScene(scene,true);
        }
        SetResolution();
        return Inspect();
    }
    public static void SetResolution()
    {
        var assembly=typeof(Editor).Assembly;
        var sizesType=assembly.GetType("UnityEditor.GameViewSizes");
        var singleton=typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
        var sizes=singleton.GetProperty("instance").GetValue(null);
        var groupType=assembly.GetType("UnityEditor.GameViewSizeGroupType");
        var group=sizesType.GetMethod("GetGroup").Invoke(sizes,new[]{Enum.Parse(groupType,"Standalone")});
        var sizeType=assembly.GetType("UnityEditor.GameViewSize");
        var fixedType=assembly.GetType("UnityEditor.GameViewSizeType");
        int count=(int)group.GetType().GetMethod("GetTotalCount").Invoke(group,null);
        int index=-1;
        for(int i=0;i<count;i++) {
            var candidate=group.GetType().GetMethod("GetGameViewSize").Invoke(group,new object[]{i});
            if((int)sizeType.GetProperty("width").GetValue(candidate)==2560 && (int)sizeType.GetProperty("height").GetValue(candidate)==1440) {index=i;break;}
        }
        if(index<0) {
            var size=Activator.CreateInstance(sizeType,new object[]{Enum.Parse(fixedType,"FixedResolution"),2560,1440,"SkillSync 2560x1440"});
            group.GetType().GetMethod("AddCustomSize").Invoke(group,new[]{size});index=count;
        }
        var gameViewType=assembly.GetType("UnityEditor.GameView");
        var gameView=EditorWindow.GetWindow(gameViewType);
        gameViewType.GetProperty("selectedSizeIndex",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic).SetValue(gameView,index);
    }
    public static string Inspect()
    {
        var lines=new System.Collections.Generic.List<string>();
        for(int i=0;i<SceneManager.sceneCount;i++) {
            var scene=SceneManager.GetSceneAt(i);
            lines.Add($"Scene {scene.path}, dirty={scene.isDirty}");
            foreach(var root in scene.GetRootGameObjects()) lines.Add("Root "+root.name);
        }
        var view=UnityEngine.Object.FindFirstObjectByType<SkillSyncDesignView>();
        if(view!=null) {
            lines.Add("Canvas gamma="+view.GetComponentInParent<Canvas>().vertexColorAlwaysGammaSpace+", screen="+Screen.width+"x"+Screen.height);
            foreach(var component in view.transform.root.GetComponents<MonoBehaviour>())
                lines.Add("Root component "+component.GetType().Name+" enabled="+component.enabled);
            foreach(var v in view.visuals.Where(v=>v.target.activeInHierarchy && (v.role=="saved"||v.role=="status"||v.role=="workspaceHint")))
            {
                v.label.ForceMeshUpdate();
                var mesh=v.label.textInfo.meshInfo[0];
                lines.Add($"Text {v.role} {v.label.text} active={v.target.activeInHierarchy} index={v.target.transform.GetSiblingIndex()} color={v.label.color} font={v.label.font.name} face={v.label.fontSharedMaterial.GetColor("_FaceColor")} mesh={(mesh.colors32.Length>0?mesh.colors32[0].ToString():"none")} material={v.label.fontSharedMaterial.name} shader={v.label.fontSharedMaterial.shader.name}");
            }
        }
        return string.Join("\n",lines);
    }
}

