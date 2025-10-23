using System.Diagnostics;
using System.Net;
using System.Text.Json;
using BBT.Aether.AspNetCore.ExceptionHandling;
using BBT.Aether.ExceptionHandling;
using BBT.Aether.Http;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;

namespace BBT.Workflow.ExceptionHandling;

/// <summary>
/// Exception handler that converts exceptions to RFC 7807 ProblemDetails format.
/// </summary>
public sealed class ProblemDetailsExceptionHandler
    : IExceptionHandler
{
    internal const string ErrorFormat = "_vnext_error_format";
    private readonly Func<object, Task> _clearCacheHeadersDelegate;
    private readonly ILogger<ProblemDetailsExceptionHandler> _logger;

    public ProblemDetailsExceptionHandler(
        ILogger<ProblemDetailsExceptionHandler> logger)
    {
        _logger = logger;
        _clearCacheHeadersDelegate = ClearCacheHeaders;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        await HandleAndWrapException(httpContext, exception, cancellationToken);

        // Return true to indicate the exception was handled
        return true;
    }

    private async Task HandleAndWrapException(HttpContext httpContext, Exception exception,
        CancellationToken cancellationToken)
    {
        _logger.LogException(exception);
        var errorInfoConverter = httpContext.RequestServices.GetRequiredService<IExceptionToErrorInfoConverter>();
        var statusCodeFinder = httpContext.RequestServices.GetRequiredService<IHttpExceptionStatusCodeFinder>();
        var exceptionHandlingOptions = httpContext.RequestServices
            .GetRequiredService<IOptions<AetherExceptionHandlingOptions>>().Value;

        var statusCode = statusCodeFinder.GetStatusCode(httpContext, exception);

        var errorInfo = errorInfoConverter.Convert(exception, options =>
        {
            options.SendExceptionsDetailsToClients =
                exceptionHandlingOptions.SendExceptionsDetailsToClients;
            options.SendStackTraceToClients = exceptionHandlingOptions.SendStackTraceToClients;
        });

        httpContext.Response.Clear();
        httpContext.Response.StatusCode = statusCode.GetHashCode();
        httpContext.Response.OnStarting(_clearCacheHeadersDelegate, httpContext.Response);
        httpContext.Response.Headers.Append(ErrorFormat, "true");
        httpContext.Response.Headers.Append("Content-Type", "application/json");

        await httpContext.Response.WriteAsync(
            JsonSerializer.Serialize(
                CreateFromServiceErrorInfo(errorInfo, statusCode),
                new JsonSerializerOptions() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true }
            ), cancellationToken: cancellationToken);
    }

    private ProblemDetails CreateFromServiceErrorInfo(ServiceErrorInfo errorInfo, HttpStatusCode statusCode)
    {
        var errorCode = errorInfo.Code ?? "unknown";
        var problemDetails = new ProblemDetails
        {
            Type = $"{WorkflowErrorCodes.ErrorUri}/{errorCode.Replace('.', '/').Replace(':', '/')}",
            Title = statusCode.ToString(),
            Status = statusCode.GetHashCode(),
            Detail = errorInfo.Message,
            Extensions =
            {
                ["code"] = errorCode,
                ["traceId"] = Activity.Current?.Id ?? string.Empty
            }
        };

        // Add validation errors at root level (RFC 7807 common practice)
        if (errorInfo.ValidationErrors is { Length: > 0 })
        {
            problemDetails.Extensions["validationErrors"] = errorInfo.ValidationErrors;
        }

        // Collect other additional data under "data" property for better structure
        var dataCollection = new Dictionary<string, object?>();

        // Add additional data from ServiceErrorInfo
        if (errorInfo.Data is IDictionary<string, object> { Count: > 0 } dataDict)
        {
            foreach (var kvp in dataDict)
            {
                dataCollection[kvp.Key] = kvp.Value;
            }
        }

        if (errorInfo.Details is not null)
        {
            dataCollection["details"] = errorInfo.Details;
        }

        // Add the data collection to extensions if it contains any items
        if (dataCollection.Count > 0)
        {
            problemDetails.Extensions["data"] = dataCollection;
        }

        return problemDetails;
    }

    private Task ClearCacheHeaders(object state)
    {
        var response = (HttpResponse)state;

        response.Headers[HeaderNames.CacheControl] = "no-cache";
        response.Headers[HeaderNames.Pragma] = "no-cache";
        response.Headers[HeaderNames.Expires] = "-1";
        response.Headers.Remove(HeaderNames.ETag);

        return Task.CompletedTask;
    }
}