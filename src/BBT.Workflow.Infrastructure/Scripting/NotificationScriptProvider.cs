using BBT.Aether.Results;
using BBT.Workflow.Scripting;
using Microsoft.Extensions.Logging;

namespace BBT.Workflow.Infrastructure.Scripting;

/// <summary>
/// Provides notification-specific embedded scripts through the generic embedded script provider.
/// Acts as a thin adapter that encapsulates notification script key constants.
/// </summary>
/// <remarks>
/// This implementation delegates to <see cref="IEmbeddedScriptProvider"/> using well-known
/// script keys for notification tasks, providing a cleaner domain-specific API.
/// </remarks>
public sealed class NotificationScriptProvider : INotificationScriptProvider
{
    /// <summary>
    /// The script key for the default notification input mapping script.
    /// </summary>
    internal const string DefaultKey = "notification.default";

    private readonly IEmbeddedScriptProvider _embeddedScriptProvider;
    private readonly ILogger<NotificationScriptProvider> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="NotificationScriptProvider"/> class.
    /// </summary>
    /// <param name="embeddedScriptProvider">The generic embedded script provider.</param>
    /// <param name="logger">Logger for diagnostics and error reporting.</param>
    public NotificationScriptProvider(
        IEmbeddedScriptProvider embeddedScriptProvider,
        ILogger<NotificationScriptProvider> logger)
    {
        _embeddedScriptProvider = embeddedScriptProvider;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result<string>> GetDefaultScriptAsync(CancellationToken cancellationToken = default)
    {
        var result = await _embeddedScriptProvider.GetAsync(DefaultKey, cancellationToken);

        if (!result.IsSuccess)
        {
            _logger.LogError(
                "Failed to load default notification script: {ErrorMessage}",
                result.Error.Message);
        }

        return result;
    }
}

