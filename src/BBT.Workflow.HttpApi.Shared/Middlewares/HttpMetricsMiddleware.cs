using BBT.Workflow.Monitoring;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace BBT.Workflow.Middlewares;

/// <summary>
/// Middleware that automatically records comprehensive HTTP metrics for all requests.
/// This middleware captures request duration, response size, error rates, and other HTTP metrics.
/// </summary>
public sealed class HttpMetricsMiddleware(
    RequestDelegate next,
    IWorkflowMetrics workflowMetrics,
    ILogger<HttpMetricsMiddleware> logger)
{
    /// <summary>
    /// Processes HTTP request and records comprehensive metrics
    /// </summary>
    public async Task InvokeAsync(HttpContext context)
    {
        var stopwatch = Stopwatch.StartNew();
        var method = context.Request.Method;
        var endpoint = GetNormalizedEndpoint(context.Request.Path);
        
        // Capture original response body stream
        var originalBodyStream = context.Response.Body;
        
        try
        {
            // Use a memory stream to capture response size
            using var responseBody = new MemoryStream();
            context.Response.Body = responseBody;

            // Process the request
            await next(context);
            
            stopwatch.Stop();
            var statusCode = context.Response.StatusCode.ToString();
            var responseSize = responseBody.Length;
            
            // Copy response back to original stream
            responseBody.Seek(0, SeekOrigin.Begin);
            await responseBody.CopyToAsync(originalBodyStream);
            
            // Record successful request metrics
            workflowMetrics.RecordHttpRequest(method, endpoint, statusCode);
            workflowMetrics.RecordHttpRequestDuration(method, endpoint, statusCode, stopwatch.Elapsed.TotalSeconds);
            workflowMetrics.RecordHttpResponseSize(method, endpoint, statusCode, responseSize);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            context.Response.Body = originalBodyStream; // Restore original stream
            
            var statusCode = context.Response.StatusCode.ToString();
            var errorType = ex.GetType().Name;
            
            // Record HTTP error metrics
            workflowMetrics.RecordHttpRequest(method, endpoint, statusCode);
            workflowMetrics.RecordHttpError(method, endpoint, errorType);
            workflowMetrics.RecordHttpRequestDuration(method, endpoint, statusCode, stopwatch.Elapsed.TotalSeconds);
            
            // Record workflow error and exception metrics
            workflowMetrics.RecordWorkflowError("system", "high", "HttpMiddleware");
            workflowMetrics.RecordWorkflowException(errorType, "HttpMiddleware", $"{method} {endpoint}");
            
            logger.LogWarning(ex, "HTTP request failed: {Method} {Endpoint} with {ErrorType} after {Duration}ms",
                method, endpoint, errorType, stopwatch.ElapsedMilliseconds);
            
            throw;
        }
    }

    /// <summary>
    /// Normalizes endpoint paths to reduce cardinality for metrics.
    /// Replaces path parameters with placeholders to prevent metric explosion.
    /// </summary>
    private static string GetNormalizedEndpoint(PathString path)
    {
        var pathValue = path.Value ?? "/";
        
        // Skip metrics endpoint to avoid self-monitoring
        if (pathValue.Equals("/metrics", StringComparison.OrdinalIgnoreCase))
        {
            return "/metrics";
        }
        
        // Skip health checks
        if (pathValue.StartsWith("/health", StringComparison.OrdinalIgnoreCase))
        {
            return "/health";
        }
        
        // Normalize API paths by replacing common variable patterns
        return NormalizeApiPath(pathValue);
    }

    /// <summary>
    /// Normalizes API paths by replacing path parameters with placeholders
    /// to prevent metric cardinality explosion.
    /// </summary>
    private static string NormalizeApiPath(string path)
    {
        // Common patterns to normalize
        var patterns = new[]
        {
            // Instance IDs (GUIDs)
            (@"/instances/[0-9a-fA-F-]{36}", "/instances/{instanceId}"),
            
            // Workflow/domain names (replace specific names with placeholder)
            (@"/api/v[\d\.]+/([^/]+)/workflows/([^/]+)", "/api/v{version}/{domain}/workflows/{workflow}"),
            
            // Version patterns  
            (@"/api/v[\d\.]+", "/api/v{version}"),
            
            // Generic GUID patterns
            (@"/[0-9a-fA-F-]{36}", "/{id}"),
            
            // Numeric IDs
            (@"/\d+", "/{id}")
        };

        var normalizedPath = path;
        foreach (var (pattern, replacement) in patterns)
        {
            normalizedPath = System.Text.RegularExpressions.Regex.Replace(
                normalizedPath, 
                pattern, 
                replacement, 
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        }
        
        return normalizedPath;
    }
}