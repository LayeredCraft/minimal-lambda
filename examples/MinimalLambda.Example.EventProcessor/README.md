# MinimalLambda.Example.EventProcessor

Sample event-processor registrations. `MapEventHandler` consumes the single invocation handler slot,
so this project registers exactly one family per run and selects it with `EVENT_PROCESSOR_SAMPLE`.

| Value          | Family | Shows                                                                 |
| -------------- | ------ | --------------------------------------------------------------------- |
| `n1`           | N1     | per-item DI scope, processing token, `StopProcessing()`               |
| `n2`           | N2     | `createResponse` from successfully handled source items               |
| `n3`           | N3     | `onError` mapping failures and building an SQS partial batch response |
| `r1`           | R1     | per-item `TResult` contributions through a typed delegate local       |
| `r2` (default) | R2     | success and retry contributions, graceful stop, method-group shape    |

## Program.cs

- `RegisterNoResult` - no result, no response.
- `RegisterNoResultWithResponse` - response from completed source items.
- `RegisterNoResultWithFailures` - successes contribute nothing, failures map to SQS batch item
  failures.
- `RegisterResults` - every completed item contributes one result.
- `RegisterResultsWithRecovery` - the SQS partial batch response shape.

## Prerequisites

- .NET SDK 8.0 or later.
- AWS credentials configured locally if you plan to deploy.
- Amazon.Lambda.Tools for command-line deployment.

## Restore and build

```bash
dotnet restore
dotnet build
```

## Run a sample locally

```bash
EVENT_PROCESSOR_SAMPLE=r2 dotnet run
```

Deploying an R2/N3 SQS sample requires `ReportBatchItemFailures` on the event source mapping, and
the FIFO/SQS limits described in `docs/features/event-processor.md`.
