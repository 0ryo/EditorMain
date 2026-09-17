using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;

public class SelectionService : MonoBehaviour
{
    /// <summary>選択が変わるたびに発火する。null は選択解除を意味する。</summary>
    public event System.Action<PlacedObject> OnSelectionChanged;

    public Camera cam;
    public LayerMask pickMask = ~0;
    public PlacedObject Current;
    readonly List<PlacedObject> selected = new();
    public IReadOnlyList<PlacedObject> Selected => selected;
    public bool Contains(PlacedObject item) => selected.Contains(item);
    public static bool AdditiveSelection => Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift) ||
        Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
    public static bool CanEdit(PlacedObject item)
    {
        if (item == null || !item.gameObject.activeInHierarchy) return false;
        for (var node = item.transform; node != null; node = node.parent)
        {
            var state = node.GetComponent<PlacedObjectEditState>();
            if (state != null && (state.Hidden || state.Locked)) return false;
        }
        return true;
    }
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
        if (CanEdit(Current)) selected.Add(Current);
        EnsureOutline();
    }

    readonly SelectionHighlightSet highlights = new();

    void LateUpdate()
    {
        highlights.Refresh(this, selected);
    }

    void OnDisable() { highlights.Clear(); }
    void OnDestroy() { highlights.Clear(); }

    void Update()
    {
        if (ObjectScreenPicker.Capturing) return;
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

        if (selected.RemoveAll(item => !CanEdit(item)) > 0) PublishSelection();

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
                if (AdditiveSelection || !Contains(picked)) Select(picked, AdditiveSelection);
                else if (picked != Current)
                {
                    selected.Remove(picked);
                    selected.Add(picked);
                    PublishSelection();
                }
            }
            else if (hitSomething)
            {
                LogDebug($"Selection cleared by non-placed hit. mouse={mousePosition}");
                if (!AdditiveSelection) Select(null);
            }
            else
            {
                LogDebug($"Selection cleared by empty workspace click. mouse={mousePosition}");
                if (!AdditiveSelection) Select(null);
            }
        }

        if (Current == null) return;
        if (EditWorkspace.IsTypingIntoInputField()) return;

        if (Input.GetKeyDown(KeyCode.Delete)) { DeleteSelected(); return; }
        bool modifier = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl) ||
            Input.GetKey(KeyCode.LeftCommand) || Input.GetKey(KeyCode.RightCommand);
        if (modifier && Input.GetKeyDown(KeyCode.D)) DuplicateSelected();
    }

    public void Select(PlacedObject po) => Select(po, false);

    public void Select(PlacedObject po, bool additive)
    {
        if (!additive) selected.Clear();
        if (CanEdit(po))
        {
            if (additive && selected.Contains(po)) selected.Remove(po);
            else if (!selected.Any(parent => po.transform.IsChildOf(parent.transform)))
            {
                // A selection is an antichain: parent motion already transforms its children.
                selected.RemoveAll(child => child.transform.IsChildOf(po.transform));
                selected.Add(po);
            }
        }
        PublishSelection();
    }

    void PublishSelection()
    {
        Current = selected.LastOrDefault();
        EnsureOutline();
        if (outline != null) outline.ShowFor(Current != null ? Current.gameObject : null);
        OnSelectionChanged?.Invoke(Current);
    }

    public void DeleteSelected()
    {
        var commands = selected.Where(CanEdit).Select(item => (IEditorCommand)new DeleteObjectCommand(
            item.gameObject, item.typeId, typeId => PlacedObjectRestoreFactory.Create(typeId, registry, placementController))).ToList();
        if (commands.Count == 0) return;
        if (Execute(new CompositeEditorCommand("Delete selection", commands))) Select(null);
    }

    public void DuplicateSelected()
    {
        var commands = selected.Where(CanEdit).Select(item => new DuplicateObjectCommand(
            item.gameObject, new Vector3(0.2f, 0f, 0.2f))).ToList();
        if (commands.Count == 0 || !Execute(new CompositeEditorCommand("Duplicate selection", commands))) return;
        selected.Clear();
        selected.AddRange(commands.Select(command => command.Result).Where(item => item != null));
        PublishSelection();
    }

    public void Align(int axis, bool distribute)
    {
        if (axis < 0 || axis > 2) return;
        var items = selected.Where(CanEdit).OrderBy(item => item.transform.position[axis]).ToList();
        if (items.Count < (distribute ? 3 : 2) || Current == null) return;
        float first = items[0].transform.position[axis];
        float last = items[items.Count - 1].transform.position[axis];
        var commands = new List<IEditorCommand>();
        for (int i = 0; i < items.Count; i++)
        {
            Vector3 from = items[i].transform.position;
            Vector3 to = from;
            to[axis] = distribute ? Mathf.Lerp(first, last, (float)i / (items.Count - 1)) : Current.transform.position[axis];
            if ((to - from).sqrMagnitude > 0.00000001f) commands.Add(new MoveObjectCommand(items[i].gameObject, from, to));
        }
        if (commands.Count > 0) Execute(new CompositeEditorCommand(distribute ? "Distribute selection" : "Align selection", commands));
    }

    static bool Execute(IEditorCommand command) => CommandService.I != null
        ? CommandService.I.Stack.Execute(command) : command.Do();

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
