using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;

public class SelectionService : MonoBehaviour
{
    /// <summary>選択が変わるたびに発火する。null は選択解除を意味する。</summary>
    public event System.Action<PlacedObject> OnSelectionChanged;
    public event System.Action<string> OperationMessage;

    /// <summary>削除前の確認。false を返すと DeleteSelected は何もしない（確認ダイアログで承諾後に DeleteSelected(true) を呼ぶ）。</summary>
    public System.Func<IReadOnlyList<PlacedObject>, bool> DeleteGuard;
    PlacedObject listRangeAnchor;
    public Camera cam;
    EditorCameraController cameraController;
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
    PlacedObject drilledGroupRoot;
    string drilledEditorGroupId;
    SelectionBodyDrag bodyDrag;
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

    void OnDisable() { CancelRegion(); bodyDrag?.Cancel(); highlights.Clear(); }
    void OnDestroy() { regionOverlay?.Dispose(); highlights.Clear(); }

    void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus) { CancelRegion(); bodyDrag?.Cancel(); }
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
            bodyDrag?.Cancel();
            return;
        }
        bodyDrag ??= new SelectionBodyDrag(this);
        if (bodyDrag.Active)
        {
            bodyDrag.Update(cam, mousePosition);
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
                out var usedMaskFallback,
                out var pickedPoint);
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
                BeginPick(picked, pickedPoint, mousePosition);
            }
            else if (EditInput.AltPressed())
            {
                // Option/Alt + drag on empty space turns the view (with Shift: pans) for trackpads; on a part Alt still means "no snapping".
                if (cameraController == null) cameraController = FindFirstObjectByType<EditorCameraController>();
                if (cameraController != null) cameraController.BeginLeftDragNavigation();
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

        bool shortcutsBlocked = EditWorkspace.AreTransformShortcutsBlocked();
        bool modifier = !shortcutsBlocked && (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl) ||
            Input.GetKey(KeyCode.LeftCommand) || Input.GetKey(KeyCode.RightCommand));
        if (modifier && Input.GetKeyDown(KeyCode.G)) { GroupSelected(EditInput.ShiftPressed()); return; }
        if (modifier && Input.GetKeyDown(KeyCode.A)) { SelectAll(); return; }
        if (modifier && Input.GetKeyDown(KeyCode.V)) { PasteSelected(EditInput.ShiftPressed()); return; }
        if (modifier && Input.GetKeyDown(KeyCode.C)) { CopySelected(); return; }
        if (EditInput.CancelPressedThisFrame()) { Select(null); return; }
        if (Current == null) return;
        if (!shortcutsBlocked && (Input.GetKeyDown(KeyCode.Delete) || Input.GetKeyDown(KeyCode.Backspace))) { DeleteSelected(); return; }
        if (modifier && Input.GetKeyDown(KeyCode.D)) DuplicateSelected();
    }

    // A press selects as before; holding and dragging the body then moves the selection along the floor.
    // Pressing something already selected defers the select so the drag moves the whole selection.
    void BeginPick(PlacedObject picked, Vector3 point, Vector2 pointer)
    {
        bool additive = AdditiveSelection;
        if (additive || EditWorkspace.AreTransformShortcutsBlocked()) { SelectPicked(picked, additive); return; }
        bool inSelection = selected.Any(item => picked == item || picked.transform.IsChildOf(item.transform));
        if (!inSelection) SelectPicked(picked, false);
        bodyDrag.Begin(picked, point, pointer, inSelection);
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
            // The range selects the same units a click does: a whole model or flat group, never a loose part.
            // Environment pieces (floor, walls) count only when fully inside, so a frame that merely touches them skips them.
            var roots = FindObjectsByType<PlacedObject>(FindObjectsSortMode.InstanceID)
                .Where(item => CanEdit(item) && SelectionRegionUtility.Overlaps(cam, region, item))
                .Select(SelectionGroups.PickRoot).Where(root => root != null && CanEdit(root)).Distinct()
                .Where(root => !SelectionGroups.IsEnvironment(root) || SelectionRegionUtility.Contains(cam, region, root)).ToArray();
            SelectMany(roots, regionAdditive);
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
        ClearDrilledGroup();
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
        ClearDrilledGroup();
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
        SelectIndividual(po, additive);
    }

    // Viewport taps enter a group first, then allow direct selection of its parts.
    public void SelectPicked(PlacedObject picked, bool additive = false)
    {
        if (!CanEdit(picked)) return;
        var root = SelectionGroups.PickRoot(picked);
        var members = SelectionGroups.Expand(new[] { root }).Where(CanEdit).ToList();
        bool sameDrilledGroup = selected.Count == 1 && Current != null &&
            ((!string.IsNullOrEmpty(root.editorGroupId) && root.editorGroupId == drilledEditorGroupId) ||
                (string.IsNullOrEmpty(root.editorGroupId) && root == drilledGroupRoot));
        bool groupSelected = members.Count > 0 && members.All(Contains);
        bool hasGroup = root != picked || !string.IsNullOrEmpty(root.editorGroupId) ||
            root.GetComponentsInChildren<PlacedObject>(true).Any(part => part != root);
        if (!additive && !hasGroup && Contains(picked))
        {
            ClearDrilledGroup();
            if (picked != Current)
            {
                selected.Remove(picked);
                selected.Add(picked);
                PublishSelection();
            }
            return;
        }
        if (!additive && hasGroup && (sameDrilledGroup || groupSelected))
        {
            drilledGroupRoot = root;
            drilledEditorGroupId = root.editorGroupId;
            SelectIndividual(picked, false);
            return;
        }

        ClearDrilledGroup();
        if (additive && members.Any(Contains))
        {
            selected.RemoveAll(members.Contains);
            PublishSelection();
        }
        else SelectMany(members, additive);
    }

    void ClearDrilledGroup()
    {
        drilledGroupRoot = null;
        drilledEditorGroupId = null;
    }

    void SelectIndividual(PlacedObject po, bool additive)
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

    /// <summary>skipGuard=true は DeleteGuard の確認ダイアログで「削除」が選ばれた後の実削除に使う。</summary>
    public void DeleteSelected(bool skipGuard = false)
    {
        var commands = selected.Where(CanEdit).Select(item => (IEditorCommand)new DeleteObjectCommand(
            item.gameObject, item.typeId, typeId => PlacedObjectRestoreFactory.Create(typeId, registry, placementController))).ToList();
        if (commands.Count == 0) return;
        if (!skipGuard && DeleteGuard != null && !DeleteGuard(selected.Where(CanEdit).ToList())) return;
        if (Execute(new CompositeEditorCommand("Delete selection", commands))) Select(null);
    }

    /// <summary>一覧の行クリック。range=Shift（直前の行から範囲選択）、toggle=Ctrl/Cmd（追加・解除）。order は一覧の表示順。</summary>
    public void SelectFromList(PlacedObject po, bool range, bool toggle, IReadOnlyList<PlacedObject> order = null)
    {
        if (po == null) return;
        if (!CanEdit(po)) { if (Current != null) Select(null); return; }
        order ??= FindObjectsByType<PlacedObject>(FindObjectsSortMode.InstanceID);
        var list = order.ToList();
        int anchorIndex = list.IndexOf(listRangeAnchor), targetIndex = list.IndexOf(po);
        if (range && anchorIndex >= 0 && targetIndex >= 0)
        {
            int first = Mathf.Min(anchorIndex, targetIndex);
            SelectMany(list.GetRange(first, Mathf.Abs(anchorIndex - targetIndex) + 1), toggle);
            return;
        }
        Select(po, toggle || range);
        listRangeAnchor = po;
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
        { OperationMessage?.Invoke(ungroup ? "解除するグループを選んでください" : "グループにするには、2個以上の部品を選んでください"); return; }
        if (ungroup && !items.Any(item => !string.IsNullOrEmpty(item.editorGroupId)))
        { OperationMessage?.Invoke("選んだ部品はグループになっていません"); return; }
        var ids = new HashSet<string>(items.Select(item => item.editorGroupId).Where(id => !string.IsNullOrEmpty(id)));
        // Include hidden members when dissolving an existing group; never leave half a group behind.
        items.AddRange(FindObjectsByType<PlacedObject>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Where(item => ids.Contains(item.editorGroupId)));
        items = items.Distinct().ToList();
        if (Execute(new SelectionGroupCommand(items, ungroup ? null : System.Guid.NewGuid().ToString("N"))))
        { PublishSelection(); OperationMessage?.Invoke(ungroup ? "グループを解除しました" : $"{items.Count}個の部品をグループにしました"); }
    }

    public void CopySelected()
    {
        try
        {
            int count = clipboard.Copy(selected);
            OperationMessage?.Invoke(count > 0 ? $"{count}個の部品をコピーしました" : "コピーする部品を選んでください");
        }
        catch (System.Exception ex) { ReportClipboardFailure("コピーできませんでした", ex); }
    }

    public void PasteSelected(bool inPlace = false)
    {
        if (!clipboard.HasContent) { OperationMessage?.Invoke("先に部品をコピーしてください（" + SkillSyncDesignView.ShortcutModifierName + "+C）"); return; }
        var command = clipboard.CreateCommand(placementController, registry, inPlace);
        try
        {
            if (!Execute(command))
            {
                command.Discard();
                ReportClipboardFailure("貼り付けできません。元の3Dモデルが残っているか確認してください", null);
                return;
            }
            clipboard.DidPaste(inPlace);
            SelectMany(command.Results);
            OperationMessage?.Invoke($"{command.Results.Count}個の部品を貼り付けました");
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
