# MinimalLambda.Example.OpenTelemetry

> 📚 **[View Full Documentation](https://layeredcraft.github.io/minimal-lambda/)**

## Getting Started

You may need to CD into this directory before running the following commands.

```bash
cd ./examples/MinimalLambda.Example.OpenTelemetry
```

### Dependencies

These are the requirements to run this example:

- [Docker](https://docs.docker.com/get-docker/)
- [Docker Compose](https://docs.docker.com/compose/install/)
- [AWS Lambda Test Tool](https://github.com/aws/aws-lambda-dotnet/tree/master/Tools/LambdaTestTool-v2)

### Jaeger

**Start Jaeger**

```bash
docker compose up
```

This should start Jaeger on [`http://localhost:16686`](http://localhost:16686).

### Lambda Local Emulator

The Lambda Local Emulator is used to run the Lambda function locally.
It can be started with the following command:

```bash
dotnet lambda-test-tool start --lambda-emulator-port 5050 --config-storage-path "./events"
```

### Running the Lambda function

The Lambda function can be run with the following command:

```bash
dotnet run
```

### Invoking an event

Once the Lambda Local Emulator and the function are running, you can invoke an event by opening the
UI and pasting the following JSON into the "Function Input" box:

```json
{
  "Name": "John"
}
```

Instead, a sample event has been saved and should be available in the "Example Requests"
dropdown menu.

You should then be able to see the trace in Jaeger. If you don't, give it a few seconds to propagate
or stop your lambda function with `ctrl+c`, this will trigger shutdown with includes a force flush
of traces and metrics.