using System.Text.Json.Serialization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MinimalLambda.Builder;

var builder = LambdaApplication.CreateBuilder();

builder.Services.AddLambdaSerializerWithContext<LambdaFunctionJsonSerializerContext>();

var lambda = builder.Build();

lambda.MapHandler(([FromEvent] string input) => input.ToUpperInvariant());

await lambda.RunAsync();

[JsonSerializable(typeof(string))]
public partial class LambdaFunctionJsonSerializerContext : JsonSerializerContext;

public partial class Program;
