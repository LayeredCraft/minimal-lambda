using System.Reflection;

namespace MinimalLambda.DurableExecution.UnitTests;

public class PackageAssemblyTests
{
    [Fact]
    public void DurablePackageAssembly_IsAvailable()
    {
        // Act
        var assembly = Assembly.Load("MinimalLambda.DurableExecution");

        // Assert
        assembly.GetName().Name.Should().Be("MinimalLambda.DurableExecution");
    }

    [Fact]
    public void AwsDurableExecutionSurface_IsAvailable()
    {
        // Act
        var publicTypes = new[]
        {
            typeof(DurableFunction),
            typeof(IDurableContext),
            typeof(DurableExecutionInvocationInput),
            typeof(DurableExecutionInvocationOutput),
        };

        // Assert
        publicTypes
            .Should()
            .AllSatisfy(type =>
                type.Assembly.GetName().Name.Should().Be("Amazon.Lambda.DurableExecution"));
    }
}
