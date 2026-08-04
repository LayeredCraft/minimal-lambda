#!/usr/bin/env bash
set -euo pipefail

ROOT=$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)
FIXTURES="$ROOT/tests/package-compatibility"
OLD_CORE_VERSION=56.0.0-x56-old
CORE_VERSION=56.1.0-x56-core
DURABLE_VERSION=$CORE_VERSION
WORK=$(mktemp -d "${TMPDIR:-/tmp}/minimal-lambda-package-compat.XXXXXX")
FEED="$WORK/feed"
CONSUMERS="$WORK/consumers"
LOGS="$WORK/logs"
CONFIG="$WORK/NuGet.Config"
SUCCESS=0

finish() {
  status=$?
  if [ "$status" -eq 0 ]; then
    SUCCESS=1
  fi

  if [ "$SUCCESS" -eq 1 ] && [ "${PACKAGE_COMPAT_KEEP_WORK:-0}" != "1" ]; then
    rm -rf "$WORK"
  else
    printf '\nPackage compatibility artifacts: %s\n' "$WORK" >&2
  fi
}
trap finish EXIT

fail() {
  printf 'ERROR: %s\n' "$*" >&2
  exit 1
}

run_logged() {
  local name=$1
  shift
  printf '\n==> %s\n' "$name"
  "$@" 2>&1 | tee "$LOGS/$name.log"
}

assert_warning_free() {
  local log=$1
  if grep -Eiq '(^|[ :])warning([[:space:]]+[A-Z]+[0-9]{4})?:' "$log"; then
    fail "warning found in $log"
  fi
}

assert_generated_once() {
  local project_dir=$1
  local target=$2
  local count
  [ -d "$project_dir/obj/generated/$target" ] || fail "generated output missing for $(basename "$project_dir")/$target"
  count=$(find "$project_dir/obj/generated/$target" -type f -name 'MinimalLambda.DurableHandlers.g.cs' | wc -l | tr -d ' ')
  [ "$count" = "1" ] || fail "expected one durable generated file for $(basename "$project_dir")/$target, found $count"
}

pack() {
  local name=$1
  local project=$2
  local version=$3
  shift 3
  run_logged "build-$name" dotnet build "$project" \
    --configuration Release \
    --nologo \
    "/p:Version=$version" \
    "/p:GeneratePackageOnBuild=false" \
    "$@"
  run_logged "pack-$name" dotnet pack "$project" \
    --configuration Release \
    --no-build \
    --output "$FEED" \
    --nologo \
    "/p:Version=$version" \
    "/p:GeneratePackageOnBuild=false" \
    "$@"
}

restore_consumer() {
  local name=$1
  local project=$2
  local core_version=$3
  shift 3
  run_logged "restore-$name" dotnet restore "$project" \
    --configfile "$CONFIG" \
    --force \
    --no-cache \
    --nologo \
    --tl:off \
    "/p:CorePackageVersion=$core_version" \
    "/p:DurablePackageVersion=$DURABLE_VERSION" \
    "$@"
}

build_consumer() {
  local name=$1
  local project=$2
  local project_dir
  project_dir=$(dirname "$project")
  run_logged "build-$name-net10.0" dotnet build "$project" \
    --configuration Release \
    --framework net10.0 \
    --no-restore \
    --nologo \
    "/p:CorePackageVersion=$CORE_VERSION" \
    "/p:DurablePackageVersion=$DURABLE_VERSION" \
    "/p:EmitCompilerGeneratedFiles=true" \
    "/p:CompilerGeneratedFilesOutputPath=$project_dir/obj/generated/net10.0"
  assert_warning_free "$LOGS/build-$name-net10.0.log"
  assert_generated_once "$project_dir" net10.0
}

case "${PACKAGE_COMPAT_RID:-}" in
  '')
    machine=$(uname -m)
    case "$(uname -s):$machine" in
      Linux:x86_64|Linux:amd64) RID=linux-x64 ;;
      Linux:aarch64|Linux:arm64) RID=linux-arm64 ;;
      Darwin:x86_64|Darwin:amd64) RID=osx-x64 ;;
      Darwin:arm64|Darwin:aarch64) RID=osx-arm64 ;;
      *) fail "cannot determine host RID; set PACKAGE_COMPAT_RID" ;;
    esac
    ;;
  *) RID=$PACKAGE_COMPAT_RID ;;
esac

mkdir -p "$FEED" "$CONSUMERS" "$LOGS" "$WORK/packages" "$WORK/dotnet-home" "$WORK/http-cache" "$WORK/plugins-cache"
export DOTNET_NOLOGO=1
export DOTNET_CLI_HOME="$WORK/dotnet-home"
export NUGET_PACKAGES="$WORK/packages"
export NUGET_HTTP_CACHE_PATH="$WORK/http-cache"
export NUGET_PLUGINS_CACHE_PATH="$WORK/plugins-cache"

printf 'Working directory: %s\n' "$WORK"
printf 'NativeAOT RID: %s\n' "$RID"

pack abstractions-old "$ROOT/src/MinimalLambda.Abstractions/MinimalLambda.Abstractions.csproj" "$OLD_CORE_VERSION"
pack core-old "$ROOT/src/MinimalLambda/MinimalLambda.csproj" "$OLD_CORE_VERSION"
pack abstractions-compatible "$ROOT/src/MinimalLambda.Abstractions/MinimalLambda.Abstractions.csproj" "$CORE_VERSION"
pack core-compatible "$ROOT/src/MinimalLambda/MinimalLambda.csproj" "$CORE_VERSION"
pack durable "$ROOT/src/MinimalLambda.DurableExecution/MinimalLambda.DurableExecution.csproj" "$DURABLE_VERSION"

python3 - "$FEED" "$OLD_CORE_VERSION" "$CORE_VERSION" "$DURABLE_VERSION" <<'PY'
import sys
import zipfile
from pathlib import Path
from xml.etree import ElementTree as ET

feed = Path(sys.argv[1])
old, core, durable = sys.argv[2:]
expected = {
    f"MinimalLambda.Abstractions.{old}.nupkg": ("MinimalLambda.Abstractions", old, "abstractions"),
    f"MinimalLambda.{old}.nupkg": ("MinimalLambda", old, "core"),
    f"MinimalLambda.Abstractions.{core}.nupkg": ("MinimalLambda.Abstractions", core, "abstractions"),
    f"MinimalLambda.{core}.nupkg": ("MinimalLambda", core, "core"),
    f"MinimalLambda.DurableExecution.{durable}.nupkg": ("MinimalLambda.DurableExecution", durable, "durable"),
}
actual = {path.name for path in feed.glob("*.nupkg") if not path.name.endswith(".snupkg")}
if actual != set(expected):
    raise SystemExit(f"nupkg set mismatch: expected {sorted(expected)}, got {sorted(actual)}")


def children(element, name):
    return [child for child in element if child.tag.rsplit("}", 1)[-1] == name]


def child(element, name):
    matches = children(element, name)
    if len(matches) != 1:
        raise AssertionError(f"expected one {name}, found {len(matches)}")
    return matches[0]


def text(element, name):
    return (child(element, name).text or "").strip()


def assert_libs(names, package_id, frameworks):
    expected_libs = {
        f"lib/{tfm}/{package_id}.dll" for tfm in frameworks
    } | {
        f"lib/{tfm}/{package_id}.xml" for tfm in frameworks
    }
    actual_libs = {name for name in names if name.startswith("lib/")}
    if actual_libs != expected_libs:
        raise AssertionError(f"{package_id} lib assets mismatch: {sorted(actual_libs)}")


for filename, (package_id, version, kind) in expected.items():
    path = feed / filename
    with zipfile.ZipFile(path) as archive:
        names = archive.namelist()
        if names.count("README.md") != 1:
            raise AssertionError(f"{filename}: expected one root README.md")
        nuspec_name = f"{package_id}.nuspec"
        if names.count(nuspec_name) != 1:
            raise AssertionError(f"{filename}: expected one {nuspec_name}")

        generator_assets = [
            name for name in names if Path(name).name == "MinimalLambda.SourceGenerators.dll"
        ]
        build_assets = [
            name for name in names
            if name.split("/", 1)[0].lower().startswith("build")
        ]
        expected_generator = ["analyzers/dotnet/cs/MinimalLambda.SourceGenerators.dll"] if kind == "core" else []
        expected_build = ["build/MinimalLambda.targets", "buildTransitive/MinimalLambda.targets"] if kind == "core" else []
        if generator_assets != expected_generator or sorted(build_assets) != sorted(expected_build):
            raise AssertionError(
                f"{filename}: unexpected generator/build assets "
                f"{generator_assets}/{build_assets}"
            )

        if kind in {"core", "abstractions"}:
            assert_libs(names, package_id, ("net8.0", "net9.0", "net10.0", "net11.0"))
        else:
            assert_libs(names, package_id, ("net10.0",))

        root = ET.fromstring(archive.read(nuspec_name))
        metadata = child(root, "metadata")
        if text(metadata, "id") != package_id:
            raise AssertionError(f"{filename}: package id mismatch")
        if text(metadata, "version") != version:
            raise AssertionError(f"{filename}: package version mismatch")
        if text(metadata, "readme") != "README.md":
            raise AssertionError(f"{filename}: package readme mismatch")

        dependency_groups = children(child(metadata, "dependencies"), "group")
        if kind == "durable":
            tfms = {group.attrib.get("targetFramework") for group in dependency_groups}
            if len(dependency_groups) != 1 or tfms != {"net10.0"}:
                raise AssertionError(f"durable dependency TFMs mismatch: {tfms}")
            for group in dependency_groups:
                dependencies = children(group, "dependency")
                by_id = {dependency.attrib.get("id"): dependency for dependency in dependencies}
                if len(dependencies) != 2 or len(by_id) != 2 or set(by_id) != {"MinimalLambda", "Amazon.Lambda.DurableExecution"}:
                    raise AssertionError(f"durable dependency set mismatch: {set(by_id)}")
                minimal = by_id["MinimalLambda"]
                if minimal.attrib.get("version") != core:
                    raise AssertionError(f"durable minimum core mismatch: {minimal.attrib}")
                if minimal.attrib.get("exclude") != "Build,Analyzers":
                    raise AssertionError(f"durable core dependency exclusion mismatch: {minimal.attrib}")
                aws = by_id["Amazon.Lambda.DurableExecution"]
                if aws.attrib.get("version") != "1.0.0":
                    raise AssertionError(f"durable AWS dependency version mismatch: {aws.attrib}")
                if aws.attrib.get("exclude") != "Build,Analyzers":
                    raise AssertionError(f"durable AWS dependency exclusion mismatch: {aws.attrib}")
        elif kind == "core":
            if {group.attrib.get("targetFramework") for group in dependency_groups} != {
                "net8.0", "net9.0", "net10.0", "net11.0"
            }:
                raise AssertionError(f"{filename}: core dependency TFMs mismatch")
            for group in dependency_groups:
                abstractions = [
                    dependency
                    for dependency in children(group, "dependency")
                    if dependency.attrib.get("id") == "MinimalLambda.Abstractions"
                ]
                if len(abstractions) != 1 or abstractions[0].attrib.get("version") != version:
                    raise AssertionError(f"{filename}: abstractions dependency mismatch")
                if abstractions[0].attrib.get("exclude") != "Build,Analyzers":
                    raise AssertionError(f"{filename}: abstractions dependency exclusion mismatch")

if durable != core:
    raise AssertionError("durable and core versions must match")
print("Package archives and nuspecs match shared-version contract.")
PY

cp -R "$FIXTURES/." "$CONSUMERS/"
if grep -R -n '<ProjectReference' "$FIXTURES" "$CONSUMERS"; then
  fail "package compatibility consumers must not contain ProjectReference"
fi

python3 - "$CONSUMERS" <<'PY'
import sys
from pathlib import Path
from xml.etree import ElementTree as ET

root = Path(sys.argv[1])
projects = sorted(root.glob("*/*.csproj"))
if len(projects) != 5:
    raise SystemExit(f"expected five copied consumer projects, found {len(projects)}")
for project in projects:
    tree = ET.parse(project)
    references = [
        element.attrib.get("Include")
        for element in tree.iter()
        if element.tag.rsplit("}", 1)[-1] == "PackageReference"
    ]
    expected = ["MinimalLambda", "MinimalLambda.DurableExecution"]
    if project.parent.name != "TaskConsumer":
        expected.append("Amazon.Lambda.DurableExecution")
    if references != expected:
        raise SystemExit(f"{project}: unexpected package references {references}")
print("Copied consumers contain package references only.")
PY

python3 - "$CONFIG" "$FEED" <<'PY'
import sys
from pathlib import Path
from xml.sax.saxutils import escape

config, feed = map(Path, sys.argv[1:])
config.write_text(f'''<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="x56-local" value="{escape(str(feed.resolve()))}" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
  <packageSourceMapping>
    <packageSource key="x56-local">
      <package pattern="MinimalLambda*" />
    </packageSource>
    <packageSource key="nuget.org">
      <package pattern="*" />
    </packageSource>
  </packageSourceMapping>
</configuration>
''', encoding="utf-8")
PY

TYPED="$CONSUMERS/TypedConsumer/TypedConsumer.csproj"
TASK="$CONSUMERS/TaskConsumer/TaskConsumer.csproj"
OLD="$CONSUMERS/OldCoreConsumer/OldCoreConsumer.csproj"
AOT="$CONSUMERS/AotConsumer/AotConsumer.csproj"
INVALID="$CONSUMERS/InvalidSignatureConsumer/InvalidSignatureConsumer.csproj"

restore_consumer typed "$TYPED" "$CORE_VERSION"
restore_consumer task "$TASK" "$CORE_VERSION"
restore_consumer invalid-signature "$INVALID" "$CORE_VERSION"

python3 - "$NUGET_PACKAGES" "$CORE_VERSION" "$DURABLE_VERSION" \
  "$CONSUMERS/TypedConsumer/obj/project.assets.json" \
  "$CONSUMERS/TaskConsumer/obj/project.assets.json" <<'PY'
import json
import sys
from pathlib import Path

packages = Path(sys.argv[1]).resolve()
core, durable = sys.argv[2:4]
for filename in sys.argv[4:]:
    path = Path(filename)
    data = json.loads(path.read_text(encoding="utf-8"))
    package_folders = {str(Path(folder.rstrip("/\\")).resolve()) for folder in data["packageFolders"]}
    if package_folders != {str(packages)}:
        raise SystemExit(f"{path}: package cache is not isolated: {package_folders}")
    expected = {f"MinimalLambda/{core}", f"MinimalLambda.DurableExecution/{durable}"}
    targets = [value for name, value in data["targets"].items() if name == "net10.0"]
    if not targets or not any(expected <= set(target) for target in targets):
        raise SystemExit(f"{path}: wrong MinimalLambda versions for net10.0")
print("Positive consumers resolved exact synthetic versions from isolated cache.")
PY

rm -rf "$CONSUMERS/TypedConsumer/obj/generated" "$CONSUMERS/TaskConsumer/obj/generated"
build_consumer typed "$TYPED"
build_consumer task "$TASK"

rm -f "$LOGS/build-invalid-signature-net10.0.log"
set +e
dotnet build "$INVALID" \
  --configuration Release \
  --framework net10.0 \
  --no-restore \
  --nologo \
  "/p:CorePackageVersion=$CORE_VERSION" \
  "/p:DurablePackageVersion=$DURABLE_VERSION" \
  >"$LOGS/build-invalid-signature-net10.0.log" 2>&1
invalid_status=$?
set -e
cat "$LOGS/build-invalid-signature-net10.0.log"
[ "$invalid_status" -ne 0 ] || fail "invalid-signature consumer unexpectedly built"
invalid_ids=$(grep -Eo 'LH[0-9]{4}' "$LOGS/build-invalid-signature-net10.0.log" | sort -u | tr '\n' ' ' | sed 's/ $//')
[ "$invalid_ids" = "LH0007" ] || fail "invalid-signature consumer emitted unexpected generator diagnostics: ${invalid_ids:-none}"
grep -Fq "LH0007" "$LOGS/build-invalid-signature-net10.0.log" || fail "invalid-signature consumer did not emit LH0007"

rm -f "$LOGS/restore-old-core.log"
set +e
dotnet restore "$OLD" \
  --configfile "$CONFIG" \
  --force \
  --no-cache \
  --nologo \
  --tl:off \
  "/flp:logfile=$LOGS/restore-old-core.log;verbosity=normal" \
  "/p:CorePackageVersion=$OLD_CORE_VERSION" \
  "/p:DurablePackageVersion=$DURABLE_VERSION" \
  >/dev/null 2>&1
old_status=$?
set -e
cat "$LOGS/restore-old-core.log"
[ "$old_status" -ne 0 ] || fail "old-core restore unexpectedly succeeded"
old_ids=$(grep -Eo 'NU[0-9]{4}' "$LOGS/restore-old-core.log" | sort -u | tr '\n' ' ' | sed 's/ $//')
[ "$old_ids" = "NU1605" ] || fail "old-core restore failed with unexpected NuGet diagnostics: ${old_ids:-none}"
grep -Fq "$OLD_CORE_VERSION" "$LOGS/restore-old-core.log" || fail "old-core restore log lacks old version"
grep -Fq "$CORE_VERSION" "$LOGS/restore-old-core.log" || fail "old-core restore log lacks durable minimum version"

restore_consumer aot "$AOT" "$CORE_VERSION" --runtime "$RID"
python3 - "$CONSUMERS/AotConsumer/obj/project.assets.json" "$CORE_VERSION" "$DURABLE_VERSION" "$NUGET_PACKAGES" <<'PY'
import json
import sys
from pathlib import Path

path = Path(sys.argv[1])
core, durable = sys.argv[2:4]
packages = Path(sys.argv[4]).resolve()
data = json.loads(path.read_text(encoding="utf-8"))
folders = {str(Path(folder.rstrip("/\\")).resolve()) for folder in data["packageFolders"]}
if folders != {str(packages)}:
    raise SystemExit(f"AOT package cache is not isolated: {folders}")
expected = {f"MinimalLambda/{core}", f"MinimalLambda.DurableExecution/{durable}"}
if not any(expected <= set(target) for target in data["targets"].values()):
    raise SystemExit("AOT consumer did not resolve exact synthetic versions")
print("AOT consumer resolved exact synthetic versions from isolated cache.")
PY

rm -rf "$CONSUMERS/AotConsumer/obj/generated"
run_logged publish-aot dotnet publish "$AOT" \
  --configuration Release \
  --runtime "$RID" \
  --self-contained true \
  --no-restore \
  --nologo \
  "/p:CorePackageVersion=$CORE_VERSION" \
  "/p:DurablePackageVersion=$DURABLE_VERSION" \
  "/p:UseLdClassicXCodeLinker=false" \
  "/p:EmitCompilerGeneratedFiles=true" \
  "/p:CompilerGeneratedFilesOutputPath=$CONSUMERS/AotConsumer/obj/generated/net10.0"
assert_warning_free "$LOGS/publish-aot.log"
assert_generated_once "$CONSUMERS/AotConsumer" net10.0

printf '\nPackage compatibility matrix passed (RID %s).\n' "$RID"
