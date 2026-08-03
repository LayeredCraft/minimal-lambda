using System.Xml.Linq;

namespace MinimalLambda.DurableExecution.UnitTests;

public class DocumentationContractTests
{
    private static readonly XDocument Documentation = XDocument.Load(
        Path.Combine(AppContext.BaseDirectory, "MinimalLambda.DurableExecution.xml"));

    [Fact]
    public void GetInvocationContext_HasRequiredXmlDocumentation()
    {
        // Arrange
        const string memberId =
            "M:MinimalLambda.DurableExecution.DurableContextExtensions.GetInvocationContext(Amazon.Lambda.DurableExecution.IDurableContext)";

        // Act
        var documentation = ResolveDocumentation(memberId);

        // Assert
        Text(documentation, "summary")
            .Should()
            .Be(
                "Gets the MinimalLambda invocation context associated with this durable execution.");
        Text(documentation, "remarks")
            .Should()
            .Contain("physical invocation context")
            .And
            .Contain("creates a new physical Lambda invocation for a replay")
            .And
            .Contain("using it to cancel the root workflow can produce a terminal durable failure")
            .And
            .Contain("Prefer cancellation tokens supplied to durable operation callbacks.");
        Text(documentation, "returns").Should().Contain("Exact instance stored in .");
        ExceptionTypes(documentation)
            .Should()
            .BeEquivalentTo("T:System.ArgumentNullException", "T:System.InvalidOperationException");
    }

    [Fact]
    public void MapDurableHandler_HasRequiredXmlDocumentation()
    {
        // Arrange
        const string memberId =
            "M:MinimalLambda.Builder.MapDurableHandlerLambdaApplicationExtensions.MapDurableHandler(MinimalLambda.Builder.ILambdaInvocationBuilder,System.Delegate)";

        // Act
        var documentation = ResolveDocumentation(memberId);

        // Assert
        Text(documentation, "summary")
            .Should()
            .Be(
                "Registers an AWS Lambda Durable Execution handler with automatic dependency injection and serialization.");
        Text(documentation, "remarks")
            .Should()
            .Contain("A compile-time interceptor must replace this call;")
            .And
            .Contain("can optionally declare a")
            .And
            .Contain("returns")
            .And
            .Contain("Middleware runs again when AWS replays a workflow")
            .And
            .Contain(
                "AWS owns durable context creation, checkpoints, replay, suspension, and durable status mapping")
            .And
            .Contain("MinimalLambda owns physical invocation hosting")
            .And
            .Contain("Use cancellation tokens supplied to durable operation callbacks");
        Text(documentation, "param")
            .Should()
            .Contain(
                "Durable handler delegate that will be intercepted and replaced at compile time");
        documentation.Element("param")?.Attribute("name")?.Value.Should().Be("handler");
        Text(documentation, "returns").Should().Contain("Current instance for method chaining.");
        ExceptionTypes(documentation).Should().Equal("T:System.InvalidOperationException");
    }

    private static XElement ResolveDocumentation(string memberId)
    {
        var publicMember = Documentation
            .Descendants("member")
            .Single(element => element.Attribute("name")?.Value == memberId);
        var inheritedMemberId = publicMember.Element("inheritdoc")?.Attribute("cref")?.Value;

        inheritedMemberId.Should().NotBeNullOrWhiteSpace();
        return Documentation
            .Descendants("member")
            .Single(element => element.Attribute("name")?.Value == inheritedMemberId);
    }

    private static string Text(XElement member, string elementName) =>
        string.Join(
            " ",
            member.Element(elementName)!.Value.Split(
                (char[]?)null,
                StringSplitOptions.RemoveEmptyEntries));

    private static IEnumerable<string> ExceptionTypes(XElement member) =>
        member.Elements("exception").Select(element => element.Attribute("cref")!.Value);
}
