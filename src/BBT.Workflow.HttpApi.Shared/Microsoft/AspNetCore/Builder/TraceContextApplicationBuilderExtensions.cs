using BBT.Workflow.HttpApi.Shared.Middlewares;

namespace Microsoft.AspNetCore.Builder;

/// <summary>
/// Extension methods for adding trace context middleware to the application pipeline.
/// </summary>
public static class TraceContextApplicationBuilderExtensions
{
    /// <summary>
    /// Adds middleware to include OpenTelemetry trace context in HTTP response headers.
    /// This enables clients to correlate responses with distributed traces using X-Trace-Id and X-Span-Id headers.
    /// </summary>
    /// <param name="app">The application builder</param>
    /// <returns>The application builder for chaining</returns>
    /// <remarks>
    /// This middleware should be added early in the pipeline to ensure trace context is available
    /// for all requests. It adds the following headers to responses:
    /// <list type="bullet">
    /// <item><description>X-Trace-Id: The W3C trace ID (32 hex characters)</description></item>
    /// <item><description>X-Span-Id: The W3C span ID (16 hex characters)</description></item>
    /// <item><description>traceparent: W3C standard trace context header</description></item>
    /// <item><description>X-Trace-State: Optional trace state information</description></item>
    /// </list>
    /// </remarks>
    public static IApplicationBuilder UseTraceContext(this IApplicationBuilder app)
    {
        return app.UseMiddleware<TraceContextMiddleware>();
    }
}

