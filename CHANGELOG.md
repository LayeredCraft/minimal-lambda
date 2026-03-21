# Changelog

All notable changes to this project will be documented in this file.
See [Conventional Commits](https://conventionalcommits.org) for commit guidelines.

## [Unreleased](https://github.com/j-d-ha/minimal-lambda/compare/v2.3.0...HEAD)

## [v2.3.0](https://github.com/j-d-ha/minimal-lambda/compare/v2.2.0...v2.3.0) - 2026-03-21

### 🚀 Features

* feat(testing): migrate test infrastructure to Microsoft Testing Platform (#288) @j-d-ha

### 🐛 Bug Fixes

* fix(deps): switch xunit to mtp-v2 and fix Scriban build error (#309) @j-d-ha

### 🔄 Refactoring

* refactor: consolidate code style and improve readability (#291) @j-d-ha

### ✅ Tests

* github: rewrite PR guide for clarity and detail (#294) @j-d-ha

### 🔧 Maintenance

* chore: Bump the minor-and-patch group with 20 updates (#307) @dependabot
* chore: Bump jetbrains.resharper.globaltools from 2025.3.1 to 2025.3.3 (#310) @dependabot
* chore(deps-dev): bump the minor-and-patch group with 2 updates (#299) @dependabot
* chore(deps): bump dotnet-sdk from 10.0.101 to 10.0.201 (#306) @dependabot
* chore(deps-dev): bump the minor-and-patch group with 2 updates (#296) @dependabot
* chore: bump urllib3 from 2.5.0 to 2.6.3 in the uv group across 1 directory (#286) @dependabot
* chore: Bump the minor-and-patch group with 16 updates (#287) @dependabot
* chore(deps): bump dotnet-sdk from 10.0.101 to 10.0.102 (#285) @dependabot

## [v2.2.0](https://github.com/j-d-ha/minimal-lambda/compare/v2.1.1...v2.2.0) - 2026-01-12

### 🚀 Features

* feat(ci): add auto-approval to automated PR workflows (#283) @j-d-ha
* feat(tests): add multi-targeting and source generator improvements (#274) @j-d-ha
* feat(tests): add scrubbers for dynamic snapshot values (#273) @j-d-ha

### 🔄 Refactoring

* refactor(source-generators): modularize code generation and improve generated file organization (#280) @j-d-ha
* refactor(source-generators): simplify and restructure code generation logic (#279) @j-d-ha

### 🔧 Maintenance

* chore: bump the minor-and-patch group with 6 updates (#281) @dependabot
* chore(deps-dev): Bump the minor-and-patch group with 2 updates (#277) @dependabot
* chore: Bump the minor-and-patch group with 4 updates (#275) @dependabot

## [v2.1.1](https://github.com/j-d-ha/minimal-lambda/compare/v2.0.0...v2.1.1) - 2025-12-22

### 🚀 Features

* feat(build): add comprehensive AOT compatibility support (#267) @j-d-ha
* feat(host): add factory-based middleware creation support (#266) @j-d-ha
* feat(source-generators): add class-based middleware support with UseMiddleware<T>() (#257) @j-d-ha

### 🐛 Bug Fixes

* fix: prevent validation project from being packaged (#270) @j-d-ha
* fix(ci): add Task installation step to main-build pipeline (#268) @j-d-ha

### 🔧 Maintenance

* fix(ci): add Task installation step to main-build pipeline (#268) @j-d-ha
* chore: migrate InternalsVisibleTo declarations to project files (#265) @j-d-ha
* chore(deps): migrate to LayeredCraft.SourceGeneratorTools.Generator v0.1.0-beta.10 (#264) @j-d-ha
* chore: update changelog for v2.0.0 (#256) @github-actions

## [v2.0.0](https://github.com/j-d-ha/minimal-lambda/compare/v1.3.1...v2.0.0) - 2025-12-18

### 🚀 Features

* feat(host): add ILambdaLifecycleContext for lifecycle handlers (#252) @j-d-ha
* feat(testing): add customizable JSON serializer options to LambdaTestServer (#251) @j-d-ha
* feat(envelopes): support multiple response types with shared extension methods (#213) @j-d-ha
* feat(testing): add MinimalLambda.Testing package (#233) @j-d-ha
* feat(docs): refine messaging to emphasize Lambda-first design (#228) @j-d-ha

### 🐛 Bug Fixes

* fix(testing): prevent DI container disposal during test execution (#234) @j-d-ha
* fix(build): add build targets for packaging (#231) @j-d-ha
* fix(core): adjust default timeout values and update documentation (#226) @j-d-ha

### 📚 Documentation

* docs: document MinimalLambda.Testing package and enhance testing guide (#235) @j-d-ha

### 🔄 Refactoring

* refactor(source-generators): improve type casting and output generation (#254) @j-d-ha
* refactor(abstractions): rename ILambdaHostContext to ILambdaInvocationContext (#253) @j-d-ha
* refactor(core): replace [Event] attribute with [FromEvent] (#250) @j-d-ha
* refactor: rename framework from AwsLambda.Host to MinimalLambda (#227) @j-d-ha

### 🔧 Maintenance

* chore(deps): bump dotnet-sdk from 10.0.100 to 10.0.101 (#239) @dependabot
* chore(deps): bump actions/checkout from 4 to 6 (#241) @dependabot
* chore(deps): bump the minor-and-patch group with 2 updates (#242) @dependabot
* chore(deps): bump actions/setup-python from 5 to 6 (#245) @dependabot
* chore(deps): bump actions/upload-pages-artifact from 3 to 4 (#246) @dependabot
* chore(github): add pip ecosystem and ignore release-drafter (#248) @j-d-ha
* chore(deps): Bump the minor-and-patch group with 12 updates (#247) @dependabot
* chore(deps): update Microsoft.Extensions.Hosting from RC to stable (#232) @j-d-ha
* chore: remove obsolete code and deprecated features (#229) @j-d-ha

### ⚠️ Breaking Changes

* refactor(abstractions): rename ILambdaHostContext to ILambdaInvocationContext (#253) @j-d-ha
* feat(host): add ILambdaLifecycleContext for lifecycle handlers (#252) @j-d-ha
* refactor: rename framework from AwsLambda.Host to MinimalLambda (#227) @j-d-ha

## [v1.3.1](https://github.com/j-d-ha/minimal-lambda/compare/v1.3.0...v1.3.1) - 2025-12-10

### 🐛 Bug Fixes

* fix(core): add missing TenantId and TraceId properties to DefaultLambdaHostContext (#224) @j-d-ha
* fix(ci): remove sign-commits from changelog workflow (#223) @j-d-ha

## [v1.3.0](https://github.com/j-d-ha/minimal-lambda/compare/v1.2.1...v1.3.0) - 2025-12-10

## 🚀 Features

* feat(source-generators): support multiple MapHandler invocations with custom feature providers (#214) @j-d-ha
* docs: update MkDocs palette toggle configuration (#211) @j-d-ha

## 🐛 Bug Fixes

* fix: update third-party license attributions (#217) @j-d-ha

## 📚 Documentation

* docs: add comprehensive getting started guide and restructure documentation (#209) @j-d-ha

## 🔄 Refactoring

* refactor(host): migrate BootstrapHttpClient from options to dependency injection (#219) @j-d-ha
* refactor(docs): replace ASPNETCORE_ENVIRONMENT with DOTNET_ENVIRONMENT (#216) @j-d-ha

## 🔧 Maintenance

* ci(github): optimize workflow triggers for draft PRs (#215) @j-d-ha

## [v1.2.1](https://github.com/j-d-ha/minimal-lambda/compare/v1.2.0...v1.2.1) - 2025-11-30

## 🐛 Bug Fixes

* fix: update build versioning (#206) @j-d-ha
* feat(ci): fixed interceptor namespace (#205) @ncipollina
* feat(ci): add transitive builds (#202) @ncipollina

## 📚 Documentation

* docs: add GitHub Pages landing page with MkDocs (#203) @j-d-ha

## 🔧 Maintenance

* ci: skip build workflows for docs-only changes (#204) @j-d-ha
* chore(github): enhance changelog update workflow (#201) @j-d-ha

## [v1.2.0](https://github.com/j-d-ha/minimal-lambda/compare/v1.1.0...v1.2.0) - 2025-11-29

## 🚀 Features

* feat(envelopes): add SqsSnsEnvelope for SNS-to-SQS subscription pattern (#196) @j-d-ha
* feat(envelopes): add CloudWatch Logs envelope support (#195) @j-d-ha
* feat(envelopes): add Kafka envelope support (#194) @j-d-ha
* feat(envelopes): add Kinesis Firehose envelope support (#193) @j-d-ha
* feat(envelopes): add Kinesis envelope for strongly-typed message handling (#192) @j-d-ha
* feat(envelopes): add Application Load Balancer envelope support (#187) @j-d-ha
* feat(envelopes): expand envelope options for multiple serialization formats (#184) @j-d-ha

## 🐛 Bug Fixes

* fix(ci): add explicit token to checkout and create-pull-request actions (#183) @j-d-ha

## 📚 Documentation

* docs: standardize README documentation across all packages (#200) @j-d-ha
* feat(envelopes): add SNS envelope for strongly-typed message handling (#190) @j-d-ha

## 🔄 Refactoring

* refactor(abstractions): replace Lazy implementation with property for JSON options (#199) @j-d-ha

## 🔧 Maintenance

* chore: cleanup repository formatting and documentation (#197) @j-d-ha
* chore(ci): pinned release-drafter action to 6.0.0 (#182) @j-d-ha

## [v1.1.0](https://github.com/j-d-ha/minimal-lambda/compare/v1.0.0...v1.1.0) - 2025-11-27

## 🚀 Features

* feat(host): add stream feature abstraction layer (#181) @j-d-ha
* feat(core): add ILambdaHostContextAccessor and context factory integration (#178) @j-d-ha
* feat(source-generators): add GeneratedCodeAttribute to all generated code classes (#174) @j-d-ha

## 🐛 Bug Fixes

* fix(ci): update changelog workflow to create pull request instead of direct commit (#172) @j-d-ha
* fix(ci): add write permissions for release asset uploads (#171) @j-d-ha

## 📚 Documentation

* docs: add GitHub issue and discussion templates (#176) @j-d-ha

## 🔧 Maintenance

* chore(github): update changelog for v1.0.0 release (#170) @j-d-ha

## [v1.0.0](https://github.com/j-d-ha/minimal-lambda/compare/v0.1.3...v1.0.0) - 2025-11-24

## 🚀 Features

* feat(host): refactor builder entrypoint and improve configuration (#166) @j-d-ha
* test(source-generators): add comprehensive tests for `HashCode` type (#153) @j-d-ha
* feat(envelopes): add modular envelope system for Lambda event handling (#131) @j-d-ha

## 🐛 Bug Fixes

* fix: resolve Sonar findings in tests and runtime code (#147) @j-d-ha

## 📚 Documentation

* docs: enhance package README overviews with detailed feature descriptions (#124) @j-d-ha

## 🔄 Refactoring

* refactor(core): refactor lambda application and builder (#163) @j-d-ha
* refactor(host): reorganize folder structure with hierarchical layers (#146) @j-d-ha

## ✅ Tests

* test(source-generators): add comprehensive tests for `HashCode` type (#153) @j-d-ha

## 🔧 Maintenance

* chore(deps): Bump the minor-and-patch group with 1 update (#165) @[dependabot[bot]](https://github.com/apps/dependabot)
* chore(github): configure Dependabot to use conventional commits (#164) @j-d-ha
* chore(deps): bump actions/checkout from 5 to 6 (#158) @[dependabot[bot]](https://github.com/apps/dependabot)
* chore(deps-dev): bump husky from 8.0.3 to 9.1.7 (#149) @[dependabot[bot]](https://github.com/apps/dependabot)
* chore(deps-dev): bump @commitlint/cli from 18.6.1 to 20.1.0 (#150) @[dependabot[bot]](https://github.com/apps/dependabot)
* chore(deps-dev): bump @commitlint/config-conventional from 18.6.3 to 20.0.0 (#151) @[dependabot[bot]](https://github.com/apps/dependabot)
* feat(opentelemetry): add OpenTelemetry unit tests and update dependencies (#148) @j-d-ha
* chore(deps-dev): bump js-yaml from 4.1.0 to 4.1.1 in the npm_and_yarn group across 1 directory (#132) @[dependabot[bot]](https://github.com/apps/dependabot)
* ci: skip workflows for draft pull requests (#127) @j-d-ha
* ci: skip pr build for draft pull requests (#126) @j-d-ha
* ci(github): replace softprops action with gh release upload command (#123) @j-d-ha
* chore(deps): bump amannn/action-semantic-pull-request from 5.4.0 to 6.1.1 (#121) @[dependabot[bot]](https://github.com/apps/dependabot)
* chore(deps): bump peter-evans/create-pull-request from 5 to 7 (#119) @[dependabot[bot]](https://github.com/apps/dependabot)
* chore(deps): bump actions/checkout from 4 to 5 (#118) @[dependabot[bot]](https://github.com/apps/dependabot)
* chore(deps): bump stefanzweifel/git-auto-commit-action from 5 to 7 (#117) @[dependabot[bot]](https://github.com/apps/dependabot)
* chore(deps): bump release-drafter/release-drafter from 6.0.0 to 6.1.0 in the minor-and-patch group (#116) @[dependabot[bot]](https://github.com/apps/dependabot)

## ⚠️ Breaking Changes

* feat(host): refactor builder entrypoint and improve configuration (#166) @j-d-ha
* refactor(core): refactor lambda application and builder (#163) @j-d-ha
* refactor(host): reorganize folder structure with hierarchical layers (#146) @j-d-ha
* feat(envelopes): add modular envelope system for Lambda event handling (#131) @j-d-ha

## [v0.1.3](https://github.com/j-d-ha/minimal-lambda/compare/v0.1.2...v0.1.3) - 2025-11-10

### 🐛 Bug Fixes

* fix: prevent example projects from being packed during release (#115) @j-d-ha

## [v0.1.2](https://github.com/j-d-ha/minimal-lambda/compare/v0.1.1...v0.1.2) - 2025-11-10

### 🔧 Maintenance

* ci(github): get NuGet package version from release tag (#113) @j-d-ha

## [v0.1.1](https://github.com/j-d-ha/minimal-lambda/compare/v0.1.1) - 2025-11-09

### 🔧 Maintenance

* chore(ci): replace release notes workflow with changelog updater (#112) @j-d-ha
