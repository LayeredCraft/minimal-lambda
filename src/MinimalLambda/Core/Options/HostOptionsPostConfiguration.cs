using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace MinimalLambda.Options;

internal class HostOptionsPostConfiguration : IPostConfigureOptions<HostOptions>
{
    private readonly LambdaHostOptions _lambdaHostOptions;

    public HostOptionsPostConfiguration(IOptions<LambdaHostOptions> lambdaHostOptions)
    {
        ArgumentNullException.ThrowIfNull(lambdaHostOptions);

        _lambdaHostOptions = lambdaHostOptions.Value;
    }

    public void PostConfigure(string? name, HostOptions options)
    {
        var shutdownTimeout = _lambdaHostOptions.ShutdownDuration
            - _lambdaHostOptions.ShutdownDurationBuffer;

        options.ShutdownTimeout =
            shutdownTimeout >= TimeSpan.Zero ? shutdownTimeout : TimeSpan.Zero;
    }
}
