# UI/UX Design Audit - 2026-06-29

## 0. Scope
- Branch: `improve/addmodel`
- Unity: `6000.2.6f2`
- UI system: uGUI + TextMeshPro
- Runtime target observed: `C:\Users\ryota\OneDrive\デスクトップ\SkillSync_Editor\Editor.exe`
- Unity Editor was not launched. This audit used static file inspection and screenshots of the running build.

## 1. Captures
- `Docs/design_audit/captures/editor-window-20260629-132958.png`: initial state
- `Docs/design_audit/captures/state-condition-added-20260629-133037.png`: condition node added
- `Docs/design_audit/captures/state-placement-armed-20260629-133037.png`: catalog card selected for placement
- `Docs/design_audit/captures/state-after-world-click-20260629-133039.png`: object placed in 3D view
- `Docs/design_audit/captures/state-object-selected-20260629-133123.png`: object selected and detail panel visible
- `Docs/design_audit/captures/state-settings-clicked-20260629-133148.png`: attempted settings access while detail panel was visible

## 2. Summary Judgment
The current build looks functionally close but visually unfinished. The strongest cause is not any single ugly component; it is the lack of visible product intent. The screen reads as a set of generated rectangles over a flat gray viewport. Users can operate it, but the interface does not yet communicate confidence, authorship, or a clear next action.

After product-direction answers, the target is now clearer: this should become a friendly training-content authoring tool for field instructors. The desired trust level is closer to Figma, Notion, Canva, and Craft: clean, light, approachable, modern, and polished, without becoming heavy like Adobe or Microsoft enterprise tools.

Detailed implementation policy has been moved into `Docs/design_audit/ui_design_implementation_policy_2026-06-29.md`.

## 3. Critical Findings

### P0: 3D viewport has no world context
Observed:
- The main 3D area is a large flat gray rectangle.
- Placed object appears as a small dark block without material nuance, scale reference, shadow, floor, grid, axis, or horizon.
- Selection outline is bright green and feels like an engine debug overlay.

Risk:
- The first impression becomes "mock/prototype" even when the feature works.
- Placement success is visually weak; after clicking, the user has to infer what happened.

Concrete correction indicators:
- Add a neutral floor/grid plane visible in the default camera view.
- Add horizon or viewport background separation so the user sees this as a workspace, not a blank panel.
- Use selected outline color from the product accent system, not raw neon green.
- Add subtle contact shadow or grounding cue for placed objects.
- Add a small viewport status chip: `配置中: Vehicle/Car_Proxy`, `選択中: obj-0001`, or `閲覧モード`.
- Add an orientation/scale reference if VR placement scale matters.

Likely files:
- `Assets/Scripts/SelectionOutline.cs`
- `Assets/Scripts/PlacementController.cs`
- `Assets/Scripts/EditCameraController.cs`
- `Assets/EditorMain.unity` via Editor automation only, if scene objects must be added later

### P0: Current action state is too quiet
Observed:
- Mode buttons exist, but the selected state is only a small blue top-left button.
- Catalog card selection shows a blue outline, but there is no explicit instruction like "click the workspace to place."
- Placement success gives no strong confirmation.
- Settings access appears blocked/covered when detail panel is visible.

Risk:
- Users lose confidence because the UI does not confirm intent, waiting state, or result.

Concrete correction indicators:
- Add one persistent command/status strip at the top of the viewport or between viewport and graph.
- During placement, show: object name, cancel action, and next click target.
- After placement, show a short success toast or status chip and automatically select the new object.
- Keep global controls such as settings accessible when the detail panel is open.
- Make active mode visually unmistakable: icon + label + selected background, with inactive buttons quieter.

Likely files:
- `Assets/Scripts/CatalogUI.cs`
- `Assets/Scripts/PlacementController.cs`
- `Assets/Scripts/UI/UiPanelDockSync.cs`
- `Assets/Editor/Automation/BuildUiPrefabs.cs`

### P0: Scenario graph hierarchy is weak
Observed:
- The graph canvas is clean but too flat.
- Save error text is small and far from the nodes that need fixing.
- Warning marks on nodes are tiny and do not explain the correction.
- Step, Condition, connector dots, delete buttons, and status text compete at similar visual priority.

Risk:
- Users can create nodes, but cannot quickly understand what is invalid or what to do next.

Concrete correction indicators:
- Convert validation status into a warning badge plus expandable validation list.
- Clicking a validation item should highlight the relevant node.
- Use node header/body separation: header for identity, body for content, footer/connectors for relationships.
- Make empty Step body intentional: e.g. `手順を紐付けてください` instead of a blank box.
- Make Condition nodes read as an action sentence, not two generic dropdown rows.
- Reduce connector visual dominance when not connecting; emphasize only on hover/drag.

Likely files:
- `Assets/Scripts/UI/ScenarioGraphUI.cs`
- `Assets/Scripts/UI/StepNodeUI.cs`
- `Assets/Scripts/UI/ConditionNodeUI.cs`
- `Assets/Scripts/UI/ConditionRowUI.cs`
- `Assets/Scripts/UI/ConnectionLineGraphic.cs`

## 4. High Priority Findings

### P1: Catalog cards look like placeholders
Observed:
- Cards are name-only gray rectangles.
- Labels expose technical type IDs such as `Vehicle/Car_Proxy`.
- The bottom `オブジェクトを追加` button looks disabled because it uses the same pale surface language as inactive controls.

Concrete correction indicators:
- Use display names as primary labels; keep type IDs as secondary metadata only if needed.
- Add a thumbnail or compact category icon. If thumbnails are not available, use a consistent generated proxy icon per category.
- Add hover/selected/placement states that differ in more than a border.
- Make `オブジェクトを追加` a true secondary or primary action, not disabled-looking.
- Empty/search states should explain what happened and what the user can do next.

Likely files:
- `Assets/Scripts/CatalogUI.cs`
- `Assets/Editor/Automation/BuildUiPrefabs.cs`
- `Assets/Scripts/UI/DesignTokenApplier.cs`

### P1: Detail panel feels bolted on
Observed:
- Detail panel appears on the right and consumes the settings area.
- Header is minimal and does not show the selected object as a strong context.
- Editable and read-only values look too similar.
- Width is narrow for Japanese labels and multiline descriptions.

Concrete correction indicators:
- Increase detail panel width target from 288 to about 360 px, or make it resizable.
- Header should show object display name, type badge, and close button.
- Use section spacing: identity, description, usage.
- Read-only rows should not look like inputs. Inputs need stronger border/focus styling.
- Empty usage state should say what makes an object "used" in a scenario.

Likely files:
- `Assets/Scripts/UI/ObjectDetailPanel.cs`
- `Assets/Scripts/UI/ObjectDetailConditionNodeStyler.cs`
- `Assets/Editor/Automation/BuildUiPrefabs.cs`

### P1: Design token drift reduces polish
Observed in code:
- `BuildUiPrefabs.cs` and `DesignTokenApplier.cs` force Canvas reference resolution to `2560x1440`.
- `Docs/rules/design_rule.md` specifies `1920x1080`.
- Several UI scripts use `FontStyles.Bold`, while the design rule allows only Regular/SemiBold.
- The settings button uses Unicode gear `\u2699`; in the screenshot it appears closer to a square than a clear gear.

Concrete correction indicators:
- Decide one reference resolution and document it. If QHD is intentional, update the design rule; otherwise revert implementation to 1920x1080.
- Replace `FontStyles.Bold` with the project's approved SemiBold TMP asset/material approach.
- Replace Unicode-only icon buttons with text labels or a bundled TMP-compatible icon strategy.
- Keep all corner radius, button height, input height, and row spacing in `DesignTokens`.

Likely files:
- `Assets/Scripts/UI/DesignTokens.cs`
- `Assets/Scripts/UI/DesignTokenApplier.cs`
- `Assets/Editor/Automation/BuildUiPrefabs.cs`
- `Assets/Scripts/UI/StepNodeUI.cs`
- `Assets/Scripts/UI/ConditionNodeUI.cs`
- `Assets/Scripts/UI/ConditionRowUI.cs`

## 5. Revised Implementation Order
Use the dedicated implementation policy as the source of truth:

- `Docs/design_audit/ui_design_implementation_policy_2026-06-29.md`

Short version:
1. Foundation and responsiveness.
2. State feedback.
3. Catalog and detail polish.
4. Scenario graph polish.
5. Optional automatic thumbnail generation.

## 6. Acceptance Criteria
- At 1920x1080, 2560x1440, and one narrow laptop resolution, no core controls overlap.
- A new user can tell within 3 seconds:
  - current mode
  - selected object, if any
  - whether the app is waiting for placement
  - why Save is disabled
- A placed object looks grounded in a workspace, not floating on a gray debug background.
- Object cards no longer look like placeholder buttons.
- Detail panel does not hide global settings or make the app feel trapped.
- Validation errors point to the thing that must be fixed.
- Japanese text and all icon-like labels render without tofu/square fallback.

## 7. Product Direction Answers
- Feel: friendly training-content tool.
- Main user: field instructors.
- Workspace balance: 3D viewport and scenario graph are equally important.
- Graph visibility: keep visible and resizable for now; improve resize affordance if needed.
- Object names: show both Japanese/display names and technical IDs.
- Catalog thumbnails: desirable only if the system can generate them automatically.
- Catalog card metadata: add at least category.
- Theme: light theme.
- Accent: calmer modern color than the current bright blue.
- Start/End nodes: quiet semantic nodes; no need to strongly stand out.
- Validation: subtle while editing, fuller interactive warning when Save is attempted.
- Hints: add a button now; content later.
- VR controller validation: later pass.
- Minimum size: laptop screens.
- Settings: global at all times.
- Detail panel: floating from screen edge with rich but subtle animation.
- Animations: necessary, subtle, and polished.
- Core command labels: Japanese text.
- Graph wording: easy Japanese, casual and approachable.
- Reference products: Figma, Notion, Canva, Craft.
