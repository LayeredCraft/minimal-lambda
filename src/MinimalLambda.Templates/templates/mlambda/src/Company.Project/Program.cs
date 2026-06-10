using Microsoft.Extensions.Hosting;
using MinimalLambda.Builder;

var builder = LambdaApplication.CreateBuilder();

var lambda = builder.Build();

lambda.MapHandler(([FromEvent] string input) => input.ToUpperInvariant());

await lambda.RunAsync();

public partial class Program;
