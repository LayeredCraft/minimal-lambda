# MinimalLambda.DurableExecution

Experimental, locally buildable AWS Lambda Durable Execution integration for MinimalLambda.

> 📚 **[View Full Documentation](https://layeredcraft.github.io/minimal-lambda/)**

## Requirements

- .NET 8 or .NET 10
- A compatible `MinimalLambda` version
- `Amazon.Lambda.DurableExecution` 1.0.0

## Installation

Install all three packages explicitly:

```bash
dotnet add package MinimalLambda
dotnet add package MinimalLambda.DurableExecution
dotnet add package Amazon.Lambda.DurableExecution --version 1.0.0
```

Keep the direct `MinimalLambda` reference so its single source generator runs for ordinary and
durable handlers; no separate durable generator package is required. The AWS Durable Execution
runtime already arrives transitively through `MinimalLambda.DurableExecution`, but transitive
package dependencies exclude analyzer assets. The direct `Amazon.Lambda.DurableExecution` reference
ensures its DE001-DE004 analyzers also run. The wrapper package does not duplicate the AWS runtime or
analyzer assemblies.

Local restore, build, and package validation do not prove cloud deployment or production-ready
NativeAOT support. Consult current project documentation and AWS runtime guidance before deployment.
