using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

// Most recently opened or saved lessons (newest first), for the start screen. Stored per user; stale paths are skipped.
public static class EditorProjectRecents
{
    const string PrefsKey = "SkillSync.Editor.RecentProjects";
    const int MaxStored = 12;

    // Recently used lessons first; if fewer than count were used on this computer, the most recently saved ones fill the rest.
    public static IReadOnlyList<EditorProjectFileInfo> Get(int count)
    {
        var result = new List<EditorProjectFileInfo>();
        foreach (string path in Read())
        {
            if (result.Count >= count) break;
            if (EditorProjectStore.TryGetFileInfo(path, out var info)) result.Add(info);
        }
        if (result.Count < count)
            foreach (var info in EditorProjectStore.ListRecentlyWritten(count + result.Count))
            {
                if (result.Count >= count) break;
                if (!result.Any(item => string.Equals(item.Path, info.Path, StringComparison.OrdinalIgnoreCase))) result.Add(info);
            }
        return result;
    }

    public static void Add(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        try
        {
            string full = Path.GetFullPath(path);
            var list = Read().Where(item => !string.Equals(item, full, StringComparison.OrdinalIgnoreCase)).ToList();
            list.Insert(0, full);
            if (list.Count > MaxStored) list.RemoveRange(MaxStored, list.Count - MaxStored);
            PlayerPrefs.SetString(PrefsKey, string.Join("\n", list));
            PlayerPrefs.Save();
        }
        catch (Exception ex) { Debug.LogWarning("[EditorProject] 最近使った教材を記録できません: " + ex.Message); }
    }

    static List<string> Read()
    {
        string stored = PlayerPrefs.GetString(PrefsKey, string.Empty);
        return stored.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries).ToList();
    }
}
