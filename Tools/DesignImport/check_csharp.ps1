# Roslyn type check against installed Unity references, without starting Unity.
# This is not a Unity asset import / Play Mode / build verification.
$ErrorActionPreference = 'Stop'
$checkDir = Join-Path $env:TEMP 'skillsync-static-check'
New-Item -ItemType Directory -Force $checkDir | Out-Null
$editorRoot = 'C:/Program Files/Unity/Hub/Editor/6000.2.6f2/Editor/Data'
$responseDir = 'Library/Bee/artifacts/1900b0aE.dag'
foreach ($assembly in @('Assembly-CSharp', 'Assembly-CSharp-Editor')) {
    $sourceFolder = if ($assembly -eq 'Assembly-CSharp') { 'Assets/Scripts' } else { 'Assets/Editor' }
    $response = Get-Content "$responseDir/$assembly.rsp" | Where-Object {
        $_ -notmatch '^-(out|refout):' -and $_ -notmatch '^"Assets/.*\.cs"$' -and $_ -notmatch '^/analyzer:'
    }
    $response = $response | ForEach-Object {
        if ($_ -match '^-r:.*Assembly-CSharp.ref.dll') { '-r:"' + $checkDir + '/Assembly-CSharp.dll"' } else { $_ }
    }
    $response += '-out:"' + $checkDir + '/' + $assembly + '.dll"'
    $response += rg --files $sourceFolder -g '*.cs' | ForEach-Object { '"' + ($_ -replace '\\','/') + '"' }
    $responsePath = Join-Path $checkDir ($assembly + '.rsp')
    $response | Set-Content $responsePath -Encoding utf8
    & "$editorRoot/NetCoreRuntime/dotnet.exe" "$editorRoot/DotNetSdkRoslyn/csc.dll" ('@' + $responsePath)
    if ($LASTEXITCODE -ne 0) { throw "$assembly static type check failed." }
}
