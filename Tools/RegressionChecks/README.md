# Managed regression checks

Run from the repository root:

```powershell
dotnet run --project Tools/RegressionChecks/RegressionChecks.csproj
```

.NET 8 only; no NuGet dependencies. Links production condition evaluation, flow, project persistence/migration, import preflight, material archive export and command history sources. Export checks cover referenced buffers/textures, duplicate model types, backups and failed writes. Tests use an isolated temporary directory and remove only that directory. An expected rollback error is printed by the negative command test.

Unity math and JSON have small managed adapters. This checks application logic, not Unity serialization fidelity, glTF rendering, UI layout, PlayMode or platform file dialogs. Run `Tools/Automation/Check Authoring Logic` inside Unity for the condition cases against actual Unity math/serialization. The agent must not launch Unity.

Fixed SkillSync condition editing checks link the production graph commands, validator and export builder, and cover all six types, target requirements, active parameters, save/reload, Undo/Redo, legacy data and locale-aware input:

```sh
dotnet run --project Tools/RegressionChecks/RegressionChecks.csproj -- --condition-editor
```

`--skip-fbx` runs the other checks while excluding the FBX suite, whose Windows absolute-path cases fail on Linux. The normal command retains those checks. Neither mode compiles the Unity UI/controller or validates native serialization and Prefab layout.

If the existing concurrent export-folder reservation check also fails on the host filesystem, `--skip-fbx --skip-material-export` explicitly excludes those two suites so persistence/history checks can still run. Report the unfiltered failures separately; this is not a full-suite pass.
