#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
TMP="$(mktemp -d)"
trap 'rm -rf "$TMP"' EXIT
git clone --no-hardlinks "$ROOT" "$TMP/repo"
cd "$TMP/repo"
dotnet build -c Release -warnaserror
dotnet test -c Release --filter 'Category!=Live'
echo "Clean-clone verification passed."
