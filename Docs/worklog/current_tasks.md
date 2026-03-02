# current_tasks

## 2026-03-02
- [completed] Task1 Step node drag behavior
  - Enabled node move by dragging outside the inner drag area on Step nodes.
  - Kept existing drag-handle behavior.
  - Added guard to block accidental drag starts from Selectable UI controls.
  - Files:
    - `Assets/Scripts/UI/NodeDragHandler.cs`
    - `Assets/Scripts/UI/ScenarioGraphUI.cs`

- [completed] Task2 Condition node visual-only update
  - Target:
    - Header text `手順 1` in bold, same size as Step `STEP 1`
    - Condition area under header with same bounds as Step drag area
    - Two dropdown rows with equal top/bottom padding
    - Right-side labels `を` and `に近づける` in bold, left edges aligned
  - Implemented files:
    - `Assets/Scripts/UI/ConditionNodeUI.cs`
    - `Assets/Scripts/UI/ConditionRowUI.cs`
    - `Assets/Scripts/UI/ScenarioGraphUI.cs`
    - `Assets/Editor/Automation/BuildUiPrefabs.cs` (template defaults)

- [completed] Task2 refinement: condition area + dropdown style
  - Same-size area as Step drag area is used for condition controls.
  - Two dropdowns and side labels stay inside area with equal insets.
  - Area and dropdowns are unified to white + thin gray outline.
  - Additional fix:
    - Disabled auto-layout on condition area to prevent top-left snapping.
    - Increased inner inset (`16`) and applied vertical inset for both rows.

- [completed] Task2 refinement: remove extra inner box + condition drag
  - Removed unintended visual frame of `ConditionRow` container (no fill / no outline).
  - Kept current dropdown positions unchanged.
  - Enabled whole-node drag for Condition nodes (same behavior class as Step, with selectable-start guard).

- [completed] Hotfix: CS0136 in ScenarioGraphUI
  - Fixed local variable name collision in `ConfigureNodeDragCallbacks`.
  - Renamed inner-scope `dragHandle` to `dragHandleRt` to avoid duplicate declaration.
  - File:
    - `Assets/Scripts/UI/ScenarioGraphUI.cs`

- [completed] Task2 refinement: condition area yellow background removal
  - Added runtime visual reset for condition-area containers to avoid legacy prefab tint.
  - `ConditionList` parent / `ConditionRow` / `LineA` / `LineB` now clear container fill and disable container outline.
  - Dropdown backgrounds remain white with thin gray outline.
  - File:
    - `Assets/Scripts/UI/ConditionNodeUI.cs`

- [completed] Session checkpoint (temporary stop)
  - In this session, work is completed through **Task2 adjustments**.
  - Next scope (`Task3`: nesting hierarchy + dynamic resize script) has not started yet.
