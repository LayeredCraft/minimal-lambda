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
PACKAGE_ID = "MinimalLambda.DurableExecution"


def expect_failure(name: str, action, contains: str) -> None:
    try:
        action()
    except ValueError as error:
        if contains not in str(error):
            raise AssertionError(f"{name}: wrong error: {error}") from error
        print(f"passed negative test: {name}: {error}")
        return
    raise AssertionError(f"{name}: validation unexpectedly passed")


def write_template_package(path: Path) -> None:
    with zipfile.ZipFile(path, "w") as archive:
        archive.writestr("content/templates/mlambda-durable/Program.cs", "// deferred")


def main() -> None:
    if validator.version_from_tag("core", f"v{VERSION}") != VERSION:
        raise AssertionError("standard release tag did not produce package version")
    expect_failure(
        "separate durable release tag",
        lambda: validator.version_from_tag("core", f"durable-v{VERSION}"),
        "expected v<semver>",
    )

    with tempfile.TemporaryDirectory(prefix="minimal-lambda-validator-") as temporary:
        template_package = Path(temporary) / "MinimalLambda.Templates.nupkg"
        write_template_package(template_package)
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
