using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;

public class SelectionService : MonoBehaviour
{
    /// <summary>選択が変わるたびに発火する。null は選択解除を意味する。</summary>
    public event System.Action<PlacedObject> OnSelectionChanged;
    public event System.Action<string> OperationMessage;

    public Camera cam;
    public LayerMask pickMask = ~0;
    public PlacedObject Current;
    readonly List<PlacedObject> selected = new();
    readonly SelectionClipboard clipboard = new();
    public IReadOnlyList<PlacedObject> Selected => selected;
    public bool Contains(PlacedObject item) => selected.Contains(item);
    public void ReportOperationMessage(string message) => OperationMessage?.Invoke(message);
    public static bool AdditiveSelection => Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift) ||
        Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl) ||
        Input.GetKey(KeyCode.LeftCommand) || Input.GetKey(KeyCode.RightCommand);
    public static bool CanEdit(PlacedObject item)
    {
        if (item == null || !item.gameObject.activeInHierarchy) return false;
        for (var node = item.transform; node != null; node = node.parent)
        {
            var placed = node.GetComponent<PlacedObject>();
            if (placed != null && !PlacedObjectEditState.IsVisibleByCategory(placed)) return false;
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
    float nextDependencyResolveTime;
    bool regionPending;
    bool regionDragging;
    bool regionAdditive;
    Vector2 regionStart;
    SelectionRegionOverlay regionOverlay;
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

    void OnDisable() { CancelRegion(); highlights.Clear(); }
    void OnDestroy() { regionOverlay?.Dispose(); highlights.Clear(); }

    void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus) CancelRegion();
    }

    void Update()
    {
        if (ObjectScreenPicker.Capturing) { CancelRegion(); return; }
        EnsureOutline();

        if (Time.unscaledTime >= nextDependencyResolveTime)
        {
            nextDependencyResolveTime = Time.unscaledTime + 0.5f;
            if (placementController == null)
                placementController = FindFirstObjectByType<PlacementController>();

            if (moveTool == null)
                moveTool = FindFirstObjectByType<MoveTool>();
        }

        if (cam == null)
        {
            cam = EditWorkspace.ResolveCamera();
        }

        if (selected.RemoveAll(item => !CanEdit(item)) > 0) PublishSelection();

        var mousePosition = EditInput.MousePosition;
        if (!Application.isFocused || EditWorkspace.IsTypingIntoInputField() ||
            (placementController != null && !string.IsNullOrEmpty(placementController.CurrentTypeId)))
        {
            CancelRegion();
            return;
        }
        if (regionPending)
        {
            UpdateRegion(mousePosition);
            return;
        }
        if (mousePosition.x < 0 || mousePosition.y < 0 || mousePosition.x >= Screen.width || mousePosition.y >= Screen.height) return;
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
            else
            {
                // Defer clearing until release, so Escape can cancel a range gesture.
                regionPending = true;
                regionDragging = false;
                regionStart = mousePosition;
                regionAdditive = AdditiveSelection;
            }
        }

        bool modifier = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl) ||
            Input.GetKey(KeyCode.LeftCommand) || Input.GetKey(KeyCode.RightCommand);
        if (modifier && Input.GetKeyDown(KeyCode.G)) { GroupSelected(EditInput.ShiftPressed()); return; }
        if (modifier && Input.GetKeyDown(KeyCode.A)) { SelectAll(); return; }
        if (modifier && Input.GetKeyDown(KeyCode.V)) { PasteSelected(EditInput.ShiftPressed()); return; }
        if (modifier && Input.GetKeyDown(KeyCode.C)) { CopySelected(); return; }
        if (EditInput.CancelPressedThisFrame()) { Select(null); return; }
        if (Current == null) return;
        if (Input.GetKeyDown(KeyCode.Delete)) { DeleteSelected(); return; }
        if (modifier && Input.GetKeyDown(KeyCode.D)) DuplicateSelected();
    }

    void UpdateRegion(Vector2 pointer)
    {
        if (EditInput.CancelPressedThisFrame() || EditInput.MiddlePressed() || EditInput.RightPressed() ||
            pointer.x < 0 || pointer.y < 0 || pointer.x >= Screen.width || pointer.y >= Screen.height ||
            PlacementController.IsScreenPositionOverBlockingUi(pointer))
        {
            CancelRegion();
            return;
        }
        regionDragging |= (pointer - regionStart).sqrMagnitude >= 36f;
        Rect region = SelectionRegionUtility.FromPoints(regionStart, pointer);
        if (regionDragging)
        {
            regionOverlay ??= new SelectionRegionOverlay();
            regionOverlay.Show(region);
        }
        if (EditInput.LeftPressed()) return;
        if (regionDragging)
        {
            var matches = FindObjectsByType<PlacedObject>(FindObjectsSortMode.InstanceID)
                .Where(item => CanEdit(item) && SelectionRegionUtility.Overlaps(cam, region, item)).ToArray();
            // Touching one part should not select its entire model through the parent bounds.
            SelectMany(matches.Where(item => !matches.Any(child => child != item &&
                child.transform.IsChildOf(item.transform))), regionAdditive);
        }
        else if (!regionAdditive) Select(null);
        CancelRegion();
    }

    void CancelRegion()
    {
        regionPending = regionDragging = false;
        regionOverlay?.Hide();
    }

    public void SelectAll()
    {
        SelectMany(FindObjectsByType<PlacedObject>(FindObjectsSortMode.InstanceID));
    }

    public void SelectMany(IEnumerable<PlacedObject> items, bool additive = false)
    {
        // Materialize before clearing: callers may pass Selected itself.
        var candidates = items == null ? new List<PlacedObject>() : items.Where(CanEdit).Distinct().ToList();
        candidates = SelectionGroups.Expand(candidates).Where(CanEdit).Distinct().ToList();
        if (!additive) selected.Clear();
        else selected.RemoveAll(item => !CanEdit(item));
        foreach (var item in candidates)
        {
            if (selected.Any(parent => item.transform.IsChildOf(parent.transform))) continue;
            selected.RemoveAll(child => child.transform.IsChildOf(item.transform));
            selected.Add(item);
        }
        PublishSelection();
    }

    public void Select(PlacedObject po) => Select(po, false);

    public void Select(PlacedObject po, bool additive)
    {
        if (CanEdit(po) && !string.IsNullOrEmpty(po.editorGroupId))
        {
            var members = SelectionGroups.Expand(new[] { po }).Where(CanEdit).ToList();
            if (additive && selected.Contains(po))
            {
                selected.RemoveAll(members.Contains);
                PublishSelection();
            }
            else SelectMany(members, additive);
            return;
        }
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
        SelectionGroups.Reidentify(commands.Select(command => command.Result));
        selected.Clear();
        selected.AddRange(commands.Select(command => command.Result).Where(item => item != null));
        PublishSelection();
    }

    public void GroupSelected(bool ungroup)
    {
        var items = selected.Where(CanEdit).ToList();
        if (items.Count < (ungroup ? 1 : 2))
        { OperationMessage?.Invoke("グループ化は2件以上を選択してください（Ctrl+G / 解除 Ctrl+Shift+G）"); return; }
        var ids = new HashSet<string>(items.Select(item => item.editorGroupId).Where(id => !string.IsNullOrEmpty(id)));
        // Include hidden members when dissolving an existing group; never leave half a group behind.
        items.AddRange(FindObjectsByType<PlacedObject>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Where(item => ids.Contains(item.editorGroupId)));
        if (Execute(new SelectionGroupCommand(items.Distinct(), ungroup ? null : System.Guid.NewGuid().ToString("N"))))
        { PublishSelection(); OperationMessage?.Invoke(ungroup ? "グループを解除しました" : "グループ化しました（Ctrl+Shift+Gで解除）"); }
    }

    public void CopySelected()
    {
        try
        {
            int count = clipboard.Copy(selected);
            OperationMessage?.Invoke(count > 0 ? $"{count}件をコピーしました" : "コピーする対象を選択してください");
        }
        catch (System.Exception ex) { ReportClipboardFailure("コピーできませんでした", ex); }
    }

    public void PasteSelected(bool inPlace = false)
    {
        if (!clipboard.HasContent) { OperationMessage?.Invoke("先にオブジェクトをコピーしてください"); return; }
        var command = clipboard.CreateCommand(placementController, registry, inPlace);
        try
        {
            if (!Execute(command))
            {
                command.Discard();
                ReportClipboardFailure("貼り付けできません。元モデルを確認してください", null);
                return;
            }
            clipboard.DidPaste(inPlace);
            SelectMany(command.Results);
            OperationMessage?.Invoke($"{command.Results.Count}件を貼り付けました");
        }
        catch (System.Exception ex) { command.Discard(); ReportClipboardFailure("貼り付けできませんでした", ex); }
    }

    void ReportClipboardFailure(string message, System.Exception exception)
    {
        LogWarning(message + (exception != null ? ": " + exception.Message : string.Empty));
        OperationMessage?.Invoke(message);
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
