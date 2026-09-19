using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public static class ApplyViewportImprovements
{
    const string RendererPath = "Assets/Settings/SkillSyncViewportRenderer.asset";

    [MenuItem("Tools/Automation/Apply Viewport Improvements")]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Playを停止してから適用してください。");
        var pipelines = new HashSet<UniversalRenderPipelineAsset>();
        if (GraphicsSettings.defaultRenderPipeline is UniversalRenderPipelineAsset defaultPipeline)
            pipelines.Add(defaultPipeline);
        for (int i = 0; i < QualitySettings.names.Length; i++)
            if (QualitySettings.GetRenderPipelineAssetAt(i) is UniversalRenderPipelineAsset qualityPipeline)
                pipelines.Add(qualityPipeline);
        if (pipelines.Count == 0) throw new InvalidOperationException("URP assetが設定されていません。");

        var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
        if (renderer == null)
        {
            renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
            renderer.name = ViewportLightingController.RendererName;
            AssetDatabase.CreateAsset(renderer, RendererPath);
        }
        foreach (var pipeline in pipelines)
        {
            var serialized = new SerializedObject(pipeline);
            var list = serialized.FindProperty("m_RendererDataList");
            bool exists = false;
            for (int i = 0; i < list.arraySize; i++)
                if (list.GetArrayElementAtIndex(i).objectReferenceValue == renderer) exists = true;
            if (exists) continue;
            Undo.RecordObject(pipeline, "Add viewport renderer");
            int index = list.arraySize;
            list.InsertArrayElementAtIndex(index);
            list.GetArrayElementAtIndex(index).objectReferenceValue = renderer;
            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(pipeline);
        }
        // Keep every existing renderer index and default renderer intact.
        ApplyAuthoringFeatures.Apply();
        AssetDatabase.SaveAssets();
        Debug.Log("[Viewport] 一覧UIと編集カメラ用3D Rendererを適用しました。SampleScene／EditorMainをPlayして表示を確認してください。");
    }
}
