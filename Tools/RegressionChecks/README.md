# Managed regression checks

Run from the repository root:

```powershell
dotnet run --project Tools/RegressionChecks/RegressionChecks.csproj
```

.NET 8 only; no NuGet dependencies. Links production condition evaluation, flow, project persistence/migration, import preflight, material archive export and command history sources. Export checks cover referenced buffers/textures, duplicate model types, backups and failed writes. Tests use an isolated temporary directory and remove only that directory. An expected rollback error is printed by the negative command test.

Export workflow checks link the production save/validate/export orchestration. They inject a project tmp-write failure and a missing archive asset, verify original project/backup/recovery/distribution bytes, retained authored data, retry, invalid graph handling and separate Japanese save/export outcomes. The save delegate uses the production Store; Unity Service dirty tracking and notification rendering still require Unity verification.

Run only these checks with `dotnet run --project Tools/RegressionChecks/RegressionChecks.csproj -- --export-workflow`. On Linux, the default run currently stops at the FBX group's Windows absolute-path fixture. Use `--keep-going` to report failed groups and continue through the remaining checks; the command still exits nonzero if any group failed. This environment also fails the unchanged concurrent export directory reservation check; neither failure counts as a full-suite pass.

Unity math and JSON have small managed adapters. This checks application logic, not Unity serialization fidelity, glTF rendering, UI layout, PlayMode or platform file dialogs. Run `Tools/Automation/Check Authoring Logic` inside Unity for the condition cases against actual Unity math/serialization. The agent must not launch Unity.
