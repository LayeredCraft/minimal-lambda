#!/usr/bin/env bash
set -euo pipefail

if [ "$#" -ne 1 ]; then
  echo "Usage: $0 <version>" >&2
  exit 1
fi

VERSION="$1"

if [ -z "$VERSION" ]; then
  echo "Version must not be empty" >&2
  exit 1
fi

FILES=(
  "src/MinimalLambda.Templates/README.md"
  "src/MinimalLambda.Templates/templates/mlambda/src/BlueprintBaseName.1/BlueprintBaseName.1.csproj"
  "src/MinimalLambda.Templates/templates/mlambda/src/BlueprintBaseName.1/README.md"
  "src/MinimalLambda.Templates/templates/mlambda/test/BlueprintBaseName.1.Tests/BlueprintBaseName.1.Tests.csproj"
  "src/MinimalLambda.Templates/templates/mlambda-aot/src/BlueprintBaseName.1/BlueprintBaseName.1.csproj"
  "src/MinimalLambda.Templates/templates/mlambda-aot/src/BlueprintBaseName.1/README.md"
  "src/MinimalLambda.Templates/templates/mlambda-aot/test/BlueprintBaseName.1.Tests/BlueprintBaseName.1.Tests.csproj"
)

export VERSION
perl -0pi -e 's/(<(?:PackageReference|PackageVersion) Include="MinimalLambda(?:\.Testing)?" Version=")[^"]+(")/$1$ENV{VERSION}$2/g' "${FILES[@]}"
