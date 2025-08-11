namespace BBT.Workflow.Headers;

/// <summary>
/// HTTP context-based implementation of <see cref="IHeaderService"/> that stores headers
/// in the current HTTP request context.
/// </summary>
/// <remarks>
/// This implementation uses <see cref="IHttpContextAccessor"/> to access the current HTTP context
/// and stores headers in the <see cref="Microsoft.AspNetCore.Http.HttpContext.Items"/> collection.
/// Headers are maintained for the duration of the HTTP request.
/// </remarks>
/// <param name="httpContextAccessor">The HTTP context accessor for accessing the current request context.</param>
internal sealed class HttpContextHeaderService(IHttpContextAccessor httpContextAccessor) : IHeaderService
{
    /// <summary>
    /// The key used to store headers in the HTTP context items collection.
    /// </summary>
    /// <value>The constant value "WorkflowHeaders".</value>
    internal const string HeaderKey = "WorkflowHeaders";

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">
    /// This exception is not thrown but the method will silently fail if no HTTP context is available.
    /// </exception>
    public void AddHeader(string key, string value)
    {
        if (string.IsNullOrWhiteSpace(key) || value == null)
            return;

        var httpContext = httpContextAccessor.HttpContext;
        if (httpContext == null) return;
        
        if (!httpContext.Items.ContainsKey(HeaderKey))
        {
            httpContext.Items[HeaderKey] = new Dictionary<string, string>();
        }

        var headers = (Dictionary<string, string>)httpContext.Items[HeaderKey]!;
        headers[key] = value;
    }

    /// <inheritdoc />
    /// <exception cref="InvalidCastException">
    /// Thrown if the stored object in HTTP context cannot be cast to the expected dictionary type.
    /// This should not occur under normal circumstances.
    /// </exception>
    public IReadOnlyDictionary<string, string>? GetAllHeaders()
    {
        var httpContext = httpContextAccessor.HttpContext;
        if (httpContext == null)
            return new Dictionary<string, string>();

        if (!httpContext.Items.TryGetValue(HeaderKey, out var obj))
        {
            return new Dictionary<string, string>();
        }

        return obj as IReadOnlyDictionary<string, string>;
    }
}