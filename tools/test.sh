#!/usr/bin/env bash
# Runs the core unit tests. Extra args go to `dotnet test` (e.g. --filter FittingTests).
set -euo pipefail
cd "$(dirname "$0")/.."
dotnet test tests/WeaponBuilder.Core.Tests "$@"
