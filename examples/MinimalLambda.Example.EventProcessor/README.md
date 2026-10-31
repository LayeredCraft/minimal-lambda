# MinimalLambda.Example.EventProcessor

Sample registrations for the optional `MinimalLambda.EventProcessing` package. `MapEventHandler` consumes the single invocation handler slot,
so this project registers exactly one family per run and selects it with `EVENT_PROCESSOR_SAMPLE`.

| Value          | Family | Shows                                                                 |
| -------------- | ------ | --------------------------------------------------------------------- |
| `n1`           | N1     | per-item DI scope and processing token               |
| `n2`           | N2     | complete source-item outcomes and success counts               |
| `n3`           | N3     | `onError` mapping failures and building an SQS partial batch response |
| `r1`           | R1     | per-item `TResult` contributions through a typed delegate local       |
| `r2` (default) | R2     | complete outcomes and retries for skipped/cancelled items    |

## Program.cs

- `RegisterNoResult` - no result, no response.
- `RegisterNoResultWithResponse` - response from ordered outcomes for every source item.
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

Response builders receive `IReadOnlyList<EventItemOutcome<TItem, TValue>>`. Check status and
HasValue before using contributions. The SQS examples explicitly retry unstarted/cancelled records.
DI/handler errors reach onError; scope cleanup failure fails the invocation. Invocation cancellation
through cleanup suppresses the response. No required event-processing service registration exists.

Only R2 demonstrates stopping early; it includes unstarted messages in its retry response.
