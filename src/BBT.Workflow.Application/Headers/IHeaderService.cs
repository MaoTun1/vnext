namespace BBT.Workflow.Headers;

/// <summary>
/// Provides functionality for managing HTTP headers within the workflow context.
/// </summary>
/// <remarks>
/// This service allows adding and retrieving custom headers that can be used
/// across the workflow execution pipeline.
/// </remarks>
public interface IHeaderService
{
    /// <summary>
    /// Adds a custom header with the specified key and value.
    /// </summary>
    /// <param name="key">The header key. Must not be null or whitespace.</param>
    /// <param name="value">The header value. Must not be null.</param>
    /// <remarks>
    /// If the key already exists, the value will be updated.
    /// Invalid keys or null values will be ignored silently.
    /// </remarks>
    /// <example>
    /// <code>
    /// headerService.AddHeader("X-User-Id", "12345");
    /// headerService.AddHeader("X-Tenant-Id", "tenant-abc");
    /// </code>
    /// </example>
    void AddHeader(string key, string value);

    /// <summary>
    /// Retrieves all currently stored headers as a read-only dictionary.
    /// </summary>
    /// <returns>
    /// A read-only dictionary containing all headers, or null if no headers exist.
    /// Returns an empty dictionary if the service is not properly initialized.
    /// </returns>
    /// <remarks>
    /// The returned dictionary is read-only to prevent external modification.
    /// Changes should be made through the AddHeader method.
    /// </remarks>
    /// <example>
    /// <code>
    /// var headers = headerService.GetAllHeaders();
    /// if (headers != null && headers.ContainsKey("X-User-Id"))
    /// {
    ///     var userId = headers["X-User-Id"];
    /// }
    /// </code>
    /// </example>
    IReadOnlyDictionary<string, string>? GetAllHeaders();
}