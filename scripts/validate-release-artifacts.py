#!/usr/bin/env python3
"""Validate release tags and NuGet artifact manifests before trusted publishing."""

from __future__ import annotations

import argparse
import json
import re
import sys
import urllib.error
import urllib.request
import xml.etree.ElementTree as ET
import zipfile
from pathlib import Path

CORE_PACKAGE_IDS = frozenset(
    {
        "MinimalLambda",
        "MinimalLambda.Abstractions",
        "MinimalLambda.Envelopes",
        "MinimalLambda.Envelopes.Alb",
        "MinimalLambda.Envelopes.ApiGateway",
        "MinimalLambda.Envelopes.CloudWatchLogs",
        "MinimalLambda.Envelopes.Kafka",
        "MinimalLambda.Envelopes.Kinesis",
        "MinimalLambda.Envelopes.KinesisFirehose",
        "MinimalLambda.Envelopes.Sns",
        "MinimalLambda.Envelopes.Sqs",
        "MinimalLambda.OpenTelemetry",
        "MinimalLambda.Templates",
        "MinimalLambda.Testing",
    }
)
CORE_SYMBOL_PACKAGE_IDS = CORE_PACKAGE_IDS - {"MinimalLambda.Templates"}
DURABLE_PACKAGE_ID = "MinimalLambda.DurableExecution"
SEMVER = re.compile(
    r"^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)"
    r"(?:-(?:0|[1-9]\d*|\d*[A-Za-z-][0-9A-Za-z-]*)"
    r"(?:\.(?:0|[1-9]\d*|\d*[A-Za-z-][0-9A-Za-z-]*))*)?$"
)


def fail(message: str) -> None:
    raise ValueError(message)


def version_from_tag(lane: str, tag: str) -> str:
    prefix = "v" if lane == "core" else "durable-v"
    if not tag.startswith(prefix):
        fail(f"{lane} lane rejects tag {tag!r}; expected {prefix}<semver>")
    version = tag.removeprefix(prefix)
    if not SEMVER.fullmatch(version):
        fail(f"malformed {lane} release tag {tag!r}; expected {prefix}<semver>")
    return version


def local_name(element: ET.Element) -> str:
    return element.tag.rsplit("}", 1)[-1]


def read_identity(package: Path) -> tuple[str, str, ET.Element]:
    try:
        with zipfile.ZipFile(package) as archive:
            nuspecs = [name for name in archive.namelist() if name.endswith(".nuspec")]
            if len(nuspecs) != 1:
                fail(f"{package.name}: expected one nuspec, found {len(nuspecs)}")
            root = ET.fromstring(archive.read(nuspecs[0]))
    except (zipfile.BadZipFile, ET.ParseError) as error:
        fail(f"{package.name}: unreadable package metadata: {error}")

    metadata = next((item for item in root.iter() if local_name(item) == "metadata"), None)
    if metadata is None:
        fail(f"{package.name}: nuspec has no metadata")
    package_id = next((item.text for item in metadata if local_name(item) == "id"), None)
    version = next((item.text for item in metadata if local_name(item) == "version"), None)
    if not package_id or not version:
        fail(f"{package.name}: nuspec identity is incomplete")
    return package_id, version, root


def durable_project_data() -> tuple[str, int]:
    project = (
        Path(__file__).resolve().parents[1]
        / "src/MinimalLambda.DurableExecution/MinimalLambda.DurableExecution.csproj"
    )
    root = ET.parse(project).getroot()
    minimum = next(
        (item.text for item in root.iter() if local_name(item) == "MinimalLambdaMinimumVersion"),
        None,
    )
    target_frameworks = next(
        (item.text for item in root.iter() if local_name(item) == "TargetFrameworks"),
        None,
    )
    if not minimum:
        fail(f"{project}: MinimalLambdaMinimumVersion is missing")
    frameworks = [framework for framework in (target_frameworks or "").split(";") if framework]
    if not frameworks:
        fail(f"{project}: TargetFrameworks is missing")
    return minimum, len(frameworks)


def validate_durable_dependencies(nuspec: ET.Element, minimum: str, expected_groups: int) -> None:
    dependencies = next(
        (item for item in nuspec.iter() if local_name(item) == "dependencies"),
        None,
    )
    groups = [] if dependencies is None else [item for item in dependencies if local_name(item) == "group"]
    if len(groups) != expected_groups:
        fail(f"durable nuspec must contain {expected_groups} TFM dependency groups; found={len(groups)}")

    seen_frameworks: set[str] = set()
    for group in groups:
        framework = group.attrib.get("targetFramework", "")
        if not framework or framework in seen_frameworks:
            fail(f"durable nuspec has missing/duplicate dependency targetFramework {framework!r}")
        seen_frameworks.add(framework)
        versions = [
            item.attrib.get("version")
            for item in group
            if local_name(item) == "dependency" and item.attrib.get("id") == "MinimalLambda"
        ]
        if versions != [minimum]:
            fail(
                f"MinimalLambda dependency in {framework} must occur once at minimum "
                f"{minimum!r}; found={versions}"
            )


def nuget_version_exists(package_id: str, version: str) -> bool:
    url = (
        "https://api.nuget.org/v3-flatcontainer/"
        f"{package_id.lower()}/index.json"
    )
    try:
        with urllib.request.urlopen(url, timeout=30) as response:
            payload = json.load(response)
    except urllib.error.HTTPError as error:
        if error.code == 404:
            return False
        raise
    versions = payload.get("versions", [])
    return version.casefold() in {str(item).casefold() for item in versions}


def validate_no_nuget_collisions(package_ids: set[str] | frozenset[str], version: str) -> None:
    collisions = [package_id for package_id in sorted(package_ids) if nuget_version_exists(package_id, version)]
    if collisions:
        fail(f"NuGet package/version already exists for {version}: {', '.join(collisions)}")
    print(f"validated NuGet availability: {len(package_ids)} package IDs at {version}")


def validate_artifacts(
    lane: str,
    artifacts: Path,
    expected_version: str | None,
    preview_run_number: str | None,
    expected_minimum: str | None,
) -> tuple[set[str] | frozenset[str], str]:
    if not artifacts.is_dir():
        fail(f"artifact directory does not exist: {artifacts}")

    files = sorted(path for path in artifacts.rglob("*") if path.is_file())
    unexpected_files = [path.name for path in files if not path.name.endswith((".nupkg", ".snupkg"))]
    if unexpected_files:
        fail(f"unexpected artifact files: {', '.join(unexpected_files)}")

    primary = [path for path in files if path.name.endswith(".nupkg") and not path.name.endswith(".snupkg")]
    symbols = [path for path in files if path.name.endswith(".snupkg")]
    identities: dict[str, tuple[str, Path, ET.Element]] = {}
    for package in primary:
        package_id, version, nuspec = read_identity(package)
        if package_id in identities:
            fail(f"duplicate package ID: {package_id}")
        identities[package_id] = (version, package, nuspec)

    expected_ids = CORE_PACKAGE_IDS if lane in {"core", "core-preview"} else {DURABLE_PACKAGE_ID}
    actual_ids = set(identities)
    if actual_ids != expected_ids:
        missing = sorted(expected_ids - actual_ids)
        extra = sorted(actual_ids - expected_ids)
        fail(f"package ID collision/omission; missing={missing}, unexpected={extra}")

    versions = {identity[0] for identity in identities.values()}
    if len(versions) != 1:
        fail(f"package versions differ: {sorted(versions)}")
    actual_version = next(iter(versions))
    if expected_version is not None and actual_version != expected_version:
        fail(f"artifact version {actual_version!r} does not match expected version {expected_version!r}")
    if lane == "core-preview":
        if preview_run_number is None:
            fail("core-preview validation requires --preview-run-number")
        if not re.fullmatch(rf"\d+\.\d+\.\d+-preview\.{re.escape(preview_run_number)}", actual_version):
            fail(f"preview version {actual_version!r} does not end in -preview.{preview_run_number}")

    expected_primary_names = {f"{package_id}.{actual_version}.nupkg" for package_id in expected_ids}
    actual_primary_names = {path.name for path in primary}
    if actual_primary_names != expected_primary_names:
        fail(f"primary filenames differ; expected={sorted(expected_primary_names)}, actual={sorted(actual_primary_names)}")

    expected_symbol_ids = CORE_SYMBOL_PACKAGE_IDS if lane in {"core", "core-preview"} else {DURABLE_PACKAGE_ID}
    symbol_identities: dict[str, tuple[str, Path]] = {}
    for package in symbols:
        package_id, version, _ = read_identity(package)
        if package_id in symbol_identities:
            fail(f"duplicate symbol package ID: {package_id}")
        symbol_identities[package_id] = (version, package)
    if set(symbol_identities) != expected_symbol_ids:
        missing = sorted(expected_symbol_ids - set(symbol_identities))
        extra = sorted(set(symbol_identities) - expected_symbol_ids)
        fail(f"symbol package ID collision/omission; missing={missing}, unexpected={extra}")
    for package_id, (version, path) in symbol_identities.items():
        if version != actual_version:
            fail(f"{path.name}: symbol version {version!r} does not match {actual_version!r}")
        expected_name = f"{package_id}.{actual_version}.snupkg"
        if path.name != expected_name:
            fail(f"symbol filename differs; expected={expected_name!r}, actual={path.name!r}")

    if lane == "durable":
        project_minimum, expected_groups = durable_project_data()
        minimum = expected_minimum or project_minimum
        validate_durable_dependencies(identities[DURABLE_PACKAGE_ID][2], minimum, expected_groups)

    manifest = ", ".join(f"{package_id}@{identities[package_id][0]}" for package_id in sorted(identities))
    print(f"validated {lane} manifest: {manifest}; symbols={len(symbols)}")
    return expected_ids, actual_version


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--lane", choices=("core", "core-preview", "durable"), required=True)
    parser.add_argument("--tag")
    parser.add_argument("--artifacts", type=Path)
    parser.add_argument("--tag-only", action="store_true")
    parser.add_argument("--preview-run-number")
    parser.add_argument("--expected-version")
    parser.add_argument("--expected-minimum-version")
    parser.add_argument("--check-nuget", action="store_true")
    args = parser.parse_args()

    try:
        expected_version = args.expected_version
        if args.lane in {"core", "durable"}:
            if not args.tag:
                fail(f"{args.lane} validation requires --tag")
            tagged_version = version_from_tag(args.lane, args.tag)
            if expected_version is not None and expected_version != tagged_version:
                fail(f"explicit version {expected_version!r} does not match tag version {tagged_version!r}")
            expected_version = tagged_version
        elif args.tag:
            fail("core-preview lane does not accept a release tag")

        if args.tag_only:
            if args.lane == "core-preview":
                fail("--tag-only is invalid for core-preview")
            print(f"validated {args.lane} tag: {args.tag} -> {expected_version}")
            return 0
        if args.artifacts is None:
            fail("artifact validation requires --artifacts")
        package_ids, actual_version = validate_artifacts(
            args.lane,
            args.artifacts,
            expected_version,
            args.preview_run_number,
            args.expected_minimum_version,
        )
        if args.check_nuget:
            validate_no_nuget_collisions(package_ids, actual_version)
    except (OSError, ValueError, urllib.error.URLError, zipfile.BadZipFile, ET.ParseError) as error:
        print(f"release validation failed: {error}", file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
