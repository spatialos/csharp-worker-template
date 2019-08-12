$ErrorActionPreference = "Stop"

Set-Location "$PSScriptRoot/../"

& dotnet publish Workers/GameLogic/GameLogic.csproj -r linux-x64 -c Release -p:Platform=x64 --self-contained
if (!$?) {
    exit 1
}
