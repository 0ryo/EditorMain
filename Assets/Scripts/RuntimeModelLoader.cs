using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
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

    public static async Task<GameObject> LoadModelAsync(string absolutePath)
    {
        if (string.IsNullOrWhiteSpace(absolutePath))
            return null;

        if (!File.Exists(absolutePath))
        {
            Debug.LogError($"[RuntimeModelLoader] File not found: {absolutePath}");
            return null;
        }

        var gltf = new GltfImport();
        var uri = new Uri(absolutePath).AbsoluteUri;
        var success = await gltf.Load(uri);
        if (!success)
        {
            Debug.LogError($"[RuntimeModelLoader] Failed to load: {absolutePath}");
            return null;
        }

        var go = new GameObject(Path.GetFileNameWithoutExtension(absolutePath));
        var instantiated = await gltf.InstantiateMainSceneAsync(go.transform);
        if (!instantiated)
        {
            UnityEngine.Object.Destroy(go);
            Debug.LogError($"[RuntimeModelLoader] Failed to instantiate: {absolutePath}");
            return null;
        }

        go.SetActive(false);
        return go;
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
