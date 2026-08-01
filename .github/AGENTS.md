# AGENTS.md (GitHub / CI / release)

## PRs

- PR titles must use Conventional Commits format.
- Use `.github/pull_request_template.md` for PR descriptions.
- Dependabot PRs are exempt from title validation.

Allowed types:

- `feat`
- `fix`
- `docs`
- `refactor`
- `test`
- `chore`
- `ci`

Allowed scopes:

- `host`
- `envelopes`
- `abstractions`
- `opentelemetry`
- `source-generators`
- `deps`
- `build`
- `ci`
- `github`
- `core`
- `docs`
- `testing`
- `tests`

Examples:

- `feat(host): add handler support`
- `fix(abstractions): resolve dependency issue`
- `docs: update README examples`

## Workflows

- PR build, PR title check, and release drafter use `LayeredCraft/devops-templates` reusable workflows.
- Package publishing stays local because `devops-templates` v10.1 uploads only `*.nupkg`, strips only a leading `v` from release tags, and its push action suppresses duplicate failures. Local lanes need exact symbol manifests, independent `durable-v` tags, collision failure, and no `--skip-duplicate`.
- Publish jobs use `NuGet/login@v1` directly for trusted-publishing credentials. Never replace OIDC with stored NuGet API keys.
- `pr-quality.yaml` is intentionally local: it preserves MinimalLambda-specific AOT, CleanupCode formatting, and Codecov gates.
- `docs.yaml` is intentionally local: it builds/deploys the Zensical docs site with `uv` and GitHub Pages.

## Releases

- Release Drafter creates draft releases from PR titles.
- Merges to `main` publish preview packages through NuGet trusted publishing.
- User manually publishes GitHub release.
- Publishing a `v<semver>` release publishes the synchronous core cohort through NuGet trusted publishing. Core preview and release packing use `MinimalLambda.Packages.slnf`; they never pack `MinimalLambda.DurableExecution`.
- Publishing a `durable-v<semver>` release publishes only `MinimalLambda.DurableExecution` and its symbols through NuGet trusted publishing. Prerelease SemVer tags such as `durable-v2.7.0-beta.1` use this same lane.
- Core workflow ignores durable tags. Durable workflow ignores core tags. Artifact guards reject malformed lane tags, unexpected package IDs, version mismatches, missing/corrupt symbols, existing NuGet versions, and package collisions before push.
- NuGet trusted-publisher policy must explicitly authorize `.github/workflows/publish-preview.yaml`, `.github/workflows/publish-release.yaml`, and `.github/workflows/publish-durable-release.yaml`. Verify external policy before first run of each lane.
- Do not use NuGet API keys for publishing.
- Do not manually bump `Directory.Build.props` for routine core releases.
- Do not publish NuGet packages manually.
- Do not create GitHub releases directly.
- Run `task local:release-dry-run` to pack distinct fake core and durable versions and validate isolated manifests without publishing.

Core packages version synchronously:

- `MinimalLambda`
- `MinimalLambda.Abstractions`
- `MinimalLambda.Envelopes`
- `MinimalLambda.Envelopes.Alb`
- `MinimalLambda.Envelopes.ApiGateway`
- `MinimalLambda.Envelopes.CloudWatchLogs`
- `MinimalLambda.Envelopes.Kafka`
- `MinimalLambda.Envelopes.Kinesis`
- `MinimalLambda.Envelopes.KinesisFirehose`
- `MinimalLambda.Envelopes.Sns`
- `MinimalLambda.Envelopes.Sqs`
- `MinimalLambda.OpenTelemetry`
- `MinimalLambda.Templates`
- `MinimalLambda.Testing`

`MinimalLambda.DurableExecution` versions independently. Its nuspec dependency on `MinimalLambda` remains the minimum declared by `MinimalLambdaMinimumVersion` in its project. Generator-contract changes can require a core release first, followed by a deliberate `MinimalLambdaMinimumVersion` bump before the durable release. Once that contract floor is published and recorded, later durable releases do not require a matching core version.

Durable workflow validates local durable artifact plus exact published core minimum before push. After trusted publishing, it retries restore of exact published pair to cover NuGet propagation delay.
