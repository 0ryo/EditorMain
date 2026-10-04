# Managed regression checks

Run from the repository root:

```sh
dotnet run --project Tools/RegressionChecks/RegressionChecks.csproj
```

.NET 8 only; no NuGet dependencies. Links production condition evaluation, flow,
project persistence/migration, graph services/commands, import preflight, material
archive export and command history sources. Tests use isolated temporary directories
and remove only those directories. An expected rollback error is printed by the
negative command test.

- Condition editor checks cover all six types, target requirements, active parameters,
  save/reload, Undo/Redo, legacy data and locale-aware input.
- Step deletion checks cover first/middle/last/only-step deletion, bound conditions,
  references, neighboring selection, Undo/Redo, rejected branches/drafts, and later
  description edits and reordering.
- Export workflow checks inject a project tmp-write failure and missing archive asset,
  verify original project/backup/recovery/distribution bytes, retained authored data,
  retry, invalid graph handling and separate Japanese save/export outcomes.
- Startup recovery protection checks cover late UI observation, duplicate suppression,
  normal-save independence, retry/discard resolution and non-destructive preservation.
- Material export checks cover referenced buffers/textures, duplicate model types,
  backups and failed writes.

Run a focused suite with `--condition-editor`, `--step-deletion` or `--export-workflow`:

```sh
dotnet run --project Tools/RegressionChecks/RegressionChecks.csproj -- --condition-editor
```

Use `--keep-going` to report failed groups and continue through remaining groups;
the command still exits nonzero if any group fails. On Linux, the default FBX group's
Windows absolute-path fixtures fail, and the unchanged concurrent export-folder
reservation check can also fail on the host filesystem. These failures also occur
on the unmodified baseline and must not be reported as a full-suite pass.

For focused diagnosis, `--skip-fbx` and `--skip-material-export` explicitly bypass
those groups. `--persistence` bypasses both while running the other suites. Skips
are printed in the output; a skipped run is not a full regression pass.

Unity math, JSON and scene discovery have small managed adapters. Scene objects are
test fixtures, not actual Unity objects. Neither mode compiles the Unity UI/controller
or validates native serialization, object lifecycle, glTF rendering, UI layout,
PlayMode or platform file dialogs. Service dirty tracking and notification rendering
still require Unity verification. Run `Tools/Automation/Check Authoring Logic` inside
Unity for condition cases against actual Unity math/serialization. The agent must
not launch Unity.

Unity-only `RecoveryStartupWarningTests` cover late-created library UI, close/reopen,
explicit discard, automatic retry and separation from later autosave errors. Run
these PlayMode tests in an isolated test scene after integration.
