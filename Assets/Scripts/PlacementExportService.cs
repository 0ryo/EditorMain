using UnityEngine;

public class PlacementExportService : MonoBehaviour
{
    [Header("Meta")]
    public string projectName = "MyProject";

    public bool TryExportPlacementJson(out string path, out string error)
    {
        path = null;
        error = null;
        var data = new PlacementExport
        {
            version = 2,
            projectName = projectName
        };

        var all = FindObjectsByType<PlacedObject>(FindObjectsSortMode.InstanceID);
        for (int i = 0; i < all.Length; i++)
        {
            var po = all[i];
            if (po.modelRoot != null) continue;
            po.EnsureHasId();

            data.objects.Add(new PlacementExportObject
            {
                id = po.id,
                sourceNodePath = po.sourceNodePath,
                sourceSignature = po.sourceSignature,
                parts = ImportedModelParts.Capture(po),
                typeId = po.typeId,
                displayName = po.GetDisplayName(),
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
