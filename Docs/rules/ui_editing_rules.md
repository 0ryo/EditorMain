# UI Editing Rules

## 1. General UI Design Conventions
- Separate "presentation" from "state / logic" in all UI work.
  - View (display): `UI/*` namespace or `UI/Views/*`
  - State / Logic: `UI/State/*` or `UI/Controllers/*`
- Directly mutating data from UI code is prohibited. Always go through the Service / UseCase layer (or its equivalent).
- UI adjustments must preserve existing Prefab / Scene structure and be kept to the minimum necessary additions.

## 2. When Using UI Toolkit (UXML / USS)
- Additions and modifications must follow this separation:
  - Layout: `.uxml`
  - Appearance: `.uss`
  - Wiring: `.cs` (View initialization, event subscriptions, bindings)
- Keep USS scoped locally. Avoid globally-affecting selectors as much as possible.
- If an existing class naming convention exists, follow it (e.g., BEM-style, kebab-case, etc.).

## 3. When Using uGUI (Canvas / Prefab)
- **New UI must be implemented using Canvas (uGUI)** (do not treat code-driven dynamic UI hierarchy generation as the standard approach).
- Do not increase the number of objects placed directly in Scenes. Where possible, turn UI into Prefabs and inject them via references.
- Preserve existing RectTransform / anchor / scale conventions.
- Prioritize creating shared components for buttons, inputs, lists, etc. (do not duplicate the same appearance and behavior).
- Before implementing or adjusting UI, check `Docs/worklog_UI/`. If the implementation results in any spec differences, update the documents in that directory.

## 4. When Scene / Prefab Updates Are Needed (Important)
- Direct YAML editing is prohibited (last resort only).
- Instead, create an **Editor script** under `Assets/Editor/Automation/` that:
  - Loads the target Scene / Prefab via `AssetDatabase`
  - Adds the necessary GameObjects / Components and sets references
  - Saves using `PrefabUtility.SaveAsPrefabAsset` or Scene save
  - Logs the changes made
  — This approach lets Unity write the correct references.
