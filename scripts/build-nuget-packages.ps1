$ErrorActionPreference = "Stop"

Set-Location "$PSScriptRoot/../"

$OutputDir=(Get-Location)

New-Item -ItemType Directory -Force -Path "$OutputDir/nupkgs" | Out-Null

& dotnet pack Improbable/Improbable.WorkerSdkInterop.sln -p:Platform=x64 --output "$OutputDir/nupkgs"
if (!$?) {
    exit 1
}

& dotnet pack Improbable/Improbable.sln -p:Platform=x64 --output "$OutputDir/nupkgs"
if (!$?) {
    exit 1
}

[System.Media.SystemSounds]::Beep.Play()
