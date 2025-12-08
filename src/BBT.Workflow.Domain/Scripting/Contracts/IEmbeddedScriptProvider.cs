using BBT.Aether.Results;

namespace BBT.Workflow.Scripting;

/// <summary>
/// Provides access to embedded script resources from assembly manifest resources.
/// Scripts are loaded lazily and cached in memory for optimal performance.
/// </summary>
/// <remarks>
/// This provider is designed to be registered as a singleton service.
/// All scripts are loaded from embedded resources and cached on first access,
/// ensuring minimal I/O overhead during runtime execution.
/// </remarks>
public interface IEmbeddedScriptProvider
{
    /// <summary>
    /// Retrieves an embedded script by its logical key.
    /// </summary>
    /// <param name="scriptKey">
    /// The logical key identifying the script (e.g., "notification.input.default").
    /// Keys are case-insensitive.
    /// </param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>
    /// A <see cref="Result{T}"/> containing the script content on success,
    /// or an error if the script key is invalid, not configured, or cannot be loaded.
    /// </returns>
    /// <example>
    /// <code>
    /// var result = await provider.GetAsync("notification.input.default", cancellationToken);
    /// if (result.IsSuccess)
    /// {
    ///     var scriptCode = result.Value;
    ///     // Use the script code
    /// }
    /// </code>
    /// </example>
    Task<Result<string>> GetAsync(string scriptKey, CancellationToken cancellationToken = default);
}

