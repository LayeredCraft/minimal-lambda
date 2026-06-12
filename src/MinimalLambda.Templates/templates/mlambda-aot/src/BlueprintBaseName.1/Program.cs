using System.Text.Json.Serialization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MinimalLambda.Builder;

var builder = LambdaApplication.CreateBuilder();

// Register all Lambda event, response, and DTO types with source-generated JSON.
builder.Services.AddLambdaSerializerWithContext<BlueprintBaseName__1JsonSerializerContext>();

var lambda = builder.Build();

// [FromEvent] binds the incoming Lambda event to the handler parameter.
lambda.MapHandler(([FromEvent] string input) => input.ToUpperInvariant());

// Start the Lambda runtime loop.
await lambda.RunAsync();

// Add every type that crosses the Lambda JSON boundary.
[JsonSerializable(typeof(string))]
public partial class BlueprintBaseName__1JsonSerializerContext : JsonSerializerContext;

// Required so the generated test project can reference this top-level Program.
public partial class Program;
