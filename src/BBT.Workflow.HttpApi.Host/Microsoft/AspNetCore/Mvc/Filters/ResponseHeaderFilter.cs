using BBT.Workflow.Headers;

namespace Microsoft.AspNetCore.Mvc.Filters;

public sealed class ResponseHeaderFilter(IHeaderService headerService) : IActionFilter
{
    public void OnActionExecuting(ActionExecutingContext context)
    {
        // No-op
    }

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