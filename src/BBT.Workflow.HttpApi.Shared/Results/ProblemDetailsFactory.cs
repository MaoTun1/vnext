using System.Diagnostics;
using System.Net;
using BBT.Aether.AspNetCore.ExceptionHandling;
using BBT.Aether.Http;
using BBT.Workflow.Domain;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace BBT.Workflow.HttpApi.Shared;

/// <summary>
/// Centralized factory for creating RFC 7807 compliant ProblemDetails.
/// Ensures consistent error response format across exception handling and result pattern.
/// Uses Aether's AetherExceptionHttpStatusCodeOptions for consistent status code resolution.
/// </summary>
public sealed class ProblemDetailsFactory
{
    private readonly AetherExceptionHttpStatusCodeOptions _statusCodeOptions;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProblemDetailsFactory"/> class.
    /// </summary>
    /// <param name="statusCodeOptions">Aether's HTTP exception status code options</param>
    public ProblemDetailsFactory(IOptions<AetherExceptionHttpStatusCodeOptions> statusCodeOptions)
    {
        _statusCodeOptions = statusCodeOptions.Value;
    }

    /// <summary>
    /// Creates ProblemDetails from a domain Error (Result pattern).
    /// </summary>
    /// <param name="error">The domain error</param>
    /// <returns>RFC 7807 compliant ProblemDetails</returns>
    public ProblemDetails CreateFromError(Error error)
    {
        // Resolve status code from configured options, default to BadRequest if not mapped
        var statusCode = GetStatusCodeForErrorCode(error.Code);
        var title = GetTitleFromStatusCode(statusCode);
        var problemDetails = new ProblemDetails
        {
            Type = $"{WorkflowErrorCodes.ErrorUri}/{error.Code.Replace('.', '/').Replace(':', '/')}",
            Title = title,
            Status = (int)statusCode,
            Detail = error.Message ?? error.Detail,
            Extensions =
            {
                ["code"] = error.Code,
                ["traceId"] = Activity.Current?.Id ?? string.Empty
            }
        };

        // Add target if available
        if (!string.IsNullOrEmpty(error.Target))
        {
            problemDetails.Extensions["target"] = error.Target;
        }

        // Add validation errors at root level (RFC 7807 common practice)
        if (error.ValidationErrors is { Count: > 0 })
        {
            problemDetails.Extensions["validationErrors"] = error.ValidationErrors
                .Select(ve => new ServiceValidationErrorInfo
                {
                    Members = ve.MemberNames.ToArray(),
                    Message = ve.ErrorMessage ?? string.Empty
                })
                .ToList();
        }

        return problemDetails;
    }
    
    /// <summary>
    /// Gets the HTTP status code for the given error code from configuration.
    /// Defaults to InternalServerError (500) if no mapping is found.
    /// </summary>
    /// <param name="errorCode">The error code to look up</param>
    /// <returns>The mapped HTTP status code or InternalServerError if not found</returns>
    private HttpStatusCode GetStatusCodeForErrorCode(string errorCode)
    {
        if (_statusCodeOptions.ErrorCodeToHttpStatusCodeMappings.TryGetValue(errorCode, out var status))
        {
            return status;
        }
        
        // Default to InternalServerError if no mapping found
        return HttpStatusCode.InternalServerError;
    }
    
    private static string GetTitleFromStatusCode(HttpStatusCode statusCode)
    {
        return statusCode switch
        {
            HttpStatusCode.BadRequest => "Bad Request",
            HttpStatusCode.Unauthorized => "Unauthorized",
            HttpStatusCode.Forbidden => "Forbidden",
            HttpStatusCode.NotFound => "Not Found",
            HttpStatusCode.Conflict => "Conflict",
            HttpStatusCode.UnprocessableEntity => "Unprocessable Entity",
            HttpStatusCode.InternalServerError => "Internal Server Error",
            HttpStatusCode.BadGateway => "Bad Gateway",
            HttpStatusCode.ServiceUnavailable => "Service Unavailable",
            _ => statusCode.ToString()
        };
    }
}
