#!/usr/bin/env python3
"""Focused negative tests for release artifact validation."""

from __future__ import annotations

import importlib.util
import tempfile
import zipfile
from pathlib import Path

SCRIPT = Path(__file__).with_name("validate-release-artifacts.py")
spec = importlib.util.spec_from_file_location("release_validator", SCRIPT)
assert spec and spec.loader
validator = importlib.util.module_from_spec(spec)
spec.loader.exec_module(validator)

VERSION = "81.2.3-rc.7"
MINIMUM = "2.6.0-beta.2"
PACKAGE_ID = "MinimalLambda.DurableExecution"


def nuspec(package_id: str, groups: list[tuple[str, bool]]) -> str:
    dependencies = "".join(
        f'<group targetFramework="{framework}">'
        + (f'<dependency id="MinimalLambda" version="{MINIMUM}" />' if has_core else "")
        + '<dependency id="Amazon.Lambda.DurableExecution" version="1.0.0" />'
        + "</group>"
        for framework, has_core in groups
    )
    return (
        '<?xml version="1.0"?>'
        '<package><metadata>'
        f'<id>{package_id}</id><version>{VERSION}</version>'
        f'<dependencies>{dependencies}</dependencies>'
        '</metadata></package>'
    )


def write_package(path: Path, package_id: str = PACKAGE_ID, *, missing_core: bool = False) -> None:
    groups = [("net10.0", not missing_core)]
    with zipfile.ZipFile(path, "w") as archive:
        archive.writestr(f"{package_id}.nuspec", nuspec(package_id, groups))


def expect_failure(name: str, action, contains: str) -> None:
    try:
        action()
    except ValueError as error:
        if contains not in str(error):
            raise AssertionError(f"{name}: wrong error: {error}") from error
        print(f"passed negative test: {name}: {error}")
        return
    raise AssertionError(f"{name}: validation unexpectedly passed")


def validate(directory: Path) -> None:
    validator.validate_artifacts("durable", directory, VERSION, None, MINIMUM)


def write_template_package(path: Path, *, durable_template: bool) -> None:
    with zipfile.ZipFile(path, "w") as archive:
        archive.writestr("MinimalLambda.Templates.nuspec", nuspec("MinimalLambda.Templates", []))
        if durable_template:
            archive.writestr("content/templates/mlambda-durable/Program.cs", "// deferred")


def main() -> None:
    with tempfile.TemporaryDirectory(prefix="minimal-lambda-validator-") as temporary:
        root = Path(temporary)

        corrupt = root / "corrupt"
        corrupt.mkdir()
        write_package(corrupt / f"{PACKAGE_ID}.{VERSION}.nupkg")
        (corrupt / f"{PACKAGE_ID}.{VERSION}.snupkg").write_text("not a zip", encoding="utf-8")
        expect_failure("corrupt symbol archive", lambda: validate(corrupt), "unreadable package metadata")

        renamed = root / "renamed"
        renamed.mkdir()
        write_package(renamed / f"{PACKAGE_ID}.{VERSION}.nupkg")
        write_package(
            renamed / f"{PACKAGE_ID}.{VERSION}.snupkg",
            "Other.Owner.Package",
        )
        expect_failure("renamed foreign symbol", lambda: validate(renamed), "symbol package ID collision/omission")

        missing_group = root / "missing-group"
        missing_group.mkdir()
        write_package(
            missing_group / f"{PACKAGE_ID}.{VERSION}.nupkg",
            missing_core=True,
        )
        write_package(missing_group / f"{PACKAGE_ID}.{VERSION}.snupkg")
        expect_failure(
            "missing TFM dependency",
            lambda: validate(missing_group),
            "must occur once",
        )

        template_package = root / "MinimalLambda.Templates.nupkg"
        write_template_package(template_package, durable_template=True)
        expect_failure(
            "deferred durable template content",
            lambda: validator.validate_templates_content(template_package),
            "must not ship deferred mlambda-durable template",
        )

        original_lookup = validator.nuget_version_exists
        validator.nuget_version_exists = lambda package_id, version: package_id == PACKAGE_ID
        try:
            expect_failure(
                "existing NuGet version collision",
                lambda: validator.validate_no_nuget_collisions({PACKAGE_ID}, VERSION),
                "already exists",
            )
        finally:
            validator.nuget_version_exists = original_lookup


if __name__ == "__main__":
    main()
