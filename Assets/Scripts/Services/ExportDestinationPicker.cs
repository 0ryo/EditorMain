using System;
using System.IO;
using System.Text;
using UnityEngine;

// Where a lesson is exported: a folder dialog (macOS; other platforms use the default folder) whose last
// answer is remembered, and "show in Finder/Explorer" for the finished ZIP.
public static class ExportDestinationPicker
{
    const string LastFolderKey = "SkillSync.Export.LastFolder";

    static bool IsMac => Application.platform == RuntimePlatform.OSXPlayer || Application.platform == RuntimePlatform.OSXEditor;
    static bool IsWindows => Application.platform == RuntimePlatform.WindowsPlayer || Application.platform == RuntimePlatform.WindowsEditor;

    public static string RevealLabel => IsMac ? "Finderで表示" : IsWindows ? "フォルダで表示" : "フォルダを開く";

    // The folder to export into. Null with cancelled=false means no dialog exists here: use the default folder.
    public static string Choose(out bool cancelled)
    {
        cancelled = false;
#if UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
        try
        {
            string picked = ChooseFolderMac(InitialFolder());
            if (string.IsNullOrEmpty(picked)) { cancelled = true; return null; }
            picked = Path.GetFullPath(picked);
            PlayerPrefs.SetString(LastFolderKey, picked);
            PlayerPrefs.Save();
            return picked;
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[Export] 書き出し先の選択画面を開けないため、既定のフォルダへ書き出します: " + ex.Message);
        }
#endif
        return null;
    }

    static string InitialFolder()
    {
        string last = PlayerPrefs.GetString(LastFolderKey, "");
        if (!string.IsNullOrEmpty(last) && Directory.Exists(last)) return last;
        try
        {
            string fallback = RuntimeExportPathUtility.ExportsDirectory;
            Directory.CreateDirectory(fallback);
            return fallback;
        }
        catch (Exception)
        {
            return Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        }
    }

    public static void Reveal(string path)
    {
        try
        {
            if (IsMac) Start("/usr/bin/open", "-R " + Quote(path));
            else if (IsWindows) Start("explorer.exe", "/select," + Quote(path));
            else Application.OpenURL(new Uri(Path.GetDirectoryName(path)).AbsoluteUri);
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[Export] 書き出し先を開けませんでした: " + ex.Message);
            try { Application.OpenURL(new Uri(Path.GetDirectoryName(path)).AbsoluteUri); } catch (Exception) { }
        }
    }

    static void Start(string file, string arguments)
    {
        using (System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(file, arguments) { UseShellExecute = false, CreateNoWindow = true })) { }
    }

    static string Quote(string value) => "\"" + value.Replace("\"", "\\\"") + "\"";

#if UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
    static string ChooseFolderMac(string initialFolder)
    {
        // Standard Additions shows the macOS folder picker without a native plugin (same route as the 3D model picker).
        using (var process = new System.Diagnostics.Process())
        {
            process.StartInfo.FileName = "/usr/bin/osascript";
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardInput = true;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
            process.StartInfo.StandardOutputEncoding = Encoding.UTF8;
            process.StartInfo.StandardErrorEncoding = Encoding.UTF8;
            process.StartInfo.CreateNoWindow = true;
            process.Start();
            process.StandardInput.WriteLine("try");
            process.StandardInput.WriteLine("return POSIX path of (choose folder with prompt " +
                AppleScriptText("書き出し先のフォルダを選んでください（この中に教材名のフォルダが作られます）") +
                " default location (POSIX file " + AppleScriptText(initialFolder) + "))");
            process.StandardInput.WriteLine("on error number -128");
            process.StandardInput.WriteLine("return \"\"");
            process.StandardInput.WriteLine("end try");
            process.StandardInput.Close();

            string path = process.StandardOutput.ReadToEnd().TrimEnd('\r', '\n');
            string error = process.StandardError.ReadToEnd();
            process.WaitForExit();
            if (process.ExitCode != 0)
                throw new IOException("macOSのフォルダ選択を開けませんでした。 " + error.Trim());
            return path;
        }
    }
#endif

    // Plain ASCII stays literal; everything else becomes (character id N), so no script encoding is involved.
    internal static string AppleScriptText(string value)
    {
        var parts = new System.Collections.Generic.List<string>();
        var run = new StringBuilder();
        void Flush()
        {
            if (run.Length > 0) parts.Add("\"" + run + "\"");
            run.Clear();
        }
        for (int i = 0; i < value.Length; i++)
        {
            char c = value[i];
            if (c >= 0x20 && c <= 0x7E && c != '"' && c != '\\') { run.Append(c); continue; }
            Flush();
            int codePoint = char.IsHighSurrogate(c) && i + 1 < value.Length ? char.ConvertToUtf32(c, value[++i]) : c;
            parts.Add("(character id " + codePoint + ")");
        }
        Flush();
        return parts.Count == 0 ? "\"\"" : string.Join(" & ", parts);
    }
}
