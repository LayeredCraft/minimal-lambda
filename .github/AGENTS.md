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

- Use `LayeredCraft/devops-templates` reusable workflows whenever they support required behavior. Do not replace them with custom workflow logic for package building, publishing, or standard PR checks.
- If a local workflow or step is required, document why DevOps templates cannot support it in the PR and keep exception narrowly scoped.
- PR build, PR title check, release drafter, preview publishing, and release publishing use `LayeredCraft/devops-templates` reusable workflows.
- `pr-quality.yaml` is intentionally local: it preserves MinimalLambda-specific AOT, CleanupCode formatting, and Codecov gates.
- `docs.yaml` is intentionally local: it builds/deploys the Zensical docs site with `uv` and GitHub Pages.

## Releases

- Release Drafter creates draft releases from PR titles.
- Merges to `main` publish preview packages through NuGet trusted publishing.
- User manually publishes GitHub release.
- Publishing release triggers release NuGet package publishing through NuGet trusted publishing.
- Do not use NuGet API keys for publishing.
- Do not manually bump `Directory.Build.props` for routine releases.
- Do not publish NuGet packages manually.
- Do not create GitHub releases directly.

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

`MinimalLambda.DurableExecution` publishes independently from `durable-v<semver>` releases. Its package declares minimum compatible core version.
