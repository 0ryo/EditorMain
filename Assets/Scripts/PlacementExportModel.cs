using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class PlacementExport
{
    public int version = 1;
    public string projectName = "MyProject";
    public List<PlacementExportObject> objects = new();
}

[Serializable]
public class PlacementExportObject
{
    public string id;
    public string typeId;

    // World（絶対）
    public Vector3 position;
    public Quaternion rotation;

    // Local（将来の拡縮に備えて）
    public Vector3 scale;
}
