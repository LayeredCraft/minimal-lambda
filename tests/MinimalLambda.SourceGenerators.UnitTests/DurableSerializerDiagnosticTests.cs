#if MINIMALLAMBDA_DURABLE
using AwesomeAssertions;

namespace MinimalLambda.SourceGenerators.UnitTests;

public class DurableSerializerDiagnosticTests
{
    [Fact]
    public void DoesNotInspectSerializerContextRoots()
    {
        var (driver, _) = GeneratorTestHelpers.GenerateFromSource(
            """
            using System.Text.Json.Serialization;
            using System.Threading.Tasks;
            using Amazon.Lambda.DurableExecution;
            using Microsoft.Extensions.DependencyInjection;
            using MinimalLambda;
            using MinimalLambda.Builder;

            var builder = LambdaApplication.CreateBuilder();
            builder.Services.AddLambdaSerializerWithContext<AppJsonContext>();
            var app = builder.Build();
            app.MapDurableHandler(Handle);
            static Task Handle([FromEvent] string input, IDurableContext durable) => Task.CompletedTask;

            [JsonSerializable(typeof(string))]
            abstract partial class AppJsonContext : JsonSerializerContext
            {
                protected AppJsonContext() : base(null) { }
            }
            """,
            includeDurableReferences: true);

        driver.GetRunResult().Diagnostics.Should().BeEmpty();
    }
}
#endif
