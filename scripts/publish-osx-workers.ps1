$ErrorActionPreference = "Stop"

cd "$PSScriptRoot/../"

& dotnet publish Workers/GameLogic/GameLogic.csproj -r osx-x64 -c Release -p:Platform=x64 --self-contained

