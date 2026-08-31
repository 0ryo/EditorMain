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
    public bool enableDiagnostics = true;

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

        if (Current != null && !Current.gameObject.activeInHierarchy)
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
            bool pickedPlacedObject = PlacedObjectPicker.TryPick(
                ray,
                pickMask,
                out var picked,
                out var hitSomething,
                out var usedMaskFallback);
            if (usedMaskFallback && !warnedPickMaskExclusion)
            {
                warnedPickMaskExclusion = true;
                LogWarning($"pickMask excluded selected object layer. picked={picked.name}");
            }

            if (pickedPlacedObject)
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
                LogDebug($"Selection cleared by empty workspace click. mouse={mousePosition}");
                Select(null);
            }
        }

        if (Current == null) return;
        if (EditWorkspace.IsTypingIntoInputField()) return;

        if (Input.GetKeyDown(KeyCode.Delete))
        {
            System.Func<string, GameObject> factory = (tId) =>
            {
                return PlacedObjectRestoreFactory.Create(tId, registry, placementController);
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
            var duplicateCmd = new DuplicateObjectCommand(
                Current.gameObject,
                new Vector3(0.2f, 0f, 0.2f)
            );

            if (CommandService.I != null && CommandService.I.Stack != null)
            {
                CommandService.I.Stack.Execute(duplicateCmd);
            }
            else
            {
                duplicateCmd.Do();
                LogWarning("Duplicate applied without undo because CommandService is missing.");
            }

            var po = duplicateCmd.Result;
            if (po == null) return;

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

}
