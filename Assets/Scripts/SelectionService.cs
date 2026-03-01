using UnityEngine;
using UnityEngine.EventSystems;

public class SelectionService : MonoBehaviour
{
    public Camera cam;
    public LayerMask pickMask = ~0;
    public PlacedObject Current;
    public SelectionOutline outline;

    public PrefabRegistry registry;
    public PlacementController placementController;
    public MoveTool moveTool;
    public float pickabilityAutoFixInterval = 1f;

    float nextPickabilityFixTime;
    bool warnedCameraMissing;
    bool warnedPickMaskExclusion;

    void Update()
    {
        if (placementController == null)
        {
            placementController = FindFirstObjectByType<PlacementController>();
        }

        if (moveTool == null)
        {
            moveTool = FindFirstObjectByType<MoveTool>();
        }

        AutoFixPickabilityIfNeeded();

        if (Current != null && Current.gameObject == null)
        {
            Select(null);
        }

        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;
        if (moveTool != null && moveTool.ShouldConsumeSelectionClick()) return;

        if (Input.GetMouseButtonDown(0))
        {
            if (cam == null)
            {
                if (!warnedCameraMissing)
                {
                    warnedCameraMissing = true;
                    Debug.LogWarning("[Selection] Camera is not assigned.");
                }
                return;
            }

            Ray ray = cam.ScreenPointToRay(Input.mousePosition);
            if (TryPickPlacedObject(ray, out var picked, out var hitSomething))
            {
                Select(picked);
            }
            else if (hitSomething)
            {
                Select(null);
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
        Current = po;
        if (outline != null) outline.ShowFor(po ? po.gameObject : null);
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
            Debug.Log($"[Selection] Auto-fixed pickability. collidersAdded={fixedCount}");
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
                Debug.LogWarning($"[Selection] pickMask excluded selected object layer. picked={fallback.name}");
            }
            return true;
        }

        return false;
    }

    static bool IsLayerIncluded(int layer, LayerMask mask)
    {
        return (mask.value & (1 << layer)) != 0;
    }
}
