using Microsoft.Extensions.Options;

namespace MinimalLambda.Options;

internal sealed class EventHandlerOptionsValidator : IValidateOptions<EventHandlerOptions>
{
    public ValidateOptionsResult Validate(string? name, EventHandlerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return options.MaxConcurrentExecutions < 1
            ? ValidateOptionsResult.Fail(
                $"{nameof(EventHandlerOptions)}.{nameof(EventHandlerOptions.MaxConcurrentExecutions)} must be greater than zero.")
            : ValidateOptionsResult.Success;
    }
}
