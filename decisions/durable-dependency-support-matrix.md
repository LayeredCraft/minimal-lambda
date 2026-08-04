# Durable Execution dependency and support matrix

**Verified:** 2026-08-01

## Selected dependencies

| Package                                  | Selected version | Supported asset | Purpose                                       |
| ---------------------------------------- | ---------------- | --------------- | --------------------------------------------- |
| `Amazon.Lambda.DurableExecution`         | `1.0.0`          | `net10.0`       | Runtime and DE001-DE004 analyzers             |
| `Amazon.Lambda.DurableExecution.Testing` | `1.0.0`          | `net10.0`       | In-memory workflow testing                    |
| `Amazon.Lambda.Tools`                    | `7.0.0` minimum  | `net10.0`       | Durable deployment and invocation CLI support |

Consumers must reference `MinimalLambda`, `MinimalLambda.DurableExecution`, and
`Amazon.Lambda.DurableExecution` directly. Direct AWS reference activates AWS analyzers; direct
MinimalLambda reference activates MinimalLambda source generation.

## Framework and runtime support

| Area                                  | `net10.0` / `dotnet10`           | Other frameworks/runtimes |
| ------------------------------------- | -------------------------------- | ------------------------- |
| MinimalLambda durable package         | Supported                        | Unsupported               |
| Canonical example                     | Supported                        | Unsupported               |
| Candidate durable template            | Deferred from published package  | Unsupported               |
| Local restore/build and test coverage | Required                         | Out of scope              |
| NativeAOT publish                     | Experimental local evidence only | Unsupported               |
| Managed Durable Execution deployment  | Not cloud-verified               | Unsupported               |

`MinimalLambda.DurableExecution` targets only `net10.0`. NuGet asset fallback is not a support
claim. Candidate `mlambda-durable` source uses managed `dotnet10`; it is deferred from the published template package and existing ordinary templates remain unchanged.

## Evidence boundary

Verified locally:

- Package metadata, source generation, serializer roots, and unit/integration tests.
- Standard-template packing, installation, generation, restore, and build.
- Candidate durable template source is excluded from published template-package contents.
- Durable package and testing package dependency graph.

Requires AWS cloud verification:

- Durable function creation and qualified invocation.
- Checkpoint/replay, wait/suspension, callback, failure, retention, and IAM paths.
- Managed hosting and NativeAOT under replay.

Do not describe durable execution as cloud-verified or production-ready NativeAOT until those tests
run.

## Sources

- [Amazon.Lambda.DurableExecution 1.0.0](https://www.nuget.org/packages/Amazon.Lambda.DurableExecution/1.0.0)
- [Amazon.Lambda.DurableExecution.Testing 1.0.0](https://www.nuget.org/packages/Amazon.Lambda.DurableExecution.Testing/1.0.0)
- [AWS durable supported runtimes](https://docs.aws.amazon.com/lambda/latest/dg/durable-supported-runtimes.html)
- [AWS durable infrastructure configuration](https://docs.aws.amazon.com/lambda/latest/dg/durable-getting-started-iac.html)
