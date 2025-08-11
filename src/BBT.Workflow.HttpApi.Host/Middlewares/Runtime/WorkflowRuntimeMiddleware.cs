namespace BBT.Workflow.Runtime;

/// <summary>
/// Middleware that adds runtime information to HTTP response headers.
/// This middleware automatically injects server information including runtime version
/// and domain into the 'Server' header of all HTTP responses.
/// </summary>
/// <param name="runtimeInfoProvider">Provider for runtime information such as version and domain</param>
public sealed class WorkflowRuntimeMiddleware(IRuntimeInfoProvider runtimeInfoProvider): IMiddleware
{
    /// <summary>
    /// Invokes the middleware to process the HTTP request and add runtime information to the response.
    /// Adds a 'Server' header containing the runtime version and domain information before
    /// passing control to the next middleware in the pipeline.
    /// </summary>
    /// <param name="context">The HTTP context for the current request</param>
    /// <param name="next">The next delegate in the middleware pipeline</param>
    /// <returns>A task representing the asynchronous middleware operation</returns>
    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        context.Response.OnStarting(() =>
        {
            context.Response.Headers["Server"] =  $"amorphie-runtime/{runtimeInfoProvider.Version} ({runtimeInfoProvider.Domain})";;
            return Task.CompletedTask;
        });   
        await next(context);
    }
}