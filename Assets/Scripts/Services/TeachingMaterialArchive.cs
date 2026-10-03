using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using UnityEngine;

public static class TeachingMaterialArchive
{
    public static string GetPath(string jsonPath)
    {
        return Path.ChangeExtension(jsonPath, ".zip");
    }

    public static void Write(ScenarioExport export, string jsonPath)
    {
        string directory = Path.GetDirectoryName(Path.GetFullPath(jsonPath));
        Directory.CreateDirectory(directory);
        string path = GetPath(jsonPath);
        string tempPath = path + ".tmp";
        try
        {
            using (var stream = File.Create(tempPath))
            using (var archive = new ZipArchive(stream, ZipArchiveMode.Create))
            {
                var jsonEntry = archive.CreateEntry(Path.GetFileName(jsonPath));
                using (var writer = new StreamWriter(jsonEntry.Open(), new UTF8Encoding(false)))
                    writer.Write(JsonUtility.ToJson(export, true));

                var files = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                if (!string.IsNullOrEmpty(export.prefabPackage)) files.Add(Within(directory, export.prefabPackage));
                foreach (var model in export.models)
                {
                    if (string.IsNullOrEmpty(model.uri)) continue;
                    string modelPath = Within(directory, model.uri);
                    if (!File.Exists(modelPath)) throw new FileNotFoundException("書き出すモデルがありません。", model.uri);
                    // The model's folder also contains the referenced glTF buffers and textures.
                    foreach (string file in Directory.GetFiles(Path.GetDirectoryName(modelPath), "*", SearchOption.AllDirectories))
                        files.Add(file);
                }
                foreach (string file in files)
                {
                    string relative = file.Substring(directory.Length + 1).Replace('\\', '/');
                    archive.CreateEntryFromFile(file, relative, System.IO.Compression.CompressionLevel.Fastest);
                }
            }
            ExportFileWriter.PublishTempFileWithBackup(tempPath, path);
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }

    static string Within(string directory, string relative)
    {
        if (Path.IsPathRooted(relative)) throw new IOException("配布素材には相対パスが必要です。");
        string path = Path.GetFullPath(Path.Combine(directory, relative));
        if (!path.StartsWith(directory + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new IOException("出力先の外側の素材は同梱できません。");
        return path;
    }
}
