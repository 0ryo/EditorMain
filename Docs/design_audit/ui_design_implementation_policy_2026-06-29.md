# UI Design Implementation Policy - 2026-06-29

## 0. Product Direction
This project should feel like a friendly training-content authoring tool for field instructors.

It should borrow trust signals from Figma, Notion, Canva, and Craft:
- clean light surfaces
- calm accent color
- clear hierarchy
- approachable Japanese wording
- subtle but polished animation
- strong enough structure to feel reliable, without looking heavy like Adobe or Microsoft enterprise tools

The 3D viewport and scenario graph are equal main workspaces. The UI should not over-optimize for one at the expense of the other.

## 1. Design Principles For This Project

### 1.1 Friendly, Not Toy-Like
Use plain Japanese and gentle visual treatment, but keep layout precision high. The tool should feel easy for field instructors, not childish.

Concrete rules:
- Prefer Japanese text labels for core commands.
- Avoid unexplained English terms such as `Condition` in visible UI.
- Keep technical IDs visible as secondary information, not as the main label.
- Use calm color, spacing, and animation to create confidence.

### 1.2 Two Equal Workspaces
The 3D viewport and scenario graph must both remain understandable at the same time.

Concrete rules:
- Keep the graph visible and resizable.
- Add a visible affordance to the graph resize handle.
- Maintain a minimum graph height for laptop screens.
- Avoid hiding the graph in tabs or drawers for now.

### 1.3 State Must Be Obvious
The user should always know:
- current mode
- selected object
- whether the app is waiting for placement
- whether Save is possible
- what to fix when Save is blocked

Concrete rules:
- Add a persistent status/action strip.
- Use subtle warnings while editing.
- Show fuller warning details interactively when the user is about to save.
- Do not rely only on small gray status text.

### 1.4 Animation Is Part Of Trust
Use subtle motion for state changes, not decorative motion.

Concrete rules:
- Detail panel slides from the right edge with easing.
- Toast/status messages fade and move slightly.
- Mode and selected-card changes can animate color/scale subtly.
- Keep durations short: 120-240 ms for most UI, 240-320 ms for panel entrance.
- Use existing Unity/uGUI coroutines; add no animation package.

## 2. Revised Visual System

### 2.1 Theme
Keep a light neutral theme.

Base:
- background: soft neutral off-white
- surfaces: white/off-white
- dividers: warm/cool neutral gray
- text: high-contrast neutral, not pure black

Avoid:
- dark pro-tool theme
- high-chroma blue everywhere
- heavy shadows
- glossy/gradient styling

### 2.2 Accent Color
Replace the current bright `#0A84FF` style with a calmer modern accent.

Recommended first candidate:
- accent: `#2563EB`
- hover: `#1D4ED8`
- press: `#1E40AF`
- selected background tint: accent at 8-12% alpha

Reason:
- still reads as action/selection
- calmer than the current neon-like blue
- works well on a light Notion/Figma-like interface
- easier to pair with neutral UI than teal/green when validation also uses semantic colors

Do not change semantic warning/error/success meaning while changing accent.

### 2.3 Start/End Nodes
Start/End should be semantic but quiet.

Concrete rules:
- Use neutral surface with a small label or subtle badge.
- Do not use strong red/blue filled nodes.
- Preserve clear connector direction.
- The path structure should carry meaning more than the node color.

## 3. Responsive Layout Policy

### 3.1 Laptop Support
Minimum target should include laptop screens. Test at:
- 1366 x 768
- 1440 x 900
- 1920 x 1080
- 2560 x 1440

Implementation policy:
- Standardize Canvas reference resolution to `1920 x 1080` unless a later measured reason says otherwise.
- Keep panels responsive through min/max widths and heights.
- Avoid QHD-only spacing assumptions.

### 3.2 Panel Layout
Catalog:
- minimum width: 240
- comfortable default: 300-320
- maximum: 420 unless user resizes wider

Scenario graph:
- minimum height: 220 on laptop
- default height: around 35-40% of vertical space
- add visible resize grip on top edge
- cursor/visual change on hover if practical

Detail panel:
- floating inspector from right edge when object is selected
- target width: 360 on desktop, clamp to about 32-38% of screen width
- should not cover global settings access
- should include a close action and allow deselection to hide

Settings:
- global at all times
- Japanese text label is acceptable, e.g. `設定`
- avoid Unicode gear as the only visible label because TMP glyph fallback can make it look broken

## 4. Feature-Specific Implementation Policy

### 4.1 3D Viewport
Goal:
Make the viewport feel like an intentional training workspace, not a blank gray debug area.

Minimum realistic pass:
- Add a neutral floor/grid visual.
- Add subtle object grounding, such as contact shadow or a darker base plane.
- Replace bright green selection outline with accent-based selection.
- Add viewport status/action strip:
  - `閲覧中`
  - `配置中: 車両 / Vehicle/Car_Proxy`
  - `選択中: obj-0001`
- After placement, show a short success message and select the new object if possible.

Avoid for first pass:
- complex lighting rewrite
- high-end post-processing
- full 3D gizmo redesign
- VR controller validation

Likely files:
- `Assets/Scripts/SelectionOutline.cs`
- `Assets/Scripts/PlacementController.cs`
- `Assets/Scripts/EditCameraController.cs`
- `Assets/Scripts/CatalogUI.cs`
- `Assets/Editor/Automation/BuildUiPrefabs.cs`

### 4.2 Catalog
Goal:
Make object cards feel like training assets, not placeholder buttons.

Minimum realistic pass:
- Primary label: Japanese/display name.
- Secondary label: technical ID.
- Category badge, e.g. `車両`, `工具`, `環境`, `その他`.
- Selected/placement state with accent tint and clearer status.
- Make `オブジェクトを追加` look available, not disabled.

Thumbnail policy:
- If automatic thumbnail generation can be done through Unity APIs without manual user work, support it.
- Do not block the first visual pass on thumbnails.
- Use category-based fallback visual blocks first.
- Add thumbnail generation as a later automation task because it may require controlled Editor/API execution by the user.

Likely files:
- `Assets/Scripts/CatalogUI.cs`
- `Assets/Editor/Automation/BuildUiPrefabs.cs`
- `Assets/Scripts/UI/DesignTokenApplier.cs`

### 4.3 Scenario Graph
Goal:
Make scenario construction approachable and readable.

Terminology:
- `Step` -> `手順`
- `Condition` -> preferably `条件` or action-oriented `動作条件`
- `Save` -> `保存`
- `START` -> `開始`
- `END` -> `終了`

Minimum realistic pass:
- Keep warnings subtle while editing: prominent icon, not large error text.
- When user presses Save and validation fails, show an interactive warning panel/list.
- Warning panel should list issues in plain Japanese.
- Each warning item should identify the related node where possible.
- Empty step body should show friendly placeholder text.
- Condition row should read like a sentence:
  - `<対象A> を <対象B> に近づける`

Avoid for first pass:
- full graph engine rewrite
- advanced auto-layout
- hidden graph tabs/drawers

Likely files:
- `Assets/Scripts/UI/ScenarioGraphUI.cs`
- `Assets/Scripts/UI/StepNodeUI.cs`
- `Assets/Scripts/UI/ConditionNodeUI.cs`
- `Assets/Scripts/UI/ConditionRowUI.cs`
- `Assets/Scripts/UI/ConnectionLineGraphic.cs`

### 4.4 Detail Panel
Goal:
Make object details feel like a floating, polished inspector.

Minimum realistic pass:
- Float from the right edge with a richer slide/fade animation.
- Do not cover global settings access.
- Header shows display name and technical ID.
- Separate sections:
  - 基本情報
  - 説明
  - 使用中の条件
- Read-only values and editable fields should look different.
- Empty usage state should explain: `このオブジェクトはまだ手順で使われていません`

Likely files:
- `Assets/Scripts/UI/ObjectDetailPanel.cs`
- `Assets/Scripts/UI/ObjectDetailConditionNodeStyler.cs`
- `Assets/Scripts/UI/UiPanelDockSync.cs`
- `Assets/Editor/Automation/BuildUiPrefabs.cs`

### 4.5 Hints
Goal:
Add the entry point now; content can come later.

Minimum realistic pass:
- Add a Japanese text button: `ヒント`
- Place it in a global but non-primary area.
- On click, open a simple empty/help placeholder panel.
- Keep the implementation content-ready but do not author the final hint text yet.

Likely files:
- `Assets/Scripts/CatalogUI.cs`
- `Assets/Editor/Automation/BuildUiPrefabs.cs`

## 5. Implementation Phases

### Phase 1: Foundation And Responsiveness
Purpose:
Make the current UI stop fighting the design system.

Tasks:
- Standardize Canvas reference resolution decision.
- Update `DesignTokens` for calmer accent.
- Remove strong Start/End fills.
- Replace Unicode-only settings button with Japanese text.
- Ensure global settings remains accessible when detail panel is shown.
- Add laptop-resolution layout clamps.

Verification:
- Static code check.
- User-run Unity compile.
- User-run screenshots at laptop and desktop sizes.

### Phase 2: State Feedback
Purpose:
Make current mode, placement, selection, and save state obvious.

Tasks:
- Add viewport/action status strip.
- Add placement waiting feedback.
- Add placement success feedback.
- Improve selected catalog card treatment.
- Keep save warning subtle until Save is attempted.

Verification:
- Browse -> place -> select -> save-invalid flow.
- Confirm user can explain current state from screen alone.

### Phase 3: Catalog And Detail Polish
Purpose:
Reduce placeholder feel in side panels.

Tasks:
- Catalog cards: display name + technical ID + category.
- Category fallback visuals.
- Detail panel width, hierarchy, animation.
- Read-only/editable distinction.

Verification:
- Add object.
- Select object.
- Edit name/description.
- Confirm settings remains reachable.

### Phase 4: Scenario Graph Polish
Purpose:
Make graph editing friendly and error recovery clear.

Tasks:
- Japanese terminology pass.
- Empty-state text in step/condition nodes.
- Subtle warning icons.
- Save-attempt warning panel.
- Resize-handle affordance.

Verification:
- Add step.
- Add condition.
- Leave invalid graph.
- Press save and verify warning details.
- Resize graph on laptop-sized window.

### Phase 5: Optional Thumbnail Automation
Purpose:
Improve catalog confidence without requiring manual image preparation.

Tasks:
- Research Unity-safe thumbnail generation path.
- Add Editor automation only if it can generate stable thumbnails.
- Fall back gracefully to category visuals.

Verification:
- User executes automation/Unity verification.
- No manual thumbnail preparation required.

## 6. Non-Goals For The Next Pass
- Dark theme.
- Adobe/Microsoft-like dense enterprise UI.
- New Unity package or external UI framework.
- Manual Inspector setup.
- Direct Scene/Prefab YAML editing.
- Full VR controller UX.
- Complex 3D rendering overhaul.
- Manual catalog thumbnail preparation.

## 7. Acceptance Criteria
- A field instructor can identify the current mode and next action within 3 seconds.
- The UI uses approachable Japanese for primary commands.
- Technical IDs are visible but secondary.
- The 3D viewport and scenario graph both remain first-class workspaces.
- The graph resize handle is discoverable enough to try.
- Settings is reachable even when the object detail panel is open.
- Validation during editing is subtle, but Save failure is explicit and interactive.
- Detail panel feels intentionally animated, not abruptly attached.
- Object cards no longer look like blank mock rectangles.
- The app remains usable on laptop-sized screens.
