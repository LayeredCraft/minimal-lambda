using System.Reflection;
using System.Runtime.CompilerServices;
using MinimalLambda.Builder;

namespace MinimalLambda.DurableExecution.UnitTests;

public class PublicApiTests
{
    [Fact]
    public void MapDurableHandler_HasExpectedPublicExtensionSignature()
    {
        // Act
        var method = typeof(MapDurableHandlerLambdaApplicationExtensions).GetMethod(
            "MapDurableHandler",
            BindingFlags.Public | BindingFlags.Static);

        // Assert
        typeof(MapDurableHandlerLambdaApplicationExtensions)
            .Namespace
            .Should()
            .Be("MinimalLambda.Builder");
        typeof(MapDurableHandlerLambdaApplicationExtensions)
            .Assembly
            .GetName()
            .Name
            .Should()
            .Be("MinimalLambda.DurableExecution");
        method.Should().NotBeNull();
        method!.ReturnType.Should().Be<ILambdaInvocationBuilder>();
        method.IsDefined(typeof(ExtensionAttribute), false).Should().BeTrue();
        method
            .GetParameters()
            .Select(parameter => parameter.ParameterType)
            .Should()
            .Equal(typeof(ILambdaInvocationBuilder), typeof(Delegate));
    }

    [Fact]
    public void GetInvocationContext_HasExpectedPublicExtensionSignature()
    {
        // Act
        var method = typeof(DurableContextExtensions).GetMethod(
            "GetInvocationContext",
            BindingFlags.Public | BindingFlags.Static);

        // Assert
        typeof(DurableContextExtensions).Namespace.Should().Be("MinimalLambda.DurableExecution");
        typeof(DurableContextExtensions)
            .Assembly
            .GetName()
            .Name
            .Should()
            .Be("MinimalLambda.DurableExecution");
        method.Should().NotBeNull();
        method!.ReturnType.Should().Be<ILambdaInvocationContext>();
        method.IsDefined(typeof(ExtensionAttribute), false).Should().BeTrue();
        method
            .GetParameters()
            .Select(parameter => parameter.ParameterType)
            .Should()
            .Equal(typeof(IDurableContext));
    }
}
