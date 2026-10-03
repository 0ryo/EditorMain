# Managed regression checks

Run from the repository root:

```powershell
dotnet run --project Tools/RegressionChecks/RegressionChecks.csproj
```

.NET 8 only; no NuGet dependencies. Links production condition evaluation, flow, project persistence/migration, import preflight, material archive export and command history sources. Export checks cover referenced buffers/textures, duplicate model types, backups and failed writes. Tests use an isolated temporary directory and remove only that directory. An expected rollback error is printed by the negative command test.

Unity math and JSON have small managed adapters. This checks application logic, not Unity serialization fidelity, glTF rendering, UI layout, PlayMode or platform file dialogs. Run `Tools/Automation/Check Authoring Logic` inside Unity for the condition cases against actual Unity math/serialization. The agent must not launch Unity.

Startup recovery protection checks cover late UI observation, duplicate suppression,
normal-save independence, retry/discard resolution and non-destructive preservation.
The default FBX path checks have a known Windows-path failure on Linux. Run
`dotnet run --project Tools/RegressionChecks/RegressionChecks.csproj -- --skip-fbx`
to execute the remaining checks; report the skipped FBX failure separately.
`--persistence` runs authoring logic, persistence/history (including recovery
ownership) and startup protection checks without the separate FBX/material export suites.
Unity-only `RecoveryStartupWarningTests` cover the late-created library UI,
close/reopen behavior, explicit discard, automatic retry and separation from later
autosave errors. These PlayMode tests require an isolated test scene and must be
run by the user after integration; the agent must not launch Unity.
