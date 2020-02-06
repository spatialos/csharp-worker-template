#!/usr/bin/env bash
set -e -u -x -o pipefail

cd "$(dirname "$0")/../"

mkdir -p ./nupkgs

# For simplicity, some packages depend on Improbable.WorkerSdkInterop. Make sure that's packaged first.
dotnet pack Improbable/Improbable.WorkerSdkInterop.sln -p:Platform=x64 --output "$(pwd)/nupkgs"
dotnet pack Improbable/Improbable.sln -p:Platform=x64 --output "$(pwd)/nupkgs"

# Beep.
echo -ne '\007'
