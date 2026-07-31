# Durable Execution dependency and support matrix

**Verified:** 2026-07-31

This record fixes package versions and evidence boundaries for initial
`MinimalLambda.DurableExecution` implementation. Recheck links and versions before release because
AWS Durable Execution support is new and documentation is still changing.

## Selected dependencies

| Package                                  | Selected version                         | Released assets                             | Important direct dependencies                                                                                                            |
| ---------------------------------------- | ---------------------------------------- | ------------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------- |
| `Amazon.Lambda.DurableExecution`         | `1.0.0`                                  | `net8.0`, `net10.0`                         | `Amazon.Lambda.Core >= 3.2.0`, `AWSSDK.Lambda >= 4.0.13.1`, `Microsoft.Extensions.Logging.Abstractions >= 8.0.0`                         |
| `Amazon.Lambda.DurableExecution.Testing` | `1.0.0`                                  | `net8.0`, `net10.0`                         | `Amazon.Lambda.DurableExecution >= 1.0.0`, `Amazon.Lambda.Serialization.SystemTextJson >= 3.0.0`, `Amazon.Lambda.TestUtilities >= 4.2.0` |
| `Amazon.Lambda.Tools`                    | `7.0.0` minimum for durable CLI features | .NET tool assets for `net8.0` and `net10.0` | Used for deployment and durable invocation; not a runtime dependency                                                                     |

Central versions also select `AWSSDK.Lambda` `4.0.13.1` and retain `AWSSDK.Core` `4.0.3.20` for
projects that reference them directly. `AWSSDK.Lambda` permits `AWSSDK.Core` in
`[4.0.3.12, 5.0.0)`, so those selected versions are compatible. Representative clean `net8.0` and
`net10.0` projects restored both durable packages without downgrade warnings; because central
transitive pinning is disabled, their unpinned transitive graph resolved `AWSSDK.Core` `4.0.3.12`.
`Microsoft.Extensions.Logging.Abstractions` versions already selected per target framework satisfy
the durable package minimum.

`Amazon.Lambda.DurableExecution` includes
`analyzers/dotnet/cs/Amazon.Lambda.DurableExecution.Analyzers.dll` with diagnostics DE001-DE004.
Consumers referencing the runtime package directly receive these analyzers. The Testing package
excludes runtime build and analyzer assets from its dependency, so a Testing-only reference does not
activate them. MinimalLambda must not repack or reference a second copy of the AWS analyzer.

## Framework and runtime support

| Area                                        | `net8.0` / `dotnet8`                                                                    | `net10.0` / `dotnet10`                         | `net9.0` / `net11.0`                                                         |
| ------------------------------------------- | --------------------------------------------------------------------------------------- | ---------------------------------------------- | ---------------------------------------------------------------------------- |
| Released runtime and testing package assets | Supported                                                                               | Supported                                      | No native package asset; NuGet fallback compatibility is not a support claim |
| AWS managed durable runtime                 | Listed as supported                                                                     | Listed as supported                            | Not listed                                                                   |
| AWS .NET durable blueprint                  | Not provided                                                                            | Class-library blueprints target this runtime   | Not provided                                                                 |
| MinimalLambda local restore/build           | Must be verified by package and consumer tests                                          | Must be verified by package and consumer tests | Out of initial durable package scope                                         |
| NativeAOT publish                           | SDK has a local `net8.0` AOT publish test; MinimalLambda integration remains unverified | No equivalent upstream proof found             | Unsupported                                                                  |
| Durable NativeAOT cloud deployment          | Not verified                                                                            | Not verified                                   | Unsupported                                                                  |

Initial `MinimalLambda.DurableExecution` package therefore targets only `net8.0` and `net10.0`.
Package compatibility, managed-runtime availability, hosting model, and cloud behavior are separate
evidence levels. No `net9.0`, `net11.0`, or production-ready NativeAOT promise follows from NuGet
fallback selection or a successful local publish.

AWS documentation lists both `dotnet8` and `dotnet10` as managed durable runtimes, while current AWS
.NET durable blueprints use `dotnet10`. MinimalLambda's executable RuntimeSupport host must be tested
against the service before either runtime is documented as cloud-verified for this integration.

## Deployment tooling

`Amazon.Lambda.Tools` 7.0.0 adds:

- `--durable-execution-timeout` and `--durable-retention-period` for deployment/configuration;
- role-policy assistance for tool-created roles;
- `--invoke-mode DurableExecution` with execution polling;
- published-version output used to invoke a qualified version or alias.

Durable configuration must be present when a function is created. Configuration switches may update
an already-durable function but cannot convert an existing ordinary function. A durable invocation
must target a qualified version or alias.

These capabilities describe tool support, not deployment evidence. Static packaging and template
validation can run locally; IAM, service availability, replay, waits, callbacks, retention, and
qualified invocation require cloud tests.

## Evidence levels

### Verified from released packages and local source

- GA package versions, target assets, nuspec dependency ranges, and analyzer contents.
- AWS Testing package dependency graph and in-memory runner availability.
- Upstream `net8.0` NativeAOT publish test configuration.
- Clean `net8.0` and `net10.0` package restores without downgrade warnings.
- AWS blueprint runtime choices and Amazon.Lambda.Tools 7.0.0 feature contract.

### Must be verified in this repository

- Build/pack of repository durable projects for `net8.0` and `net10.0` without downgrade warnings.
- Analyzer coexistence and transitivity with the MinimalLambda generator.
- Source-generated serializer coverage and warning-free NativeAOT publish.
- Package-only consumers using independently versioned MinimalLambda packages.

### Requires AWS cloud verification

- MinimalLambda executable hosting on each claimed managed runtime.
- Durable create, qualified invoke, checkpoint/replay, wait/suspension, callback, and failure paths.
- IAM policy behavior and deployment updates.
- NativeAOT durable execution under replay.

Until those tests run, use **locally buildable**, **syntax-validated**, or **experimental** wording.
Do not use **cloud-verified**, **production-ready NativeAOT**, or equivalent claims.

## Known risks

- AWS documentation has recently contained inconsistent language lists; prefer current supported
  runtime table and GA announcement, then verify in cloud before release.
- Upstream calls the SDK AOT-friendly but does not explicitly certify Durable Execution with
  NativeAOT end to end.
- In-memory testing proves workflow behavior against the local engine, not managed-service
  availability, IAM, deployment, or serialization behavior.
- Dependency minimum ranges permit future versions; direct central versions and package-consumer
  tests must prevent unreviewed drift.

## Sources

- [Amazon.Lambda.DurableExecution 1.0.0](https://www.nuget.org/packages/Amazon.Lambda.DurableExecution/1.0.0)
- [Amazon.Lambda.DurableExecution 1.0.0 nuspec](https://api.nuget.org/v3-flatcontainer/amazon.lambda.durableexecution/1.0.0/amazon.lambda.durableexecution.nuspec)
- [Amazon.Lambda.DurableExecution.Testing 1.0.0](https://www.nuget.org/packages/Amazon.Lambda.DurableExecution.Testing/1.0.0)
- [Amazon.Lambda.DurableExecution.Testing 1.0.0 nuspec](https://api.nuget.org/v3-flatcontainer/amazon.lambda.durableexecution.testing/1.0.0/amazon.lambda.durableexecution.testing.nuspec)
- [AWS .NET Durable Execution GA announcement](https://aws.amazon.com/about-aws/whats-new/2026/07/lambdadf-dotnet/)
- [AWS durable supported runtimes](https://docs.aws.amazon.com/lambda/latest/dg/durable-supported-runtimes.html)
- [AWS NativeAOT guidance](https://docs.aws.amazon.com/lambda/latest/dg/dotnet-native-aot.html)
- [AWS durable `net8.0` NativeAOT publish test](https://github.com/aws/aws-lambda-dotnet/blob/831492165a694825e0e762d68ec74f3e36b628c0/Libraries/test/Amazon.Lambda.DurableExecution.AotPublishTest/Amazon.Lambda.DurableExecution.AotPublishTest.csproj)
- [AWS durable `net10.0` blueprint](https://github.com/aws/aws-lambda-dotnet/blob/831492165a694825e0e762d68ec74f3e36b628c0/Blueprints/BlueprintDefinitions/vs2026/DurableFunction/template/src/BlueprintBaseName.1/BlueprintBaseName.1.csproj)
- [Amazon.Lambda.Tools 7.0.0 release notes](https://github.com/aws/aws-extensions-for-dotnet-cli/releases/tag/release_2026-06-30)
- [AWS durable infrastructure configuration](https://docs.aws.amazon.com/lambda/latest/dg/durable-getting-started-iac.html)
- [AWS durable testing](https://docs.aws.amazon.com/lambda/latest/dg/durable-testing.html)
