using System.Collections.Generic;
using System;
using UnityEngine;

public class PlacementController : MonoBehaviour
{
    static readonly string[] BlockingUiRectNames =
    {
        "Panel_Catalog",
        "Panel_Settings",
        "Panel_NewObjectSettings",
        "EditModeRow",
        "EditModeRow_Runtime",
        "Button_Settings",
        "Button_Settings_Runtime"
    };

    public PrefabRegistry registry;
    public Camera cam;
    public float gridSize = 0.1f;
    public float placementYOffset = 0.5f;
    public SelectionService selection;

    string currentTypeId;
    Dictionary<string, GameObject> map;
    static bool uiDragInProgress;
    public event Action<string> PlacementTypeChanged;
    public event Action<PlacedObject, string> ObjectPlaced;
    public string CurrentTypeId => currentTypeId;
    public string LastDebugMessage { get; private set; }

    public static void SetUiDragInProgress(bool isDragging)
    {
        uiDragInProgress = isDragging;
    }

    void Awake()
    {
        EnsureCameraAssigned();
        EnsureRegistryAssigned();
        RebuildTypeMapFromRegistry();
        EditWorkspace.EnsureWorkspaceVisuals();
    }

    void Start()
    {
        EnsureCameraAssigned();
        EditWorkspace.EnsureWorkspaceVisuals();
    }

    void EnsureCameraAssigned()
    {
        cam = EditWorkspace.ResolveCamera(cam);
    }

    void EnsureRegistryAssigned()
    {
        if (registry != null && registry.HasEntries) return;

        var defaultRegistry = PrefabRegistry.LoadDefault();
        if (defaultRegistry == null || !defaultRegistry.HasEntries) return;

        registry = defaultRegistry;
        Debug.Log($"[Placement] Bound default registry: {PrefabRegistry.DefaultAssetPath}");
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
        EnsureRegistryAssigned();
        if (map != null && map.Count > 0) return;
        RebuildTypeMapFromRegistry();
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
            LogDebug($"CancelPlacement: {currentTypeId}");
        }
        SetCurrentTypeId(null);
    }

    public void EnterPlacement(string typeId)
    {
        CancelPlacement();

        if (string.IsNullOrEmpty(typeId))
        {
            LogWarning("EnterPlacement called with null/empty typeId");
            return;
        }

        if (!TryGetPrefab(typeId, out _))
        {
            LogWarning($"EnterPlacement NG: {typeId} is not registered");
            return;
        }

        SetCurrentTypeId(typeId);
        if (EditModeService.I != null)
        {
            EditModeService.I.SetMode(EditMode.Place);
        }

        LogDebug($"EnterPlacement OK: {currentTypeId}. Click the 3D viewport to place.");
    }

    void SetCurrentTypeId(string typeId)
    {
        if (string.Equals(currentTypeId, typeId, StringComparison.Ordinal)) return;
        currentTypeId = typeId;
        PlacementTypeChanged?.Invoke(currentTypeId);
    }

    public bool PlaceOnceAtScreenPoint(string typeId, Vector2 screenPosition)
    {
        if (string.IsNullOrWhiteSpace(typeId)) return false;
        EnsureCameraAssigned();
        if (cam == null)
        {
            LogWarning("PlaceOnceAtScreenPoint failed. Camera is null.");
            return false;
        }

        if (!TryGetPlacementPoint(screenPosition, out var placementPoint, out var resolveReason))
        {
            LogWarning($"PlaceOnceAtScreenPoint failed. Could not resolve placement point. screen={screenPosition}");
            return false;
        }

        LogDebug($"Placement point resolved by {resolveReason}: {placementPoint}");
        return PlaceType(typeId, placementPoint);
    }

    void Update()
    {
        if (string.IsNullOrEmpty(currentTypeId)) return;
        if (uiDragInProgress) return;

        if (!Input.GetMouseButtonDown(0)) return;

        if (EditWorkspace.TryGetBlockingUiName(Input.mousePosition, BlockingUiRectNames, out var blockingUiName))
        {
            LogDebug($"Placement click blocked by UI: {blockingUiName}, screen={Input.mousePosition}");
            return;
        }

        LogDebug($"Placement click accepted: type={currentTypeId}, screen={Input.mousePosition}");

        if (PlaceOnceAtScreenPoint(currentTypeId, Input.mousePosition))
        {
            CancelPlacement();
        }
    }

    public static bool IsScreenPositionOverBlockingUi(Vector2 screenPosition)
    {
        return EditWorkspace.TryGetBlockingUiName(screenPosition, BlockingUiRectNames, out _);
    }

    bool TryGetPlacementPoint(Vector2 screenPosition, out Vector3 point, out string resolveReason)
    {
        EnsureCameraAssigned();
        return EditWorkspace.TryScreenToGround(cam, screenPosition, out point, out resolveReason);
    }

    bool PlaceType(string typeId, Vector3 floorPoint)
    {
        if (!TryGetPrefab(typeId, out var prefab))
        {
            LogWarning($"PlaceType failed. {typeId} is not registered.");
            return false;
        }

        var placedPosition = EditWorkspace.SnapPlacementPoint(floorPoint, gridSize, placementYOffset);

        PlacedObject createdPlacedObject = null;
        GameObject createdObject = null;
        System.Func<string, GameObject> factory = tId =>
        {
            createdObject = CreatePlacedObject(tId, out createdPlacedObject);
            return createdObject;
        };

        var cmd = new PlaceObjectCommand(typeId, placedPosition, Quaternion.identity, factory);
        if (CommandService.I != null && CommandService.I.Stack != null)
        {
            CommandService.I.Stack.Execute(cmd);
        }
        else
        {
            LogWarning("CommandService is missing. Placing object directly without undo stack.");
            createdObject = factory(typeId);
            if (createdObject == null)
            {
                LogWarning($"Direct placement failed. Factory returned null: {typeId}");
                return false;
            }
            createdObject.transform.SetPositionAndRotation(placedPosition, Quaternion.identity);
        }

        if (createdObject == null || createdPlacedObject == null)
        {
            LogWarning($"PlaceType failed. Object was not created: {typeId}");
            return false;
        }

        if (selection != null)
        {
            selection.Select(createdPlacedObject);
        }

        ObjectPlaced?.Invoke(createdPlacedObject, typeId);

        LogDebug($"Placed OK: type={typeId}, id={createdPlacedObject?.Id ?? "(unknown)"}, position={placedPosition}");
        return true;
    }

    GameObject CreatePlacedObject(string typeId, out PlacedObject placed)
    {
        placed = null;
        if (!TryGetPrefab(typeId, out var sourcePrefab)) return null;

        var obj = Instantiate(sourcePrefab);
        if (!obj.activeSelf)
        {
            obj.SetActive(true);
        }

        placed = obj.GetComponent<PlacedObject>();
        if (placed == null) placed = obj.AddComponent<PlacedObject>();

        placed.InitType(typeId);
        placed.ForceNewId();
        PlacedObjectPickability.EnsurePickable(placed, true);
        return obj;
    }

    void LogDebug(string message)
    {
        LastDebugMessage = message;
        Debug.Log("[Placement] " + message);
    }

    void LogWarning(string message)
    {
        LastDebugMessage = message;
        Debug.LogWarning("[Placement] " + message);
    }
}

public class PlacedObject : MonoBehaviour
{
    public string id;
    public string typeId;
    /// <summary>
    /// ユーザーが設定した表示名。未設定の場合は id (obj-0001) を返す。
    /// Condition ノードのドロップダウンなど、UI 表示には GetDisplayName() を使用する。
    /// </summary>
    public string displayName;

    /// <summary>表示名が変更されたときに発火する。購読側はドロップダウン等を再描画する。</summary>
    public static event System.Action<PlacedObject> OnDisplayNameChanged;

    static int fallbackSeq;

    public string Id => id;
    public string TypeId => typeId;

    /// <summary>displayName が設定されていれば返し、未設定なら id を返す。</summary>
    public string GetDisplayName() =>
        string.IsNullOrWhiteSpace(displayName) ? id : displayName;

    /// <summary>表示名を更新し OnDisplayNameChanged を発火する。</summary>
    public void SetDisplayName(string name)
    {
        displayName = name?.Trim() ?? string.Empty;
        OnDisplayNameChanged?.Invoke(this);
    }

    public string description;
    public bool hasDescriptionOverride;

    public string GetDescription() =>
        description ?? string.Empty;

    public string GetDisplayDescription(string fallback)
    {
        if (hasDescriptionOverride) return GetDescription();
        if (!string.IsNullOrWhiteSpace(description)) return description;
        return fallback ?? string.Empty;
    }

    public void SetDescription(string value)
    {
        description = value?.Trim() ?? string.Empty;
        hasDescriptionOverride = true;
    }

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
