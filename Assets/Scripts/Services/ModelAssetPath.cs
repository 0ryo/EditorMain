using System;
using System.IO;

// AssetDatabase requires forward-slash, project-relative paths on Windows too.
public static class ModelAssetPath
{
    public static string FromFile(string source, string assetsDirectory)
    {
        if (string.IsNullOrWhiteSpace(source)) return null;
        string assets = Path.GetFullPath(assetsDirectory).Replace('\\', '/').TrimEnd('/');
        string normalized = source.Replace('\\', '/');
        string absolute = Path.GetFullPath(Path.IsPathRooted(normalized) ? normalized :
            Path.Combine(Path.GetDirectoryName(assets), normalized)).Replace('\\', '/');
        return absolute.StartsWith(assets + "/", StringComparison.OrdinalIgnoreCase)
            ? "Assets/" + absolute.Substring(assets.Length + 1) : null;
    }
}
