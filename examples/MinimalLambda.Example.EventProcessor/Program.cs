using Microsoft.Extensions.Hosting;
using MinimalLambda.Builder;

var builder = LambdaApplication.CreateBuilder();

var lambda = builder.Build();

// [FromEvent] binds the incoming Lambda event to the handler parameter.
lambda.MapHandler(([FromEvent] string input) => input.ToUpperInvariant());

// Start the Lambda runtime loop.
await lambda.RunAsync();

// Required so the generated test project can reference this top-level Program.
public partial class Program;
