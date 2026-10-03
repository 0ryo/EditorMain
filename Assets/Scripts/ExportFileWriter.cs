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
        try
        {
            File.WriteAllText(tempPath, contents ?? string.Empty, new UTF8Encoding(false));
            PublishTempFileWithBackup(tempPath, resolvedPath);
        }
        catch
        {
            TryDeleteTempFile(tempPath);
            throw;
        }
    }

    // The caller creates and closes the temporary file in the destination directory.
    public static void PublishTempFileWithBackup(string tempPath, string finalPath)
    {
        if (File.Exists(finalPath)) File.Replace(tempPath, finalPath, finalPath + ".bak");
        else File.Move(tempPath, finalPath);
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
