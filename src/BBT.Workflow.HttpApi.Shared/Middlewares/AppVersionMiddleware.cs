using BBT.Workflow.Versioning;
using Microsoft.AspNetCore.Http;

namespace BBT.Workflow.Middlewares;

/// <summary>
/// Middleware that adds the X-App-Version header to every HTTP response.
/// Registered early in the pipeline (before exception handler) to ensure
/// the header is present on both success and error responses.
/// </summary>
public sealed class AppVersionMiddleware(RequestDelegate next, IAppVersionProvider versionProvider)
{
    private const string HEADER_NAME = "X-App-Version";

    /// <summary>
    /// Adds the X-App-Version header and invokes the next middleware.
    /// </summary>
    public Task InvokeAsync(HttpContext context)
    {
        context.Response.Headers[HEADER_NAME] = versionProvider.GetVersion();
        return next(context);
    }
}
