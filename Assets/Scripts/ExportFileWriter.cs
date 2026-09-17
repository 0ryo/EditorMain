using System;
using System.IO;
using System.Text;

public static class ExportFileWriter
{
    public static void WriteAllTextWithBackup(string finalPath, string contents)
    {
        if (string.IsNullOrWhiteSpace(finalPath))
        {
            throw new ArgumentException("Export path is empty.", nameof(finalPath));
        }

        string resolvedPath = Path.GetFullPath(finalPath);
        string directory = Path.GetDirectoryName(resolvedPath);
        if (string.IsNullOrWhiteSpace(directory))
        {
            throw new InvalidOperationException("Export directory could not be resolved.");
        }

        Directory.CreateDirectory(directory);
        string tempPath = resolvedPath + ".tmp";
        string backupPath = resolvedPath + ".bak";

        try
        {
            File.WriteAllText(tempPath, contents ?? string.Empty, new UTF8Encoding(false));
            if (File.Exists(resolvedPath))
            {
                File.Replace(tempPath, resolvedPath, backupPath);
            }
            else
            {
                File.Move(tempPath, resolvedPath);
            }
        }
        catch
        {
            TryDeleteTempFile(tempPath);
            throw;
        }
    }

    static void TryDeleteTempFile(string tempPath)
    {
        try
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
        catch
        {
            // Preserve the original save exception. A stale temp file is safe to overwrite later.
        }
    }
}
