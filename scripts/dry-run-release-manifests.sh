#!/usr/bin/env bash
set -euo pipefail

repo_root=$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)
temporary=$(mktemp -d "${TMPDIR:-/tmp}/minimal-lambda-release.XXXXXX")
cleanup() {
  rm -rf "$temporary"
}
trap cleanup EXIT INT TERM

# Template packing stamps source files. Copy checkout first so interruption, hard kill, or
# concurrent runs cannot alter active working tree.
work_root="$temporary/repository"
mkdir -p "$work_root"
rsync -a \
  --exclude .git \
  --exclude .cache \
  --exclude artifacts \
  --exclude bin \
  --exclude obj \
  --exclude __pycache__ \
  "$repo_root/" "$work_root/"

artifacts="$temporary/artifacts"
version=91.2.3-preview.456

cd "$work_root"

dotnet restore MinimalLambda.Packages.slnf --tl:off
dotnet build MinimalLambda.Packages.slnf \
  --tl:off \
  --configuration Release \
  --no-restore \
  -p:Version="$version"
dotnet pack MinimalLambda.Packages.slnf \
  --tl:off \
  --configuration Release \
  --no-build \
  --output "$artifacts/packages" \
  -p:Version="$version"

python3 scripts/validate-release-artifacts.py \
  --lane core \
  --tag "v$version" \
  --artifacts "$artifacts/packages"

python3 scripts/validate-release-artifacts.py \
  --lane core-preview \
  --expected-version "$version" \
  --preview-run-number 456 \
  --artifacts "$artifacts/packages"

python3 scripts/test-release-artifact-validator.py

echo "Shared release manifest validated; active checkout was not packed."
