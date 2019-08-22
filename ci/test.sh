#!/usr/bin/env bash

set -euxo pipefail

cd "$(dirname "$0")/../"

ls -lah $IMPROBABLE_CONFIG_DIR/oauth2

./scripts/build-nuget-packages.sh

dotnet build ./Workers.sln -p:Platform=x64
