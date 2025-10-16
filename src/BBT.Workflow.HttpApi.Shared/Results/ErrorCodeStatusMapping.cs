using Microsoft.AspNetCore.Http;

namespace BBT.Workflow.HttpApi.Shared;

/// <summary>
/// Configurable mapping between error code patterns and HTTP status codes.
/// Allows customization of status code responses for specific error codes.
/// </summary>
public sealed class ErrorCodeStatusMapping
{
    private readonly Dictionary<string, (int StatusCode, string Title)> _exactMatches = new();
    private readonly List<(string Prefix, int StatusCode, string Title)> _prefixMatches = new();

    /// <summary>
    /// Gets the default mapping configuration.
    /// </summary>
    public static ErrorCodeStatusMapping Default { get; } = CreateDefault();

    /// <summary>
    /// Adds an exact error code match with its status code and title.
    /// </summary>
    /// <param name="errorCode">The exact error code to match</param>
    /// <param name="statusCode">The HTTP status code to return</param>
    /// <param name="title">The problem details title</param>
    public ErrorCodeStatusMapping AddExactMatch(string errorCode, int statusCode, string title)
    {
        _exactMatches[errorCode] = (statusCode, title);
        return this;
    }

    /// <summary>
    /// Adds a prefix-based error code match with its status code and title.
    /// </summary>
    /// <param name="prefix">The error code prefix to match (e.g., "validation.", "notfound.")</param>
    /// <param name="statusCode">The HTTP status code to return</param>
    /// <param name="title">The problem details title</param>
    public ErrorCodeStatusMapping AddPrefixMatch(string prefix, int statusCode, string title)
    {
        _prefixMatches.Add((prefix, statusCode, title));
        return this;
    }

    /// <summary>
    /// Gets the status code and title for a given error code.
    /// First checks exact matches, then prefix matches, then returns default.
    /// </summary>
    /// <param name="errorCode">The error code to look up</param>
    /// <returns>A tuple containing the status code and title</returns>
    public (int StatusCode, string Title) GetMapping(string errorCode)
    {
        // 1. Check exact matches first
        if (_exactMatches.TryGetValue(errorCode, out var exactMatch))
        {
            return exactMatch;
        }

        // 2. Check prefix matches (in order of registration)
        foreach (var (prefix, statusCode, title) in _prefixMatches)
        {
            if (errorCode.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return (statusCode, title);
            }
        }

        // 3. Return default
        return (StatusCodes.Status500InternalServerError, "Internal Server Error");
    }

    /// <summary>
    /// Creates the default mapping configuration with standard error prefixes.
    /// </summary>
    private static ErrorCodeStatusMapping CreateDefault()
    {
        var mapping = new ErrorCodeStatusMapping();

        // Standard prefix-based mappings (order matters - more specific first)
        mapping.AddPrefixMatch(WorkflowErrorCodes.Prefixes.Validation, 
            StatusCodes.Status400BadRequest, 
            "Validation Failed");

        mapping.AddPrefixMatch(WorkflowErrorCodes.Prefixes.NotFound, 
            StatusCodes.Status404NotFound, 
            "Resource Not Found");

        mapping.AddPrefixMatch(WorkflowErrorCodes.Prefixes.Conflict, 
            StatusCodes.Status409Conflict, 
            "Conflict");

        mapping.AddPrefixMatch(WorkflowErrorCodes.Prefixes.Unauthorized, 
            StatusCodes.Status401Unauthorized, 
            "Unauthorized");

        mapping.AddPrefixMatch(WorkflowErrorCodes.Prefixes.Forbidden, 
            StatusCodes.Status403Forbidden, 
            "Forbidden");

        mapping.AddPrefixMatch(WorkflowErrorCodes.Prefixes.Auth, 
            StatusCodes.Status401Unauthorized, 
            "Authentication Failed");

        mapping.AddPrefixMatch(WorkflowErrorCodes.Prefixes.Transient, 
            StatusCodes.Status503ServiceUnavailable, 
            "Service Temporarily Unavailable");

        mapping.AddPrefixMatch(WorkflowErrorCodes.Prefixes.Dependency, 
            StatusCodes.Status502BadGateway, 
            "Dependency Error");

        mapping.AddPrefixMatch(WorkflowErrorCodes.Prefixes.Failure, 
            StatusCodes.Status500InternalServerError, 
            "Operation Failed");

        // Specific workflow error code overrides (optional examples)
        mapping.AddExactMatch(WorkflowErrorCodes.SubFlowBlocked, 
            StatusCodes.Status409Conflict, 
            "SubFlow Blocking Transition");

        mapping.AddExactMatch(WorkflowErrorCodes.TransitionLocked, 
            StatusCodes.Status409Conflict, 
            "Transition Already In Progress");

        mapping.AddExactMatch(WorkflowErrorCodes.UnauthorizedTransition, 
            StatusCodes.Status403Forbidden, 
            "Transition Not Authorized");

        return mapping;
    }

    /// <summary>
    /// Creates a custom mapping configuration builder.
    /// </summary>
    public static ErrorCodeStatusMapping CreateCustom()
    {
        return new ErrorCodeStatusMapping();
    }
}

