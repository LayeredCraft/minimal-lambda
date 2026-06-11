using Microsoft.Extensions.Hosting;
using MinimalLambda.Builder;

// Create the Lambda host builder. Use builder.Services to register dependencies,
// configuration, logging, and other services used by your handler.
var builder = LambdaApplication.CreateBuilder();

var lambda = builder.Build();

// Map the Lambda handler. [FromEvent] marks the value deserialized from the
// incoming Lambda event. Change the event type, return type, and handler body
// to match your Lambda trigger and business logic.
lambda.MapHandler(([FromEvent] string input) => input.ToUpperInvariant());

// Start the Lambda runtime loop. In AWS Lambda this listens to the Runtime API;
// tests can invoke the same entry point through MinimalLambda.Testing.
await lambda.RunAsync();

// Required so the generated test project can reference this top-level Program.
public partial class Program;
