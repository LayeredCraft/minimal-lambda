using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using Amazon.Lambda.Core;
#if NET10_0_OR_GREATER
using Amazon.Lambda.DurableExecution;
#endif
using Amazon.Lambda.RuntimeSupport;
using Amazon.Lambda.Serialization.SystemTextJson;
using AwesomeAssertions;
using Basic.Reference.Assemblies;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using MinimalLambda.Builder;

namespace MinimalLambda.SourceGenerators.UnitTests;

internal static class GeneratorTestHelpers
{
    internal static Task Verify(
        string source,
        int expectedTrees = -1,
        bool includeDurableReferences = false,
        IReadOnlyList<(string FilePath, string Source)>? additionalSources = null,
        IReadOnlyCollection<string>? expectedDiagnosticIds = null)
    {
        var (driver, originalCompilation) = GenerateFromSource(
            source,
            includeDurableReferences: includeDurableReferences,
            additionalSources: additionalSources);

        driver.Should().NotBeNull();

        var result = driver.GetRunResult();

        if (expectedDiagnosticIds is null)
        {
            result
                .Diagnostics
                .Should()
                .BeEmpty(
                    "code should be generated without errors, but found:\n"
                    + string.Join(
                        "\n---\n",
                        result.Diagnostics.Select(e =>
                            $"  - {e.Id}: {e.GetMessage()} at {e.Location}")));
        }
        else
        {
            result
                .Diagnostics
                .Select(diagnostic => diagnostic.Id)
                .Should()
                .BeEquivalentTo(expectedDiagnosticIds);
            result
                .Diagnostics
                .Should()
                .OnlyContain(diagnostic => diagnostic.Severity != DiagnosticSeverity.Error);
        }

        // Reparse generated trees with the same parse options as the original compilation
        // to ensure consistent syntax tree features (e.g., InterceptorsNamespaces)
        var parseOptions = originalCompilation.SyntaxTrees.First().Options;
        var reparsedTrees = result
            .GeneratedTrees
            .Select(tree => CSharpSyntaxTree.ParseText(
                tree.GetText(),
                (CSharpParseOptions)parseOptions,
                tree.FilePath))
            .ToArray();

        // Add generated trees to original compilation
        var outputCompilation = originalCompilation.AddSyntaxTrees(reparsedTrees);

        var errors = outputCompilation
            .GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToList();

        errors
            .Should()
            .BeEmpty(
                "generated code should compile without errors, but found:\n"
                + string.Join(
                    "\n",
                    errors.Select(e => $"  - {e.Id}: {e.GetMessage()} at {e.Location}")));

        if (expectedTrees > -1)
            result.GeneratedTrees.Length.Should().Be(expectedTrees);

        return Verifier
            .Verify(driver)
            .UseDirectory("Snapshots")
            .DisableDiff()
            .ScrubLinesWithReplace(line =>
            {
                // replace
                // [global::System.CodeDom.Compiler.GeneratedCode("MinimalLambda.SourceGenerators",
                // "0.0.0")]
                if (line.Contains(
                    "global::System.CodeDom.Compiler.GeneratedCode",
                    StringComparison.Ordinal))
                    return RegexHelper.GeneratedCodeAttributeRegex().Replace(line, "REPLACED");

                // replace [InterceptsLocation(1, "")]
                if (line.Contains("InterceptsLocation", StringComparison.Ordinal))
                    return RegexHelper.InterceptsLocationRegex().Replace(line, "REPLACED");

                return line;
            });
    }

    [RequiresAssemblyFiles("Calls System.Reflection.Assembly.Location")]
    internal static (GeneratorDriver driver, Compilation compilation) GenerateFromSource(
        string source,
        Dictionary<string, ReportDiagnostic>? diagnosticsToSuppress = null,
        LanguageVersion languageVersion = LanguageVersion.CSharp14,
        bool includeDurableReferences = false,
        IReadOnlyList<(string FilePath, string Source)>? additionalSources = null,
        bool treatWarningsAsErrors = false)
    {
        IEnumerable<KeyValuePair<string, string>> features =
        [
            new("InterceptorsNamespaces", "MinimalLambda"),
        ];

        var parseOptions = CSharpParseOptions
            .Default
            .WithLanguageVersion(languageVersion)
            .WithFeatures(features);

        var syntaxTrees = new List<SyntaxTree>
        {
            CSharpSyntaxTree.ParseText(source, parseOptions, "InputFile.cs"),
        };
        if (additionalSources is not null)
            syntaxTrees.AddRange(
                additionalSources.Select(item =>
                    CSharpSyntaxTree.ParseText(item.Source, parseOptions, item.FilePath)));

        List<MetadataReference> references =
        [
#if NET11_0_OR_GREATER
            .. Net110.References.All.ToList(),
#elif NET10_0
            .. Net100.References.All.ToList(),
#elif NET9_0
            .. Net90.References.All.ToList(),
#else
            .. Net80.References.All.ToList(),
#endif
            MetadataReference.CreateFromFile(typeof(LambdaApplication).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(FromKeyedServicesAttribute).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(ILambdaContext).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(IHost).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(HostBuilder).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(DefaultLambdaJsonSerializer).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(LambdaBootstrapBuilder).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(IOptions<>).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(ILambdaInvocationContext).Assembly.Location),
        ];

#if NET10_0_OR_GREATER
        if (includeDurableReferences)
        {
            references.Add(
                MetadataReference.CreateFromFile(
                    typeof(MinimalLambda.DurableExecution.DurableContextExtensions).Assembly
                        .Location));
            references.Add(
                MetadataReference.CreateFromFile(typeof(IDurableContext).Assembly.Location));
        }
#endif

        var compilationOptions = new CSharpCompilationOptions(
            OutputKind.ConsoleApplication,
            nullableContextOptions: NullableContextOptions.Enable,
            generalDiagnosticOption: treatWarningsAsErrors
                ? ReportDiagnostic.Error
                : ReportDiagnostic.Default);

        if (diagnosticsToSuppress is not null)
            compilationOptions =
                compilationOptions.WithSpecificDiagnosticOptions(diagnosticsToSuppress);

        var compilation = CSharpCompilation.Create(
            "Tests",
            syntaxTrees,
            references,
            compilationOptions);

        var generator = new MinimalLambdaGenerator().AsSourceGenerator();

        var driver = CSharpGeneratorDriver.Create(generator);
        var updatedDriver = driver.RunGenerators(compilation, CancellationToken.None);

        return (updatedDriver, compilation);
    }
}

internal static partial class RegexHelper
{
    [GeneratedRegex("""(\d+\.\d+\.\d+\.\d+)""", RegexOptions.None, "en-US")]
    internal static partial Regex GeneratedCodeAttributeRegex();

    [GeneratedRegex(
        """(?<=\[InterceptsLocation\(\d+, ")([A-Za-z0-9+/=]{2,})(?="\)\])""",
        RegexOptions.None,
        "en-US")]
    internal static partial Regex InterceptsLocationRegex();
}
