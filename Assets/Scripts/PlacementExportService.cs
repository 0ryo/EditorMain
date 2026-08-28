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
        if (TryExportPlacementJson(out var path, out var error))
        {
            Debug.Log($"[Export] Saved: {path}");
            return;
        }

        Debug.LogError("[Export] Save failed: " + error);
    }

    public bool TryExportPlacementJson(out string path, out string error)
    {
        path = null;
        error = null;
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

        string safeProjectName = ExportFileNameUtility.SanitizeProjectName(projectName, "MyProject");
        string fileName = $"{safeProjectName}-placement.json";
        path = RuntimeExportPathUtility.BuildPath(fileName);

        string json = JsonUtility.ToJson(data, true);
        try
        {
            ExportFileWriter.WriteAllTextWithBackup(path, json);
            return true;
        }
        catch (System.Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }
}
