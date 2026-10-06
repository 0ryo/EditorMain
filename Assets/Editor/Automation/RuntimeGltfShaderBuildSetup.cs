using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

// Runtime imports are not asset dependencies of a scene. Keep every material
// variant: users can import models that did not exist when the Player was built.
public sealed class RuntimeGltfShaderBuildSetup : IPreprocessBuildWithReport
{
    const string ShaderRoot = "Packages/com.unity.cloud.gltfast/Runtime/Shader/";
    static readonly string[] ShaderPaths =
    {
        "glTF-pbrMetallicRoughness.shadergraph",
        "glTF-pbrSpecularGlossiness.shadergraph",
        "glTF-unlit.shadergraph",
        "URP/glTF-pbrMetallicRoughness-Clearcoat.shadergraph"
    };

    public int callbackOrder => -1000;

    public void OnPreprocessBuild(BuildReport report) => EnsureIncluded();

    [MenuItem("Tools/Automation/Include Runtime GLB Shaders")]
    public static void EnsureIncluded()
    {
        var settings = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset");
        if (settings.Length == 0) throw new BuildFailedException("Graphics Settings が見つかりません。");
        var serialized = new SerializedObject(settings[0]);
        var shaders = serialized.FindProperty("m_AlwaysIncludedShaders");
        if (shaders == null) throw new BuildFailedException("GLB描画用シェーダーのビルド設定を取得できません。");
        foreach (string path in ShaderPaths)
        {
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderRoot + path);
            if (shader == null) throw new BuildFailedException("GLB描画用シェーダーがありません: " + path);
            bool included = false;
            for (int i = 0; i < shaders.arraySize; i++)
                if (shaders.GetArrayElementAtIndex(i).objectReferenceValue == shader) included = true;
            if (included) continue;
            int index = shaders.arraySize;
            shaders.InsertArrayElementAtIndex(index);
            shaders.GetArrayElementAtIndex(index).objectReferenceValue = shader;
        }
        if (serialized.ApplyModifiedPropertiesWithoutUndo()) AssetDatabase.SaveAssets();
        Debug.Log("[GLB build] All four glTFast URP shaders and their material variants are included.");
    }
}
