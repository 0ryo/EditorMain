using System.IO;
using System.Text;
using UnityEngine;

public class PlacementExportService : MonoBehaviour
{
    [Header("Meta")]
    public string projectName = "MyProject";

    /// <summary>
    /// フェーズ1-8：配置済みPlacedObjectの一覧をConsoleに出す
    /// </summary>
    public void PrintPlacedObjects()
    {
        var all = FindObjectsOfType<PlacedObject>();
        var sb = new StringBuilder();
        sb.AppendLine($"[Export] PlacedObject count = {all.Length}");

        for (int i = 0; i < all.Length; i++)
        {
            var po = all[i];
            po.EnsureHasId();

            sb.AppendLine(
                $"  [{i}] id={po.id}, typeId={po.typeId}, pos={po.transform.position}, rot={po.transform.rotation}, scale={po.transform.localScale}"
            );
        }

        Debug.Log(sb.ToString());
    }

    public void ExportPlacementJson()
    {
        var data = new PlacementExport
        {
            version = 1,
            projectName = projectName
        };

        var all = FindObjectsOfType<PlacedObject>();
        for (int i = 0; i < all.Length; i++)
        {
            var po = all[i];
            po.EnsureHasId();

            data.objects.Add(new PlacementExportObject
            {
                id = po.id,
                typeId = po.typeId,
                position = po.transform.position,
                rotation = po.transform.rotation,
                scale = po.transform.localScale
            });
        }

        string dir = Path.Combine(Application.dataPath, "Exports");
        Directory.CreateDirectory(dir);

        string safeProjectName = ExportFileNameUtility.SanitizeProjectName(projectName, "MyProject");
        string fileName = $"{safeProjectName}-placement.json";
        string path = Path.Combine(dir, fileName);

        string json = JsonUtility.ToJson(data, true);
        File.WriteAllText(path, json);

        Debug.Log($"[Export] Saved: Assets/Exports/{fileName} (count={data.objects.Count})");
    }
}
