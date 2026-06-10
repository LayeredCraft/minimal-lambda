using System.Text.Json.Serialization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MinimalLambda.Builder;

var builder = LambdaApplication.CreateBuilder();

builder.Services.AddLambdaSerializerWithContext<LambdaFunctionJsonSerializerContext>();

var lambda = builder.Build();

lambda.MapHandler(([FromEvent] GreetingRequest request) =>
new GreetingResponse($"Hello {request.Name}!"));

await lambda.RunAsync();

public sealed record GreetingRequest(string Name);

public sealed record GreetingResponse(string Message);

[JsonSerializable(typeof(GreetingRequest))]
[JsonSerializable(typeof(GreetingResponse))]
public partial class LambdaFunctionJsonSerializerContext : JsonSerializerContext;

public partial class Program;
