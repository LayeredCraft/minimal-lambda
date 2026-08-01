# Deploy Durable Execution

Package and deployment assets for the [canonical durable example](https://github.com/LayeredCraft/minimal-lambda/tree/main/examples/MinimalLambda.Example.DurableExecution) are local, syntax-oriented recipes. They use executable managed `dotnet8` hosting: Lambda handler is assembly name `MinimalLambda.Example.DurableExecution`, not `Assembly::Type::Method`.

!!! warning "Static evidence only"

    User declined AWS resource deployment. Cloud create, successful invoke, `PENDING`/terminal-state polling, checkpoint replay, callback, and denied-role failure were **not run**. Commands below that create, update, upload, or invoke AWS resources are syntax only and were not executed. Local build, package, YAML parse, and template validation do not make this example cloud-ready or cloud-verified.

## Timeouts and creation constraint

`Timeout: 30` (or `--function-timeout 30`) limits one physical Lambda invocation. `DurableConfig.ExecutionTimeout: 86400` (or `--durable-execution-timeout 86400`) limits complete logical execution across suspensions and replays. `RetentionPeriodInDays: 7` retains completed execution history for seven days.

Durable configuration is create-only in one important sense: include `DurableConfig` when creating function. Updating ordinary function with durable settings cannot convert it into durable function. Settings can be updated only after function was created as durable.

## Package executable ZIP

**Local-only recipe — run locally; no AWS resources:**

```bash
# Amazon.Lambda.Tools 7.0.0 or later is required for durable flags.
dotnet tool install Amazon.Lambda.Tools --version 7.0.0 --tool-path .cache/lambda-tools-7

mkdir -p examples/MinimalLambda.Example.DurableExecution/artifacts
.cache/lambda-tools-7/dotnet-lambda package \
  examples/MinimalLambda.Example.DurableExecution/artifacts/MinimalLambda.Example.DurableExecution.zip \
  --project-location examples/MinimalLambda.Example.DurableExecution \
  --configuration Release \
  --framework net8.0
```

`aws-lambda-tools-defaults.json` carries same runtime, executable assembly handler, version publishing, invocation timeout, execution timeout, and retention defaults.

## Deploy directly with Amazon.Lambda.Tools

**Syntax only — creates or updates AWS resources; not run:**

```bash
.cache/lambda-tools-7/dotnet-lambda deploy-function minimal-lambda-durable-example \
  --project-location examples/MinimalLambda.Example.DurableExecution \
  --function-runtime dotnet8 \
  --function-handler MinimalLambda.Example.DurableExecution \
  --function-publish true \
  --function-timeout 30 \
  --durable-execution-timeout 86400 \
  --durable-retention-period 7 \
  --function-role arn:aws:iam::123456789012:role/minimal-lambda-durable-role
```

Supplied role must already contain durable permissions. If tool creates role instead, Amazon.Lambda.Tools 7 can attach AWS-managed `AWSLambdaBasicDurableExecutionRolePolicy`. After deployment, use published version printed by tool to create or update an alias:

```bash
# Syntax only — mutates alias; not run.
aws lambda create-alias \
  --function-name minimal-lambda-durable-example \
  --name prod \
  --function-version PUBLISHED_VERSION
```

Use `update-alias` instead when alias already exists.

## Validate and deploy with AWS SAM / CloudFormation

[`template.yaml`](https://github.com/LayeredCraft/minimal-lambda/blob/main/examples/MinimalLambda.Example.DurableExecution/template.yaml) points `CodeUri` at packaged ZIP, uses managed `dotnet8`, sets executable assembly handler and `DurableConfig`, attaches managed durable execution policy, publishes `prod` alias with `AutoPublishAlias`, and outputs qualified alias ARN.

**Read-only syntax validation — calls CloudFormation but creates no resources:**

```bash
aws cloudformation validate-template \
  --template-body file://examples/MinimalLambda.Example.DurableExecution/template.yaml
```

This repository recipe passed `validate-template`. Local SAM CLI schema versions can lag new
`DurableConfig` support; use a current SAM CLI before relying on `sam validate --lint`.

**Syntax only — uploads package and creates or updates AWS resources; not run:**

```bash
sam package \
  --template-file examples/MinimalLambda.Example.DurableExecution/template.yaml \
  --s3-bucket YOUR_ARTIFACT_BUCKET \
  --output-template-file packaged.yaml

sam deploy \
  --template-file packaged.yaml \
  --stack-name minimal-lambda-durable-example \
  --capabilities CAPABILITY_IAM
```

## IAM boundaries

Function execution role needs `lambda:CheckpointDurableExecution` and `lambda:GetDurableExecutionState`. Template attaches `AWSLambdaBasicDurableExecutionRolePolicy`, which includes basic logging plus these checkpoint/state permissions. For custom roles, attach managed policy or create equivalent scoped policy following current [AWS infrastructure guidance](https://docs.aws.amazon.com/lambda/latest/dg/durable-getting-started-iac.html). Do not assume ordinary `AWSLambdaBasicExecutionRole` is sufficient.

Resource scoping differs by operation. Check current Lambda service-authorization table before replacing managed policy; execution ARN is allocated at runtime. Chained `IDurableContext.InvokeAsync` calls support same-account targets and require caller role `lambda:InvokeFunction` on downstream qualified function version/alias. Target must grant invocation when resource-based permission is required. If low-level escape hatch supplies custom `IAmazonLambda`, its credentials need checkpoint/state permissions and any downstream invoke permissions too.

Callback sender principal needs relevant `lambda:SendDurableExecutionCallbackSuccess`, `lambda:SendDurableExecutionCallbackFailure`, and/or `lambda:SendDurableExecutionCallbackHeartbeat` permissions. Workflow role also needs `lambda:InvokeFunction` when workflow submits callback work to another Lambda.

Principal running Amazon.Lambda.Tools invocation needs `lambda:GetFunctionConfiguration`, `lambda:ListVersionsByFunction`, `lambda:InvokeFunction`, `lambda:GetDurableExecution`, and `lambda:GetDurableExecutionHistory` on appropriate qualified resources. These caller permissions are separate from function execution role.

**Syntax only — expected authorization failure scenario; not run:** deploy otherwise identical create-only function with role that has logging but omits checkpoint/state permissions, then invoke qualified alias. Expected outcome is durable execution failure caused by denied checkpoint/state API, not success or valid replay evidence. Do not use denied role for production traffic.

## Invoke qualified code

Use published version or alias for stable production routing. `AutoPublishAlias: prod` makes SAM deployment alias target and stack output exposes its ARN. Direct tool deployment prints published version when `--function-publish true`; create alias before invoking it.

Amazon.Lambda.Tools 7 resolves non-ARN names through version listing, so pass full qualified ARN to preserve chosen alias/version.

**Syntax only — invokes AWS and polls durable execution; not run:**

```bash
.cache/lambda-tools-7/dotnet-lambda invoke-function \
  arn:aws:lambda:REGION:ACCOUNT_ID:function:minimal-lambda-durable-example:prod \
  --invoke-mode DurableExecution \
  --payload '{"OrderId":"order-123"}'

# Explicit published version is also qualified.
.cache/lambda-tools-7/dotnet-lambda invoke-function \
  arn:aws:lambda:REGION:ACCOUNT_ID:function:minimal-lambda-durable-example:1 \
  --invoke-mode DurableExecution \
  --payload '{"OrderId":"order-123"}'
```

## Version and rollback safety

Execution stays pinned to version that started it. Moving `prod` alias sends new executions to new version; it does not migrate active executions. Keep versions referenced by active executions until they finish and retention/operational requirements allow deletion. Roll back by repointing alias to known-good published version; rollback affects new executions only. Ensure old version remains compatible with its in-flight checkpoints and external callbacks.

## Evidence boundary

Static checks can prove project compiles, Amazon.Lambda.Tools creates ZIP, YAML parses, and CloudFormation accepts template syntax. They cannot prove managed service reaches `PENDING`, succeeds, replays checkpoint, enforces denied role, completes callback, or safely updates live alias. Those cloud results remain unverified because no AWS resources were created.

Related: [Durable Execution](../features/durable-execution.md), [Testing](testing.md), and [AWS infrastructure configuration](https://docs.aws.amazon.com/lambda/latest/dg/durable-getting-started-iac.html).
