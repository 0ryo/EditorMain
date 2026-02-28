using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class PlacementController : MonoBehaviour
{
    public PrefabRegistry registry;
    public Camera cam;
    public float gridSize = 0.1f;
    public LayerMask floorMask;
    public SelectionService selection;

    string currentTypeId;
    Dictionary<string, GameObject> map;
    static bool uiDragInProgress;

    public static void SetUiDragInProgress(bool isDragging)
    {
        uiDragInProgress = isDragging;
    }

    void Awake()
    {
        RebuildTypeMapFromRegistry();
    }

    void RebuildTypeMapFromRegistry()
    {
        map = new Dictionary<string, GameObject>();

        if (registry == null)
        {
            Debug.LogError("PlacementController: registry not set");
            return;
        }

        foreach (var entry in registry.entries)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.typeId) || entry.prefab == null) continue;
            if (!map.ContainsKey(entry.typeId))
            {
                map.Add(entry.typeId, entry.prefab);
            }
        }

        Debug.Log($"[Placement] Registry loaded. entries={map.Count}");
    }

    void EnsureTypeMap()
    {
        if (map != null) return;
        map = new Dictionary<string, GameObject>();
    }

    public bool RegisterRuntimePrefab(string typeId, GameObject prefab)
    {
        if (string.IsNullOrWhiteSpace(typeId) || prefab == null) return false;

        EnsureTypeMap();
        map[typeId] = prefab;
        Debug.Log($"[Placement] Runtime prefab registered: {typeId}");
        return true;
    }

    public bool TryGetPrefab(string typeId, out GameObject prefab)
    {
        prefab = null;
        if (string.IsNullOrWhiteSpace(typeId)) return false;

        EnsureTypeMap();
        return map.TryGetValue(typeId, out prefab) && prefab != null;
    }

    void CancelPlacement()
    {
        if (!string.IsNullOrEmpty(currentTypeId))
        {
            Debug.Log($"[Placement] CancelPlacement: {currentTypeId}");
        }
        currentTypeId = null;
    }

    public void EnterPlacement(string typeId)
    {
        CancelPlacement();

        if (string.IsNullOrEmpty(typeId))
        {
            Debug.LogWarning("[Placement] EnterPlacement called with null/empty typeId");
            return;
        }

        if (!TryGetPrefab(typeId, out _))
        {
            Debug.LogWarning($"[Placement] EnterPlacement NG: {typeId} is not registered");
            return;
        }

        currentTypeId = typeId;
        if (EditModeService.I != null)
        {
            EditModeService.I.SetMode(EditMode.Place);
        }

        Debug.Log($"[Placement] EnterPlacement OK: {currentTypeId}");
    }

    public bool PlaceOnceAtScreenPoint(string typeId, Vector2 screenPosition)
    {
        if (string.IsNullOrWhiteSpace(typeId)) return false;
        if (cam == null)
        {
            Debug.LogWarning("[Placement] PlaceOnceAtScreenPoint failed. Camera is null.");
            return false;
        }

        if (!TryRaycastFloor(screenPosition, out var hit))
        {
            Debug.LogWarning("[Placement] PlaceOnceAtScreenPoint failed. Floor raycast did not hit.");
            return false;
        }

        return PlaceType(typeId, hit.point);
    }

    void Update()
    {
        if (string.IsNullOrEmpty(currentTypeId)) return;
        if (uiDragInProgress) return;

        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        {
            return;
        }

        if (!Input.GetMouseButtonDown(0)) return;

        if (PlaceOnceAtScreenPoint(currentTypeId, Input.mousePosition))
        {
            CancelPlacement();
        }
    }

    bool TryRaycastFloor(Vector2 screenPosition, out RaycastHit hit)
    {
        hit = default;
        if (cam == null) return false;

        Ray ray = cam.ScreenPointToRay(screenPosition);
        return Physics.Raycast(ray, out hit, 1000f, floorMask);
    }

    bool PlaceType(string typeId, Vector3 floorPoint)
    {
        if (!TryGetPrefab(typeId, out var prefab))
        {
            Debug.LogWarning($"[Placement] PlaceType failed. {typeId} is not registered.");
            return false;
        }

        var placedPosition = floorPoint;
        placedPosition.x = Mathf.Round(placedPosition.x / gridSize) * gridSize;
        placedPosition.z = Mathf.Round(placedPosition.z / gridSize) * gridSize;
        placedPosition.y = floorPoint.y + 0.5f;

        System.Func<string, GameObject> factory = (tId) =>
        {
            if (!TryGetPrefab(tId, out var sourcePrefab)) return null;

            var obj = Object.Instantiate(sourcePrefab);
            var placed = obj.GetComponent<PlacedObject>();
            if (placed == null) placed = obj.AddComponent<PlacedObject>();

            placed.InitType(tId);
            placed.ForceNewId();
            PlacedObjectPickability.EnsurePickable(placed, true);

            if (selection != null)
            {
                selection.Select(placed);
            }

            return obj;
        };

        var cmd = new PlaceObjectCommand(typeId, placedPosition, Quaternion.identity, factory);
        CommandService.I.Stack.Execute(cmd);
        Debug.Log($"[Placement] Placed {typeId} at {placedPosition} via Command");
        return true;
    }
}

public class PlacedObject : MonoBehaviour
{
    public string id;
    public string typeId;

    static int fallbackSeq;

    public string Id => id;
    public string TypeId => typeId;

    public void InitType(string t)
    {
        typeId = t;
    }

    public void EnsureHasId()
    {
        if (!string.IsNullOrEmpty(id)) return;

        if (IdGenerator.I != null)
        {
            id = IdGenerator.I.NewObjectId();
            return;
        }

        id = "obj-" + (++fallbackSeq).ToString("D4");
        Debug.LogWarning("[PlacedObject] IdGenerator not found. Fallback sequence is used.");
    }

    public void ForceNewId()
    {
        id = null;
        EnsureHasId();
    }

    public void Init(string t)
    {
        InitType(t);
        ForceNewId();
    }
}
