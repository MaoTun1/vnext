using BBT.Workflow.Headers;

namespace Microsoft.AspNetCore.Mvc.Filters;

/// <summary>
/// Action filter that adds custom headers to HTTP responses based on workflow context
/// </summary>
/// <param name="headerService">Service for managing workflow headers</param>
public sealed class ResponseHeaderFilter(IHeaderService headerService) : IActionFilter
{
    /// <summary>
    /// Called before the action method is invoked - no operation needed
    /// </summary>
    /// <param name="context">The action executing context</param>
    public void OnActionExecuting(ActionExecutingContext context)
    {
        // No-op
    }

    /// <summary>
    /// Called after the action method is invoked - adds workflow headers to response
    /// </summary>
    /// <param name="context">The action executed context</param>
    public void OnActionExecuted(ActionExecutedContext context)
    {
        if (context.HttpContext.Response.HasStarted)
            return;

        var headers = headerService.GetAllHeaders();
        if (headers != null)
        {
            foreach (var header in headers)
            {
                context.HttpContext.Response.Headers[header.Key] = header.Value;
            }
        }

        context.HttpContext.Items.Remove(HttpContextHeaderService.HeaderKey);
    }
} 