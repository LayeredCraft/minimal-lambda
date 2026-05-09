#!/usr/bin/env python3
"""Validate MinimalLambda skill references against repo layout.

Run from repo root:
  python skills/minimal-lambda/scripts/validate_references.py
"""

from __future__ import annotations

from pathlib import Path
import re
import sys

ROOT = Path.cwd()
SKILL = ROOT / "skills" / "minimal-lambda"

REQUIRED_FILES = [
    SKILL / "SKILL.md",
    SKILL / "references" / "core-hosting.md",
    SKILL / "references" / "best-practices.md",
    SKILL / "references" / "client-project-setup.md",
    SKILL / "references" / "envelopes.md",
    SKILL / "references" / "testing.md",
    SKILL / "references" / "opentelemetry.md",
    SKILL / "references" / "troubleshooting.md",
    SKILL / "references" / "repo-workflow.md",
    SKILL / "references" / "patterns" / "handler-patterns.md",
    SKILL / "references" / "patterns" / "middleware-patterns.md",
    SKILL / "references" / "patterns" / "envelope-patterns.md",
    SKILL / "references" / "patterns" / "testing-patterns.md",
    SKILL / "references" / "patterns" / "aot-and-envelopes.md",
    SKILL / "evals" / "evals.json",
]

SYMBOL_CHECKS = {
    "LambdaApplication": "src/MinimalLambda/Builder/LambdaApplication.cs",
    "MapHandler": "src/MinimalLambda/Builder/InterceptionTargets/MapHandlerLambdaApplicationExtensions.cs",
    "FromEventAttribute": "src/MinimalLambda.Abstractions/Attributes/FromEventAttribute.cs",
    "AddLambdaSerializerWithContext": "src/MinimalLambda/Builder/Extensions/SerializerServiceCollectionExtensions.cs",
    "ConfigureEnvelopeOptions": "src/MinimalLambda/Builder/Extensions/ConfigurationServiceCollectionExtensions.cs",
    "LambdaApplicationFactory": "src/MinimalLambda.Testing/LambdaApplicationFactory.cs",
    "ApiGatewayV2RequestEnvelope": "src/Envelopes/MinimalLambda.Envelopes.ApiGateway/ApiGatewayV2RequestEnvelope.cs",
    "SqsEnvelope": "src/Envelopes/MinimalLambda.Envelopes.Sqs/SqsEnvelope.cs",
    "KinesisEnvelope": "src/Envelopes/MinimalLambda.Envelopes.Kinesis/KinesisEnvelope.cs",
}


def fail(message: str) -> None:
    print(f"FAIL: {message}")
    sys.exit(1)


def main() -> None:
    missing = [path for path in REQUIRED_FILES if not path.exists()]
    if missing:
        fail("missing skill files:\n" + "\n".join(str(p) for p in missing))

    for symbol, rel_path in SYMBOL_CHECKS.items():
        path = ROOT / rel_path
        if not path.exists():
            fail(f"symbol source path missing for {symbol}: {rel_path}")
        if symbol not in path.read_text(encoding="utf-8"):
            fail(f"symbol {symbol} not found in {rel_path}")

    skill_text = "\n".join(p.read_text(encoding="utf-8") for p in SKILL.rglob("*.md"))
    if re.search(r"\.Response\s*=", skill_text):
        fail("docs use non-existent IResponseFeature<T>.Response setter; use SetResponse(...)")

    client_reference_text = "\n".join(
        p.read_text(encoding="utf-8")
        for p in SKILL.rglob("*.md")
        if p.name != "repo-workflow.md"
    )
    if re.search(r"`(?:src|docs|tests|examples)/", client_reference_text):
        fail("client-facing skill references should not point at MinimalLambda repo-local paths")

    print("OK: MinimalLambda skill references validated")


if __name__ == "__main__":
    main()
