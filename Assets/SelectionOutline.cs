using UnityEngine;
using System.Collections.Generic;

public class SelectionOutline : MonoBehaviour
{
    public Material lineMat; // URP/Unlit Color 推奨
    List<LineRenderer> lines = new();
    GameObject target;

    void EnsureLines(int count)
    {
        while (lines.Count < count)
        {
            var go = new GameObject("OutlineLine");
            go.transform.SetParent(transform, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.material = lineMat;
            lr.positionCount = 2;
            lr.useWorldSpace = true;

            // 見やすいように太め＋カメラ向き
            lr.widthMultiplier = 0.03f;          // 必要なら 0.05f くらいまで上げてもOK
            lr.alignment = LineAlignment.View;    // カメラに正対
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;

            lines.Add(lr);
        }

        for (int i = 0; i < lines.Count; i++)
        {
            lines[i].gameObject.SetActive(i < count);
        }
    }

    public void ShowFor(GameObject t)
    {
        target = t;
        if (target == null)
        {
            EnsureLines(0);
            return;
        }

        var r = new Bounds(target.transform.position, Vector3.one * 0.5f);
        var renderers = target.GetComponentsInChildren<Renderer>();
        foreach (var rr in renderers) r.Encapsulate(rr.bounds);

        // 8頂点
        Vector3[] v = new Vector3[8];
        Vector3 min = r.min, max = r.max;
        v[0] = new Vector3(min.x, min.y, min.z);
        v[1] = new Vector3(max.x, min.y, min.z);
        v[2] = new Vector3(max.x, min.y, max.z);
        v[3] = new Vector3(min.x, min.y, max.z);
        v[4] = new Vector3(min.x, max.y, min.z);
        v[5] = new Vector3(max.x, max.y, min.z);
        v[6] = new Vector3(max.x, max.y, max.z);
        v[7] = new Vector3(min.x, max.y, max.z);

        // 12辺（下4,上4,縦4）
        Vector3[,] edges = {
            { v[0], v[1] }, { v[1], v[2] }, { v[2], v[3] }, { v[3], v[0] },
            { v[4], v[5] }, { v[5], v[6] }, { v[6], v[7] }, { v[7], v[4] },
            { v[0], v[4] }, { v[1], v[5] }, { v[2], v[6] }, { v[3], v[7] }
        };

        EnsureLines(12);
        for (int i = 0; i < 12; i++)
        {
            lines[i].SetPosition(0, edges[i, 0]);
            lines[i].SetPosition(1, edges[i, 1]);
        }
    }
}
