using UnityEngine;
using GLTFast;

// Only the loaded catalog prototype owns these resources; clones do not copy this field.
public sealed class RuntimeModelResources : MonoBehaviour
{
    GltfImport import;
    public void Initialize(GltfImport value) => import = value;
    void OnDestroy() { import?.Dispose(); import = null; }
}
