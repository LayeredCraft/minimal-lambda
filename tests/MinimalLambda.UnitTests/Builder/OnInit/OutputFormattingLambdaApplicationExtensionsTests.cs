using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MinimalLambda.UnitTests.Application.Extensions;

[TestSubject(typeof(OutputFormattingLambdaApplicationExtensions))]
public class OutputFormattingLambdaApplicationExtensionsTests
{
    private static IHost CreateHostWithServices()
    {
        var builder = new LambdaApplicationBuilder(new LambdaApplicationOptions());
        builder.Logging.SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.None);

        return builder.Build();
    }

    [Fact]
    public void OnInitClearLambdaOutputFormatting_WithNullApplication_ThrowsArgumentNullException()
    {
        // Arrange
        ILambdaOnInitBuilder? application = null;

        // Act
        var act = () => application!.OnInitClearLambdaOutputFormatting();

        // Assert
        act.Should().ThrowExactly<ArgumentNullException>();
    }

    [Fact]
    public void OnInitClearLambdaOutputFormatting_WithValidApplication_ReturnsBuilder()
    {
        // Arrange
        var host = CreateHostWithServices();
        var app = new LambdaApplication(host);

        // Act
        var result = app.OnInitClearLambdaOutputFormatting();

        // Assert
        result.Should().Be(app);
    }

    [Fact]
    public void OnInitClearLambdaOutputFormatting_AddsInitHandler()
    {
        // Arrange
        var host = CreateHostWithServices();
        var app = new LambdaApplication(host);

        // Act
        app.OnInitClearLambdaOutputFormatting();

        // Assert
        app.InitHandlers.Should().HaveCount(1);
    }

    [Fact]
    public void OnInitClearLambdaOutputFormatting_EnablesMethodChaining()
    {
        // Arrange
        var host = CreateHostWithServices();
        var app = new LambdaApplication(host);

        // Act
        var result = app.OnInitClearLambdaOutputFormatting().OnInitClearLambdaOutputFormatting();

        // Assert
        result.Should().Be(app);
        app.InitHandlers.Should().HaveCount(2);
    }

    [Fact]
    public async Task OnInitClearLambdaOutputFormatting_InitHandlerSucceeds()
    {
        // Arrange
        var host = CreateHostWithServices();
        var app = new LambdaApplication(host);
        app.OnInitClearLambdaOutputFormatting();

        // Act
        var builder = (ILambdaOnInitBuilder)app;
        var buildResult = builder.Build();
        var result = await buildResult!(CancellationToken.None);

        // Assert
        result.Should().BeTrue();
    }
}
