using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public partial class ScenarioGraphUI
{
    readonly HashSet<string> selectedNodeIds = new();
    readonly Dictionary<string, Vector2> selectionDragStart = new();
    readonly Dictionary<string, Vector2> copiedPositions = new();
    readonly List<RaycastResult> selectionRaycasts = new();
    string nodeClipboard;
    int nodePasteCount;

    void BindNodeSelection(string id, RectTransform root)
    {
        var selector = root.GetComponent<ScenarioNodeSelectionHandle>();
        if (selector == null) selector = root.gameObject.AddComponent<ScenarioNodeSelectionHandle>();
        selector.onSelect = additive => SelectNode(id, additive);
        selector.SetSelected(selectedNodeIds.Contains(id));
    }

    void SelectNode(string id, bool additive)
    {
        if (!additive) selectedNodeIds.Clear();
        if (!selectedNodeIds.Add(id) && additive) selectedNodeIds.Remove(id);
        RefreshNodeSelection();
        if (statusText != null) statusText.text = $"{selectedNodeIds.Count}件選択 · Shift+クリックで追加 · Ctrl+C/V · Ctrl+Shift+L 左揃え / T 上揃え";
    }

    void RefreshNodeSelection()
    {
        selectedNodeIds.RemoveWhere(id => graph.FindNode(id) == null);
        foreach (var pair in nodeUIs)
            if (pair.Value?.root != null) pair.Value.root.GetComponent<ScenarioNodeSelectionHandle>()?.SetSelected(selectedNodeIds.Contains(pair.Key));
    }

    void HandleSelectionShortcuts()
    {
#if ENABLE_LEGACY_INPUT_MANAGER
        if (EditWorkspace.IsTypingIntoInputField() || nodeArea == null ||
            !RectTransformUtility.RectangleContainsScreenPoint(nodeArea, EditInput.MousePosition, GetComponentInParent<Canvas>().worldCamera)) return;
        if (EventSystem.current != null)
        {
            selectionRaycasts.Clear();
            EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = EditInput.MousePosition }, selectionRaycasts);
            if (selectionRaycasts.Count == 0 || !selectionRaycasts[0].gameObject.transform.IsChildOf(nodeArea)) return;
        }
        bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl) || Input.GetKey(KeyCode.LeftCommand) || Input.GetKey(KeyCode.RightCommand);
        if (ctrl && Input.GetKeyDown(KeyCode.A))
        { selectedNodeIds.Clear(); selectedNodeIds.UnionWith(nodeUIs.Keys); RefreshNodeSelection(); }
        if (ctrl && Input.GetKeyDown(KeyCode.C))
        {
            nodeClipboard = graph.CopyNodes(selectedNodeIds);
            copiedPositions.Clear();
            foreach (var pair in nodePositions) copiedPositions[pair.Key] = pair.Value;
            nodePasteCount = 0;
            if (statusText != null) statusText.text = string.IsNullOrEmpty(nodeClipboard) ? "コピーする手順・条件を選択してください" : "ノードと選択範囲内の接続をコピーしました";
        }
        if (ctrl && Input.GetKeyDown(KeyCode.V) && !string.IsNullOrEmpty(nodeClipboard))
        {
            var map = graph.PasteNodes(nodeClipboard);
            nodePasteCount++;
            selectedNodeIds.Clear();
            foreach (var pair in map)
            {
                selectedNodeIds.Add(pair.Value);
                nodePositions[pair.Value] = (copiedPositions.TryGetValue(pair.Key, out var at) ? at : Vector2.zero) + new Vector2(48, -48) * nodePasteCount;
            }
            RebuildAll();
        }
        if (Input.GetKeyDown(KeyCode.Delete)) graph.DeleteNodes(selectedNodeIds);
        if (Input.GetKeyDown(KeyCode.Escape)) { selectedNodeIds.Clear(); RefreshNodeSelection(); }
        if (ctrl && EditInput.ShiftPressed() && Input.GetKeyDown(KeyCode.L)) AlignSelectedNodes(0);
        if (ctrl && EditInput.ShiftPressed() && Input.GetKeyDown(KeyCode.T)) AlignSelectedNodes(1);
#endif
    }

    void BeginSelectionDrag(string id)
    {
        if (!selectedNodeIds.Contains(id)) SelectNode(id, false);
        selectionDragStart.Clear();
        foreach (var selected in selectedNodeIds)
            if (nodeUIs.TryGetValue(selected, out var view) && view.root != null) selectionDragStart[selected] = view.root.anchoredPosition;
    }

    void MoveSelectionWith(string id, Vector2 position)
    {
        if (!selectionDragStart.TryGetValue(id, out var start)) return;
        var delta = position - start;
        foreach (var pair in selectionDragStart)
        {
            if (!nodeUIs.TryGetValue(pair.Key, out var view) || view.root == null) continue;
            var proposed = pair.Value + delta;
            delta += ClampNodePosition(view.root, proposed) - proposed;
        }
        foreach (var pair in selectionDragStart) SetNodePosition(pair.Key, pair.Value + delta);
    }

    bool FinishSelectionDrag(string id)
    {
        if (selectionDragStart.Count < 2) return false;
        var commands = selectionDragStart.Where(p => nodeUIs.ContainsKey(p.Key))
            .Select(p => (IEditorCommand)new NodePositionCommand(this, p.Key, p.Value, nodeUIs[p.Key].root.anchoredPosition)).ToList();
        CommandService.I?.Stack?.RecordApplied(new CompositeEditorCommand("Move scenario selection", commands));
        selectionDragStart.Clear();
        return true;
    }

    void AlignSelectedNodes(int axis)
    {
        var items = selectedNodeIds.Where(id => nodeUIs.ContainsKey(id) && nodeUIs[id].root != null).OrderBy(id => id).ToList();
        if (items.Count < 2) return;
        float Edge(RectTransform rect) => axis == 0 ? rect.anchoredPosition.x - rect.rect.width * rect.pivot.x :
            rect.anchoredPosition.y + rect.rect.height * (1 - rect.pivot.y);
        float coordinate = axis == 0 ? items.Min(id => Edge(nodeUIs[id].root)) : items.Max(id => Edge(nodeUIs[id].root));
        var commands = new List<IEditorCommand>();
        foreach (var id in items)
        {
            var from = nodeUIs[id].root.anchoredPosition;
            var to = from; to[axis] += coordinate - Edge(nodeUIs[id].root);
            commands.Add(new NodePositionCommand(this, id, from, to));
        }
        CommandService.I?.Stack?.Execute(new CompositeEditorCommand("Align scenario selection", commands));
    }
}
