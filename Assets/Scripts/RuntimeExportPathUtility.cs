using System.IO;
using UnityEngine;

public static class RuntimeExportPathUtility
{
    public static string ExportsDirectory =>
        Path.Combine(Application.persistentDataPath, "Exports");

    public static string BuildPath(string fileName)
    {
        return Path.Combine(ExportsDirectory, fileName);
    }
}
