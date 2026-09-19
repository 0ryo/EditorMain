using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

// Filesystem-only search. Unity objects are inspected before this runs on a worker.
public static class MaterialFileSearch
{
    public sealed class Result
    {
        public readonly List<string> matches = new();
        public readonly List<string> nearby = new();
        public int files, skippedDirectories;
        public bool limited;
    }
    static readonly HashSet<string> Extensions = new(StringComparer.OrdinalIgnoreCase)
    { ".mat", ".mtl", ".png", ".jpg", ".jpeg", ".tga", ".tif", ".tiff", ".bmp", ".dds", ".exr", ".hdr", ".ktx", ".ktx2", ".webp" };

    public static Result Find(string directory, IEnumerable<string> names, CancellationToken cancellation)
    {
        string root = Path.GetFullPath(directory);
        if (!Directory.Exists(root)) throw new DirectoryNotFoundException("検索フォルダーが見つかりません。");
        var expected = new HashSet<string>(names.Where(n => !string.IsNullOrWhiteSpace(n)).Select(Normalize), StringComparer.OrdinalIgnoreCase);
        var result = new Result();
        var pending = new Stack<string>();
        pending.Push(root);
        int directories = 1;
        while (pending.Count > 0)
        {
            cancellation.ThrowIfCancellationRequested();
            if (directories > 4000 || result.files >= 20000) { result.limited = true; break; }
            string folder = pending.Pop();
            try
            {
                // Do not follow junctions/symlinks out of the chosen search tree.
                if ((File.GetAttributes(folder) & FileAttributes.ReparsePoint) != 0) { result.skippedDirectories++; continue; }
                foreach (string path in Directory.EnumerateFileSystemEntries(folder))
                {
                    cancellation.ThrowIfCancellationRequested();
                    var attributes = File.GetAttributes(path);
                    if ((attributes & FileAttributes.ReparsePoint) != 0) continue;
                    if ((attributes & FileAttributes.Directory) != 0)
                    {
                        if (++directories > 4000) { result.limited = true; break; }
                        pending.Push(path);
                        continue;
                    }
                    if (++result.files > 20000) { result.limited = true; break; }
                    if (!Extensions.Contains(Path.GetExtension(path))) continue;
                    bool exact = expected.Contains(Normalize(path));
                    var target = exact ? result.matches : result.nearby;
                    if (target.Count < 100) target.Add(path);
                    else result.limited = true;
                }
            }
            catch (UnauthorizedAccessException) { result.skippedDirectories++; }
            catch (IOException) { result.skippedDirectories++; }
            if (result.files >= 20000 || directories > 4000) { result.limited = true; break; }
        }
        result.matches.Sort(StringComparer.OrdinalIgnoreCase);
        result.nearby.Sort(StringComparer.OrdinalIgnoreCase);
        return result;
    }

    public static string Normalize(string value)
    {
        string name = Path.GetFileName(value.Replace('\\', '/'));
        name = name.Replace(" (Instance)", "").Trim().Normalize(NormalizationForm.FormC);
        return Extensions.Contains(Path.GetExtension(name)) ? Path.GetFileNameWithoutExtension(name) : name;
    }

    public static string Format(Result result)
    {
        var text = new StringBuilder();
        text.AppendLine($"調査: {result.files}ファイル / 名前一致候補: {result.matches.Count}件");
        text.AppendLine("下記は検索候補です。実際の再接続結果は上部に表示します。");
        foreach (var path in result.matches) text.AppendLine("[名前一致] " + path);
        if (result.nearby.Count > 0)
        {
            text.AppendLine("以下は検索範囲内の素材です（対応未確認）。");
            foreach (var path in result.nearby) text.AppendLine(path);
        }
        if (result.matches.Count == 0 && result.nearby.Count == 0) text.AppendLine("マテリアル・画像ファイルが見つかりませんでした。");
        if (result.limited) text.AppendLine("検索・表示上限に達しました。フォルダーを絞って再検索してください。");
        if (result.skippedDirectories > 0) text.AppendLine($"読取不可・リンク等で省略: {result.skippedDirectories}箇所");
        return text.ToString();
    }
}
