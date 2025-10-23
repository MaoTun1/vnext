using Microsoft.AspNetCore.Http;
using System.Diagnostics;

namespace BBT.Workflow.HttpApi.Shared.Middlewares;

/// <summary>
/// Middleware that adds OpenTelemetry trace context to HTTP response headers.
/// This allows clients to correlate responses with distributed traces.
/// </summary>
public sealed class TraceContextMiddleware
{
    private readonly RequestDelegate _next;

    /// <summary>
    /// Initializes a new instance of the TraceContextMiddleware.
    /// </summary>
    /// <param name="next">The next middleware in the pipeline</param>
    public TraceContextMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    /// <summary>
    /// Processes an HTTP request and adds trace context to the response headers.
    /// </summary>
    /// <param name="context">The HTTP context</param>
    /// <returns>A task representing the asynchronous operation</returns>
    public async Task InvokeAsync(HttpContext context)
    {
        // Get the current activity (span)
        var activity = Activity.Current;

        // Add trace context to response headers if available
        if (activity != null)
        {
            // Add TraceId (W3C format: 32 hex characters)
            if (activity.TraceId != default)
            {
                context.Response.Headers["X-Trace-Id"] = activity.TraceId.ToString();
            }

            // Add SpanId (W3C format: 16 hex characters)
            if (activity.SpanId != default)
            {
                context.Response.Headers["X-Span-Id"] = activity.SpanId.ToString();
            }

            // Add TraceState if present (optional W3C header)
            if (!string.IsNullOrEmpty(activity.TraceStateString))
            {
                context.Response.Headers["X-Trace-State"] = activity.TraceStateString;
            }

            // Add W3C Trace Context standard header (traceparent)
            // Format: version-traceId-spanId-flags
            var traceParent = $"00-{activity.TraceId}-{activity.SpanId}-{(activity.ActivityTraceFlags.HasFlag(ActivityTraceFlags.Recorded) ? "01" : "00")}";
            context.Response.Headers["traceparent"] = traceParent;
        }

        // Call the next middleware in the pipeline
        await _next(context);
    }
}

