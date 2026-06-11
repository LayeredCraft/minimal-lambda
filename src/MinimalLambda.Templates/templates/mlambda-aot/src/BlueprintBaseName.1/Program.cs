using System.Text.Json.Serialization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MinimalLambda.Builder;

var builder = LambdaApplication.CreateBuilder();

builder.Services.AddLambdaSerializerWithContext<BlueprintBaseName__1JsonSerializerContext>();

var lambda = builder.Build();

lambda.MapHandler(([FromEvent] string input) => input.ToUpperInvariant());

await lambda.RunAsync();

[JsonSerializable(typeof(string))]
public partial class BlueprintBaseName__1JsonSerializerContext : JsonSerializerContext;

public partial class Program;
