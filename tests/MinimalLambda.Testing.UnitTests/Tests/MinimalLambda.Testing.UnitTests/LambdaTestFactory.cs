using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace MinimalLambda.Testing.UnitTests;

internal static class LambdaTestFactory
{
    private const string LambdaLogLevelEnvironmentVariable = "AWS_LAMBDA_LOG_LEVEL";

    private static readonly KeyValuePair<string, string?>[] HostConfiguration =
    [
        new("Logging:LogLevel:Default", "None"),
    ];

    static LambdaTestFactory()
    {
        if (string.IsNullOrEmpty(
            Environment.GetEnvironmentVariable(LambdaLogLevelEnvironmentVariable)))
            Environment.SetEnvironmentVariable(LambdaLogLevelEnvironmentVariable, "Critical");
    }

    public static LambdaApplicationFactory<TEntryPoint> Create<TEntryPoint>()
        where TEntryPoint : class =>
        new LambdaApplicationFactory<TEntryPoint>().WithHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, configuration) =>
                configuration.AddInMemoryCollection(HostConfiguration)));
}
