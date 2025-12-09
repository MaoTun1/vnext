using BBT.Aether.Results;

namespace BBT.Workflow.Scripting;

/// <summary>
/// Provides access to notification-specific embedded scripts.
/// Acts as a domain-specific adapter over <see cref="IEmbeddedScriptProvider"/>.
/// </summary>
/// <remarks>
/// This interface abstracts the script key details from notification task implementations,
/// providing a cleaner API for accessing notification-related scripts.
/// </remarks>
public interface INotificationScriptProvider
{
    /// <summary>
    /// Retrieves the default input mapping script for notification tasks.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>
    /// A <see cref="Result{T}"/> containing the script content on success,
    /// or an error if the script cannot be loaded.
    /// </returns>
    Task<Result<string>> GetDefaultScriptAsync(CancellationToken cancellationToken = default);
}

