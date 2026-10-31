#if !DEBUG
using System.Reflection;
using MinimalLambda.Builder;

namespace MinimalLambda.UnitTests.Builder.InterceptionTargets;

public class MapEventHandlerLambdaApplicationExtensionsTests
{
    // Debug.Fail in the stub contract FailFasts under a Debug build, so this executes in Release
    // only.
    [Fact]
    public void StubFailsClearlyWhenInterceptionDoesNotOccur()
    {
        // Arrange
        var application = Substitute.For<ILambdaInvocationBuilder>();
        var split = (StubBatch batch, ILambdaInvocationContext _) => batch.Items;
        Func<StubItem, Task> handle = _ => Task.CompletedTask;
        var stub = typeof(MapEventHandlerLambdaApplicationExtensions)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(method =>
                method.Name == "MapEventHandler"
                && method.IsGenericMethodDefinition
                && method.GetGenericArguments().Length == 2);
        var closed = stub.MakeGenericMethod(typeof(StubBatch), typeof(StubItem));

        // Act
        var act = () => closed.Invoke(null, [application, split, handle]);

        // Assert
        act
            .Should()
            .Throw<TargetInvocationException>()
            .WithInnerException<InvalidOperationException>()
            .WithMessage("*compile time*");
    }

    private sealed record StubBatch(IReadOnlyList<StubItem> Items);

    private sealed record StubItem(int Id);
}
#endif
