using BBT.Workflow.Domain;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace BBT.Workflow.HttpApi.Shared;

/// <summary>
/// Maps Result types to ASP.NET Core IResult and ProblemDetails (RFC 7807 compliant).
/// Provides consistent error responses across all API endpoints.
/// </summary>
public static class ProblemDetailsMapper
{
    /// <summary>
    /// Gets or sets the error code status mapping configuration.
    /// Defaults to <see cref="ErrorCodeStatusMapping.Default"/>.
    /// Can be customized for application-specific error handling.
    /// </summary>
    public static ErrorCodeStatusMapping StatusMapping { get; set; } = ErrorCodeStatusMapping.Default;
    /// <summary>
    /// Converts a non-generic Result to an HTTP response.
    /// Success returns 204 No Content, failure returns ProblemDetails.
    /// </summary>
    public static IResult ToHttpResult(this Result result)
        => result.IsSuccess ? Results.NoContent() : Results.Problem(result.ToProblemDetails());

    /// <summary>
    /// Converts a Result&lt;T&gt; to an HTTP response with custom success handling.
    /// </summary>
    /// <typeparam name="T">The value type</typeparam>
    /// <param name="result">The result to convert</param>
    /// <param name="onSuccess">Optional function to transform the value for response</param>
    /// <param name="statusCode">HTTP status code for success (default: 200 OK)</param>
    public static IResult ToHttpResult<T>(
        this Result<T> result, 
        Func<T, object>? onSuccess = null, 
        int statusCode = StatusCodes.Status200OK)
        => result.IsSuccess 
            ? Results.Json(onSuccess is null ? result.Value : onSuccess(result.Value!), statusCode: statusCode)
            : Results.Problem(result.ToProblemDetails());

    /// <summary>
    /// Converts a non-generic Result to ProblemDetails.
    /// </summary>
    public static ProblemDetails ToProblemDetails(this Result result)
        => ToProblemDetails(result.Error);

    /// <summary>
    /// Converts a Result&lt;T&gt; to ProblemDetails.
    /// </summary>
    public static ProblemDetails ToProblemDetails<T>(this Result<T> result)
        => ToProblemDetails(result.Error);

    /// <summary>
    /// Converts an Error to RFC 7807 compliant ProblemDetails.
    /// Maps error codes to appropriate HTTP status codes using the configured <see cref="StatusMapping"/>.
    /// </summary>
    public static ProblemDetails ToProblemDetails(this Error error)
    {
        // Use the configurable mapping instead of hard-coded switch
        var (status, title) = StatusMapping.GetMapping(error.Code);

        var problemDetails = new ProblemDetails
        {
            Title = title,
            Detail = error.Message ?? error.Detail,
            Status = status,
            Type = $"https://errors.vnext.io/{error.Code.Replace('.', '/')}",
            Extensions = 
            { 
                ["code"] = error.Code, 
                ["target"] = error.Target ?? string.Empty,
                ["traceId"] = System.Diagnostics.Activity.Current?.Id ?? string.Empty
            }
        };

        // Include validation errors if available
        if (error.ValidationErrors is { Count: > 0 })
        {
            problemDetails.Extensions["errors"] = error.ValidationErrors
                .Select(ve => new
                {
                    field = ve.MemberNames.FirstOrDefault() ?? string.Empty,
                    message = ve.ErrorMessage
                })
                .ToList();
        }

        return problemDetails;
    }

    /// <summary>
    /// Creates an ActionResult from a Result for use in MVC controllers.
    /// </summary>
    public static ActionResult ToActionResult(this Result result)
        => result.IsSuccess 
            ? new NoContentResult() 
            : new ObjectResult(result.ToProblemDetails()) 
            { 
                StatusCode = result.ToProblemDetails().Status 
            };

    /// <summary>
    /// Creates an IActionResult from a Result&lt;T&gt; for use in MVC controllers.
    /// </summary>
    public static IActionResult ToActionResult<T>(this Result<T> result)
        => result.IsSuccess 
            ? new OkObjectResult(result.Value!) 
            : new ObjectResult(result.ToProblemDetails()) 
            { 
                StatusCode = result.ToProblemDetails().Status 
            };

    /// <summary>
    /// Creates a CreatedResult from a Result&lt;T&gt; with location.
    /// </summary>
    public static IResult ToCreatedResult<T>(
        this Result<T> result, 
        string location, 
        Func<T, object>? selector = null)
        => result.IsSuccess 
            ? Results.Created(location, selector is null ? result.Value : selector(result.Value!))
            : Results.Problem(result.ToProblemDetails());
}

