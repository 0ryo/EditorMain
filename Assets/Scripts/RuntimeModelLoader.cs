using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Threading;
using UnityEngine;
using GLTFast;

public static class RuntimeModelLoader
{
    static readonly string[] SupportedExtensions = { ".glb", ".gltf" };

    public static bool IsSupportedExtension(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;
        var ext = Path.GetExtension(path);
        foreach (var supported in SupportedExtensions)
        {
            if (string.Equals(ext, supported, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    // The limit is for the input file; textures and decompressed geometry may use more memory.
    public const long MaximumFileBytes = 256L * 1024 * 1024;

    public static async Task<GameObject> LoadModelAsync(string absolutePath,
        CancellationToken cancellationToken = default, Action<string> progress = null)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!IsSupportedExtension(absolutePath)) throw new IOException("GLBまたはglTFを選択してください。");
        var file = new FileInfo(absolutePath);
        if (!file.Exists) throw new FileNotFoundException("モデルファイルが見つかりません。", absolutePath);
        if (file.Length == 0 || file.Length > MaximumFileBytes)
            throw new IOException("モデルは空でない256 MB以下のファイルを使用してください。");
        progress?.Invoke("ファイルと参照素材を検証中…");
        ImportedModelStore.ValidateInput(file.FullName, MaximumFileBytes);
        cancellationToken.ThrowIfCancellationRequested();
        var gltf = new GltfImport();
        GameObject go = null;
        bool retained = false;
        try
        {
            progress?.Invoke("モデルを読み込み中…（追加ボタンで中止）");
            if (!await gltf.Load(new Uri(file.FullName), cancellationToken: cancellationToken))
                throw new IOException("モデルを読み込めません。形式、外部素材、破損の有無を確認してください。");
            cancellationToken.ThrowIfCancellationRequested();
            progress?.Invoke("形状・材質を構築中…（追加ボタンで中止）");
            go = new GameObject(Path.GetFileNameWithoutExtension(absolutePath));
            var resources = go.AddComponent<RuntimeModelResources>();
            go.SetActive(false);
            if (!await gltf.InstantiateMainSceneAsync(go.transform, cancellationToken))
                throw new IOException("モデルのシーンを構築できません。");
            cancellationToken.ThrowIfCancellationRequested();
            ValidateRuntimeModel(go);
            // The catalog prototype owns glTF assets for the lifetime of its placed copies.
            resources.Initialize(gltf);
            retained = true;
            return go;
        }
        finally
        {
            if (!retained)
            {
                if (go != null) UnityEngine.Object.Destroy(go);
                gltf.Dispose();
            }
        }
    }

    static void ValidateRuntimeModel(GameObject model)
    {
        long vertices = 0, textureBytes = 0;
        var meshes = new System.Collections.Generic.HashSet<Mesh>();
        var textures = new System.Collections.Generic.HashSet<Texture>();
        foreach (var filter in model.GetComponentsInChildren<MeshFilter>(true))
            if (filter.sharedMesh != null) meshes.Add(filter.sharedMesh);
        foreach (var skin in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            if (skin.sharedMesh != null) meshes.Add(skin.sharedMesh);
        foreach (var mesh in meshes) vertices += mesh.vertexCount;
        if (vertices == 0) throw new IOException("表示可能な形状がありません。");
        if (vertices > 5000000) throw new IOException("モデルの頂点数を500万以下に減らしてください。");
        foreach (var renderer in model.GetComponentsInChildren<Renderer>(true))
            foreach (var material in renderer.sharedMaterials)
            {
                if (material == null) continue;
                foreach (var key in material.GetTexturePropertyNames())
                {
                    var texture = material.GetTexture(key);
                    if (texture != null) textures.Add(texture);
                }
            }
        foreach (var texture in textures)
        {
            if (texture.width > 8192 || texture.height > 8192) throw new IOException("テクスチャの縦横を8192px以下に減らしてください。");
            textureBytes += UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(texture);
        }
        if (textureBytes > MaximumFileBytes) throw new IOException("展開後のテクスチャ容量を合計256 MB以下に減らしてください。");
    }

#if UNITY_STANDALONE_WIN
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct OPENFILENAME
    {
        public int lStructSize;
        public IntPtr hwndOwner;
        public IntPtr hInstance;
        public string lpstrFilter;
        public string lpstrCustomFilter;
        public int nMaxCustFilter;
        public int nFilterIndex;
        public IntPtr lpstrFile;
        public int nMaxFile;
        public string lpstrFileTitle;
        public int nMaxFileTitle;
        public string lpstrInitialDir;
        public string lpstrTitle;
        public int Flags;
        public short nFileOffset;
        public short nFileExtension;
        public string lpstrDefExt;
        public IntPtr lCustData;
        public IntPtr lpfnHook;
        public string lpTemplateName;
        public IntPtr pvReserved;
        public int dwReserved;
        public int flagsEx;
    }

    [DllImport("comdlg32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    static extern bool GetOpenFileNameW(ref OPENFILENAME lpOpenFileName);

    const int OFN_FILEMUSTEXIST = 0x00001000;
    const int OFN_PATHMUSTEXIST = 0x00000800;
    const int OFN_NOCHANGEDIR = 0x00000008;

    public static string OpenFileDialog(string title, string initialDir)
    {
        var ofn = new OPENFILENAME();
        ofn.lStructSize = Marshal.SizeOf(ofn);
        ofn.lpstrFilter = "3D Models (*.glb, *.gltf)\0*.glb;*.gltf\0All Files (*.*)\0*.*\0\0";
        ofn.lpstrTitle = title;
        if (!string.IsNullOrWhiteSpace(initialDir))
            ofn.lpstrInitialDir = initialDir;
        ofn.Flags = OFN_FILEMUSTEXIST | OFN_PATHMUSTEXIST | OFN_NOCHANGEDIR;

        const int maxFile = 260;
        IntPtr fileBuffer = Marshal.AllocHGlobal(maxFile * 2);
        Marshal.Copy(new byte[maxFile * 2], 0, fileBuffer, maxFile * 2);
        ofn.lpstrFile = fileBuffer;
        ofn.nMaxFile = maxFile;

        string result = null;
        try
        {
            if (GetOpenFileNameW(ref ofn))
            {
                result = Marshal.PtrToStringUni(fileBuffer);
            }
        }
        finally
        {
            Marshal.FreeHGlobal(fileBuffer);
        }

        return result;
    }
#endif
}
