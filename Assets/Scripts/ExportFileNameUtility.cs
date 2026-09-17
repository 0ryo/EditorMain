using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

public static class ExportFileNameUtility
{
    const string DefaultProjectName = "Project";
    const int MaxProjectNameLength = 96;
    const string WindowsInvalidCharacters = "<>:\"/\\|?*";

    static readonly HashSet<string> WindowsReservedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
    };

    public static string SanitizeProjectName(string projectName, string fallback = DefaultProjectName)
    {
        string sanitized = SanitizeCore(projectName);
        if (string.IsNullOrWhiteSpace(sanitized))
        {
            sanitized = SanitizeCore(fallback);
        }

        if (string.IsNullOrWhiteSpace(sanitized))
        {
            sanitized = DefaultProjectName;
        }

        if (IsWindowsReservedName(sanitized))
        {
            sanitized = "_" + sanitized;
        }

        if (sanitized.Length > MaxProjectNameLength)
        {
            sanitized = sanitized.Substring(0, MaxProjectNameLength).TrimEnd(' ', '.');
        }

        return string.IsNullOrWhiteSpace(sanitized) ? DefaultProjectName : sanitized;
    }

    static string SanitizeCore(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;

        char[] platformInvalidCharacters = Path.GetInvalidFileNameChars();
        var result = new StringBuilder(value.Length);
        bool previousWasReplacement = false;

        foreach (char character in value.Trim())
        {
            bool invalid = char.IsControl(character) ||
                           WindowsInvalidCharacters.IndexOf(character) >= 0 ||
                           Array.IndexOf(platformInvalidCharacters, character) >= 0;
            if (invalid)
            {
                if (!previousWasReplacement)
                {
                    result.Append('_');
                    previousWasReplacement = true;
                }
                continue;
            }

            result.Append(character);
            previousWasReplacement = false;
        }

        return result.ToString().Trim().Trim(' ', '.');
    }

    static bool IsWindowsReservedName(string value)
    {
        int extensionSeparator = value.IndexOf('.');
        string deviceName = extensionSeparator >= 0 ? value.Substring(0, extensionSeparator) : value;
        return WindowsReservedNames.Contains(deviceName);
    }
}
