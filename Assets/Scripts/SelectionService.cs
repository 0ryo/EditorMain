using UnityEngine;
using UnityEngine.EventSystems;

public class SelectionService : MonoBehaviour
{
    /// <summary>選択が変わるたびに発火する。null は選択解除を意味する。</summary>
    public event System.Action<PlacedObject> OnSelectionChanged;

    public Camera cam;
    public LayerMask pickMask = ~0;
    public PlacedObject Current;
    public SelectionOutline outline;

    public PrefabRegistry registry;
    public PlacementController placementController;
    public MoveTool moveTool;
    public float pickabilityAutoFixInterval = 1f;
    public bool enableDiagnostics = true;

    float nextPickabilityFixTime;
    bool warnedCameraMissing;
    bool warnedPickMaskExclusion;
    public string LastDebugMessage { get; private set; }

    void Awake()
    {
        EnsureOutline();
    }

    void Update()
    {
        EnsureOutline();

        if (placementController == null)
        {
            placementController = FindFirstObjectByType<PlacementController>();
        }

        if (moveTool == null)
        {
            moveTool = FindFirstObjectByType<MoveTool>();
        }

        if (cam == null)
        {
            cam = EditWorkspace.ResolveCamera();
        }

        AutoFixPickabilityIfNeeded();

        if (Current != null && Current.gameObject == null)
        {
            Select(null);
        }

        var mousePosition = EditInput.MousePosition;
        if (PlacementController.IsScreenPositionOverBlockingUi(mousePosition))
        {
            if (EditInput.LeftPressedThisFrame())
            {
                LogDebug($"Selection click blocked by editor UI. mouse={mousePosition}");
            }
            return;
        }

        if (moveTool != null && moveTool.ShouldConsumeSelectionClick())
        {
            if (EditInput.LeftPressedThisFrame())
            {
                LogDebug("Selection click consumed by MoveTool.");
            }
            return;
        }

        if (outline != null && outline.ShouldConsumeSelectionClick())
        {
            return;
        }

        if (EditInput.LeftPressedThisFrame())
        {
            if (cam == null)
            {
                if (!warnedCameraMissing)
                {
                    warnedCameraMissing = true;
                    LogWarning("Camera is not assigned.");
                }
                return;
            }

            Ray ray = cam.ScreenPointToRay(mousePosition);
            if (TryPickPlacedObject(ray, out var picked, out var hitSomething))
            {
                if (picked != Current)
                {
                    LogDebug($"Picked placed object: id={picked.Id}, name={picked.name}, mouse={mousePosition}");
                }
                Select(picked);
            }
            else if (hitSomething)
            {
                LogDebug($"Selection cleared by non-placed hit. mouse={mousePosition}");
                Select(null);
            }
            else
            {
                LogDebug($"Selection click hit nothing. mouse={mousePosition}");
            }
        }

        if (Current == null) return;

        if (Input.GetKeyDown(KeyCode.Delete))
        {
            System.Func<string, GameObject> factory = (tId) =>
            {
                if (registry != null)
                {
                    var entry = registry.entries.Find(e => e.typeId == tId);
                    if (entry != null && entry.prefab != null)
                    {
                        return InstantiatePlacedForUndo(entry.prefab, tId);
                    }
                }

                if (placementController != null && placementController.TryGetPrefab(tId, out var runtimePrefab))
                {
                    return InstantiatePlacedForUndo(runtimePrefab, tId);
                }

                return null;
            };

            var deleteCmd = new DeleteObjectCommand(Current.gameObject, Current.typeId, factory);
            CommandService.I.Stack.Execute(deleteCmd);

            Select(null);
            return;
        }

        bool controlKey = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
        bool commandKey = Input.GetKey(KeyCode.LeftCommand) || Input.GetKey(KeyCode.RightCommand);

        if ((controlKey || commandKey) && Input.GetKeyDown(KeyCode.D))
        {
            var dup = Instantiate(
                Current.gameObject,
                Current.transform.position + new Vector3(0.2f, 0f, 0.2f),
                Current.transform.rotation
            );

            var po = dup.GetComponent<PlacedObject>();
            if (po == null) po = dup.AddComponent<PlacedObject>();

            if (string.IsNullOrEmpty(po.typeId))
            {
                po.typeId = Current.typeId;
            }

            po.ForceNewId();
            PlacedObjectPickability.EnsurePickable(po, true);

            Select(po);
            return;
        }
    }

    public void Select(PlacedObject po)
    {
        EnsureOutline();
        if (Current == po)
        {
            if (outline != null) outline.ShowFor(po ? po.gameObject : null);
            return;
        }

        Current = po;
        if (outline != null) outline.ShowFor(po ? po.gameObject : null);
        OnSelectionChanged?.Invoke(po);
        LogDebug(po != null ? $"Selected: id={po.Id}, type={po.TypeId}" : "Selection cleared.");
    }

    GameObject InstantiatePlacedForUndo(GameObject prefab, string typeId)
    {
        if (prefab == null || string.IsNullOrWhiteSpace(typeId)) return null;

        var created = Instantiate(prefab);
        var placed = created.GetComponent<PlacedObject>();
        if (placed == null) placed = created.AddComponent<PlacedObject>();

        placed.Init(typeId);
        PlacedObjectPickability.EnsurePickable(placed, true);
        Select(placed);
        return created;
    }

    void AutoFixPickabilityIfNeeded()
    {
        if (pickabilityAutoFixInterval <= 0f) return;
        if (Time.unscaledTime < nextPickabilityFixTime) return;
        nextPickabilityFixTime = Time.unscaledTime + pickabilityAutoFixInterval;

        var allPlaced = FindObjectsByType<PlacedObject>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        int fixedCount = 0;
        foreach (var placed in allPlaced)
        {
            if (PlacedObjectPickability.EnsurePickable(placed))
            {
                fixedCount++;
            }
        }

        if (fixedCount > 0)
        {
            LogDebug($"Auto-fixed pickability. collidersAdded={fixedCount}");
        }
    }

    void EnsureOutline()
    {
        if (outline != null) return;

        outline = FindFirstObjectByType<SelectionOutline>();
        if (outline == null)
        {
            var outlines = FindObjectsByType<SelectionOutline>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (outlines != null && outlines.Length > 0)
            {
                outline = outlines[0];
                outline.gameObject.SetActive(true);
                outline.enabled = true;
            }
        }

        if (outline == null)
        {
            var outlineRoot = new GameObject("SelectionOutlineRoot_Runtime");
            outline = outlineRoot.AddComponent<SelectionOutline>();
        }

        if (Current != null)
        {
            outline.ShowFor(Current.gameObject);
        }
    }

    bool TryPickPlacedObject(Ray ray, out PlacedObject picked, out bool hitSomething)
    {
        picked = null;
        hitSomething = false;

        var hits = Physics.RaycastAll(ray, 1000f, ~0, QueryTriggerInteraction.Collide);
        if (hits == null || hits.Length == 0) return false;
        hitSomething = true;

        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        PlacedObject fallback = null;
        foreach (var hit in hits)
        {
            var collider = hit.collider;
            if (collider == null) continue;

            var placed = collider.GetComponentInParent<PlacedObject>();
            if (placed == null) continue;

            if (fallback == null) fallback = placed;

            if (IsLayerIncluded(collider.gameObject.layer, pickMask) || IsLayerIncluded(placed.gameObject.layer, pickMask))
            {
                picked = placed;
                return true;
            }
        }

        if (fallback != null)
        {
            picked = fallback;
            if (!warnedPickMaskExclusion)
            {
                warnedPickMaskExclusion = true;
                LogWarning($"pickMask excluded selected object layer. picked={fallback.name}");
            }
            return true;
        }

        return false;
    }

    void LogDebug(string message)
    {
        LastDebugMessage = message;
        if (!enableDiagnostics) return;
        Debug.Log("[Selection] " + message);
    }

    void LogWarning(string message)
    {
        LastDebugMessage = message;
        Debug.LogWarning("[Selection] " + message);
    }

    static bool IsLayerIncluded(int layer, LayerMask mask)
    {
        return (mask.value & (1 << layer)) != 0;
    }
}
