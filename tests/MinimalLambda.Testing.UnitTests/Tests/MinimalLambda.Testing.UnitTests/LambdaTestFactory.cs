using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace MinimalLambda.Testing.UnitTests;

internal static class LambdaTestFactory
{
    private const string LambdaLogLevelEnvironmentVariable = "AWS_LAMBDA_LOG_LEVEL";

    static LambdaTestFactory()
    {
        if (string.IsNullOrEmpty(
            Environment.GetEnvironmentVariable(LambdaLogLevelEnvironmentVariable)))
            Environment.SetEnvironmentVariable(LambdaLogLevelEnvironmentVariable, "Critical");
    }

    public static LambdaApplicationFactory<TEntryPoint> Create<TEntryPoint>()
        where TEntryPoint : class =>
        new LambdaApplicationFactory<TEntryPoint>().WithHostBuilder(builder =>
        {
            builder.ConfigureServices((_, services) =>
            {
                services.RemoveAll<ILoggerFactory>();
                services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
            });
        });
}
