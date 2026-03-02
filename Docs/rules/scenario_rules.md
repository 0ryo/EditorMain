# Scenario Creation Feature: Rules

## 1. Design and Permissions 🔐

### 1.1 The first implementation must work with zero additional permissions (top priority)
The default save location for scenario data is:
- Under `Application.persistentDataPath`
- Format: JSON (recommended) or ScriptableObject (depending on use case)
- "Let the user choose a custom save location" is deferred (= increases permission and OS-specific differences)

> `persistentDataPath` is the standard path Unity provides for "data that should persist between sessions."  
> By starting within this path, most cases will not require additional OS permissions.  

### 1.2 Patterns where permissions increase in future extensions (only when needed)
Adding the following features may require platform-specific configuration:
- Using the device's **camera / microphone / location** (iOS requires Usage Descriptions in Info.plist)
- Saving directly to the device's **external storage** (Android has particularly strict restrictions)
- **Cloud sync** (network access, authentication / OAuth, ATS / certificate configuration, etc.)

The AI agent must proceed as follows:
1) Explain "why this is necessary" (alternative: is `persistentDataPath` insufficient?)
2) List the target platforms (Standalone / Android / iOS / WebGL, etc.)
3) Specify the Unity-side configuration points before implementing

### 1.3 iOS Notes
- Accessing sensitive information or device features on iOS requires permission descriptions (Usage Descriptions).
- Proceed on the assumption that adding Usage Descriptions in Unity's Player Settings will be reflected in Info.plist.

### 1.4 Android Notes
- On Android, permissions may need to be added to the Manifest.
- If adding permissions, handle `Assets/Plugins/Android/AndroidManifest.xml` (or Unity's generated / custom procedure).
- However, the initial MVP follows the "save to persistentDataPath" approach and must not trigger Manifest changes.

---

## 2. Minimum Viable Implementation (MVP) 🧱

### 2.1 Data Model (required)
- Scenario
  - id (stable identifier)
  - title
  - description (optional)
  - steps[] (array)
- Step (procedure / page)
  - id
  - type (e.g., Text / Choice / Action / Wait, etc. — starting with Text only is acceptable)
  - payload (body text, choices, parameters, etc.)
  - next (transition — linear flow is acceptable initially)

### 2.2 Save / Load (required)
- Save: under persistentDataPath as `scenarios/*.json`
- Load: load the list at startup or when the screen is displayed
- Corruption protection:
  - Write to a temporary file → atomic rename
  - Include a JSON schema version (`schemaVersion`)

### 2.3 Editing UI (required)
- List view (create / duplicate / delete / search can be deferred)
- Edit view (edit title + steps)
- Preview (simple runtime playback preview)

### 2.4 UI Integration (required)
- Add a "Scenario Creation" entry point to the existing UI / screen navigation
- Do not break existing input / VR interaction conventions (align with the existing interaction model if needed)
