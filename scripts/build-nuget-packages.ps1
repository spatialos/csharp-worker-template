$ErrorActionPreference = "Stop"

Set-Location "$PSScriptRoot/../"

$OutputDir=(Get-Location)

if (Test-Path "$env:USERPROFILE/.nuget/packages") {
    Remove-Item -Recurse -Force -Path "$env:USERPROFILE/.nuget/packages/improbable.*" | Out-Null
}

New-Item -ItemType Directory -Force -Path "$OutputDir/nupkgs" | Out-Null

# For simplicity, some packages depend on Improbable.WorkerSdkInterop. Make sure that's packaged first.
& dotnet pack Improbable/WorkerSdkInterop/Improbable.WorkerSdkInterop -p:Platform=x64 --output "$OutputDir/nupkgs"
if (!$?) {
    exit 1
}

& dotnet pack Improbable -p:Platform=x64 --output "$OutputDir/nupkgs"
if (!$?) {
    exit 1
}

[System.Media.SystemSounds]::Beep.Play()
