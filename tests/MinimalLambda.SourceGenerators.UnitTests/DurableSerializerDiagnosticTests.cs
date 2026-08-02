#if NET10_0_OR_GREATER
using AwesomeAssertions;

namespace MinimalLambda.SourceGenerators.UnitTests;

public class DurableSerializerDiagnosticTests
{
    [Fact]
    public void ReportsEveryMissingExplicitDurableRootAtRegistration()
    {
        var diagnostics = Generate(ContextDeclaration(""));

        diagnostics
            .Select(diagnostic => diagnostic.Id)
            .Should()
            .Equal("LH0011", "LH0011", "LH0011", "LH0011");
        diagnostics
            .Select(diagnostic => diagnostic.GetMessage())
            .Should()
            .Contain(message =>
                message.Contains("DurableExecutionInvocationInput", StringComparison.Ordinal));
        diagnostics
            .Select(diagnostic => diagnostic.GetMessage())
            .Should()
            .Contain(message =>
                message.Contains("DurableExecutionInvocationOutput", StringComparison.Ordinal));
        diagnostics
            .Select(diagnostic => diagnostic.GetMessage())
            .Should()
            .Contain(message => message.Contains("global::Input", StringComparison.Ordinal));
        diagnostics
            .Select(diagnostic => diagnostic.GetMessage())
            .Should()
            .Contain(message => message.Contains("global::Output", StringComparison.Ordinal));
        diagnostics
            .Select(diagnostic => diagnostic.Location.SourceSpan)
            .Distinct()
            .Should()
            .ContainSingle();
    }

    [Fact]
    public void MissingExplicitRootFailsWhenWarningsAreErrors()
    {
        var source = ContextDeclaration(
            """
            [JsonSerializable(typeof(DurableExecutionInvocationInput))]
            [JsonSerializable(typeof(DurableExecutionInvocationOutput))]
            [JsonSerializable(typeof(Input))]
            """);

        var (driver, _) = GeneratorTestHelpers.GenerateFromSource(
            source,
            includeDurableReferences: true,
            treatWarningsAsErrors: true);

        var diagnostic =
            driver.GetRunResult().Diagnostics.Single(diagnostic => diagnostic.Id == "LH0011");
        diagnostic.Severity.Should().Be(Microsoft.CodeAnalysis.DiagnosticSeverity.Error);
        diagnostic.IsWarningAsError.Should().BeTrue();
        diagnostic.GetMessage().Should().Contain("global::Output");
    }

    [Fact]
    public void CompleteExplicitContextIsSilent()
    {
        var diagnostics = Generate(
            ContextDeclaration(
                """
                [JsonSerializable(typeof(DurableExecutionInvocationInput))]
                [JsonSerializable(typeof(DurableExecutionInvocationOutput))]
                [JsonSerializable(typeof(Input))]
                [JsonSerializable(typeof(Output))]
                """));

        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public void TaskHandlerContributesExactlyThreeRoots()
    {
        var source = ContextDeclaration("")
            .Replace("static Task<Output> Handle", "static Task Handle", StringComparison.Ordinal)
            .Replace(
                "=>\n    Task.FromResult(new Output());",
                "=> Task.CompletedTask;",
                StringComparison.Ordinal);

        var diagnostics = Generate(source);

        diagnostics.Should().HaveCount(3);
        diagnostics
            .Select(diagnostic => diagnostic.GetMessage())
            .Should()
            .Contain(message =>
                message.Contains("DurableExecutionInvocationInput", StringComparison.Ordinal));
        diagnostics
            .Select(diagnostic => diagnostic.GetMessage())
            .Should()
            .Contain(message =>
                message.Contains("DurableExecutionInvocationOutput", StringComparison.Ordinal));
        diagnostics
            .Select(diagnostic => diagnostic.GetMessage())
            .Should()
            .Contain(message => message.Contains("global::Input", StringComparison.Ordinal));
        diagnostics
            .Should()
            .NotContain(diagnostic =>
                diagnostic.GetMessage().Contains("global::Output", StringComparison.Ordinal));
    }

    [Fact]
    public void IndependentBuildersKeepContextsAndRootsIsolated()
    {
        var diagnostics = Generate(
            """
            using System.Text.Json.Serialization;
            using System.Threading.Tasks;
            using Amazon.Lambda.DurableExecution;
            using Microsoft.Extensions.DependencyInjection;
            using MinimalLambda;
            using MinimalLambda.Builder;

            var firstBuilder = LambdaApplication.CreateBuilder();
            firstBuilder.Services.AddLambdaSerializerWithContext<FirstContext>();
            var first = firstBuilder.Build();
            first.MapDurableHandler(First);

            var secondBuilder = LambdaApplication.CreateBuilder();
            secondBuilder.Services.AddLambdaSerializerWithContext<SecondContext>();
            var second = secondBuilder.Build();
            second.MapDurableHandler(Second);

            static Task First([FromEvent] FirstInput input, IDurableContext context) => Task.CompletedTask;
            static Task<SecondOutput> Second([FromEvent] SecondInput input, IDurableContext context) => null!;
            partial class FirstContext : JsonSerializerContext { }
            partial class SecondContext : JsonSerializerContext { }
            sealed class FirstInput { }
            sealed class SecondInput { }
            sealed class SecondOutput { }
            """);

        diagnostics.Should().HaveCount(7);
        var firstMessages = diagnostics
            .Select(diagnostic => diagnostic.GetMessage())
            .Where(message => message.Contains("FirstContext", StringComparison.Ordinal))
            .ToArray();
        var secondMessages = diagnostics
            .Select(diagnostic => diagnostic.GetMessage())
            .Where(message => message.Contains("SecondContext", StringComparison.Ordinal))
            .ToArray();

        firstMessages.Should().HaveCount(3);
        firstMessages
            .Should()
            .Contain(message =>
                message.Contains("DurableExecutionInvocationInput", StringComparison.Ordinal));
        firstMessages
            .Should()
            .Contain(message =>
                message.Contains("DurableExecutionInvocationOutput", StringComparison.Ordinal));
        firstMessages
            .Should()
            .Contain(message => message.Contains("global::FirstInput", StringComparison.Ordinal));
        secondMessages.Should().HaveCount(4);
        secondMessages
            .Should()
            .Contain(message =>
                message.Contains("DurableExecutionInvocationInput", StringComparison.Ordinal));
        secondMessages
            .Should()
            .Contain(message =>
                message.Contains("DurableExecutionInvocationOutput", StringComparison.Ordinal));
        secondMessages
            .Should()
            .Contain(message => message.Contains("global::SecondInput", StringComparison.Ordinal));
        secondMessages
            .Should()
            .Contain(message => message.Contains("global::SecondOutput", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("AppJsonContext")]
    [InlineData("OtherJsonContext")]
    public void SequentialEqualOrDifferentContextsSuppressWarnings(string secondContext)
    {
        var source = ContextDeclaration("")
                .Replace(
                    "builder.Services.AddLambdaSerializerWithContext<AppJsonContext>();",
                    $$"""
                      builder.Services.AddLambdaSerializerWithContext<AppJsonContext>();
                      builder.Services.AddLambdaSerializerWithContext<{{secondContext}}>();
                      """,
                    StringComparison.Ordinal)
            + "\ninternal partial class OtherJsonContext : JsonSerializerContext { }";

        Generate(source).Should().BeEmpty();
    }

    [Theory]
    [InlineData(
        "builder.Services.AddSingleton<Amazon.Lambda.Core.ILambdaSerializer>(null!);\nbuilder.Services.AddLambdaSerializerWithContext<AppJsonContext>();")]
    [InlineData(
        "builder.Services.AddLambdaSerializerWithContext<AppJsonContext>();\nbuilder.Services.AddSingleton<Amazon.Lambda.Core.ILambdaSerializer>(null!);")]
    public void SerializerOverrideBeforeOrAfterContextSuppressesWarnings(string registrations)
    {
        var source = ContextDeclaration("")
            .Replace(
                "builder.Services.AddLambdaSerializerWithContext<AppJsonContext>();",
                registrations,
                StringComparison.Ordinal);

        Generate(source).Should().BeEmpty();
    }

    [Fact]
    public void OpaqueServiceMutationSuppressesWarnings()
    {
        var source = ContextDeclaration("")
            .Replace(
                "builder.Services.AddLambdaSerializerWithContext<AppJsonContext>();",
                """
                builder.Services.AddSingleton(new object());
                builder.Services.AddLambdaSerializerWithContext<AppJsonContext>();
                """,
                StringComparison.Ordinal);

        Generate(source).Should().BeEmpty();
    }

    [Fact]
    public void ConditionalRegistrationSuppressesWarnings()
    {
        var source = ContextDeclaration("")
            .Replace(
                "builder.Services.AddLambdaSerializerWithContext<AppJsonContext>();",
                """
                if (System.DateTime.UtcNow.Ticks > 0)
                    builder.Services.AddLambdaSerializerWithContext<AppJsonContext>();
                builder.Services.AddLambdaSerializerWithContext<AppJsonContext>();
                """,
                StringComparison.Ordinal);

        Generate(source).Should().BeEmpty();
    }

    [Fact]
    public void ApplicationAliasSuppressesWarnings()
    {
        var source = ContextDeclaration("")
            .Replace(
                "app.MapDurableHandler(Handle);",
                """
                var alias = app;
                app.MapDurableHandler(Handle);
                """,
                StringComparison.Ordinal);

        Generate(source).Should().BeEmpty();
    }

    [Theory]
    [InlineData("builder.Services.AddSingleton(builder);")]
    [InlineData("builder.Services.AddSingleton<IServiceCollection>(builder.Services);")]
    public void SafeLookingRegistrationThatEscapesBuilderSuppressesWarnings(string registration)
    {
        var source = ContextDeclaration("")
            .Replace(
                "builder.Services.AddLambdaSerializerWithContext<AppJsonContext>();",
                $"{registration}\nbuilder.Services.AddLambdaSerializerWithContext<AppJsonContext>();",
                StringComparison.Ordinal);

        Generate(source).Should().BeEmpty();
    }

    [Fact]
    public void OpenGenericServiceRegistrationSuppressesWarnings()
    {
        var source = ContextDeclaration("")
            .Replace(
                "var builder = LambdaApplication.CreateBuilder();",
                """
                Configure<Service>();
                return;
                static void Configure<T>() where T : class
                {
                var builder = LambdaApplication.CreateBuilder();
                """,
                StringComparison.Ordinal)
            .Replace(
                "builder.Services.AddLambdaSerializerWithContext<AppJsonContext>();",
                """
                builder.Services.AddSingleton<T>();
                builder.Services.AddLambdaSerializerWithContext<AppJsonContext>();
                """,
                StringComparison.Ordinal)
            .Replace(
                "app.MapDurableHandler(Handle);",
                """
                app.MapDurableHandler(Handle);
                }
                """,
                StringComparison.Ordinal);

        Generate(source).Should().BeEmpty();
    }

    [Theory]
    [InlineData("if (System.DateTime.UtcNow.Ticks > 0) {", "}")]
    [InlineData("while (System.DateTime.UtcNow.Ticks > 0) {", "break; }")]
    [InlineData("try {", "} catch { }")]
    public void WholePipelineInsideControlFlowSuppressesWarnings(string prefix, string suffix)
    {
        var source = ContextDeclaration("")
            .Replace(
                "var builder = LambdaApplication.CreateBuilder();",
                $"{prefix}\nvar builder = LambdaApplication.CreateBuilder();",
                StringComparison.Ordinal)
            .Replace(
                "app.MapDurableHandler(Handle);",
                $"app.MapDurableHandler(Handle);\n{suffix}",
                StringComparison.Ordinal);

        Generate(source).Should().BeEmpty();
    }

    [Fact]
    public void ConditionalMapSuppressesWholeApplicationTrace()
    {
        var source = ContextDeclaration("")
            .Replace(
                "app.MapDurableHandler(Handle);",
                """
                if (System.DateTime.UtcNow.Ticks > 0)
                    app.MapDurableHandler(Handle);
                """,
                StringComparison.Ordinal);

        Generate(source).Should().BeEmpty();
    }

    [Fact]
    public void BaseContextDeclarationsSatisfyRoots()
    {
        var source = ContextDeclaration(
                """
                [JsonSerializable(typeof(Input))]
                [JsonSerializable(typeof(Output))]
                """)
            .Replace(
                "internal partial class AppJsonContext : JsonSerializerContext { }",
                """
                [JsonSerializable(typeof(DurableExecutionInvocationInput))]
                [JsonSerializable(typeof(DurableExecutionInvocationOutput))]
                internal partial class BaseJsonContext : JsonSerializerContext { }
                internal partial class AppJsonContext : BaseJsonContext { }
                """,
                StringComparison.Ordinal);

        Generate(source).Should().BeEmpty();
    }

    [Fact]
    public void InvalidHandlerContributesNoSignatureRoots()
    {
        var source = ContextDeclaration(
                """
                [JsonSerializable(typeof(DurableExecutionInvocationInput))]
                [JsonSerializable(typeof(DurableExecutionInvocationOutput))]
                """)
            .Replace(
                "static Task<Output> Handle([FromEvent] Input input, IDurableContext context) =>",
                "static Task<Output> Handle(Input input, IDurableContext context) =>",
                StringComparison.Ordinal);

        Generate(source).Select(diagnostic => diagnostic.Id).Should().Equal("LH0007");
    }

    [Fact]
    public void DuplicateNormalizedRootsWarnOnce()
    {
        var source = ContextDeclaration("")
            .Replace(
                "app.MapDurableHandler(Handle);",
                """
                app.MapDurableHandler(Handle);
                app.MapDurableHandler(HandleAgain);
                """,
                StringComparison.Ordinal)
            .Replace(
                "static Task<Output> Handle([FromEvent] Input input, IDurableContext context) =>",
                """
                static Task<Output> HandleAgain([FromEvent] Input? input, IDurableContext context) =>
                    Task.FromResult(new Output());
                static Task<Output> Handle([FromEvent] Input input, IDurableContext context) =>
                """,
                StringComparison.Ordinal);

        Generate(source).Where(diagnostic => diagnostic.Id == "LH0011").Should().HaveCount(4);
    }

    [Fact]
    public void PropertyMediatedApplicationReceiverSuppressesWarnings()
    {
        var source = ContextDeclaration("")
                .Replace(
                    "app.MapDurableHandler(Handle);",
                    "app.Proxy.MapDurableHandler(Handle);",
                    StringComparison.Ordinal)
            + """

              internal static class ProxyExtensions
              {
                  extension(ILambdaInvocationBuilder application)
                  {
                      internal ILambdaInvocationBuilder Proxy => application;
                  }
              }
              """;

        Generate(source).Should().BeEmpty();
    }

    [Fact]
    public void ProvenNonSerializerRegistrationPreservesWarnings()
    {
        var source = ContextDeclaration("")
            .Replace(
                "builder.Services.AddLambdaSerializerWithContext<AppJsonContext>();",
                """
                builder.Services.AddSingleton<Service>();
                builder.Services.AddLambdaSerializerWithContext<AppJsonContext>();
                """,
                StringComparison.Ordinal);

        Generate(source).Should().HaveCount(4);
    }

    private static string ContextDeclaration(string attributes) =>
        $$"""
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

          static Task<Output> Handle([FromEvent] Input input, IDurableContext context) =>
              Task.FromResult(new Output());

          {{attributes}}
          internal partial class AppJsonContext : JsonSerializerContext { }
          internal sealed class Input { }
          internal sealed class Output { }
          internal sealed class Service { }
          """;

    private static Microsoft.CodeAnalysis.Diagnostic[] Generate(string source)
    {
        var (driver, _) = GeneratorTestHelpers.GenerateFromSource(
            source,
            includeDurableReferences: true);
        return driver.GetRunResult().Diagnostics.ToArray();
    }
}
#endif
