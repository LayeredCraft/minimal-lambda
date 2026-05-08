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

## Releases

- Release Drafter creates draft releases from PR titles.
- User manually publishes GitHub release.
- Publishing release triggers NuGet package publishing.
- Do not manually bump `Directory.Build.props` for routine releases.
- Do not publish NuGet packages manually.
- Do not create GitHub releases directly.

Packages version synchronously:

- `MinimalLambda`
- `MinimalLambda.Abstractions`
- `MinimalLambda.OpenTelemetry`
