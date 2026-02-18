using BBT.Workflow.Versioning;
using Microsoft.AspNetCore.Http;

namespace BBT.Workflow.Middlewares;

/// <summary>
/// Middleware that adds the X-App-Version header to every HTTP response.
/// Uses OnStarting callback to ensure the header is present even on error responses.
/// </summary>
public sealed class AppVersionMiddleware(RequestDelegate next, IAppVersionProvider versionProvider)
{
    /// <summary>
    /// Adds the X-App-Version header and invokes the next middleware.
    /// </summary>
    public Task InvokeAsync(HttpContext context)
    {
        context.Response.OnStarting(() =>
        {
            context.Response.Headers["X-App-Version"] = versionProvider.GetVersion();
            return Task.CompletedTask;
        });

        return next(context);
    }
}
