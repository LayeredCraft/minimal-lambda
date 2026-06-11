using System.Text.Json.Serialization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MinimalLambda.Builder;

// Create the Lambda host builder. Keep startup code AOT/trimming-friendly:
// prefer source-generated serializers and avoid reflection-heavy patterns.
var builder = LambdaApplication.CreateBuilder();

// Native AOT disables reflection-based JSON serialization in this template.
// Register a source-generated System.Text.Json context and add every event,
// response, and DTO type used by your handlers with [JsonSerializable].
builder.Services.AddLambdaSerializerWithContext<BlueprintBaseName__1JsonSerializerContext>();

var lambda = builder.Build();

// Map the Lambda handler. [FromEvent] marks the value deserialized from the
// incoming Lambda event. Change the event type, return type, and handler body
// to match your Lambda trigger and business logic.
lambda.MapHandler(([FromEvent] string input) => input.ToUpperInvariant());

// Start the Lambda runtime loop. In AWS Lambda this listens to the Runtime API;
// tests can invoke the same entry point through MinimalLambda.Testing.
await lambda.RunAsync();

// Add JsonSerializable attributes for all types that cross the Lambda JSON
// boundary. This keeps Native AOT deployments trim-safe and reflection-free.
[JsonSerializable(typeof(string))]
public partial class BlueprintBaseName__1JsonSerializerContext : JsonSerializerContext;

// Required so the generated test project can reference this top-level Program.
public partial class Program;
