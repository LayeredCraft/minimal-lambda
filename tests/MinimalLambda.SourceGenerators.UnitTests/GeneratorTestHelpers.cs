using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using Amazon.Lambda.Core;
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
    internal static Task Verify(string source, int expectedTrees = -1)
    {
        var (driver, originalCompilation) = GenerateFromSource(source);

        driver.Should().NotBeNull();

        var result = driver.GetRunResult();

        result
            .Diagnostics
            .Should()
            .BeEmpty(
                "code should be generated without errors, but found:\n"
                + string.Join(
                    "\n---\n",
                    result.Diagnostics.Select(e =>
                        $"  - {e.Id}: {e.GetMessage()} at {e.Location}")));

        // Reparse generated trees with the same parse options as the original compilation
        // to ensure consistent syntax tree features (e.g., InterceptorsNamespaces)
        var parseOptions = originalCompilation.SyntaxTrees.First().Options;
        var reparsedTrees = result
            .GeneratedTrees
            .Select(tree =>
                CSharpSyntaxTree.ParseText(tree.GetText(), (CSharpParseOptions)parseOptions))
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
        LanguageVersion languageVersion = LanguageVersion.CSharp14)
    {
        IEnumerable<KeyValuePair<string, string>> features =
        [
            new("InterceptorsNamespaces", "MinimalLambda"),
        ];

        var parseOptions = CSharpParseOptions
            .Default
            .WithLanguageVersion(languageVersion)
            .WithFeatures(features);

        var syntaxTree = CSharpSyntaxTree.ParseText(source, parseOptions, "InputFile.cs");

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

        var compilationOptions = new CSharpCompilationOptions(
            OutputKind.ConsoleApplication,
            nullableContextOptions: NullableContextOptions.Enable);

        if (diagnosticsToSuppress is not null)
            compilationOptions =
                compilationOptions.WithSpecificDiagnosticOptions(diagnosticsToSuppress);

        var compilation = CSharpCompilation.Create(
            "Tests",
            [syntaxTree],
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
