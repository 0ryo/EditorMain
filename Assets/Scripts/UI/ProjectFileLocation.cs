using System;
using System.IO;
using UnityEngine;

// Shows a saved lesson file in the OS file manager (the app's data folder is otherwise hidden from the user).
public static class ProjectFileLocation
{
    public static string RevealLabel
    {
        get
        {
            switch (Application.platform)
            {
                case RuntimePlatform.OSXEditor: case RuntimePlatform.OSXPlayer: return "Finderで表示";
                case RuntimePlatform.WindowsEditor: case RuntimePlatform.WindowsPlayer: return "エクスプローラーで表示";
                default: return "フォルダを開く";
            }
        }
    }

    public static void Reveal(string path)
    {
        if (string.IsNullOrEmpty(path)) return;
        string full = Path.GetFullPath(path);
        try
        {
            var start = new System.Diagnostics.ProcessStartInfo { UseShellExecute = false, CreateNoWindow = true };
            switch (Application.platform)
            {
                case RuntimePlatform.OSXEditor: case RuntimePlatform.OSXPlayer:
                    start.FileName = "open"; start.ArgumentList.Add("-R"); start.ArgumentList.Add(full);
                    break;
                case RuntimePlatform.WindowsEditor: case RuntimePlatform.WindowsPlayer:
                    start.FileName = "explorer.exe"; start.Arguments = "/select,\"" + full + "\"";
                    break;
                default:
                    Application.OpenURL(new Uri(Path.GetDirectoryName(full)).AbsoluteUri);
                    return;
            }
            System.Diagnostics.Process.Start(start)?.Dispose();
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[ProjectFileLocation] " + ex.Message);
            try { Application.OpenURL(new Uri(Path.GetDirectoryName(full)).AbsoluteUri); } catch { }
        }
    }
}
