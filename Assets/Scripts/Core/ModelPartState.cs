using System;
using UnityEngine;

[Serializable]
public sealed class ModelPartState
{
    public string nodePath;
    public string sourceSignature;
    public string id;
    public string displayName;
    public string description;
    public string editorGroupId;
    public bool hasDescriptionOverride;
    public Vector3 localPosition;
    public Quaternion localRotation = Quaternion.identity;
    public Vector3 localScale = Vector3.one;
    public bool active = true;
    public bool hidden;
    public bool locked;
}

