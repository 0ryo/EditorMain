using System;
using System.Globalization;
using System.IO;
using UnityEngine;

public static class RuntimeExportPathUtility
{
    public static string ExportsDirectory
    {
        get
        {
#if UNITY_EDITOR
            // Keep deliverables beside Assets, outside Unity's import pipeline.
            return Path.Combine(Path.GetDirectoryName(Application.dataPath), "Exports");
#else
            string documents = System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments);
            string root = string.IsNullOrWhiteSpace(documents) ? Application.persistentDataPath : documents;
            return Path.Combine(root, "SkillSync", "Exports");
#endif
        }
    }

    public static string BuildPath(string fileName)
    {
        return Path.Combine(ExportsDirectory, fileName);
    }

    public static string CreateTeachingMaterialDirectory(string outputDirectory, DateTime savedAt)
    {
        string root = Path.GetFullPath(outputDirectory);
        Directory.CreateDirectory(root);
        string name = "XR教材データ-" + savedAt.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);
        string staging = Path.Combine(root, ".XR-" + Guid.NewGuid().ToString("N") + ".tmp");
        Directory.CreateDirectory(staging);
        try
        {
            for (int index = 1; ; index++)
            {
                string suffix = index == 1 ? string.Empty : "-" + index.ToString(CultureInfo.InvariantCulture);
                string target = Path.Combine(root, name + suffix);
                try
                {
                    // Moving an owned empty folder reserves the name without reusing an existing export.
                    Directory.Move(staging, target);
                    return target;
                }
                catch (IOException) when (Directory.Exists(target) || File.Exists(target))
                {
                    // Exports within the same second, including another process, get their own folder.
                }
            }
        }
        finally
        {
            if (Directory.Exists(staging)) Directory.Delete(staging);
        }
    }
}
