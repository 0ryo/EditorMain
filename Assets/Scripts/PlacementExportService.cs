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
            // 念のため id を保証（EnsureHasId がある前提）
            po.EnsureHasId();

            sb.AppendLine(
                $"  [{i}] id={po.id}, typeId={po.typeId}, pos={po.transform.position}, rot={po.transform.rotation}, scale={po.transform.localScale}"
            );
        }

        Debug.Log(sb.ToString());
    }
}
