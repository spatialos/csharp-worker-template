$ErrorActionPreference = "Stop"

Set-Location "$PSScriptRoot/../../"

mkdir ./nuget-cache
$env:NUGET_PACKAGES=Resolve-Path "./nuget-cache"

./scripts/build-nuget-packages.ps1
if (!$?) {
    exit 1
}

& dotnet build ./Workers.sln -p:Platform=x64
if (!$?) {
    exit 1
}

./scripts/publish-windows-workers.ps1
if (!$?) {
    exit 1
}

./scripts/publish-osx-workers.ps1
if (!$?) {
    exit 1
}

./scripts/publish-linux-workers.ps1
if (!$?) {
    exit 1
}
