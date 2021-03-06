#!/usr/bin/env bash
set -e -u -x -o pipefail

cd "$(dirname "$0")/../"

dotnet publish Workers/GameLogic/GameLogic.csproj -r win-x64 -c Release -p:Platform=x64 --self-contained
