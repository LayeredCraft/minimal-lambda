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

REPO_PATHS = [
    "README.md",
    "docs/getting-started/core-concepts.md",
    "docs/guides/handler-registration.md",
    "docs/guides/dependency-injection.md",
    "docs/guides/middleware.md",
    "docs/guides/lifecycle-management.md",
    "docs/guides/configuration.md",
    "docs/guides/testing.md",
    "docs/features/envelopes.md",
    "docs/features/open_telemetry.md",
    "src/MinimalLambda/Builder/LambdaApplication.cs",
    "src/MinimalLambda/Builder/Extensions/BuilderLambdaApplicationExtensions.cs",
    "src/MinimalLambda/Builder/InterceptionTargets/MapHandlerLambdaApplicationExtensions.cs",
    "src/MinimalLambda/Core/Features/FeatureLambdaInvocationContextExtensions.cs",
    "src/MinimalLambda.Testing/README.md",
    "src/Envelopes/MinimalLambda.Envelopes.ApiGateway/README.md",
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

    missing_repo = [ROOT / p for p in REPO_PATHS if not (ROOT / p).exists()]
    if missing_repo:
        fail("missing repo source/doc paths:\n" + "\n".join(str(p) for p in missing_repo))

    for symbol, rel_path in SYMBOL_CHECKS.items():
        path = ROOT / rel_path
        if not path.exists():
            fail(f"symbol source path missing for {symbol}: {rel_path}")
        if symbol not in path.read_text(encoding="utf-8"):
            fail(f"symbol {symbol} not found in {rel_path}")

    skill_text = "\n".join(p.read_text(encoding="utf-8") for p in SKILL.rglob("*.md"))
    if re.search(r"\.Response\s*=", skill_text):
        fail("docs use non-existent IResponseFeature<T>.Response setter; use SetResponse(...)")

    for rel_path in REPO_PATHS:
        if rel_path not in skill_text and not re.search(re.escape(Path(rel_path).name), skill_text):
            print(f"WARN: repo path not mentioned explicitly: {rel_path}")

    print("OK: MinimalLambda skill references validated")


if __name__ == "__main__":
    main()
