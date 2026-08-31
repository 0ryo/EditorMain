using System;
using System.IO;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public static class CatalogModelImportNaming
{
    public static string BuildImportedTypeId(string assetPath, string displayLabel)
    {
        string stem = string.IsNullOrWhiteSpace(displayLabel)
            ? Path.GetFileNameWithoutExtension(assetPath)
            : displayLabel;
        string sanitized = SanitizeName(stem);
        if (string.IsNullOrWhiteSpace(sanitized))
        {
            sanitized = "Model";
        }

        return $"Imported/{sanitized}_{DateTime.UtcNow.Ticks}";
    }

    public static string GetDefaultName(string assetPath)
    {
        if (string.IsNullOrWhiteSpace(assetPath)) return string.Empty;

        string stem = Path.GetFileNameWithoutExtension(assetPath);
        return string.IsNullOrWhiteSpace(stem) ? string.Empty : stem;
    }

    public static string SanitizeName(string source)
    {
        if (string.IsNullOrWhiteSpace(source)) return string.Empty;

        char[] characters = source.ToCharArray();
        for (int i = 0; i < characters.Length; i++)
        {
            char character = characters[i];
            bool valid = char.IsLetterOrDigit(character) || character == '_' || character == '-';
            if (!valid)
            {
                characters[i] = '_';
            }
        }

        return new string(characters).Trim('_');
    }
}

#if UNITY_EDITOR
public static class EditorModelImportService
{
    const string ImportedAssetFolder = "Assets/ImportedFbx";

    public static string SelectModelPath()
    {
        return EditorUtility.OpenFilePanel("Select 3D Model", GetDefaultModelDirectory(), string.Empty);
    }

    public static bool TryLoadFbxAsset(
        string absolutePath,
        out GameObject prefab,
        out string assetPath,
        out string errorMessage)
    {
        prefab = null;
        assetPath = null;
        errorMessage = null;

        if (string.IsNullOrWhiteSpace(absolutePath))
        {
            errorMessage = "Model path is empty.";
            return false;
        }

        if (!string.Equals(Path.GetExtension(absolutePath), ".fbx", StringComparison.OrdinalIgnoreCase))
        {
            errorMessage = "Please select an .fbx file.";
            return false;
        }

        if (!File.Exists(absolutePath))
        {
            errorMessage = "Selected file does not exist.";
            return false;
        }

        if (!TryToAssetPath(absolutePath, out assetPath))
        {
            string targetDirectory = EnsureImportedAssetFolder();
            string fileStem = CatalogModelImportNaming.SanitizeName(Path.GetFileNameWithoutExtension(absolutePath));
            if (string.IsNullOrWhiteSpace(fileStem))
            {
                fileStem = "ImportedModel";
            }

            string uniqueSuffix = DateTime.UtcNow.Ticks.ToString();
            assetPath = $"{targetDirectory}/{fileStem}_{uniqueSuffix}.fbx";
            FileUtil.CopyFileOrDirectory(absolutePath, assetPath);
        }

        AssetDatabase.ImportAsset(
            assetPath,
            ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
        prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
        if (prefab == null)
        {
            errorMessage = "Failed to import selected FBX.";
            return false;
        }

        return true;
    }

    static string GetDefaultModelDirectory()
    {
        string documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        return Directory.Exists(documents) ? documents : string.Empty;
    }

    static string EnsureImportedAssetFolder()
    {
        if (!AssetDatabase.IsValidFolder(ImportedAssetFolder))
        {
            AssetDatabase.CreateFolder("Assets", "ImportedFbx");
        }

        return ImportedAssetFolder;
    }

    static bool TryToAssetPath(string absolutePath, out string assetPath)
    {
        assetPath = NormalizePath(FileUtil.GetProjectRelativePath(Path.GetFullPath(absolutePath)));
        if (string.IsNullOrWhiteSpace(assetPath) ||
            (!assetPath.Equals("Assets", StringComparison.OrdinalIgnoreCase) &&
             !assetPath.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase)))
        {
            assetPath = null;
            return false;
        }

        return true;
    }

    static string NormalizePath(string path)
    {
        return path.Replace('\\', '/');
    }
}
#endif
