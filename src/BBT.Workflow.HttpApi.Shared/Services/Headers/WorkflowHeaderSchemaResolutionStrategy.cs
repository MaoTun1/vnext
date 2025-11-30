using BBT.Aether.AspNetCore.MultiSchema;
using Microsoft.AspNetCore.Http;

namespace BBT.Workflow.Headers;

public class WorkflowHeaderSchemaResolutionStrategy : ISchemaResolutionStrategy
{
    /// <inheritdoc />
    public string? TryResolve(HttpContext httpContext)
    {
        var info = httpContext.GetWorkflowInfo();
        if (info is null)
        {
            return null;
        }

        return string.IsNullOrWhiteSpace(info.Workflow) ? null : info.Workflow;
    }
}