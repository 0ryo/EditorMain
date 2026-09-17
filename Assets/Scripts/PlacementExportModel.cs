using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class PlacementExport
{
    public int version = 2;
    public string projectName = "MyProject";
    public List<PlacementExportObject> objects = new List<PlacementExportObject>();
}

[Serializable]
public class PlacementExportObject
{
    // Instantiate this source subtree once, then restore parts on existing child nodes.
    public string sourceNodePath;
    public string sourceSignature;
    public List<ModelPartState> parts = new List<ModelPartState>();
    public string id;
    public string typeId;

    // World（絶対）
    public Vector3 position;
    public Quaternion rotation;

    // Local（将来の拡縮に備えて）
    public Vector3 scale;
}
