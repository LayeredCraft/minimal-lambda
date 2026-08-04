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
core_version=91.2.3-preview.456
durable_version=81.2.3-rc.7
minimum_version=$(sed -n 's:.*<MinimalLambdaMinimumVersion>\([^<]*\)</MinimalLambdaMinimumVersion>.*:\1:p' \
  "$work_root/src/MinimalLambda.DurableExecution/MinimalLambda.DurableExecution.csproj")

if [[ -z "$minimum_version" ]]; then
  echo "Could not resolve MinimalLambdaMinimumVersion" >&2
  exit 1
fi

cd "$work_root"

dotnet restore MinimalLambda.Packages.slnf --tl:off
dotnet build MinimalLambda.Packages.slnf \
  --tl:off \
  --configuration Release \
  --no-restore \
  -p:Version="$core_version"
dotnet pack MinimalLambda.Packages.slnf \
  --tl:off \
  --configuration Release \
  --no-build \
  --output "$artifacts/core" \
  -p:Version="$core_version"

python3 scripts/validate-release-artifacts.py \
  --lane core \
  --tag "v$core_version" \
  --artifacts "$artifacts/core"

python3 scripts/validate-release-artifacts.py \
  --lane core-preview \
  --expected-version "$core_version" \
  --preview-run-number 456 \
  --artifacts "$artifacts/core"

dotnet restore src/MinimalLambda.DurableExecution/MinimalLambda.DurableExecution.csproj --tl:off
dotnet build src/MinimalLambda.DurableExecution/MinimalLambda.DurableExecution.csproj \
  --tl:off \
  --configuration Release \
  --no-restore \
  -p:Version="$durable_version"
dotnet pack src/MinimalLambda.DurableExecution/MinimalLambda.DurableExecution.csproj \
  --tl:off \
  --configuration Release \
  --no-build \
  --output "$artifacts/durable" \
  -p:Version="$durable_version"

python3 scripts/validate-release-artifacts.py \
  --lane durable \
  --tag "durable-v$durable_version" \
  --expected-minimum-version "$minimum_version" \
  --artifacts "$artifacts/durable"

python3 scripts/test-release-artifact-validator.py

echo "Core and durable dry-run manifests are isolated; active checkout was not packed."
