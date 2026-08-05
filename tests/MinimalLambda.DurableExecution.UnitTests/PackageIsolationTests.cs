using System.Reflection;
using System.Xml.Linq;
using MinimalLambda.Builder;

namespace MinimalLambda.DurableExecution.UnitTests;

public class PackageIsolationTests
{
    private const string AwsDurableAssemblyName = "Amazon.Lambda.DurableExecution";

    [Fact]
    public void CoreAssemblyReferenceGraph_DoesNotReferenceAwsDurableExecution()
    {
        // Arrange
        var coreAssemblies = MinimalLambdaReferenceGraph(typeof(LambdaApplication).Assembly);

        // Act
        var references = coreAssemblies.SelectMany(assembly => assembly.GetReferencedAssemblies());

        // Assert
        references.Should().NotContain(reference => reference.Name == AwsDurableAssemblyName);
    }

    [Fact]
    public void DurableAssembly_ReferencesAwsDurableExecution()
    {
        // Act
        var references = typeof(DurableContextExtensions).Assembly.GetReferencedAssemblies();

        // Assert
        references.Should().ContainSingle(reference => reference.Name == AwsDurableAssemblyName);
    }

    [Fact]
    public void CoreProjectMetadata_DoesNotDependOnAwsDurableExecution()
    {
        // Act
        var references = PackageReferences("src/MinimalLambda/MinimalLambda.csproj");

        // Assert
        references.Should().NotContain(AwsDurableAssemblyName);
    }

    [Fact]
    public void DurableProjectMetadata_DependsOnAwsDurableExecution()
    {
        // Act
        var references = PackageReferences(
            "src/MinimalLambda.DurableExecution/MinimalLambda.DurableExecution.csproj");

        // Assert
        references.Should().Contain(AwsDurableAssemblyName);
    }

    private static IReadOnlyCollection<Assembly> MinimalLambdaReferenceGraph(Assembly root)
    {
        var assemblies =
            new Dictionary<string, Assembly>(StringComparer.Ordinal)
            {
                [root.GetName().Name!] = root,
            };
        var pending = new Queue<Assembly>();
        pending.Enqueue(root);

        while (pending.TryDequeue(out var assembly))
            foreach (var reference in assembly
                .GetReferencedAssemblies()
                .Where(reference =>
                    reference.Name?.StartsWith("MinimalLambda", StringComparison.Ordinal) == true))
            {
                if (assemblies.ContainsKey(reference.Name!))
                    continue;

                var referencedAssembly = Assembly.Load(reference);
                assemblies.Add(reference.Name!, referencedAssembly);
                pending.Enqueue(referencedAssembly);
            }

        return assemblies.Values;
    }

    private static string[] PackageReferences(string relativeProjectPath)
    {
        var project = XDocument.Load(Path.Combine(RepositoryRoot(), relativeProjectPath));
        return project
            .Descendants("PackageReference")
            .Select(element => element.Attribute("Include")?.Value)
            .Where(value => value is not null)
            .Cast<string>()
            .ToArray();
    }

    private static string RepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
            directory is not null;
            directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "MinimalLambda.sln")))
                return directory.FullName;

        throw new InvalidOperationException("Repository root was not found.");
    }
}
