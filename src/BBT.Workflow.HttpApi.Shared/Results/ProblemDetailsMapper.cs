using BBT.Workflow.Domain;
using BBT.Workflow.Instances;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace BBT.Workflow.HttpApi.Shared;

/// <summary>
/// Maps Result types to ASP.NET Core IResult and ProblemDetails (RFC 7807 compliant).
/// Provides consistent error responses across all API endpoints.
/// Uses centralized ProblemDetailsFactory for consistent error formatting.
/// </summary>
public static class ProblemDetailsMapper
{
    private static ProblemDetailsFactory? _factory;
    
    /// <summary>
    /// Sets the ProblemDetailsFactory to use for creating ProblemDetails.
    /// This should be called during application startup.
    /// </summary>
    internal static void Configure(ProblemDetailsFactory factory)
    {
        _factory = factory;
    }
    
    private static ProblemDetailsFactory GetFactory()
    {
        if (_factory is null)
        {
            throw new InvalidOperationException(
                "ProblemDetailsMapper has not been configured. " +
                "Ensure UseWorkflowApiBase is called during application startup.");
        }
        
        return _factory;
    }
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
    /// Uses centralized ProblemDetailsFactory for consistent error formatting.
    /// </summary>
    public static ProblemDetails ToProblemDetails(this Error error)
    {
        return GetFactory().CreateFromError(error);
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
    /// Creates an IActionResult from a ConditionalResult&lt;T&gt; for use in MVC controllers.
    /// Handles 304 Not Modified status for conditional requests.
    /// </summary>
    public static IActionResult ToActionResult<T>(this ConditionalResult<T> conditionalResult)
    {
        if (conditionalResult.IsNotModified)
        {
            return new StatusCodeResult(StatusCodes.Status304NotModified);
        }

        return conditionalResult.Result.ToActionResult();
    }

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

