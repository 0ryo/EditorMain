using System;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

// Where each lesson was left: placement or steps, the selected step and condition, the left tab and the 3D view.
// Kept per lesson file in PlayerPrefs (never in the lesson itself), so reopening a lesson continues where it was.
public static class SkillSyncViewMemory
{
    [Serializable]
    public sealed class State
    {
        public int mode;
        public string stepId, conditionId;
        // "手順3を編集中" / "配置を編集中": shown on the start screen's continue card without opening the file.
        public string label;
        public bool placedTab;
        public bool hasCamera, orthographic;
        public Vector3 pivot;
        public float pitch, yaw, distance, size;
    }

    const string Prefix = "EditorMain.ViewState.";

    static string Key(string path)
    {
        if (string.IsNullOrEmpty(path)) return null;
        string full;
        try { full = System.IO.Path.GetFullPath(path).ToLowerInvariant(); } catch (Exception) { full = path.ToLowerInvariant(); }
        using (var md5 = MD5.Create())
        {
            var hash = md5.ComputeHash(Encoding.UTF8.GetBytes(full));
            var builder = new StringBuilder(Prefix, Prefix.Length + 32);
            foreach (var b in hash) builder.Append(b.ToString("x2"));
            return builder.ToString();
        }
    }

    public static State Load(string path)
    {
        string key = Key(path);
        if (key == null) return null;
        try
        {
            string json = PlayerPrefs.GetString(key, "");
            return string.IsNullOrEmpty(json) ? null : JsonUtility.FromJson<State>(json);
        }
        catch (Exception) { return null; }
    }

    public static void Save(string path, State state)
    {
        string key = Key(path);
        if (key == null || state == null) return;
        try { PlayerPrefs.SetString(key, JsonUtility.ToJson(state)); PlayerPrefs.Save(); }
        catch (Exception) { }
    }
}
